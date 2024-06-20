using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Converter for DateTime to UTC.
/// </summary>
public class UniversalDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UniversalDateTimeConverter"/> class.
    /// </summary>
    public UniversalDateTimeConverter()
        : base(
            // Convert DateTime to database
            v => v.ToUniversalTime(),
            // Convert DateTime from database
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            new ConverterMappingHints(size: 0))
    {
        
    }
}
