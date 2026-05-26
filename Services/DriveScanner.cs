using System.IO;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>
/// Walks a drive's catalog targets, summing sizes and building the results tree. Designed for speed:
/// it uses a single low-level directory enumeration per folder (no per-file <c>FileInfo</c> stat),
/// lets the OS skip inaccessible and reparse-point entries, and walks each target's top-level
/// subfolders in parallel. Cloud-synced placeholder files are never read or counted.
/// </summary>
public sealed class DriveScanner
{
    /// <summary>How deep the displayed tree goes. Deeper folders are still fully summed into their
    /// ancestors' sizes, just not materialized as individual nodes (keeps the UI responsive on huge
    /// temp folders).</summary>
    private const int MaxTreeDepth = 6;

    private static readonly int MaxParallelism = Math.Min(8, Environment.ProcessorCount);

    private static readonly EnumerationOptions EnumOptions = new()
    {
        IgnoreInaccessible = true,            // skip denied entries instead of throwing (fast)
        AttributesToSkip = FileAttributes.ReparsePoint, // prune junctions/symlinks/cloud roots
        RecurseSubdirectories = false,        // we recurse manually to build the tree
        ReturnSpecialDirectories = false,
        BufferSize = 64 * 1024,
    };

    /// <summary>Scans one drive and returns its drive node. Throws only on cancellation; a single
    /// failing target is skipped so the rest of the drive still reports.</summary>
    public ScanNode ScanDrive(
        DriveItem drive,
        IReadOnlyList<ScanTarget> targets,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        int total = targets.Count;
        progress.Report(new ScanProgress(drive.Root, ScanState.Scanning, 0, total, 0, null));

        // Build every target first; a category with several resolved paths (e.g. Chrome's Cache,
        // Code Cache, GPUCache) is grouped into a single node afterwards.
        var built = new List<(ScanTarget Target, ScanNode Node)>();
        long runningBytes = 0;
        int done = 0;

        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report(new ScanProgress(drive.Root, ScanState.Scanning, done, total, runningBytes, target.Path));

            ScanNode node;
            try
            {
                node = BuildCategoryRoot(target, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // One target failing (transient IO, vanished folder) must not abort the whole drive.
                done++;
                progress.Report(new ScanProgress(drive.Root, ScanState.Scanning, done, total, runningBytes, null));
                continue;
            }

            if (node.Size > 0 || node.HasChildren)
            {
                built.Add((target, node));
                runningBytes += node.Size;
            }

            done++;
            progress.Report(new ScanProgress(drive.Root, ScanState.Scanning, done, total, runningBytes, null));
        }

        var categoryNodes = GroupByCategory(built);
        long driveTotal = categoryNodes.Sum(n => n.Size);

        var driveNode = new ScanNode
        {
            Kind = NodeKind.Drive,
            Name = drive.DisplayName,
            FullPath = drive.Root,
            Category = null,
            OwnFileSize = 0,
            Size = driveTotal,
        };
        foreach (var child in categoryNodes.OrderByDescending(c => c.Size))
        {
            child.Parent = driveNode;
            driveNode.Children.Add(child);
        }

        progress.Report(new ScanProgress(drive.Root, ScanState.Completed, total, total, driveTotal, null));
        return driveNode;
    }

    /// <summary>
    /// Collapses targets that share a category (and user) into one node. A single resolved path stays
    /// a flat category node; multiple paths become a parent category node with one child per path,
    /// labelled by its location relative to their common folder.
    /// </summary>
    private static List<ScanNode> GroupByCategory(List<(ScanTarget Target, ScanNode Node)> built)
    {
        var result = new List<ScanNode>();

        foreach (var group in built.GroupBy(b => (b.Target.Category.Id, b.Target.ProfileLabel)))
        {
            var members = group.ToList();
            var category = members[0].Target.Category;
            var label = members[0].Target.ProfileLabel;
            var displayName = label is null ? category.Name : $"{category.Name} — {label}";

            if (members.Count == 1)
            {
                // BuildCategoryRoot already named it; keep it as a flat category node.
                result.Add(members[0].Node);
                continue;
            }

            var commonBase = CommonDirectory(members.Select(m => m.Target.Path).ToList());
            var parent = new ScanNode
            {
                Kind = NodeKind.Category,
                Name = displayName,
                FullPath = commonBase,
                Category = category,
                OwnFileSize = 0,
                Size = members.Sum(m => m.Node.Size),
                IsGroupRoot = true,
            };

            foreach (var (target, node) in members.OrderByDescending(m => m.Node.Size))
            {
                node.Kind = NodeKind.Directory;
                node.Name = SafeRelativePath(commonBase, target.Path);
                node.Parent = parent;
                parent.Children.Add(node);
            }

            result.Add(parent);
        }

        return result;
    }

