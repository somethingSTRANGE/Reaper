# Reaper

<img align="left" src="assets/ReaperIcon.png" width="110" alt="Reaper">

**Age-based pruning for scratch and temp directories.**

Reaper tracks when it first observed each file in a target folder, then removes files that have gone untouched beyond a configurable retention period. It uses a small SQLite database per folder rather than relying on filesystem timestamps, which are unreliable as age signals on Windows.

## Why not filesystem timestamps?

Windows filesystem timestamps are misleading in too many common situations to be trusted as age signals:

- **Same-volume move** — creation and modified timestamps are preserved; the file appears as old as its origin
- **Cross-volume move** — creation is updated but modified still reflects the original; age is ambiguous
- **Archive extraction** — modified typically reflects the timestamp stored inside the archive, not when it was extracted
- **LastAccess** — nominally disabled by default on modern Windows (`NtfsDisableLastAccessUpdate`), but not reliably so in practice; AV scans, indexing, and even routine reads have been observed to update it anyway. Reaper does not use it as a signal.
- **Folder LastAccess** — reading a file inside a folder does not reliably update the folder's own timestamp

Reaper sidesteps all of this. The `first_seen` timestamp in the database is set to **now** when a file is first observed, and reset to **now** if its creation or modified timestamp advances (indicating external modification). Everything else ages from when Reaper first noticed it.

## How it works

On each `execute` run:

