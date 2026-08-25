using System.Linq;
using KCDMerge.Core.Data;
using Xunit;

namespace KCDMerge.Tests;

public class CfgMergerTests
{
    [Fact]
    public void CfgParser_ParsesEntriesAndComments()
    {
        string content = @"-- Graphics settings
r_vsync = 1
r_fullscreen = 1

-- Camera settings
cl_fov = 75
";
        CfgFile parsed = CfgParser.Parse(content);

        Assert.Equal(3, parsed.Entries.Count);
        Assert.Equal("r_vsync", parsed.Entries[0].Variable);
        Assert.Equal("1", parsed.Entries[0].Value);
        Assert.Single(parsed.Entries[0].Comments);
        Assert.Equal("-- Graphics settings", parsed.Entries[0].Comments[0]);

        Assert.Equal("cl_fov", parsed.Entries[2].Variable);
        Assert.Equal("75", parsed.Entries[2].Value);
    }

    [Fact]
    public void CfgParser_Serialize_RoundtripsCorrectly()
    {
        string content = @"-- FOV settings
cl_fov = 85
";
        CfgFile parsed = CfgParser.Parse(content);
        string serialized = CfgParser.Serialize(parsed);

        Assert.Contains("cl_fov = 85", serialized);
        Assert.Contains("-- FOV settings", serialized);
    }
}
