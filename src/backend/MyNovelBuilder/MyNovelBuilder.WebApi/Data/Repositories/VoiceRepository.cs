using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for voices.
/// </summary>
public class VoiceRepository : Repository<Voice>, IVoiceRepository
{
    /// <summary></summary>
    public VoiceRepository(AppDbContext context) : base(context)
    {
    }
}
