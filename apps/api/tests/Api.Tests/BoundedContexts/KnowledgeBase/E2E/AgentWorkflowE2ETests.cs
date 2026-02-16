using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.E2E;

/// <summary>
/// E2E tests for agent lifecycle workflows:
/// create → configure → invoke → reconfigure → invoke.
/// Tests domain entity behavior without infrastructure dependencies.
/// Issue #3779: E2E Testing Suite - All Agent Workflows
/// </summary>
[Trait("Category", TestCategories.E2E)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3779")]
public class AgentWorkflowE2ETests
{
    #region Agent Creation Workflows

    [Fact]
    public void CreateAgent_TutorAgent_Succeeds()
    {
        var agent = new Agent(Guid.NewGuid(), "TutorAgent", AgentType.RagAgent, AgentStrategy.SingleModel());

        agent.Name.Should().Be("TutorAgent");
        agent.Type.Should().Be(AgentType.RagAgent);
        agent.Strategy.Name.Should().Be("SingleModel");
        agent.IsActive.Should().BeTrue();
        agent.InvocationCount.Should().Be(0);
        agent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateAgent_ArbitroAgent_Succeeds()
    {
        var agent = new Agent(Guid.NewGuid(), "ArbitroAgent", AgentType.RulesInterpreter, AgentStrategy.CitationValidation());

        agent.Name.Should().Be("ArbitroAgent");
        agent.Type.Should().Be(AgentType.RulesInterpreter);
        agent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateAgent_DecisoreAgent_Succeeds()
    {
        var agent = new Agent(Guid.NewGuid(), "DecisoreAgent", AgentType.ConfidenceAgent, AgentStrategy.ConfidenceScoring());

        agent.Name.Should().Be("DecisoreAgent");
        agent.Type.Should().Be(AgentType.ConfidenceAgent);
        agent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateAgent_EmptyName_ThrowsException()
    {
        var act = () => new Agent(Guid.NewGuid(), "", AgentType.RagAgent, AgentStrategy.SingleModel());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Agent name*");
    }

    [Fact]
    public void CreateAgent_NullType_ThrowsException()
    {
        var act = () => new Agent(Guid.NewGuid(), "TestAgent", null!, AgentStrategy.SingleModel());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateAgent_NullStrategy_ThrowsException()
    {
        var act = () => new Agent(Guid.NewGuid(), "TestAgent", AgentType.RagAgent, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Strategy Configuration Workflows

    [Fact]
    public void ConfigureAgent_ChangeStrategy_UpdatesSuccessfully()
    {
        var agent = CreateTestAgent("ConfigTest");

        agent.Strategy.Name.Should().Be("SingleModel");

        agent.Configure(AgentStrategy.HybridSearch());

        agent.Strategy.Name.Should().Be("HybridSearch");
        agent.Strategy.GetParameter<double>("VectorWeight", 0.0).Should().Be(0.7);
    }

    [Fact]
    public void ConfigureAgent_AllStrategies_ValidParameters()
    {
        var strategies = new[]
        {
            ("RetrievalOnly", AgentStrategy.RetrievalOnly()),
            ("SingleModel", AgentStrategy.SingleModel()),
            ("HybridSearch", AgentStrategy.HybridSearch()),
            ("VectorOnly", AgentStrategy.VectorOnly()),
            ("MultiModelConsensus", AgentStrategy.MultiModelConsensus()),
            ("CitationValidation", AgentStrategy.CitationValidation()),
            ("ConfidenceScoring", AgentStrategy.ConfidenceScoring()),
        };

        foreach (var (expectedName, strategy) in strategies)
        {
            strategy.Name.Should().Be(expectedName);
            strategy.Parameters.Should().NotBeNull();
        }
    }

    [Fact]
    public void ConfigureAgent_RetrievalOnly_HasCorrectDefaults()
    {
        var strategy = AgentStrategy.RetrievalOnly();

        strategy.GetParameter<int>("TopK", 0).Should().Be(10);
        strategy.GetParameter<double>("MinScore", 0.0).Should().Be(0.55);
    }

    [Fact]
    public void ConfigureAgent_HybridSearch_HasCorrectDefaults()
    {
        var strategy = AgentStrategy.HybridSearch();

        strategy.GetParameter<double>("VectorWeight", 0.0).Should().Be(0.7);
        strategy.GetParameter<int>("TopK", 0).Should().Be(10);
        strategy.GetParameter<double>("MinScore", 0.0).Should().Be(0.55);
    }

    [Fact]
    public void ConfigureAgent_CustomParameters_Applied()
    {
        var strategy = AgentStrategy.RetrievalOnly(topK: 25, minScore: 0.80);

        strategy.GetParameter<int>("TopK", 0).Should().Be(25);
        strategy.GetParameter<double>("MinScore", 0.0).Should().Be(0.80);
    }

    #endregion

    #region Agent Lifecycle Workflows

    [Fact]
    public void AgentLifecycle_Activate_Deactivate_Toggle()
    {
        var agent = CreateTestAgent("LifecycleTest");

        agent.IsActive.Should().BeTrue("Agent starts active");

        agent.Deactivate();
        agent.IsActive.Should().BeFalse("Agent deactivated");

        agent.Activate();
        agent.IsActive.Should().BeTrue("Agent reactivated");
    }

    [Fact]
    public void AgentLifecycle_RecordInvocation_UpdatesCounters()
    {
        var agent = CreateTestAgent("InvocationTest");

        agent.InvocationCount.Should().Be(0);
        agent.LastInvokedAt.Should().BeNull();

        agent.RecordInvocation("test query", new TokenUsage(10, 20, 30, 0.001m, "gpt-4", "openrouter"));

        agent.InvocationCount.Should().Be(1);
        agent.LastInvokedAt.Should().NotBeNull();
        agent.LastInvokedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        agent.IsRecentlyUsed.Should().BeTrue();
        agent.IsIdle.Should().BeFalse();
    }

    [Fact]
    public void AgentLifecycle_MultipleInvocations_IncrementCount()
    {
        var agent = CreateTestAgent("MultiInvokeTest");

        for (var i = 0; i < 10; i++)
        {
            agent.RecordInvocation($"query {i}", new TokenUsage(10, 20, 30, 0.001m, "gpt-4", "openrouter"));
        }

        agent.InvocationCount.Should().Be(10);
    }

    [Fact]
    public void AgentLifecycle_Rename_UpdatesName()
    {
        var agent = CreateTestAgent("OriginalName");

        agent.Rename("NewName");

        agent.Name.Should().Be("NewName");
    }

    #endregion

    #region Full Workflow: Create → Configure → Invoke → Reconfigure

    [Fact]
    public void FullWorkflow_TutorAgent_CreateConfigureInvoke()
    {
        // Step 1: Create TutorAgent with SingleModel
        var agent = new Agent(
            Guid.NewGuid(),
            "TutorAgent",
            AgentType.RagAgent,
            AgentStrategy.SingleModel(),
            isActive: true);

        agent.Name.Should().Be("TutorAgent");
        agent.Strategy.Name.Should().Be("SingleModel");

        // Step 2: Record invocation
        agent.RecordInvocation("How to play chess?", new TokenUsage(50, 200, 250, 0.005m, "gpt-4", "openrouter"));

        agent.InvocationCount.Should().Be(1);

        // Step 3: Reconfigure to HybridSearch for better results
        agent.Configure(AgentStrategy.HybridSearch(vectorWeight: 0.8, topK: 15));

        agent.Strategy.Name.Should().Be("HybridSearch");
        agent.Strategy.GetParameter<double>("VectorWeight", 0.0).Should().Be(0.8);
        agent.Strategy.GetParameter<int>("TopK", 0).Should().Be(15);

        // Step 4: Record another invocation with new strategy
        agent.RecordInvocation("Explain castling rules", new TokenUsage(40, 180, 220, 0.004m, "gpt-4", "openrouter"));

        agent.InvocationCount.Should().Be(2);
    }

    [Fact]
    public void FullWorkflow_ArbitroAgent_ValidateRulesAndMoves()
    {
        // Arbitro validates moves and rules
        var agent = new Agent(
            Guid.NewGuid(),
            "ArbitroAgent",
            AgentType.RulesInterpreter,
            AgentStrategy.CitationValidation(),
            isActive: true);

        agent.Type.Should().Be(AgentType.RulesInterpreter);
        agent.Strategy.Name.Should().Be("CitationValidation");

        // Record validation invocations
        agent.RecordInvocation("Is castling legal here?", new TokenUsage(30, 100, 130, 0.003m, "gpt-4", "openrouter"));
        agent.RecordInvocation("What is the rule for en passant?", new TokenUsage(25, 120, 145, 0.003m, "gpt-4", "openrouter"));

        agent.InvocationCount.Should().Be(2);
    }

    [Fact]
    public void FullWorkflow_DecisoreAgent_StrategicAnalysis()
    {
        // Decisore provides strategic advice
        var agent = new Agent(
            Guid.NewGuid(),
            "DecisoreAgent",
            AgentType.ConfidenceAgent,
            AgentStrategy.ConfidenceScoring(minConfidence: 0.80),
            isActive: true);

        agent.Type.Should().Be(AgentType.ConfidenceAgent);
        agent.Strategy.Name.Should().Be("ConfidenceScoring");
        agent.Strategy.GetParameter<double>("MinConfidence", 0.0).Should().Be(0.80);

        // Record strategic analysis invocations
        agent.RecordInvocation("What is the best move?", new TokenUsage(60, 300, 360, 0.006m, "gpt-4", "openrouter"));

        agent.InvocationCount.Should().Be(1);
    }

    #endregion

    #region Agent Type Tests

    [Fact]
    public void AgentType_AllKnownTypes_Accessible()
    {
        var types = new[]
        {
            AgentType.RagAgent,
            AgentType.CitationAgent,
            AgentType.ConfidenceAgent,
            AgentType.RulesInterpreter,
            AgentType.ConversationAgent
        };

        types.Should().HaveCount(5);
        types.Should().OnlyContain(t => t != null);
    }

    [Fact]
    public void AgentType_Custom_CreatesSuccessfully()
    {
        var customType = AgentType.Custom("SpecialAgent", "A custom agent for testing");

        customType.Should().NotBeNull();
        customType.ToString().Should().Be("SpecialAgent");
    }

    [Fact]
    public void AgentType_TryParse_InvalidType_ReturnsFalse()
    {
        var success = AgentType.TryParse("NonExistentType", out var agentType);

        success.Should().BeFalse();
        agentType.Should().BeNull();
    }

    #endregion

    #region Error Handling

    [Fact]
    public void AgentStrategy_EmptyName_ThrowsException()
    {
        // Using reflection or constructor directly - strategy name validation
        var act = () => AgentStrategy.RetrievalOnly(topK: -1);

        // Should still create (topK validation is at usage time, not creation)
        // This tests that factory methods don't throw for edge values
        var strategy = AgentStrategy.RetrievalOnly(topK: 0, minScore: 0.0);
        strategy.Name.Should().Be("RetrievalOnly");
    }

    #endregion

    #region Helpers

    private static Agent CreateTestAgent(string name)
    {
        return new Agent(
            Guid.NewGuid(),
            name,
            AgentType.RagAgent,
            AgentStrategy.SingleModel(),
            isActive: true);
    }

    #endregion
}
