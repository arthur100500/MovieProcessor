﻿namespace MovieProcessor

open System
open System.Collections.Generic

module Movie =
    [<Literal>]
    let comma = ", "

    type Tag = string

    type Person(id, name) =
        let Id : string = id
        let PrimaryName: string = name
        let movies: HashSet<Movie> = HashSet<Movie>([])
        member _.AddMovie(movie) = movies.Add movie
        member _.GetPrimaryName() = PrimaryName
        member _.GetId() = Id
        member _.GetMovies() = movies
        override _.ToString() = PrimaryName
        interface IComparable with
            member this.CompareTo(obj : obj) = this.GetHashCode().CompareTo(obj.GetHashCode())

    and Movie(id, name, tags) =
        let Id : string = id
        let Title : string = name
        let Actors : HashSet<Person> = HashSet<Person>([])
        let mutable Director : Person option = None
        let Tags: HashSet<Tag> = tags
        let mutable Rating: float32 = -1f
        member _.SetDirector(director : Person) = Director <- Some director
        member _.AddActor(actor : Person) = Actors.Add(actor)
        member _.AddTag(tag) = Tags.Add(tag)
        member _.GetTags() = Tags
        member _.SetRating(rating) = Rating <- rating
        member _.GetId() = Id
        member _.GetTitle() = Title
        member _.GetDirector() = Director
        member _.GetActors() = Actors
        member _.GetRating() = Rating
        member _.ParsedCompletely() = Title <> "" && Director.IsSome && Actors.Count > 0 && Tags.Count > 0 && Rating > -1f
        override _.ToString() = $"{Director} - {Title} {Rating} [{String.Join(comma, Tags |> Seq.map _.ToString())}] [{String.Join(comma, Actors |> Seq.map _.ToString())}]"
        interface IComparable with
            member this.CompareTo(obj : obj) = this.GetHashCode().CompareTo(obj.GetHashCode())
