using System.Text.Json;
using RoboSharp;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        string? machine = null;
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var mirror = false;
        var dryRun = false;
        var list = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--help": case "-h":
                    Console.WriteLine("MachineBackup [NAME | --machine NAME] [--settings FILE] [--list-machines] [--dry-run] [--mirror]\nExample: MachineBackup lenovo --dry-run (names are case-insensitive).\nDefaults: Windows PC name, appsettings.json beside the executable, additive backup.\nEach source is copied into DestinationPath/<source folder name>. --mirror deletes destination extras.");
                    return 0;
                case "--machine":
                    if (machine is not null) throw new ArgumentException("Specify the machine only once.");
                    machine = ReadValue(args, ref i);
                    break;
                case "--settings": settingsPath = Path.GetFullPath(ReadValue(args, ref i)); break;
                case "--mirror": mirror = true; break;
                case "--dry-run": dryRun = true; break;
                case "--list-machines": list = true; break;
                default:
                    if (args[i].StartsWith('-')) throw new ArgumentException($"Unknown option '{args[i]}'. Use --help.");
                    if (machine is not null) throw new ArgumentException("Specify the machine only once.");
                    if (string.IsNullOrWhiteSpace(args[i])) throw new ArgumentException("Machine name must not be empty.");
                    machine = args[i];
                    break;
            }
        }

        var settings = JsonSerializer.Deserialize<BackupSettings>(await File.ReadAllTextAsync(settingsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
            ?? throw new ArgumentException("Settings file is empty.");
        if (settings.Machines is null || settings.Machines.Count == 0)
            throw new ArgumentException("Configure at least one entry in Machines.");
        if (list)
        {
            foreach (var name in settings.Machines.Keys) Console.WriteLine(name);
            return 0;
        }
        machine ??= Environment.MachineName;
        var matches = settings.Machines.Where(p => p.Key.Equals(machine, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            throw new ArgumentException($"No unique configuration for '{machine}'. Use --machine with one of: {string.Join(", ", settings.Machines.Keys)}.");
        var config = matches[0].Value ?? throw new ArgumentException("Machine configuration is empty.");
        if (config.SourcePaths is null || config.SourcePaths.Length == 0)
            throw new ArgumentException("SourcePaths must contain at least one folder.");
        var root = ResolvePath(config.DestinationPath);
        var jobs = new List<(string Source, string Destination)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in config.SourcePaths)
        {
            var source = ResolvePath(path);
            if (!Directory.Exists(source)) throw new ArgumentException($"Source folder does not exist: {source}");
            var name = Path.GetFileName(source);
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Drive roots cannot be used as sources. Select a subfolder.");
            if (!names.Add(name)) throw new ArgumentException($"Sources share the folder name '{name}'. Choose distinct folder names to prevent merging backups.");
            jobs.Add((source, Path.Combine(root, name)));
        }
        // Validate the entire plan before any copy, including cross-source overlap.
        foreach (var source in jobs.Select(j => j.Source))
            if (ContainsPath(source, root) || ContainsPath(root, source))
                throw new ArgumentException($"Source and destination must not overlap: {source} and {root}");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("RoboSharp backups require Windows and robocopy.");

        Console.WriteLine($"Machine: {matches[0].Key} | Mode: {(mirror ? "MIRROR (deletes extras)" : "ADDITIVE")} | Preview: {dryRun}");
        var report = new ChangeReport(dryRun, mirror);
        foreach (var job in jobs)
        {
            Console.WriteLine($"\n{job.Source} -> {job.Destination}");
            using var command = new RoboCommand();
            command.CopyOptions.Source = job.Source;
            command.CopyOptions.Destination = job.Destination;
            command.CopyOptions.CopySubdirectoriesIncludingEmpty = true;
            command.CopyOptions.Mirror = mirror;
            command.CopyOptions.DirectoryCopyFlags = "T";
            command.SelectionOptions.ExcludeJunctionPoints = true;
            command.RetryOptions.RetryCount = 1;
            command.RetryOptions.RetryWaitTime = 1;
            command.LoggingOptions.ListOnly = dryRun;
            command.LoggingOptions.IncludeFullPathNames = true;
            command.LoggingOptions.ReportExtraFiles = true;
            command.LoggingOptions.NoJobHeader = true;
            command.LoggingOptions.NoJobSummary = true;
            var changes = new List<BackupChange>();
            command.OnFileProcessed += (_, e) =>
            {
                var change = ChangeReport.Classify(e.ProcessedFile, command.Configuration, job.Source, job.Destination, mirror);
                if (change is not null) changes.Add(change);
            };
            var errors = false;
            command.OnCommandError += (_, e) => { errors = true; Console.Error.WriteLine(e.Error); };
            command.OnError += (_, e) => { errors = true; Console.Error.WriteLine(e.Error); };
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; command.Stop(); };
            Console.CancelKeyPress += cancel;
            try
            {
                await command.Start();
                var result = command.GetResults();
                if (errors || result is null || (int)result.Status.ExitCode >= 8 || command.IsCancelled)
                {
                    report.PrintChanges(changes, completed: false);
                    report.PrintSummary();
                    Console.Error.WriteLine("Backup failed or was cancelled.");
                    return 1;
                }
                report.PrintChanges(changes, completed: true);
            }
            finally { Console.CancelKeyPress -= cancel; }
        }
        report.PrintSummary();
        Console.WriteLine(dryRun ? "Preview completed. No files were copied or deleted." : "Backup completed successfully.");
        return 0;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"Error: {e.Message}");
        return 1;
    }
}

static string ReadValue(string[] args, ref int index)
{
    if (++index >= args.Length || args[index].StartsWith("--") || string.IsNullOrWhiteSpace(args[index]))
        throw new ArgumentException("Option requires a value.");
    return args[index];
}

static string ResolvePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Paths must not be empty.");
    var expanded = Environment.ExpandEnvironmentVariables(path);
    if (expanded.Contains('%')) throw new ArgumentException($"Unresolved environment variable in path: {path}");
    if (!Path.IsPathFullyQualified(expanded)) throw new ArgumentException($"Use an absolute path: {path}");
    return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded));
}

static bool ContainsPath(string parent, string child) =>
    parent.Equals(child, StringComparison.OrdinalIgnoreCase) ||
    child.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

sealed class BackupSettings
{
    public Dictionary<string, MachineSettings?>? Machines { get; set; }
}

sealed class MachineSettings
{
    public string[]? SourcePaths { get; set; }
    public string? DestinationPath { get; set; }
}
