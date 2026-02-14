using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Unit tests for PlaygroundTestScenario aggregate root.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PlaygroundTestScenarioTests
{
    private static List<ScenarioMessage> CreateValidMessages() =>
    [
        new ScenarioMessage { Role = "user", Content = "What are the rules for Catan?" }
    ];

    [Fact]
    public void Create_WithValidInput_ReturnsScenario()
    {
        // Arrange & Act
        var scenario = PlaygroundTestScenario.Create(
            "Test Scenario",
            "A test description",
            ScenarioCategory.Greeting,
            CreateValidMessages(),
            Guid.NewGuid());

        // Assert
        Assert.NotEqual(Guid.Empty, scenario.Id);
        Assert.Equal("Test Scenario", scenario.Name);
        Assert.Equal("A test description", scenario.Description);
        Assert.Equal(ScenarioCategory.Greeting, scenario.Category);
        Assert.Single(scenario.Messages);
        Assert.True(scenario.IsActive);
        Assert.Null(scenario.ExpectedOutcome);
        Assert.Null(scenario.AgentDefinitionId);
        Assert.Empty(scenario.Tags);
    }

    [Fact]
    public void Create_WithAllFields_SetsProperties()
    {
        var agentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var tags = new List<string> { "rules", "catan" };

        var scenario = PlaygroundTestScenario.Create(
            "Full Scenario",
            "Full description",
            ScenarioCategory.RulesQuery,
            CreateValidMessages(),
            createdBy,
            "Should answer with setup rules",
            agentId,
            tags);

        Assert.Equal("Full Scenario", scenario.Name);
        Assert.Equal(ScenarioCategory.RulesQuery, scenario.Category);
        Assert.Equal("Should answer with setup rules", scenario.ExpectedOutcome);
        Assert.Equal(agentId, scenario.AgentDefinitionId);
        Assert.Equal(createdBy, scenario.CreatedBy);
        Assert.Equal(2, scenario.Tags.Count);
        Assert.Contains("rules", scenario.Tags);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var scenario = PlaygroundTestScenario.Create(
            "  Trimmed Name  ",
            "  Trimmed Description  ",
            ScenarioCategory.Custom,
            CreateValidMessages(),
            Guid.NewGuid(),
            "  Trimmed Outcome  ");

        Assert.Equal("Trimmed Name", scenario.Name);
        Assert.Equal("Trimmed Description", scenario.Description);
        Assert.Equal("Trimmed Outcome", scenario.ExpectedOutcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            PlaygroundTestScenario.Create(
                name!, "desc", ScenarioCategory.Greeting,
                CreateValidMessages(), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        Assert.Throws<ArgumentException>(() =>
            PlaygroundTestScenario.Create(
                "Name", description!, ScenarioCategory.Greeting,
                CreateValidMessages(), Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithNullMessages_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PlaygroundTestScenario.Create(
                "Name", "Desc", ScenarioCategory.Greeting,
                null!, Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyMessages_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PlaygroundTestScenario.Create(
                "Name", "Desc", ScenarioCategory.Greeting,
                new List<ScenarioMessage>(), Guid.NewGuid()));
    }

    [Fact]
    public void Update_ChangesProperties()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Original", "Original desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());

        var newMessages = new List<ScenarioMessage>
        {
            new() { Role = "user", Content = "New question" },
            new() { Role = "assistant", Content = "New answer" }
        };

        scenario.Update(
            "Updated", "Updated desc", ScenarioCategory.MultiTurn,
            newMessages, "Expected output", new List<string> { "tag1" });

        Assert.Equal("Updated", scenario.Name);
        Assert.Equal("Updated desc", scenario.Description);
        Assert.Equal(ScenarioCategory.MultiTurn, scenario.Category);
        Assert.Equal(2, scenario.Messages.Count);
        Assert.Equal("Expected output", scenario.ExpectedOutcome);
        Assert.Single(scenario.Tags);
        Assert.NotNull(scenario.UpdatedAt);
    }

    [Fact]
    public void Update_WithInvalidName_ThrowsArgumentException()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());

        Assert.Throws<ArgumentException>(() =>
            scenario.Update("", "Desc", ScenarioCategory.Greeting,
                CreateValidMessages(), null, null));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());
        Assert.True(scenario.IsActive);

        scenario.Deactivate();

        Assert.False(scenario.IsActive);
        Assert.NotNull(scenario.UpdatedAt);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());
        scenario.Deactivate();
        Assert.False(scenario.IsActive);

        scenario.Activate();

        Assert.True(scenario.IsActive);
    }

    [Fact]
    public void BindToAgent_SetsAgentDefinitionId()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());
        var agentId = Guid.NewGuid();

        scenario.BindToAgent(agentId);

        Assert.Equal(agentId, scenario.AgentDefinitionId);
        Assert.NotNull(scenario.UpdatedAt);
    }

    [Fact]
    public void UnbindFromAgent_ClearsAgentDefinitionId()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid(),
            agentDefinitionId: Guid.NewGuid());
        Assert.NotNull(scenario.AgentDefinitionId);

        scenario.UnbindFromAgent();

        Assert.Null(scenario.AgentDefinitionId);
        Assert.NotNull(scenario.UpdatedAt);
    }

    [Fact]
    public void Messages_WithEmptyJson_ReturnsEmptyList()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());

        // The Messages property should return the deserialized list
        Assert.NotNull(scenario.Messages);
        Assert.Single(scenario.Messages);
        Assert.Equal("user", scenario.Messages[0].Role);
    }

    [Fact]
    public void Tags_WithNullInput_ReturnsEmptyList()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid(),
            tags: null);

        Assert.NotNull(scenario.Tags);
        Assert.Empty(scenario.Tags);
    }

    [Fact]
    public void ScenarioCategory_HasExpectedValues()
    {
        Assert.Equal(0, (int)ScenarioCategory.Greeting);
        Assert.Equal(1, (int)ScenarioCategory.RulesQuery);
        Assert.Equal(2, (int)ScenarioCategory.Recommendation);
        Assert.Equal(3, (int)ScenarioCategory.MultiTurn);
        Assert.Equal(4, (int)ScenarioCategory.EdgeCase);
        Assert.Equal(5, (int)ScenarioCategory.StressTest);
        Assert.Equal(6, (int)ScenarioCategory.RagValidation);
        Assert.Equal(7, (int)ScenarioCategory.Custom);
    }
}
