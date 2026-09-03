using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Tests.Unit.Extensions;

public class StringExtensionsTests
{
    [Fact]
    public void AllIndexesOf_WhenSubstringExistsMultipleTimes_ReturnsAllIndexes()
    {
        // Arrange
        const string str = "the quick brown fox jumps over the lazy dog";
        const string value = "the";

        // Act
        var result = str.AllIndexesOf(value);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0]);
        Assert.Equal(31, result[1]);
    }

    [Fact]
    public void AllIndexesOf_WhenSubstringDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        const string str = "the quick brown fox";
        const string value = "cat";

        // Act
        var result = str.AllIndexesOf(value);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AllIndexesOf_WhenSubstringIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        const string str = "the quick brown fox";
        const string value = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => str.AllIndexesOf(value));
    }

    [Fact]
    public void AllIndexesOf_WithCaseInsensitiveComparison_ReturnsCorrectIndexes()
    {
        // Arrange
        const string str = "The Quick Brown Fox the";
        const string value = "the";

        // Act
        var result = str.AllIndexesOf(value, StringComparison.OrdinalIgnoreCase);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0]);
        Assert.Equal(20, result[1]);
    }
}
