using System.ComponentModel;
using Reaper.Db;
using Reaper.Pruning;
using Reaper.Scanning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Reaper.Commands;

public sealed class ExecuteCommand : Command<ExecuteCommand.Settings>
{
    public sealed class Settings : ConfigurableSettings
    {
        [CommandOption("--dry-run")]
        [Description("Preview what would be deleted without making any changes")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = Pipeline.ResolveRoot(settings.Root);
        if (!Pipeline.CheckSafety(root))       return 1;
        if (!Pipeline.EnsureInitialized(root)) return 1;

        var config = Pipeline.LoadConfig(root, settings.RetentionDays, settings.ConfigFile);

        if (settings.DryRun)
            return Pipeline.Preview(root, config);

        var now       = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var retention = (long)config.RetentionDays * 86_400L;

        using var db = new ReaperDb(Path.Combine(root, Pipeline.DbFileName));

        var dbMap = db.GetAll().ToDictionary(e => e.Path);
        var fsMap = Scanner.Scan(root).ToDictionary(fe => fe.Path);

        var orphans = dbMap.Keys.Except(fsMap.Keys).ToList();
        if (orphans.Count > 0)
        {
            db.Delete(orphans);
            foreach (var o in orphans) dbMap.Remove(o);
        }

        var toUpsert = new List<Entry>();
        foreach (var (path, fsEntry) in fsMap)
        {
            if (!dbMap.TryGetValue(path, out var dbEntry))
            {
                var e = new Entry(path, now, now);
                toUpsert.Add(e);
                dbMap[path] = e;
            }
            else if (fsEntry.MaxTimestamp > dbEntry.FirstSeen)
            {
                var e = new Entry(path, now, now);
                toUpsert.Add(e);
                dbMap[path] = e;
            }
        }
        if (toUpsert.Count > 0)
            db.Upsert(toUpsert);

        var toDelete  = Pruner.FlagForRemoval(dbMap.Values, retention, now);
        var cap       = config.MaxDeletesPerRun;
        var deleted   = new List<string>();
        var attempted = 0;

        foreach (var relPath in toDelete)
        {
            if (cap > 0 && attempted >= cap) break;
            attempted++;

            var absPath = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                File.Delete(absPath);
                deleted.Add(relPath);
            }
            catch
            {
                // Locked or otherwise undeletable — retain; folder atomicity protects
                // ancestors on the next run once the lock is released.
            }
        }

        if (deleted.Count > 0)
        {
            db.Delete(deleted);
            if (config.DeleteEmptyDirs)
                DeleteEmptyAncestors(root, deleted);
        }

        var capNote = cap > 0 && attempted >= cap ? $" [grey](cap of {cap} reached)[/]" : "";
        AnsiConsole.MarkupLine($"[green]Done.[/] Deleted [bold]{deleted.Count}[/] file(s){capNote}.");
        return 0;
    }

    private static void DeleteEmptyAncestors(string root, IEnumerable<string> deletedRelPaths)
    {
        var dirs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in deletedRelPaths)
        {
            var parts = p.Split('/');
            for (var i = 1; i < parts.Length; i++)
                dirs.Add(string.Join('/', parts[..i]));
        }

        foreach (var dir in dirs.OrderByDescending(d => d.Length))
        {
            var absDir = Path.Combine(root, dir.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(absDir) &&
                !Directory.EnumerateFileSystemEntries(absDir).Any())
            {
                try { Directory.Delete(absDir); } catch { }
            }
        }
    }
}
