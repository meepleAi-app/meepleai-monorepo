using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.ChatSession;

/// <summary>
/// Tests for CreateChatSessionCommandHandler.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateChatSessionCommandHandlerTests
{
    private readonly Mock<IChatSessionRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<CreateChatSessionCommandHandler>> _mockLogger;
    private readonly CreateChatSessionCommandHandler _handler;

    public CreateChatSessionCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatSessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<CreateChatSessionCommandHandler>>();
        _handler = new CreateChatSessionCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsNewSessionId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new CreateChatSessionCommand(
            UserId: userId,
            GameId: gameId,
            Title: "Test Session");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _mockRepository.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(
                s => s.UserId == userId && s.GameId == gameId && s.Title == "Test Session"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllOptionalParameters_CreatesSessionWithAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userLibraryEntryId = Guid.NewGuid();
        var agentSessionId = Guid.NewGuid();
        var agentConfigJson = "{\"model\":\"claude-3\"}";

        var command = new CreateChatSessionCommand(
            UserId: userId,
            GameId: gameId,
            Title: "Full Session",
            UserLibraryEntryId: userLibraryEntryId,
            AgentSessionId: agentSessionId,
            AgentConfigJson: agentConfigJson);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _mockRepository.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(
                s => s.UserLibraryEntryId == userLibraryEntryId &&
                     s.AgentSessionId == agentSessionId &&
                     s.AgentConfigJson == agentConfigJson),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutTitle_CreatesSessionWithNullTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new CreateChatSessionCommand(
            UserId: userId,
            GameId: gameId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _mockRepository.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(
                s => s.Title == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullAgentConfig_DefaultsToEmptyJson()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new CreateChatSessionCommand(
            UserId: userId,
            GameId: gameId,
            AgentConfigJson: null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(
                s => s.AgentConfigJson == "{}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CallsRepositoryAndUnitOfWorkInOrder()
    {
        // Arrange
        var callOrder = new List<string>();
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("Repository.AddAsync"));
        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("UnitOfWork.SaveChangesAsync"));

        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("Repository.AddAsync", callOrder[0]);
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
            new CreateChatSessionCommandHandler(
                null!,
                _mockUnitOfWork.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateChatSessionCommandHandler(
                _mockRepository.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateChatSessionCommandHandler(
                _mockRepository.Object,
                _mockUnitOfWork.Object,
                null!));
    }
}
