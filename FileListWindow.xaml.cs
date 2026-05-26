using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using SpaceCleaner.Infrastructure;
using SpaceCleaner.Models;
using SpaceCleaner.Services;

namespace SpaceCleaner;

/// <summary>
/// A flat, sortable list of every file under the selected items (full path + size). Enumerates on a
/// background thread so the window stays responsive on large selections.
/// </summary>
public partial class FileListWindow : Window
{
    private readonly IReadOnlyList<string> _roots;
    private readonly CancellationTokenSource _cts = new();
    private IReadOnlyList<SelectedFileEntry> _files = Array.Empty<SelectedFileEntry>();

    public FileListWindow(IReadOnlyList<string> roots)
    {
        InitializeComponent();
        _roots = roots;
        Loaded += OnLoaded;
        Closed += (_, _) => _cts.Cancel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var token = _cts.Token;
        try
        {
            _files = await Task.Run(() => SelectedFilesExporter.CollectFiles(_roots, token), token);

            Grid.ItemsSource = _files;
            long total = _files.Sum(f => f.Size);
            Summary.Text = $"{_files.Count:N0} files · {ByteSize.Format(total)}";
            Loading.Visibility = Visibility.Collapsed;
            SaveButton.IsEnabled = _files.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Window closed while listing.
        }
        catch (Exception ex)
        {
            Loading.Text = $"Could not list files: {ex.Message}";
        }
    }

    private void SaveCsv_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save list of selected files",
            FileName = $"SpaceCleaner-selected-files-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            Filter = "CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt",
            DefaultExt = "csv",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            SelectedFilesExporter.WriteCsv(_files, dialog.FileName);
            Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the CSV:\n{ex.Message}", "SpaceCleaner",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Grid_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not SelectedFileEntry file)
            return;

        try
        {
            if (File.Exists(file.Path))
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{file.Path}\"", UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            // File may have been removed since listing.
        }
    }
}
