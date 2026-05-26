namespace SpaceCleaner.Models;

/// <summary>How safe a location is to remove, from the user's point of view.</summary>
public enum SafetyLevel
{
    /// <summary>Regenerated automatically; removing it has no real downside.</summary>
    Safe,

    /// <summary>Fine to remove, but it will be re-downloaded or rebuilt, which costs time/bandwidth.</summary>
    Caution,

    /// <summary>May contain data you want, or affect system rollback — look before removing.</summary>
    Review,
}

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

    /// <summary>Optional explicit safety rating. When unset, <see cref="SafetyLevel"/> is derived.</summary>
    public SafetyLevel? SafetyLevelOverride { get; init; }

    /// <summary>
    /// How safe this location is to remove. Derived from its nature unless explicitly overridden:
    /// Recycle Bin and Windows upgrade leftovers are <see cref="SafetyLevel.Review"/>; anything that
    /// re-downloads, is an installer cache, or carries a caution is <see cref="SafetyLevel.Caution"/>;
    /// everything else regenerates freely and is <see cref="SafetyLevel.Safe"/>.
    /// </summary>
    public SafetyLevel SafetyLevel => SafetyLevelOverride ?? (
        Kind is CategoryKind.RecycleBin or CategoryKind.UpgradeLeftovers
            ? SafetyLevel.Review
            : Redownloads || Kind == CategoryKind.Installer || Caution is not null
                ? SafetyLevel.Caution
                : SafetyLevel.Safe);

    /// <summary>One line explaining <i>why</i> this location got its safety rating.</summary>
    public string SafetyRationale => SafetyLevel switch
    {
        SafetyLevel.Safe =>
            "Everything here is recreated automatically, so removing it has no lasting effect.",
        SafetyLevel.Caution when Redownloads =>
            "Removing this is harmless in itself, but the contents will be downloaded again from the " +
            "internet the next time they are needed.",
        SafetyLevel.Caution =>
            "Removing this is harmless in itself, but the contents will be rebuilt the next time they " +
            "are needed, which can take time.",
        SafetyLevel.Review =>
            "This can hold things you may still want, or affect your ability to undo a recent Windows " +
            "change, so it is worth a look before removing.",
        _ => string.Empty,
    };

    /// <summary>What could actually go wrong if this location is removed. Uses the category-specific
    /// caution when there is one, otherwise a sensible default for the rating.</summary>
    public string PotentialRisks => Caution ?? SafetyLevel switch
    {
        SafetyLevel.Safe =>
            "None of note. At most, a brief one-time regeneration the next time the data is needed.",
        SafetyLevel.Caution when Redownloads =>
            "Re-downloading the cleared data uses time and bandwidth — for example, the next build or " +
            "install will be slower.",
        SafetyLevel.Caution =>
            "The cleared data has to be rebuilt, which can make the next use slower.",
        SafetyLevel.Review =>
            "Removal may be irreversible and could delete files you still need or prevent rolling back " +
            "a Windows update. Check the contents first.",
        _ => string.Empty,
    };

    public required CategoryScope Scope { get; init; }

    /// <summary>
    /// Path templates. Tokens: <c>{DRIVE}</c> (drive root incl. trailing slash), <c>{LOCALAPPDATA}</c>,
    /// <c>{APPDATA}</c>, <c>{USERPROFILE}</c>, <c>{TEMP}</c>. A <c>*</c> path segment is expanded to all
    /// matching subdirectories (e.g. browser profiles).
    /// </summary>
    public required string[] PathTemplates { get; init; }
}
