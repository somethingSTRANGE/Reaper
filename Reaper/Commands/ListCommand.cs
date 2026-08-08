// -------------------------------------------------------------------------------------
// <copyright file="ListCommand.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Commands;

using System.Diagnostics.CodeAnalysis;

using Reaper.Db;
using Reaper.Pruning;

using Spectre.Console;
using Spectre.Console.Cli;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class ListCommand : Command<ListCommand.Settings>
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
      var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      var retention = config.RetentionDays * 86_400L;

      using var db = new ReaperDb(Path.Combine(root, Pipeline.DbFileName));
      var entries = db.GetAll();

      Pipeline.PrintScheduleInfo(root);
      AnsiConsole.WriteLine();

      if (entries.Count == 0)
      {
         AnsiConsole.MarkupLine("[grey]No tracked entries.[/]");
         return 0;
      }

      var flaggedNow = Pruner.FlagForRemoval(entries, retention, now);

      var table = new Table().Border(TableBorder.Rounded);
      table.AddColumn("Path");
      table.AddColumn("First seen");
      table.AddColumn("Refreshed");
      table.AddColumn(new TableColumn("Size").RightAligned());
      table.AddColumn("Eligible");
      table.AddColumn("State");

      foreach (var entry in entries.OrderBy(e => e.RefreshedAt))
      {
         var firstSeenLocal = DateTimeOffset.FromUnixTimeSeconds(entry.FirstSeen).LocalDateTime;
         var refreshedLocal = DateTimeOffset.FromUnixTimeSeconds(entry.RefreshedAt).LocalDateTime;
         var eligibleLocal = refreshedLocal.AddSeconds(retention);
         var expiredByAge = now - entry.RefreshedAt > retention;

         var state = flaggedNow.Contains(entry.Path) ? "[red]reap next run[/]" :
            expiredByAge ? "[yellow]protected[/]" : "[grey]retained[/]";

         table.AddRow(
            Markup.Escape(entry.Path),
            firstSeenLocal.ToString("yyyy-MM-dd"),
            refreshedLocal.ToString("yyyy-MM-dd"),
            FormatSize(entry.Size),
            eligibleLocal.ToString("yyyy-MM-dd"),
            state);
      }

      AnsiConsole.Write(table);
      return 0;
   }

   private static string FormatSize(long bytes)
   {
      string[] units = ["B", "KB", "MB", "GB"];
      double size = bytes;
      var unit = 0;
      while ((size >= 1024) && (unit < units.Length - 1))
      {
         size /= 1024;
         unit++;
      }

      return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.#} {units[unit]}";
   }

   [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
   public sealed class Settings : ConfigurableSettings
   {
   }
}
