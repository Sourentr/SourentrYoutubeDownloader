using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;


namespace YoutubeDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly YtDlpService _ytDlpService = new YtDlpService();
        private readonly DirectMediaDownloadService _downloadService = new DirectMediaDownloadService();
        public MainWindow()
        {
            InitializeComponent();

            OutputPathTextBox.Text = System.IO.Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "Downloads");
        }

        private void BrowseFolderButton_Click(
        object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();

            dialog.Description = "Choose a folder for downloaded media";
            dialog.UseDescriptionForTitle = true;

            if (dialog.ShowDialog() ==
                System.Windows.Forms.DialogResult.OK)
            {
                OutputPathTextBox.Text = dialog.SelectedPath;
                StatusText.Text = "Save location updated.";
            }
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    UrlTextBox.Text = System.Windows.Clipboard.GetText().Trim();
                    StatusText.Text = "Link pasted. You can now analyze it.";
                }
                else
                {
                    StatusText.Text = "Your clipboard does not contain text.";
                }
            }
            catch (Exception)
            {
                StatusText.Text = "Unable to access the clipboard.";
            }
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                StatusText.Text = "Please enter a YouTube URL.";
                return;
            }

            // Disable the button to prevent repeated requests.
            if (sender is System.Windows.Controls.Button button)
                button.IsEnabled = false;

            try
            {
                StatusText.Text = "Retrieving video information...";
                VideoTitleText.Text = "Loading video information...";
                ChannelText.Text = "Contacting YouTube...";
                DurationText.Text = "Duration: —";

                YouTubeVideoMetadata video =
                    await _ytDlpService.GetVideoMetadataAsync(url);

                // Display the metadata returned by YouTube.
                VideoTitleText.Text = video.Title;
                ChannelText.Text = "Channel: " + video.Channel;
                DurationText.Text = "Duration: " + video.Duration;

                StatusText.Text = "Video information retrieved successfully.";
            }
            catch (ArgumentException ex)
            {
                StatusText.Text = ex.Message;
            }
            catch (HttpRequestException)
            {
                StatusText.Text =
                    "Connection failed. Check your internet connection.";
            }
            catch (TaskCanceledException)
            {
                StatusText.Text =
                    "The request timed out. Please try again.";
            }
            catch (InvalidOperationException ex)
            {
                StatusText.Text = ex.Message;
            }
            catch (Exception)
            {
                StatusText.Text =
                    "An unexpected error occurred while retrieving video information.";
            }
            finally
            {
                if (sender is System.Windows.Controls.Button analyzeButton)
                    analyzeButton.IsEnabled = true;
            }
        }

        private void FormatComboBox_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (QualityComboBox == null || FormatComboBox == null)
                return;

            bool isAudio = FormatComboBox.SelectedIndex == 1;

            QualityComboBox.Items.Clear();

            if (isAudio)
            {
                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "128 kbps" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "192 kbps" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "320 kbps" });
            }
            else
            {
                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "360p" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "480p" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "720p" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "1080p" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "1440p" });

                QualityComboBox.Items.Add(
                    new ComboBoxItem { Content = "2160p (4K)" });
            }

            QualityComboBox.SelectedIndex = isAudio ? 1 : 2;
        }
        private async void DownloadButton_Click(
    object sender, RoutedEventArgs e)
        {
            string sourceUrl = UrlTextBox.Text.Trim();
            string outputFolder = OutputPathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                StatusText.Text = "Enter a video URL first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                StatusText.Text = "Choose a save folder first.";
                return;
            }

            if (!Uri.TryCreate(
                    sourceUrl,
                    UriKind.Absolute,
                    out Uri? parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp &&
                 parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                StatusText.Text =
                    "Enter a valid HTTP or HTTPS URL.";
                return;
            }

            DownloadButton.IsEnabled = false;

            DownloadProgressBar.IsIndeterminate = true;
            DownloadProgressBar.Value = 0;
            ProgressPercentText.Text = "—";
            ProgressStatusText.Text = "Preparing download...";
            SpeedText.Text = "Speed: —";
            RemainingText.Text = "Time remaining: —";

            try
            {
                StatusText.Text = "Starting media download...";

                using var timeout =
                    new CancellationTokenSource(TimeSpan.FromHours(2));

                var progress = new Progress<DownloadProgressInfo>(info =>
                {
                    if (info.Percentage.HasValue)
                    {
                        double percentage = Math.Clamp(
                            info.Percentage.Value, 0, 100);

                        DownloadProgressBar.IsIndeterminate = false;
                        DownloadProgressBar.Value = percentage;
                        ProgressPercentText.Text =
                            $"{percentage:0.0}%";

                        ProgressStatusText.Text = "Downloading...";
                    }
                    else
                    {
                        // Keep the progress bar animated while yt-dlp is
                        // working but has not reported a usable total size.
                        DownloadProgressBar.IsIndeterminate = true;
                        ProgressPercentText.Text = "—";
                        ProgressStatusText.Text =
                            "Working — total size not available yet";
                    }

                    SpeedText.Text =
                        $"Speed: {FormatBytes(info.BytesPerSecond)}/s";

                    if (info.TotalBytes.HasValue &&
                        info.BytesPerSecond > 0)
                    {
                        long remainingBytes = Math.Max(
                            0,
                            info.TotalBytes.Value - info.BytesDownloaded);

                        TimeSpan remainingTime = TimeSpan.FromSeconds(
                            remainingBytes / info.BytesPerSecond);

                        RemainingText.Text =
                            $"Time remaining: {remainingTime:hh\\:mm\\:ss}";
                    }
                    else
                    {
                        RemainingText.Text =
                            "Time remaining: —";
                    }
                });

                string savedPath;

                if (YtDlpService.IsYouTubeUrl(sourceUrl))
                {
                    bool audioOnly = FormatComboBox.SelectedIndex == 1;
                    string selectedQuality =
                        (QualityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                        ?? (audioOnly ? "192 kbps" : "1080p");

                    StatusText.Text = audioOnly
                        ? "Extracting audio and downloading..."
                        : "Downloading video...";

                    savedPath = await _ytDlpService.DownloadAsync(
                        sourceUrl,
                        outputFolder,
                        audioOnly,
                        selectedQuality,
                        progress,
                        timeout.Token);
                }
                else
                {
                    // Preserve the existing direct-file download workflow for non-YouTube URLs.
                    savedPath = await _downloadService.DownloadAsync(
                        sourceUrl,
                        outputFolder,
                        progress,
                        timeout.Token);
                }

                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = 100;
                ProgressPercentText.Text = "100%";
                ProgressStatusText.Text = "Download complete";
                SpeedText.Text = "Speed: —";
                RemainingText.Text = "Time remaining: 00:00:00";

                StatusText.Text = $"Saved successfully: {savedPath}";
            }
            catch (ArgumentException ex)
            {
                ProgressStatusText.Text = "Invalid URL";
                StatusText.Text = "URL error: " + ex.Message;
            }
            catch (HttpRequestException ex)
            {
                ProgressStatusText.Text = "Download failed";
                StatusText.Text =
                    $"Could not download the file: {ex.Message}";
            }
            catch (InvalidOperationException ex)
            {
                ProgressStatusText.Text = "Download failed";
                StatusText.Text = ex.Message;
            }
            catch (OperationCanceledException)
            {
                ProgressStatusText.Text = "Download timed out";
                StatusText.Text =
                    "The download took too long. Please try again.";
            }
            catch (IOException ex)
            {
                ProgressStatusText.Text = "File error";
                StatusText.Text =
                    $"Could not save the file: {ex.Message}";
            }
            catch (Exception ex)
            {
                ProgressStatusText.Text = "Download failed";
                StatusText.Text =
                    $"Unexpected error: {ex.Message}";
            }
            finally
            {
                DownloadProgressBar.IsIndeterminate = false;
                DownloadButton.IsEnabled = true;
            }
        }

        private static string FormatBytes(double bytesPerSecond)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            int unitIndex = 0;
            double size = Math.Max(0, bytesPerSecond);

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }
    }
}