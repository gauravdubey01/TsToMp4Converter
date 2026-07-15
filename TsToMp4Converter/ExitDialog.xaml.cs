using System.Diagnostics;
using System.Windows;

namespace TsToMp4Converter;

public partial class ExitDialog : Window
{
    public bool Confirmed { get; private set; }

    public ExitDialog()
    {
        InitializeComponent();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void KofiButton_Click(object sender, RoutedEventArgs e)
    {
        OpenKofi();
    }

    private void KofiLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        OpenKofi();
        e.Handled = true;
    }

    private static void OpenKofi()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://ko-fi.com/gauravdubeypro",
            UseShellExecute = true
        });
    }
}