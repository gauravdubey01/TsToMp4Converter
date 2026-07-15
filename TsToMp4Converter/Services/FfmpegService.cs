using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TsToMp4Converter.Models;

namespace TsToMp4Converter.Services;

public partial class FfmpegService
{
    private readonly string _ffmpegPath;

    public FfmpegService()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _ffmpegPath = Path.Combine(baseDir, "bin", "ffmpeg.exe");
    }

    public string FfmpegPath => _ffmpegPath;

    public bool FfmpegExists => File.Exists(_ffmpegPath);

    public async Task ConvertAsync(
        ConversionJob job,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Path.GetDirectoryName(job.OutputPath)
            ?? Path.GetDirectoryName(job.InputPath)!;

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{job.InputPath}\" -c:v copy -c:a copy -map 0 -y \"{job.OutputPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var totalDuration = await GetDurationAsync(job.InputPath, cancellationToken);

        var tcs = new TaskCompletionSource<bool>();
        var outputLines = new List<string>();

        process.Start();

        process.BeginErrorReadLine();

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (outputLines) outputLines.Add(e.Data);

            if (totalDuration > 0)
            {
                var match = TimeRegex().Match(e.Data);
                if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, out var current))
                {
                    var pct = Math.Min(current.TotalSeconds / totalDuration * 100.0, 99.9);
                    progress?.Report(pct);
                }
            }
        };

        process.Exited += (_, _) => tcs.TrySetResult(true);

        using var reg = cancellationToken.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            tcs.TrySetResult(false);
        });

        await tcs.Task;

        if (cancellationToken.IsCancellationRequested)
        {
            job.Status = ConversionStatus.Cancelled;
            return;
        }

        lock (outputLines)
        {
            var errorText = string.Join(Environment.NewLine, outputLines);
            if (process.ExitCode != 0)
            {
                var msg = ExtractErrorMessage(errorText);
                throw new InvalidOperationException($"FFmpeg error (exit code {process.ExitCode}): {msg}");
            }
        }

        if (!File.Exists(job.OutputPath))
            throw new InvalidOperationException("Output file was not created.");

        progress?.Report(100);
    }

    private static string ExtractErrorMessage(string errorText)
    {
        var lines = errorText.Split('\n');
        var relevant = lines
            .Where(l => l.Contains("Error", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("not found", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return relevant.Count > 0
            ? string.Join("; ", relevant.Select(l => l.Trim()).Distinct())
            : "Unknown error occurred during conversion.";
    }

    public async Task<double> GetDurationAsync(string filePath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var match = DurationRegex().Match(error);
        if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, out var duration))
            return duration.TotalSeconds;

        return 0;
    }

    [GeneratedRegex(@"time=(\d{2}:\d{2}:\d{2}\.\d+)")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"Duration: (\d{2}:\d{2}:\d{2}\.\d+)")]
    private static partial Regex DurationRegex();
}
