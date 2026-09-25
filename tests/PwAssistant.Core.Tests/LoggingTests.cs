using PwAssistant.Core.Ux;

namespace PwAssistant.Core.Tests;

public sealed class LogRedactorTests
{
    [Fact]
    public void CredentialArguments_AreMasked()
    {
        string line = "startbypatcher user:hero pwd:s3cret role:HeroNick";
        string redacted = LogRedactor.Redact(line);
        Assert.DoesNotContain("s3cret", redacted);
        Assert.DoesNotContain("hero", redacted);
        Assert.DoesNotContain("HeroNick", redacted);
        Assert.Contains("pwd:***", redacted);
        Assert.Contains("user:***", redacted);
        Assert.Contains("role:***", redacted);
    }

    [Fact]
    public void CredentialArguments_AreMaskedCaseInsensitively()
    {
        string redacted = LogRedactor.Redact("startbypatcher USER:hero PWD:s3cret");
        Assert.DoesNotContain("hero", redacted);
        Assert.DoesNotContain("s3cret", redacted);
    }

    [Fact]
    public void Null_IsEmpty()
    {
        Assert.Equal(string.Empty, LogRedactor.Redact(null));
    }
}

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ditto-log-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Write_RedactsSecrets_OnDisk()
    {
        var log = new FileLogger(_dir);
        log.Info("launch user:hero pwd:s3cret");

        string content = File.ReadAllText(Directory.GetFiles(_dir, "app-*.log").Single());
        Assert.DoesNotContain("s3cret", content);
        Assert.DoesNotContain("hero", content);
        Assert.Contains("pwd:***", content);
    }

    [Fact]
    public void Constructor_RemovesLogsOlderThanRetention()
    {
        string old = Path.Combine(_dir, "app-2000-01-01.log");
        string recent = Path.Combine(_dir, "app-2099-01-01.log");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(old, "old");
        File.WriteAllText(recent, "recent");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-(FileLogger.RetentionDays + 1)));

        _ = new FileLogger(_dir);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void ApplyRetention_CapsTotalSize_OldestFirst()
    {
        Directory.CreateDirectory(_dir);
        string oldest = Path.Combine(_dir, "app-2026-01-01.log");
        string middle = Path.Combine(_dir, "app-2026-01-02.log");
        string newest = Path.Combine(_dir, "app-2026-01-03.log");
        File.WriteAllText(oldest, new string('a', 60));
        File.WriteAllText(middle, new string('b', 60));
        File.WriteAllText(newest, new string('c', 60));
        DateTime now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(oldest, now.AddHours(-3));
        File.SetLastWriteTimeUtc(middle, now.AddHours(-2));
        File.SetLastWriteTimeUtc(newest, now.AddHours(-1));

        int removed = FileLogger.ApplyRetention(_dir, retentionDays: 365, maxTotalBytes: 100);

        Assert.Equal(2, removed);
        Assert.False(File.Exists(oldest));
        Assert.False(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void ApplyRetention_IgnoresForeignFiles()
    {
        Directory.CreateDirectory(_dir);
        string foreign = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(foreign, "keep me");

        int removed = FileLogger.ApplyRetention(_dir, retentionDays: 0, maxTotalBytes: 0);

        Assert.Equal(0, removed);
        Assert.True(File.Exists(foreign));
    }
}
