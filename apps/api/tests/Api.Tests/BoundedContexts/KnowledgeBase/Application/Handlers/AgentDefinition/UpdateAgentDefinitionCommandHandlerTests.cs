using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
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
/// Unit tests for UpdateAgentDefinitionCommandHandler (Issue #3808)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class UpdateAgentDefinitionCommandHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly Mock<ILogger<UpdateAgentDefinitionCommandHandler>> _mockLogger;
    private readonly UpdateAgentDefinitionCommandHandler _handler;

    public UpdateAgentDefinitionCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _mockLogger = new Mock<ILogger<UpdateAgentDefinitionCommandHandler>>();
        _handler = new UpdateAgentDefinitionCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldUpdateAgentDefinition()
    {
        // Arrange
        var existingAgent = AgentDefinitionEntity.Create(
            "OriginalName",
            "Original desc",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());

        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgent);

        var command = new UpdateAgentDefinitionCommand(
            Id: existingAgent.Id,
            Name: "UpdatedName",
            Description: "Updated description",
            Type: "RAG",
            Model: "claude-3",
            MaxTokens: 4096,
            Temperature: 0.9f);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("UpdatedName");
        result.Config.Model.Should().Be("claude-3");
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingId_ShouldThrowNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinitionEntity?)null);

        var command = new UpdateAgentDefinitionCommand(
            Id: Guid.NewGuid(),
            Name: "Name",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
