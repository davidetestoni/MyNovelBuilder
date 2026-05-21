using MyNovelBuilder.WebApi.Services.VideoGeneration;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class DeApiVideoRequestSizingTests
{
    [Theory]
    [InlineData(832, 1248, 683, 1024)]
    [InlineData(1248, 832, 1024, 683)]
    [InlineData(1024, 1024, 1024, 1024)]
    [InlineData(512, 768, 512, 768)]
    public void FitWithinLimits_ClampsToDeApiBounds_WhilePreservingAspectRatio(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        var result = DeApiVideoRequestSizing.FitWithinLimits(width, height);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.True(result.Width <= DeApiVideoRequestSizing.MaxDimension);
        Assert.True(result.Height <= DeApiVideoRequestSizing.MaxDimension);
    }

    [Theory]
    [InlineData(0, 512)]
    [InlineData(512, 0)]
    public void FitWithinLimits_ThrowsForNonPositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeApiVideoRequestSizing.FitWithinLimits(width, height));
    }
}
