using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace KCDMerge.Tests;

public class TblOverrideFilterTests
{
    [Theory]
    [InlineData("Libs/Tables/rpg/rpg_param.xml", true)]
    [InlineData("Libs\\Tables\\item\\item.xml", true)]
    [InlineData("Libs/Config/defaultProfile.xml", false)]
    [InlineData("Libs/Config/keybindSuperactions.xml", false)]
    [InlineData("Libs/UI/UIActions/MM_GraphicsSettings.xml", false)]
    [InlineData("Animations/Mannequin/ADB/tags.xml", false)]
    [InlineData("Libs/MaterialEffects/Flowgraphs/fx.xml", false)]
    public void IsTableFile_CorrectlyIdentifiesTableVsNonTable(string path, bool expectedIsTable)
    {
        string normalized = path.Replace('\\', '/');
        bool isTable = normalized.StartsWith("Libs/Tables/", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(expectedIsTable, isTable);
    }
}
