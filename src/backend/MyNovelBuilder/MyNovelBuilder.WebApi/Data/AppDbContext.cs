using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Data;

/// <summary>
/// The application database context.
/// </summary>
public class AppDbContext : DbContext
{
    /// <inheritdoc />
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    /// <summary>
    /// The novels in the database.
    /// </summary>
    public DbSet<Novel> Novels { get; init; }
    
    /// <summary>
    /// The compendiums in the database.
    /// </summary>
    public DbSet<Compendium> Compendiums { get; init; }
    
    /// <summary>
    /// The compendium records in the database.
    /// </summary>
    public DbSet<CompendiumRecord> CompendiumRecords { get; init; }
    
    /// <summary>
    /// The prompts in the database.
    /// </summary>
    public DbSet<Prompt> Prompts { get; init; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Prompt>().OwnsMany(
            prompt => prompt.Messages, builder => builder.ToJson());
        
        // Use UTC for DateTime
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime)
                    || property.ClrType == typeof(DateTime?))
                {
                    modelBuilder.Entity(entityType.Name)
                        .Property(property.Name)
                        .HasConversion(new UniversalDateTimeConverter());
                }
            }
        }
    }
}
