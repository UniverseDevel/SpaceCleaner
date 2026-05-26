namespace SpaceCleaner.Models;

/// <summary>
/// Resolved per-user folder locations used to expand user-profile catalog templates. When scanning
/// all users, one of these is produced per account so each user's caches are reported separately.
/// </summary>
public sealed record ProfileContext(
    string UserName,
    string UserProfile,
    string LocalAppData,
    string AppData,
    string Temp,
    bool IsCurrentUser);
