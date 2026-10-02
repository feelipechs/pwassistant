using PwAssistant.Core.Input;

namespace PwAssistant.Core.Tests;

public sealed class SenderContractTests
{
    [Fact]
    public void Workload_RoundTripsThroughJson()
    {
        var workload = new SenderWorkload
        {
            ProcessId = 1234,
            Verbose = true,
            Actions =
            {
                SenderAction.Key(113),
                SenderAction.Click(0.5, 0.25, "right"),
                SenderAction.Sleep(250),
            }
        };

        SenderWorkload parsed = SenderCodec.FromJson(SenderCodec.ToJson(workload));

        Assert.Equal(1234, parsed.ProcessId);
        Assert.True(parsed.Verbose);
        Assert.Equal(3, parsed.Actions.Count);
        Assert.Equal("key", parsed.Actions[0].Kind);
        Assert.Equal(113, parsed.Actions[0].VirtualKey);
        Assert.Equal("click", parsed.Actions[1].Kind);
        Assert.Equal(0.5, parsed.Actions[1].X);
        Assert.Equal("right", parsed.Actions[1].Button);
        Assert.Equal("sleep", parsed.Actions[2].Kind);
        Assert.Equal(250, parsed.Actions[2].Ms);
    }

    [Fact]
    public void FromJson_RejectsEmptyAndGarbage()
    {
        Assert.Throws<ArgumentException>(() => SenderCodec.FromJson(""));
        Assert.Throws<ArgumentException>(() => SenderCodec.FromJson("   "));
        Assert.Throws<System.Text.Json.JsonException>(() => SenderCodec.FromJson("{oops"));
    }

    [Fact]
    public void ParseOutput_ReadsResultAndErrorLines()
    {
        SenderResult ok = SenderCodec.ParseOutput(0, new[]
        {
            "  [s] hwnd=0x1 setfg=0 gui=0x0 focus=0x0 pumpMs=9",
            "RESULT sent=2 failed=0",
        });

        Assert.True(ok.Ok);
        Assert.Equal(2, ok.Sent);
        Assert.Equal(0, ok.Failed);
        Assert.Null(ok.Error);
    }

    [Fact]
    public void ParseOutput_FailureWithoutResultLineStillFails()
    {
        SenderResult result = SenderCodec.ParseOutput(1, new[] { "ERROR PostMessage failed: KEYDOWN" });

        Assert.False(result.Ok);
        Assert.Equal(1, result.Failed);
        Assert.Contains("KEYDOWN", result.Error);
    }

    [Fact]
    public void ParseOutput_NonzeroExitWithoutLinesStillFails()
    {
        SenderResult result = SenderCodec.ParseOutput(2, Array.Empty<string>());

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseOutput_ExitZeroWithoutResultLineFails()
    {
        // Regression guard: a worker that prints help (or dies silent) must
        // never read as success — every Ok needs its RESULT line.
        SenderResult result = SenderCodec.ParseOutput(0, new[] { "some banner, no result" });

        Assert.False(result.Ok);
        Assert.Contains("RESULT", result.Error);
    }

    [Fact]
    public void HasKeys_DetectsKeyActionsCaseInsensitively()
    {
        Assert.True(new SenderWorkload { Actions = { SenderAction.Key(113) } }.HasKeys);
        Assert.True(new SenderWorkload
        {
            Actions = { SenderAction.Click(0.5, 0.5, "left"), new SenderAction { Kind = "KEY", VirtualKey = 114 } }
        }.HasKeys);
        Assert.False(new SenderWorkload
        {
            Actions = { SenderAction.Click(0.5, 0.5, "left"), SenderAction.Sleep(100) }
        }.HasKeys);
        Assert.False(new SenderWorkload().HasKeys);
    }

    [Fact]
    public void ParseOutput_IgnoresDiagnosticsAndToleratesOrder()
    {
        SenderResult result = SenderCodec.ParseOutput(0, new[]
        {
            "RESULT failed=1 sent=3 extra=ignored",
        });

        Assert.False(result.Ok);
        Assert.Equal(3, result.Sent);
        Assert.Equal(1, result.Failed);
    }
}
