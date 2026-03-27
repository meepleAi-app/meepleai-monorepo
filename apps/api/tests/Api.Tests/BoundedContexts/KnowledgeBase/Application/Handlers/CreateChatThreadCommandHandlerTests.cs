using Api.Infrastructure.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CreateChatThreadCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateChatThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IAgentRepository> _mockAgentRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly Mock<ILogger<CreateChatThreadCommandHandler>> _mockLogger;
    private readonly CreateChatThreadCommandHandler _handler;

    public CreateChatThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockAgentRepository = new Mock<IAgentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPublisher = new Mock<IPublisher>();
        _mockLogger = new Mock<ILogger<CreateChatThreadCommandHandler>>();

        // Default: resolve any GameId to itself (identity mapping)
        _mockAgentRepository
            .Setup(r => r.ResolveGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id);

        // Default: no agents for any game
        _mockAgentRepository
            .Setup(r => r.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        _handler = new CreateChatThreadCommandHandler(_mockRepository.Object, _mockAgentRepository.Object, _mockUnitOfWork.Object, _mockPublisher.Object, CreatePermissiveRagAccessServiceMock(), _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithInitialMessage_CreatesThreadWithMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Test Thread";
        var initialMessage = "Hello, how do I play this game?";
        var command = new CreateChatThreadCommand(userId, gameId, title, initialMessage);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Title.Should().Be(title);
        result.Status.Should().Be("active");
        result.MessageCount.Should().Be(1);
        result.Messages.Should().ContainSingle();
        result.Messages[0].Content.Should().Be(initialMessage);
        result.Messages[0].Role.Should().Be("user");

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutInitialMessage_CreatesEmptyThread()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Empty Thread";
        var command = new CreateChatThreadCommand(userId, gameId, title, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Title.Should().Be(title);
        result.Status.Should().Be("active");
        result.MessageCount.Should().Be(0);
        result.Messages.Should().BeEmpty();

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutGameId_CreatesThreadWithNullGameId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var title = "General Thread";
        var command = new CreateChatThreadCommand(userId, null, title, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameId.Should().BeNull();
        result.Title.Should().Be(title);
        result.Status.Should().Be("active");

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}
