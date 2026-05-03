# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project: Reaper

A CLI tool for age-based pruning of scratch/temp directories. It maintains a local SQLite database per target folder to track "first-seen" timestamps, sidestepping the unreliability of native filesystem timestamps.

## Why a Database Instead of Filesystem Timestamps

Filesystem timestamps are untrustworthy as age signals:

- **Same-volume move**: creation and modified timestamps are preserved — the file appears older than it is
- **Cross-volume move**: equivalent to copy+delete, so creation is touched but modified is still the original
- **Archive extraction**: creation may be touched, but modified typically reflects the timestamp stored inside the archive
- **LastAccess on Windows**: `NtfsDisableLastAccessUpdate` is enabled by default on modern Windows; file reads do not update LastAccess
- **Folder LastAccess**: reading a file inside a folder does not reliably update the folder's own LastAccess stamp; folder timestamps are especially unreliable

The DB timestamp (`first_seen`, set to `NOW` on initial observation) is the authoritative age signal. FS timestamps are only consulted as signals that a file has been *externally modified* since it was first seen.

## Core Algorithm

On each `execute` run against a target `<root>`:

1. Load `<root>/.reaper.db` (create if missing, equivalent to `init`)
2. Scan all FS entries under `<root>` recursively (excluding `.reaper.db` and `.reaper.toml`)
3. **Orphan cleanup**: remove DB entries with no corresponding FS entry
4. **Per-entry evaluation**:
   - Not in DB → insert with `first_seen = NOW`; no removal action this run
   - In DB and `max(fs.created, fs.modified, fs.accessed) > db.first_seen` → update `first_seen = NOW` (file was externally touched); no removal action this run
   - In DB and `first_seen` is older than the retention threshold → flag for removal
5. **Folder atomicity pass**: walk flags upward — if any file in a subtree is flagged for *retention* (i.e., not flagged for removal), clear removal flags on all ancestors up to `<root>` and all other entries under those ancestors
6. Delete all remaining flagged files, then delete any resulting empty directories (bottom-up)

## Folder Atomicity Rule

Retention is contagious upward. A single non-expired file protects its entire ancestor chain.

```
Temp/
  ProjectFoo/          ← retained (because Bar/ is retained)
    Bar/               ← retained (because baz.txt is retained)
      baz.txt          ← first_seen = 2 days ago → retained
      old.log          ← first_seen = 30 days ago → would be removed, but Bar/ is retained
    other.txt          ← first_seen = 30 days ago → would be removed, but ProjectFoo/ is retained
  StaleStuff/          ← all contents expired → deleted
```

This treats subdirectories as atomic units. You never end up with a half-deleted project folder.

## Database Schema

Single SQLite file at `<root>/.reaper.db`. Always excluded from pruning logic.

```sql
CREATE TABLE entries (
    path       TEXT    PRIMARY KEY,  -- relative to root, forward slashes, no leading slash
    first_seen INTEGER NOT NULL,     -- Unix epoch seconds; set to NOW on first observation or external touch
    updated_at INTEGER NOT NULL      -- Unix epoch seconds; last time this row was written
);
```

## CLI Commands

| Command | Description |
|---|---|
| `reap version` | Print version and build info |
| `reap status <path>` | Show DB stats: entry count, oldest entry, how many would be pruned at current threshold |
| `reap preview <path>` | Read-only dry-run: list exactly what would be deleted, grouped by directory. Does **not** update DB timestamps — even files with newer FS timestamps will not have their `first_seen` reset until `execute` runs. |
| `reap execute <path>` | Perform the prune |
| `reap touch <root> <target>` | Reset `first_seen` to NOW for `<target>` (file or directory) within the DB at `<root>`. If `<target>` is a directory, resets all entries under it. `<target>` may be absolute or relative to `<root>`. |
| `reap init <path>` | Create `.reaper.db` and `.reaper.toml` in the target folder without scanning |

**Common flags** (all commands that take `<path>`):

| Flag | Default | Description |
|---|---|---|
| `--days N` / `-d N` | 7 | Retention threshold in days |
| `--dry-run` | false | Alias for `preview` behavior when passed to `execute` |
| `--config <file>` | `<path>/.reaper.toml` | Explicit config file location |

## Per-Folder Configuration

`.reaper.toml` sits alongside `.reaper.db` and allows different folders to have different behavior without CLI flags. CLI flags override config file values.

```toml
retention_days = 7
delete_empty_dirs = true
max_deletes_per_run = 0   # 0 = unlimited; set a positive number as a safety cap
```

## Technology Stack

