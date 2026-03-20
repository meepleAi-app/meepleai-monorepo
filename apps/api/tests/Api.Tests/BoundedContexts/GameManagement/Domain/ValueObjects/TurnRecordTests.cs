using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class TurnRecordTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesSuccessfully()
    {
        var playerId = Guid.NewGuid();
        var start = DateTime.UtcNow;
        var record = new TurnRecord(1, playerId, start);

        record.TurnIndex.Should().Be(1);
        record.PlayerId.Should().Be(playerId);
        record.StartedAt.Should().Be(start);
        Assert.Null(record.EndedAt);
        Assert.Null(record.Duration);
        Assert.False(record.IsCompleted);
    }

    [Fact]
    public void Constructor_WithEndTime_CalculatesDuration()
    {
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(5);
        var record = new TurnRecord(1, Guid.NewGuid(), start, endedAt: end);

        record.EndedAt.Should().Be(end);
        record.Duration.Should().Be(TimeSpan.FromMinutes(5));
        Assert.True(record.IsCompleted);
    }

    [Fact]
    public void Constructor_WithPhaseInfo_StoresPhase()
    {
        var record = new TurnRecord(1, Guid.NewGuid(), DateTime.UtcNow, 0, "Draw Phase");
        record.PhaseIndex.Should().Be(0);
        record.PhaseName.Should().Be("Draw Phase");
    }

    [Fact]
    public void Constructor_NegativeTurnIndex_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new TurnRecord(-1, Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_EmptyPlayerId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new TurnRecord(1, Guid.Empty, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_EndBeforeStart_ThrowsValidationException()
    {
        var start = DateTime.UtcNow;
        Assert.Throws<ValidationException>(() =>
            new TurnRecord(1, Guid.NewGuid(), start, endedAt: start.AddMinutes(-1)));
    }

    [Fact]
    public void Constructor_PhaseNameTooLong_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new TurnRecord(1, Guid.NewGuid(), DateTime.UtcNow, 0, new string('x', 101)));
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var playerId = Guid.NewGuid();
        var start = DateTime.UtcNow;
        var r1 = new TurnRecord(1, playerId, start);
        var r2 = new TurnRecord(1, playerId, start);
        r2.Should().Be(r1);
    }

    [Fact]
    public void ToString_InProgress_ShowsInProgress()
    {
        var record = new TurnRecord(1, Guid.NewGuid(), DateTime.UtcNow);
        record.ToString().Should().Contain("In Progress");
    }

    [Fact]
    public void ToString_Completed_ShowsDuration()
    {
        var start = DateTime.UtcNow;
        var record = new TurnRecord(1, Guid.NewGuid(), start, endedAt: start.AddMinutes(3));
        record.ToString().Should().Contain("Duration");
    }
}
