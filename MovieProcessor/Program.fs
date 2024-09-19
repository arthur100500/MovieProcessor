namespace MovieProcessor

open MovieLoader

module Program =
    [<EntryPoint>]
    let main args =
        MovieLoader.load "" |> ignore
        0