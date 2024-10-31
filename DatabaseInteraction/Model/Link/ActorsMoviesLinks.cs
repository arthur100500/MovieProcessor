using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction.Link;

[PrimaryKey(nameof(ActorsMoviesLinkId))]
public record ActorsMoviesLinks()
{
    public int ActorsMoviesLinkId { get; set; }
    public int ActorId { get; set; }
    public int MovieId { get; set; }
    public Person? Actor { get; set; }
    public Movie? Movie { get; set; }

    public ActorsMoviesLinks(Person actor, Movie movie) : this()
    {
        ActorId = actor.PersonId;
        MovieId = movie.MovieId;
        Actor = actor;
        Movie = movie;
    }

    public string ToSqlString() => $"({ActorId}, {MovieId})";
}