# Movie processor
Homework for .NET course, by Alekseev Artur.

### History of improvements and performance increase
Every run was done with Release build and on my laptop (i will not share details). <br>
Run time is avarage between 3 runs

| № | Commit  | Run time | Increase | Description                                     |
|---|---------|---------:|----------|-------------------------------------------------|
| 1 | 22bb500 |   1m 41s | -        | Initial build                                   |
| 2 | 17c690a |   1m 05s | -        | Replaced CSVProvider by custom implementation   |
| 3 | 168ffb9 |      45s | -        | Replaced Split() to Substring in critical parts |
| 3 | 168ffb9 |   1m 20s | -        | Async with blocking collection                  |
| 3 | 168ffb9 |   1m 01s | -        | Blocking collection + reading files parallel    |

### Improvement and implementation details

**Build 1**: Used F# and CsvProvider types to parse files, looped through all the lines and stored data in 3 objects of type Dictionary<T>. String are splitted by CsvProvider. Only one thread was used.

**Build 2:** Replaced CSVProvider with custom implementation like this:
```fsharp
// Iterates over file by line applying: f (parse line)
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
```

And line parsers like this
```fsharp
[<Struct>]
type tagScoresRow = {movieId: int; tagId: int; relevance: float32; }
    
let splitLinks (line : string) =
    let [|movieId; imdbId; _|] = line.Split(",")
    {movieId=movieId |> Int32.Parse
     imdbId=imdbId |> Int32.Parse}
```

**Build 3:** Replaced string.Split() with string.Subsctring and indexOf in critical parts (while parsing top 3 biggest files)
```fsharp
let splitMovieCodes (line : string) =
    let [|titleId; _; title; region; _; _; _; _|] = line.Split("\t")
    {titleId=titleId.Substring(2, 7) |> Int32.Parse
     title=title
     region=region}
```

**Parallel implementations:** Parallel implementation did nothing to improve performance, although the usage of CPU was far higher. 
There were 2 distinct ways to increase performance, firstly, while reading a file, create a task for each line, and execute parallelly. This failed miserably, as the amount of tasks to manage was too high.
We tried limiting tasks using a counter and interlocked operations to have only 12 tasks running (CPU Threads), but this had no success
Secondly, the better way was blocking collection

**Parallel implementation with blocking collection:** Blocking collection was used to create following pipeline instead of usual process:
```fsharp
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
```

**Parallel implementation with blocking collection configurations:**
There were two configurations, in first one every file was read one after another, just the operations while reading the file were parallel.
```
Task 3:                       Save Value   Save Value   Save Value ....
Task 2:            Parse line | Parse line | Parse line | ....
Task 1:  Read line | Read line  |  Read line |   ....
Time:   +---------++-----------++-----------++--------------------------------> Finished first file, start second etc for 6 files

Task 1-3:  Movies ----| People -----| Links -----------| Tags ---| Ratings ---| Finish
Time:   +---------++-----------++-----------++--------------------------------> 
```
The second configuration was more parallel, it started reading Movies and People first, then Ratings, Tags and Links
```
Task 7-9:                   Links --------------------------|
Task 4-6:   People --|      Ratings ------|
Task 1-3:   Movies --------|Tags ------|                     Finish
Time:   -----------------------------------------------------|----------------> 
```
