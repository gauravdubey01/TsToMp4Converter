using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using TsToMp4Converter.ViewModels;

namespace TsToMp4Converter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var dialog = new ExitDialog { Owner = this };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            e.Cancel = true;

        base.OnClosing(e);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var tutorial = new TutorialWindow();
        tutorial.Owner = this;
        tutorial.ShowDialog();
    }

    private void KofiButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://ko-fi.com/gauravdubeypro",
            UseShellExecute = true
        });
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            vm.AddFilesFromPaths(files.Where(f => f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }
}
