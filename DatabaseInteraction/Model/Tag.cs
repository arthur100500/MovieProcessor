﻿using System.ComponentModel.DataAnnotations;
using Humanizer;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

[PrimaryKey(nameof(TagId))]
public record Tag(int TagId, string Name, ICollection<Movie> Movies)
{
    public Tag() : this(0, "", new HashSet<Movie>()) { }
    public ICollection<Movie> Movies { get; private set; } = Movies;
    public int TagId { get; private set; } = TagId;
    [StringLength(64)]
    public string Name { get; private set; }= Name.Truncate(64);

    public Tag WithNoLinks()
    {
        var copy = new Tag
        {
            TagId = TagId,
            Name = Name
        };
        return copy;
    }

    public int CountMovies(ApplicationContext context)
    {
        return context.TagsMovies.Count(x => x.TagId == TagId);
    }

    public IQueryable<Movie> GetMovies(ApplicationContext context)
    {
        var movies = context.TagsMovies
            .Where(p => p.TagId == TagId)
            .Join(context.Movies, l => l.MovieId, p => p.MovieId, (u, c) => c);
        return movies;
    }
}