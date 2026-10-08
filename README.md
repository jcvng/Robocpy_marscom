# MachineBackup

Windows console application using [RoboSharp](https://github.com/PCAssistSoftware/RoboSharp) to back up multiple folders. Requires the .NET 8 runtime to run or a .NET 8 or newer SDK to build. The original batch scripts remain available.

## Configure

Edit `src/MachineBackup/appsettings.json`. `Machines` contains named profiles such as `Lenovo`, `Asus`, and `HomePc`. Example:

```json
{
  "Machines": {
    "Lenovo": {
      "SourcePaths": ["C:\\Projects", "%USERPROFILE%\\Documents"],
      "DestinationPath": "%USERPROFILE%\\Dropbox\\Dropbox\\LenovoT480sWin11"
    }
  }
}
```

Each source goes into a folder named after its last path segment: `Projects` and `Documents` in this example. Source folders must exist and have distinct names. Sources and the destination must not overlap. Paths must be absolute; environment variables such as `%USERPROFILE%` are expanded at runtime. JSON requires backslashes to be doubled. The sample profiles are examples: adjust them for your PCs before running.

## Run

```powershell
dotnet run --project src/MachineBackup -- --list-machines
dotnet run --project src/MachineBackup -- --machine Lenovo --dry-run
dotnet run --project src/MachineBackup -- --machine Lenovo
dotnet run --project src/MachineBackup -- --machine Asus
dotnet run --project src/MachineBackup -- --machine HomePc
```

Select a profile with either a positional name (`lenovo`) or `--machine Lenovo`. Both are case-insensitive. Specify the machine only once. If omitted, the app uses `Environment.MachineName`: name the configuration section after the actual Windows PC name to enable automatic selection. An unknown machine produces an error and lists the available profiles.

When your terminal is already in `src\MachineBackup`, run:

```powershell
dotnet run -- lenovo --dry-run
dotnet run -- lenovo
```

To run the Lenovo backup by double-clicking, open `src/MachineBackup/Backup-Lenovo.bat`. For mirror mode, open `src/MachineBackup/Backup-Lenovo-Mirror.bat`; this deletes destination-only files and folders. Both locate the project relative to the batch file and keep the console window open after completion. They require the .NET SDK, just like `dotnet run`. Windows desktop shortcuts can point to these batch files.

By default, settings are loaded from `appsettings.json` beside the executable, independent of the current working directory. Use `--settings "C:\\path\\appsettings.json"` to select another file. `--help` shows all options.

The default additive mode copies new and updated files and empty folders, preserving destination-only files. `--mirror` makes each destination subfolder match its source and **deletes destination-only files**. Preview it first:

```powershell
dotnet run --project src/MachineBackup -- --machine Lenovo --mirror --dry-run
dotnet run --project src/MachineBackup -- --machine Lenovo --mirror
```

Junctions are excluded, directory timestamps are copied, and failed copies are retried once after one second. Sources run sequentially; a failure stops the backup. Each source produces a bordered table with **Action**, **Item**, **Size**, and **Destination path** columns. Actions are **Copied** (new file), **Updated** (existing file replaced), **Created** (new folder), **Deleted** (mirror extras), and **Preserved** (additive extras). Unchanged files are omitted. Long paths wrap without truncation. A final table totals all reported changes. Dry runs use **Would copy/update/create/delete** labels. Tables are printed after each source completes; if it fails or is cancelled, its reported actions are marked **Unconfirmed** and excluded from completed totals. Ctrl+C stops the current operation. Exit code is `0` on success (including Robocopy codes 0–7) and `1` on configuration errors, copy failures, or cancellation. The application does not pause at exit.

## Build and publish

```powershell
dotnet build src/MachineBackup
dotnet publish src/MachineBackup -c Release -o publish
.\publish\MachineBackup.exe --machine Lenovo --dry-run
```

The settings file is copied to the build/publish output. Edit the published copy for deployed use; source settings may overwrite it on the next publish. Dropbox uploads are handled by your installed Dropbox client after the local backup finishes.

Run `./tests/SmokeTest.ps1` to verify copying, updates, additive preservation, mirror deletion, previews, and configuration validation using isolated temporary folders.
