namespace MovieProcessor
open System
open System.Collections.Concurrent
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
          tags: IDictionary<string, ConcurrentHashSet<Movie>>
          moviesById: IDictionary<int, Movie>
          peopleById: IDictionary<int, Person> }

    let inline dictByField fieldFactory collectionIter =
        let dictionary = ConcurrentDictionary<_, _>()
        collectionIter (fun r -> dictionary.TryAdd(fieldFactory r, r) |> ignore)
        dictionary

    let inline groupByField fieldFactory collectionIter =
        let allFields = collectionIter fieldFactory |> Seq.distinct
        let result = ConcurrentDictionary<_, _>()
        Seq.iter (fun f -> result.TryAdd(f, ConcurrentHashSet<_>()) |> ignore) allFields
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
            |> FileParser.fileMap splitActorsDirectors
            
        let ratingsImdb =
            Path.Combine [|path; "Ratings_IMDB.tsv"|]
            |> FileParser.fileIter splitRatings
            
        let linksImdbMovieLens =
            Path.Combine [|path; "links_IMDB_MovieLens.csv"|]
            |> FileParser.fileIter splitLinks
            
        let movieCodes =
            Path.Combine [|path; "MovieCodes_IMDB.tsv"|]
            |> FileParser.fileMap splitMovieCodes
            
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
        let movies = ConcurrentDictionary<int, Movie>(Environment.ProcessorCount, 980000)
        // NM ID to Person
        let people = ConcurrentDictionary<int, Person>(Environment.ProcessorCount, 1500000)
        // Tags
        let tags = ConcurrentDictionary<string, ConcurrentHashSet<Movie>>(Environment.ProcessorCount, 2000)

        let ruEn = movieCodes |> Seq.filter (fun r -> r.region = "RU" || r.region = "US" || r.region = "GB" || r.region = "AU")
        let ruEnImdbIds = ConcurrentHashSet<_>()

        // Create movie with empty tags and no rating
        logger.info "Loading movies"
        for row in ruEn do
            let imdbId = row.titleId
            ruEnImdbIds.Add imdbId |> ignore
            let movie = Movie(imdbId, row.title, ConcurrentHashSet<Tag>())
            movies.TryAdd(imdbId, movie) |> ignore

        // Set rating
        logger.info "Loading movie ratings"
        ratingsImdb (fun row ->
            let movie = tryGet movies row.tconst
            let rating = row.averageRating |> float32
            movie |> Option.iter (_.SetRating(rating)))
            
        // Set tags
        // TODO: Filter by imdbId in ru/en
        logger.info "Loading movie tags"
        tagScores (fun row ->
            let imdbId = row.movieId |> tryGet imdbOfMl |> Option.map (_.imdbId)
            let movie = imdbId |> Option.bind (tryGet movies)
            let tag = tryGet tagCodesDict row.tagId |> Option.map (_.tag)
            let scoreValid = (float32 row.relevance) > 0.5f
            if scoreValid then
                movie |> Option.iter (fun movie ->
                tag |> Option.iter (fun tag ->
                tags.TryAdd(tag, ConcurrentHashSet<Movie>()) |> ignore
                tags[tag].Add movie |> ignore
                movie.AddTag tag |> ignore))
            else ())
        
        logger.info "Calculating relevant actors and directors ids"

        // Create people
        logger.info "Loading people"
        actorsDirectorsNames (fun row ->
            let person = Person(row.nconst, row.primaryName)
            people.TryAdd(row.nconst, person) |> ignore)

        // Link people to movies
        logger.info "Linking people and movies"
        actorsDirectorsCodes (fun row ->
            tryGet movies row.tconst |> Option.iter (fun movie ->
            tryGet people row.nconst |> Option.iter (fun person ->
            match row.category with
            | 'a' | 's' ->
                movie.AddActor person |> ignore
                person.AddMovie movie |> ignore
            | 'd' ->
                movie.AddDirector person |> ignore
                person.AddMovie movie |> ignore
            | _ -> ())))

        let moviesByName = Seq.map (fun (KeyValue(_, movie : Movie)) -> movie.GetTitle(), movie) movies |> dict
        let peopleByName = Seq.map (fun (KeyValue(_, person : Person)) -> person.GetPrimaryName(), person) people |> dict

        waitForAllTasks()

        { movies = moviesByName; people = peopleByName; tags = tags; moviesById = movies; peopleById = people}