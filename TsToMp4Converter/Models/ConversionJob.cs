using CommunityToolkit.Mvvm.ComponentModel;

namespace TsToMp4Converter.Models;

public partial class ConversionJob : ObservableObject
{
    public string InputPath { get; }
    public string OutputPath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(InputPath);

    [ObservableProperty]
    private ConversionStatus _status = ConversionStatus.Queued;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ConversionJob(string inputPath)
    {
        InputPath = inputPath;
    }
}
