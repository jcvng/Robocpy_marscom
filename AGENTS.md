# Repository Guidelines

## Project Structure & Module Organization

This repository contains two Windows batch scripts that back up six local folders to `%USERPROFILE%\Dropbox\Marscom`:

- `BackupToDropox.bat` mirrors sources with `/MIR`, deleting destination-only files.
- `BackupToDropox-Safe.bat` adds and updates files with `/E`, then reports destination-only files using `/MIR /L`.
- `CLAUDE.md` documents source mappings and robocopy behavior.
- `.gitignore` excludes Windows metadata and `*.log` files.

There are no separate source, test, or asset directories. Preserve the existing `Dropox` filenames when referencing the scripts.

## Build, Test, and Development Commands

No build, package installation, or compilation is required. Use Windows with `cmd.exe` and `robocopy` available.

- `cmd /c BackupToDropox-Safe.bat` runs the additive backup and displays a change summary.
- `cmd /c BackupToDropox.bat` runs the destructive mirror backup.
- `git diff --check` checks changes for whitespace errors.

Both scripts pause before closing. Before executing either for development, create temporary copies and redirect every source and destination to disposable fixture folders. Do not validate changes against personal folders or live Dropbox data.

## Coding Style & Naming Conventions

Follow existing batch syntax: uppercase configuration variables (`DEST`, `SRC1`), lowercase subroutine labels (`:copy`, `:mirror`), and four-space indentation inside parenthesized blocks. Use `set "NAME=value"` and quote path arguments.

Use `%USERPROFILE%` instead of literal usernames; accented characters in batch source can cause codepage-related path failures. Preserve `/XJ` to skip junctions. Keep source mappings, ordering, and numbered progress counters synchronized across both scripts. No formatter or linter is configured.

## Testing Guidelines

There is no automated framework or coverage requirement. Manually test disposable fixtures containing new files, updated files, and destination-only files. Verify Safe retains extras, Mirror removes them, and summaries reflect each case. Check missing-source failures and preserve robocopy's failure threshold of exit codes 8 or higher. Summary matching must use `EXTRA File` without a leading asterisk.

## Commit & Pull Request Guidelines

History uses concise imperative subjects, such as `Add .codex/skills as sixth backup source`. Follow this style. Pull requests should explain changed paths or flags, describe deletion behavior, and record fixture validation. Update `CLAUDE.md` when mappings or operating behavior change. Never commit personal backup contents, credentials, or temporary logs.
