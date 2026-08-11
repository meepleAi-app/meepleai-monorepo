using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNights;

/// <summary>
/// Tests for <see cref="GameNightMapperHelper"/> — specifically the #3084
/// SessionCount field that drives the dashboard "Recenti" card ("N partite").
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameNightMapperHelperTests
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
    public void MapToDto_SetsSessionCount_FromAggregateSessions()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");

        var dto = GameNightMapperHelper.MapToDto(evt, "Organizer");

        dto.SessionCount.Should().Be(2);
    }

    [Fact]
    public void MapToDto_SessionCountIsZero_WhenNoSessions()
    {
        var evt = CreatePublishedEvent();

        var dto = GameNightMapperHelper.MapToDto(evt, "Organizer");

        dto.SessionCount.Should().Be(0);
    }
}