    /// <summary>Longest shared directory prefix of the given absolute paths.</summary>
    private static string CommonDirectory(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
            return Path.GetDirectoryName(paths[0]) ?? paths[0];

        var split = paths.Select(p => p.Split(Path.DirectorySeparatorChar)).ToList();
        int min = split.Min(s => s.Length);
        var common = new List<string>();
        for (int i = 0; i < min; i++)
        {
            var segment = split[0][i];
            if (split.All(s => string.Equals(s[i], segment, StringComparison.OrdinalIgnoreCase)))
                common.Add(segment);
            else
                break;
        }

        if (common.Count == 0)
            return Path.GetPathRoot(paths[0]) ?? paths[0];

        var joined = string.Join(Path.DirectorySeparatorChar, common);
        if (joined.EndsWith(':')) // bare drive like "C:" needs the separator to be a real root
            joined += Path.DirectorySeparatorChar;
        return joined;
    }

    private static string SafeRelativePath(string baseDir, string fullPath)
    {
        try
        {
            return Path.GetRelativePath(baseDir, fullPath);
        }
        catch (ArgumentException)
        {
            return Path.GetFileName(fullPath);
        }
    }

    /// <summary>Builds a category node, walking its immediate subfolders in parallel.</summary>
    private static ScanNode BuildCategoryRoot(ScanTarget target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (ownFiles, subdirs) = Enumerate(target.Path, ct);

        var childNodes = new List<ScanNode>();
        long childTotal = 0;

        if (subdirs.Count > 0)
        {
            var built = new ScanNode?[subdirs.Count];
            var options = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = MaxParallelism,
            };

            Parallel.For(0, subdirs.Count, options, i =>
            {
                built[i] = BuildDirectory(subdirs[i], target.Category, depth: 1, ct);
            });

            foreach (var child in built)
            {
                if (child is null)
                    continue;
                childTotal += child.Size;
                if (child.Size > 0 || child.HasChildren)
                    childNodes.Add(child);
            }
        }

        var node = new ScanNode
        {
            Kind = NodeKind.Category,
            Name = target.ProfileLabel is null
                ? target.Category.Name
                : $"{target.Category.Name} — {target.ProfileLabel}",
            FullPath = target.Path,
            Category = target.Category,
            OwnFileSize = ownFiles,
            Size = ownFiles + childTotal,
        };
        foreach (var child in childNodes.OrderByDescending(c => c.Size))
        {
            child.Parent = node;
            node.Children.Add(child);
        }
        return node;
    }

    private static ScanNode BuildDirectory(string path, CleanupCategory category, int depth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (ownFiles, subdirs) = Enumerate(path, ct);

        var childNodes = new List<ScanNode>();
        long childTotal = 0;
        bool aggregated = false;

        if (depth < MaxTreeDepth)
        {
            foreach (var sub in subdirs)
            {
                var child = BuildDirectory(sub, category, depth + 1, ct);
                childTotal += child.Size;
                if (child.Size > 0 || child.HasChildren)
                    childNodes.Add(child);
            }
        }
        else
        {
            // Past the display-depth cap: still account for the bytes, just don't make nodes.
            foreach (var sub in subdirs)
                childTotal += FastSize(sub, ct);
            aggregated = subdirs.Count > 0;
        }

        var name = Path.GetFileName(path);
        var node = new ScanNode
        {
            Kind = NodeKind.Directory,
            Name = string.IsNullOrEmpty(name) ? path : name,
            FullPath = path,
            Category = category,
            OwnFileSize = ownFiles,
            Size = ownFiles + childTotal,
            ChildrenAggregated = aggregated,
        };
        foreach (var child in childNodes.OrderByDescending(c => c.Size))
        {
            child.Parent = node;
            node.Children.Add(child);
        }
        return node;
    }

    /// <summary>Sums a subtree without building nodes (used beyond the display-depth cap).</summary>
    private static long FastSize(string path, CancellationToken ct)
    {
        var (ownFiles, subdirs) = Enumerate(path, ct);
        long total = ownFiles;
        foreach (var sub in subdirs)
            total += FastSize(sub, ct);
        return total;
    }

    /// <summary>
    /// Single-pass enumeration of one directory: returns the byte total of files stored directly in
    /// it and the list of subdirectories worth descending into. Cloud placeholders, reparse points
    /// and forbidden folders are filtered out here.
    /// </summary>
    private static (long OwnFiles, List<string> Subdirs) Enumerate(string path, CancellationToken ct)
    {
        long ownFiles = 0;
        var subdirs = new List<string>();

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(path).EnumerateFileSystemInfos("*", EnumOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (0, subdirs);
        }

        try
        {
            int counter = 0;
            foreach (var entry in entries)
            {
                if ((++counter & 0x3FF) == 0)
                    ct.ThrowIfCancellationRequested();

                FileAttributes attrs;
                try
                {
                    attrs = entry.Attributes;
                }
                catch
                {
                    continue;
                }

                if ((attrs & FileAttributes.Directory) != 0)
                {
                    if (SafetyGuard.ShouldSkipDirectory(entry.FullName, attrs))
                        continue;
                    subdirs.Add(entry.FullName);
                }
                else
                {
                    // Never touch the contents of a cloud placeholder — reading it forces a download.
                    if (SafetyGuard.IsCloudPlaceholder(attrs))
                        continue;
                    try
                    {
                        ownFiles += ((FileInfo)entry).Length;
                    }
                    catch
                    {
                        // File vanished or its length is momentarily unavailable; ignore it.
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Enumeration interrupted (folder deleted mid-scan, etc.) — return what we have.
        }

        return (ownFiles, subdirs);
    }
}
