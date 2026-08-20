# Robocpy_marscom — Claude Code Guide

Two Windows batch scripts that back up several local folders into Dropbox using `robocopy`.
Destination for everything: `%USERPROFILE%\Dropbox\Marscom`.

## Files

| Script | Mode | Deletes from Dropbox? |
|---|---|---|
| `BackupToDropox.bat` | Mirror (`/MIR`) | **Yes** — Dropbox is made to exactly match the source; files removed from source are deleted in Dropbox. |
| `BackupToDropox-Safe.bat` | Additive (`/E`) | **No** — nothing is ever deleted. A second list-only (`/MIR /L`) pass only *reports* files present in Dropbox but not in source. |

Both scripts print an end-of-run **CHANGE SUMMARY** with three sections: ADDED (`New File`),
CHANGED (`Newer`), and REMOVED / EXTRA (`EXTRA File`), produced by grepping a temp robocopy log.

## Sources (identical in both scripts, `SRC1`–`SRC5`)

1. `%USERPROFILE%\Documents` → `Documents`
2. `%USERPROFILE%\.claude\skills` → `skills`
3. `C:\Projects\Testing\GraveyardsMvc\Graveyards\Graveyards.Web\Properties\PublishProfiles` → `GraveyardsPublishProfiles`
4. `%USERPROFILE%\Pictures` → `Pictures`
5. `%USERPROFILE%\Videos` → `Videos`

## Conventions & gotchas

- **Use `%USERPROFILE%`, never the literal username.** The account name contains accented
  characters (á, š, č); `cmd.exe` reads `.bat` files in the legacy OEM codepage and mangles them,
  producing `ERROR 3 – cannot find the path specified`. `%USERPROFILE%` is expanded at runtime and
  sidesteps this entirely. Apply the same rule to any new source path under the user profile.
- **`/XJ` (skip junctions) is required.** `Documents`, `Pictures`, `Videos` contain hidden legacy
  junctions (`My Music`, `My Pictures`, `My Videos`) that deny access and throw `ERROR 5`; `/XJ`
  skips them. Junctions can also cause runaway recursion — always keep `/XJ`.
- **Summary match tag is `"EXTRA File"` without a leading `*`.** robocopy pads the tag into a
  column, so `findstr /c:"*EXTRA File"` fails to match. Do not re-add the asterisk.
- **`errorlevel >= 8`** is a real robocopy failure (codes 0–7 are normal success/copied/extra states).
  Each step checks `if errorlevel 8 goto :failed`.
- Shared robocopy flags: `/DCOPY:T /R:1 /W:1 /NP /NDL /NC /NS /TEE` plus `/LOG+` to the temp log.

## Adding a new source

1. Add `set "SRCn=..."` (prefer `%USERPROFILE%\...`) near the top of **both** scripts.
2. Add a numbered `echo [n/N] ...` + `call :mirror` / `call :copy` block, and renumber the `/N`
   counters in every step.
3. Keep the mirror and safe scripts in sync — same sources, same order.

## Running

Double-click either `.bat`, or run from a terminal. The window stays open (`pause`) so the change
summary is readable. Run **Safe** routinely; run **Mirror** when you want Dropbox cleaned up to match
source — review the EXTRA list from a Safe run first, since Mirror will delete exactly those files
(Dropbox keeps deletions recoverable for ~30 days).
