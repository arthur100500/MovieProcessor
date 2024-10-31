using DatabaseInteraction.Link;
using Microsoft.EntityFrameworkCore;

namespace DatabaseInteraction;

public class ApplicationContext : DbContext
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ActorsMoviesLinks> ActorsMovies => Set<ActorsMoviesLinks>();
    public DbSet<DirectorsMoviesLinks> DirectorsMovies => Set<DirectorsMoviesLinks>();
    public DbSet<TagsMoviesLinks> TagsMovies => Set<TagsMoviesLinks>();

    public ApplicationContext() => Database.EnsureCreated();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=movies.db");
        optionsBuilder.EnableSensitiveDataLogging();
    }

    public int GetTableRowsCount<T>(DbSet<T> set) where T : class
    {
        return set.Count();
    }

    public int ExecuteSql(string query) => Database.ExecuteSqlRaw(query);


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>()
            .HasMany(b => b.Actors)
            .WithMany(a => a.Movies)
            .UsingEntity<ActorsMoviesLinks>(
                r => r.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId),
                l => l.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId));

        modelBuilder.Entity<Movie>()
            .HasMany(b => b.Directors)
            .WithMany(a => a.Movies)
            .UsingEntity<DirectorsMoviesLinks>(
                r => r.HasOne(x => x.Director).WithMany().HasForeignKey(x => x.DirectorId),
                l => l.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId));

        modelBuilder.Entity<Movie>()
            .HasMany(b => b.Tags)
            .WithMany(a => a.Movies)
            .UsingEntity<TagsMoviesLinks>(
                r => r.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId),
                l => l.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId));

        modelBuilder.Entity<Title>()
            .Property(f => f.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<ActorsMoviesLinks>()
            .Property(f => f.ActorsMoviesLinkId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<DirectorsMoviesLinks>()
            .Property(f => f.DirectorsMoviesLinkId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<TagsMoviesLinks>()
            .Property(f => f.TagsMoviesId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Tag>(f => f.ToTable("Tags"));
    }
}