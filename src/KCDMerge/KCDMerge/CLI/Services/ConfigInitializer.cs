using System.IO;
using KCDMerge.Core.Configuration;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.CLI.Services;

public class ConfigInitializer
{
	public bool InitializeConfig(AppConfig config, string configPath, ConfigurationService configService)
	{
		bool flag = false;
		bool flag2 = File.Exists(configPath);
		string? value = null;
		string? value2 = null;
		if (flag2)
		{
			try
			{
				string input = File.ReadAllText(configPath);
				AppConfig? appConfig = new DeserializerBuilder()
					.WithNamingConvention(PascalCaseNamingConvention.Instance)
					.IgnoreUnmatchedProperties()
					.Build()
					.Deserialize<AppConfig>(input);
				value = appConfig?.GamePath;
				value2 = appConfig?.ModsPath;
			}
			catch
			{
				value = null;
				value2 = null;
			}
		}
		if (!string.IsNullOrWhiteSpace(config.GamePath))
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				AnsiConsole.MarkupLine("[yellow]Auto-detected GamePath:[/] " + config.GamePath);
				if (!AnsiConsole.Confirm("Use this path?"))
				{
					string entered = AnsiConsole.Ask<string>("[yellow]Enter GamePath:[/]");
					config.GamePath = AppConfig.ResolvePath(entered);
				}
				flag = true;
			}
		}
		else
		{
			AnsiConsole.MarkupLine("[red]Could not auto-detect GamePath.[/]");
			string entered = AnsiConsole.Ask<string>("[yellow]Enter GamePath:[/]");
			config.GamePath = AppConfig.ResolvePath(entered);
			flag = true;
		}
		if (!string.IsNullOrWhiteSpace(config.ModsPath))
		{
			if (string.IsNullOrWhiteSpace(value2))
			{
				AnsiConsole.MarkupLine("[yellow]Auto-detected ModsPath:[/] " + config.ModsPath);
				if (!AnsiConsole.Confirm("Use this path?"))
				{
					string entered = AnsiConsole.Ask<string>("[yellow]Enter ModsPath:[/]");
					config.ModsPath = AppConfig.ResolvePath(entered);
				}
				flag = true;
			}
		}
		else
		{
			AnsiConsole.MarkupLine("[red]Could not auto-detect ModsPath.[/]");
			string entered = AnsiConsole.Ask<string>("[yellow]Enter ModsPath:[/]");
			config.ModsPath = AppConfig.ResolvePath(entered);
			flag = true;
		}
		if (flag || !flag2)
		{
			configService.SaveConfiguration(config);
			AnsiConsole.MarkupLine("[green]Configuration saved to config.yaml[/]");
		}
		if (!configService.ValidateConfiguration(out string error))
		{
			AnsiConsole.MarkupLine("[bold red]Configuration Error:[/] " + error);
			AnsiConsole.MarkupLine("[yellow]The path you entered does not exist or is invalid.[/]");
			AnsiConsole.MarkupLine("[yellow]Please edit config.yaml manually or delete it and run again.[/]");
			return false;
		}
		return true;
	}
}
