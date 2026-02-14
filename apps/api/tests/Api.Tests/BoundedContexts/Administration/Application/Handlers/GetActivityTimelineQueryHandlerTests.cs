using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

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
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Activities.Count);

        // Verify game_added mapping
        var gameActivity = result.Activities[0];
        Assert.Equal("game_added", gameActivity.Type);
        Assert.NotNull(gameActivity.GameName);
        Assert.Equal("Catan", gameActivity.GameName);
        Assert.NotNull(gameActivity.CoverUrl);
        Assert.Null(gameActivity.SessionId);
        Assert.Null(gameActivity.ChatId);

        // Verify session_completed mapping
        var sessionActivity = result.Activities[1];
        Assert.Equal("session_completed", sessionActivity.Type);
        Assert.NotNull(sessionActivity.SessionId);
        Assert.Equal(90, sessionActivity.Duration);
        Assert.Null(sessionActivity.GameId);

        // Verify chat_saved mapping
        var chatActivity = result.Activities[2];
        Assert.Equal("chat_saved", chatActivity.Type);
        Assert.NotNull(chatActivity.ChatId);
        Assert.Equal("Rules clarification", chatActivity.Topic);
        Assert.Null(chatActivity.GameId);
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
        Assert.NotNull(result);
        Assert.Empty(result.Activities);
        Assert.Equal(0, result.TotalCount);
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
        Assert.Single(result.Activities);
        var dto = result.Activities[0];
        Assert.Equal("wishlist_added", dto.Type);
        Assert.NotNull(dto.GameId);
        Assert.Equal("Terraforming Mars", dto.GameName);
        Assert.Null(dto.SessionId);
        Assert.Null(dto.ChatId);
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
        Assert.Equal(2, result.Activities.Count);
        Assert.Equal(10, result.TotalCount);
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
        Assert.Equal(3, result.Page); // skip=20, take=10 → page 3
        Assert.Equal(10, result.PageSize);
        Assert.True(result.HasMore); // 20+10=30 < 50
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
        Assert.False(result.HasMore); // 8+5=13 >= 10
    }
}
