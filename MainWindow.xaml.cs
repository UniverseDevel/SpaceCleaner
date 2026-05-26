using System.Windows;
using System.Windows.Controls;
using SpaceCleaner.Models;
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
}
