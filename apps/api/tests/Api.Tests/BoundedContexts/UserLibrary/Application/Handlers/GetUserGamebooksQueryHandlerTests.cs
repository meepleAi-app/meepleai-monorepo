using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="GetUserGamebooksQueryHandler"/> (Issue #1288 + #1292).
///
/// Verifies:
///   - composition of <see cref="IUserGamebookViewRepository"/> + mapping to
///     <see cref="GamebookCardDataDto"/> (issue #1288)
///   - cache key + tag contract through <see cref="IHybridCacheService"/>
///     (issue #1292 AC-6.1/AC-6.2)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetUserGamebooksQueryHandlerTests
{
    private readonly Mock<IUserGamebookViewRepository> _viewRepoMock = new(MockBehavior.Strict);
    private readonly Mock<IUserLibraryRepository> _userLibraryRepoMock = new(MockBehavior.Loose);
    private readonly Mock<IHybridCacheService> _cacheMock = new(MockBehavior.Strict);

    public GetUserGamebooksQueryHandlerTests()
    {
        // Default cache behaviour: passthrough to factory (simulates cache miss + populate).
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<UserGamebookViewItem>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<UserGamebookViewItem>>> factory, string[]? _, TimeSpan? _, CancellationToken ct) => factory(ct));
    }

    private GetUserGamebooksQueryHandler CreateHandler() =>
        new(_viewRepoMock.Object, _userLibraryRepoMock.Object, _cacheMock.Object);

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
            ChunkCount: 8842,
            SessionsCount: 2,
            ReadyPdfCount: 120,
            IndexingPdfCount: 3,
            FailedPdfCount: 33,
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
        card.Chunks.Should().Be(8842);
        card.SessionsCount.Should().Be(2);
        card.Pages.Should().Be(120);
        card.TotalPages.Should().Be(156);
    }

    [Fact]
    public async Task Handle_WithIndexingPdfsOnly_ReturnsIndexingStatus()
    {
        // Arrange — no Ready PDFs yet, some are still processing
        var userId = Guid.NewGuid();
        var entry = new UserGamebookViewItem(
            LibraryEntryId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Just Uploaded",
            Year: 2024,
            Cover: null,
            HasActiveCampaign: true,
            HasPrivatePdf: false,
            ChunkCount: 0,
            SessionsCount: 0,
            ReadyPdfCount: 0,
            IndexingPdfCount: 2,
            FailedPdfCount: 0,
            LastActivityAt: DateTime.UtcNow);

        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });

        // Act
        var result = await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        result[0].Status.Should().Be("indexing");
    }

    [Fact]
    public async Task Handle_WithFailedPdfsOnly_ReturnsErrorStatus()
    {
        // Arrange — all PDFs failed processing
        var userId = Guid.NewGuid();
        var entry = new UserGamebookViewItem(
            LibraryEntryId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Broken",
            Year: 2024,
            Cover: null,
            HasActiveCampaign: false,
            HasPrivatePdf: true,
            ChunkCount: 0,
            SessionsCount: 0,
            ReadyPdfCount: 0,
            IndexingPdfCount: 0,
            FailedPdfCount: 1,
            LastActivityAt: DateTime.UtcNow);

        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });

        // Act
        var result = await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        result[0].Status.Should().Be("error");
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
            ChunkCount: 100,
            SessionsCount: 1,
            ReadyPdfCount: 1,
            IndexingPdfCount: 0,
            FailedPdfCount: 0,
            LastActivityAt: now.AddDays(-5));

        var newer = new UserGamebookViewItem(
            LibraryEntryId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Recent Campaign",
            Year: 2024,
            Cover: null,
            HasActiveCampaign: true,
            HasPrivatePdf: false,
            ChunkCount: 500,
            SessionsCount: 3,
            ReadyPdfCount: 5,
            IndexingPdfCount: 0,
            FailedPdfCount: 0,
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
            ChunkCount: 0,
            SessionsCount: 0,
            ReadyPdfCount: 1,
            IndexingPdfCount: 0,
            FailedPdfCount: 0,
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

    // Issue #1292 — Cache contract verification.

    [Fact]
    public async Task Handle_UsesCanonicalCacheKeyAndTag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedKey = $"userlibrary:gamebooks:view:{userId}";
        var expectedTag = $"user:{userId}";

        _viewRepoMock
            .Setup(r => r.GetGamebookEntriesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGamebookViewItem>());
        _userLibraryRepoMock
            .Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await CreateHandler().Handle(new GetUserGamebooksQuery(userId), CancellationToken.None);

        // Assert
        _cacheMock.Verify(c => c.GetOrCreateAsync(
            expectedKey,
            It.IsAny<Func<CancellationToken, Task<List<UserGamebookViewItem>>>>(),
            It.Is<string[]?>(tags => tags != null && tags.Length == 1 && tags[0] == expectedTag),
            It.Is<TimeSpan?>(t => t.HasValue && t.Value == TimeSpan.FromMinutes(5)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildCacheKey_FollowsExpectedPattern()
    {
        // Arrange / Act
        var userId = Guid.Parse("c8ff6a6b-c764-4f63-9ea8-f3c0705225c1");
        var key = GetUserGamebooksQueryHandler.BuildCacheKey(userId);
        var tag = GetUserGamebooksQueryHandler.BuildUserTag(userId);

        // Assert — exact strings for contract testing.
        key.Should().Be("userlibrary:gamebooks:view:c8ff6a6b-c764-4f63-9ea8-f3c0705225c1");
        tag.Should().Be("user:c8ff6a6b-c764-4f63-9ea8-f3c0705225c1");
    }
}
