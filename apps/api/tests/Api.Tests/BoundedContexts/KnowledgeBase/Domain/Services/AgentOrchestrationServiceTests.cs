using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Moq;
using Xunit;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Application.Services;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for AgentOrchestrationService.
/// Tests agent selection logic and execution coordination.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AgentOrchestrationServiceTests
{
    private readonly AgentOrchestrationService _service;

    public AgentOrchestrationServiceTests()
    {
        _service = new AgentOrchestrationService();
    }

    [Fact]
    public void SelectAgentForQuery_WithRulesInterpretationQuery_SelectsRulesInterpreterAgent()
    {
        // Arrange
        var query = "What are the rules for moving a pawn in chess?";
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Rules Agent", AgentType.RulesInterpreter),
            CreateAgent("Citation Agent", AgentType.CitationAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("Rules Agent", selected.Name);
        Assert.Equal(AgentType.RulesInterpreter.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_WithCitationQuery_SelectsCitationAgent()
    {
        // Arrange
        var query = "Where did this information come from? Show me the source.";
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Citation Agent", AgentType.CitationAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("Citation Agent", selected.Name);
        Assert.Equal(AgentType.CitationAgent.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_WithConfidenceQuery_SelectsConfidenceAgent()
    {
        // Arrange
        var query = "How confident are you in this answer?";
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Confidence Agent", AgentType.ConfidenceAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("Confidence Agent", selected.Name);
        Assert.Equal(AgentType.ConfidenceAgent.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_WithConversationQuery_SelectsConversationAgent()
    {
        // Arrange
        var query = "And what about the other rule you mentioned earlier?";
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Conversation Agent", AgentType.ConversationAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("Conversation Agent", selected.Name);
        Assert.Equal(AgentType.ConversationAgent.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_WithGeneralQuery_SelectsRagAgent()
    {
        // Arrange
        var query = "Tell me about Catan";
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Citation Agent", AgentType.CitationAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("RAG Agent", selected.Name);
        Assert.Equal(AgentType.RagAgent.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_WithNoSpecializedAgent_FallsBackToRagAgent()
    {
        // Arrange
        var query = "What are the rules for setup phase?";
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Citation Agent", AgentType.CitationAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("RAG Agent", selected.Name);
    }

    [Fact]
    public void SelectAgentForQuery_WithInactiveAgents_ReturnsNull()
    {
        // Arrange
        var query = "Any query";
        var agents = new List<Agent>
        {
            CreateInactiveAgent("Inactive Agent", AgentType.RagAgent)
        };

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.Null(selected);
    }

    [Fact]
    public void SelectAgentForQuery_WithEmptyAgentList_ReturnsNull()
    {
        // Arrange
        var query = "Any query";
        var agents = new List<Agent>();

        // Act
        var selected = _service.SelectAgentForQuery(query, agents);

        // Assert
        Assert.Null(selected);
    }

    [Fact]
    public void SelectAgentForQuery_WithNullAgentList_ReturnsNull()
    {
        // Arrange
        var query = "Any query";

        // Act
        var selected = _service.SelectAgentForQuery(query, null!);

        // Assert
        Assert.Null(selected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SelectAgentForQuery_WithInvalidQuery_ThrowsArgumentException(string invalidQuery)
    {
        // Arrange
        var agents = new List<Agent> { CreateAgent("Test", AgentType.RagAgent) };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.SelectAgentForQuery(invalidQuery, agents));
    }

    [Fact]
    public void ExecuteAgent_WithValidInputs_ReturnsSuccessfulResult()
    {
        // Arrange
        var agent = CreateAgent("Test Agent", AgentType.RagAgent);
        var queryVector = new Vector(new[] { 0.1f, 0.2f, 0.3f });
        var similarVector = new Vector(new[] { 0.11f, 0.21f, 0.31f }); // Similar but not identical
        var embeddings = new List<Embedding>
        {
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: "chunk1",
                vector: similarVector,
                model: "test-model",
                chunkIndex: 0,
                pageNumber: 1
            )
        };
        var context = new AgentInvocationContext(
            "test query",
            queryVector,
            embeddings
        );

        var vectorSearch = new VectorSearchDomainService();
        var qualityTracking = new QualityTrackingDomainService();

        // Act
        var result = _service.ExecuteAgent(agent, context, vectorSearch, qualityTracking);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agent.Id, result.AgentId);
        Assert.Equal(agent.Name, result.AgentName);
        Assert.Equal("test query", result.Query);
        Assert.Equal(1, agent.InvocationCount);
    }

    [Fact]
    public void ExecuteAgent_WithInactiveAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var agent = CreateInactiveAgent("Inactive", AgentType.RagAgent);
        var context = CreateTestContext();
        var vectorSearch = new VectorSearchDomainService();
        var qualityTracking = new QualityTrackingDomainService();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.ExecuteAgent(agent, context, vectorSearch, qualityTracking));
        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecuteAgent_WithNullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateTestContext();
        var vectorSearch = new VectorSearchDomainService();
        var qualityTracking = new QualityTrackingDomainService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.ExecuteAgent(null!, context, vectorSearch, qualityTracking));
    }

    [Fact]
    public void ExecuteAgent_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var agent = CreateAgent("Test", AgentType.RagAgent);
        var vectorSearch = new VectorSearchDomainService();
        var qualityTracking = new QualityTrackingDomainService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.ExecuteAgent(agent, null!, vectorSearch, qualityTracking));
    }

    [Fact]
    public void ExecuteAgent_ExtractsStrategyParametersCorrectly()
    {
        // Arrange
        var customStrategy = AgentStrategy.HybridSearch(vectorWeight: 0.8, topK: 15, minScore: 0.60);
        var agent = new Agent(
            Guid.NewGuid(),
            "Custom Agent",
            AgentType.RagAgent,
            customStrategy,
            isActive: true
        );

        var context = CreateTestContextWithDifferentVectors();
        var vectorSearch = new VectorSearchDomainService();
        var qualityTracking = new QualityTrackingDomainService();

        // Act
        var result = _service.ExecuteAgent(agent, context, vectorSearch, qualityTracking);

        // Assert
        Assert.NotNull(result);
        // Verify strategy was applied (indirectly through result)
        Assert.NotNull(result.SearchResults);
    }

    // Helper methods
    private static Agent CreateAgent(string name, AgentType type)
    {
        return new Agent(
            Guid.NewGuid(),
            name,
            type,
            AgentStrategy.HybridSearch(),
            isActive: true
        );
    }

    private static Agent CreateInactiveAgent(string name, AgentType type)
    {
        var agent = new Agent(
            Guid.NewGuid(),
            name,
            type,
            AgentStrategy.HybridSearch(),
            isActive: true
        );
        agent.Deactivate();
        return agent;
    }

    private static AgentInvocationContext CreateTestContext()
    {
        var queryVector = new Vector(new[] { 0.1f, 0.2f, 0.3f });
        var similarVector = new Vector(new[] { 0.11f, 0.21f, 0.31f }); // Similar but not identical
        var embeddings = new List<Embedding>
        {
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: "test chunk",
                vector: similarVector,
                model: "test-model",
                chunkIndex: 0,
                pageNumber: 1
            )
        };
        return new AgentInvocationContext(
            "test query",
            queryVector,
            embeddings
        );
    }

    private static AgentInvocationContext CreateTestContextWithDifferentVectors()
    {
        var queryVector = new Vector(new[] { 0.5f, 0.5f, 0.5f });
        var similarVector1 = new Vector(new[] { 0.6f, 0.4f, 0.5f });
        var similarVector2 = new Vector(new[] { 0.4f, 0.6f, 0.5f });
        var embeddings = new List<Embedding>
        {
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: "test chunk 1",
                vector: similarVector1,
                model: "test-model",
                chunkIndex: 0,
                pageNumber: 1
            ),
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: "test chunk 2",
                vector: similarVector2,
                model: "test-model",
                chunkIndex: 1,
                pageNumber: 1
            )
        };
        return new AgentInvocationContext(
            "test query",
            queryVector,
            embeddings
        );
    }
}

