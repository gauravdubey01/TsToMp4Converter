using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TsToMp4Converter.Models;
using TsToMp4Converter.Services;

namespace TsToMp4Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FfmpegService _ffmpeg = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<ConversionJob> Files { get; } = [];

    [ObservableProperty]
    private string? _outputFolder;

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _completedFiles;

    [ObservableProperty]
    private bool _ffmpegMissing;

    public string Version => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0";

    public MainViewModel()
    {
        FfmpegMissing = !_ffmpeg.FfmpegExists;
    }

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "TS Video Files (*.ts)|*.ts|All Files (*.*)|*.*",
            Title = "Select TS files to convert"
        };

        if (dialog.ShowDialog() != true) return;

        AddFilesFromPaths(dialog.FileNames);
    }

    public void AddFilesFromPaths(IEnumerable<string> paths)
    {
        foreach (var file in paths)
        {
            var ext = System.IO.Path.GetExtension(file);
            if (!string.Equals(ext, ".ts", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!Files.Any(f => f.InputPath.Equals(file, StringComparison.OrdinalIgnoreCase)))
            {
                Files.Add(new ConversionJob(file));
            }
        }

        UpdateCounts();
        UpdateAutoOutputFolder();
    }

    [RelayCommand]
    private void RemoveFile(ConversionJob job)
    {
        Files.Remove(job);
        UpdateCounts();
        if (Files.Count == 0)
        {
            OverallProgress = 0;
            CompletedFiles = 0;
            TotalFiles = 0;
            StatusText = "Ready";
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        Files.Clear();
        OverallProgress = 0;
        CompletedFiles = 0;
        TotalFiles = 0;
        StatusText = "Ready";
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        var folder = OutputFolder;
        if (string.IsNullOrEmpty(folder) && Files.Count > 0)
        {
            folder = Path.GetDirectoryName(Files[0].InputPath);
        }
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            Process.Start("explorer.exe", $"\"{folder}\"");
        }
    }

    [RelayCommand]
    private async Task ConvertAll()
    {
        if (IsConverting || Files.Count == 0) return;

        var pending = Files.Where(f => f.Status is ConversionStatus.Queued or ConversionStatus.Failed).ToList();
        if (pending.Count == 0)
        {
            StatusText = "No files to convert.";
            return;
        }

        _cts = new CancellationTokenSource();
        IsConverting = true;
        OverallProgress = 0;
        CompletedFiles = 0;
        TotalFiles = pending.Count;

        for (int i = 0; i < pending.Count; i++)
        {
            var job = pending[i];
            if (_cts.Token.IsCancellationRequested) break;

            SetOutputPathIfNeeded(job);
            job.Status = ConversionStatus.Converting;
            job.Progress = 0;
            job.ErrorMessage = string.Empty;
            StatusText = $"Converting {i + 1} of {pending.Count}: {job.FileName}";

            var jobProgress = new Progress<double>(p =>
            {
                job.Progress = p;
                UpdateOverallProgress(pending);
            });

            try
            {
                await _ffmpeg.ConvertAsync(job, jobProgress, _cts.Token);

                if (_cts.Token.IsCancellationRequested)
                {
                    job.Status = ConversionStatus.Cancelled;
                }
                else
                {
                    job.Status = ConversionStatus.Completed;
                    job.Progress = 100;
                    CompletedFiles++;
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = ConversionStatus.Cancelled;
            }
            catch (Exception ex)
            {
                job.Status = ConversionStatus.Failed;
                job.ErrorMessage = ex.Message;
            }

            UpdateOverallProgress(pending);
        }

        IsConverting = false;
        var success = Files.Count(f => f.Status == ConversionStatus.Completed);
        var failed = Files.Count(f => f.Status == ConversionStatus.Failed);
        var cancelled = Files.Count(f => f.Status == ConversionStatus.Cancelled);
        StatusText = $"Done — {success} converted, {failed} failed, {cancelled} cancelled";
        _cts = null;

        if (success > 0)
        {
            OpenOutputFolder();
        }
    }

    [RelayCommand]
    private void CancelConversion()
    {
        _cts?.Cancel();
        StatusText = "Cancelling...";
    }

    private void UpdateOverallProgress(List<ConversionJob> pending)
    {
        if (pending.Count == 0) return;
        OverallProgress = pending.Average(j => j.Progress);
    }

    private void UpdateCounts()
    {
        TotalFiles = Files.Count;
        CompletedFiles = Files.Count(f => f.Status == ConversionStatus.Completed);
    }

    private void UpdateAutoOutputFolder()
    {
        if (string.IsNullOrEmpty(OutputFolder) && Files.Count > 0)
        {
            var dir = System.IO.Path.GetDirectoryName(Files[0].InputPath);
            if (!string.IsNullOrEmpty(dir))
                OutputFolder = dir;
        }
    }

    private void SetOutputPathIfNeeded(ConversionJob job)
    {
        if (string.IsNullOrEmpty(job.OutputPath))
        {
            var folder = string.IsNullOrEmpty(OutputFolder)
                ? System.IO.Path.GetDirectoryName(job.InputPath) ?? string.Empty
                : OutputFolder;

            var name = System.IO.Path.GetFileNameWithoutExtension(job.InputPath);
            job.OutputPath = System.IO.Path.Combine(folder, name + ".mp4");
        }
    }
}
