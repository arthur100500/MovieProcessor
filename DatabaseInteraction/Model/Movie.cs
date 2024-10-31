using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Humanizer;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

[PrimaryKey(nameof(MovieId))]
public record Movie
{
    public int MovieId { get; private set; }
    [NotMapped]
    public ICollection<Title> Titles { get; private set; }
    public ICollection<Person> Actors { get; private set; }
    public ICollection<Person> Directors { get; private set; }
    public ICollection<Tag> Tags { get; private set; }
    public float Rating { get; set; }
    public string PrimaryTitle { get; private set; }

    public Movie() { }

    public Movie(int movieId, string title, ICollection<Person> actors, ICollection<Person> directors, ICollection<Tag> tags, float rating)
    {
        MovieId = movieId;
        Titles = new HashSet<Title>([new Title(title, this)]);
        Actors = actors;
        Directors = directors;
        PrimaryTitle = title;
        Tags = tags;
        Rating = rating;
    }

    public Movie WithNoLinks()
    {
        var copy = new Movie
        {
            MovieId = MovieId,
            PrimaryTitle = PrimaryTitle,
            Rating = Rating
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
}