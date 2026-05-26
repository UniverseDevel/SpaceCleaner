using System.IO;

namespace SpaceCleaner.Services;

/// <summary>
/// Decides which directories and files the scanner is allowed to descend into. This is the
/// safety backbone of the app: it keeps the scan away from system-critical folders and, crucially,
/// away from cloud-synced storage whose files would be <i>downloaded</i> if we touched them.
/// </summary>
public static class SafetyGuard
{
    // These Windows attribute flags mark cloud placeholder files but are not exposed by the managed
    // FileAttributes enum, so we define the raw values. See CldApi / Cloud Files API.
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    /// <summary>File attributes that mark a cloud placeholder. Reading such a file's <i>content</i>
    /// triggers a download from the cloud, so we never count these files and never descend into
    /// directories carrying them.</summary>
    public const FileAttributes CloudPlaceholderMask =
        FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess;

    // Absolute roots we must never scan into (system-critical). Stored lower-cased with a trailing
    // separator so we can do clean prefix matching.
    private static readonly string[] ExcludedRoots = BuildExcludedRoots();

    // Specific safe folders that sit *inside* an excluded root but are explicitly permitted (e.g.
    // C:\Windows\Temp). These override the excluded-root check, so we can reach exactly these
    // locations without opening up the rest of Windows / ProgramData.
    private static readonly string[] AllowedExceptions = BuildAllowedExceptions();

    // Cloud-storage roots discovered from environment variables (OneDrive, etc.).
    private static readonly string[] CloudRoots = BuildCloudRoots();

    // Directory names that are off-limits wherever they appear.
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System Volume Information",
        "WindowsApps",
        "$WinREAgent",
        "$SysReset",
    };

    // Directory names that indicate a cloud-sync root, wherever they appear.
    private static readonly HashSet<string> CloudNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "OneDrive",
        "OneDriveTemp",
        "Dropbox",
        "Google Drive",
        "GoogleDrive",
        "DriveFS",
        "iCloudDrive",
        "Creative Cloud Files",
        "Box",
        "MEGA",
        "pCloudDrive",
    };

    private static string[] BuildExcludedRoots()
    {
        var roots = new List<string?>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // C:\ProgramData
        };

        return roots
            .Where(static r => !string.IsNullOrEmpty(r))
            .Select(static r => Normalize(r!))
            .Distinct()
            .ToArray();
    }

    private static string[] BuildAllowedExceptions()
    {
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var paths = new List<string>();
        if (!string.IsNullOrEmpty(windir))
        {
            paths.Add(Path.Combine(windir, "Temp"));
            paths.Add(Path.Combine(windir, "Prefetch"));
            paths.Add(Path.Combine(windir, "Logs"));
            paths.Add(Path.Combine(windir, "SoftwareDistribution", "Download"));
            paths.Add(Path.Combine(windir, "SoftwareDistribution", "DeliveryOptimization"));
        }
        if (!string.IsNullOrEmpty(programData))
        {
            paths.Add(Path.Combine(programData, "NVIDIA Corporation", "Downloader"));
            paths.Add(Path.Combine(programData, "NVIDIA Corporation", "NV_Cache"));
            paths.Add(Path.Combine(programData, "Microsoft", "VisualStudio", "Packages"));
            paths.Add(Path.Combine(programData, "Microsoft", "Windows", "WER"));
        }

        return paths.Select(Normalize).Distinct().ToArray();
    }

    private static string[] BuildCloudRoots()
    {
        string[] vars = { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" };
        return vars
            .Select(Environment.GetEnvironmentVariable)
            .Where(static v => !string.IsNullOrEmpty(v))
            .Select(static v => Normalize(v!))
            .Distinct()
            .ToArray();
    }

    /// <summary>Lower-cased absolute path ending in a directory separator.</summary>
    private static string Normalize(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar))
            path += Path.DirectorySeparatorChar;
        return path.ToLowerInvariant();
    }

    /// <summary>True if a directory we are about to descend into should be skipped.</summary>
    public static bool ShouldSkipDirectory(string fullPath, FileAttributes attributes)
    {
        // Reparse points (junctions, symlinks, cloud placeholders): skip to avoid loops and downloads.
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return true;

        // Cloud placeholder directory.
        if ((attributes & CloudPlaceholderMask) != 0)
            return true;

        var name = Path.GetFileName(fullPath.AsSpan().TrimEnd(Path.DirectorySeparatorChar)).ToString();
        if (ExcludedNames.Contains(name) || IsCloudName(name))
            return true;

        var normalized = Normalize(fullPath);

        // An explicit allow-list entry overrides the excluded-root check below.
        if (IsAllowedException(normalized))
            return false;

        foreach (var root in ExcludedRoots)
        {
            if (normalized.StartsWith(root, StringComparison.Ordinal))
                return true;
        }

        foreach (var root in CloudRoots)
        {
            if (normalized.StartsWith(root, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsCloudName(string name)
        => CloudNames.Contains(name)
           || name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase); // e.g. "OneDrive - Contoso"

    private static bool IsAllowedException(string normalizedPath)
    {
        foreach (var allowed in AllowedExceptions)
        {
            if (normalizedPath.StartsWith(allowed, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>True if a file is a cloud placeholder whose bytes live only in the cloud.</summary>
    public static bool IsCloudPlaceholder(FileAttributes attributes)
        => (attributes & CloudPlaceholderMask) != 0;

    /// <summary>True if an absolute path sits inside an excluded or cloud-synced root. Used to keep
    /// catalog targets (e.g. a drive-root Temp folder) out of forbidden territory before scanning.</summary>
    public static bool IsForbiddenPath(string fullPath)
    {
        var normalized = Normalize(fullPath);

        if (IsAllowedException(normalized))
            return false;

        foreach (var root in ExcludedRoots)
        {
            if (normalized.StartsWith(root, StringComparison.Ordinal))
                return true;
        }

        foreach (var root in CloudRoots)
        {
            if (normalized.StartsWith(root, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
