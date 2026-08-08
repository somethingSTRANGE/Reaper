// -------------------------------------------------------------------------------------
// <copyright file="InitCommand.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Commands;

using System.Diagnostics.CodeAnalysis;

using Reaper.Db;

using Spectre.Console;
using Spectre.Console.Cli;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class InitCommand : Command<InitCommand.Settings>
{
   protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
   {
      var root = Pipeline.ResolveRoot(settings.Root);

      if (!Pipeline.CheckSafety(root))
      {
         return 1;
      }

      if (!Directory.Exists(root))
      {
         AnsiConsole.MarkupLine($"[red]Error:[/] Directory does not exist: [grey]{Markup.Escape(root)}[/]");
         return 1;
      }

      var dbPath = Path.Combine(root, Pipeline.DbFileName);
      var tomlPath = Path.Combine(root, Pipeline.TomlFileName);

      if (File.Exists(dbPath))
      {
         AnsiConsole.MarkupLine($"[yellow]Already initialized:[/] [grey]{Markup.Escape(root)}[/]");
         return 0;
      }

      using (new ReaperDb(dbPath))
      {
      }

      if (!File.Exists(tomlPath))
      {
         File.WriteAllText(
            tomlPath,
            """
            retention_days    = 7
            delete_empty_dirs = true
            max_deletes_per_run = 0   # 0 = unlimited
            """);
      }

      AnsiConsole.MarkupLine($"[green]Initialized:[/] [grey]{Markup.Escape(root)}[/]");
      return 0;
   }

   [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
   public sealed class Settings : PathSettings
   {
   }
}
