namespace MovieProcessor

open System.Collections.Concurrent
open System.Globalization
open System.IO
open System
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
        {tagId=tagId |> Int32.Parse; tag=tag} |> Some
        
    // "tconst	ordering	nconst	category	job	characters"
    // -> tconst, nconst, category
    let splitActorsDirectors (line : string) =
        let secondSeparatorPlacement = line.IndexOf('\009', 10)
        let thirdSeparatorPlacement = line.IndexOf('\009', secondSeparatorPlacement + 9)
        let tconst = line.Substring(2, 7)
        let nconst = line.Substring(secondSeparatorPlacement + 3, 7)
        {tconst=tconst |> Int32.Parse
         nconst=nconst |> Int32.Parse
         category=line[thirdSeparatorPlacement + 1]} |> Some

    // "nconst	primaryName	birthYear	deathYear	primaryProfession	knownForTitles"
    // -> nconst, primaryName
    let splitActorsDirectorsNames (line : string) =
        let secondSeparatorPlacement = line.IndexOf('\009', 12)
        {nconst=line.Substring(2, 7) |> Int32.Parse
         primaryName=line.Substring(10, secondSeparatorPlacement - 10)} |> Some

    // "tconst	averageRating	numVotes"
    // -> tconst, averageRating
    let splitRatings (line : string) =
        let [|tconst; averageRating; _|] = line.Split('\009')
        {tconst=tconst.Substring(2, 7) |> Int32.Parse
         averageRating=averageRating |> fun x -> Single.Parse(x, CultureInfo.InvariantCulture)} |> Some
        
    // "movieId,imdbId,tmdbId"
    // -> movieId, imdbId
    let splitLinks (line : string) =
        let [|movieId; imdbId; _|] = line.Split(',')
        {movieId=movieId |> Int32.Parse
         imdbId=imdbId |> Int32.Parse} |> Some
        
    // "titleId	ordering	title	region	language	types	attributes	isOriginalTitle"
    // -> titleId, title, region
    let splitMovieCodes (line : string) =
        let inline checkRegion r = r = "RU" || r = "US" || r = "GB" || r = "AU"
        let [|titleId; _; title; region; _; _; _; _|] = line.Split("\t")
        match checkRegion region with
        | true -> Some { titleId=titleId.Substring(2, 7) |> Int32.Parse
                         title=title
                         region=region }
        | false -> None

        
    // "movieId,tagId,relevance"
    // -> movieId, tagId, relevance
    let splitTagScores (line : string) =
        let firstSplit = line.IndexOf(',', 1)
        let secondSplit = line.IndexOf(',', firstSplit + 1)
        {movieId=line.Substring(0, firstSplit) |> Int32.Parse
         tagId=line.Substring(firstSplit + 1, secondSplit - firstSplit - 1)  |> Int32.Parse
         relevance=line.Substring(secondSplit + 1, line.Length - secondSplit - 1) |> fun x -> Single.Parse(x, CultureInfo.InvariantCulture)} |> Some
    
    let fileIter parse (path: string) f =
        let fileReadStream = File.OpenRead path
        let fileReadCollection = new BlockingCollection<_>()
        let funcApplyCollection = new BlockingCollection<_>()
        let reader = new StreamReader(fileReadStream)

        let addTask = Task.Run (fun () ->
            printf "  | Adding lines "
            reader.ReadLine() |> ignore
            let mutable line = reader.ReadLine()
            while line <> null do
                fileReadCollection.Add(line)
                line <- reader.ReadLine()
            fileReadCollection.CompleteAdding()
            printf "  | Complete     "
            (* reader.Dispose()*))

        let parseTask = Task.Run (fun () ->
            printf "| Parsing lines "
            let readSequence = fileReadCollection.GetConsumingEnumerable()
            readSequence |> Seq.iter (fun i -> parse i |> Option.iter funcApplyCollection.Add)
            funcApplyCollection.CompleteAdding()
            printf "| Complete      "
            (* fileReadCollection.Dispose()*) )

        let funcApplyTask = Task.Run (fun () ->
            printfn "| Applying functions"
            let applySequence = funcApplyCollection.GetConsumingEnumerable()
            applySequence |> Seq.iter f
            printfn "| Complete"
            (*funcApplyCollection.Dispose()*))

        [addTask; parseTask; funcApplyTask] |> Task.WhenAll


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
