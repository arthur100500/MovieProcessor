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
            |> fileIter 1

        let actorsDirectorsNames =
            Path.Combine [|path; "ActorsDirectorsNames_IMDB.txt"|]
            |> fileIter 2
            
        let ratingsImdb =
            Path.Combine [|path; "Ratings_IMDB.tsv"|]
            |> fileIter 3
            
        let linksImdbMovieLens =
            Path.Combine [|path; "links_IMDB_MovieLens.csv"|]
            |> fileIter 4
            
        let movieCodes =
            Path.Combine [|path; "MovieCodes_IMDB.tsv"|]
            |> fileIter 5
            
        let tagCodes =
            Path.Combine [|path; "TagCodes_MovieLens.csv"|]
            |> fileIter 0
            
        let tagScores =
            Path.Combine [|path; "TagScores_MovieLens.csv"|]
            |> fileIter 6
            
        // Dictionaries for fast loading
        logger.info "Organizing data into dictionaries"
        logger.info "Tag Codes"
        let tagCodesDict = dictByField (fun (x : obj) -> (x :?> tagCodesRow).tagId) tagCodes
        logger.info "Movie ID Links"
        let imdbOfMl = dictByField (fun (x : obj) -> (x :?> linksRow).movieId) linksImdbMovieLens

        // IMDB ID to Movie
        let movies = ConcurrentDictionary<int, Movie>(Environment.ProcessorCount, 980000)
        // NM ID to Person
        let people = ConcurrentDictionary<int, Person>(Environment.ProcessorCount, 1500000)
        // Tags
        let tags = ConcurrentDictionary<string, ConcurrentHashSet<Movie>>(Environment.ProcessorCount, 2000)

        // Create movie with empty tags and no rating
        logger.info "Loading movies"
        movieCodes (fun row ->
            let row = row :?> movieCodesRow
            let imdbId = row.titleId
            let movie = Movie(imdbId, row.title, ConcurrentHashSet<Tag>())
            movies.TryAdd(imdbId, movie) |> ignore)

        // Set rating
        logger.info "Loading movie ratings"
        ratingsImdb (fun row ->
            let row = row :?> ratingsRow
            let movie = tryGet movies row.tconst
            let rating = row.averageRating |> float32
            movie |> Option.iter (_.SetRating(rating)))
            
        // Set tags
        // TODO: Filter by imdbId in ru/en
        logger.info "Loading movie tags"
        tagScores (fun row ->
            let row = row :?> tagScoresRow
            let imdbId = row.movieId |> tryGet imdbOfMl |> Option.map (fun x -> (x :?> linksRow).imdbId)
            let movie = imdbId |> Option.bind (tryGet movies)
            let tag = tryGet tagCodesDict row.tagId |> Option.map (fun x -> (x :?> tagCodesRow).tag)
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
            let row = row :?> actorsDirectorsNamesRow
            let person = Person(row.nconst, row.primaryName)
            people.TryAdd(row.nconst, person) |> ignore)

        // Link people to movies
        logger.info "Linking people and movies"
        actorsDirectorsCodes (fun row ->
            let row = row :?> actorsDirectorsRow
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

        { movies = moviesByName; people = peopleByName; tags = tags; moviesById = movies; peopleById = people}