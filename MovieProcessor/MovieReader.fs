namespace MovieProcessor
open System
open System.Collections.Generic
open System.IO
open FSharp.Data
open MovieProcessor.FileParser
open MovieProcessor.Logger
open MovieProcessor.Movie

module MovieLoader =

    type Dataset =
        { movies: IDictionary<string, Movie>
          people: IDictionary<string, Person>
          tags: IDictionary<string, HashSet<Movie>>
          moviesById: IDictionary<int, Movie>
          peopleById: IDictionary<int, Person> }

    let inline dictByField fieldFactory collectionIter =
        collectionIter (fun r -> (fieldFactory r, r)) |> dict

    let inline groupByField fieldFactory collectionIter =
        let allFields = collectionIter fieldFactory |> Seq.distinct
        let result = Dictionary<_, _>()
        Seq.iter (fun f -> result.Add(f, HashSet<_>([]))) allFields
        collectionIter (fun entry -> result[fieldFactory entry].Add(entry)) |> ignore
        result

    let inline tryGet (dictionary : IDictionary<_, _>) key =
        match dictionary.TryGetValue key with
        | true, item -> Some item
        | false, _ -> None


    let load (path : string) (logger : ILogger) =
        if (Path.Exists(path) |> not) then raise <| ArgumentException($"Path {path} does not exist")

        let actorsDirectorsCodes =
            Path.Combine [|path; "ActorsDirectorsCodes_IMDB.tsv"|]
            |> FileParser.fileIter splitActorsDirectors

        let actorsDirectorsNames =
            Path.Combine [|path; "ActorsDirectorsNames_IMDB.txt"|]
            |> FileParser.fileIter splitActorsDirectorsNames
            
        let actorsDirectorsCodesCopy =
            Path.Combine [|path; "ActorsDirectorsCodes_IMDB.tsv"|]
            |> FileParser.fileIter splitActorsDirectors
            
        let ratingsImdb =
            Path.Combine [|path; "Ratings_IMDB.tsv"|]
            |> FileParser.fileIter splitRatings
            
        let linksImdbMovieLens =
            Path.Combine [|path; "links_IMDB_MovieLens.csv"|]
            |> FileParser.fileIter splitLinks
            
        let movieCodes =
            Path.Combine [|path; "MovieCodes_IMDB.tsv"|]
            |> FileParser.fileIter splitMovieCodes
            
        let tagCodes =
            Path.Combine [|path; "TagCodes_MovieLens.csv"|]
            |> FileParser.fileIter splitTagCodes
            
        let tagScores =
            Path.Combine [|path; "TagScores_MovieLens.csv"|]
            |> FileParser.fileIter splitTagScores
            
        // Dictionaries for fast loading
        logger.info "Organizing data into dictionaries"
        logger.info "Tag Codes"
        let tagCodesDict = dictByField (fun (x : tagCodesRow) -> x.tagId) tagCodes
        logger.info "Movie ID Links"
        let imdbOfMl = dictByField (fun (x : linksRow) -> x.movieId) linksImdbMovieLens

        // IMDB ID to Movie
        let movies = Dictionary<int, Movie>()
        // NM ID to Person
        let people = Dictionary<int, Person>()
        // Tags
        let tags = Dictionary<string, HashSet<Movie>>()

        let ruEn = movieCodes.Rows |> Seq.filter (fun r -> r.Region = "RU" || r.Region = "US" || r.Region = "GB" || r.Region = "AU")
        let ruEnImdbIds = HashSet<_>([])
        
        let intOfId = fun (t : string) -> t.Substring(2) |> int
        
        // Create movie with empty tags and no rating
        logger.info "Loading movies"
        for row in ruEn do
            let imdbId = row.TitleId
            ruEnImdbIds.Add imdbId |> ignore
            let movie = Movie(imdbId, row.Title, HashSet<Tag>([]))
            movies.TryAdd(intOfId imdbId, movie) |> ignore

        // Set rating
        logger.info "Loading movie ratings"
        for row in (ratingsImdb.Rows |> Seq.filter (fun x -> ruEnImdbIds.Contains(x.Tconst))) do
            let movie = tryGet movies (intOfId <| row.Tconst)
            let rating = row.AverageRating |> float32
            movie |> Option.iter (_.SetRating(rating))
            
        // Set tags
        // TODO: Filter by imdbId in ru/en
        logger.info "Loading movie tags"
        for row in tagScores.Rows do
            let imdbId = row.MovieId |> tryGet imdbOfMl |> Option.map (_.ImdbId)
            let movie = imdbId |> Option.map intOfId |> Option.bind (tryGet movies)
            let tag = tryGet tagCodesDict row.TagId |> Option.map (_.Tag)
            let scoreValid = (float32 row.Relevance) > 0.5f
            if scoreValid then
                movie |> Option.iter (fun movie ->
                tag |> Option.iter (fun tag ->
                tags.TryAdd(tag, HashSet<Movie>([])) |> ignore
                tags[tag].Add movie |> ignore
                movie.AddTag tag |> ignore))
            else ()
        
        logger.info "Calculating relevant actors and directors ids"
        let relevantActorsIds =
            actorsDirectorsCodesCopy.Rows
            |> Seq.filter (fun x -> ruEnImdbIds.Contains(x.Tconst))
            |> Seq.map (fun t -> t.Nconst |> intOfId)
            |> HashSet<_>

        // Create people
        logger.info "Loading people"
        for row in (actorsDirectorsNames.Rows |> Seq.filter (fun r -> relevantActorsIds.Contains(r.Nconst |> intOfId))) do
            let person = Person(row.Nconst, row.PrimaryName)
            people.TryAdd(intOfId row.Nconst, person) |> ignore

        // Link people to movies
        logger.info "Linking people and movies"
        for row in actorsDirectorsCodes.Rows |> Seq.filter (fun r -> relevantActorsIds.Contains(r.Nconst |> intOfId)) do
            tryGet movies (intOfId row.Tconst) |> Option.iter (fun movie ->
            tryGet people (intOfId row.Nconst) |> Option.iter (fun person ->
            match row.Category with
            | "actor" | "actress" | "self" ->
                movie.AddActor person |> ignore
                person.AddMovie movie |> ignore
            | "director" ->
                movie.AddDirector person |> ignore
                person.AddMovie movie |> ignore
            | _ -> ()))

        let moviesByName = Seq.map (fun (KeyValue(_, movie : Movie)) -> movie.GetTitle(), movie) movies |> dict
        let peopleByName = Seq.map (fun (KeyValue(_, person : Person)) -> person.GetPrimaryName(), person) people |> dict

        { movies = moviesByName; people = peopleByName; tags = tags; moviesById = movies; peopleById = people}