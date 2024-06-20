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
    public DbSet<NovelEntity> Novels { get; init; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
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
