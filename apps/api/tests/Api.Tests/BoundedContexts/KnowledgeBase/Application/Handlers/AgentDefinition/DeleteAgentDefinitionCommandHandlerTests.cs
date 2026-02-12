using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Unit tests for DeleteAgentDefinitionCommandHandler (Issue #3808)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class DeleteAgentDefinitionCommandHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly Mock<ILogger<DeleteAgentDefinitionCommandHandler>> _mockLogger;
    private readonly DeleteAgentDefinitionCommandHandler _handler;

    public DeleteAgentDefinitionCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _mockLogger = new Mock<ILogger<DeleteAgentDefinitionCommandHandler>>();
        _handler = new DeleteAgentDefinitionCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingId_ShouldDelete()
    {
        // Arrange
        var agentDefinition = AgentDefinitionEntity.Create(
            "TestAgent",
            "Desc",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());

        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDefinition);

        var command = new DeleteAgentDefinitionCommand(agentDefinition.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(agentDefinition.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingId_ShouldThrowNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinitionEntity?)null);

        var command = new DeleteAgentDefinitionCommand(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
