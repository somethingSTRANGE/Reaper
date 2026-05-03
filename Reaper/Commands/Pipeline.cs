using Reaper.Config;
using Reaper.Db;
using Reaper.Pruning;
using Reaper.Safety;
using Spectre.Console;

namespace Reaper.Commands;

public static class Pipeline
{
    public const string DbFileName   = ".reaper.db";
    public const string TomlFileName = ".reaper.toml";

    public static string ResolveRoot(string rawPath) => Path.GetFullPath(rawPath);

    public static bool CheckSafety(string absoluteRoot)
    {
        if (!SafetyChecker.IsProtected(absoluteRoot)) return true;
        AnsiConsole.MarkupLine(
            $"[red]Error:[/] [grey]{Markup.Escape(absoluteRoot)}[/] is a protected system path.");
        return false;
    }

    public static bool EnsureInitialized(string absoluteRoot)
    {
        if (File.Exists(Path.Combine(absoluteRoot, DbFileName))) return true;
        AnsiConsole.MarkupLine(
            $"[red]Error:[/] [grey]{Markup.Escape(absoluteRoot)}[/] is not tracked. " +
            $"Run [yellow]reap init \"{Markup.Escape(absoluteRoot)}\"[/] first.");
        return false;
    }

    public static ReaperConfig LoadConfig(string absoluteRoot, int? daysOverride, string? configFile)
    {
        var tomlPath  = configFile ?? Path.Combine(absoluteRoot, TomlFileName);
        var overrides = daysOverride.HasValue ? new CliOverrides(daysOverride) : null;
        return ConfigLoader.Load(tomlPath, overrides);
    }

    public static string ResolveRelativeTarget(string absoluteRoot, string target)
    {
        var abs = Path.IsPathRooted(target)
            ? target
            : Path.GetFullPath(Path.Combine(absoluteRoot, target));
        return Path.GetRelativePath(absoluteRoot, abs).Replace('\\', '/');
    }

    public static int Preview(string absoluteRoot, ReaperConfig config)
    {
        var now       = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var retention = (long)config.RetentionDays * 86_400L;

        using var db = new ReaperDb(Path.Combine(absoluteRoot, DbFileName));
        var entries  = db.GetAll();
        var toDelete = Pruner.FlagForRemoval(entries, retention, now);

        if (toDelete.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing to delete.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine(
            $"[yellow]Preview[/] — [bold]{toDelete.Count}[/] file(s) would be deleted " +
            $"[grey](retention: {config.RetentionDays}d)[/]\n");

        var byEntry     = entries.ToDictionary(e => e.Path);
        var printedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in toDelete.OrderBy(p => p))
        {
            var parts = path.Split('/');

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var dirPath = string.Join('/', parts[..(i + 1)]);
                if (printedDirs.Add(dirPath))
                    AnsiConsole.MarkupLine(
                        $"{new string(' ', (i + 1) * 2)}[blue]{Markup.Escape(parts[i])}/[/]");
            }

            var age = byEntry.TryGetValue(path, out var entry)
                ? (int)((now - entry.FirstSeen) / 86_400)
                : 0;
            AnsiConsole.MarkupLine(
                $"{new string(' ', parts.Length * 2)}{Markup.Escape(parts[^1])}  [grey]{age}d[/]");
        }

        return 0;
    }
}
