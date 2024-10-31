namespace MovieProcessor

open System.Collections.Generic
open System.Threading.Tasks
open DatabaseInteraction
open DatabaseInteraction.Link
open MovieLoader
open MovieProcessor.Logger

module Program =
    let writeDataset (dataset : Dataset) (logger : ILogger) =
        use db = new ApplicationContext()
        let moviesArray = Array.zeroCreate dataset.moviesById.Count
        let peopleArray = Array.zeroCreate dataset.peopleById.Count
        let tagArray = Array.zeroCreate dataset.tags.Count
        dataset.moviesById.Values.CopyTo(moviesArray, 0)
        dataset.peopleById.Values.CopyTo(peopleArray, 0)
        dataset.tags.Values.CopyTo(tagArray, 0)
        // Create entities
        logger.info "Writing entities into DB (with no links)"
        let tasksToWait = List<Task>()
        moviesArray |> Seq.map (_.WithNoLinks()) |> Seq.toArray |> db.Movies.AddRangeAsync |> tasksToWait.Add
        peopleArray |> Seq.map (_.WithNoLinks()) |> Seq.toArray |> db.People.AddRangeAsync |> tasksToWait.Add
        tagArray |> Seq.map (_.WithNoLinks()) |> Seq.toArray |> db.Tags.AddRangeAsync |> tasksToWait.Add
        let actorsMovies = List<ActorsMoviesLinks>();
        let directorsMovies = List<DirectorsMoviesLinks>();
        let tagsMovies = List<TagsMoviesLinks>()
        // Link entities
        Task.WaitAll <| tasksToWait.ToArray()
        db.SaveChanges() |> ignore
        logger.info "Creating links"
        moviesArray |> Seq.iter (fun (movie : Movie) ->
            movie.Actors |> Seq.iter (fun person -> actorsMovies.Add(ActorsMoviesLinks(person, movie)))
            movie.Directors |> Seq.iter (fun director -> directorsMovies.Add(DirectorsMoviesLinks(director, movie)))
            movie.Tags |> Seq.iter (fun tag -> tagsMovies.Add(TagsMoviesLinks(tag, movie))))
        logger.info "Writing links into DB"
        let comma = ", "
        let actorsMoviesData = String.concat comma (Seq.map (fun (f : ActorsMoviesLinks) -> f.ToSqlString()) actorsMovies)
        let directorsMoviesData = String.concat comma (Seq.map (fun (f : DirectorsMoviesLinks) -> f.ToSqlString()) directorsMovies)
        let tagsMoviesData = String.concat comma (Seq.map (fun (f : TagsMoviesLinks) -> f.ToSqlString()) tagsMovies)
        db.ExecuteSql($"INSERT INTO ActorsMovies (ActorId, MovieId) VALUES {actorsMoviesData};") |> ignore
        db.ExecuteSql($"INSERT INTO DirectorsMovies (DirectorId, MovieId) VALUES {directorsMoviesData};") |> ignore
        db.ExecuteSql($"INSERT INTO TagsMovies (TagId, MovieId) VALUES {tagsMoviesData};") |> ignore
        logger.info "Saving database"
        db.SaveChanges() |> ignore

    [<EntryPoint>]
    let main args =
        let path = args[0]
        let logger = ConsoleLogger()
        let dataset = load path logger
        writeDataset dataset logger
        (logger :> ILogger).info "Ready"
        // CLI.run dataset
        0