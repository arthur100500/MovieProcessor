namespace MovieProcessor

open MovieLoader
open MovieProcessor.Logger

module Program =
    [<EntryPoint>]
    let main args =
        let path = args[0]
        let logger = ConsoleLogger()
        let dataset = load path logger
        (logger :> ILogger).info "Ready"
        CLI.run dataset
        0