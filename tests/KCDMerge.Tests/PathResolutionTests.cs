using System;
using System.IO;
using KCDMerge.Core.Configuration;
using Xunit;

namespace KCDMerge.Tests;

public class PathResolutionTests
{
    [Fact]
    public void ResolvePath_ExpandsTildeToUserProfile()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string input = "~/.steam/steam/steamapps/common/KingdomComeDeliverance";
        string expected = Path.Combine(home, ".steam/steam/steamapps/common/KingdomComeDeliverance");

        string result = AppConfig.ResolvePath(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolvePath_StripsQuotes()
    {
        string input = "\"/home/test/Games/KCD\"";
        string expected = "/home/test/Games/KCD";

        string result = AppConfig.ResolvePath(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolvePath_ResolvesTempOnLinux()
    {
        string input = "%TEMP%\\KCDMerge";
        string result = AppConfig.ResolvePath(input, isTemp: true);

        if (!OperatingSystem.IsWindows())
        {
            string expected = Path.Combine(Path.GetTempPath(), "KCDMerge");
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void AppConfig_GetResolvedTempPath_ReturnsNonEmptyValidPath()
    {
        var config = new AppConfig();
        string tempPath = config.GetResolvedTempPath();

        Assert.False(string.IsNullOrWhiteSpace(tempPath));
        Assert.DoesNotContain("%TEMP%", tempPath);
    }
}
