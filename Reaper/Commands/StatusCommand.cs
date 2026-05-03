using Reaper.Db;
using Reaper.Pruning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Reaper.Commands;

public sealed class StatusCommand : Command<StatusCommand.Settings>
{
    public sealed class Settings : ConfigurableSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = Pipeline.ResolveRoot(settings.Root);
        if (!Pipeline.CheckSafety(root))       return 1;
        if (!Pipeline.EnsureInitialized(root)) return 1;

        var config    = Pipeline.LoadConfig(root, settings.RetentionDays, settings.ConfigFile);
        var now       = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var retention = (long)config.RetentionDays * 86_400L;

        using var db   = new ReaperDb(Path.Combine(root, Pipeline.DbFileName));
        var entries    = db.GetAll();
        var pruneCount = Pruner.FlagForRemoval(entries, retention, now).Count;

        var oldestLabel = entries.Count > 0
            ? DateTimeOffset.FromUnixTimeSeconds(entries.Min(e => e.FirstSeen))
                            .LocalDateTime.ToString("yyyy-MM-dd")
            : "—";

        var table = new Table().Border(TableBorder.None).HideHeaders();
        table.AddColumn(new TableColumn("").RightAligned());
        table.AddColumn("");
        table.AddRow("[grey]Target[/]",          Markup.Escape(root));
        table.AddRow("[grey]Tracked entries[/]",  entries.Count.ToString());
        table.AddRow("[grey]Oldest entry[/]",     oldestLabel);
        table.AddRow("[grey]Retention[/]",        $"{config.RetentionDays}d");
        table.AddRow("[grey]Would prune[/]",      $"[yellow]{pruneCount}[/] file(s)");

        AnsiConsole.Write(table);
        return 0;
    }
}
