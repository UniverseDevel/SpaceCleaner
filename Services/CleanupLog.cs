using System.IO;
using SpaceCleaner.Infrastructure;
using SpaceCleaner.Models;

namespace SpaceCleaner.Services;

/// <summary>
/// Append-only audit log for a cleanup run, written to a <c>logs</c> folder next to the executable.
/// Every deleted/skipped file is recorded with its size and (on failure) the reason, so there is a
/// durable record of exactly what was removed. Written only from the delete thread.
/// </summary>
public sealed class CleanupLog : IDisposable
{
    private readonly StreamWriter? _writer;
    private int _sinceFlush;

    public string? Path { get; }

    public CleanupLog(int fileCount, IReadOnlyList<string> roots)
    {
        try
        {
            var exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var logDir = System.IO.Path.Combine(exeDir, "logs");
            Directory.CreateDirectory(logDir);
            Path = System.IO.Path.Combine(logDir, $"cleanup-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            _writer = new StreamWriter(Path, append: false) { AutoFlush = false };
        }
        catch
        {
            // Fall back to a per-user location if the exe folder is not writable.
            try
            {
                var fallback = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SpaceCleaner", "logs");
                Directory.CreateDirectory(fallback);
                Path = System.IO.Path.Combine(fallback, $"cleanup-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                _writer = new StreamWriter(Path, append: false) { AutoFlush = false };
            }
            catch
            {
                _writer = null;
                Path = null;
            }
        }

        if (_writer is null)
            return;

        _writer.WriteLine("SpaceCleaner cleanup audit log");
        _writer.WriteLine($"Started : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _writer.WriteLine($"User    : {Environment.UserName}   Machine: {Environment.MachineName}");
        _writer.WriteLine($"Files   : {fileCount:N0} candidate file(s)");
        _writer.WriteLine($"Selected locations ({roots.Count}):");
        foreach (var root in roots)
            _writer.WriteLine($"    {root}");
        _writer.WriteLine(new string('-', 80));
        _writer.WriteLine("STATE    SIZE            PATH    [REASON]");
        _writer.Flush();
    }

    public void WriteResult(CleanupStatus status, string path, long size, string? error)
    {
        if (_writer is null)
            return;

        var state = status == CleanupStatus.Deleted ? "DELETED" : "SKIPPED";
        var line = $"{state,-8} {ByteSize.Format(size),-14}  {path}";
        if (!string.IsNullOrEmpty(error))
            line += $"\t[{error}]";
        _writer.WriteLine(line);

        if (++_sinceFlush >= 1000)
        {
            _writer.Flush();
            _sinceFlush = 0;
        }
    }

    public void WriteSummary(int deleted, long freed, int skipped, bool cancelled)
    {
        if (_writer is null)
            return;

        _writer.WriteLine(new string('-', 80));
        if (cancelled)
            _writer.WriteLine("Run CANCELLED before completion.");
        _writer.WriteLine($"Finished: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _writer.WriteLine($"Deleted : {deleted:N0} file(s), freed {ByteSize.Format(freed)}");
        _writer.WriteLine($"Skipped : {skipped:N0} file(s)");
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
    }
}
