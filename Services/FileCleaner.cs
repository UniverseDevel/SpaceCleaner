using System.IO;

namespace SpaceCleaner.Services;

/// <summary>Deletes individual files defensively: clears a read-only flag if needed, treats an
/// already-missing file as success, and turns any failure into a short, human-readable reason
/// instead of throwing.</summary>
public static class FileCleaner
{
    public static (bool Ok, string? Error) TryDelete(string path)
    {
        try
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(path);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return (true, null); // already gone — nothing to do
            }

            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

            File.Delete(path);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, Describe(ex));
        }
    }

    private static string Describe(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Access denied",
        FileNotFoundException or DirectoryNotFoundException => "Already gone",
        IOException io => Shorten(io.Message),
        _ => Shorten(ex.Message),
    };

    private static string Shorten(string message)
    {
        message = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return message.Length > 140 ? message[..140] + "…" : message;
    }
}
