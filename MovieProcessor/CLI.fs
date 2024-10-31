namespace MovieProcessor

open System
open System.Linq
open DatabaseInteraction

module CLI =
    [<Literal>]
    let pageSize = 20

    let helpMessage = $"""
Movie Processor command list
Warning: all commands are case-sensitive
- help: see command list
- tags: get list of tags with their respective ids
- tags <Page>: get {pageSize} tags with offset being Page * {pageSize}
- tag <Tag>: get movies with tag Tag (by name or id)
- movies <Page>: get {pageSize} movies with offset being Page * {pageSize}
- movie <Movie>: get data about movie by Movie (by name or id (!!without tt))
- people <Page>: get {pageSize} people with offset being Page * {pageSize}
- person <Person>: get data about person by Person (by name or id (!!without nm))
"""

    let comma = ","

    let stringOfPeople (people : Person seq) (separator : string) =
        let people = if people = null then Seq.ofList [] else people
        let mapped = Seq.map (fun (p : Person) -> $"{p.PersonId} - {p.PrimaryName}") people
        String.Join(separator, mapped)

    let stringOfMovieNames (movies : Movie seq) (separator : string) =
        let movies = if movies = null then Seq.ofList [] else movies
        let mapped = Seq.map (fun (m : Movie) -> $"{m.MovieId} - {m.PrimaryTitle}") movies
        String.Join(separator, mapped)

    let stringOfTags (tags: Tag seq) =
        let tags = if tags = null then Seq.ofList [] else tags
        let tags = Seq.map (fun (i : Tag) -> i.Name) tags
        String.Join(", ", tags)

    let printTags (tagsOrdered : Tag array) (separator: string) (start : int)=
        let mapped = Seq.mapi (fun i -> fun (item : Tag) -> $"{start + i} - {item.Name} ({item.Movies.Count})") tagsOrdered
        String.Join(separator, mapped) |> printfn "%s"

    let printTag (identifier : string) (tagsOrdered : Tag array) =
        let isInt, value = identifier |> Int32.TryParse
        let result = if isInt then Array.tryItem value tagsOrdered else None
        let result = if result.IsNone then Array.tryFind (fun (e : Tag) -> e.Name = identifier) tagsOrdered else result
        match result with
        | Some tag -> printfn $"Movies for {tag.Name}: {stringOfMovieNames tag.Movies comma}"
        | None -> printfn $"Found no tags with name or id being \"{identifier}\""

    let printMovie (identifier : string) (dataset : ApplicationContext) =
        let isInt, value = identifier |> Int32.TryParse
        let result = if isInt then dataset.Movies.Find(value) |> Option.ofObj else None
        let result = if result.IsNone then dataset.Movies.SingleOrDefault(fun (e : Movie) -> e.PrimaryTitle = identifier) |> Option.ofObj else result
        match result with
        | Some movie ->
            printfn $"Title - {movie.PrimaryTitle}"
            printfn $"Director - {stringOfPeople (movie.GetDirectors(dataset)) comma}"
            printfn $"Cast - [{stringOfPeople (movie.GetActors(dataset)) comma}]"
            printfn $"Tags - [{stringOfTags (movie.GetTags(dataset))}]"
            printfn $"Rating - {movie.Rating}"
        | None -> printfn $"Found no movie with name or id being \"{identifier}\""

    let printPerson (identifier : string) (dataset : ApplicationContext) =
        let isInt, value = identifier |> Int32.TryParse
        let result = if isInt then dataset.People.Find(value) |> Option.ofObj else None
        let result = if result.IsNone then dataset.People.Find(fun (e : Person) -> e.PrimaryName = identifier) |> Option.ofObj else result
        match result with
        | Some person ->
            printfn $"Name - {person.PrimaryName}"
            printfn $"Movies - [{stringOfMovieNames person.Movies comma}]"
        | None -> printfn $"Found no movie with name or id being \"{identifier}\""

    let printMoviePage (page : string) (dataset : ApplicationContext) =
        let movieCount = dataset.GetTableRowsCount(dataset.Movies)
        let maxPage = float32 movieCount / (float32 pageSize) |> ceil |> int
        let isInt, value = page |> Int32.TryParse
        match isInt && value < maxPage && value > -1 with
        | true ->
            let page = dataset.Movies |> Seq.skip (pageSize * value) |> Seq.truncate pageSize
            printfn "%s" (stringOfMovieNames page "\n")
        | false -> printfn $"Provided page is not valid! (Not an integer or bigger than {movieCount/ pageSize})"

    let printPeoplePage (page : string) (dataset : ApplicationContext) =
        let peopleCount = dataset.GetTableRowsCount(dataset.People)
        let maxPage = float32 peopleCount / (float32 pageSize) |> ceil |> int
        let isInt, value = page |> Int32.TryParse
        match isInt && value < maxPage && value > -1 with
        | true ->
            let page = dataset.People |> Seq.skip (pageSize * value) |> Seq.truncate pageSize
            printfn "%s" (stringOfPeople page "\n")
        | false -> printfn $"Provided page is not valid! (Not an integer or bigger than {peopleCount / pageSize})"

    let printTagPage (page : string) (tagsOrdered : Tag array) =
        let maxPage = float32 tagsOrdered.Length / (float32 pageSize) |> ceil |> int
        let isInt, value = page |> Int32.TryParse
        match isInt && value < maxPage && value > -1 with
        | true ->
            let page = tagsOrdered |> Seq.skip (pageSize * value) |> Seq.truncate pageSize |> Seq.toArray
            printTags page "\n" (pageSize * value)
        | false -> printfn $"Provided page is not valid! (Not an integer or bigger than {tagsOrdered.Length / pageSize})"


    let rec run (data : ApplicationContext) =
        printfn "Loading CLI..."
        let tagsOrdered = data.GetAllTags() |> Seq.sortByDescending (_.Movies.Count) |> Seq.toArray
        let rec loop () =
            printf "MP> "
            let input = Console.ReadLine().Split() |> List.ofArray
            match input with
            | ["help"] -> printfn $"%s{helpMessage}"
            | ["tags"] -> printTags tagsOrdered ", " 0
            | ["tags"; page] -> printTagPage page tagsOrdered
            | "tag" :: id -> printTag (String.Join(" ", id)) tagsOrdered
            | ["movies"; page] -> printMoviePage page data
            | "movie" :: id -> printMovie (String.Join(" ", id)) data
            | ["people"; page] -> printPeoplePage page data
            | "person" :: id -> printPerson (String.Join(" ", id)) data
            | _ -> printfn $"%s{helpMessage}"
            printfn ""
            loop ()
        loop ()