using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SpaceCleaner.Services;

/// <summary>
/// Resolves a path to its real on-disk location, following junctions and symbolic links anywhere in
/// the path (via <c>GetFinalPathNameByHandle</c>). This lets the scanner attribute a location to the
/// drive it <i>physically</i> lives on — so a folder that looks like it's on C: but is junctioned to
/// another drive is treated as belonging to that other drive, and is never scanned or deleted as a
/// surprise under the drive the user actually selected.
/// </summary>
public static class LinkResolver
{
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint FILE_SHARE_DELETE = 0x4;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000; // required to open a directory handle

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle hFile, [Out] char[] lpszFilePath, uint cchFilePath, uint dwFlags);

    /// <summary>The canonical real path with all reparse points resolved, or <paramref name="path"/>
    /// unchanged if it can't be resolved.</summary>
    public static string GetRealPath(string path)
    {
        try
        {
            using var handle = CreateFileW(
                path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
            if (handle.IsInvalid)
                return path;

            var buffer = new char[600];
            uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
            if (length == 0)
                return path;
            if (length > buffer.Length)
            {
                buffer = new char[length];
                length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
                if (length == 0)
                    return path;
            }

            var result = new string(buffer, 0, (int)length);
            // Strip the win32 extended-length prefix.
            if (result.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
                result = @"\\" + result[8..];
            else if (result.StartsWith(@"\\?\", StringComparison.Ordinal))
                result = result[4..];
            return result;
        }
        catch
        {
            return path;
        }
    }

    /// <summary>True if the path's real location differs from where it appears (i.e. it crosses a
    /// junction/symlink).</summary>
    public static bool IsRedirected(string logicalPath, string realPath)
    {
        static string Norm(string p)
        {
            try { p = Path.GetFullPath(p); } catch { /* keep as-is */ }
            return p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return !string.Equals(Norm(logicalPath), Norm(realPath), StringComparison.OrdinalIgnoreCase);
    }
}