1. Abort if the folder has not been initialised — `reap init` must be run first
2. Scan all files recursively (excluding `.reaper.db`, `.reaper.toml`, and `desktop.ini` at the root level)
3. Remove database entries for files that no longer exist
4. For each file on disk:
   - Not in database → record with `first_seen = now` and `refreshed_at = now`; no deletion this run
   - In database, filesystem timestamps advanced or file size changed → reset `refreshed_at = now` (`first_seen` is untouched — it's a permanent record of when Reaper first saw the file); no deletion this run
   - In database, `refreshed_at` older than the retention threshold → flag for removal
5. **Folder atomicity pass** — if any file in a subtree is retained, clear removal flags on all ancestors and their other contents
6. Delete flagged files; remove any directories that are now empty

### Folder atomicity

A single retained file protects its entire ancestor chain. You never end up with a half-deleted project folder:

```
Scratch/
  ProjectFoo/          ← retained (Bar/ is retained)
    Bar/               ← retained (baz.txt is retained)
      baz.txt          ← 2 days old → kept
      old.log          ← 30 days old → would be removed, but Bar/ is protected
    other.txt          ← 30 days old → would be removed, but ProjectFoo/ is protected
  StaleStuff/          ← all contents expired → deleted
```

## Installation

Download `reap.exe` and `reap-silent.exe` from the [Releases](../../releases) page. Both are self-contained single-file executables targeting Windows x64 — no installer, no runtime dependency.

Place both files in the same folder on your `PATH` (e.g. `C:\Tools\`). They must live together: `reap-silent.exe` is a thin launcher used with Task Scheduler that delegates to `reap.exe` in the same directory.

## Quick start

```
reap init D:\Scratch\Temp
reap execute D:\Scratch\Temp
```

The first `execute` records every file with `first_seen = now` and deletes nothing. Files begin expiring after the retention period (default: 7 days).

Typical targets include personal scratch and temp folders, or anywhere files accumulate without a clear expiry:

```
reap init %USERPROFILE%\Temp        # working files, short retention
reap init %USERPROFILE%\Scratch     # project scratch space
reap init %USERPROFILE%\Downloads   # auto-prune old downloads
```

### Automating with Task Scheduler

Use `reap-silent.exe` rather than `reap.exe` when scheduling. It starts `reap.exe` without allocating a console window, so nothing flashes on screen during scheduled runs.

**`schtasks` (one-liner):**

```
schtasks /create /tn "Reaper - Temp" /tr "\"C:\Tools\reap-silent.exe\" execute \"D:\Scratch\Temp\"" /sc daily /st 03:00
```

**PowerShell:**

```powershell
$action   = New-ScheduledTaskAction -Execute 'C:\Tools\reap-silent.exe' -Argument 'execute "D:\Scratch\Temp"'
$trigger  = New-ScheduledTaskTrigger -Daily -At '3:00AM'
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName 'Reaper - Temp' -Action $action -Trigger $trigger -Settings $settings
```

Exit codes are the only signal when running under Task Scheduler — any non-zero exit indicates an error.

## Commands

### `reap version`

Print version and runtime info.

### `reap init <path>`

Initialise a folder for tracking. Creates `.reaper.db` and a default `.reaper.toml` in the target folder. Safe to run on an already-initialised folder (no-op, exits 0).

All other commands abort with an error if the folder has not been initialised.

### `reap status <path> [flags]`

Show a summary of the database state: entry count, oldest recorded file, and how many files would be pruned at the current threshold.

### `reap preview <path> [flags]`

List exactly which files would be deleted, grouped by directory. Does **not** update the database — files with recently changed filesystem timestamps will not have their clocks reset until `execute` runs.

> Preview reflects the current database state. New files and recently modified files are not yet recorded — `execute` may retain more folders than shown here.

### `reap list <path> [flags]`

List every tracked entry with `first_seen`, `refreshed_at`, size, age, computed eligibility date, and current state (retained / protected by folder atomicity / will reap next run). Also shows any Task Scheduler task detected as targeting this folder, with its next/last run time — useful for confirming Reaper is actually scheduled to run before wondering why nothing's been pruned.

### `reap execute <path> [flags]`

Perform the prune. Reconciles the database with the filesystem, resets clocks on touched files, then deletes anything that has aged out. Pass `--dry-run` to get preview output without making any changes.

### `reap touch <root> <target>`

Reset the aging clock (`refreshed_at`) to now for a specific file or directory within the database at `<root>`. `first_seen` is never modified — it stays a permanent record of when Reaper first saw the path. If `<target>` is a directory, all entries under it are reset. `<target>` may be an absolute path or relative to `<root>`.

Useful for protecting a folder from pruning without modifying any files inside it.

### Common flags

All commands that take `<path>`:

| Flag | Default | Description |
|---|---|---|
| `--days N` / `-d N` | 7 | Retention threshold in days |
| `--config <file>` | `<path>/.reaper.toml` | Explicit config file location |

`execute` only:

| Flag | Default | Description |
|---|---|---|
| `--dry-run` | false | Preview what would be deleted without making changes |

## Configuration

Each tracked folder may contain a `.reaper.toml` alongside its database. `reap init` creates one with defaults:

```toml
retention_days    = 7
delete_empty_dirs = true
max_deletes_per_run = 0   # 0 = unlimited
```

CLI flags override config file values.

| Option | Type | Default | Description |
|---|---|---|---|
| `retention_days` | integer | `7` | Days a file must go unmodified before it is eligible for removal |
| `delete_empty_dirs` | bool | `true` | Remove directories that become empty after file deletions |
| `max_deletes_per_run` | integer | `0` | Cap on file deletions per run; `0` means unlimited |

## Design notes

**First run** — the first `execute` on a freshly initialised folder records all files with `first_seen = now` and `refreshed_at = now`, and deletes nothing. Reaper needs at least one full retention period of observation before it removes anything.

**`first_seen` vs `refreshed_at`** — `first_seen` is set once and never changes again; it's a pure audit trail. `refreshed_at` is the actual aging clock: it starts equal to `first_seen`, and resets independently whenever an external touch is detected. `reap list` shows both, plus the computed eligibility date, so you can see e.g. a file first seen a month ago whose clock was refreshed last week — evidence something touched it, without losing the original history.

**Symlinks** — never followed. A symlink is tracked as an opaque file (it ages, it can be deleted) but Reaper never traverses into a symlinked directory or resolves the target. This is a hard invariant, not a configuration option.

**Locked files** — Reaper does not pre-check locks before attempting deletion (pre-checks are a race condition). If a deletion fails, the file is treated as retained and folder atomicity protects its ancestors. It will be retried on the next run once the lock is released.

**Nested tracked folders** — if a subfolder has its own `.reaper.db`, the outer Reaper instance tracks the inner `.reaper.db` as a regular file. When the inner `execute` runs, it updates the database file's modified timestamp; the outer instance detects this as a recent touch and resets the clock, protecting the entire inner subtree via folder atomicity. An abandoned inner database will naturally age out along with its folder.

**Protected paths** — Reaper refuses to operate on system directories. Drive roots and the following paths are blocked, along with everything underneath them: `%WINDIR%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`, and `%ProgramData%`. `%USERPROFILE%` itself is also blocked, but its subdirectories are not — `%USERPROFILE%\Temp`, `%USERPROFILE%\Scratch`, `%USERPROFILE%\Downloads`, and similar are the primary intended use cases.

**Replacing files with older versions** — Reaper also compares file size, which catches most cases where a file is overwritten with a version whose timestamps are preserved from elsewhere (e.g. extracted from an archive) but whose byte size differs. It won't catch a same-size in-place edit with preserved timestamps — a narrow remaining gap. Full content hashing would close it but requires reading every tracked file on every run, which trades away the cheap metadata-only scan for a real, ongoing I/O cost; not worth it for a scratch-folder pruning tool.

**Inspecting the database** — `.reaper.db` is a standard unencrypted SQLite file. You can open it with [DB Browser for SQLite](https://sqlitebrowser.org/) or query it with the `sqlite3` command-line tool.

## Building from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet publish Reaper -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
dotnet publish Reaper.Silent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

Published binaries appear at `Reaper\bin\Release\net10.0\win-x64\publish\reap.exe` and `Reaper.Silent\bin\Release\net10.0\win-x64\publish\reap-silent.exe`.

## License

MIT — see [LICENSE](LICENSE).
