using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for DeleteUserAgentCommandHandler.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteUserAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _repository = new();
    private readonly Mock<ILogger<DeleteUserAgentCommandHandler>> _logger = new();
    private readonly DeleteUserAgentCommandHandler _handler;

    public DeleteUserAgentCommandHandlerTests()
    {
        _handler = new DeleteUserAgentCommandHandler(_repository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_OwnerDeletes_DeactivatesAgent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new DeleteUserAgentCommand(AgentId: agentId, UserId: userId, UserRole: "User");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        // Soft-delete: should call UpdateAsync (deactivate), NOT DeleteAsync
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AdminDeletes_DeactivatesAgent()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: ownerId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new DeleteUserAgentCommand(AgentId: agentId, UserId: adminId, UserRole: "Admin");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonOwnerDeletes_ThrowsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: ownerId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new DeleteUserAgentCommand(AgentId: agentId, UserId: otherId, UserRole: "User");

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AgentNotFound_ThrowsNotFound()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        var command = new DeleteUserAgentCommand(AgentId: agentId, UserId: Guid.NewGuid(), UserRole: "User");

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
