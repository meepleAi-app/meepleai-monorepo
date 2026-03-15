using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

/// <summary>
/// Unit tests for QuickCreateAgentCommandHandler.
/// Ownership/RAG access feature.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class QuickCreateAgentCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly Mock<IChatThreadRepository> _mockChatThreadRepo;
    private readonly Mock<IRagAccessService> _mockRagService;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<QuickCreateAgentCommandHandler>> _mockLogger;

    public QuickCreateAgentCommandHandlerTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockChatThreadRepo = new Mock<IChatThreadRepository>();
        _mockRagService = new Mock<IRagAccessService>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<QuickCreateAgentCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithRagAccess_CreatesAgentAndChatThread()
    {
        // Arrange
        var db = CreateDbWithSharedGameAndDocs(2);

        _mockRagService.Setup(r => r.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockAgentRepo.Setup(r => r.ExistsByNameForUserAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockAgentRepo.Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockChatThreadRepo.Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockUow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = CreateHandler(db);
        var command = new QuickCreateAgentCommand(UserId, GameId, GameId, "User", "free");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.AgentId);
        Assert.NotEqual(Guid.Empty, result.ChatThreadId);
        Assert.Contains("Test Game", result.AgentName);
        Assert.Equal(2, result.KbCardCount);
        _mockAgentRepo.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChatThreadRepo.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutRagAccess_ThrowsForbiddenException()
    {
        // Arrange
        var db = CreateDbWithSharedGameAndDocs(0);

        _mockRagService.Setup(r => r.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler(db);
        var command = new QuickCreateAgentCommand(UserId, GameId, null, "User", "free");

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AutoSelectsOnlyIndexedKbCards()
    {
        // Arrange — 2 completed, 1 pending
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = GameId,
            Title = "Test Game",
            IsRagPublic = true,
            IsDeleted = false,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        var completedDoc1 = Guid.NewGuid();
        var completedDoc2 = Guid.NewGuid();
        var pendingDoc = Guid.NewGuid();
        db.VectorDocuments.AddRange(
            new VectorDocumentEntity { Id = completedDoc1, SharedGameId = GameId, PdfDocumentId = Guid.NewGuid(), IndexingStatus = "completed" },
            new VectorDocumentEntity { Id = completedDoc2, SharedGameId = GameId, PdfDocumentId = Guid.NewGuid(), IndexingStatus = "completed" },
            new VectorDocumentEntity { Id = pendingDoc, SharedGameId = GameId, PdfDocumentId = Guid.NewGuid(), IndexingStatus = "pending" }
        );
        await db.SaveChangesAsync();

        _mockRagService.Setup(r => r.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockAgentRepo.Setup(r => r.ExistsByNameForUserAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockAgentRepo.Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockChatThreadRepo.Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockUow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = CreateHandler(db);
        var command = new QuickCreateAgentCommand(UserId, GameId, GameId, "User", "free");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — only completed docs are counted
        Assert.Equal(2, result.KbCardCount);
    }

    #region Helpers

    private QuickCreateAgentCommandHandler CreateHandler(MeepleAiDbContext db)
    {
        return new QuickCreateAgentCommandHandler(
            _mockAgentRepo.Object,
            _mockChatThreadRepo.Object,
            _mockRagService.Object,
            _mockUow.Object,
            db,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    private static MeepleAiDbContext CreateDbWithSharedGameAndDocs(int completedDocCount)
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = GameId,
            Title = "Test Game",
            IsRagPublic = true,
            IsDeleted = false,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        for (var i = 0; i < completedDocCount; i++)
        {
            db.VectorDocuments.Add(new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = GameId,
                PdfDocumentId = Guid.NewGuid(),
                IndexingStatus = "completed"
            });
        }
        db.SaveChanges();
        return db;
    }

    #endregion
}
