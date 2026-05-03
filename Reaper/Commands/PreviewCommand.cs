using Spectre.Console.Cli;

namespace Reaper.Commands;

public sealed class PreviewCommand : Command<PreviewCommand.Settings>
{
    public sealed class Settings : ConfigurableSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = Pipeline.ResolveRoot(settings.Root);
        if (!Pipeline.CheckSafety(root))       return 1;
        if (!Pipeline.EnsureInitialized(root)) return 1;

        var config = Pipeline.LoadConfig(root, settings.RetentionDays, settings.ConfigFile);
        return Pipeline.Preview(root, config);
    }
}
