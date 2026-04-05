using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
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
    private readonly CreateChatThreadCommandHandler _handler;

    public CreateChatThreadCommandHandlerTests()
    {
        _mockRepo = new Mock<IChatThreadRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _handler = new CreateChatThreadCommandHandler(_mockRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task Handle_WithGameId_CreatesThreadAndReturnsDto()
    {
        var gameId = Guid.NewGuid();
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

    [Fact]
    public async Task Handle_WithPrivateGameId_UsesPrivateGameIdAsEffectiveGameId()
    {
        var privateGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId, PrivateGameId: privateGameId, Title: "Private Game Chat");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.GameId.Should().Be(privateGameId); // PrivateGameId stored as GameId for RAG search
        result.AgentType.Should().BeNull();
        _mockRepo.Verify(r => r.AddAsync(It.Is<ChatThread>(t => t.GameId == privateGameId), It.IsAny<CancellationToken>()), Times.Once);
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
        var gameId = Guid.NewGuid();
        var kbIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new CreateChatThreadCommand(UserId: userId, GameId: gameId, SelectedKnowledgeBaseIds: kbIds);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        _mockRepo.Verify(r => r.AddAsync(
            It.Is<ChatThread>(t => t.GetSelectedKnowledgeBaseIds() != null && t.GetSelectedKnowledgeBaseIds()!.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
