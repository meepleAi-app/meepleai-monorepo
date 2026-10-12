using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

/// <summary>
/// End-to-end flow integration tests for the private game RAG feature.
/// Covers: create private game → upload PDF → simulate indexing → check KB status → create chat thread.
/// Test 1 uses InMemory DB with real handler (tagged Integration).
/// Test 2 uses mocks only (tagged Unit) to verify PrivateGameId is used as effective GameId.
/// Issue #3664: Private game PDF support.
/// </summary>
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3664")]
public sealed class PrivateGameRagFlowIntegrationTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;

    public PrivateGameRagFlowIntegrationTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>
    /// Full flow: seed private game + PDF in Ready state → call GetKnowledgeBaseStatusQuery →
    /// verify status is Completed with 100% progress.
    /// Uses InMemory DB + real handler (no Testcontainers required).
    /// </summary>
    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task Handle_PrivateGame_FullFlow_KbStatusReturnsCompleted()
    {
        // Arrange — seed private game entity
        var privateGameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = privateGameId,
            Title = "My Private Rulebook Game",
            OwnerId = ownerId,
            Source = PrivateGameSource.Manual,
            MinPlayers = 2,
            MaxPlayers = 4,
        });

        // Seed PDF document simulating completed indexing (ProcessingState = "Ready")
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = privateGameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            FileSizeBytes = 102400,
            UploadedByUserId = ownerId,
            UploadedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingState = "Ready",
            IsActiveForRag = true,
        });

        await _dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeBaseStatusQueryHandler(
            _dbContext,
            new Api.BoundedContexts.KnowledgeBase.Infrastructure.Services.RagAccessService(_dbContext),
            NullLogger<GetKnowledgeBaseStatusQueryHandler>.Instance);

        // Act
        // Issue #4137: the handler authorizes, so the owner has to ask.
        var query = new GetKnowledgeBaseStatusQuery(privateGameId, ownerId, "User", IsPrivateGame: true);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.Progress.Should().Be(100);
        result.GameName.Should().Be("My Private Rulebook Game");
        result.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Flow: CreateChatThreadCommand on the caller's own private game.
    /// Issue #4230: this test asserted that the handler stored the private id in the thread's
    /// GameId, and it passed because nothing here enforces the foreign key to shared_games. On
    /// Postgres that request was a 500. Until the thread can hold a private game (#4244) the
    /// handler refuses it explicitly and stores nothing.
    /// </summary>
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Handle_PrivateGame_CreateChatThread_IsRefusedUntilTheThreadCanHoldAPrivateGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        var mockRepo = new Mock<IChatThreadRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PrivateGames.Add(new Api.Infrastructure.Entities.UserLibrary.PrivateGameEntity
        {
            Id = privateGameId, OwnerId = userId, Title = "Home-made", MinPlayers = 1, MaxPlayers = 4
        });
        await db.SaveChangesAsync();

        var handler = new CreateChatThreadCommandHandler(mockRepo.Object, mockUow.Object, db);
        var command = new CreateChatThreadCommand(
            UserId: userId,
            PrivateGameId: privateGameId,
            Title: "Private Game RAG Chat");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.PrivateGameChatUnavailableException>();
        mockRepo.Verify(
            r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
