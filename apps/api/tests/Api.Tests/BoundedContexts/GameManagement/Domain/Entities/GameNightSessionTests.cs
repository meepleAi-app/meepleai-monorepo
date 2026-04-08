using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

public class GameNightSessionTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        var gameNightId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var gns = GameNightSession.Create(gameNightId, sessionId, gameId, "Catan", 1);

        Assert.Equal(gameNightId, gns.GameNightEventId);
        Assert.Equal(sessionId, gns.SessionId);
        Assert.Equal(gameId, gns.GameId);
        Assert.Equal("Catan", gns.GameTitle);
        Assert.Equal(1, gns.PlayOrder);
        Assert.Equal(GameNightSessionStatus.Pending, gns.Status);
        Assert.Null(gns.WinnerId);
        Assert.Null(gns.StartedAt);
        Assert.Null(gns.CompletedAt);
    }

    [Fact]
    public void Create_WithEmptyGameNightId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameNightSession.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Catan", 1));
    }

    [Fact]
    public void Create_WithEmptyGameTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", 1));
    }

    [Fact]
    public void Create_WithZeroPlayOrder_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 0));
    }

    [Fact]
    public void Reconstitute_RestoresAllProperties()
    {
        var id = Guid.NewGuid();
        var gameNightId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var completedAt = DateTimeOffset.UtcNow;

        var gns = GameNightSession.Reconstitute(
            id, gameNightId, sessionId, gameId, "Catan", 1,
            GameNightSessionStatus.Completed, winnerId, startedAt, completedAt);

        Assert.Equal(id, gns.Id);
        Assert.Equal(GameNightSessionStatus.Completed, gns.Status);
        Assert.Equal(winnerId, gns.WinnerId);
        Assert.Equal(startedAt, gns.StartedAt);
        Assert.Equal(completedAt, gns.CompletedAt);
    }

    [Fact]
    public void Start_FromPending_TransitionsToInProgress()
    {
        var gns = GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 1);
        gns.Start();
        Assert.Equal(GameNightSessionStatus.InProgress, gns.Status);
        Assert.NotNull(gns.StartedAt);
    }

    [Fact]
    public void Start_FromNonPending_Throws()
    {
        var gns = GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 1);
        gns.Start();
        Assert.Throws<InvalidOperationException>(() => gns.Start());
    }

    [Fact]
    public void Complete_WithWinner_SetsWinnerAndTimestamp()
    {
        var winnerId = Guid.NewGuid();
        var gns = GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 1);
        gns.Start();
        gns.Complete(winnerId);
        Assert.Equal(GameNightSessionStatus.Completed, gns.Status);
        Assert.Equal(winnerId, gns.WinnerId);
        Assert.NotNull(gns.CompletedAt);
    }

    [Fact]
    public void Complete_WithoutStart_Throws()
    {
        var gns = GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 1);
        Assert.Throws<InvalidOperationException>(() => gns.Complete(null));
    }

    [Fact]
    public void Skip_FromPending_TransitionsToSkipped()
    {
        var gns = GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 1);
        gns.Skip();
        Assert.Equal(GameNightSessionStatus.Skipped, gns.Status);
    }

    [Fact]
    public void Skip_FromNonPending_Throws()
    {
        var gns = GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 1);
        gns.Start();
        Assert.Throws<InvalidOperationException>(() => gns.Skip());
    }
}
