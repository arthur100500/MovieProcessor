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
| 4 | 17cb41d |   1m 12s | -        | Add blocking collections                        |

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

**Build 4** Add blocking collections to create pipeline like this: readline |> parse |> store

