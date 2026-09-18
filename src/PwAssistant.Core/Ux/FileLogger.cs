namespace PwAssistant.Core.Ux;

public enum LogLevel
{
    Info,
    Warn,
    Error
}

/// <summary>Date-rolling file log. Every line passes through
/// <see cref="LogRedactor"/> — secrets never reach disk.</summary>
public sealed class FileLogger
{
    private readonly string _directory;
    private readonly object _gate = new();

    public FileLogger(string directory)
    {
        _directory = directory;
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
}
