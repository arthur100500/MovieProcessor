namespace MovieProcessor

open System.Collections.Concurrent

type ConcurrentHashSet<'a>() =
    let innerSet = ConcurrentDictionary<'a, byte>()
    member _.Add(element) = innerSet.TryAdd(element, 0uy)
    member _.Contains(element) = innerSet.ContainsKey(element)
    member _.Count = innerSet.Count
    member _.Dict = innerSet