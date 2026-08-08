// -------------------------------------------------------------------------------------
// <copyright file="FsEntry.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Scanning;

// Size is 0 for directory-type entries (symlinked/junction directories tracked as opaque
// entries) — they have no meaningful byte length, and reading a size through a reparse point
// risks resolving the link target, which Reaper must never do.
public record FsEntry(string Path, long MaxTimestamp, long Size);
