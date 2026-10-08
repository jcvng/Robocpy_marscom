using System.Globalization;
using RoboSharp;

sealed record BackupChange(string Action, string Kind, long? Bytes, string Path);

sealed class ChangeReport(bool preview, bool mirror)
{
    private readonly Dictionary<string, int> totals = new();

    public static BackupChange? Classify(ProcessedFileInfo item, RoboSharpConfiguration configuration,
        string source, string destination, bool mirror)
    {
        string? action = null;
        var directory = item.FileClassType == FileClassType.NewDir;
        if (directory && item.TryGetDirectoryClass(configuration, out var dirFlag))
            action = dirFlag switch
            {
                ProcessedDirectoryFlag.NewDir => "Created",
                ProcessedDirectoryFlag.ExtraDir => mirror ? "Deleted" : "Preserved",
                _ => null
            };
        else if (item.FileClassType == FileClassType.File && item.TryGetFileClass(configuration, out var fileFlag))
            action = fileFlag switch
            {
                ProcessedFileFlag.NewFile => "Copied",
                ProcessedFileFlag.NewerFile or ProcessedFileFlag.OlderFile or ProcessedFileFlag.MisMatch => "Updated",
                ProcessedFileFlag.ExtraFile => mirror ? "Deleted" : "Preserved",
                ProcessedFileFlag.Failed => "Failed",
                _ => null
            };
        if (action is null) return null;
        // Extras are already destination paths. Other log paths refer to the source.
        var path = Path.TrimEndingDirectorySeparator(item.Name.Trim());
        if (action is not ("Deleted" or "Preserved"))
            path = Path.GetFullPath(Path.Combine(destination, Path.GetRelativePath(source, path)));
        return new(action, directory ? "Folder" : "File", directory ? null : item.Size, path);
    }

    public void PrintChanges(IEnumerable<BackupChange> changes, bool completed)
    {
        var rows = changes.Distinct().ToArray();
        if (rows.Length == 0)
        {
            Console.WriteLine("No changes reported.");
            return;
        }
        if (!completed)
            Console.WriteLine("Operation incomplete: reported actions are unconfirmed; some may have completed.");
        ConsoleTable.Print(["Action", "Item", "Size", "Destination path"], rows.Select(row => new[]
        {
            row.Action == "Failed" ? "Failed" : !completed ? "Unconfirmed " + row.Action :
                preview && row.Action != "Preserved" ? row.Action switch
                {
                    "Copied" => "Would copy", "Updated" => "Would update",
                    "Created" => "Would create", "Deleted" => "Would delete", _ => row.Action
                } : row.Action,
            row.Kind,
            row.Bytes is { } bytes ? FormatSize(bytes) : "-",
            row.Path
        }).ToArray());
        if (completed)
            foreach (var row in rows)
                totals[row.Action] = totals.GetValueOrDefault(row.Action) + 1;
    }

    public void PrintSummary()
    {
        Console.WriteLine(preview ? "\nPreview summary (files and folders)" : "\nCompleted changes (files and folders)");
        ConsoleTable.Print(["Copied", "Updated", "Deleted", "Created", "Preserved"],
            [new[] { "Copied", "Updated", "Deleted", "Created", "Preserved" }
                .Select(action => totals.GetValueOrDefault(action).ToString(CultureInfo.InvariantCulture)).ToArray()]);
        if (!mirror) Console.WriteLine("Destination-only items are preserved in additive mode.");
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
    }
}

static class ConsoleTable
{
    public static void Print(string[] headers, string[][] rows)
    {
        // Keep redirected output and narrow terminals readable; wrap paths without truncating them.
        var terminalWidth = 120;
        if (!Console.IsOutputRedirected)
            try { terminalWidth = Math.Max(60, Console.WindowWidth); } catch (IOException) { }
        var widths = headers.Select((header, i) => Math.Max(header.Length,
            rows.Select(row => Clean(row[i]).Length).DefaultIfEmpty(0).Max())).ToArray();
        if (headers[^1] == "Destination path")
            widths[^1] = Math.Max(16, Math.Min(widths[^1], terminalWidth - widths.Take(widths.Length - 1).Sum() - 3 * widths.Length - 1));
        var border = "+" + string.Join("+", widths.Select(width => new string('-', width + 2))) + "+";
        Console.WriteLine(border);
        WriteRow(headers, widths);
        Console.WriteLine(border);
        foreach (var row in rows) WriteRow(row, widths);
        Console.WriteLine(border);
    }

    private static void WriteRow(string[] row, int[] widths)
    {
        var cells = row.Select(Clean).ToArray();
        var lines = cells.Select((cell, i) => Math.Max(1, (cell.Length + widths[i] - 1) / widths[i])).Max();
        for (var line = 0; line < lines; line++)
        {
            Console.Write("|");
            for (var i = 0; i < cells.Length; i++)
            {
                var start = line * widths[i];
                var text = start >= cells[i].Length ? "" : cells[i].Substring(start, Math.Min(widths[i], cells[i].Length - start));
                Console.Write(" " + text.PadRight(widths[i]) + " |");
            }
            Console.WriteLine();
        }
    }

    private static string Clean(string text) => new(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
}
