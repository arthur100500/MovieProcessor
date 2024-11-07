namespace MovieProcessor
open System
open System.Collections.Generic
open System.IO
open DatabaseInteraction
open MovieProcessor.FileParser
open MovieProcessor.Logger

module MovieLoader =

    type Dataset =
        { movies: IDictionary<string, Movie>
          people: IDictionary<string, Person>
          tags: IDictionary<string, Tag>
          moviesById: IDictionary<int, Movie>
          peopleById: IDictionary<int, Person> }

    let inline dictByField fieldFactory collectionIter =
        let dictionary = Dictionary<_, _>()
        collectionIter (fun r -> dictionary.TryAdd(fieldFactory r, r) |> ignore)
        dictionary

    let inline groupByField fieldFactory collectionIter =
        let allFields = collectionIter fieldFactory |> Seq.distinct
        let result = Dictionary<_, _>()
        Seq.iter (fun f -> result.TryAdd(f, HashSet<_>()) |> ignore) allFields
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
            |> fileIter splitActorsDirectors

        let actorsDirectorsNames =
            Path.Combine [|path; "ActorsDirectorsNames_IMDB.txt"|]
            |> fileIter splitActorsDirectorsNames
            
        let actorsDirectorsCodesCopy =
            Path.Combine [|path; "ActorsDirectorsCodes_IMDB.tsv"|]
            |> fileMap splitActorsDirectors
            
        let ratingsImdb =
            Path.Combine [|path; "Ratings_IMDB.tsv"|]
            |> fileIter splitRatings
            
        let linksImdbMovieLens =
            Path.Combine [|path; "links_IMDB_MovieLens.csv"|]
            |> fileIter splitLinks
            
        let movieCodes =
            Path.Combine [|path; "MovieCodes_IMDB.tsv"|]
            |> fileMap splitMovieCodes
            
        let tagCodes =
            Path.Combine [|path; "TagCodes_MovieLens.csv"|]
            |> fileIter splitTagCodes
            
        let tagScores =
            Path.Combine [|path; "TagScores_MovieLens.csv"|]
            |> fileIter splitTagScores
            
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
        let tags = Dictionary<int, Tag>()

        let ruEn = movieCodes |> Seq.filter (fun r -> r.region = "RU" || r.region = "US" || r.region = "GB" || r.region = "AU")
        let ruEnImdbIds = HashSet<_>()

        let mutable latestId = -1
        let mutable latestMovie : Movie = null
        let mutable numericalId = 0
        let mutable personNumericalId = 0;

        // Create movie with empty tags and no rating
        logger.info "Loading movies"
        for row in ruEn do
            if latestId < row.titleId then
                numericalId <- numericalId + 1
                let imdbId = row.titleId
                ruEnImdbIds.Add imdbId |> ignore
                let movie = Movie(imdbId, row.title, HashSet<_>(), HashSet<_>(), HashSet<Tag>(), 0f, numericalId)
                movies.TryAdd(imdbId, movie) |> ignore
                latestId <- row.titleId
                latestMovie <- movie
            else
                latestMovie.AddTitle(row.title)

        // Set rating
        logger.info "Loading movie ratings"
        ratingsImdb (fun row ->
            let movie = tryGet movies row.tconst
            let rating = row.averageRating |> float32
            movie |> Option.iter (fun (x : Movie) -> x.Rating <- rating))
            
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
                tags.TryAdd(row.tagId, Tag(row.tagId, tag, HashSet<Movie>())) |> ignore
                tags[row.tagId].Movies.Add movie
                movie.Tags.Add (tags[row.tagId])))
            else ())
        
        logger.info "Calculating relevant actors and directors ids"

        // Create people
        logger.info "Loading people"
        actorsDirectorsNames (fun row ->
            personNumericalId <- personNumericalId + 1
            let person = Person(row.nconst, row.primaryName, HashSet<_>(), personNumericalId)
            people.TryAdd(row.nconst, person) |> ignore)

        // Link people to movies
        logger.info "Linking people and movies"
        actorsDirectorsCodes (fun row ->
            tryGet movies row.tconst |> Option.iter (fun movie ->
            tryGet people row.nconst |> Option.iter (fun person ->
            match row.category with
            | 'a' | 's' ->
                movie.Actors.Add person
                person.Movies.Add movie
            | 'd' ->
                movie.Directors.Add person
                person.Movies.Add movie
            | _ -> ())))

        let moviesByName = Seq.map (fun (KeyValue(_, movie : Movie)) -> movie.PrimaryTitle, movie) movies |> dict
        let peopleByName = Seq.map (fun (KeyValue(_, person : Person)) -> person.PrimaryName, person) people |> dict
        let tagsByName = Seq.map (fun (KeyValue(_, person : Tag)) -> person.Name, person) tags |> dict

        waitForAllTasks()

        { movies = moviesByName; people = peopleByName; tags = tagsByName; moviesById = movies; peopleById = people}