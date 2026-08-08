// -------------------------------------------------------------------------------------
// <copyright file="ExecuteCommand.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Commands;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Reaper.Db;
using Reaper.Pruning;
using Reaper.Scanning;

using Spectre.Console;
using Spectre.Console.Cli;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class ExecuteCommand : Command<ExecuteCommand.Settings>
{
   protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
   {
      var root = Pipeline.ResolveRoot(settings.Root);
      if (!Pipeline.CheckSafety(root))
      {
         return 1;
      }

      if (!Pipeline.EnsureInitialized(root))
      {
         return 1;
      }

      var config = Pipeline.LoadConfig(root, settings.RetentionDays, settings.ConfigFile);

      if (settings.DryRun)
      {
         return Pipeline.Preview(root, config);
      }

      var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      var retention = config.RetentionDays * 86_400L;

      using var db = new ReaperDb(Path.Combine(root, Pipeline.DbFileName));

      var dbMap = db.GetAll().ToDictionary(e => e.Path);
      var fsMap = Scanner.Scan(root).ToDictionary(fe => fe.Path);

      RemoveOrphans(db, dbMap, fsMap);
      ReconcileTouchedEntries(db, dbMap, fsMap, now);

      var toDelete = Pruner.FlagForRemoval(dbMap.Values, retention, now);
      var (deletedCount, capReached) = DeleteFlagged(root, db, toDelete, config.MaxDeletesPerRun);

      if (config.DeleteEmptyDirs)
      {
         DeleteEmptyDirs(root);
      }

      var capNote = capReached ? $" [grey](cap of {config.MaxDeletesPerRun} reached)[/]" : "";
      AnsiConsole.MarkupLine($"[green]Done.[/] Deleted [bold]{deletedCount}[/] file(s){capNote}.");
      return 0;
   }

   [SuppressMessage(
      "ReSharper",
      "EmptyGeneralCatchClause",
      Justification = "A directory that fails to delete (race with something recreating it, "
                      + "permissions, etc.) is simply left in place; the next run retries.")]
   private static void DeleteEmptyDirs(string root)
   {
      foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                  .OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar)))
      {
         if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
         {
            try
            {
               Directory.Delete(dir);
            }
            catch
            {
            }
         }
      }
   }

   [SuppressMessage(
      "ReSharper",
      "EmptyGeneralCatchClause",
      Justification = "Deletion failures (locked file, permissions, AV, etc.) are expected and "
                      + "handled by design: retain the entry and let folder atomicity protect its ancestors. "
                      + "The next scheduled run retries once the lock is released.")]
   private static (int DeletedCount, bool CapReached) DeleteFlagged(
      string root,
      ReaperDb db,
      IEnumerable<string> toDelete,
      int cap)
   {
      var deleted = new List<string>();
      var attempted = 0;

      foreach (var relPath in toDelete)
      {
         if ((cap > 0) && (attempted >= cap))
         {
            break;
         }

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
      }

      return (deleted.Count, (cap > 0) && (attempted >= cap));
   }

   // Inserts newly discovered entries and resets the aging clock (not first_seen) for anything
   // whose timestamps advanced or size changed since it was last recorded.
   private static void ReconcileTouchedEntries(
      ReaperDb db,
      Dictionary<string, Entry> dbMap,
      Dictionary<string, FsEntry> fsMap,
      long now)
   {
      var toUpsert = new List<Entry>();
      foreach (var (path, fsEntry) in fsMap)
      {
         if (!dbMap.TryGetValue(path, out var dbEntry))
         {
            var e = new Entry(path, now, now, fsEntry.Size);
            toUpsert.Add(e);
            dbMap[path] = e;
         }
         else if ((fsEntry.MaxTimestamp > dbEntry.RefreshedAt) || (fsEntry.Size != dbEntry.Size))
         {
            // first_seen carries forward unchanged — only the aging clock resets
            var e = new Entry(path, dbEntry.FirstSeen, now, fsEntry.Size);
            toUpsert.Add(e);
            dbMap[path] = e;
         }
      }

      if (toUpsert.Count > 0)
      {
         db.Upsert(toUpsert);
      }
   }

   // Removes DB entries with no corresponding filesystem entry.
   private static void RemoveOrphans(ReaperDb db, Dictionary<string, Entry> dbMap, Dictionary<string, FsEntry> fsMap)
   {
      var orphans = dbMap.Keys.Except(fsMap.Keys).ToList();
      if (orphans.Count == 0)
      {
         return;
      }

      db.Delete(orphans);
      foreach (var o in orphans)
      {
         dbMap.Remove(o);
      }
   }

   [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
   public sealed class Settings : ConfigurableSettings
   {
      [CommandOption("--dry-run")]
      [Description("Preview what would be deleted without making any changes")]
      [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
      public bool DryRun { get; init; }
   }
}
