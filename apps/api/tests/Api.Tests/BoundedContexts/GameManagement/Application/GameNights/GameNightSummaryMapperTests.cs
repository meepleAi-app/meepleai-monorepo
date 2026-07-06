using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNights;

/// <summary>
/// Unit tests for <see cref="GameNightSummaryMapper"/> — the MVP/KPI/recap aggregation (#2702).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Feature", "GameNightSummary")]
public class GameNightSummaryMapperTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 21, 0, 0, TimeSpan.Zero);

    private static GameNightSession CompletedSession(
        Guid eventId, int order, Guid gameId, string title, Guid? winnerId, int durationMinutes)
    {
        return GameNightSession.Reconstitute(
            id: Guid.NewGuid(),
            gameNightEventId: eventId,
            sessionId: Guid.NewGuid(),
            gameId: gameId,
            gameTitle: title,
            playOrder: order,
            status: GameNightSessionStatus.Completed,
            winnerId: winnerId,
            startedAt: Start,
            completedAt: Start.AddMinutes(durationMinutes));
    }

    [Fact]
    public void Map_ComputesMvpKpisAndRecap()
    {
        var evt = GameNightEvent.Create(Guid.NewGuid(), "Serata", Start.AddDays(-1));
        var davide = Guid.NewGuid();
        var marco = Guid.NewGuid();
        evt.RestoreSessions(new[]
        {
            CompletedSession(evt.Id, 1, Guid.NewGuid(), "Brass", davide, 120),
            CompletedSession(evt.Id, 2, Guid.NewGuid(), "Spirit", davide, 60),
            CompletedSession(evt.Id, 3, Guid.NewGuid(), "Azul", marco, 30),
        });
        var names = new Dictionary<Guid, string> { [davide] = "Davide", [marco] = "Marco" };
        string? Resolver(GameNightSession s) => s.WinnerId is { } w ? names.GetValueOrDefault(w) : null;

        var dto = GameNightSummaryMapper.Map(evt, Resolver, isViewerOrganizer: true);

        dto.Mvp.Should().NotBeNull();
        dto.Mvp!.PlayerName.Should().Be("Davide");
        dto.Mvp.Wins.Should().Be(2);
        dto.Kpis.TotalGames.Should().Be(3);
        dto.Kpis.CompletedGames.Should().Be(3);
        dto.Kpis.WinnersCount.Should().Be(3);
        dto.Kpis.TotalDurationMinutes.Should().Be(210);
        dto.Games.Should().HaveCount(3);
        dto.Games[0].GameTitle.Should().Be("Brass");
        dto.Games[0].WinnerName.Should().Be("Davide");
        dto.Games[0].DurationMinutes.Should().Be(120);
    }

    [Fact]
    public void Map_NoWinners_YieldsNullMvpAndZeroWinnersCount()
    {
        var evt = GameNightEvent.Create(Guid.NewGuid(), "Serata", Start.AddDays(-1));
        evt.RestoreSessions(new[]
        {
            CompletedSession(evt.Id, 1, Guid.NewGuid(), "Brass", winnerId: null, 90),
        });

        var dto = GameNightSummaryMapper.Map(evt, _ => null, isViewerOrganizer: true);

        dto.Mvp.Should().BeNull();
        dto.Kpis.WinnersCount.Should().Be(0);
        dto.Kpis.TotalDurationMinutes.Should().Be(90);
    }

    [Fact]
    public void Map_NonOrganizer_OmitsShareTokenButKeepsSharedFlag()
    {
        var evt = GameNightEvent.Create(Guid.NewGuid(), "Serata", Start.AddDays(-1));
        evt.Publish(new List<Guid>());
        evt.Complete();
        evt.GenerateShareToken();

        var organizerView = GameNightSummaryMapper.Map(evt, _ => null, isViewerOrganizer: true);
        var guestView = GameNightSummaryMapper.Map(evt, _ => null, isViewerOrganizer: false);

        organizerView.ShareToken.Should().NotBeNull();
        guestView.ShareToken.Should().BeNull();
        guestView.IsShared.Should().BeTrue();
    }
}
