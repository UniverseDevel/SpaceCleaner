using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using SpaceCleaner.Infrastructure;
using SpaceCleaner.Models;
using SpaceCleaner.Services;

namespace SpaceCleaner.ViewModels;

/// <summary>
/// Orchestrates drive selection, the per-drive scan threads, live progress and the results tree.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly DriveScanner _scanner = new();
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        ScanCommand = new RelayCommand(async _ => await ScanAsync(), _ => CanScan);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsScanning);
        OpenInExplorerCommand = new RelayCommand(p => OpenInExplorer(p as ScanNode));
        RefreshDrivesCommand = new RelayCommand(_ => LoadDrives(), _ => !IsScanning);

        SelectAllCommand = new RelayCommand(_ => TogglePreset(_ => true), _ => HasResults);
        SelectTempAndLogsCommand = new RelayCommand(
            _ => TogglePreset(c => c.Kind is CategoryKind.TempFiles or CategoryKind.Logs or CategoryKind.CrashDumps),
            _ => HasResults);
        SelectNonRedownloadableCommand = new RelayCommand(
            _ => TogglePreset(c => !c.Redownloads), _ => HasResults);

        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasResults));

        LoadDrives();
    }

    public ObservableCollection<DriveItem> Drives { get; } = new();
    public ObservableCollection<DriveScanStatus> ScanStatuses { get; } = new();
    public ObservableCollection<ScanNode> Results { get; } = new();

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand RefreshDrivesCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectTempAndLogsCommand { get; }
    public ICommand SelectNonRedownloadableCommand { get; }

    public bool HasResults => Results.Count > 0;

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetField(ref _isScanning, value))
                OnPropertyChanged(nameof(CanScan));
        }
    }

    public bool CanScan => !IsScanning && Drives.Any(d => d.IsSelected);

    private bool _includeAllUserProfiles = true;
    public bool IncludeAllUserProfiles
    {
        get => _includeAllUserProfiles;
        set => SetField(ref _includeAllUserProfiles, value);
    }

    private ScanNode? _selectedNode;
    public ScanNode? SelectedNode
    {
        get => _selectedNode;
        set => SetField(ref _selectedNode, value);
    }

    private long _totalReclaimable;
    public long TotalReclaimable
    {
        get => _totalReclaimable;
        private set
        {
            if (SetField(ref _totalReclaimable, value))
                OnPropertyChanged(nameof(TotalReclaimableText));
        }
    }

    public string TotalReclaimableText => ByteSize.Format(TotalReclaimable);

    private string _statusMessage = "Select the drives to scan, then press Scan.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    private void LoadDrives()
    {
        Drives.Clear();
        foreach (var drive in DriveProvider.GetScannableDrives())
        {
            drive.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DriveItem.IsSelected))
                    OnPropertyChanged(nameof(CanScan));
            };
            Drives.Add(drive);
        }
        OnPropertyChanged(nameof(CanScan));
    }

    private async Task ScanAsync()
    {
        var selected = Drives.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        Results.Clear();
        ScanStatuses.Clear();
        SelectedNode = null;
        TotalReclaimable = 0;

        var profiles = ProfileProvider.GetProfiles(IncludeAllUserProfiles);
        var targetsByDrive = CatalogResolver.BuildTargets(selected, profiles);

        // One status row (and progress bar) per selected drive.
        var statusByRoot = new Dictionary<string, DriveScanStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in selected)
        {
            var status = new DriveScanStatus
            {
                Root = drive.Root,
                Header = drive.DisplayName,
                Total = targetsByDrive[drive].Count,
                State = ScanState.Pending,
            };
            statusByRoot[drive.Root] = status;
            ScanStatuses.Add(status);
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsScanning = true;
        StatusMessage = "Scanning…";

        // Progress<T> captures this (UI) synchronization context, so handlers run on the UI thread.
        var progress = new Progress<ScanProgress>(p => OnProgress(statusByRoot, p));

        try
        {
            // Each drive scans on its own thread; results stream in as each finishes.
            await Task.WhenAll(selected.Select(drive =>
                RunDriveAsync(drive, targetsByDrive[drive], statusByRoot[drive.Root], progress, token)));

            StatusMessage = token.IsCancellationRequested
                ? "Scan cancelled."
                : $"Scan complete. Found {ByteSize.Format(TotalReclaimable)} that can be reviewed for removal.";
        }
        finally
        {
            IsScanning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task RunDriveAsync(
        DriveItem drive,
        IReadOnlyList<ScanTarget> targets,
        DriveScanStatus status,
        IProgress<ScanProgress> progress,
        CancellationToken token)
    {
        try
        {
            var driveNode = await Task.Run(() => _scanner.ScanDrive(drive, targets, progress, token), token);

            // Back on the UI thread (awaited from the UI context): publish the results.
            if (driveNode.Size > 0 || driveNode.HasChildren)
            {
                driveNode.IsExpanded = true;
                Results.Add(driveNode);
                TotalReclaimable += driveNode.Size;
            }
        }
        catch (OperationCanceledException)
        {
            status.State = ScanState.Cancelled;
        }
        catch (Exception ex)
        {
            status.State = ScanState.Error;
            status.CurrentPath = ex.Message;
        }
    }

    private void OnProgress(IReadOnlyDictionary<string, DriveScanStatus> statusByRoot, ScanProgress p)
    {
        if (!statusByRoot.TryGetValue(p.Root, out var status))
            return;

        status.Total = p.Total;
        status.Completed = p.Completed;
        status.BytesFound = p.BytesFound;
        if (p.CurrentPath is not null)
            status.CurrentPath = p.CurrentPath;

        // Don't overwrite a terminal state (Cancelled/Error) set by the drive task.
        if (status.State is not (ScanState.Cancelled or ScanState.Error))
            status.State = p.State;
    }

    private void Cancel()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling…";
    }

    /// <summary>The top-level category nodes across all scanned drives.</summary>
    private IEnumerable<ScanNode> CategoryNodes()
        => Results.SelectMany(drive => drive.Children);

    /// <summary>
    /// Selects every category matching <paramref name="predicate"/>; if they are already all selected,
    /// deselects them instead — so pressing the same preset button toggles its set on and off.
    /// Checking a category node propagates to its whole subtree via the tri-state logic.
    /// </summary>
    private void TogglePreset(Func<CleanupCategory, bool> predicate)
    {
        var nodes = CategoryNodes()
            .Where(n => n.Category is not null && predicate(n.Category))
            .ToList();
        if (nodes.Count == 0)
            return;

        bool allSelected = nodes.All(n => n.IsChecked == true);
        bool target = !allSelected;
        foreach (var node in nodes)
            node.IsChecked = target;
    }

    private static void OpenInExplorer(ScanNode? node)
    {
        if (node is null)
            return;

        try
        {
            if (Directory.Exists(node.FullPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{node.FullPath}\"",
                    UseShellExecute = true,
                });
            }
            else if (File.Exists(node.FullPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{node.FullPath}\"",
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            // Folder may have been removed since the scan; nothing actionable to do.
        }
    }
}
