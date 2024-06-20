using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// A JSON converter for UTC dates.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    /// <inheritdoc/>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var date = reader.GetString()!;
        return DateTime.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
    }
}
