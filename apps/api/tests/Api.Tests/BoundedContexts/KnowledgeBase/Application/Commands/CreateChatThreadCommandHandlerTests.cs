using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CreateChatThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly MeepleAiDbContext _db;
    private readonly CreateChatThreadCommandHandler _handler;

    public CreateChatThreadCommandHandlerTests()
    {
        _mockRepo = new Mock<IChatThreadRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _handler = new CreateChatThreadCommandHandler(_mockRepo.Object, _mockUow.Object, _db);
    }

    private async Task<Guid> SeedSharedGameAsync()
    {
        var game = new SharedGameEntity { Id = Guid.NewGuid(), Title = "Catan" };
        _db.SharedGames.Add(game);
        await _db.SaveChangesAsync();
        return game.Id;
    }

    private async Task<Guid> SeedPrivateGameAsync(Guid ownerId)
    {
        var game = new PrivateGameEntity { Id = Guid.NewGuid(), OwnerId = ownerId, Title = "Home-made", MinPlayers = 1, MaxPlayers = 4 };
        _db.PrivateGames.Add(game);
        await _db.SaveChangesAsync();
        return game.Id;
    }

    private void NothingWasStored()
    {
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithGameId_CreatesThreadAndReturnsDto()
    {
        var gameId = await SeedSharedGameAsync();
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId, GameId: gameId, Title: "Test");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Title.Should().Be("Test");
        result.Status.Should().Be("active");
        result.Messages.Should().BeEmpty();
        _mockRepo.Verify(r => r.AddAsync(It.Is<ChatThread>(t => t.UserId == userId && t.GameId == gameId), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Issue #4230. A thread on the caller's own private game cannot be stored: ChatThreads.GameId
    /// references shared_games. This test used to assert the opposite ("PrivateGameId stored as
    /// GameId"), and passed only because the in-memory provider enforces no foreign key; on
    /// Postgres the same request was a 500. The web client sends the private id as <c>gameId</c>,
    /// the API also accepts <c>privateGameId</c>: both are refused the same way.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_OnTheCallersOwnPrivateGame_SaysItIsNotAvailableYet(bool sentAsPrivateGameId)
    {
        var userId = Guid.NewGuid();
        var privateGameId = await SeedPrivateGameAsync(userId);
        var command = sentAsPrivateGameId
            ? new CreateChatThreadCommand(UserId: userId, PrivateGameId: privateGameId, Title: "Private Game Chat")
            : new CreateChatThreadCommand(UserId: userId, GameId: privateGameId, Title: "Private Game Chat");

        var act = () => _handler.Handle(command, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<PrivateGameChatUnavailableException>();
        thrown.Which.PrivateGameId.Should().Be(privateGameId);
        thrown.Which.StatusCode.Should().Be(422);
        thrown.Which.ErrorCode.Should().Be("PRIVATE_GAME_CHAT_UNAVAILABLE");
        NothingWasStored();
    }

    /// <summary>
    /// Someone else's private game answers exactly like a game that does not exist, so the
    /// response does not reveal that the id is real.
    /// </summary>
    [Fact]
    public async Task Handle_OnSomeoneElsesPrivateGame_IsNotFound()
    {
        var privateGameId = await SeedPrivateGameAsync(ownerId: Guid.NewGuid());
        var command = new CreateChatThreadCommand(UserId: Guid.NewGuid(), GameId: privateGameId, Title: "Not mine");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        NothingWasStored();
    }

    /// <summary>
    /// A deleted game is gone for the chat too. For a shared game this is a change: its row
    /// stays in the table (soft delete), so the foreign key held and the thread used to be
    /// created on a game nobody can open.
    /// </summary>
    [Fact]
    public async Task Handle_OnASoftDeletedSharedGame_IsNotFound()
    {
        var game = new SharedGameEntity { Id = Guid.NewGuid(), Title = "Withdrawn", IsDeleted = true };
        _db.SharedGames.Add(game);
        await _db.SaveChangesAsync();
        var command = new CreateChatThreadCommand(UserId: Guid.NewGuid(), GameId: game.Id, Title: "Gone");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        NothingWasStored();
    }

    [Fact]
    public async Task Handle_OnTheCallersOwnSoftDeletedPrivateGame_IsNotFound()
    {
        var userId = Guid.NewGuid();
        var game = new PrivateGameEntity { Id = Guid.NewGuid(), OwnerId = userId, Title = "Deleted", MinPlayers = 1, MaxPlayers = 4, IsDeleted = true };
        _db.PrivateGames.Add(game);
        await _db.SaveChangesAsync();
        var command = new CreateChatThreadCommand(UserId: userId, GameId: game.Id, Title: "Gone");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        NothingWasStored();
    }

    [Fact]
    public async Task Handle_WithNoGameId_CreatesThreadWithNullGameId()
    {
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.GameId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAgentId_SetsAgentOnThread()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId, AgentId: agentId, AgentType: "tutor");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AgentId.Should().Be(agentId);
        result.AgentType.Should().Be("tutor");
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithSelectedKnowledgeBaseIds_SetsThemOnThread()
    {
        var userId = Guid.NewGuid();
        var gameId = await SeedSharedGameAsync();
        var kbIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new CreateChatThreadCommand(UserId: userId, GameId: gameId, SelectedKnowledgeBaseIds: kbIds);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        _mockRepo.Verify(r => r.AddAsync(
            It.Is<ChatThread>(t => t.GetSelectedKnowledgeBaseIds() != null && t.GetSelectedKnowledgeBaseIds()!.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDirectGameId_DoesNotResolve()
    {
        // Arrange: game exists by its direct Id
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Catan" });
        await _db.SaveChangesAsync();

        var command = new CreateChatThreadCommand(UserId: userId, GameId: gameId, Title: "Chat about Catan");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: the thread should keep the same gameId (no resolution needed)
        result.Should().NotBeNull();
        result.GameId.Should().Be(gameId);
    }

    /// <summary>
    /// Issue #4230. This test used to be <c>Handle_WithUnknownGameId_KeepsOriginalId</c>, with the
    /// comment "will fail FK at DB level, but handler doesn't enforce": it asserted the defect.
    /// </summary>
    [Fact]
    public async Task Handle_WithUnknownGameId_IsNotFound_AndStoresNothing()
    {
        var command = new CreateChatThreadCommand(UserId: Guid.NewGuid(), GameId: Guid.NewGuid(), Title: "Unknown game");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        NothingWasStored();
    }
}
