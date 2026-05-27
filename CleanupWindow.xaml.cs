using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SpaceCleaner.Infrastructure;
using SpaceCleaner.Models;
using SpaceCleaner.Services;

namespace SpaceCleaner;

/// <summary>
/// Runs the deletion of every file under the selected items, showing a live, auto-scrolling list with
/// a per-file status (Deleted / Skipped + reason). The delete loop runs on a background thread; the UI
/// is refreshed in batches by a dispatcher timer so it stays responsive on very large selections.
/// </summary>
public partial class CleanupWindow : Window
{
    private readonly IReadOnlyList<string> _roots;
    private readonly Action? _onRescan;
    private readonly CancellationTokenSource _cts = new();

    private CleanupItem[] _items = Array.Empty<CleanupItem>();
    private ListCollectionView? _view;
    private DispatcherTimer? _timer;

    private int _processed;          // written by delete thread, read by UI (via Volatile)
    private volatile bool _finished;
    private int _shown;
    private int _deleted;
    private int _skipped;
    private long _freed;
    private string? _logPath;

    public CleanupWindow(IReadOnlyList<string> roots, Action? onRescan)
    {
        InitializeComponent();
        _roots = roots;
        _onRescan = onRescan;
        Loaded += OnLoaded;
        Closed += (_, _) => _cts.Cancel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var token = _cts.Token;

        List<SelectedFileEntry> files;
        try
        {
            files = await Task.Run(() => SelectedFilesExporter.CollectFiles(_roots, token), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        _items = files.Select(f => new CleanupItem(f.Path, f.Size)).ToArray();
        _view = new ListCollectionView(_items);
        Grid.ItemsSource = _view;

        long totalBytes = _items.Sum(i => i.Size);
        Bar.Maximum = Math.Max(1, _items.Length);
        Loading.Visibility = Visibility.Collapsed;
        Summary.Text = $"Deleting {_items.Length:N0} files ({ByteSize.Format(totalBytes)})…";

        if (_items.Length == 0)
        {
            Finish(cancelled: false);
            return;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(80) };
        _timer.Tick += OnTick;
        _timer.Start();

        _ = Task.Run(() => DeleteLoop(token), token);
    }

    private void DeleteLoop(CancellationToken token)
    {
        using var log = new CleanupLog(_items.Length, _roots);
        _logPath = log.Path;

        int deleted = 0, skipped = 0;
        long freed = 0;

        for (int i = 0; i < _items.Length; i++)
        {
            if (token.IsCancellationRequested)
                break;

            var item = _items[i];
            var (ok, error) = FileCleaner.TryDelete(item.Path);
            if (ok)
            {
                item.Status = CleanupStatus.Deleted;
                deleted++;
                freed += item.Size;
            }
            else
            {
                item.Status = CleanupStatus.Skipped;
                item.Error = error;
                skipped++;
            }

            log.WriteResult(item.Status, item.Path, item.Size, item.Error);
            Volatile.Write(ref _processed, i + 1); // publishes the item's status to the UI thread
        }

        log.WriteSummary(deleted, freed, skipped, token.IsCancellationRequested);
        _finished = true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        int processed = Volatile.Read(ref _processed);

        for (; _shown < processed; _shown++)
        {
            var item = _items[_shown];
            if (item.Status == CleanupStatus.Deleted)
            {
                _deleted++;
                _freed += item.Size;
            }
            else
            {
                _skipped++;
            }
            item.NotifyStatus();
        }

        Bar.Value = processed;
        Summary.Text = $"Deleting… {processed:N0}/{_items.Length:N0}  ·  deleted {_deleted:N0} " +
                       $"({ByteSize.Format(_freed)}), skipped {_skipped:N0}";

        if (processed > 0)
            Grid.ScrollIntoView(_items[processed - 1]);

        if (_finished || processed >= _items.Length)
            Finish(_cts.IsCancellationRequested);
    }

    private void Finish(bool cancelled)
    {
        _timer?.Stop();
        _timer = null;

        CancelButton.IsEnabled = false;
        CancelButton.Visibility = Visibility.Collapsed;
        RescanButton.Visibility = _onRescan is null ? Visibility.Collapsed : Visibility.Visible;
        FailedFilter.IsEnabled = _skipped > 0;
        RescanHint.Visibility = Visibility.Visible;

        var prefix = cancelled ? "Cancelled." : "Done.";
        var logNote = _logPath is null ? string.Empty : $"   ·   Audit log: {_logPath}";
        Summary.Text = $"{prefix}  Deleted {_deleted:N0} files ({ByteSize.Format(_freed)}), " +
                       $"skipped {_skipped:N0}.{logNote}";
    }

    private void FailedFilter_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_view is null)
            return;

        _view.Filter = FailedFilter.IsChecked == true
            ? o => o is CleanupItem { Failed: true }
            : null;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        CancelButton.IsEnabled = false;
        Summary.Text = "Cancelling…";
    }

    private void Rescan_OnClick(object sender, RoutedEventArgs e)
    {
        _onRescan?.Invoke();
        Close();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Grid_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not CleanupItem item)
            return;
        try
        {
            if (File.Exists(item.Path))
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{item.Path}\"", UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            // Gone already.
        }
    }
}
