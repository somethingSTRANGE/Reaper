// -------------------------------------------------------------------------------------
// <copyright file="CommonSettings.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Commands;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Spectre.Console.Cli;

public class PathSettings : CommandSettings
{
   [CommandArgument(0, "<root>")]
   [Description("Target folder to operate on")]
   public string Root { get; init; } = "";
}

public class ConfigurableSettings : PathSettings
{
   [CommandOption("--config")]
   [Description("Config file path (default: <root>/.reaper.toml)")]
   [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
   public string? ConfigFile { get; init; }

   [CommandOption("--days|-d")]
   [Description("Retention threshold in days (overrides config file)")]
   [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
   public int? RetentionDays { get; init; }
}
