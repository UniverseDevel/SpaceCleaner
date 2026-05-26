using System.Windows;
using System.Windows.Controls;
using SpaceCleaner.Models;
using SpaceCleaner.Services;
using SpaceCleaner.ViewModels;

namespace SpaceCleaner;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    // TreeView.SelectedItem is read-only and not directly bindable, so we forward the selection
    // change to the view model here.
    private void ResultsTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel vm)
            vm.SelectedNode = e.NewValue as ScanNode;
    }

    private void ViewSelectedFiles_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var roots = SelectedFilesExporter.CollectRoots(vm.Results);
        if (roots.Count == 0)
            return;

        new FileListWindow(roots) { Owner = this }.Show();
    }
}
