using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Humanizer;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

[PrimaryKey(nameof(MovieId))]
public record Movie : IWithNumericalId
{
    public int MovieId { get; private set; }
    public int NumericalId { get; private set; }
    [NotMapped]
    public ICollection<Title> Titles { get; private set; }
    public ICollection<Person> Actors { get; private set; }
    public ICollection<Person> Directors { get; private set; }
    public ICollection<Tag> Tags { get; private set; }
    public float Rating { get; set; }
    public string PrimaryTitle { get; private set; }

    public Movie() { }

    public Movie(int movieId, string title, ICollection<Person> actors, ICollection<Person> directors, ICollection<Tag> tags, float rating, int numericalId)
    {
        MovieId = movieId;
        Titles = new HashSet<Title>([new Title(title, this)]);
        Actors = actors;
        Directors = directors;
        PrimaryTitle = title;
        Tags = tags;
        Rating = rating;
        NumericalId = numericalId;
    }

    public Movie WithNoLinks()
    {
        var copy = new Movie
        {
            MovieId = MovieId,
            PrimaryTitle = PrimaryTitle,
            Rating = Rating,
            NumericalId = NumericalId
        };
        return copy;
    }

    public void AddTitle(string title)
    {
        Titles.Add(new Title(title.Truncate(64), this));
    }

    public override string ToString()
    {
        return $"{MovieId} - {PrimaryTitle}";
    }

    public IQueryable<Person> GetActors(ApplicationContext context)
    {
        var actors = context.ActorsMovies
            .Where(p => p.MovieId == MovieId)
            .Join(context.People, l => l.ActorId, p => p.PersonId, (u, c) => c);
        return actors;
    }

    public IQueryable<Person> GetDirectors(ApplicationContext context)
    {
        var directors = context.DirectorsMovies
            .Where(p => p.MovieId == MovieId)
            .Join(context.People, l => l.DirectorId, p => p.PersonId, (u, c) => c);
        return directors;
    }

    public IQueryable<Tag> GetTags(ApplicationContext context)
    {
        var tags = context.TagsMovies
            .Where(p => p.MovieId == MovieId)
            .Join(context.Tags, l => l.TagId, p => p.TagId, (u, c) => c);
        return tags;
    }

    public IQueryable<Person> GetCast(ApplicationContext context)
    {
        var actors = GetActors(context);
        var directors = GetDirectors(context);
        var actorsDirectors = actors.Concat(directors);
        return actorsDirectors;
    }
}