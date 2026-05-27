using SpaceCleaner.Infrastructure;

namespace SpaceCleaner.Models;

public enum CleanupStatus
{
    Pending,
    Deleted,
    Skipped,
}

/// <summary>
/// One file in the cleanup run. <see cref="Status"/> and <see cref="Error"/> are written from the
/// background delete loop as plain fields and surfaced to the UI in batches via
/// <see cref="NotifyStatus"/> (called on the UI thread), avoiding cross-thread change notifications.
/// </summary>
public sealed class CleanupItem : ObservableObject
{
    public CleanupItem(string path, long size)
    {
        Path = path;
        Size = size;
    }

    public string Path { get; }
    public long Size { get; }
    public string SizeText => ByteSize.Format(Size);

    // Written on the delete thread; read on the UI thread after a memory barrier (see CleanupWindow).
    public CleanupStatus Status;
    public string? Error;

    public bool Failed => Status == CleanupStatus.Skipped;

    public string StatusText => Status switch
    {
        CleanupStatus.Deleted => "Deleted",
        CleanupStatus.Skipped => string.IsNullOrEmpty(Error) ? "Skipped" : $"Skipped — {Error}",
        _ => string.Empty,
    };

    /// <summary>Raises change notifications for the status-derived bindings. Call on the UI thread.</summary>
    public void NotifyStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Failed));
    }
}
