namespace MovieProcessor
open System
open System.Collections.Generic
open System.IO
open FSharp.Data

module MovieLoader =

    [<Literal>]
    let comma = ", "

    type Tag = string

    type Person(name) =
        let Name: string = name
        let movies: HashSet<Movie> = HashSet<Movie>([])
        member _.AddMovie(movie) = movies.Add movie
        override _.ToString() = Name
        interface IComparable with
            member this.CompareTo(obj : obj) = this.GetHashCode().CompareTo(obj.GetHashCode())

    and Movie(name, tags) =
        let Name : string = name
        let Actors : HashSet<Person> = HashSet<Person>([])
        let mutable Director : Person option = None
        let Tags: HashSet<Tag> = tags
        let mutable Rating: float32 = -1f
        member _.SetDirector(director : Person) = Director <- Some director
        member _.AddActor(actor : Person) = Actors.Add(actor)
        member _.AddTag(tag) = Tags.Add(tag)
        member _.GetTags() = Tags
        member _.SetRating(rating) = Rating <- rating
        member _.ParsedCompletely() = Name <> "" && Director.IsSome && Actors.Count > 0 && Tags.Count > 0 && Rating > -1f
        override _.ToString() = $"{Director} - {Name} {Rating} [{String.Join(comma, Tags |> Seq.map _.ToString())}] [{String.Join(comma, Actors |> Seq.map _.ToString())}]"
        interface IComparable with
            member this.CompareTo(obj : obj) = this.GetHashCode().CompareTo(obj.GetHashCode())


    let dictByField fieldFactory rows =
        Seq.map (fun r -> (fieldFactory r, r)) rows |> dict

    let groupByField fieldFactory rows =
        let allFields = rows |> Seq.map fieldFactory |> Seq.distinct
        let result = Dictionary<_, _>()
        Seq.iter (fun f -> result.Add(f, HashSet<_>([]))) allFields
        for entry in rows do result[fieldFactory entry].Add(entry) |> ignore
        result

    let tryGet (dictionary : IDictionary<_, _>) key =
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

    let load path =
        let path = @"C:\Users\arthu\Desktop\ml-latest"

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
        printfn "Organizing data into dictionaries"
        printfn "Tag Codes"
        let tagCodesDict = dictByField (fun (x : TagCodesProvider.Row) -> x.TagId) tagCodes.Rows
        printfn "Movie ID Links"
        let imdbOfMl = dictByField (fun (x : LinksProvider.Row) -> x.MovieId) linksImdbMovieLens.Rows

        // IMDB ID to Movie
        let moviesById = Dictionary<int, Movie>()
        // NM ID to Person
        let peopleById = Dictionary<int, Person>()

        let ruEn = movieCodes.Rows |> Seq.filter (fun r -> r.Region = "RU" || r.Region = "US" || r.Region = "GB" || r.Region = "AU")
        let ruEnImdbIds = HashSet<_>([])
        
        let intOfId = fun (t : string) -> t.Substring(2) |> int
        
        // Create movie with empty tags and no rating
        printfn "Loading movies"
        for row in ruEn do
            let imdbId = row.TitleId
            ruEnImdbIds.Add imdbId |> ignore
            let movie = Movie(row.Title, HashSet<Tag>([]))
            moviesById.TryAdd(intOfId imdbId, movie) |> ignore
        
        // Set rating
        printfn "Loading movie ratings"
        for row in (ratingsImdb.Rows |> Seq.filter (fun x -> ruEnImdbIds.Contains(x.Tconst))) do
            let movie = tryGet moviesById (intOfId <| row.Tconst)
            let rating = row.AverageRating |> float32
            movie |> Option.iter (_.SetRating(rating))
            
        // Set tags
        // TODO: Filter by imdbId in ru/en
        printfn "Loading movie tags"
        for row in (tagScores.Rows) do
            let imdbId = row.MovieId |> tryGet imdbOfMl |> Option.map (_.ImdbId)
            let movie = imdbId |> Option.map intOfId |> Option.bind (tryGet moviesById)
            let tag = tryGet tagCodesDict row.TagId |> Option.map (_.Tag)
            let scoreValid = (float32 row.Relevance) > 0.5f
            if scoreValid then
                movie |> Option.iter (fun movie ->
                tag |> Option.iter (fun tag ->
                movie.AddTag tag |> ignore))
            else ()
        
        printfn "Calculating relevant actors and directors ids"
        let relevantActorsIds =
            actorsDirectorsCodesCopy.Rows
            |> Seq.filter (fun x -> ruEnImdbIds.Contains(x.Tconst))
            |> Seq.map (fun t -> t.Nconst |> intOfId)
            |> HashSet<_>

        // Create people
        printfn "Loading people"
        for row in (actorsDirectorsNames.Rows |> Seq.filter (fun r -> relevantActorsIds.Contains(r.Nconst |> intOfId))) do
            let person = Person(row.PrimaryName)
            peopleById.TryAdd(intOfId row.Nconst, person) |> ignore

        // Link people to movies
        printfn "Linking people and movies"
        for row in actorsDirectorsCodes.Rows |> Seq.filter (fun r -> relevantActorsIds.Contains(r.Nconst |> intOfId)) do
            tryGet moviesById (intOfId row.Tconst) |> Option.iter (fun movie ->
            tryGet peopleById (intOfId row.Nconst) |> Option.iter (fun person ->
            match row.Category with
            | "actor" | "actress" | "self" ->
                movie.AddActor person |> ignore
                person.AddMovie movie |> ignore
            | "director" ->
                movie.SetDirector person
                person.AddMovie movie |> ignore
            | _ -> ()))

        Seq.iter (fun (f : KeyValuePair<_,Movie>) -> if f.Value.ParsedCompletely() then printfn $"{f.Value.ToString()}" else ()) moviesById
        []