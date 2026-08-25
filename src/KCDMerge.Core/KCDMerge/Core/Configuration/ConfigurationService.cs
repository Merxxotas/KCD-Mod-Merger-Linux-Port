using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.Core.Configuration;

public class ConfigurationService : IConfigurationService
{
	private readonly string _configPath;

	private readonly ISerializer _serializer;

	private readonly IDeserializer _deserializer;

	private AppConfig? _cachedConfig;

	public ConfigurationService(string configPath = "config.yaml")
	{
		_configPath = Path.GetFullPath(configPath);
		_serializer = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();
		_deserializer = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
	}

	public AppConfig LoadConfiguration()
	{
		if (_cachedConfig != null)
		{
			return _cachedConfig;
		}
		AppConfig appConfig;
		if (!File.Exists(_configPath))
		{
			appConfig = new AppConfig();
		}
		else
		{
			try
			{
				string input = File.ReadAllText(_configPath);
				appConfig = _deserializer.Deserialize<AppConfig>(input) ?? new AppConfig();
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to load config from {ConfigPath}", _configPath);
				appConfig = new AppConfig();
			}
		}

		if (!string.IsNullOrWhiteSpace(appConfig.GamePath))
		{
			appConfig.GamePath = AppConfig.ResolvePath(appConfig.GamePath);
		}
		if (!string.IsNullOrWhiteSpace(appConfig.ModsPath))
		{
			appConfig.ModsPath = AppConfig.ResolvePath(appConfig.ModsPath);
		}

		if (string.IsNullOrWhiteSpace(appConfig.GamePath))
		{
			appConfig.GamePath = TryAutoDetectGamePath() ?? string.Empty;
		}
		if (string.IsNullOrWhiteSpace(appConfig.ModsPath) && !string.IsNullOrWhiteSpace(appConfig.GamePath))
		{
			appConfig.ModsPath = TryAutoDetectModsPath(appConfig.GamePath) ?? string.Empty;
		}
		_cachedConfig = appConfig;
		return appConfig;
	}

	private string? TryAutoDetectGamePath()
	{
		var candidatePaths = new List<string>();

		if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
		{
			string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			candidatePaths.AddRange(new[]
			{
				Path.Combine(home, ".steam/steam/steamapps/common/KingdomComeDeliverance"),
				Path.Combine(home, ".steam/root/steamapps/common/KingdomComeDeliverance"),
				Path.Combine(home, ".local/share/Steam/steamapps/common/KingdomComeDeliverance"),
				Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/KingdomComeDeliverance"),
				Path.Combine(home, ".var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/KingdomComeDeliverance"),
				Path.Combine(home, "Games/Heroic/KingdomComeDeliverance"),
				Path.Combine(home, "Games/kingdom-come-deliverance"),
				Path.Combine(home, "GOG Games/Kingdom Come Deliverance")
			});

			var vdfPaths = new[]
			{
				Path.Combine(home, ".steam/steam/steamapps/libraryfolders.vdf"),
				Path.Combine(home, ".local/share/Steam/steamapps/libraryfolders.vdf"),
				Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/libraryfolders.vdf")
			};

			foreach (var vdf in vdfPaths)
			{
				if (File.Exists(vdf))
				{
					try
					{
						foreach (var line in File.ReadAllLines(vdf))
						{
							int pathIdx = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
							if (pathIdx >= 0)
							{
								var parts = line.Substring(pathIdx + 6).Trim().Trim('"', '\t', ' ');
								if (!string.IsNullOrEmpty(parts) && Directory.Exists(parts))
								{
									candidatePaths.Add(Path.Combine(parts, "steamapps/common/KingdomComeDeliverance"));
								}
							}
						}
					}
					catch { }
				}
			}
		}

		candidatePaths.AddRange(new[]
		{
			"C:\\Program Files (x86)\\Steam\\steamapps\\common\\KingdomComeDeliverance",
			"C:\\Program Files\\Steam\\steamapps\\common\\KingdomComeDeliverance",
			"D:\\SteamLibrary\\steamapps\\common\\KingdomComeDeliverance",
			"E:\\SteamLibrary\\steamapps\\common\\KingdomComeDeliverance",
			"F:\\SteamLibrary\\steamapps\\common\\KingdomComeDeliverance",
			"G:\\SteamLibrary\\steamapps\\common\\KingdomComeDeliverance",
			"C:\\Program Files (x86)\\GOG Galaxy\\Games\\Kingdom Come Deliverance",
			"C:\\GOG Games\\Kingdom Come Deliverance",
			"D:\\GOG Games\\Kingdom Come Deliverance",
			"C:\\Program Files\\Epic Games\\KingdomComeDeliverance",
			"D:\\Epic Games\\KingdomComeDeliverance"
		});

		foreach (string text in candidatePaths.Distinct())
		{
			if (Directory.Exists(text) && Directory.Exists(Path.Combine(text, "Data")))
			{
				Log.Information("Auto-detected GamePath: {GamePath}", text);
				return text;
			}
		}
		return null;
	}

