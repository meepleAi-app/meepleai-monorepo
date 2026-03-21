using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for AgentDefinition aggregate (Issue #3808, Epic #3687)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Epic", "3687")]
public sealed class AgentDefinitionTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateAgentDefinition()
    {
        // Arrange
        var config = AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f);

        // Act
        var agent = AgentDefinition.Create("TestAgent", "Test description", AgentType.RagAgent, config);

        // Assert
        agent.Should().NotBeNull();
        agent.Id.Should().NotBe(Guid.Empty);
        agent.Name.Should().Be("TestAgent");
        agent.Description.Should().Be("Test description");
        agent.Config.Should().Be(config);
        agent.IsActive.Should().BeFalse();
        agent.Status.Should().Be(AgentDefinitionStatus.Draft);
        agent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        agent.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var config = AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f);

        // Act
        var act = () => AgentDefinition.Create("", "Description", AgentType.RagAgent, config);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowArgumentException()
    {
        // Arrange
        var config = AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f);
        var longName = new string('a', 101);

        // Act
        var act = () => AgentDefinition.Create(longName, "Description", AgentType.RagAgent, config);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => AgentDefinition.Create("Name", "Description", AgentType.RagAgent, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public void Create_WithPromptsAndTools_ShouldStoreCorrectly()
    {
        // Arrange
        var config = AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f);
        var prompts = new List<AgentPromptTemplate>
        {
            AgentPromptTemplate.Create("system", "You are a helpful assistant")
        };
        var tools = new List<AgentToolConfig>
        {
            AgentToolConfig.Create("web_search", new Dictionary<string, object> { ["max_results"] = 10 })
        };

        // Act
        var agent = AgentDefinition.Create("TestAgent", "Description", AgentType.RagAgent, config, null, prompts, tools);

        // Assert
        agent.Prompts.Should().HaveCount(1);
        agent.Prompts[0].Role.Should().Be("system");
        agent.Tools.Should().HaveCount(1);
        agent.Tools[0].Name.Should().Be("web_search");
    }

    [Fact]
    public void UpdateConfig_WithValidConfig_ShouldUpdateAndSetTimestamp()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Description", AgentType.RagAgent, AgentDefinitionConfig.Default());
        agent.UpdatedAt.Should().BeNull();
        var newConfig = AgentDefinitionConfig.Create("claude-3", 4096, 0.9f);

        // Act
        var beforeUpdate = DateTime.UtcNow;
        Thread.Sleep(10);
        agent.UpdateConfig(newConfig);

        // Assert
        agent.Config.Should().Be(newConfig);
        agent.Config.Model.Should().Be("claude-3");
        agent.Config.MaxTokens.Should().Be(4096);
        agent.UpdatedAt.Should().NotBeNull().And.BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void UpdateNameAndDescription_WithValidData_ShouldUpdate()
    {
        // Arrange
        var agent = AgentDefinition.Create("OriginalName", "Original desc", AgentType.RagAgent, AgentDefinitionConfig.Default());

        // Act
        agent.UpdateNameAndDescription("NewName", "New description");

        // Assert
        agent.Name.Should().Be("NewName");
        agent.Description.Should().Be("New description");
        agent.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePrompts_WithValidPrompts_ShouldUpdate()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var newPrompts = new List<AgentPromptTemplate>
        {
            AgentPromptTemplate.Create("system", "New system prompt"),
            AgentPromptTemplate.Create("user", "User template")
        };

        // Act
        agent.UpdatePrompts(newPrompts);

        // Assert
        agent.Prompts.Should().HaveCount(2);
        agent.Prompts[0].Role.Should().Be("system");
        agent.Prompts[1].Role.Should().Be("user");
        agent.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePrompts_WithMoreThan20Prompts_ShouldThrowArgumentException()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var tooManyPrompts = Enumerable.Range(0, 21)
            .Select(i => AgentPromptTemplate.Create("system", $"Prompt {i}"))
            .ToList();

        // Act
        var act = () => agent.UpdatePrompts(tooManyPrompts);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*20 prompt*");
    }

    [Fact]
    public void UpdateTools_WithValidTools_ShouldUpdate()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var newTools = new List<AgentToolConfig>
        {
            AgentToolConfig.Create("web_search"),
            AgentToolConfig.Create("calculator")
        };

        // Act
        agent.UpdateTools(newTools);

        // Assert
        agent.Tools.Should().HaveCount(2);
        agent.Tools[0].Name.Should().Be("web_search");
        agent.Tools[1].Name.Should().Be("calculator");
        agent.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateTools_WithMoreThan50Tools_ShouldThrowArgumentException()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var tooManyTools = Enumerable.Range(0, 51)
            .Select(i => AgentToolConfig.Create($"tool_{i}"))
            .ToList();

        // Act
        var act = () => agent.UpdateTools(tooManyTools);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*50 tools*");
    }

    [Fact]
    public void Activate_WhenInactive_ShouldActivate()
    {
        // Arrange — Create() now defaults to inactive, no need to deactivate first
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());

        // Act
        agent.Activate();

        // Assert
        agent.IsActive.Should().BeTrue();
        agent.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivate()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        agent.Activate(); // Create() now defaults to inactive, so activate first

        // Act
        agent.Deactivate();

        // Assert
        agent.IsActive.Should().BeFalse();
        agent.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldNotChangeState()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        agent.Activate(); // Create() defaults to inactive, activate first

        // Act — calling Activate() again should be a no-op
        agent.Activate();

        // Assert
        agent.IsActive.Should().BeTrue();
    }

    // ===== NEW TESTS FOR ISSUE #3708 =====

    [Fact]
    public void Create_WithAgentType_ShouldStoreTypeCorrectly()
    {
        // Arrange
        var type = AgentType.CitationAgent;
        var config = AgentDefinitionConfig.Default();

        // Act
        var agent = AgentDefinition.Create("CitationTest", "Description", type, config);

        // Assert
        agent.Type.Should().NotBeNull();
        agent.Type.Value.Should().Be("Citation");
        agent.Type.Description.Should().Contain("sources");
    }

    [Fact]
    public void Create_WithCustomAgentType_ShouldStoreCustomType()
    {
        // Arrange
        var type = AgentType.Custom("Decisore", "Strategic decision making agent");
        var config = AgentDefinitionConfig.Default();

        // Act
        var agent = AgentDefinition.Create("DecisoreAgent", "Desc", type, config);

        // Assert
        agent.Type.Value.Should().Be("Decisore");
        agent.Type.Description.Should().Be("Strategic decision making agent");
    }

    [Fact]
    public void Create_WithNullType_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = AgentDefinitionConfig.Default();

        // Act
        var act = () => AgentDefinition.Create("Name", "Desc", null!, config);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    [Fact]
    public void UpdateType_WithValidType_ShouldUpdateAndSetTimestamp()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var newType = AgentType.ConfidenceAgent;

        // Act
        var beforeUpdate = DateTime.UtcNow;
        Thread.Sleep(10);
        agent.UpdateType(newType);

        // Assert
        agent.Type.Value.Should().Be("Confidence");
        agent.UpdatedAt.Should().NotBeNull().And.BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void UpdateType_WithNullType_ShouldThrowArgumentNullException()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());

        // Act
        var act = () => agent.UpdateType(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    [Fact]
    public void Create_WithStrategy_ShouldStoreStrategyCorrectly()
    {
        // Arrange
        var type = AgentType.RagAgent;
        var config = AgentDefinitionConfig.Default();
        var strategy = AgentStrategy.VectorOnly(topK: 15, minScore: 0.85);

        // Act
        var agent = AgentDefinition.Create("TestAgent", "Desc", type, config, strategy);

        // Assert
        agent.Strategy.Should().NotBeNull();
        agent.Strategy.Name.Should().Be("VectorOnly");
        agent.Strategy.GetParameter<int>("TopK", 0).Should().Be(15);
        agent.Strategy.GetParameter<double>("MinScore", 0).Should().Be(0.85);
    }

    [Fact]
    public void Create_WithoutStrategy_ShouldUseDefaultHybridSearch()
    {
        // Arrange
        var type = AgentType.RagAgent;
        var config = AgentDefinitionConfig.Default();

        // Act
        var agent = AgentDefinition.Create("TestAgent", "Desc", type, config);

        // Assert
        agent.Strategy.Should().NotBeNull();
        agent.Strategy.Name.Should().Be("HybridSearch");
        agent.Strategy.HasParameter("VectorWeight").Should().BeTrue();
    }

    [Fact]
    public void UpdateStrategy_WithValidStrategy_ShouldUpdateAndSetTimestamp()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());
        var newStrategy = AgentStrategy.MultiModelConsensus(new[] { "gpt-4", "claude-3" }, 0.9);

        // Act
        var beforeUpdate = DateTime.UtcNow;
        Thread.Sleep(10);
        agent.UpdateStrategy(newStrategy);

        // Assert
        agent.Strategy.Name.Should().Be("MultiModelConsensus");
        agent.Strategy.GetParameter<double>("ConsensusThreshold", 0).Should().Be(0.9);
        agent.UpdatedAt.Should().NotBeNull().And.BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void UpdateStrategy_WithNullStrategy_ShouldThrowArgumentNullException()
    {
        // Arrange
        var agent = AgentDefinition.Create("TestAgent", "Desc", AgentType.RagAgent, AgentDefinitionConfig.Default());

        // Act
        var act = () => agent.UpdateStrategy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("strategy");
    }
}

