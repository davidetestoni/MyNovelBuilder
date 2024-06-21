using MyNovelBuilder.WebApi.Data.Repositories;

namespace MyNovelBuilder.WebApi.Data;

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
    /// Repository for compendia.
    /// </summary>
    ICompendiumRepository Compendia { get; }
    
    /// <summary>
    /// Repository for compendium records.
    /// </summary>
    ICompendiumRecordRepository CompendiumRecords { get; }
    
    /// <summary>
    /// Repository for prompts.
    /// </summary>
    IPromptRepository Prompts { get; }
    
    /// <summary>
    /// Complete the current transaction.
    /// </summary>
    Task<int> SaveChangesAsync();
}
