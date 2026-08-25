using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KCDMerge.Core.Data;

public static class CfgParser
{
	public static CfgFile Parse(string content)
	{
		if (string.IsNullOrEmpty(content))
		{
			return new CfgFile();
		}
		return Parse(content.Split(new string[3] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
	}

	public static CfgFile Parse(string[] lines)
	{
		CfgFile cfgFile = new CfgFile();
		List<string> list = new List<string>();
		foreach (string text in lines)
		{
			string text2 = text.Trim();
			if (string.IsNullOrEmpty(text2))
			{
				list.Add(string.Empty);
				continue;
			}
			if (text2.StartsWith("--"))
			{
				list.Add(text);
				continue;
			}
			int num = text2.IndexOf('=');
			if (num > 0)
			{
				string variable = text2.Substring(0, num).Trim();
				string value = text2.Substring(num + 1).Trim();
				CfgEntry item = new CfgEntry
				{
					Variable = variable,
					Value = value,
					Comments = new List<string>(list)
				};
				cfgFile.Entries.Add(item);
				list.Clear();
			}
			else
			{
				list.Add(text);
			}
		}
		cfgFile.TrailingLines = list;
		return cfgFile;
	}

	public static string Serialize(CfgFile cfg)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (CfgEntry entry in cfg.Entries)
		{
			foreach (string comment in entry.Comments)
			{
				stringBuilder.AppendLine(comment);
			}
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 2, stringBuilder2);
			handler.AppendFormatted(entry.Variable);
			handler.AppendLiteral(" = ");
			handler.AppendFormatted(entry.Value);
			stringBuilder2.AppendLine(ref handler);
		}
		foreach (string trailingLine in cfg.TrailingLines)
		{
			stringBuilder.AppendLine(trailingLine);
		}
		return stringBuilder.ToString();
	}

	public static CfgFile LoadFromFile(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return new CfgFile();
		}
		return Parse(File.ReadAllText(filePath, Encoding.UTF8));
	}

	public static void SaveToFile(CfgFile cfg, string filePath)
	{
		string contents = Serialize(cfg);
		File.WriteAllText(filePath, contents, Encoding.UTF8);
	}
}
