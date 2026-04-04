using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
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
/// Unit tests for SearchAgentDefinitionsQueryHandler (Issue #3808)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SearchAgentDefinitionsQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly SearchAgentDefinitionsQueryHandler _handler;

    public SearchAgentDefinitionsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _handler = new SearchAgentDefinitionsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithMatchingSearch_ShouldReturnResults()
    {
        // Arrange
        var agents = new List<AgentDefinitionEntity>
        {
            AgentDefinitionEntity.Create("SearchAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default())
        };

        _mockRepository
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new SearchAgentDefinitionsQuery("search");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("SearchAgent");
        _mockRepository.Verify(r => r.SearchAsync("search", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new SearchAgentDefinitionsQuery("nonexistent");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