	private string? TryAutoDetectModsPath(string gamePath)
	{
		string text = Path.Combine(gamePath, "Mods");
		if (Directory.Exists(text))
		{
			Log.Information("Auto-detected ModsPath: {ModsPath}", text);
			return text;
		}

		string textLower = Path.Combine(gamePath, "mods");
		if (Directory.Exists(textLower))
		{
			Log.Information("Auto-detected ModsPath: {ModsPath}", textLower);
			return textLower;
		}

		return null;
	}

	public void SaveConfiguration(AppConfig config)
	{
		try
		{
			string yaml;
			if (File.Exists(_configPath))
			{
				yaml = File.ReadAllText(_configPath);
			}
			else
			{
				string path = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "config_template.yaml");
				yaml = (File.Exists(path) ? File.ReadAllText(path) : _serializer.Serialize(config));
			}
			yaml = ReplaceYamlValue(yaml, "GamePath", config.GamePath);
			yaml = ReplaceYamlValue(yaml, "ModsPath", config.ModsPath);
			File.WriteAllText(_configPath, yaml);
		}
		catch (Exception ex)
		{
			throw new IOException("Failed to save configuration to '" + _configPath + "': " + ex.Message, ex);
		}
	}

	private static string ReplaceYamlValue(string yaml, string key, string value)
	{
		string[] array = yaml.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].TrimEnd('\r').StartsWith(key + ":", StringComparison.Ordinal))
			{
				string text = (string.IsNullOrEmpty(value) ? "" : (" \"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
				array[i] = key + ":" + text;
				break;
			}
		}
		return string.Join('\n', array);
	}

	public bool ValidateConfiguration(out string error)
	{
		AppConfig appConfig = LoadConfiguration();
		if (string.IsNullOrWhiteSpace(appConfig.GamePath))
		{
			error = "GamePath is not configured.";
			return false;
		}
		if (!Directory.Exists(appConfig.GamePath))
		{
			error = "GamePath directory does not exist: " + appConfig.GamePath;
			return false;
		}
		try
		{
			Directory.GetDirectories(appConfig.GamePath).Take(1).ToArray();
		}
		catch (UnauthorizedAccessException)
		{
			error = "GamePath exists but is not accessible (permission denied): " + appConfig.GamePath;
			return false;
		}
		catch (IOException ex2)
		{
			error = $"GamePath exists but cannot be read: {appConfig.GamePath} ({ex2.Message})";
			return false;
		}
		if (!Directory.Exists(Path.Combine(appConfig.GamePath, "Data")))
		{
			error = "GamePath does not contain a 'Data' directory: " + appConfig.GamePath + ". Is this the correct game installation path?";
			return false;
		}
		if (string.IsNullOrWhiteSpace(appConfig.ModsPath))
		{
			error = "ModsPath is not configured.";
			return false;
		}
		if (!Directory.Exists(appConfig.ModsPath))
		{
			error = "ModsPath directory does not exist: " + appConfig.ModsPath;
			return false;
		}
		try
		{
			Directory.GetDirectories(appConfig.ModsPath).Take(1).ToArray();
		}
		catch (UnauthorizedAccessException)
		{
			error = "ModsPath exists but is not accessible (permission denied): " + appConfig.ModsPath;
			return false;
		}
		catch (IOException ex4)
		{
			error = $"ModsPath exists but cannot be read: {appConfig.ModsPath} ({ex4.Message})";
			return false;
		}
		error = string.Empty;
		return true;
	}
}
