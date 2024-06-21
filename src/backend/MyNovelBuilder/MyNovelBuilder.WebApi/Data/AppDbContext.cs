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
    /// The compendia in the database.
    /// </summary>
    public DbSet<Compendium> Compendia { get; init; }
    
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
    
    /// <inheritdoc />
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e is { Entity: TimestampedEntity, State: EntityState.Added or EntityState.Modified });

        foreach (var entry in entries)
        {
            var entity = (TimestampedEntity)entry.Entity;
            var now = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
            }
            
            entity.UpdatedAt = now;
        }
    }
}
