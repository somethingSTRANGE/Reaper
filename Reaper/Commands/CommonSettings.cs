using System.ComponentModel;
using Spectre.Console.Cli;

namespace Reaper.Commands;

public class PathSettings : CommandSettings
{
    [CommandArgument(0, "<root>")]
    [Description("Target folder to operate on")]
    public string Root { get; init; } = "";
}

public class ConfigurableSettings : PathSettings
{
    [CommandOption("--days|-d")]
    [Description("Retention threshold in days (overrides config file)")]
    public int? RetentionDays { get; init; }

    [CommandOption("--config")]
    [Description("Config file path (default: <root>/.reaper.toml)")]
    public string? ConfigFile { get; init; }
}
