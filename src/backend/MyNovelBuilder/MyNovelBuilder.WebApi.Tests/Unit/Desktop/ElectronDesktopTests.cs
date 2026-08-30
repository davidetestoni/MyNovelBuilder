using MyNovelBuilder.WebApi.Desktop;

namespace MyNovelBuilder.WebApi.Tests.Unit.Desktop;

public class ElectronDesktopTests
{
    [Theory]
    [InlineData("-unpackeddotnet")]
    [InlineData("--unpackeddotnet")]
    [InlineData("-dotnetpacked")]
    [InlineData("--dotnetpacked")]
    [InlineData("/electronPort=12345")]
    [InlineData("/ELECTRONPORT=12345")]
    public void IsRequested_RecognizesElectronLaunchArguments(string argument)
    {
        Assert.True(ElectronDesktop.IsRequested([argument]));
    }

    [Theory]
    [InlineData()]
    [InlineData("--environment=Development")]
    [InlineData("--data-dir", "/tmp/my novels")]
    [InlineData("-unpackedelectron")]
    public void IsRequested_LeavesNormalWebLaunchesAlone(params string[] arguments)
    {
        Assert.False(ElectronDesktop.IsRequested(arguments));
    }
}
