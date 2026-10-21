# YoutubeDownloader

A Windows desktop media downloader built with C#, WPF, and .NET 8. The application integrates `yt-dlp` for YouTube metadata retrieval and downloads, with support for direct media URLs.

## Features

- Retrieve YouTube video metadata, including title, channel, and duration.
- Download video and audio using the available format and quality options.
- Extract audio to MP3 and merge or convert supported media formats using FFmpeg.
- Download media from supported direct-media URLs.
- Browse downloaded files through the My Downloads page.
- View download history, including completed and failed attempts.
- Configure application settings using a dark-themed WPF interface.

## Requirements

To build the application from source, you need:

- **Windows 10 or Windows 11**
- **Visual Studio 2022**
- **.NET 8 SDK**
- The Visual Studio **.NET desktop development** workload

The following executables are expected in the project's `YoutubeDownloader/tools/` directory:

- `yt-dlp.exe` — YouTube metadata retrieval and downloading.
- `ffmpeg.exe` — Audio extraction, media conversion, and merging.
- `ffprobe.exe` — Media information and stream inspection.

These tools are included with the project when the corresponding executable files are present in the repository. You do not need to download them separately.

A supported JavaScript runtime, such as Deno, may also be required for full YouTube extractor functionality, depending on the current `yt-dlp` requirements.

## Project Structure

```text
SourentrYoutubeDownloader/
├── YoutubeDownloader.sln
├── README.md
└── YoutubeDownloader/
    ├── YoutubeDownloader.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    ├── YtDlpService.cs
    ├── DirectMediaDownloadService.cs
    ├── DownloadHistoryService.cs
    ├── DownloadHistoryEntry.cs
    ├── YouTubeVideoMetadata.cs
    └── tools/
        ├── yt-dlp.exe
        ├── ffmpeg.exe
        └── ffprobe.exe
```

The project also contains supporting files for application settings, navigation, and the WPF interface.

## How to Build and Run

### 1. Clone or download the repository

Clone the repository using Git:

```bash
git clone https://github.com/Sourentr/SourentrYoutubeDownloader.git
```

Alternatively, download the repository as a ZIP file from GitHub and extract it.

### 2. Check the bundled tools

Verify that the following files are present:

```text
YoutubeDownloader/tools/yt-dlp.exe
YoutubeDownloader/tools/ffmpeg.exe
YoutubeDownloader/tools/ffprobe.exe
```

If the repository uses Git LFS for these executables, ensure the files have been downloaded correctly rather than receiving Git LFS pointer files.

### 3. Open the solution

Open `YoutubeDownloader.sln` in Visual Studio 2022.

Allow Visual Studio to restore the project dependencies.



## Application Data

Download history and application preferences are stored locally under:

```text
%AppData%\YoutubeDownloader\
```

The application uses `settings.json` for preferences and `history.json` for download history.

Clearing the download history does not delete downloaded media files.

## Notes and Limitations

- Available formats and resolutions depend on the source media.
- The requested video resolution is a maximum; a lower resolution may be selected when the requested quality is unavailable.
- MP3 extraction and some merging or conversion operations require FFmpeg.
- YouTube extractor functionality may depend on updates to `yt-dlp` and its runtime requirements.
- Keep bundled third-party tools updated and obtain them only from their official sources.
- Download only media you are authorised to download and comply with applicable copyright law and platform terms.

## Technologies

- C#
- .NET 8
- Windows Presentation Foundation (WPF)
- yt-dlp
- FFmpeg
