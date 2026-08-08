// -------------------------------------------------------------------------------------
// <copyright file="PreviewCommand.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Commands;

using System.Diagnostics.CodeAnalysis;

using Spectre.Console.Cli;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class PreviewCommand : Command<PreviewCommand.Settings>
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
      return Pipeline.Preview(root, config);
   }

   [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
   public sealed class Settings : ConfigurableSettings
   {
   }
}
