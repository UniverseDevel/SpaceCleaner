namespace SpaceCleaner.Models;

/// <summary>Broad nature of a location, used to drive the quick-selection presets.</summary>
public enum CategoryKind
{
    TempFiles,
    Logs,
    CrashDumps,
    Cache,
    RecycleBin,
    Installer,
    UpgradeLeftovers,
}

/// <summary>How a category's path templates are resolved onto the drives being scanned.</summary>
public enum CategoryScope
{
    /// <summary>Templates resolve to a single absolute location (typically under the user profile).
    /// The category only appears for a drive if that absolute path lives on it.</summary>
    UserProfile,

    /// <summary>Templates contain a <c>{DRIVE}</c> token and are evaluated once per scanned drive
    /// (e.g. each drive has its own Recycle Bin).</summary>
    PerDrive,

    /// <summary>Machine-wide locations resolved once via <c>{WINDIR}</c>/<c>{PROGRAMDATA}</c> (not
    /// tied to a user profile), attached to whichever scanned drive physically holds them.</summary>
    Machine,
}

/// <summary>
/// A well-known location that is safe to clear. Every reported file ultimately belongs to one of
/// these categories so the UI can always explain <i>why</i> a folder is safe and what it holds.
/// </summary>
public sealed class CleanupCategory
{
    public required string Id { get; init; }

    /// <summary>Short display name, e.g. "Google Chrome Cache".</summary>
    public required string Name { get; init; }

    /// <summary>Why the contents are considered safe to remove.</summary>
    public required string WhySafe { get; init; }

    /// <summary>What kind of files this location usually contains.</summary>
    public required string TypicalContents { get; init; }

    /// <summary>Optional warning shown alongside the explanation (e.g. "will be re-downloaded").</summary>
    public string? Caution { get; init; }

    /// <summary>Broad classification used by the selection presets.</summary>
    public required CategoryKind Kind { get; init; }

    /// <summary>True when clearing this location forces content to be downloaded again from the
    /// internet on next use (package/installer caches, update downloads). Drives the
    /// "non-redownloadable" preset.</summary>
    public bool Redownloads { get; init; }

    public required CategoryScope Scope { get; init; }

    /// <summary>
    /// Path templates. Tokens: <c>{DRIVE}</c> (drive root incl. trailing slash), <c>{LOCALAPPDATA}</c>,
    /// <c>{APPDATA}</c>, <c>{USERPROFILE}</c>, <c>{TEMP}</c>. A <c>*</c> path segment is expanded to all
    /// matching subdirectories (e.g. browser profiles).
    /// </summary>
    public required string[] PathTemplates { get; init; }
}
