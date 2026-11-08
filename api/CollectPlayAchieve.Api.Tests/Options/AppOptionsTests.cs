using CollectPlayAchieve.Api.Options;

namespace CollectPlayAchieve.Api.Tests.Options;

internal class AppOptionsTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task GetDataFolderFullPath_WhenDataFolderIsNotConfigured_UsesDefault(
        string? dataFolder
    )
    {
        var options = new AppOptions { DataFolder = dataFolder };
        var expectedPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data"));

        await Assert.That(options.GetDataFolderFullPath()).IsEqualTo(expectedPath);
    }

    [Test]
    [Arguments("~")]
    [Arguments("~/cpa-data")]
    [Arguments(@"~\cpa-data")]
    public async Task GetDataFolderFullPath_WhenPathUsesHomeDirectoryShorthand_ResolvesWithinHome(
        string configuredPath
    )
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var options = new AppOptions { DataFolder = configuredPath };
        var expectedPath =
            configuredPath == "~"
                ? Path.GetFullPath(homeDirectory)
                : Path.GetFullPath(Path.Combine(homeDirectory, configuredPath[2..]));

        await Assert.That(options.GetDataFolderFullPath()).IsEqualTo(expectedPath);
    }

    [Test]
    public async Task GetDataFolderFullPath_WhenPathIsAbsolute_AcceptsPath()
    {
        var absolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cpa-absolute-data"));
        var options = new AppOptions { DataFolder = absolutePath };

        await Assert.That(options.GetDataFolderFullPath()).IsEqualTo(absolutePath);
    }

    [Test]
    [Arguments("relative")]
    [Arguments("relative/data")]
    public async Task GetDataFolderFullPath_WhenPathIsRelative_Throws(string configuredPath)
    {
        var options = new AppOptions { DataFolder = configuredPath };

        await Assert.That(options.GetDataFolderFullPath).Throws<InvalidOperationException>();
    }
}
