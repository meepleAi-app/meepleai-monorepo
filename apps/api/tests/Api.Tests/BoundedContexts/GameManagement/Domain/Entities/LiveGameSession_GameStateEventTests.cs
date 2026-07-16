using System.Text.Json;

using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Tests.Constants;

using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>#3025 L1: UpdateGameState raises LiveSessionGameStateEvent carrying the raw JSON.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSession_GameStateEventTests
{
    private readonly FakeTimeProvider _timeProvider =
        new(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    private LiveGameSession CreateInProgressSession(Guid? creator = null)
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            creator ?? Guid.NewGuid(),
            "Mage Knight",
            _timeProvider);
        session.AddPlayer(null, "Alice", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        return session;
    }

    [Fact]
    public void UpdateGameState_RaisesLiveSessionGameStateEvent_WithSessionIdAndRawState()
    {
        var session = CreateInProgressSession();
        var state = JsonDocument.Parse("""{"board":"opaque"}"""); // ownership transferred to the aggregate

        session.UpdateGameState(state);

        var evt = Assert.Single(session.DomainEvents, e => e is LiveSessionGameStateEvent);
        var gs = Assert.IsType<LiveSessionGameStateEvent>(evt);
        Assert.Equal(session.Id, gs.SessionId);
        Assert.Contains("\"board\"", gs.RawStateJson);
    }
}
