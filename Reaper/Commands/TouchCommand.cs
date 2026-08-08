// -------------------------------------------------------------------------------------
// <copyright file="TouchCommand.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Commands;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Reaper.Db;

using Spectre.Console;
using Spectre.Console.Cli;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class TouchCommand : Command<TouchCommand.Settings>
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

      var relTarget = Pipeline.ResolveRelativeTarget(root, settings.Target);
      var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

      using var db = new ReaperDb(Path.Combine(root, Pipeline.DbFileName));
      var count = db.Touch(relTarget, now);

      if (count == 0)
      {
         AnsiConsole.MarkupLine($"[yellow]No entries matched[/] [grey]{Markup.Escape(relTarget)}[/].");
      }
      else
      {
         AnsiConsole.MarkupLine(
            $"[green]Reset[/] [bold]{count}[/] entr{(count == 1 ? "y" : "ies")} "
            + $"for [grey]{Markup.Escape(relTarget)}[/].");
      }

      return 0;
   }

   [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
   public sealed class Settings : CommandSettings
   {
      [CommandArgument(0, "<root>")]
      [Description("Tracked folder containing the database")]
      public string Root { get; init; } = "";

      [CommandArgument(1, "<target>")]
      [Description("File or directory to reset (absolute or relative to <root>)")]
      public string Target { get; init; } = "";
   }
}
