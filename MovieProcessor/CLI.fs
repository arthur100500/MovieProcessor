namespace MovieProcessor

open System
open System.Collections.Generic
open MovieProcessor.Movie
open MovieProcessor.MovieLoader

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

    let stringOfPeople (people : Person seq) (separator : string) =
        let mapped = Seq.map (fun (p : Person) -> $"{p.GetId()} - {p.GetPrimaryName()}") people
        String.Join(separator, mapped)

    let stringOfMovieNames (movies : Movie seq) (separator : string) =
        let mapped = Seq.map (fun (m : Movie) -> $"{m.GetId()} - {m.GetTitle()}") movies
        String.Join(separator, mapped)

    let stringOfTags (tags: Tag seq) =
        String.Join(", ", tags)

    let printTags (tagsOrdered : KeyValuePair<string, ConcurrentHashSet<Movie>> array) (separator: string) (start : int)=
        let mapped = Seq.mapi (fun i -> fun (item : KeyValuePair<string, ConcurrentHashSet<Movie>>) -> $"{start + i} - {item.Key} ({item.Value.Count})") tagsOrdered
        String.Join(separator, mapped) |> printfn "%s"

    let printTag (identifier : string) (tagsOrdered : KeyValuePair<string, ConcurrentHashSet<Movie>> array) (tagsDict : IDictionary<_,ConcurrentHashSet<Movie>>) =
        let isInt, value = identifier |> Int32.TryParse
        let result = if isInt then Array.tryItem value tagsOrdered else None
        let result = if result.IsNone then tryGet tagsDict identifier |> Option.map (fun r -> KeyValuePair(identifier, r)) else result
        match result with
        | Some tag -> printfn $"Movies for {tag.Key}: {stringOfMovieNames tag.Value.Dict.Keys comma}"
        | None -> printfn $"Found no tags with name or id being \"{identifier}\""

    let printMovie (identifier : string) (dataset : Dataset) =
        let isInt, value = identifier |> Int32.TryParse
        let result = if isInt then tryGet dataset.moviesById value else None
        let result = if result.IsNone then tryGet dataset.movies identifier else result
        match result with
        | Some movie ->
            printfn $"Title - {movie.GetTitle()}"
            printfn $"Director - {movie.GetDirector()}"
            printfn $"Cast - [{stringOfPeople (movie.GetActors().Dict.Keys) comma}]"
            printfn $"Tags - [{stringOfTags (movie.GetTags().Dict.Keys)}]"
            printfn $"Rating - {movie.GetRating()}"
        | None -> printfn $"Found no movie with name or id being \"{identifier}\""

    let printPerson (identifier : string) (dataset : Dataset) =
        let isInt, value = identifier |> Int32.TryParse
        let result = if isInt then tryGet dataset.peopleById value else None
        let result = if result.IsNone then tryGet dataset.people identifier else result
        match result with
        | Some person ->
            printfn $"Name - {person.GetPrimaryName()}"
            printfn $"Movies - [{stringOfMovieNames (person.GetMovies().Dict.Keys) comma}]"
        | None -> printfn $"Found no movie with name or id being \"{identifier}\""

    let printMoviePage (page : string) (dataset : Dataset) =
        let maxPage = float32 dataset.movies.Count / (float32 pageSize) |> ceil |> int
        let isInt, value = page |> Int32.TryParse
        match isInt && value < maxPage && value > -1 with
        | true ->
            let page = dataset.movies.Values |> Seq.skip (pageSize * value) |> Seq.truncate pageSize
            printfn "%s" (stringOfMovieNames page "\n")
        | false -> printfn $"Provided page is not valid! (Not an integer or bigger than {dataset.movies.Count / pageSize})"

    let printPeoplePage (page : string) (dataset : Dataset) =
        let maxPage = float32 dataset.people.Count / (float32 pageSize) |> ceil |> int
        let isInt, value = page |> Int32.TryParse
        match isInt && value < maxPage && value > -1 with
        | true ->
            let page = dataset.people.Values |> Seq.skip (pageSize * value) |> Seq.truncate pageSize
            printfn "%s" (stringOfPeople page "\n")
        | false -> printfn $"Provided page is not valid! (Not an integer or bigger than {dataset.people.Count / pageSize})"

    let printTagPage (page : string) (tagsOrdered : KeyValuePair<string, ConcurrentHashSet<Movie>> array) =
        let maxPage = float32 tagsOrdered.Length / (float32 pageSize) |> ceil |> int
        let isInt, value = page |> Int32.TryParse
        match isInt && value < maxPage && value > -1 with
        | true ->
            let page = tagsOrdered |> Seq.skip (pageSize * value) |> Seq.truncate pageSize |> Seq.toArray
            printTags page "\n" (pageSize * value)
        | false -> printfn $"Provided page is not valid! (Not an integer or bigger than {tagsOrdered.Length / pageSize})"


    let rec run (data : Dataset) =
        printf "MP> "
        let input = Console.ReadLine().Split() |> List.ofArray
        let tagsOrdered = data.tags |> Seq.sortByDescending (_.Value.Count) |> Seq.toArray
        match input with
        | ["help"] -> printfn $"%s{helpMessage}"
        | ["tags"] -> printTags tagsOrdered ", " 0
        | ["tags"; page] -> printTagPage page tagsOrdered
        | "tag" :: id -> printTag (String.Join(" ", id)) tagsOrdered data.tags
        | ["movies"; page] -> printMoviePage page data
        | "movie" :: id -> printMovie (String.Join(" ", id)) data
        | ["people"; page] -> printPeoplePage page data
        | "person" :: id -> printPerson (String.Join(" ", id)) data
        | _ -> printfn $"%s{helpMessage}"
        printfn ""
        run data