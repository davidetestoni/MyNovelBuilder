using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// A value converter that converts a JSON string to a value of type <typeparamref name="T"/>.
/// </summary>
public class JsonValueConverter<T> : ValueConverter<T, string>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    /// <summary></summary>
    public JsonValueConverter() : base(
        v => JsonSerializer.Serialize(v, _jsonSerializerOptions),
        v => JsonSerializer.Deserialize<T>(v, _jsonSerializerOptions)!)
    {
        
    }
}
