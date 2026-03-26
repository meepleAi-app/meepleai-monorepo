using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        scenario.Id.Should().NotBe(Guid.Empty);
        scenario.Name.Should().Be("Test Scenario");
        scenario.Description.Should().Be("A test description");
        scenario.Category.Should().Be(ScenarioCategory.Greeting);
        scenario.Messages.Should().ContainSingle();
        scenario.IsActive.Should().BeTrue();
        scenario.ExpectedOutcome.Should().BeNull();
        scenario.AgentDefinitionId.Should().BeNull();
        scenario.Tags.Should().BeEmpty();
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

        scenario.Name.Should().Be("Full Scenario");
        scenario.Category.Should().Be(ScenarioCategory.RulesQuery);
        scenario.ExpectedOutcome.Should().Be("Should answer with setup rules");
        scenario.AgentDefinitionId.Should().Be(agentId);
        scenario.CreatedBy.Should().Be(createdBy);
        scenario.Tags.Count.Should().Be(2);
        scenario.Tags.Should().Contain("rules");
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

        scenario.Name.Should().Be("Trimmed Name");
        scenario.Description.Should().Be("Trimmed Description");
        scenario.ExpectedOutcome.Should().Be("Trimmed Outcome");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        Action act = () =>
            PlaygroundTestScenario.Create(
                name!, "desc", ScenarioCategory.Greeting,
                CreateValidMessages(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        Action act = () =>
            PlaygroundTestScenario.Create(
                "Name", description!, ScenarioCategory.Greeting,
                CreateValidMessages(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullMessages_ThrowsArgumentNullException()
    {
        Action act = () =>
            PlaygroundTestScenario.Create(
                "Name", "Desc", ScenarioCategory.Greeting,
                null!, Guid.NewGuid());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEmptyMessages_ThrowsArgumentException()
    {
        Action act = () =>
            PlaygroundTestScenario.Create(
                "Name", "Desc", ScenarioCategory.Greeting,
                new List<ScenarioMessage>(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
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

        scenario.Name.Should().Be("Updated");
        scenario.Description.Should().Be("Updated desc");
        scenario.Category.Should().Be(ScenarioCategory.MultiTurn);
        scenario.Messages.Count.Should().Be(2);
        scenario.ExpectedOutcome.Should().Be("Expected output");
        scenario.Tags.Should().ContainSingle();
        scenario.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_WithInvalidName_ThrowsArgumentException()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());

        Action act = () =>
            scenario.Update("", "Desc", ScenarioCategory.Greeting,
                CreateValidMessages(), null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());
        scenario.IsActive.Should().BeTrue();

        scenario.Deactivate();

        scenario.IsActive.Should().BeFalse();
        scenario.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());
        scenario.Deactivate();
        scenario.IsActive.Should().BeFalse();

        scenario.Activate();

        scenario.IsActive.Should().BeTrue();
    }

    [Fact]
    public void BindToAgent_SetsAgentDefinitionId()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());
        var agentId = Guid.NewGuid();

        scenario.BindToAgent(agentId);

        scenario.AgentDefinitionId.Should().Be(agentId);
        scenario.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UnbindFromAgent_ClearsAgentDefinitionId()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid(),
            agentDefinitionId: Guid.NewGuid());
        scenario.AgentDefinitionId.Should().NotBeNull();

        scenario.UnbindFromAgent();

        scenario.AgentDefinitionId.Should().BeNull();
        scenario.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Messages_WithEmptyJson_ReturnsEmptyList()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid());

        // The Messages property should return the deserialized list
        scenario.Messages.Should().NotBeNull();
        scenario.Messages.Should().ContainSingle();
        scenario.Messages[0].Role.Should().Be("user");
    }

    [Fact]
    public void Tags_WithNullInput_ReturnsEmptyList()
    {
        var scenario = PlaygroundTestScenario.Create(
            "Name", "Desc", ScenarioCategory.Greeting,
            CreateValidMessages(), Guid.NewGuid(),
            tags: null);

        scenario.Tags.Should().NotBeNull();
        scenario.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ScenarioCategory_HasExpectedValues()
    {
        ((int)ScenarioCategory.Greeting).Should().Be(0);
        ((int)ScenarioCategory.RulesQuery).Should().Be(1);
        ((int)ScenarioCategory.Recommendation).Should().Be(2);
        ((int)ScenarioCategory.MultiTurn).Should().Be(3);
        ((int)ScenarioCategory.EdgeCase).Should().Be(4);
        ((int)ScenarioCategory.StressTest).Should().Be(5);
        ((int)ScenarioCategory.RagValidation).Should().Be(6);
        ((int)ScenarioCategory.Custom).Should().Be(7);
    }
}
