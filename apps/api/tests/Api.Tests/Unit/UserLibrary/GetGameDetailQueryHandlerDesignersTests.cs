using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// Unit tests covering issue #2035 BE: <see cref="GetGameDetailQueryHandler"/> must
/// surface the designer names declared on the underlying SharedGame aggregate.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetGameDetailQueryHandlerDesignersTests
{
    private static GetGameDetailQueryHandler CreateHandler(
        Mock<IUserLibraryRepository> libraryRepo,
        Mock<ISharedGameRepository> sharedGameRepo,
        Mock<IGameLabelRepository> labelRepo)
    {
        // Issue #2790: HybridCache cannot be mocked (sealed). Use an in-memory L1 instance.
        HybridCache cache = TestDbContextFactory.CreateInMemoryHybridCache();

        // Issue #2034: handler now also depends on agent + chat-thread repos for
        // ConnectionBar pill counts. These tests don't exercise that surface, so
        // we stub the queries to return empty (counts default to 0).
        var agentRepo = new Mock<IAgentDefinitionRepository>();
        agentRepo
            .Setup(r => r.CountActiveByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.FindByUserIdAndGameIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatThread>());

        return new GetGameDetailQueryHandler(
            libraryRepo.Object,
            sharedGameRepo.Object,
            labelRepo.Object,
            agentRepo.Object,
            chatThreadRepo.Object,
            cache,
            NullLogger<GetGameDetailQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsDesigners_WhenSharedGameHasDesigners()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var sharedGame = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Trade, build, and settle the island of Catan.",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.3m,
            averageRating: 7.1m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 13);

        sharedGame.AddDesigner("Klaus Teuber");

        var gameId = sharedGame.Id;
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var libraryRepo = new Mock<IUserLibraryRepository>();
        libraryRepo
            .Setup(r => r.GetUserGameWithStatsAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        var sharedGameRepo = new Mock<ISharedGameRepository>();
        sharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var labelRepo = new Mock<IGameLabelRepository>();
        labelRepo
            .Setup(r => r.GetLabelsForEntryAsync(libraryEntry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.UserLibrary.Domain.Entities.GameLabel>());

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo);
        var query = new GetGameDetailQuery(userId, gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Designers.Should().NotBeNull("the handler must always emit a list for designers");
        result.Designers!.Should().ContainSingle().Which.Should().Be("Klaus Teuber");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyDesigners_WhenSharedGameHasNoDesigners()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var sharedGame = SharedGame.Create(
            title: "Codenames",
            yearPublished: 2015,
            description: "Two teams compete to find their agents.",
            minPlayers: 2,
            maxPlayers: 8,
            playingTimeMinutes: 15,
            minAge: 14,
            complexityRating: 1.3m,
            averageRating: 7.6m,
            imageUrl: "https://example.com/codenames.jpg",
            thumbnailUrl: "https://example.com/codenames-thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 178900);

        // No designers added → aggregate exposes empty IReadOnlyCollection.

        var gameId = sharedGame.Id;
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var libraryRepo = new Mock<IUserLibraryRepository>();
        libraryRepo
            .Setup(r => r.GetUserGameWithStatsAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        var sharedGameRepo = new Mock<ISharedGameRepository>();
        sharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var labelRepo = new Mock<IGameLabelRepository>();
        labelRepo
            .Setup(r => r.GetLabelsForEntryAsync(libraryEntry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.UserLibrary.Domain.Entities.GameLabel>());

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo);
        var query = new GetGameDetailQuery(userId, gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert: empty (NOT null) — the handler always emits a list for consistency
        // with the FE consumer (GameDetailDesktop.tsx reads game?.designers?.[0]?.name).
        result.Should().NotBeNull();
        result.Designers.Should().NotBeNull();
        result.Designers!.Should().BeEmpty();
    }
}
