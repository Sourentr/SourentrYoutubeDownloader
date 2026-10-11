# YoutubeDownloader — yt-dlp integration

This version replaces the YouTube Data API metadata dependency with yt-dlp for metadata lookup and YouTube downloads. The existing direct-media downloader remains in place for non-YouTube direct file URLs.

## Setup

1. Download `yt-dlp.exe` from the official releases: https://github.com/yt-dlp/yt-dlp/releases
2. Place it at `YoutubeDownloader/tools/yt-dlp.exe`.
3. For MP3 extraction and merging/converting some video formats, install FFmpeg and make `ffmpeg.exe` and `ffprobe.exe` available either in the same `tools` folder or on your Windows PATH.
4. Open `YoutubeDownloader.sln` in Visual Studio 2022 with the .NET 8 SDK installed. Build and run.

The project copies files in the `tools` folder to the build output folder.

## Notes

- Video quality is treated as a maximum resolution; if the source has no stream at that resolution, yt-dlp can select a lower available one.
- MP3 output and some MP4 merging/re-encoding operations require FFmpeg. Full YouTube extractor support may also require a supported JavaScript runtime such as Deno.
- Download only media you are authorised to download and follow the applicable platform terms and copyright law.
