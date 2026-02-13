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
/// Unit tests for GetAllAgentDefinitionsQueryHandler (Issue #3808)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAllAgentDefinitionsQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly GetAllAgentDefinitionsQueryHandler _handler;

    public GetAllAgentDefinitionsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _handler = new GetAllAgentDefinitionsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithAllAgents_ShouldReturnAll()
    {
        // Arrange
        var agents = new List<AgentDefinitionEntity>
        {
            AgentDefinitionEntity.Create("Agent1", "Desc1", AgentType.RagAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("Agent2", "Desc2", AgentType.RagAgent, AgentDefinitionConfig.Default())
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetAllAgentDefinitionsQuery(ActiveOnly: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Name == "Agent1");
        result.Should().Contain(a => a.Name == "Agent2");
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFlag_ShouldCallCorrectRepositoryMethod()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAllAgentDefinitionsQuery(ActiveOnly: true);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
