namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Interface for the Unit of Work pattern.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Repository for novels.
    /// </summary>
    INovelRepository Novels { get; }
    
    /// <summary>
    /// Repository for compendiums.
    /// </summary>
    ICompendiumRepository Compendiums { get; }
    
    /// <summary>
    /// Complete the current transaction.
    /// </summary>
    Task<int> SaveChangesAsync();
}
