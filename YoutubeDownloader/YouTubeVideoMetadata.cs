namespace YoutubeDownloader;

public sealed class YouTubeVideoMetadata
{
    public string VideoId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Channel { get; init; } = "";
    public string Duration { get; init; } = "";
    public string ThumbnailUrl { get; init; } = "";
}
