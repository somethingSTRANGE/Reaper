using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Reaper.Commands;

public sealed class VersionCommand : Command<VersionCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var asm     = typeof(VersionCommand).Assembly;
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        ?.InformationalVersion
                     ?? asm.GetName().Version?.ToString()
                     ?? "unknown";

        var buildTimeRaw = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                               .FirstOrDefault(a => a.Key == "BuildTime")?.Value;
        var buildTimeLabel = DateTimeOffset.TryParse(buildTimeRaw, out var buildTime)
            ? buildTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "unknown";

        AnsiConsole.MarkupLine($"reap [yellow]{Markup.Escape(version)}[/]");
        AnsiConsole.MarkupLine($"[grey].NET {Environment.Version}[/]");
        AnsiConsole.MarkupLine($"[grey]Built {buildTimeLabel}[/]");
        return 0;
    }
}
