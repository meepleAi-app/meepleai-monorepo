using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
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
/// Unit tests for GetAgentDefinitionStatsQueryHandler (Issue #3708)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3708")]
public sealed class GetAgentDefinitionStatsQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly GetAgentDefinitionStatsQueryHandler _handler;

    public GetAgentDefinitionStatsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _handler = new GetAgentDefinitionStatsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithMultipleDefinitions_ShouldReturnAggregatedStats()
    {
        // Arrange
        var definitions = new List<AgentDefinitionEntity>
        {
            AgentDefinitionEntity.Create("Agent1", "Desc1", AgentType.RagAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("Agent2", "Desc2", AgentType.CitationAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("Agent3", "Desc3", AgentType.RagAgent, AgentDefinitionConfig.Default())
        };
        definitions[2].Deactivate(); // One inactive

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        var query = new GetAgentDefinitionStatsQuery { ActiveOnly = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalDefinitions.Should().Be(3);
        result.ActiveDefinitions.Should().Be(2);
        result.InactiveDefinitions.Should().Be(1);
        result.DistributionByType.Should().HaveCount(2);
        result.DistributionByType.Should().Contain(d => d.Type == "RAG" && d.Count == 2);
        result.DistributionByType.Should().Contain(d => d.Type == "Citation" && d.Count == 1);
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFilter_ShouldReturnOnlyActive()
    {
        // Arrange
        var activeAgent = AgentDefinitionEntity.Create("Active", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var inactiveAgent = AgentDefinitionEntity.Create("Inactive", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        inactiveAgent.Deactivate();

        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { activeAgent });

        var query = new GetAgentDefinitionStatsQuery { ActiveOnly = true };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalDefinitions.Should().Be(1);
        result.ActiveDefinitions.Should().Be(1);
        result.InactiveDefinitions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithEmptyRepository_ShouldReturnZeroStats()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAgentDefinitionStatsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalDefinitions.Should().Be(0);
        result.ActiveDefinitions.Should().Be(0);
        result.InactiveDefinitions.Should().Be(0);
        result.DistributionByType.Should().BeEmpty();
        result.RecentDefinitions.Should().BeEmpty();
        result.OldestCreatedAt.Should().BeNull();
        result.NewestCreatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithMixedTypes_ShouldGroupCorrectly()
    {
        // Arrange
        var definitions = new List<AgentDefinitionEntity>
        {
            AgentDefinitionEntity.Create("RAG1", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("RAG2", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("Citation1", "Desc", AgentType.CitationAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("Confidence1", "Desc", AgentType.ConfidenceAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("Rules1", "Desc", AgentType.RulesInterpreter, AgentDefinitionConfig.Default())
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        var query = new GetAgentDefinitionStatsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.DistributionByType.Should().HaveCount(4);
        result.DistributionByType.First().Type.Should().Be("RAG"); // Most common first
        result.DistributionByType.First().Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnRecentDefinitionsInDescendingOrder()
    {
        // Arrange
        var definitions = new List<AgentDefinitionEntity>
        {
            AgentDefinitionEntity.Create("Old", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default()),
            AgentDefinitionEntity.Create("New", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default())
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        var query = new GetAgentDefinitionStatsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.RecentDefinitions.Should().HaveCount(2);
        result.RecentDefinitions[0].Name.Should().Be("New");
        result.RecentDefinitions[1].Name.Should().Be("Old");
    }
}
