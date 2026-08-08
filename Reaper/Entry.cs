// -------------------------------------------------------------------------------------
// <copyright file="Entry.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper;

// FirstSeen is immutable after insert — a pure audit trail of when Reaper first noticed this
// path. RefreshedAt is the mutable aging clock: it drives retention eligibility and resets on
// insert or external touch. Keeping them separate lets `reap list` show both a file's true
// history and why its clock was reset, instead of overwriting the history every time.
public record Entry(string Path, long FirstSeen, long RefreshedAt, long Size);
