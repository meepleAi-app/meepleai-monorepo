using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class TimerToolConfigTests
{
    [Fact]
    public void Constructor_WithDefaults_CreatesCountdownTimer()
    {
        var config = new TimerToolConfig("Round Timer", 60);

        Assert.Equal("Round Timer", config.Name);
        Assert.Equal(60, config.DurationSeconds);
        Assert.Equal(TimerType.CountDown, config.TimerType);
        Assert.False(config.AutoStart);
        Assert.Null(config.Color);
        Assert.False(config.IsPerPlayer);
        Assert.Null(config.WarningThresholdSeconds);
    }

    [Fact]
    public void Constructor_WithAllParams_CreatesFullConfig()
    {
        var config = new TimerToolConfig(
            name: "Chess Clock",
            durationSeconds: 300,
            timerType: TimerType.Chess,
            autoStart: true,
            color: "#FF0000",
            isPerPlayer: true,
            warningThresholdSeconds: 30);

        Assert.Equal("Chess Clock", config.Name);
        Assert.Equal(300, config.DurationSeconds);
        Assert.Equal(TimerType.Chess, config.TimerType);
        Assert.True(config.AutoStart);
        Assert.Equal("#FF0000", config.Color);
        Assert.True(config.IsPerPlayer);
        Assert.Equal(30, config.WarningThresholdSeconds);
    }

    [Fact]
    public void Constructor_CountUpTimer_Succeeds()
    {
        var config = new TimerToolConfig("Stopwatch", 3600, TimerType.CountUp);
        Assert.Equal(TimerType.CountUp, config.TimerType);
    }

    [Fact]
    public void Constructor_ChessTimerNotPerPlayer_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TimerToolConfig("Chess", 300, TimerType.Chess, isPerPlayer: false));
    }

    [Fact]
    public void Constructor_ChessTimerPerPlayer_Succeeds()
    {
        var config = new TimerToolConfig("Chess", 300, TimerType.Chess, isPerPlayer: true);
        Assert.Equal(TimerType.Chess, config.TimerType);
        Assert.True(config.IsPerPlayer);
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TimerToolConfig("", 60));
    }

    [Fact]
    public void Constructor_WithDurationBelowMin_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TimerToolConfig("Timer", 0));
    }

    [Fact]
    public void Constructor_WithDurationAboveMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TimerToolConfig("Timer", 86401));
    }

    [Fact]
    public void Constructor_WithWarningThresholdZero_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TimerToolConfig("Timer", 60, warningThresholdSeconds: 0));
    }

    [Fact]
    public void Constructor_WithWarningThresholdEqualToDuration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TimerToolConfig("Timer", 60, warningThresholdSeconds: 60));
    }

    [Fact]
    public void Constructor_WithWarningThresholdAboveDuration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TimerToolConfig("Timer", 60, warningThresholdSeconds: 90));
    }

    [Fact]
    public void Constructor_WithValidWarningThreshold_Succeeds()
    {
        var config = new TimerToolConfig("Timer", 60, warningThresholdSeconds: 10);
        Assert.Equal(10, config.WarningThresholdSeconds);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var config = new TimerToolConfig("  Round Timer  ", 60);
        Assert.Equal("Round Timer", config.Name);
    }

    [Fact]
    public void Constructor_PerPlayerCountdownTimer_Succeeds()
    {
        var config = new TimerToolConfig("Turn Timer", 120, TimerType.CountDown, isPerPlayer: true);
        Assert.True(config.IsPerPlayer);
        Assert.Equal(TimerType.CountDown, config.TimerType);
    }
}
