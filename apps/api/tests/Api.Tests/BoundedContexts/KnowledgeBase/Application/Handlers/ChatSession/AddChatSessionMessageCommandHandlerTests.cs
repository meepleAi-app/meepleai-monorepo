using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.ChatSession;

/// <summary>
/// Tests for AddChatSessionMessageCommandHandler.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AddChatSessionMessageCommandHandlerTests
{
    private readonly Mock<IChatSessionRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<AddChatSessionMessageCommandHandler>> _mockLogger;
    private readonly AddChatSessionMessageCommandHandler _handler;

    public AddChatSessionMessageCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatSessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<AddChatSessionMessageCommandHandler>>();
        _handler = new AddChatSessionMessageCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsMessageAndReturnsId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: SessionChatMessage.UserRole,
            Content: "Hello, I need help with the rules");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBe(Guid.Empty);
        _mockRepository.Verify(
            r => r.UpdateAsync(
                It.Is<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(
                    s => s.MessageCount == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAssistantRole_AddsAssistantMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: SessionChatMessage.AssistantRole,
            Content: "I can help you with that!");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBe(Guid.Empty);
        session.MessageCount.Should().Be(1);
        session.LastMessage!.IsAssistantMessage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithSystemRole_AddsSystemMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: SessionChatMessage.SystemRole,
            Content: "You are a helpful assistant");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBe(Guid.Empty);
        session.LastMessage!.IsSystemMessage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithMetadata_IncludesMetadataInMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var metadata = new Dictionary<string, object>
        {
            { "tokenCount", 50 },
            { "model", "claude-3" }
        };

        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: SessionChatMessage.UserRole,
            Content: "Test message",
            Metadata: metadata);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.LastMessage!.MetadataJson.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession?)null);

        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: SessionChatMessage.UserRole,
            Content: "Test message");

        // Act & Assert
        Func<Task> act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.Message.Should().Contain("ChatSession");
    }

    [Fact]
    public async Task Handle_MultipleMessages_AssignsCorrectSequenceNumbers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Add first message
        await _handler.Handle(
            new AddChatSessionMessageCommand(sessionId, SessionChatMessage.UserRole, "Message 1"),
            TestContext.Current.CancellationToken);

        // Add second message
        await _handler.Handle(
            new AddChatSessionMessageCommand(sessionId, SessionChatMessage.AssistantRole, "Message 2"),
            TestContext.Current.CancellationToken);

        // Assert
        session.MessageCount.Should().Be(2);
        var messages = session.Messages.ToList();
        messages[0].SequenceNumber.Should().Be(0);
        messages[1].SequenceNumber.Should().Be(1);
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
            .Setup(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("Repository.UpdateAsync"));
        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("UnitOfWork.SaveChangesAsync"));

        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: SessionChatMessage.UserRole,
            Content: "Test");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        callOrder.Count.Should().Be(2);
        callOrder[0].Should().Be("Repository.UpdateAsync");
        callOrder[1].Should().Be("UnitOfWork.SaveChangesAsync");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () =>
            _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new AddChatSessionMessageCommandHandler(
                null!,
                _mockUnitOfWork.Object,
                _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new AddChatSessionMessageCommandHandler(
                _mockRepository.Object,
                null!,
                _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new AddChatSessionMessageCommandHandler(
                _mockRepository.Object,
                _mockUnitOfWork.Object,
                null!);
        act.Should().Throw<ArgumentNullException>();
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
