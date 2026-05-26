using System.IO;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>
/// Turns the static catalog plus the user's drive selection (and chosen profiles) into the concrete
/// list of targets each drive's scan thread will walk.
/// </summary>
public static class CatalogResolver
{
    /// <summary>
    /// Builds the targets per selected drive.
    /// <list type="bullet">
    /// <item>User-profile categories are resolved for each <paramref name="profiles"/> entry and
    /// attached to whichever selected drive physically holds them.</item>
    /// <item>Machine categories are resolved once and attached to their owning drive.</item>
    /// <item>Per-drive categories are evaluated for every selected drive.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyDictionary<DriveItem, List<ScanTarget>> BuildTargets(
        IReadOnlyList<DriveItem> selectedDrives,
        IReadOnlyList<ProfileContext> profiles)
    {
        var byDrive = selectedDrives.ToDictionary(d => d, _ => new List<ScanTarget>());
        var rootLookup = selectedDrives.ToDictionary(d => d.Root, d => d, StringComparer.OrdinalIgnoreCase);
        var seen = selectedDrives.ToDictionary(d => d, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var currentProfile = profiles.FirstOrDefault(p => p.IsCurrentUser) ?? profiles[0];

        foreach (var category in CleanupCatalog.All)
        {
            switch (category.Scope)
            {
                case CategoryScope.UserProfile:
                    foreach (var profile in profiles)
                    {
                        var label = profile.IsCurrentUser ? null : profile.UserName;
                        foreach (var template in category.PathTemplates)
                        foreach (var path in PathResolver.Resolve(template, driveRoot: null, profile))
                            AttachByRoot(byDrive, seen, rootLookup, category, path, label);
                    }
                    break;

                case CategoryScope.Machine:
                    foreach (var template in category.PathTemplates)
                    foreach (var path in PathResolver.Resolve(template, driveRoot: null, currentProfile))
                        AttachByRoot(byDrive, seen, rootLookup, category, path, profileLabel: null);
                    break;

                case CategoryScope.PerDrive:
                    foreach (var drive in selectedDrives)
                    foreach (var template in category.PathTemplates)
                    foreach (var path in PathResolver.Resolve(template, drive.Root, currentProfile))
                        AddTarget(byDrive, seen, drive, category, path, profileLabel: null);
                    break;
            }
        }

        return byDrive;
    }

    private static void AttachByRoot(
        IReadOnlyDictionary<DriveItem, List<ScanTarget>> byDrive,
        IReadOnlyDictionary<DriveItem, HashSet<string>> seen,
        IReadOnlyDictionary<string, DriveItem> rootLookup,
        CleanupCategory category,
        string path,
        string? profileLabel)
    {
        var root = Path.GetPathRoot(path);
        if (root is null || !rootLookup.TryGetValue(root, out var drive))
            return; // resolved to a drive the user didn't select
        AddTarget(byDrive, seen, drive, category, path, profileLabel);
    }

    private static void AddTarget(
        IReadOnlyDictionary<DriveItem, List<ScanTarget>> byDrive,
        IReadOnlyDictionary<DriveItem, HashSet<string>> seen,
        DriveItem drive,
        CleanupCategory category,
        string path,
        string? profileLabel)
    {
        if (seen[drive].Add(path))
            byDrive[drive].Add(new ScanTarget(drive, category, path, profileLabel));
    }
}
