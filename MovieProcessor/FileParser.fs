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
    let splitTagCodes (line : Span<char>) : obj =
        let firstSeparatorPlacement = line.IndexOf(',')
        {tagId=Int32.Parse(line.Slice(0, firstSeparatorPlacement)); tag=line.Slice(firstSeparatorPlacement).ToString()}
        
    // "tconst	ordering	nconst	category	job	characters"
    // -> tconst, nconst, category
    let splitActorsDirectors (line : Span<char>) : obj =
        let secondSeparatorPlacement = line.Slice(10).IndexOf('\009') + 10
        let thirdSeparatorPlacement = line.Slice(secondSeparatorPlacement + 9).IndexOf('\009') + secondSeparatorPlacement + 9
        let tconst = line.Slice(2, 7)
        let nconst = line.Slice(secondSeparatorPlacement + 3, 7)
        {tconst=Int32.Parse(tconst)
         nconst=Int32.Parse(nconst)
         category=line[thirdSeparatorPlacement + 1]}

    // "nconst	primaryName	birthYear	deathYear	primaryProfession	knownForTitles"
    // -> nconst, primaryName
    let splitActorsDirectorsNames (line : Span<char>) : obj =
        let secondSeparatorPlacement = line.Slice(12).IndexOf('\009') + 12
        {nconst=Int32.Parse(line.Slice(2, 7))
         primaryName=line.Slice(10, secondSeparatorPlacement - 10).ToString()}

    // "tconst	averageRating	numVotes"
    // -> tconst, averageRating
    let splitRatings (line : Span<char>) : obj =
        let firstSeparatorPlacement = line.IndexOf('\009')
        let secondSeparatorPlacement = line.Slice(12).IndexOf('\009') + 12
        {tconst=Int32.Parse(line.Slice(2, 7))
         averageRating=Single.Parse(line.Slice(firstSeparatorPlacement + 1, secondSeparatorPlacement - firstSeparatorPlacement), CultureInfo.InvariantCulture)}
        
    // "movieId,imdbId,tmdbId"
    // -> movieId, imdbId
    let splitLinks (line : Span<char>) : obj =
        let firstSeparatorPlacement = line.IndexOf(',')
        let secondSeparatorPlacement = line.Slice(firstSeparatorPlacement + 1).IndexOf(',') + firstSeparatorPlacement + 1
        // Here exc is ok!
        {movieId=Int32.Parse(line.Slice(0, firstSeparatorPlacement))
         imdbId=Int32.Parse(line.Slice(firstSeparatorPlacement + 1, secondSeparatorPlacement - firstSeparatorPlacement - 1))}

    // "titleId	ordering	title	region	language	types	attributes	isOriginalTitle"
    // -> titleId, title, region
    let splitMovieCodes (line : Span<char>) : obj =
        let firstSeparatorPlacement = line.IndexOf('\009')
        let secondSeparatorPlacement = line.Slice(firstSeparatorPlacement + 1).IndexOf('\009') + firstSeparatorPlacement + 1
        let thirdSeparatorPlacement = line.Slice(secondSeparatorPlacement + 1).IndexOf('\009') + secondSeparatorPlacement + 1
        let fourthSeparatorPlacement = line.Slice(thirdSeparatorPlacement + 1).IndexOf('\009') + thirdSeparatorPlacement + 1
        // [|titleId; _; title; region; _; _; _; _|]
        {titleId=Int32.Parse(line.Slice(2, firstSeparatorPlacement - 2))
         title=line.Slice(secondSeparatorPlacement + 1, thirdSeparatorPlacement - secondSeparatorPlacement).ToString()
         region=line.Slice(thirdSeparatorPlacement + 1, fourthSeparatorPlacement - thirdSeparatorPlacement).ToString()}

    // "movieId,tagId,relevance"
    // -> movieId, tagId, relevance
    let splitTagScores (line : Span<char>) : obj =
        let firstSplit = line.Slice(1).IndexOf(',') + 1
        let secondSplit = line.Slice(firstSplit + 1).IndexOf(',') + firstSplit + 1
        {movieId=Int32.Parse(line.Slice(0, firstSplit))
         tagId=Int32.Parse(line.Slice(firstSplit + 1, secondSplit - firstSplit - 1))
         relevance=Single.Parse(line.Slice(secondSplit + 1, line.Length - secondSplit - 1), CultureInfo.InvariantCulture)}

    // parserId:
    //
    let commonArray = Array.create<char> 100000 '\000'

    let splitFileUsingSpan (fileReadStream : Stream) parserId k =
        use reader = new StreamReader(fileReadStream)
        let allFileSpan = System.Span(commonArray)
        let mutable length = reader.Read(allFileSpan)
        let mutable lineStart = 0
        let mutable index = 0
        let mutable isHeaderLine = true
        // TODO: If there is a preprocessor, use preprocessor to generate functions for each file
        while length = 100000 || isHeaderLine do
            for c in allFileSpan do
                match c with
                | '\n' when isHeaderLine ->
                    lineStart <- index + 1
                    isHeaderLine <- false
                | '\n' ->
                    let d = (allFileSpan.Slice(lineStart, index - lineStart - 1))
                    match parserId with
                    | 0 -> k(splitTagCodes d)
                    | 1 -> k(splitActorsDirectors d)
                    | 2 -> k(splitActorsDirectorsNames d)
                    | 3 -> k(splitRatings d)
                    | 4 ->
                        try
                            k(splitLinks d)
                        with _ -> ()
                    | 5 ->
                        let r = splitMovieCodes d :?> movieCodesRow
                        if (r.region = "RU" || r.region = "US" || r.region = "EN" || r.region = "AU") then k(r)
                        else ()
                    | 6 -> k(splitTagScores d)
                    | _ -> failwith "Unknown parser ID"
                    lineStart <- index + 1
                | _ when index % 100000 = 0 ->
                    length <- reader.Read(commonArray)
                    lineStart <- 0
                    index <- 0
                | _ -> ()
                index <- index + 1

    let fileIter (parserId : int) (path: string) (k : obj -> unit) =
        let fileReadStream = File.OpenRead(path)
        splitFileUsingSpan fileReadStream parserId k


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
