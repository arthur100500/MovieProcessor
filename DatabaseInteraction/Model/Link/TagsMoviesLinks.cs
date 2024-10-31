using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction.Link;

[PrimaryKey(nameof(TagsMoviesId))]
public record TagsMoviesLinks()
{
    public int TagsMoviesId { get; set; }
    public int MovieId { get; set; }
    public int TagId { get; set; }
    public Movie Movie { get; set; }
    public Tag Tag { get; set; }

    public TagsMoviesLinks(Tag tag, Movie movie) : this()
    {
        TagId = tag.TagId;
        MovieId = movie.MovieId;
        Tag = tag;
        Movie = movie;
    }

    public string ToSqlString() => $"({TagId}, {MovieId})";
}