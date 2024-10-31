using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction.Link;

[PrimaryKey(nameof(DirectorsMoviesLinkId))]
public record DirectorsMoviesLinks()
{
    public int DirectorsMoviesLinkId { get; set; }
    public int DirectorId { get; set; }
    public int MovieId { get; set; }
    public Person Director { get; set; }
    public Movie Movie { get; set; }

    public DirectorsMoviesLinks(Person director, Movie movie) : this()
    {
        DirectorId = director.PersonId;
        MovieId = movie.MovieId;
        Director = director;
        Movie = movie;
    }

    public string ToSqlString() => $"({DirectorId}, {MovieId})";
}