using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

[PrimaryKey(nameof(PersonId))]
public record Person(int PersonId, string PrimaryName, ICollection<Movie> Movies, int NumericalId) : IWithNumericalId
{
    public Person() : this(0, "", new HashSet<Movie>(), -1) { }
    public Person(int id) : this(0, "", new HashSet<Movie>(), id) { }
    public int PersonId { get; private set; } = PersonId;
    public int NumericalId { get; private set; } = NumericalId;
    [StringLength(64)]
    public string PrimaryName { get; private set; } = PrimaryName;
    public ICollection<Movie> Movies { get; private set; } = Movies;

    public Person WithNoLinks()
    {
        var copy = new Person(NumericalId)
        {
            PersonId = PersonId,
            PrimaryName = PrimaryName
        };
        return copy;
    }

    public IQueryable<Movie> GetMovies(ApplicationContext context)
    {
        var asActors = context.ActorsMovies
            .Where(p => p.ActorId == PersonId)
            .Join(context.Movies, l => l.MovieId, p => p.MovieId, (u, c) => c);
        var asDirectors = context.DirectorsMovies
            .Where(p => p.DirectorId == PersonId)
            .Join(context.Movies, l => l.MovieId, p => p.MovieId, (u, c) => c);
        return asActors.Concat(asDirectors);
    }

    public IQueryable<Movie> GetMoviesAsActor(ApplicationContext context)
    {
        var asActors = context.ActorsMovies
            .Where(p => p.ActorId == PersonId)
            .Join(context.Movies, l => l.MovieId, p => p.MovieId, (u, c) => c);
        return asActors;
    }

    public IQueryable<Movie> GetMoviesAsDirector(ApplicationContext context)
    {
        var asDirectors = context.DirectorsMovies
            .Where(p => p.DirectorId == PersonId)
            .Join(context.Movies, l => l.MovieId, p => p.MovieId, (u, c) => c);
        return  asDirectors;
    }
}