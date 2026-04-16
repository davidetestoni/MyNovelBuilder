using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;

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
    
    /// <summary>
    /// The voices in the database.
    /// </summary>
    public DbSet<Voice> Voices { get; init; }

    /// <summary>
    /// The linked media folders in the database.
    /// </summary>
    public DbSet<MediaFolder> MediaFolders { get; init; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<CharacterVoiceAssignment>();

        modelBuilder.Entity<Prompt>().Property(p => p.Messages)
            .HasConversion(new JsonValueConverter<IEnumerable<PromptMessage>>())
            .Metadata.SetValueComparer(new JsonValueComparer<IEnumerable<PromptMessage>>());

        modelBuilder.Entity<CompendiumRecord>().Property(p => p.CharacterVoiceAssignments)
            .HasConversion(new JsonValueConverter<IEnumerable<CharacterVoiceAssignment>>())
            .Metadata.SetValueComparer(new JsonValueComparer<IEnumerable<CharacterVoiceAssignment>>());
        
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
