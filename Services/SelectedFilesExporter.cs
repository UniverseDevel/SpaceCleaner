using System.IO;
using System.Text;
using SpaceCleaner.Infrastructure;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>
/// Produces a flat CSV list (full path + size) of every file under the checked items in the results
/// tree. Uses the same safety filtering as the scan, so cloud placeholders and reparse points are
/// never read.
/// </summary>
public static class SelectedFilesExporter
{
    private static readonly EnumerationOptions EnumOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        BufferSize = 64 * 1024,
    };

    /// <summary>The distinct, non-overlapping folder roots that the current selection covers.</summary>
    public static List<string> CollectRoots(IEnumerable<ScanNode> results)
    {
        var roots = new List<string>();
        foreach (var node in results)
            Collect(node, roots);
        return Dedupe(roots);
    }

    private static void Collect(ScanNode node, List<string> roots)
    {
        switch (node.IsChecked)
        {
            case false:
                return;
            // Drive and grouped-category nodes are aggregators whose FullPath (e.g. "C:\" or a common
            // base) is NOT a real scan target — descend to the actual child paths instead of
            // enumerating them, which would sweep the whole drive.
            case true when node.Kind == NodeKind.Drive || node.IsGroupRoot:
            case null: // partially selected — descend to the checked parts only.
                foreach (var child in node.Children)
                    Collect(child, roots);
                return;
            case true:
                roots.Add(node.FullPath);
                return;
        }
    }

    private static List<string> Dedupe(List<string> roots)
    {
        var ordered = roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<string>();
        foreach (var path in ordered)
        {
            bool nestedInExisting = result.Any(r =>
                string.Equals(path, r, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (!nestedInExisting)
                result.Add(path);
        }
        return result;
    }

    /// <summary>Enumerates every file under <paramref name="roots"/>, sorted largest-first.</summary>
    public static List<SelectedFileEntry> CollectFiles(IReadOnlyList<string> roots, CancellationToken ct)
    {
        var files = new List<SelectedFileEntry>();
        foreach (var root in roots)
            EnumerateFiles(root, files, ct);

        files.Sort((a, b) => b.Size.CompareTo(a.Size));
        return files;
    }

    /// <summary>Writes the given files to a CSV. Returns the total bytes.</summary>
    public static long WriteCsv(IReadOnlyList<SelectedFileEntry> files, string outputPath)
    {
        long total = 0;
        using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("\"Full Path\",\"Size (Bytes)\",\"Size\"");
        foreach (var file in files)
        {
            total += file.Size;
            writer.WriteLine($"\"{file.Path.Replace("\"", "\"\"")}\",{file.Size},\"{file.SizeText}\"");
        }
        return total;
    }

    /// <summary>Convenience: collect files under <paramref name="roots"/> and write them to a CSV.</summary>
    public static (int Count, long TotalBytes) ExportCsv(
        IReadOnlyList<string> roots, string outputPath, CancellationToken ct)
    {
        var files = CollectFiles(roots, ct);
        long total = WriteCsv(files, outputPath);
        return (files.Count, total);
    }

    private static void EnumerateFiles(string directory, List<SelectedFileEntry> files, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(directory).EnumerateFileSystemInfos("*", EnumOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return;
        }

        try
        {
            int counter = 0;
            foreach (var entry in entries)
            {
                if ((++counter & 0x3FF) == 0)
                    ct.ThrowIfCancellationRequested();

                FileAttributes attrs;
                try { attrs = entry.Attributes; }
                catch { continue; }

                if ((attrs & FileAttributes.Directory) != 0)
                {
                    if (!SafetyGuard.ShouldSkipDirectory(entry.FullName, attrs))
                        EnumerateFiles(entry.FullName, files, ct);
                }
                else
                {
                    if (SafetyGuard.IsCloudPlaceholder(attrs))
                        continue;
                    try { files.Add(new SelectedFileEntry(entry.FullName, ((FileInfo)entry).Length)); }
                    catch { /* vanished */ }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Folder changed mid-enumeration — keep what we have.
        }
    }
}
