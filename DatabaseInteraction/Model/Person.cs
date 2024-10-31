using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

[PrimaryKey(nameof(PersonId))]
public record Person(int PersonId, string PrimaryName, ICollection<Movie> Movies)
{
    public Person() : this(0, "", new HashSet<Movie>()) { }
    public int PersonId { get; private set; } = PersonId;
    [StringLength(64)]
    public string PrimaryName { get; private set; } = PrimaryName;
    public ICollection<Movie> Movies { get; private set; } = Movies;

    public Person WithNoLinks()
    {
        var copy = new Person
        {
            PersonId = PersonId,
            PrimaryName = PrimaryName
        };
        return copy;
    }
}