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

        AnsiConsole.MarkupLine($"reap [yellow]{Markup.Escape(version)}[/]");
        AnsiConsole.MarkupLine($"[grey].NET {Environment.Version}[/]");
        return 0;
    }
}
