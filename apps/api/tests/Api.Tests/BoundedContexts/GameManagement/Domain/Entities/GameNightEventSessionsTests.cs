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

    // SI-4 (#2635): resuming a night for a 2nd+ sitting. After the first game starts, the night is
    // InProgress (#15). Adding a session for the next game must be allowed — this is the WS1 latent
    // multi-game bug the resume slice fixes (AddSession was Published-only). The max-1-live guard (#10)
    // lives in EnsureCanStartSession/StartCurrentSession, so relaxing AddSession creates no 2nd live.
    [Fact]
    public void AddSession_ToInProgressEvent_Succeeds()
    {
        var evt = CreatePublishedEvent();
        var firstSessionId = Guid.NewGuid();
        evt.AddSession(firstSessionId, evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.HandleFirstSessionStarted(firstSessionId); // Published → InProgress
        evt.CompleteCurrentSession(winnerId: null);     // first game done → no live session
        Assert.Equal(GameNightStatus.InProgress, evt.Status);

        var session = evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");

        Assert.Equal(2, evt.Sessions.Count);
        Assert.Equal(2, session.PlayOrder);
        Assert.Equal(GameNightStatus.InProgress, evt.Status);
    }

    // SI-4: resume-from-Completed is OUT — a finalized night is terminal. Reject at the command
    // boundary with guidance to start a new session, never resurrect a terminal state (spec-panel).
    [Fact]
    public void AddSession_ToCompletedEvent_ThrowsWithResumeGuidance()
    {
        var evt = CreatePublishedEvent();
        var firstSessionId = Guid.NewGuid();
        evt.AddSession(firstSessionId, evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.HandleFirstSessionStarted(firstSessionId); // Published → InProgress
        evt.CompleteCurrentSession(winnerId: null);
        evt.CompleteAdHoc();                            // InProgress → Completed (production door)
        Assert.Equal(GameNightStatus.Completed, evt.Status);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit"));
        Assert.Contains("finalized", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("new session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddSession_ToCancelledEvent_Throws()
    {
        var evt = CreatePublishedEvent();
        evt.Cancel(); // Published → Cancelled
        Assert.Throws<InvalidOperationException>(() =>
            evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan"));
    }

    // SI-4 AC3: after resuming for a 2nd sitting, the InProgress night remains finalizable — the
    // relaxed AddSession leaves no orphaned live child that would block completion (CompleteAdHoc).
    [Fact]
    public void CompleteAdHoc_AfterSecondSitting_FinalizesInProgressNight()
    {
        var evt = CreatePublishedEvent();
        var firstSessionId = Guid.NewGuid();
        evt.AddSession(firstSessionId, evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.HandleFirstSessionStarted(firstSessionId); // Published → InProgress
        evt.CompleteCurrentSession(winnerId: null);

        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit"); // 2nd sitting (relaxed path)
        evt.StartCurrentSession();
        evt.CompleteCurrentSession(winnerId: null);              // no live session remains

        evt.CompleteAdHoc();                                     // InProgress → Completed

        Assert.Equal(GameNightStatus.Completed, evt.Status);
        Assert.Equal(2, evt.Sessions.Count);
        Assert.All(evt.Sessions, s => Assert.Equal(GameNightSessionStatus.Completed, s.Status));
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
