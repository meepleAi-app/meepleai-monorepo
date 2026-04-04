using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Unit tests for GetActivityTimelineQueryHandler (Issue #3973, #3923).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetActivityTimelineQueryHandlerTests
{
    private readonly Mock<IActivityTimelineService> _mockTimelineService;
    private readonly GetActivityTimelineQueryHandler _handler;

    public GetActivityTimelineQueryHandlerTests()
    {
        _mockTimelineService = new Mock<IActivityTimelineService>();
        _handler = new GetActivityTimelineQueryHandler(_mockTimelineService.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnTimelineResponse_WithCorrectDtoMapping()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId);
        var events = new List<ActivityEvent>
        {
            new GameAddedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddHours(-1),
                Type = "game_added",
                GameId = Guid.NewGuid(),
                GameName = "Catan",
                CoverUrl = "https://example.com/catan.jpg"
            },
            new SessionCompletedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddHours(-2),
                Type = "session_completed",
                SessionId = Guid.NewGuid(),
                GameName = "Wingspan",
                Duration = 90
            },
            new ChatSavedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddHours(-3),
                Type = "chat_saved",
                ChatId = Guid.NewGuid(),
                Topic = "Rules clarification"
            }
        };

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, null, null, 0, 20, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)events, 3));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(3);
        result.Activities.Count.Should().Be(3);

        // Verify game_added mapping
        var gameActivity = result.Activities[0];
        gameActivity.Type.Should().Be("game_added");
        gameActivity.GameName.Should().NotBeNull();
        gameActivity.GameName.Should().Be("Catan");
        gameActivity.CoverUrl.Should().NotBeNull();
        gameActivity.SessionId.Should().BeNull();
        gameActivity.ChatId.Should().BeNull();

        // Verify session_completed mapping
        var sessionActivity = result.Activities[1];
        sessionActivity.Type.Should().Be("session_completed");
        sessionActivity.SessionId.Should().NotBeNull();
        sessionActivity.Duration.Should().Be(90);
        sessionActivity.GameId.Should().BeNull();

        // Verify chat_saved mapping
        var chatActivity = result.Activities[2];
        chatActivity.Type.Should().Be("chat_saved");
        chatActivity.ChatId.Should().NotBeNull();
        chatActivity.Topic.Should().Be("Rules clarification");
        chatActivity.GameId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyResponse_WhenNoEvents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId);

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, null, null, 0, 20, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)new List<ActivityEvent>(), 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Activities.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldPassFilterParametersToService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var types = new[] { "game_added", "session_completed" };
        var query = new GetActivityTimelineQuery(
            userId,
            Types: types,
            SearchTerm: "wingspan",
            Skip: 10,
            Take: 5,
            Order: SortDirection.Ascending);

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, types, "wingspan", null, null, 10, 5, SortDirection.Ascending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)new List<ActivityEvent>(), 0));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockTimelineService.Verify(
            s => s.GetFilteredActivitiesAsync(
                userId, types, "wingspan", null, null, 10, 5, SortDirection.Ascending,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapWishlistAddedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId);
        var events = new List<ActivityEvent>
        {
            new WishlistAddedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Type = "wishlist_added",
                GameId = Guid.NewGuid(),
                GameName = "Terraforming Mars"
            }
        };

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, null, null, 0, 20, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)events, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Activities.Should().ContainSingle();
        var dto = result.Activities[0];
        dto.Type.Should().Be("wishlist_added");
        dto.GameId.Should().NotBeNull();
        dto.GameName.Should().Be("Terraforming Mars");
        dto.SessionId.Should().BeNull();
        dto.ChatId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnTotalCount_SeparateFromPagedResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId, Skip: 0, Take: 2);
        var events = new List<ActivityEvent>
        {
            new GameAddedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Type = "game_added",
                GameId = Guid.NewGuid(),
                GameName = "Catan"
            },
            new GameAddedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddHours(-1),
                Type = "game_added",
                GameId = Guid.NewGuid(),
                GameName = "Wingspan"
            }
        };

        // Total count is 10 but only 2 returned (paged)
        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, null, null, 0, 2, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)events, 10));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Activities.Count.Should().Be(2);
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldCallServiceExactlyOnce()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId);

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string[]?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SortDirection>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)new List<ActivityEvent>(), 0));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockTimelineService.Verify(
            s => s.GetFilteredActivitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string[]?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SortDirection>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPassDateRangeToService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dateFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dateTo = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var query = new GetActivityTimelineQuery(userId, DateFrom: dateFrom, DateTo: dateTo);

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, dateFrom, dateTo, 0, 20, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)new List<ActivityEvent>(), 0));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockTimelineService.Verify(
            s => s.GetFilteredActivitiesAsync(
                userId, null, null, dateFrom, dateTo, 0, 20, SortDirection.Descending,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCalculatePageAndHasMore()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId, Skip: 20, Take: 10);
        var events = new List<ActivityEvent>
        {
            new GameAddedEvent
            {
                Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow,
                Type = "game_added", GameId = Guid.NewGuid(), GameName = "Catan"
            }
        };

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, null, null, 20, 10, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)events, 50));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Page.Should().Be(3); // skip=20, take=10 → page 3
        result.PageSize.Should().Be(10);
        result.HasMore.Should().BeTrue(); // 20+10=30 < 50
    }

    [Fact]
    public async Task Handle_ShouldSetHasMoreFalse_WhenOnLastPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetActivityTimelineQuery(userId, Skip: 8, Take: 5);

        _mockTimelineService
            .Setup(s => s.GetFilteredActivitiesAsync(
                userId, null, null, null, null, 8, 5, SortDirection.Descending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ActivityEvent>)new List<ActivityEvent>(), 10));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.HasMore.Should().BeFalse(); // 8+5=13 >= 10
    }
}
