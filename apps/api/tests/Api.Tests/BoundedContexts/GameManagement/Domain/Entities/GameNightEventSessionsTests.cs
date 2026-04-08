using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

public class GameNightEventSessionsTests
{
    private static GameNightEvent CreatePublishedEvent()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([Guid.NewGuid()]);
        return evt;
    }

    [Fact]
    public void AddSession_ToPublishedEvent_AddsToCollection()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        Assert.Single(evt.Sessions);
        Assert.Equal(1, evt.Sessions[0].PlayOrder);
    }

    [Fact]
    public void AddSession_AssignsIncrementalPlayOrder()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");
        Assert.Equal(2, evt.Sessions[1].PlayOrder);
    }

    [Fact]
    public void AddSession_BeyondFive_Throws()
    {
        var evt = CreatePublishedEvent();
        for (var i = 0; i < 5; i++)
            evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), $"Game{i}");
        Assert.Throws<InvalidOperationException>(() =>
            evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Game6"));
    }

    [Fact]
    public void AddSession_ToDraftEvent_Throws()
    {
        var evt = GameNightEvent.Create(Guid.NewGuid(), "Draft Night", DateTimeOffset.UtcNow.AddHours(1));
        Assert.Throws<InvalidOperationException>(() =>
            evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Catan"));
    }

    [Fact]
    public void StartCurrentSession_TransitionsFirstPendingToInProgress()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");
        evt.StartCurrentSession();
        Assert.Equal(GameNightSessionStatus.InProgress, evt.Sessions[0].Status);
        Assert.Equal(GameNightSessionStatus.Pending, evt.Sessions[1].Status);
    }

    [Fact]
    public void CompleteCurrentSession_WithWinner_MarksCompleted()
    {
        var winnerId = Guid.NewGuid();
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.CompleteCurrentSession(winnerId);
        Assert.Equal(GameNightSessionStatus.Completed, evt.Sessions[0].Status);
        Assert.Equal(winnerId, evt.Sessions[0].WinnerId);
    }

    [Fact]
    public void CurrentSession_ReturnsInProgressOrFirstPending()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        Assert.NotNull(evt.CurrentSession);
        Assert.Equal("Catan", evt.CurrentSession!.GameTitle);
    }

    [Fact]
    public void FinalizeNight_SetsCompleted_WhenAllSessionsDone()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.CompleteCurrentSession(Guid.NewGuid());
        evt.FinalizeNight();
        Assert.Equal(GameNightStatus.Completed, evt.Status);
    }

    [Fact]
    public void FinalizeNight_WithInProgressSession_Throws()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        Assert.Throws<InvalidOperationException>(() => evt.FinalizeNight());
    }

    [Fact]
    public void RestoreSessions_ReconstitutesFromPersistence()
    {
        var evt = CreatePublishedEvent();
        var session = GameNightSession.Reconstitute(
            Guid.NewGuid(), evt.Id, Guid.NewGuid(), Guid.NewGuid(),
            "Catan", 1, GameNightSessionStatus.Completed,
            Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);
        evt.RestoreSessions([session]);
        Assert.Single(evt.Sessions);
        Assert.Equal(GameNightSessionStatus.Completed, evt.Sessions[0].Status);
    }
}
