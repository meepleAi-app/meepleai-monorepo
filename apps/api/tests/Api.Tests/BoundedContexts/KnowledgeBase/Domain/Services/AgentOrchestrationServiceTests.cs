using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Tests for AgentOrchestrationService query classification and agent routing.
/// Issue #4338: Unified API Gateway
/// </summary>
public class AgentOrchestrationServiceTests
{
    private readonly AgentOrchestrationService _sut = new();

    private static Agent CreateAgent(string name, AgentType type, bool isActive = true)
    {
        return new Agent(
            id: Guid.NewGuid(),
            name: name,
            type: type,
            strategy: AgentStrategy.HybridSearch(),
            isActive: isActive);
    }

    private static List<Agent> CreateAllAgents()
    {
        return
        [
            CreateAgent("RAG Agent", AgentType.RagAgent),
            CreateAgent("Rules Agent", AgentType.RulesInterpreter),
            CreateAgent("Citation Agent", AgentType.CitationAgent),
            CreateAgent("Confidence Agent", AgentType.ConfidenceAgent),
            CreateAgent("Conversation Agent", AgentType.ConversationAgent),
        ];
    }

    // ============================================================================
    // Query Classification → Agent Selection
    // ============================================================================

    [Theory]
    [InlineData("What are the rules for trading in Catan?")]
    [InlineData("Can I place a settlement here?")]
    [InlineData("Is this a legal move?")]
    [InlineData("Explain the setup phase")]
    [InlineData("What happens during my turn?")]
    public void SelectAgentForQuery_RulesQuery_SelectsRulesInterpreter(string query)
    {
        var agents = CreateAllAgents();
        var selected = _sut.SelectAgentForQuery(query, agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.RulesInterpreter.Value, selected.Type.Value);
    }

    [Theory]
    [InlineData("Where did you find that information?")]
    [InlineData("Show me the source for that rule")]
    [InlineData("What is the citation for this?")]
    [InlineData("Can you give me a reference?")]
    public void SelectAgentForQuery_CitationQuery_SelectsCitationAgent(string query)
    {
        var agents = CreateAllAgents();
        var selected = _sut.SelectAgentForQuery(query, agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.CitationAgent.Value, selected.Type.Value);
    }

    [Theory]
    [InlineData("How sure are you about that?")]
    [InlineData("What is the accuracy of this answer?")]
    [InlineData("How confident are you?")]
    [InlineData("Are you certain about this rule?")]
    public void SelectAgentForQuery_ConfidenceQuery_SelectsConfidenceAgent(string query)
    {
        var agents = CreateAllAgents();
        var selected = _sut.SelectAgentForQuery(query, agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.ConfidenceAgent.Value, selected.Type.Value);
    }

    [Theory]
    [InlineData("And what about the other players?")]
    [InlineData("But you said earlier it was different")]
    [InlineData("Also tell me about expansions")]
    public void SelectAgentForQuery_ContinuationQuery_SelectsConversationAgent(string query)
    {
        var agents = CreateAllAgents();
        var selected = _sut.SelectAgentForQuery(query, agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.ConversationAgent.Value, selected.Type.Value);
    }

    [Theory]
    [InlineData("Tell me about Catan")]
    [InlineData("What is the best strategy?")]
    [InlineData("How many players can play Ticket to Ride?")]
    public void SelectAgentForQuery_GeneralQuery_SelectsRagAgent(string query)
    {
        var agents = CreateAllAgents();
        var selected = _sut.SelectAgentForQuery(query, agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.RagAgent.Value, selected.Type.Value);
    }

    // ============================================================================
    // Fallback Behavior
    // ============================================================================

    [Fact]
    public void SelectAgentForQuery_NoSpecialist_FallsBackToRagAgent()
    {
        var agents = new List<Agent>
        {
            CreateAgent("RAG Agent", AgentType.RagAgent),
        };

        // "rules" query but no RulesInterpreter available → falls back to RAG
        var selected = _sut.SelectAgentForQuery("What are the rules?", agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.RagAgent.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_NoRagAgent_FallsBackToMostRecentlyUsed()
    {
        var agents = new List<Agent>
        {
            CreateAgent("Citation Agent", AgentType.CitationAgent),
        };

        var selected = _sut.SelectAgentForQuery("Tell me about Catan", agents);

        Assert.NotNull(selected);
        Assert.Equal(AgentType.CitationAgent.Value, selected.Type.Value);
    }

    [Fact]
    public void SelectAgentForQuery_EmptyList_ReturnsNull()
    {
        var selected = _sut.SelectAgentForQuery("Hello", new List<Agent>());
        Assert.Null(selected);
    }

    [Fact]
    public void SelectAgentForQuery_AllInactive_ReturnsNull()
    {
        var agents = new List<Agent>
        {
            CreateAgent("Inactive RAG", AgentType.RagAgent, isActive: false),
        };

        var selected = _sut.SelectAgentForQuery("Hello", agents);
        Assert.Null(selected);
    }

    [Fact]
    public void SelectAgentForQuery_EmptyQuery_ThrowsArgumentException()
    {
        var agents = CreateAllAgents();
        Assert.Throws<ArgumentException>(() => _sut.SelectAgentForQuery("", agents));
        Assert.Throws<ArgumentException>(() => _sut.SelectAgentForQuery("  ", agents));
    }
}
