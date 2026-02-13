using Api.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Unit tests for GetAgentDefinitionByIdQueryHandler (Issue #3808)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAgentDefinitionByIdQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly GetAgentDefinitionByIdQueryHandler _handler;

    public GetAgentDefinitionByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _handler = new GetAgentDefinitionByIdQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingId_ShouldReturnDto()
    {
        // Arrange
        var agentDefinition = AgentDefinitionEntity.Create(
            "TestAgent",
            "Description",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());

        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDefinition);

        var query = new GetAgentDefinitionByIdQuery(agentDefinition.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(agentDefinition.Id);
        result.Name.Should().Be("TestAgent");
    }

    [Fact]
    public async Task Handle_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinitionEntity?)null);

        var query = new GetAgentDefinitionByIdQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
