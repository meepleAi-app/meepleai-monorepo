using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.ChatSession;

/// <summary>
/// Tests for DeleteChatSessionCommandHandler.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteChatSessionCommandHandlerTests
{
    private readonly Mock<IChatSessionRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<DeleteChatSessionCommandHandler>> _mockLogger;
    private readonly DeleteChatSessionCommandHandler _handler;

    public DeleteChatSessionCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatSessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<DeleteChatSessionCommandHandler>>();
        _handler = new DeleteChatSessionCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_DeletesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new DeleteChatSessionCommand(SessionId: sessionId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteAsync(
                It.Is<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(
                    s => s.Id == sessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession?)null);

        var command = new DeleteChatSessionCommand(SessionId: sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("ChatSession", ex.Message);
    }

    [Fact]
    public async Task Handle_CallsRepositoryAndUnitOfWorkInOrder()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var callOrder = new List<string>();

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("Repository.DeleteAsync"));
        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("UnitOfWork.SaveChangesAsync"));

        var command = new DeleteChatSessionCommand(SessionId: sessionId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("Repository.DeleteAsync", callOrder[0]);
        Assert.Equal("UnitOfWork.SaveChangesAsync", callOrder[1]);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteChatSessionCommandHandler(
                null!,
                _mockUnitOfWork.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteChatSessionCommandHandler(
                _mockRepository.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteChatSessionCommandHandler(
                _mockRepository.Object,
                _mockUnitOfWork.Object,
                null!));
    }

    private static Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession CreateTestSession(Guid sessionId)
    {
        return new Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession(
            id: sessionId,
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            title: "Test Session");
    }
}
