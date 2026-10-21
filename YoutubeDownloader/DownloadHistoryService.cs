using System.Text.Json;

namespace YoutubeDownloader;

public static class DownloadHistoryService
{
    private static readonly string AppDataDirectory = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YoutubeDownloader");

    private static readonly string HistoryFilePath = System.IO.Path.Combine(AppDataDirectory, "history.json");
    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<DownloadHistoryEntry> Load()
    {
        lock (FileLock)
        {
            try
            {
                if (!System.IO.File.Exists(HistoryFilePath))
                    return new List<DownloadHistoryEntry>();

                return JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(
                           System.IO.File.ReadAllText(HistoryFilePath)) ?? new List<DownloadHistoryEntry>();
            }
            catch (Exception)
            {
                // A missing or damaged history file should not prevent the app from opening.
                return new List<DownloadHistoryEntry>();
            }
        }
    }

    public static void Add(DownloadHistoryEntry entry)
    {
        lock (FileLock)
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppDataDirectory);
                List<DownloadHistoryEntry> entries;
                try
                {
                    entries = System.IO.File.Exists(HistoryFilePath)
                        ? JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(System.IO.File.ReadAllText(HistoryFilePath))
                          ?? new List<DownloadHistoryEntry>()
                        : new List<DownloadHistoryEntry>();
                }
                catch (Exception)
                {
                    entries = new List<DownloadHistoryEntry>();
                }

                entries.Insert(0, entry);
                if (entries.Count > 500)
                    entries = entries.Take(500).ToList();

                System.IO.File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(entries, JsonOptions));
            }
            catch (Exception)
            {
                // Logging is best-effort and must not interrupt a download.
            }
        }
    }

    public static void Clear()
    {
        lock (FileLock)
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppDataDirectory);
                System.IO.File.WriteAllText(HistoryFilePath, "[]");
            }
            catch (Exception)
            {
                // The UI will continue to work if history cannot be written.
            }
        }
    }
}
