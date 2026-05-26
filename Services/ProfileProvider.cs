using System.IO;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>Builds the list of user profiles whose caches should be scanned.</summary>
public static class ProfileProvider
{
    // Profile folders that are templates/shared, not real interactive accounts.
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Public", "Default", "Default User", "All Users", "DefaultAppPool",
    };

    public static ProfileContext Current()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new ProfileContext(
            UserName: Environment.UserName,
            UserProfile: profile,
            LocalAppData: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppData: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Temp: Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
            IsCurrentUser: true);
    }

    /// <summary>
    /// Returns the current user, plus (when <paramref name="includeAllUsers"/> is set) every other
    /// real account profile found under the Users root. Cloud folders are never touched here because
    /// only the standard AppData paths are constructed.
    /// </summary>
    public static IReadOnlyList<ProfileContext> GetProfiles(bool includeAllUsers)
    {
        var current = Current();
        var profiles = new List<ProfileContext> { current };

        if (!includeAllUsers)
            return profiles;

        var usersRoot = Directory.GetParent(current.UserProfile)?.FullName; // e.g. C:\Users
        if (usersRoot is null || !Directory.Exists(usersRoot))
            return profiles;

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(usersRoot))
            {
                var name = Path.GetFileName(dir);
                if (SkipNames.Contains(name))
                    continue;
                if (string.Equals(dir, current.UserProfile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var localAppData = Path.Combine(dir, "AppData", "Local");
                if (!Directory.Exists(localAppData))
                    continue; // not a real interactive profile

                profiles.Add(new ProfileContext(
                    UserName: name,
                    UserProfile: dir,
                    LocalAppData: localAppData,
                    AppData: Path.Combine(dir, "AppData", "Roaming"),
                    Temp: Path.Combine(localAppData, "Temp"),
                    IsCurrentUser: false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Can't list the Users root — fall back to just the current user.
        }

        return profiles;
    }
}