**Language**: C# / .NET — self-contained single-file publish (`dotnet publish --self-contained -r win-x64 -p:PublishSingleFile=true`) produces a standalone `.exe` with no runtime dependency.

**Key dependencies** (all MIT licensed):
- `Spectre.Console` — rich terminal output (tables, trees, progress); the `Spectre.Console.Cli` sub-package provides the CLI command structure
- `Microsoft.Data.Sqlite` — official thin SQLite wrapper
- `Tomlyn` — TOML config parsing

## Build & Run

```sh
dotnet build
dotnet run --project Reaper -- preview <target-folder>
dotnet test
dotnet test --filter "FullyQualifiedName~Pruner"   # run a specific test class
dotnet publish Reaper -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Project Layout

```
Reaper/
  Commands/       # one file per CLI command (StatusCommand, PreviewCommand, etc.)
  Db/             # SQLite access, schema, migrations
  Scanner/        # recursive FS walk, entry diffing
  Pruner/         # flagging logic, folder atomicity pass, deletion (namespace: Reaper.Pruning)
  Config/         # .reaper.toml loading and merging with CLI flags
Reaper.Tests/
```

## Design Notes & Edge Cases

- **The DB file itself** (`<root>/.reaper.db`) must always be excluded from all scans and deletion logic. Same for `.reaper.toml`.
- **Nested tracked folders**: do *not* explicitly exclude subdirectories that contain their own `.reaper.db`. The outer DB tracks the inner `.reaper.db` as a regular file. When the inner `execute` runs, it updates `.reaper.db`'s modified timestamp; the outer DB detects this as a recent touch and resets the clock, protecting the entire inner subtree via folder atomicity. An abandoned inner DB (whose `execute` is never run) will naturally age out and be cleaned up by the outer DB — explicit exclusion would prevent this and require manual deletion.
- **Symlinks**: never follow — hardcoded, not configurable. A symlink is tracked as an opaque file entry (it ages, it can be deleted) but Reaper never traverses into a symlinked directory or resolves what a symlink points to. This is a safety invariant, not a preference.
- **Empty directory deletion**: after file deletions, walk bottom-up and remove any directories that are now empty. This is a separate post-deletion pass, not part of the flagging logic.
- **`touch` semantics**: `reap touch <root> <target>` updates the DB entry only, not filesystem timestamps. No need to modify FS timestamps.
- **Locked files**: do not pre-check file locks before attempting deletion (pre-checks are a TOCTOU race anyway). On deletion failure, treat the file as retained — folder atomicity then protects its ancestors. Do not remove the DB entry unless deletion succeeds. Partial folder deletion is acceptable; the next scheduled run will complete cleanup once the lock is released.
- **Dual execution context**: the tool runs both interactively (terminal) and unattended (Windows Task Scheduler). Spectre.Console auto-detects TTY and strips rich formatting when stdout is not a terminal — no mode-switching needed. In Task Scheduler, all output is discarded; **exit codes are the only signal**, so all error conditions must return a non-zero exit code even when no one will see the message.
- **No disk logging**: not in scope. Deletions are not logged to file. The value of an audit trail is low, and `preview` covers the interactive "what would be removed" use case. If a log is ever wanted, redirect stdout from the Task Scheduler action.
- **Protected path checks**: validated at startup before any DB or FS operation. Resolve all protected paths at runtime via `Environment.GetFolderPath` / `Environment.GetEnvironmentVariable` — never hardcode paths, since `%USERPROFILE%` and others may not be under `C:\Users\`. Block execution if the target:
  - equals any protected path, or
  - is an ancestor of any protected path (e.g. targeting `D:\Profiles` when `%USERPROFILE%` is `D:\Profiles\mryan` must be blocked)
  - is a drive root (`C:\`, `D:\`, etc.)

  Paths to protect: `%WINDIR%`, `%SystemRoot%`, `%USERPROFILE%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%ProgramData%`, and all drive roots. Targeting a *descendant* of a protected path (e.g. `%USERPROFILE%\Temp`) is the primary intended use case and is explicitly allowed.
- **Safety cap**: `max_deletes_per_run` counts individual file deletions. When the count reaches the limit, stop — no special folder-level logic. Folder atomicity only protects folders that contain at least one *retained* file; it does not prevent partial deletion of entirely stale folders. A folder whose flagged files are split across two runs by the cap is fine — the remainder is deleted on the next run. The cap and the atomicity rule are orthogonal.
- **First run behavior**: an uninitialized folder gets a DB created and all entries timestamped NOW. Nothing is deleted on the first run. This is intentional — Reaper needs at least one full retention period to observe before it removes anything.
