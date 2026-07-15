using System.Windows;

namespace TsToMp4Converter;

public partial class TutorialWindow : Window
{
    public TutorialWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}