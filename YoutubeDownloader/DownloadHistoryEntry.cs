using System.Text.Json.Serialization;

namespace YoutubeDownloader;

public sealed class DownloadHistoryEntry
{
    public string Title { get; set; } = "Unknown title";
    public string SourceUrl { get; set; } = "";
    public string Format { get; set; } = "";
    public string Status { get; set; } = "Failed";
    public string FilePath { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string DateText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
