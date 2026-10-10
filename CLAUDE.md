# Robocpy_marscom — Claude Code Guide

Backs up local folders into Dropbox with `robocopy`. The repo holds two tools:

| Tool | Where | Status |
|---|---|---|
| **MachineBackup** (C#, RoboSharp) | `src/MachineBackup/` | Current. Per-machine profiles, change tables, dry runs. |
| Legacy batch scripts | `BackupToDropox.bat`, `BackupToDropox-Safe.bat` | Older; still work. One fixed source list → `%USERPROFILE%\Dropbox\Marscom`. |

`README.md` is the user-facing manual for MachineBackup (options, output format, exit codes).
This file covers what you need to change code safely.

---

## MachineBackup

### Files

| File | Role |
|---|---|
| `Program.cs` | Argument parsing, settings loading, plan validation, one `RoboCommand` per source. Also holds the settings model and `BackupSourceConverter`. |
| `ChangeReport.cs` | Classifies RoboSharp's processed-file events into actions and prints the bordered tables. |
| `appsettings.json` | Machine profiles (`Lenovo`, `Asus`, `HomePc`). Copied to the build output. |
| `Backup-<Machine>.bat` / `Backup-<Machine>-Mirror.bat` | Double-click launchers (`dotnet run`, then `pause`). |
| `tests/SmokeTest.ps1` | End-to-end test against isolated temp folders. |

Targets `net8.0`; the only package is `RoboSharp`.

### Configuration model

```json
"Asus": {
  "SourcePaths": [
    "%USERPROFILE%\\Documents",
    { "Path": "%USERPROFILE%\\.codex\\skills", "As": "codex-skills" }
  ],
  "DestinationPath": "%USERPROFILE%\\Dropbox\\Dropbox\\Asus_Expertbook_P1503CVA_Marscom"
}
```

- Each source lands in `DestinationPath\<name>`. `<name>` is `As` when set, otherwise the
  source folder's last path segment.
- A `SourcePaths` entry is **either a plain string or a `{Path, As}` object**, mixed freely.
  `BackupSourceConverter` handles both forms; keep plain strings working, since the Lenovo and
  HomePc profiles rely on them.
- Use `As` when two sources share a leaf name (`.claude\skills` and `.codex\skills` are both
  `skills`) or when the backup folder should be named differently from the source.

### Validation runs before any copying — keep it that way

`Program.cs` checks the entire plan up front and throws `ArgumentException` (exit 1) on:
missing source folders, unresolved `%VAR%`, relative paths, drive roots without `As`, an `As`
that is a path rather than one folder name, **duplicate destination names**, and any overlap
between a source and the destination root. The duplicate-name check prevents two sources from
silently merging into one backup folder; it compares the *resolved* name (`As` or leaf).

### Machine profiles

| Profile | Destination root | Notes |
|---|---|---|
| `Asus` | `%USERPROFILE%\Dropbox\Dropbox\Asus_Expertbook_P1503CVA_Marscom` | This PC. Same 6 sources as the legacy batch scripts (see below). Launchers pass `--machine asus` explicitly. |
| `Lenovo` | `...\Dropbox\Dropbox\LenovoT480sWin11` | Uses literal `C:\Users\jc\...` paths, which are that machine's real profile. |
| `HomePc` | `...\Dropbox\Dropbox\HomePc` | No launchers. |

The doubled `Dropbox\Dropbox\` is the real Dropbox root on these machines, not a typo. The legacy
scripts write to a different root (`Dropbox\Marscom`).

### Adding a machine

1. Add a section under `Machines` in `appsettings.json`.
2. Copy the two `Backup-Lenovo*.bat` launchers, rename them, and pass `--machine <name>`
   explicitly (don't rely on `Environment.MachineName` matching the section name).
3. `dotnet run --project src/MachineBackup -- --machine <name> --dry-run` to check the plan.

### Building

```powershell
dotnet build src/MachineBackup
./tests/SmokeTest.ps1
```

**NuGet restore can fail with `NU1301 ... 401 (Unauthorized)`.** The user's global NuGet config
includes a private Azure DevOps feed (`pkgs.dev.azure.com/iprojekt/.../Ip.Core`) that this
project doesn't need. Restore from nuget.org only, then build without restoring:

```powershell
dotnet restore src/MachineBackup --source https://api.nuget.org/v3/index.json
dotnet build src/MachineBackup --no-restore
```

Don't edit the user's global NuGet config to work around this.

### Testing safely

- Always `--dry-run` first against real profiles; it passes `/L`, so nothing is copied or
  deleted. Never run `--mirror` against real folders to test a change.
- `SmokeTest.ps1` builds fixtures in a unique temp folder and points the app at them with
  `--settings`, so it's safe to run.
- When piping the app's output in PowerShell, don't use `Select-Object -First N`. It stops the
  pipeline and kills the process mid-run, giving a misleading exit code. Write output to a file
  and read that instead.

### Known issue: wrong "Destination path" column on accented usernames

The table can show paths like `Dropbox\Dropbox\JánCvengrosMarsCom\Documents\...`. Robocopy's
console output is decoded with the OEM codepage, which turns `š` into `s` and mangles accented
filenames (`Snímek` → `Sni´me...`). `ChangeReport.Classify` then calls
`Path.GetRelativePath(source, loggedPath)`, finds no shared prefix, and builds a wrong path.

**Only the report is affected.** The copy itself receives the correct Unicode paths (the job
header lines prove it). It never appears on Lenovo because its username (`jc`) has no accents.
The probable fix is to have robocopy emit Unicode output. Unfixed for now.

---

## Legacy batch scripts

| Script | Mode | Deletes from Dropbox? |
|---|---|---|
| `BackupToDropox.bat` | Mirror (`/MIR`) | **Yes** |
| `BackupToDropox-Safe.bat` | Additive (`/E`), plus a list-only `/MIR /L` pass that only *reports* extras | **No** |

Both write to `%USERPROFILE%\Dropbox\Marscom` from the same six sources, `SRC1`–`SRC6`:

1. `%USERPROFILE%\Documents` → `Documents`
2. `%USERPROFILE%\.claude\skills` → `skills`
3. `C:\Projects\Testing\GraveyardsMvc\Graveyards\Graveyards.Web\Properties\PublishProfiles` → `GraveyardsPublishProfiles`
4. `%USERPROFILE%\Pictures` → `Pictures`
5. `%USERPROFILE%\Videos` → `Videos`
6. `%USERPROFILE%\.codex\skills` → `codex-skills`

When changing these, keep both scripts in sync (same sources, same order) and renumber every
`[n/N]` counter. The Asus profile above mirrors this list; update it too if the intent is to keep
them matching.

---

## Conventions & gotchas (both tools)

- **Use `%USERPROFILE%`, never the literal accented username.** `cmd.exe` reads `.bat` files in
  the legacy OEM codepage and mangles `á`/`š`/`č`, giving `ERROR 3 – cannot find the path
  specified`. In `appsettings.json` it's about consistency and portability. (Lenovo's literal
  `C:\Users\jc` is fine: no accents, and it's that machine's real path.)
- **Keep junction skipping** (`/XJ`, `ExcludeJunctionPoints = true`). `Documents`, `Pictures` and
  `Videos` contain hidden legacy junctions (`My Music`, `My Pictures`, `My Videos`) that deny access
  with `ERROR 5` and can cause runaway recursion.
- **Robocopy exit code ≥ 8 is a real failure**; 0–7 are normal success states.
- Legacy summary grep matches `"EXTRA File"` **without** a leading `*`, because robocopy pads the
  tag into a column.
- Preserve the `Dropox` spelling in the legacy script filenames; it's what the user runs.
