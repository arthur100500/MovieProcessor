namespace MovieProcessor

open System.Globalization
open System.IO
open System
open System.Threading
open System.Threading.Tasks

module FileParser =
    // Row Definitions
    [<Struct>]
    type tagCodesRow = {tagId: int; tag: string}
    
    [<Struct>]
    type actorsDirectorsRow = {tconst: int; nconst: int; category: char}
    
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
        let line = line.AsSpan()
        let secondSeparatorPlacement = line.Slice(10).IndexOf('\009') + 10
        let thirdSeparatorPlacement = line.Slice(secondSeparatorPlacement + 9).IndexOf('\009') + secondSeparatorPlacement + 9
        let tconst = line.Slice(2, 7)
        let nconst = line.Slice(secondSeparatorPlacement + 3, 7)
        {tconst=Int32.Parse(tconst)
         nconst=Int32.Parse(nconst)
         category=line[thirdSeparatorPlacement + 1]}

    // "nconst	primaryName	birthYear	deathYear	primaryProfession	knownForTitles"
    // -> nconst, primaryName
    let splitActorsDirectorsNames (line : string) =
        let line = line.AsSpan()
        let secondSeparatorPlacement = line.Slice(12).IndexOf('\009') + 12
        {nconst=Int32.Parse(line.Slice(2, 7))
         primaryName=line.Slice(10, secondSeparatorPlacement - 10).ToString()}

    // "tconst	averageRating	numVotes"
    // -> tconst, averageRating
    let splitRatings (line : string) =
        let [|tconst; averageRating; _|] = line.Split('\009')
        {tconst=tconst.Substring(2, 7) |> Int32.Parse
         averageRating=averageRating |> fun x -> Single.Parse(x, CultureInfo.InvariantCulture)}
        
    // "movieId,imdbId,tmdbId"
    // -> movieId, imdbId
    let splitLinks (line : string) =
        let [|movieId; imdbId; _|] = line.Split(',')
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
        let line = line.AsSpan()
        let firstSplit = line.Slice(1).IndexOf(',') + 1
        let secondSplit = line.Slice(firstSplit + 1).IndexOf(',') + firstSplit + 1
        {movieId=Int32.Parse(line.Slice(0, firstSplit))
         tagId=Int32.Parse(line.Slice(firstSplit + 1, secondSplit - firstSplit - 1))
         relevance=Single.Parse(line.Slice(secondSplit + 1, line.Length - secondSplit - 1), CultureInfo.InvariantCulture)}

    let mutable queuedProcessesCount = 0

    let fileIter parse (path: string) f =
        let fileReadStream = File.OpenRead path
        use reader = new StreamReader(fileReadStream)
        reader.ReadLine() |> ignore
        let rec inner () =
            let line = reader.ReadLine()
            match line with
            | null -> ()
            | data ->
                data |> parse |> f |> ignore
                inner ()
        inner ()

    let rec waitForAllTasks () =
        while queuedProcessesCount <> 0 do
            printfn "%d" queuedProcessesCount
            Thread.Yield() |> ignore

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
