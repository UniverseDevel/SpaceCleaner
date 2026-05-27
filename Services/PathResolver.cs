using System.IO;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>
/// Expands catalog path templates into concrete, existing directories. Handles token substitution
/// and <c>*</c> wildcard segments (e.g. browser profiles or per-version IDE folders).
/// </summary>
public static class PathResolver
{
    // Machine-wide tokens, identical for every user.
    private static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    /// <summary>
    /// Resolves a template to every existing directory it matches.
    /// <paramref name="driveRoot"/> (e.g. <c>"D:\\"</c>) is required for <c>{DRIVE}</c> templates;
    /// <paramref name="profile"/> supplies the per-user tokens.
    /// </summary>
    public static IEnumerable<string> Resolve(string template, string? driveRoot, ProfileContext profile)
    {
        string resolved = template;

        if (resolved.Contains("{DRIVE}", StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(driveRoot))
                yield break;
            resolved = resolved.Replace("{DRIVE}", driveRoot); // keeps its trailing slash
        }

        (string Token, string? Value)[] tokens =
        {
            ("{LOCALAPPDATA}", profile.LocalAppData),
            ("{APPDATA}", profile.AppData),
            ("{USERPROFILE}", profile.UserProfile),
            ("{TEMP}", profile.Temp),
            ("{WINDIR}", WinDir),
            ("{PROGRAMDATA}", ProgramData),
        };

        foreach (var (token, value) in tokens)
        {
            if (!resolved.Contains(token, StringComparison.Ordinal))
                continue;
            if (string.IsNullOrEmpty(value))
                yield break; // token unavailable -> template can't resolve
            resolved = resolved.Replace(token, value);
        }

        foreach (var candidate in ExpandWildcards(resolved))
        {
            if (!Directory.Exists(candidate) || SafetyGuard.IsForbiddenPath(candidate))
                continue;

            // Skip junctions/symlinks (anywhere in the path): a link's real content lives elsewhere
            // and following it could scan or delete files on a drive the user didn't select.
            var realPath = LinkResolver.GetRealPath(candidate);
            if (LinkResolver.IsRedirected(candidate, realPath))
                continue;

            yield return Path.TrimEndingDirectorySeparator(candidate);
        }
    }

    /// <summary>Expands any <c>*</c>/<c>?</c> segments by enumerating matching subdirectories.</summary>
    private static IEnumerable<string> ExpandWildcards(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length == 0)
            return Array.Empty<string>();

        // First segment is the drive ("C:") — turn it into a usable root ("C:\").
        IEnumerable<string> current = new[] { parts[0] + Path.DirectorySeparatorChar };

        for (int i = 1; i < parts.Length; i++)
        {
            var segment = parts[i];
            if (segment.Length == 0)
                continue;

            bool isWildcard = segment.Contains('*') || segment.Contains('?');
            var next = new List<string>();

            foreach (var dir in current)
            {
                if (isWildcard)
                {
                    if (!Directory.Exists(dir))
                        continue;
                    try
                    {
                        next.AddRange(Directory.EnumerateDirectories(dir, segment));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Inaccessible parent — nothing to expand here.
                    }
                }
                else
                {
                    next.Add(Path.Combine(dir, segment));
                }
            }

            current = next;
        }

        return current;
    }
}
