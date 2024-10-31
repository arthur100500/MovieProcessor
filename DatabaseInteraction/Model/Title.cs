using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

[PrimaryKey(nameof(Id))]
public record Title(string Name, Movie self)
{
    public int Id { get; set; }
    [ForeignKey(nameof(Movie.MovieId))] public int MovieId { get; set; } = self.MovieId;
    [StringLength(64)] public string Name { get; set; } = Name;
    public Title() : this("", new Movie()) { }
}