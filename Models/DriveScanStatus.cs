using SpaceCleaner.Infrastructure;

namespace SpaceCleaner.Models;

public enum ScanState
{
    Pending,
    Scanning,
    Completed,
    Cancelled,
    Error,
}

/// <summary>
/// Live progress for a single drive's scan thread. One of these backs each progress bar in the UI.
/// Progress is measured in catalog targets completed out of the total for the drive, which gives an
/// honest, monotonic bar without having to pre-walk every byte.
/// </summary>
public sealed class DriveScanStatus : ObservableObject
{
    public required string Root { get; init; }
    public required string Header { get; init; }

    private ScanState _state = ScanState.Pending;
    public ScanState State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(IsIndeterminate));
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

    private int _completed;
    public int Completed
    {
        get => _completed;
        set
        {
            if (SetField(ref _completed, value))
                OnPropertyChanged(nameof(ProgressPercent));
        }
    }

    private int _total;
    public int Total
    {
        get => _total;
        set
        {
            if (SetField(ref _total, value))
                OnPropertyChanged(nameof(ProgressPercent));
        }
    }

    private long _bytesFound;
    public long BytesFound
    {
        get => _bytesFound;
        set
        {
            if (SetField(ref _bytesFound, value))
                OnPropertyChanged(nameof(BytesText));
        }
    }

    private string _currentPath = string.Empty;
    public string CurrentPath
    {
        get => _currentPath;
        set => SetField(ref _currentPath, value);
    }

    public double ProgressPercent => Total <= 0 ? 0 : (double)Completed / Total * 100.0;

    public string BytesText => ByteSize.Format(BytesFound);

    /// <summary>While scanning we don't know how long the current target will take, so show a marquee
    /// bar; once finished the bar reflects the final outcome.</summary>
    public bool IsIndeterminate => State == ScanState.Scanning && Completed < Total;

    public bool IsRunning => State == ScanState.Scanning;

    public string StateText => State switch
    {
        ScanState.Pending => "Waiting…",
        ScanState.Scanning => Total > 0 ? $"Scanning {Completed}/{Total}…" : "Scanning…",
        ScanState.Completed => "Done",
        ScanState.Cancelled => "Cancelled",
        ScanState.Error => "Error",
        _ => string.Empty,
    };
}
