namespace MovieProcessor
open System
open System.Collections.Generic
open System.IO
open FSharp.Data
open MovieProcessor.Logger
open MovieProcessor.Movie

module MovieLoader =

    let inline dictByField fieldFactory rows =
        Seq.map (fun r -> (fieldFactory r, r)) rows |> dict

    let inline groupByField fieldFactory rows =
        let allFields = rows |> Seq.map fieldFactory |> Seq.distinct
        let result = Dictionary<_, _>()
        Seq.iter (fun f -> result.Add(f, HashSet<_>([]))) allFields
        for entry in rows do result[fieldFactory entry].Add(entry) |> ignore
        result

    let inline tryGet (dictionary : IDictionary<_, _>) key =
        match dictionary.TryGetValue key with
        | true, item -> Some item
        | false, _ -> None

    type TagCodesProvider = CsvProvider<"tagId,tag", IgnoreErrors=true, CacheRows=false>
    type ActorsDirectorsProvider = CsvProvider<"tconst	ordering	nconst	category	job	characters", IgnoreErrors=true, CacheRows=false>
    type ActorsDirectorsNamesProvider= CsvProvider<"nconst	primaryName	birthYear	deathYear	primaryProfession	knownForTitles", IgnoreErrors=true, CacheRows=false>
    type RatingsProvider = CsvProvider<"tconst	averageRating	numVotes", IgnoreErrors=true, CacheRows=false>
    type LinksProvider = CsvProvider<"movieId,imdbId,tmdbId", IgnoreErrors=true, CacheRows=false>
    type MovieCodesProvider = CsvProvider<"titleId	ordering	title	region	language	types	attributes	isOriginalTitle", IgnoreErrors=true, CacheRows=false>
    type TagScoresProvider = CsvProvider<"movieId,tagId,relevance", IgnoreErrors=true, CacheRows=false>

    let load (path : string) (logger : ILogger) =
        if (Path.Exists(path) |> not) then raise <| ArgumentException($"Path {path} does not exist")

        let actorsDirectorsCodes =
            Path.Combine [|path; "ActorsDirectorsCodes_IMDB.tsv"|]
            |> File.OpenRead
            |> ActorsDirectorsProvider.Load

        let actorsDirectorsNames =
            Path.Combine [|path; "ActorsDirectorsNames_IMDB.txt"|]
            |> File.OpenRead
            |> ActorsDirectorsNamesProvider.Load
            
        let actorsDirectorsCodesCopy =
            Path.Combine [|path; "ActorsDirectorsCodes_IMDB.tsv"|]
            |> File.OpenRead
            |> ActorsDirectorsProvider.Load
            
        let ratingsImdb =
            Path.Combine [|path; "Ratings_IMDB.tsv"|]
            |> File.OpenRead
            |> RatingsProvider.Load
            
        let linksImdbMovieLens =
            Path.Combine [|path; "links_IMDB_MovieLens.csv"|]
            |> File.OpenRead
            |> LinksProvider.Load
            
        let movieCodes =
            Path.Combine [|path; "MovieCodes_IMDB.tsv"|]
            |> File.OpenRead
            |> MovieCodesProvider.Load
            
        let tagCodes =
            Path.Combine [|path; "TagCodes_MovieLens.csv"|]
            |> File.OpenRead
            |> TagCodesProvider.Load
            
        let tagScores =
            Path.Combine [|path; "TagScores_MovieLens.csv"|]
            |> File.OpenRead
            |> TagScoresProvider.Load
            
        // Dictionaries for fast loading
        logger.info "Organizing data into dictionaries"
        logger.info "Tag Codes"
        let tagCodesDict = dictByField (fun (x : TagCodesProvider.Row) -> x.TagId) tagCodes.Rows
        logger.info "Movie ID Links"
        let imdbOfMl = dictByField (fun (x : LinksProvider.Row) -> x.MovieId) linksImdbMovieLens.Rows

        // IMDB ID to Movie
        let movies = Dictionary<int, Movie>()
        // NM ID to Person
        let people = Dictionary<int, Person>()

        let ruEn = movieCodes.Rows |> Seq.filter (fun r -> r.Region = "RU" || r.Region = "US" || r.Region = "GB" || r.Region = "AU")
        let ruEnImdbIds = HashSet<_>([])
        
        let intOfId = fun (t : string) -> t.Substring(2) |> int
        
        // Create movie with empty tags and no rating
        logger.info "Loading movies"
        for row in ruEn do
            let imdbId = row.TitleId
            ruEnImdbIds.Add imdbId |> ignore
            let movie = Movie(row.Title, HashSet<Tag>([]))
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
        for row in (tagScores.Rows) do
            let imdbId = row.MovieId |> tryGet imdbOfMl |> Option.map (_.ImdbId)
            let movie = imdbId |> Option.map intOfId |> Option.bind (tryGet movies)
            let tag = tryGet tagCodesDict row.TagId |> Option.map (_.Tag)
            let scoreValid = (float32 row.Relevance) > 0.5f
            if scoreValid then
                movie |> Option.iter (fun movie ->
                tag |> Option.iter (fun tag ->
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
            let person = Person(row.PrimaryName)
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
                movie.SetDirector person
                person.AddMovie movie |> ignore
            | _ -> ()))

        let moviesByName = Seq.map (fun (KeyValue(_, movie : Movie)) -> movie.GetTitle(), movie) movies |> dict
        let peopleByName = Seq.map (fun (KeyValue(_, person : Person)) -> person.GetPrimaryName(), person) people |> dict

        (moviesByName, peopleByName)