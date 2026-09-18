using PwAssistant.Core.Ux;

namespace PwAssistant.Core.Tests;

public sealed class LogRedactorTests
{
    [Fact]
    public void PasswordArgument_IsMasked()
    {
        string line = "startbypatcher user:hero pwd:s3cret role:HeroNick";
        Assert.DoesNotContain("s3cret", LogRedactor.Redact(line));
        Assert.Contains("pwd:***", LogRedactor.Redact(line));
        Assert.Contains("user:hero", LogRedactor.Redact(line));
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
        Assert.Contains("pwd:***", content);
    }
}
