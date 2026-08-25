using System.Collections.Generic;
using System.Xml.Linq;
using KCDMerge.Core.Data;
using Xunit;

namespace KCDMerge.Tests;

public class PtfGeneratorTests
{
    [Fact]
    public void GeneratePtf_GeneratesPatchDocumentForModifiedAndNewRows()
    {
        string vanillaXml = @"<table name=""test"">
  <rows>
    <row id=""1"" name=""Item A"" value=""10"" />
    <row id=""2"" name=""Item B"" value=""20"" />
  </rows>
</table>";

        string mergedXml = @"<table name=""test"">
  <rows>
    <row id=""1"" name=""Item A"" value=""999"" />
    <row id=""2"" name=""Item B"" value=""20"" />
    <row id=""3"" name=""Item C"" value=""50"" />
  </rows>
</table>";

        var vanillaDoc = XDocument.Parse(vanillaXml);
        var mergedDoc = XDocument.Parse(mergedXml);
        var ptfGen = new PtfGenerator();

        var result = ptfGen.GeneratePtf(mergedDoc, vanillaDoc, new List<string> { "id" }, "test.xml");

        Assert.False(result.RequiresFullXmlFallback);
        Assert.NotNull(result.PtfDoc);
        Assert.Equal(1, result.ModifiedRows);
        Assert.Equal(1, result.AddedRows);
        Assert.Equal(1, result.UnchangedRows);
    }
}
