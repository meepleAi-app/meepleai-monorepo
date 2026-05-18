using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="GetUserGamebooksQueryHandler"/> (Issue #1288).
///
/// Verifies the handler composes <see cref="IUserGamebookViewRepository"/>
/// correctly and maps to <see cref="GamebookCardDataDto"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetUserGamebooksQueryHandlerTests
{
    private readonly Mock<IUserGamebookViewRepository> _viewRepoMock = new(MockBehavior.Strict);

    private GetUserGamebooksQueryHandler CreateHandler() =>
        new(_viewRepoMock.Object);

    [Fact]
    public async Task Handle_WithNoEntries_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGamebookViewItem>());

        // Act
        var result = await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithSharedGameEntry_ReturnsGamebookCard()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var libraryEntryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var lastActivity = DateTime.UtcNow.AddDays(-1);

        var entry = new UserGamebookViewItem(
            LibraryEntryId: libraryEntryId,
            GameId: gameId,
            Title: "Nanolith",
            Year: 2024,
            Cover: "https://cdn.example.com/nanolith.jpg",
            HasActiveCampaign: true,
            HasPrivatePdf: false,
            LastActivityAt: lastActivity);

        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });

        // Act
        var result = await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var card = result[0];
        card.Id.Should().Be(libraryEntryId);
        card.GameId.Should().Be(gameId);
        card.Title.Should().Be("Nanolith");
        card.Year.Should().Be(2024);
        card.Cover.Should().Be("https://cdn.example.com/nanolith.jpg");
        card.Status.Should().Be("ready");
    }

    [Fact]
    public async Task Handle_WithMultipleEntries_OrdersByLastActivityDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var older = new UserGamebookViewItem(
            LibraryEntryId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Older Campaign",
            Year: 2020,
            Cover: null,
            HasActiveCampaign: true,
            HasPrivatePdf: false,
            LastActivityAt: now.AddDays(-5));

        var newer = new UserGamebookViewItem(
            LibraryEntryId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Recent Campaign",
            Year: 2024,
            Cover: null,
            HasActiveCampaign: true,
            HasPrivatePdf: false,
            LastActivityAt: now);

        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { older, newer });

        // Act
        var result = await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Recent Campaign");
        result[1].Title.Should().Be("Older Campaign");
    }

    [Fact]
    public async Task Handle_WithPrivatePdfEntry_ReturnsGamebookCard()
    {
        // Arrange — entry has private PDF but no active campaign (early-stage workflow)
        var userId = Guid.NewGuid();
        var entry = new UserGamebookViewItem(
            LibraryEntryId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Private Manual",
            Year: null,
            Cover: null,
            HasActiveCampaign: false,
            HasPrivatePdf: true,
            LastActivityAt: DateTime.UtcNow);

        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });

        // Act
        var result = await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Private Manual");
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = () => CreateHandler().Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
