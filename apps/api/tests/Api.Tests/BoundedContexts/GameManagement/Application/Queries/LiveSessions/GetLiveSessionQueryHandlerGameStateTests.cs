using System.Text.Json;

using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;

using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>#3025 L1: LiveSessionDto exposes the opaque GameState.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GetLiveSessionQueryHandlerGameStateTests
{
    private readonly FakeTimeProvider _tp = new(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    private LiveGameSession CreateInProgressSession()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mage Knight", _tp);
        session.AddPlayer(null, "Alice", PlayerColor.Red, _tp);
        session.Start(_tp);
        return session;
    }

    [Fact]
    public void MapToDto_ExposesGameState_WhenSet()
    {
        var session = CreateInProgressSession();
        session.UpdateGameState(JsonDocument.Parse("""{"board":7}"""));

        var dto = GetLiveSessionQueryHandler.MapToDto(session);

        Assert.NotNull(dto.GameState);
        Assert.Equal(7, dto.GameState!.Value.GetProperty("board").GetInt32());
    }

    [Fact]
    public void MapToDto_NullGameState_WhenUnset()
    {
        var session = CreateInProgressSession();

        var dto = GetLiveSessionQueryHandler.MapToDto(session);

        Assert.Null(dto.GameState);
    }
}
