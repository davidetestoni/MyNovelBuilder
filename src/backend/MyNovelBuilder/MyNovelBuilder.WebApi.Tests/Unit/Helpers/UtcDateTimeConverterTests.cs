using System.Text.Json;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Tests.Unit.Helpers;

public class UtcDateTimeConverterTests
{
    private readonly JsonSerializerOptions _options;

    public UtcDateTimeConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new UtcDateTimeConverter());
    }

    [Fact]
    public void Write_ConvertsToUtcAndFormatsAsIso8601()
    {
        // Arrange
        var date = new DateTime(2023, 10, 27, 10, 0, 0, DateTimeKind.Local);

        // Act
        var json = JsonSerializer.Serialize(date, _options);

        // Assert
        Assert.Contains(date.ToUniversalTime().Year.ToString(), json);
        Assert.Contains("Z", json);
    }

    [Fact]
    public void Read_ParsesIso8601String()
    {
        // Arrange
        const string json = "\"2023-10-27T10:00:00Z\"";
        var expected = new DateTime(2023, 10, 27, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = JsonSerializer.Deserialize<DateTime>(json, _options);

        // Assert
        Assert.Equal(expected.ToUniversalTime(), result.ToUniversalTime());
    }
}
