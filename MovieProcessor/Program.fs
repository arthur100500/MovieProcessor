namespace MovieProcessor

open MovieLoader
open MovieProcessor.Logger

module Program =
    [<EntryPoint>]
    let main args =
        let path = args[0]
        let movies, crew = load path (ConsoleLogger())
        printfn $"Loaded {movies.Count} movies and {crew.Count} people"
        0