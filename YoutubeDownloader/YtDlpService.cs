using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace YoutubeDownloader;

/// <summary>
/// Runs the yt-dlp command-line tool for metadata and media downloads.
/// Put yt-dlp.exe in the project's tools folder. FFmpeg is needed for MP3
/// extraction and for merging/converting some video formats.
/// </summary>
public sealed class YtDlpService
{
    public static bool IsYouTubeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        string host = uri.Host.ToLowerInvariant();
        return host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) ||
               host == "youtu.be" || host.EndsWith(".youtu.be", StringComparison.Ordinal) ||
               host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);
    }

    public async Task<YouTubeVideoMetadata> GetVideoMetadataAsync(
        string videoUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter a valid HTTP or HTTPS video URL.");
        }

        var startInfo = CreateStartInfo();
        AddArguments(startInfo,
            "--ignore-config",
            "--dump-single-json",
            "--skip-download",
            "--no-warnings",
            "--no-playlist",
            "--no-call-home",
            "--no-check-formats",
            "--no-colors",
            videoUrl);

        using Process process = StartProcess(startInfo);
        using CancellationTokenRegistration registration = RegisterKillOnCancellation(process, cancellationToken);

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(FormatError(
                "yt-dlp could not retrieve information for this video.", stderr));
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement root = document.RootElement;

            string title = GetString(root, "title") ?? "Unknown title";
            string channel = GetString(root, "channel") ??
                             GetString(root, "uploader") ?? "Unknown channel";
            string videoId = GetString(root, "id") ?? "";
            string thumbnail = GetString(root, "thumbnail") ?? "";
            double durationSeconds = GetNumber(root, "duration") ?? 0;
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
            string durationText = duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);

            return new YouTubeVideoMetadata
            {
                VideoId = videoId,
                Title = title,
                Channel = channel,
                Duration = durationText,
                ThumbnailUrl = thumbnail
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "yt-dlp returned video information in an unexpected format. " +
                "Make sure yt-dlp.exe is up to date.", ex);
        }
    }

    public async Task<string> DownloadAsync(
        string videoUrl,
        string outputFolder,
        bool audioOnly,
        string qualitySelection,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter a valid HTTP or HTTPS video URL.");
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
            throw new ArgumentException("Choose a save folder first.");

        System.IO.Directory.CreateDirectory(outputFolder);

        var startInfo = CreateStartInfo();
        AddArguments(startInfo,
            "--ignore-config",
            "--no-playlist",
            "--no-warnings",
            "--no-colors",
            "--newline",
            "--progress",
            "--progress-template",
            "download:%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s",
            "--print",
            "after_move:filepath",
            "--no-overwrites",
            "--paths",
            outputFolder,
            "--output",
            "%(title)s.%(ext)s");

        string? ffmpegDirectory = FindFfmpegDirectory();
        if (ffmpegDirectory is not null)
        {
            AddArguments(startInfo, "--ffmpeg-location", ffmpegDirectory);
        }

        if (audioOnly)
        {
            int bitrate = ParseAudioBitrate(qualitySelection);
            AddArguments(startInfo,
                "--extract-audio",
                "--audio-format", "mp3",
                "--audio-quality", bitrate.ToString(CultureInfo.InvariantCulture) + "K");
        }
        else
        {
            int height = ParseVideoHeight(qualitySelection);
            string format = $"bv*[height<={height}]+ba/b[height<={height}]";
            AddArguments(startInfo,
                "--format", format,
                "--recode-video", "mp4");
        }

        AddArguments(startInfo, videoUrl);

        using Process process = StartProcess(startInfo);
        using CancellationTokenRegistration registration = RegisterKillOnCancellation(process, cancellationToken);

        var stderr = new StringBuilder();
        string? savedPath = null;
        Task stderrTask = ReadStandardErrorAsync(process, stderr, progress);
        Task stdoutTask = ReadStandardOutputAsync(process, progress,
            path => savedPath = path, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(FormatError(
                "yt-dlp could not complete the download.", stderr.ToString()));
        }

        if (string.IsNullOrWhiteSpace(savedPath))
        {
            throw new InvalidOperationException(
                "yt-dlp finished but did not report the saved file path. " +
                "Check the output folder and try again.");
        }

        return savedPath;
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        string executable = FindYtDlpExecutable();
        return new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static string FindYtDlpExecutable()
    {
        string[] candidates =
        {
            System.IO.Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe"),
            System.IO.Path.Combine(Environment.CurrentDirectory, "tools", "yt-dlp.exe")
        };

        foreach (string candidate in candidates)
        {
            if (System.IO.File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "yt-dlp.exe was not found. Download the Windows executable from " +
            "https://github.com/yt-dlp/yt-dlp/releases and place it in the project's " +
            "tools folder (tools\\yt-dlp.exe), then rebuild the application.");
    }

    private static string? FindFfmpegDirectory()
    {
        string[] candidates =
        {
            System.IO.Path.Combine(AppContext.BaseDirectory, "tools"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "bin"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg"),
            AppContext.BaseDirectory
        };

        foreach (string candidate in candidates)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(candidate, "ffmpeg.exe")) &&
                System.IO.File.Exists(System.IO.Path.Combine(candidate, "ffprobe.exe")))
            {
                return candidate;
            }
        }

        return null; // yt-dlp can still use FFmpeg installed on PATH.
    }

    private static Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ??
                   throw new InvalidOperationException("Could not start yt-dlp.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Windows could not start yt-dlp.exe. Check that the executable is present " +
                "and is not blocked by Windows.", ex);
        }
    }

    private static CancellationTokenRegistration RegisterKillOnCancellation(
        Process process, CancellationToken token)
    {
        return token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
    }

    private static void AddArguments(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
    }

    private static async Task ReadStandardOutputAsync(
        Process process,
        IProgress<DownloadProgressInfo> progress,
        Action<string> onSavedPath,
        CancellationToken cancellationToken)
    {
        while (await process.StandardOutput.ReadLineAsync() is string line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string trimmed = line.Trim();
            if (trimmed.StartsWith("download:", StringComparison.OrdinalIgnoreCase))
            {
                ReportProgress(trimmed, progress);
            }
            else if (trimmed.Length > 0 && !trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                // --print after_move:filepath emits the completed output path.
                onSavedPath(trimmed.Trim('"'));
            }
        }
    }

    private static async Task ReadStandardErrorAsync(
        Process process,
        StringBuilder stderr,
        IProgress<DownloadProgressInfo> progress)
    {
        while (await process.StandardError.ReadLineAsync() is string line)
        {
            if (line.TrimStart().StartsWith("download:", StringComparison.OrdinalIgnoreCase))
            {
                ReportProgress(line.Trim(), progress);
                continue;
            }

            lock (stderr)
            {
                if (stderr.Length < 12000)
                    stderr.AppendLine(line);
            }
        }
    }

    private static void ReportProgress(string line, IProgress<DownloadProgressInfo> progress)
    {
        int separator = line.IndexOf(':');
        if (separator < 0) return;

        string[] parts = line[(separator + 1)..].Trim().Split('|');
        if (parts.Length < 3) return;

        long downloaded = ParseLong(parts[0]) ?? 0;

        // yt-dlp may know the exact total size or only an estimate. Prefer
        // the exact value, but fall back to the estimate when necessary.
        long? total = parts.Length >= 5
            ? ParseLong(parts[1]) ?? ParseLong(parts[2])
            : ParseLong(parts[1]);

        int speedIndex = parts.Length >= 5 ? 3 : 2;
        double speed = ParseDouble(parts[speedIndex]) ?? 0;

        progress.Report(new DownloadProgressInfo
        {
            BytesDownloaded = downloaded,
            TotalBytes = total,
            BytesPerSecond = Math.Max(0, speed)
        });
    }

    private static long? ParseLong(string value) =>
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed : null;

    private static double? ParseDouble(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed : null;

    private static int ParseVideoHeight(string selection)
    {
        string digits = new(selection.SkipWhile(c => !char.IsDigit(c))
            .TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) && height > 0
            ? height : 1080;
    }

    private static int ParseAudioBitrate(string selection)
    {
        string digits = new(selection.SkipWhile(c => !char.IsDigit(c))
            .TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bitrate) && bitrate > 0
            ? bitrate : 192;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    }

    private static double? GetNumber(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
               value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)
            ? number : null;
    }

    private static string FormatError(string heading, string details)
    {
        string usefulDetails = details.Trim();
        if (usefulDetails.Length > 1600)
            usefulDetails = usefulDetails[^1600..];

        return usefulDetails.Length == 0
            ? heading
            : heading + Environment.NewLine + usefulDetails;
    }
}
