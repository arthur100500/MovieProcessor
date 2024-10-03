namespace MovieProcessor

open System.Globalization
open System.IO
open System

module FileParser =
    // Row Definitions
    [<Struct>]
    type tagCodesRow = {tagId: int; tag: string}
    
    [<Struct>]
    type actorsDirectorsRow = {tconst: int; nconst: int; category: string}
    
    [<Struct>]
    type actorsDirectorsNamesRow = {nconst: int; primaryName: string}
    
    [<Struct>]
    type ratingsRow = {tconst: int; averageRating: float32}
    
    [<Struct>]
    type linksRow = {movieId: int; imdbId: int}
    
    [<Struct>]
    type movieCodesRow = {titleId: int; title: string; region: string}
    
    [<Struct>]
    type tagScoresRow = {movieId: int; tagId: int; relevance: float32; }
    

    // "tagId,tag"
    // -> tagId, tag
    let splitTagCodes (line : string) =
        let [|tagId; tag|] = line.Split(",")
        {tagId=tagId |> Int32.Parse; tag=tag}
        
    // "tconst	ordering	nconst	category	job	characters"
    // -> tconst, nconst, category
    let splitActorsDirectors (line : string) =
        let [|tconst; _; nconst; category; _; _|] = line.Split("\t")
        {tconst=tconst.Substring(2, 7) |> Int32.Parse
         nconst=nconst.Substring(2, 7) |> Int32.Parse
         category=category}

    // "nconst	primaryName	birthYear	deathYear	primaryProfession	knownForTitles"
    // -> nconst, primaryName
    let splitActorsDirectorsNames (line : string) =
        let [|nconst; primaryName; _; _; _; _|] = line.Split("\t")
        {nconst=nconst.Substring(2, 7) |> Int32.Parse
         primaryName=primaryName}

    // "tconst	averageRating	numVotes"
    // -> tconst, averageRating
    let splitRatings (line : string) =
        let [|tconst; averageRating; _|] = line.Split("\t")
        {tconst=tconst.Substring(2, 7) |> Int32.Parse
         averageRating=averageRating |> fun x -> Single.Parse(x, CultureInfo.InvariantCulture)}
        
    // "movieId,imdbId,tmdbId"
    // -> movieId, imdbId
    let splitLinks (line : string) =
        let [|movieId; imdbId; _|] = line.Split(",")
        {movieId=movieId |> Int32.Parse
         imdbId=imdbId |> Int32.Parse}
        
    // "titleId	ordering	title	region	language	types	attributes	isOriginalTitle"
    // -> titleId, title, region
    let splitMovieCodes (line : string) =
        let [|titleId; _; title; region; _; _; _; _|] = line.Split("\t")
        {titleId=titleId.Substring(2, 7) |> Int32.Parse
         title=title
         region=region}
        
    // "movieId,tagId,relevance"
    // -> movieId, tagId, relevance
    let splitTagScores (line : string) =
        let [|movieId; tagId; relevance|] = line.Split(",")
        {movieId=movieId |> Int32.Parse
         tagId=tagId |> Int32.Parse
         relevance=relevance |> fun x -> Single.Parse(x, CultureInfo.InvariantCulture)}
    
    let fileIter parse (path: string) f =
        let fileReadStream = File.OpenRead path
        use reader = new StreamReader(fileReadStream)
        reader.ReadLine() |> ignore
        let rec inner () =
            let line = reader.ReadLine()
            match line with
            | null -> ()
            | data ->
                data |> parse |> f
                inner ()
        inner ()

    let rec fileMap parse (path: string) =
        let fileReadStream = File.OpenRead path
        let reader = new StreamReader(fileReadStream)
        reader.ReadLine() |> ignore
        let rec inner () = seq {
            let line = reader.ReadLine()
            match line with
            | null -> ()
            | data ->
                yield data |> parse
                yield! inner ()
        }
        inner ()
