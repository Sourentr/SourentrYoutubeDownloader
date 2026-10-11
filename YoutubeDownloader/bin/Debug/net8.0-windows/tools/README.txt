External tools for YoutubeDownloader

1. Download the Windows executable (yt-dlp.exe) from the official releases page:
   https://github.com/yt-dlp/yt-dlp/releases
2. Place yt-dlp.exe directly in this tools folder.
3. For full YouTube extractor support, install a supported JavaScript runtime such as Deno
   (https://deno.com/) and ensure it is available on PATH.
4. For MP3 extraction and for merging/converting some video formats, install FFmpeg.
   Place ffmpeg.exe and ffprobe.exe in this folder, or install FFmpeg and add its bin
   directory to your Windows PATH.
5. Rebuild the project in Visual Studio. The tools files are configured to copy to
   the application output folder.

Do not download executables from untrusted mirrors. Keep yt-dlp updated because
video-site extractors can stop working when sites change.
