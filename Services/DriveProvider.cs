using System.IO;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>Enumerates the local drives the user may choose to scan.</summary>
public static class DriveProvider
{
    public static IReadOnlyList<DriveItem> GetScannableDrives()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory); // e.g. "C:\"
        var drives = new List<DriveItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
                continue;
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                continue;

            string label;
            long total = 0, free = 0;
            try
            {
                label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.DriveType.ToString()
                    : drive.VolumeLabel;
                total = drive.TotalSize;
                free = drive.AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                label = drive.DriveType.ToString();
            }

            bool isSystem = string.Equals(drive.RootDirectory.FullName, systemRoot, StringComparison.OrdinalIgnoreCase);

            drives.Add(new DriveItem
            {
                Root = drive.RootDirectory.FullName,
                Label = label,
                TotalBytes = total,
                FreeBytes = free,
                // Default to scanning the system drive, where the vast majority of temp data lives.
                IsSelected = isSystem,
            });
        }

        // If we somehow couldn't flag a system drive, select the first available one.
        if (drives.Count > 0 && drives.All(d => !d.IsSelected))
            drives[0].IsSelected = true;

        return drives;
    }
}
