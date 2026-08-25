using System;
using System.IO;
using System.Xml.Linq;
using Serilog;

namespace KCDMerge.Core.Services;

public class ModManifestGenerator
{
	private readonly GameVersionDetector _versionDetector;

	public ModManifestGenerator()
	{
		_versionDetector = new GameVersionDetector();
	}

	public void GenerateManifest(string outputModPath, string gamePath, string modName = "KCDMerge")
	{
		string text = _versionDetector.GetGameVersion(gamePath) ?? "1.9.6.0";
		string majorVersion = GetMajorVersion(text);
		XDocument xDocument = new XDocument(new XElement("kcd_mod", new XElement("info", new XElement("name", modName), new XElement("description", "Merged mod created by KCDMerge tool"), new XElement("author", "KCDMerge"), new XElement("version", "1.0"), new XElement("created_on", DateTime.Now.ToString("MM-dd-yyyy")), new XElement("modid", modName.ToLower().Replace(" ", "_")), new XElement("gameVersion", text)), new XElement("supports", new XElement("kcd_version", majorVersion + ".*"))));
		string text2 = Path.Combine(outputModPath, "mod.manifest");
		try
		{
			xDocument.Save(text2);
			Log.Information("Created mod.manifest at: {ManifestPath}", text2);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to create mod.manifest");
		}
	}

	private string GetMajorVersion(string version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return "1";
		}
		string[] array = version.Split('.');
		if (array.Length == 0)
		{
			return "1";
		}
		return array[0];
	}
}
