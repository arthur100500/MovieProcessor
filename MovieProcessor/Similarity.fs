namespace MovieProcessor

open System.Collections.Generic
open DatabaseInteraction
open System.Linq

module Similarity =
    let similarity (context : ApplicationContext) (first : Movie) (second : Movie) : float32 =
        // Cast + Director
        let firstActors = first.GetActors(context)
        let secondActors = second.GetActors(context)
        let firstActorsDirectors = firstActors |> Seq.append (first.GetDirectors(context)) |> HashSet
        let secondActorsDirectors = secondActors |> Seq.append (second.GetDirectors(context)) |> HashSet
        let firstInSecond = firstActorsDirectors |> Seq.filter secondActorsDirectors.Contains |> Seq.length
        let secondInFirst = secondActorsDirectors |> Seq.filter firstActorsDirectors.Contains |> Seq.length
        let peopleSimilarityIndex = (1 + firstInSecond + secondInFirst) / (1 + Seq.length firstActorsDirectors + Seq.length secondActorsDirectors)
        // Tags
        let firstTags = first.GetTags(context)|> HashSet
        let secondTags = second.GetTags(context)|> HashSet
        let firstInSecond = firstTags |> Seq.filter secondTags.Contains |> Seq.length
        let secondInFirst = secondTags |> Seq.filter firstTags.Contains |> Seq.length
        let tagsSimilarityIndex = (1 + firstInSecond + secondInFirst) / (1 + Seq.length firstTags + Seq.length secondTags)
        float32(peopleSimilarityIndex + tagsSimilarityIndex) * 0.25f + second.Rating * 0.0ефп 5f

    let similarityCandidates (context : ApplicationContext) (target : Movie) =
        let castIds = target.GetActors(context) |> Seq.append (target.GetDirectors(context)) |> Seq.map (_.PersonId)
        let tagsIds = target.GetTags(context) |> Seq.map (_.TagId)
        let actorMatches = context.ActorsMovies.Where(fun i -> castIds.Contains(i.ActorId))
        let tagMatches = context.TagsMovies.Where(fun i -> tagsIds.Contains(i.TagId))
        let directorMatches = context.DirectorsMovies.Where(fun i -> castIds.Contains(i.DirectorId))
        let movieIds = actorMatches.Select(fun e -> e.MovieId)
                                   .Concat(tagMatches.Select(fun e -> e.MovieId)
                                   .Concat(directorMatches.Select(fun e -> e.MovieId)))
        context.Movies.Where(fun m -> movieIds.Contains(m.MovieId))