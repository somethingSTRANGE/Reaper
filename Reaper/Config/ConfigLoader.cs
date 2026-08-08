// -------------------------------------------------------------------------------------
// <copyright file="ConfigLoader.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Config;

using System.Diagnostics.CodeAnalysis;

using Tomlyn;

public static class ConfigLoader
{
   private const bool DefaultDeleteEmptyDirs = true;

   private const int DefaultMaxDeletesPerRun = 0;

   private const int DefaultRetentionDays = 7;

   public static ReaperConfig Load(string tomlPath, CliOverrides? overrides = null)
   {
      TomlModel? toml = null;
      if (File.Exists(tomlPath))
      {
         toml = TomlSerializer.Deserialize<TomlModel>(File.ReadAllText(tomlPath));
      }

      return new ReaperConfig(
         RetentionDays: overrides?.RetentionDays ?? toml?.retention_days ?? DefaultRetentionDays,
         DeleteEmptyDirs: toml?.delete_empty_dirs ?? DefaultDeleteEmptyDirs,
         MaxDeletesPerRun: toml?.max_deletes_per_run ?? DefaultMaxDeletesPerRun);
   }

   // Property names deliberately match the .reaper.toml keys verbatim (snake_case) — Tomlyn's
   // deserializer does exact, case-sensitive name matching, not snake_case-to-PascalCase
   // convention conversion. Renaming these to PascalCase silently breaks config loading.
   [SuppressMessage("ReSharper", "InconsistentNaming")]
   [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
   private class TomlModel
   {
      [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
      public bool? delete_empty_dirs { get; set; }

      [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
      public int? max_deletes_per_run { get; set; }

      [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
      public int? retention_days { get; set; }
   }
}
