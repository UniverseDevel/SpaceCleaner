namespace SpaceCleaner.Models;

/// <summary>A concrete location to scan: one resolved catalog category path on one drive.
/// <paramref name="ProfileLabel"/> is set when the location belongs to a non-current user, so the
/// results can show whose data it is.</summary>
public sealed record ScanTarget(DriveItem Drive, CleanupCategory Category, string Path, string? ProfileLabel = null);

/// <summary>An immutable progress snapshot reported from a drive's scan thread to the UI.</summary>
public readonly record struct ScanProgress(
    string Root,
    ScanState State,
    int Completed,
    int Total,
    long BytesFound,
    string? CurrentPath);
