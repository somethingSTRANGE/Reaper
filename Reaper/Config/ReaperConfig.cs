// -------------------------------------------------------------------------------------
// <copyright file="ReaperConfig.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Config;

public record ReaperConfig(int RetentionDays, bool DeleteEmptyDirs, int MaxDeletesPerRun);
