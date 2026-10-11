
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeDownloader
{
    public sealed class DownloadProgressInfo
    {
        public long BytesDownloaded { get; init; }
        public long? TotalBytes { get; init; }
        public double BytesPerSecond { get; init; }

        public double? Percentage =>
            TotalBytes is > 0
                ? BytesDownloaded * 100.0 / TotalBytes.Value
                : null;
    }

    public sealed class DirectMediaDownloadService
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public async Task<string> DownloadAsync(
            string sourceUrl,
            string outputFolder,
            IProgress<DownloadProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(
                    sourceUrl,
                    UriKind.Absolute,
                    out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException(
                    "Enter a valid HTTP or HTTPS media URL.");
            }

            string host = uri.Host.ToLowerInvariant();

            if (host == "youtube.com" ||
                host.EndsWith(".youtube.com") ||
                host == "youtu.be" ||
                host.EndsWith(".youtu.be") ||
                host == "youtube-nocookie.com" ||
                host.EndsWith(".youtube-nocookie.com"))
            {
                throw new ArgumentException(
                    "A YouTube watch link is not a direct media URL. " +
                    "Use an authorized download source for the media.");
            }

            Directory.CreateDirectory(outputFolder);

            using var response = await Client.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            string? mediaType =
                response.Content.Headers.ContentType?.MediaType;

            if (mediaType != null &&
                (mediaType.Equals(
                    "text/html",
                    StringComparison.OrdinalIgnoreCase) ||
                 mediaType.Equals(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "The link returned a webpage or JSON, not a media file.");
            }

            string fileName = Path.GetFileName(uri.LocalPath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "downloaded-media";
            }

            fileName = Uri.UnescapeDataString(fileName);

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            // Avoid overwriting an existing file.
            string destination = Path.Combine(outputFolder, fileName);

            string extension = Path.GetExtension(destination);
            string nameWithoutExtension =
                Path.GetFileNameWithoutExtension(destination);

            int suffix = 1;

            while (File.Exists(destination))
            {
                destination = Path.Combine(
                    outputFolder,
                    $"{nameWithoutExtension} ({suffix}){extension}");

                suffix++;
            }

            string temporaryPath = destination + ".partial";

            try
            {
                await using var input =
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken);

                await using var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                long bytesDownloaded = 0;
                long? totalBytes = response.Content.Headers.ContentLength;

                var buffer = new byte[81920];
                var timer = System.Diagnostics.Stopwatch.StartNew();
                long lastReportedBytes = 0;
                double lastReportedSeconds = 0;

                while (true)
                {
                    int bytesRead = await input.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);

                    if (bytesRead == 0)
                        break;

                    await output.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);

                    bytesDownloaded += bytesRead;

                    double elapsedSeconds = timer.Elapsed.TotalSeconds;

                    if (elapsedSeconds - lastReportedSeconds >= 0.25)
                    {
                        double speed =
                            (bytesDownloaded - lastReportedBytes) /
                            Math.Max(
                                elapsedSeconds - lastReportedSeconds,
                                0.001);

                        progress.Report(new DownloadProgressInfo
                        {
                            BytesDownloaded = bytesDownloaded,
                            TotalBytes = totalBytes,
                            BytesPerSecond = speed
                        });

                        lastReportedBytes = bytesDownloaded;
                        lastReportedSeconds = elapsedSeconds;
                    }
                }

                await output.FlushAsync(cancellationToken);

                progress.Report(new DownloadProgressInfo
                {
                    BytesDownloaded = bytesDownloaded,
                    TotalBytes = totalBytes,
                    BytesPerSecond = 0
                });

                output.Close();

                File.Move(temporaryPath, destination);

                return destination;
            }
            finally
            {
                // Remove an incomplete file after an error or cancellation.
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}

