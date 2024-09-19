namespace MovieProcessor

open MovieLoader

module Program =
    [<EntryPoint>]
    let main args =
        let path = args[0]
        let movies, crew = load path
        printfn $"Loaded {movies.Count} movies and {crew.Count} people"
        0