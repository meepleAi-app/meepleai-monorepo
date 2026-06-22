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
/// Issue #2034: <see cref="GetGameDetailQueryHandler"/> must surface AgentCount
/// (cross-user agents linked to the SharedGame) and ChatThreadCount (the
/// caller's own chat threads for the game) so ConnectionBar pills can render
/// solid counts instead of the hardcoded zeros in <c>GameDetailDesktop.tsx</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetGameDetailQueryHandlerPillCountsTests
{
    private static GetGameDetailQueryHandler CreateHandler(
        Mock<IUserLibraryRepository> libraryRepo,
        Mock<ISharedGameRepository> sharedGameRepo,
        Mock<IGameLabelRepository> labelRepo,
        Mock<IAgentDefinitionRepository> agentRepo,
        Mock<IChatThreadRepository> chatThreadRepo)
    {
        HybridCache cache = TestDbContextFactory.CreateInMemoryHybridCache();

        return new GetGameDetailQueryHandler(
            libraryRepo.Object,
            sharedGameRepo.Object,
            labelRepo.Object,
            agentRepo.Object,
            chatThreadRepo.Object,
            cache,
            NullLogger<GetGameDetailQueryHandler>.Instance);
    }

    private static (SharedGame sharedGame, UserLibraryEntry libraryEntry) BuildCatan(Guid userId)
    {
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

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, sharedGame.Id);
        return (sharedGame, libraryEntry);
    }

    private static Mock<IUserLibraryRepository> StubLibrary(UserLibraryEntry entry, Guid userId, Guid gameId)
    {
        var repo = new Mock<IUserLibraryRepository>();
        repo.Setup(r => r.GetUserGameWithStatsAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        return repo;
    }

    private static Mock<ISharedGameRepository> StubSharedGame(SharedGame game)
    {
        var repo = new Mock<ISharedGameRepository>();
        repo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        return repo;
    }

    private static Mock<IGameLabelRepository> StubLabels(Guid entryId)
    {
        var repo = new Mock<IGameLabelRepository>();
        repo.Setup(r => r.GetLabelsForEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameLabel>());
        return repo;
    }

    [Fact]
    public async Task Handle_ReturnsAgentCount_FromAgentRepositoryQueryForThisGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (sharedGame, libraryEntry) = BuildCatan(userId);

        var libraryRepo = StubLibrary(libraryEntry, userId, sharedGame.Id);
        var sharedGameRepo = StubSharedGame(sharedGame);
        var labelRepo = StubLabels(libraryEntry.Id);

        var agentRepo = new Mock<IAgentDefinitionRepository>();
        agentRepo
            .Setup(r => r.CountActiveByGameIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == sharedGame.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.FindByUserIdAndGameIdAsync(userId, sharedGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatThread>());

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo, agentRepo, chatThreadRepo);

        // Act
        var result = await handler.Handle(new GetGameDetailQuery(userId, sharedGame.Id), CancellationToken.None);

        // Assert
        result.AgentCount.Should().Be(3);
        agentRepo.Verify(
            r => r.CountActiveByGameIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == sharedGame.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsChatThreadCount_ForOwningUserThisGameOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (sharedGame, libraryEntry) = BuildCatan(userId);

        var libraryRepo = StubLibrary(libraryEntry, userId, sharedGame.Id);
        var sharedGameRepo = StubSharedGame(sharedGame);
        var labelRepo = StubLabels(libraryEntry.Id);

        var agentRepo = new Mock<IAgentDefinitionRepository>();
        agentRepo
            .Setup(r => r.CountActiveByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Two threads owned by the requesting user for this game.
        var thread1 = new ChatThread(Guid.NewGuid(), userId, gameId: sharedGame.Id, agentType: "rules");
        var thread2 = new ChatThread(Guid.NewGuid(), userId, gameId: sharedGame.Id, agentType: "rules");

        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.FindByUserIdAndGameIdAsync(userId, sharedGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { thread1, thread2 });

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo, agentRepo, chatThreadRepo);

        // Act
        var result = await handler.Handle(new GetGameDetailQuery(userId, sharedGame.Id), CancellationToken.None);

        // Assert
        result.ChatThreadCount.Should().Be(2);
        chatThreadRepo.Verify(
            r => r.FindByUserIdAndGameIdAsync(userId, sharedGame.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsZeroCounts_WhenNoAgentsNoThreads()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (sharedGame, libraryEntry) = BuildCatan(userId);

        var libraryRepo = StubLibrary(libraryEntry, userId, sharedGame.Id);
        var sharedGameRepo = StubSharedGame(sharedGame);
        var labelRepo = StubLabels(libraryEntry.Id);

        var agentRepo = new Mock<IAgentDefinitionRepository>();
        agentRepo
            .Setup(r => r.CountActiveByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.FindByUserIdAndGameIdAsync(userId, sharedGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatThread>());

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo, agentRepo, chatThreadRepo);

        // Act
        var result = await handler.Handle(new GetGameDetailQuery(userId, sharedGame.Id), CancellationToken.None);

        // Assert
        result.AgentCount.Should().Be(0);
        result.ChatThreadCount.Should().Be(0);
    }
}
