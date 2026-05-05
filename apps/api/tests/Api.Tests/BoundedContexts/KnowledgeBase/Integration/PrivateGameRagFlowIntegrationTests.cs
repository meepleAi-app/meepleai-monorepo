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
            NullLogger<GetKnowledgeBaseStatusQueryHandler>.Instance);

        // Act
        var query = new GetKnowledgeBaseStatusQuery(privateGameId, IsPrivateGame: true);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.Progress.Should().Be(100);
        result.GameName.Should().Be("My Private Rulebook Game");
        result.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Flow: CreateChatThreadCommand with PrivateGameId → handler stores PrivateGameId as effective GameId.
    /// Uses mocks only — verifies PrivateGameId scoping logic without DB.
    /// </summary>
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Handle_PrivateGame_CreateChatThread_GameIdScopedToPrivateGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        ChatThread? capturedThread = null;
        var mockRepo = new Mock<IChatThreadRepository>();
        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Callback<ChatThread, CancellationToken>((t, _) => capturedThread = t)
            .Returns(Task.CompletedTask);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new CreateChatThreadCommandHandler(mockRepo.Object, mockUow.Object, db);
        var command = new CreateChatThreadCommand(
            UserId: userId,
            PrivateGameId: privateGameId,
            Title: "Private Game RAG Chat");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — PrivateGameId is stored as effective GameId
        result.Should().NotBeNull();
        result.GameId.Should().Be(privateGameId);
        result.UserId.Should().Be(userId);

        capturedThread.Should().NotBeNull();
        capturedThread!.GameId.Should().Be(privateGameId);

        mockRepo.Verify(
            r => r.AddAsync(
                It.Is<ChatThread>(t => t.GameId == privateGameId && t.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
