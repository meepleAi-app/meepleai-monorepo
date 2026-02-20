using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

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

        Assert.Equal(1, record.TurnIndex);
        Assert.Equal(playerId, record.PlayerId);
        Assert.Equal(start, record.StartedAt);
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

        Assert.Equal(end, record.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), record.Duration);
        Assert.True(record.IsCompleted);
    }

    [Fact]
    public void Constructor_WithPhaseInfo_StoresPhase()
    {
        var record = new TurnRecord(1, Guid.NewGuid(), DateTime.UtcNow, 0, "Draw Phase");
        Assert.Equal(0, record.PhaseIndex);
        Assert.Equal("Draw Phase", record.PhaseName);
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
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void ToString_InProgress_ShowsInProgress()
    {
        var record = new TurnRecord(1, Guid.NewGuid(), DateTime.UtcNow);
        Assert.Contains("In Progress", record.ToString());
    }

    [Fact]
    public void ToString_Completed_ShowsDuration()
    {
        var start = DateTime.UtcNow;
        var record = new TurnRecord(1, Guid.NewGuid(), start, endedAt: start.AddMinutes(3));
        Assert.Contains("Duration", record.ToString());
    }
}
