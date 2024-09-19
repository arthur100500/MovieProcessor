namespace MovieProcessor

open System

module Logger =
    [<Interface>]
    type ILogger =
        abstract info : string -> unit
        abstract error : string -> unit
        abstract warning : string -> unit

    type ConsoleLogger() =
        let out = Console.Out
        let getTime () = DateTime.Now.ToLongTimeString()
        interface ILogger with
            member _.info(message) = out.WriteLine $"[{getTime()}] info: {message}"
            member _.error(message) = out.WriteLine $"[{getTime()}] error: {message}"
            member _.warning(message) = out.WriteLine $"[{getTime()}] warn: {message}"

    type VoidLogger() =
        interface ILogger with
            member _.info _ = ()
            member _.error _ = ()
            member _.warning _ = ()



