using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class TimerToolConfigTests
{
    [Fact]
    public void Constructor_WithDefaults_CreatesCountdownTimer()
    {
        var config = new TimerToolConfig("Round Timer", 60);

        config.Name.Should().Be("Round Timer");
        config.DurationSeconds.Should().Be(60);
        config.TimerType.Should().Be(TimerType.CountDown);
        config.AutoStart.Should().BeFalse();
        config.Color.Should().BeNull();
        config.IsPerPlayer.Should().BeFalse();
        config.WarningThresholdSeconds.Should().BeNull();
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

        config.Name.Should().Be("Chess Clock");
        config.DurationSeconds.Should().Be(300);
        config.TimerType.Should().Be(TimerType.Chess);
        config.AutoStart.Should().BeTrue();
        config.Color.Should().Be("#FF0000");
        config.IsPerPlayer.Should().BeTrue();
        config.WarningThresholdSeconds.Should().Be(30);
    }

    [Fact]
    public void Constructor_CountUpTimer_Succeeds()
    {
        var config = new TimerToolConfig("Stopwatch", 3600, TimerType.CountUp);
        config.TimerType.Should().Be(TimerType.CountUp);
    }

    [Fact]
    public void Constructor_ChessTimerNotPerPlayer_ThrowsArgumentException()
    {
        var act = () =>
            new TimerToolConfig("Chess", 300, TimerType.Chess, isPerPlayer: false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ChessTimerPerPlayer_Succeeds()
    {
        var config = new TimerToolConfig("Chess", 300, TimerType.Chess, isPerPlayer: true);
        config.TimerType.Should().Be(TimerType.Chess);
        config.IsPerPlayer.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        ((Action)(() => new TimerToolConfig("", 60))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithDurationBelowMin_ThrowsArgumentException()
    {
        ((Action)(() => new TimerToolConfig("Timer", 0))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithDurationAboveMax_ThrowsArgumentException()
    {
        ((Action)(() => new TimerToolConfig("Timer", 86401))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWarningThresholdZero_ThrowsArgumentException()
    {
        var act2 = () =>
            new TimerToolConfig("Timer", 60, warningThresholdSeconds: 0);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWarningThresholdEqualToDuration_ThrowsArgumentException()
    {
        var act3 = () =>
            new TimerToolConfig("Timer", 60, warningThresholdSeconds: 60);
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWarningThresholdAboveDuration_ThrowsArgumentException()
    {
        var act4 = () =>
            new TimerToolConfig("Timer", 60, warningThresholdSeconds: 90);
        act4.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidWarningThreshold_Succeeds()
    {
        var config = new TimerToolConfig("Timer", 60, warningThresholdSeconds: 10);
        config.WarningThresholdSeconds.Should().Be(10);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var config = new TimerToolConfig("  Round Timer  ", 60);
        config.Name.Should().Be("Round Timer");
    }

    [Fact]
    public void Constructor_PerPlayerCountdownTimer_Succeeds()
    {
        var config = new TimerToolConfig("Turn Timer", 120, TimerType.CountDown, isPerPlayer: true);
        config.IsPerPlayer.Should().BeTrue();
        config.TimerType.Should().Be(TimerType.CountDown);
    }
}
