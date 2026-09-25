namespace PwAssistant.Core.Ux;

public enum LogLevel
{
    Info,
    Warn,
    Error
}

/// <summary>Date-rolling file log. Every line passes through
/// <see cref="LogRedactor"/> — secrets never reach disk.
/// Retention (30 days + 50 MB cap) is enforced best-effort at startup:
/// logging must never break the app.</summary>
public sealed class FileLogger
{
    public const int RetentionDays = 30;
    public const long MaxTotalBytes = 50L * 1024 * 1024;

    private readonly string _directory;
    private readonly object _gate = new();

    public FileLogger(string directory)
    {
        _directory = directory;
        try
        {
            ApplyRetention(directory, RetentionDays, MaxTotalBytes);
        }
        catch
        {
            // Best effort: a dirty log folder never blocks startup.
        }
    }

    public static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PwAssistant", "logs");

    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    public void Write(LogLevel level, string message)
    {
        string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{level}] {LogRedactor.Redact(message)}{Environment.NewLine}";
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            File.AppendAllText(Path.Combine(_directory, $"app-{DateTimeOffset.Now:yyyy-MM-dd}.log"), line);
        }
    }

    /// <summary>
    /// Deletes app-*.log files older than <paramref name="retentionDays"/>,
    /// then the oldest remaining ones while the total exceeds
    /// <paramref name="maxTotalBytes"/>. Returns the removed count.
    /// Only the app-*.log pattern is ever touched.
    /// </summary>
    public static int ApplyRetention(string directory, int retentionDays, long maxTotalBytes)
    {
        if (!Directory.Exists(directory))
            return 0;
        List<FileInfo> files;
        try
        {
            files = Directory.GetFiles(directory, "app-*.log")
                .Select(path => new FileInfo(path))
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();
        }
        catch
        {
            return 0;
        }
        int removed = 0;
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var survivors = new List<FileInfo>();
        foreach (FileInfo file in files)
        {
            if (file.LastWriteTimeUtc < cutoff)
            {
                if (TryDelete(file))
                    removed++;
            }
            else
            {
                survivors.Add(file);
            }
        }
        long total = survivors.Sum(f => f.Length);
        foreach (FileInfo file in survivors)
        {
            if (total <= maxTotalBytes)
                break;
            long length = file.Length;
            if (TryDelete(file))
            {
                removed++;
                total -= length;
            }
        }
        return removed;
    }

    private static bool TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch
        {
            // Best effort: one locked file never blocks the cleanup.
            return false;
        }
    }
}
