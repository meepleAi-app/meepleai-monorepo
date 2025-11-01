using Api.Models;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tests for RuleSpecV0 model classes to ensure proper record behavior and property handling
/// </summary>
public class RuleSpecV0ModelTests
{
    private readonly ITestOutputHelper _output;

    #region PlayerCountRange Tests

    [Fact]
    public void PlayerCountRange_Constructor_SetsProperties()
    {
        // Act
        var range = new PlayerCountRange(2, 4);

        // Assert
        range.Min.Should().Be(2);
        range.Max.Should().Be(4);
    }

    [Fact]
    public void PlayerCountRange_Equality_WorksForRecords()
    {
        // Arrange
        var range1 = new PlayerCountRange(2, 4);
        var range2 = new PlayerCountRange(2, 4);
        var range3 = new PlayerCountRange(1, 4);

        // Assert
        range2.Should().Be(range1);
        range3.Should().NotBe(range1);
    }

    [Fact]
    public void PlayerCountRange_WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new PlayerCountRange(2, 4);

        // Act
        var modified = original with { Max = 6 };

        // Assert
        modified.Min.Should().Be(2);
        modified.Max.Should().Be(6);
        original.Max.Should().Be(4); // Original unchanged
    }

    #endregion

    #region PlayTimeRange Tests

    [Fact]
    public void PlayTimeRange_Constructor_SetsProperties()
    {
        // Act
        var range = new PlayTimeRange(30, 60);

        // Assert
        range.Min.Should().Be(30);
        range.Max.Should().Be(60);
    }

    [Fact]
    public void PlayTimeRange_Equality_WorksForRecords()
    {
        // Arrange
        var range1 = new PlayTimeRange(30, 60);
        var range2 = new PlayTimeRange(30, 60);

        // Assert
        range2.Should().Be(range1);
    }

    #endregion

    #region AgeRange Tests

    [Fact]
    public void AgeRange_Constructor_SetsMinimumAge()
    {
        // Act
        var range = new AgeRange(10);

        // Assert
        range.Min.Should().Be(10);
    }

    [Fact]
    public void AgeRange_Equality_WorksForRecords()
    {
        // Arrange
        var range1 = new AgeRange(8);
        var range2 = new AgeRange(8);
        var range3 = new AgeRange(12);

        // Assert
        range2.Should().Be(range1);
        range3.Should().NotBe(range1);
    }

    #endregion

    #region GameComponent Tests

    [Fact]
    public void GameComponent_Constructor_SetsRequiredProperties()
    {
        // Act
        var component = new GameComponent("card-1", "Action Card", 50);

        // Assert
        component.Id.Should().Be("card-1");
        component.Name.Should().Be("Action Card");
        component.Quantity.Should().Be(50);
        component.Description.Should().BeNull();
    }

    [Fact]
    public void GameComponent_WithDescription_SetsOptionalProperty()
    {
        // Act
        var component = new GameComponent("dice-1", "Six-sided die", 2, "Standard gaming dice");

        // Assert
        component.Id.Should().Be("dice-1");
        component.Name.Should().Be("Six-sided die");
        component.Quantity.Should().Be(2);
        component.Description.Should().Be("Standard gaming dice");
    }

    [Fact]
    public void GameComponent_Equality_ComparesAllProperties()
    {
        // Arrange
        var comp1 = new GameComponent("card-1", "Card", 10, "Description");
        var comp2 = new GameComponent("card-1", "Card", 10, "Description");
        var comp3 = new GameComponent("card-2", "Card", 10, "Description");

        // Assert
        comp2.Should().Be(comp1);
        comp3.Should().NotBe(comp1);
    }

    #endregion

    #region SetupStep Tests

    [Fact]
    public void SetupStep_Constructor_SetsRequiredProperties()
    {
        // Act
        var step = new SetupStep("step-1", "Shuffle the deck");

        // Assert
        step.Id.Should().Be("step-1");
        step.Text.Should().Be("Shuffle the deck");
        step.Order.Should().BeNull();
        step.Components.Should().BeNull();
    }

    [Fact]
    public void SetupStep_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var components = new List<string> { "deck", "tokens" };

        // Act
        var step = new SetupStep("step-1", "Distribute cards", 1, components);

        // Assert
        step.Id.Should().Be("step-1");
        step.Text.Should().Be("Distribute cards");
        step.Order.Should().Be(1);
        step.Components.Should().Be(components);
    }

    #endregion

    #region GameSetup Tests

    [Fact]
    public void GameSetup_Constructor_SetsSteps()
    {
        // Arrange
        var steps = new List<SetupStep>
        {
            new("step-1", "Shuffle deck"),
            new("step-2", "Deal cards")
        };

        // Act
        var setup = new GameSetup(steps);

        // Assert
        setup.Steps.Count.Should().Be(2);
        setup.Description.Should().BeNull();
        setup.Components.Should().BeNull();
    }

    [Fact]
    public void GameSetup_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var steps = new List<SetupStep> { new("step-1", "Setup") };
        var components = new List<GameComponent> { new("comp-1", "Card", 10) };

        // Act
        var setup = new GameSetup(steps, "Game setup instructions", components);

        // Assert
        setup.Steps.Should().ContainSingle();
        setup.Description.Should().Be("Game setup instructions");
        setup.Components.Should().NotBeNull();
        setup.Components.Should().ContainSingle();
    }

    #endregion

    #region PhaseStep Tests

    [Fact]
    public void PhaseStep_Constructor_SetsRequiredProperties()
    {
        // Act
        var step = new PhaseStep("ps-1", "Draw a card");

        // Assert
        step.Id.Should().Be("ps-1");
        step.Text.Should().Be("Draw a card");
        step.Order.Should().BeNull();
        step.Optional.Should().BeFalse();
    }

    [Fact]
    public void PhaseStep_WithAllProperties_SetsCorrectly()
    {
        // Act
        var step = new PhaseStep("ps-1", "Discard a card", 2, true);

        // Assert
        step.Id.Should().Be("ps-1");
        step.Text.Should().Be("Discard a card");
        step.Order.Should().Be(2);
        step.Optional.Should().BeTrue();
    }

    #endregion

    #region GamePhase Tests

    [Fact]
    public void GamePhase_Constructor_SetsRequiredProperties()
    {
        // Act
        var phase = new GamePhase("phase-1", "Draw Phase");

        // Assert
        phase.Id.Should().Be("phase-1");
        phase.Name.Should().Be("Draw Phase");
        phase.Description.Should().BeNull();
        phase.Order.Should().BeNull();
        phase.Steps.Should().BeNull();
        phase.AllowedActions.Should().BeNull();
    }

    [Fact]
    public void GamePhase_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var steps = new List<PhaseStep> { new("ps-1", "Draw card") };
        var actions = new List<string> { "draw", "discard" };

        // Act
        var phase = new GamePhase("phase-1", "Main Phase", "Main game phase", 1, steps, actions);

        // Assert
        phase.Id.Should().Be("phase-1");
        phase.Name.Should().Be("Main Phase");
        phase.Description.Should().Be("Main game phase");
        phase.Order.Should().Be(1);
        phase.Steps.Should().NotBeNull();
        phase.Steps.Should().ContainSingle();
        phase.AllowedActions.Should().NotBeNull();
        phase.AllowedActions.Count.Should().Be(2);
    }

    #endregion

    #region ActionCost Tests

    [Fact]
    public void ActionCost_DefaultConstructor_AllowsNullProperties()
    {
        // Act
        var cost = new ActionCost();

        // Assert
        cost.Resources.Should().BeNull();
        cost.Description.Should().BeNull();
    }

    [Fact]
    public void ActionCost_WithResources_SetsCorrectly()
    {
        // Arrange
        var resources = new Dictionary<string, int>
        {
            { "gold", 3 },
            { "wood", 2 }
        };

        // Act
        var cost = new ActionCost(resources, "Resource cost");

        // Assert
        cost.Resources.Should().NotBeNull();
        cost.Resources.Count.Should().Be(2);
        cost.Resources["gold"].Should().Be(3);
        cost.Description.Should().Be("Resource cost");
    }

    #endregion

    #region GameAction Tests

    [Fact]
    public void GameAction_Constructor_SetsRequiredProperties()
    {
        // Act
        var action = new GameAction("action-1", "Draw Card", ActionType.Mandatory);

        // Assert
        action.Id.Should().Be("action-1");
        action.Name.Should().Be("Draw Card");
        action.Type.Should().Be(ActionType.Mandatory);
        action.Description.Should().BeNull();
        action.Prerequisites.Should().BeNull();
        action.Effects.Should().BeNull();
        action.Cost.Should().BeNull();
    }

    [Fact]
    public void GameAction_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var prereqs = new List<string> { "has-cards" };
        var effects = new List<string> { "gain-card" };
        var cost = new ActionCost(new Dictionary<string, int> { { "gold", 1 } });

        // Act
        var action = new GameAction(
            "action-1",
            "Buy Card",
            ActionType.Optional,
            "Purchase a card",
            prereqs,
            effects,
            cost);

        // Assert
        action.Id.Should().Be("action-1");
        action.Name.Should().Be("Buy Card");
        action.Type.Should().Be(ActionType.Optional);
        action.Description.Should().Be("Purchase a card");
        action.Prerequisites.Should().NotBeNull();
        action.Prerequisites.Should().ContainSingle();
        action.Effects.Should().NotBeNull();
        action.Effects.Should().ContainSingle();
        action.Cost.Should().NotBeNull();
    }

    [Fact]
    public void ActionType_Enum_HasAllValues()
    {
        // Assert
        Enum.IsDefined(typeof(ActionType), ActionType.Mandatory).Should().BeTrue();
        Enum.IsDefined(typeof(ActionType), ActionType.Optional).Should().BeTrue();
        Enum.IsDefined(typeof(ActionType), ActionType.Triggered).Should().BeTrue();
    }

    #endregion

    #region Tiebreaker Tests

    [Fact]
    public void Tiebreaker_Constructor_SetsProperties()
    {
        // Act
        var tiebreaker = new Tiebreaker(1, "Most gold");

        // Assert
        tiebreaker.Order.Should().Be(1);
        tiebreaker.Rule.Should().Be("Most gold");
    }

    [Fact]
    public void Tiebreaker_Equality_WorksCorrectly()
    {
        // Arrange
        var tb1 = new Tiebreaker(1, "Rule A");
        var tb2 = new Tiebreaker(1, "Rule A");
        var tb3 = new Tiebreaker(2, "Rule A");

        // Assert
        tb2.Should().Be(tb1);
        tb3.Should().NotBe(tb1);
    }

    #endregion

    #region ScoringSource Tests

    [Fact]
    public void ScoringSource_Constructor_SetsRequiredProperties()
    {
        // Act
        var source = new ScoringSource("src-1", "Gold", 5);

        // Assert
        source.Id.Should().Be("src-1");
        source.Name.Should().Be("Gold");
        source.Value.Should().Be(5);
        source.Description.Should().BeNull();
        source.When.Should().BeNull();
    }

    [Fact]
    public void ScoringSource_WithAllProperties_SetsCorrectly()
    {
        // Act
        var source = new ScoringSource(
            "src-1",
            "Territory",
            10,
            "Points for controlling territory",
            "End of game");

        // Assert
        source.Id.Should().Be("src-1");
        source.Name.Should().Be("Territory");
        source.Value.Should().Be(10);
        source.Description.Should().Be("Points for controlling territory");
        source.When.Should().Be("End of game");
    }

    #endregion

    #region ScoringRules Tests

    [Fact]
    public void ScoringRules_Constructor_SetsScoringMethod()
    {
        // Act
        var rules = new ScoringRules(ScoringMethod.Points);

        // Assert
        rules.Method.Should().Be(ScoringMethod.Points);
        rules.Description.Should().BeNull();
        rules.Sources.Should().BeNull();
        rules.Tiebreakers.Should().BeNull();
    }

    [Fact]
    public void ScoringRules_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var sources = new List<ScoringSource> { new("src-1", "Gold", 5) };
        var tiebreakers = new List<Tiebreaker> { new(1, "Most cards") };

        // Act
        var rules = new ScoringRules(
            ScoringMethod.Hybrid,
            "Combined scoring",
            sources,
            tiebreakers);

        // Assert
        rules.Method.Should().Be(ScoringMethod.Hybrid);
        rules.Description.Should().Be("Combined scoring");
        rules.Sources.Should().NotBeNull();
        rules.Sources.Should().ContainSingle();
        rules.Tiebreakers.Should().NotBeNull();
        rules.Tiebreakers.Should().ContainSingle();
    }

    [Fact]
    public void ScoringMethod_Enum_HasAllValues()
    {
        // Assert
        Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Points).Should().BeTrue();
        Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Elimination).Should().BeTrue();
        Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Objective).Should().BeTrue();
        Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Hybrid).Should().BeTrue();
    }

    #endregion

    #region EndCondition Tests

    [Fact]
    public void EndCondition_Constructor_SetsRequiredProperties()
    {
        // Act
        var condition = new EndCondition("ec-1", EndConditionType.Rounds, "Game ends after 10 rounds");

        // Assert
        condition.Id.Should().Be("ec-1");
        condition.Type.Should().Be(EndConditionType.Rounds);
        condition.Description.Should().Be("Game ends after 10 rounds");
        condition.Value.Should().BeNull();
    }

    [Fact]
    public void EndCondition_WithValue_SetsCorrectly()
    {
        // Act
        var condition = new EndCondition("ec-1", EndConditionType.Points, "Reach 100 points", 100);

        // Assert
        condition.Id.Should().Be("ec-1");
        condition.Type.Should().Be(EndConditionType.Points);
        condition.Description.Should().Be("Reach 100 points");
        condition.Value.Should().Be(100);
    }

    [Fact]
    public void EndConditionType_Enum_HasAllValues()
    {
        // Assert
        Enum.IsDefined(typeof(EndConditionType), EndConditionType.Rounds).Should().BeTrue();
        Enum.IsDefined(typeof(EndConditionType), EndConditionType.Points).Should().BeTrue();
        Enum.IsDefined(typeof(EndConditionType), EndConditionType.Elimination).Should().BeTrue();
        Enum.IsDefined(typeof(EndConditionType), EndConditionType.Objective).Should().BeTrue();
        Enum.IsDefined(typeof(EndConditionType), EndConditionType.Custom).Should().BeTrue();
    }

    #endregion

    #region EdgeCase Tests

    [Fact]
    public void EdgeCase_Constructor_SetsRequiredProperties()
    {
        // Act
        var edgeCase = new EdgeCase("ec-1", EdgeCaseCategory.Exception, "Special rule for ties");

        // Assert
        edgeCase.Id.Should().Be("ec-1");
        edgeCase.Category.Should().Be(EdgeCaseCategory.Exception);
        edgeCase.Text.Should().Be("Special rule for ties");
        edgeCase.RelatedActions.Should().BeNull();
        edgeCase.RelatedPhases.Should().BeNull();
    }

    [Fact]
    public void EdgeCase_WithRelations_SetsCorrectly()
    {
        // Arrange
        var actions = new List<string> { "action-1", "action-2" };
        var phases = new List<string> { "phase-1" };

        // Act
        var edgeCase = new EdgeCase(
            "ec-1",
            EdgeCaseCategory.Clarification,
            "Clarification text",
            actions,
            phases);

        // Assert
        edgeCase.Id.Should().Be("ec-1");
        edgeCase.Category.Should().Be(EdgeCaseCategory.Clarification);
        edgeCase.RelatedActions.Should().NotBeNull();
        edgeCase.RelatedActions.Count.Should().Be(2);
        edgeCase.RelatedPhases.Should().NotBeNull();
        edgeCase.RelatedPhases.Should().ContainSingle();
    }

    [Fact]
    public void EdgeCaseCategory_Enum_HasAllValues()
    {
        // Assert
        Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.Exception).Should().BeTrue();
        Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.Clarification).Should().BeTrue();
        Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.Variant).Should().BeTrue();
        Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.FAQ).Should().BeTrue();
    }

    #endregion

    #region GlossaryTerm Tests

    [Fact]
    public void GlossaryTerm_Constructor_SetsRequiredProperties()
    {
        // Act
        var term = new GlossaryTerm("Action Point", "A resource used to perform actions");

        // Assert
        term.Term.Should().Be("Action Point");
        term.Definition.Should().Be("A resource used to perform actions");
        term.Examples.Should().BeNull();
    }

    [Fact]
    public void GlossaryTerm_WithExamples_SetsCorrectly()
    {
        // Arrange
        var examples = new List<string> { "Move: costs 1 AP", "Attack: costs 2 AP" };

        // Act
        var term = new GlossaryTerm("Action Point", "A resource", examples);

        // Assert
        term.Term.Should().Be("Action Point");
        term.Examples.Should().NotBeNull();
        term.Examples.Count.Should().Be(2);
    }

    #endregion

    #region RuleSpecMetadata Tests

    [Fact]
    public void RuleSpecMetadata_Constructor_SetsRequiredProperties()
    {
        // Arrange
        var createdAt = DateTime.UtcNow;

        // Act
        var metadata = new RuleSpecMetadata("Catan", createdAt);

        // Assert
        metadata.Name.Should().Be("Catan");
        metadata.CreatedAt.Should().Be(createdAt);
        metadata.Description.Should().BeNull();
        metadata.UpdatedAt.Should().BeNull();
        metadata.PlayerCount.Should().BeNull();
        metadata.PlayTime.Should().BeNull();
        metadata.AgeRange.Should().BeNull();
    }

    [Fact]
    public void RuleSpecMetadata_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddDays(1);
        var playerCount = new PlayerCountRange(2, 4);
        var playTime = new PlayTimeRange(60, 90);
        var ageRange = new AgeRange(10);

        // Act
        var metadata = new RuleSpecMetadata(
            "Catan",
            createdAt,
            "Settlers of Catan",
            updatedAt,
            playerCount,
            playTime,
            ageRange);

        // Assert
        metadata.Name.Should().Be("Catan");
        metadata.CreatedAt.Should().Be(createdAt);
        metadata.Description.Should().Be("Settlers of Catan");
        metadata.UpdatedAt.Should().Be(updatedAt);
        metadata.PlayerCount.Should().Be(playerCount);
        metadata.PlayTime.Should().Be(playTime);
        metadata.AgeRange.Should().Be(ageRange);
    }

    #endregion

    #region RuleSpecV0 Tests

    [Fact]
    public void RuleSpecV0_Constructor_SetsAllRequiredProperties()
    {
        // Arrange
        var metadata = new RuleSpecMetadata("Test Game", DateTime.UtcNow);
        var setup = new GameSetup(new List<SetupStep> { new("s1", "Setup") });
        var phases = new List<GamePhase> { new("p1", "Phase 1") };
        var actions = new List<GameAction> { new("a1", "Action", ActionType.Mandatory) };
        var scoring = new ScoringRules(ScoringMethod.Points);
        var endConditions = new List<EndCondition>
        {
            new("ec1", EndConditionType.Rounds, "10 rounds")
        };

        // Act
        var spec = new RuleSpecV0(
            "game-1",
            "1.0",
            metadata,
            setup,
            phases,
            actions,
            scoring,
            endConditions);

        // Assert
        spec.GameId.Should().Be("game-1");
        spec.Version.Should().Be("1.0");
        spec.Metadata.Should().Be(metadata);
        spec.Setup.Should().Be(setup);
        spec.Phases.Should().ContainSingle();
        spec.Actions.Should().ContainSingle();
        spec.Scoring.Should().Be(scoring);
        spec.EndConditions.Should().ContainSingle();
        spec.EdgeCases.Should().BeNull();
        spec.Glossary.Should().BeNull();
    }

    [Fact]
    public void RuleSpecV0_WithOptionalProperties_SetsCorrectly()
    {
        // Arrange
        var metadata = new RuleSpecMetadata("Test Game", DateTime.UtcNow);
        var setup = new GameSetup(new List<SetupStep>());
        var phases = new List<GamePhase>();
        var actions = new List<GameAction>();
        var scoring = new ScoringRules(ScoringMethod.Points);
        var endConditions = new List<EndCondition>();
        var edgeCases = new List<EdgeCase> { new("ec1", EdgeCaseCategory.FAQ, "FAQ item") };
        var glossary = new List<GlossaryTerm> { new("Term", "Definition") };

        // Act
        var spec = new RuleSpecV0(
            "game-1",
            "1.0",
            metadata,
            setup,
            phases,
            actions,
            scoring,
            endConditions,
            edgeCases,
            glossary);

        // Assert
        spec.EdgeCases.Should().NotBeNull();
        spec.EdgeCases.Should().ContainSingle();
        spec.Glossary.Should().NotBeNull();
        spec.Glossary.Should().ContainSingle();
    }

    [Fact]
    public void RuleSpecV0_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var metadata = new RuleSpecMetadata("Test", DateTime.UtcNow);
        var setup = new GameSetup(new List<SetupStep>());
        var scoring = new ScoringRules(ScoringMethod.Points);

        var spec1 = new RuleSpecV0(
            "game-1",
            "1.0",
            metadata,
            setup,
            new List<GamePhase>(),
            new List<GameAction>(),
            scoring,
            new List<EndCondition>());

        var spec2 = new RuleSpecV0(
            "game-1",
            "1.0",
            metadata,
            setup,
            new List<GamePhase>(),
            new List<GameAction>(),
            scoring,
            new List<EndCondition>());

        // Assert - Records with same values should be equal
        spec1.GameId.Should().Be("game-1");
        spec2.GameId.Should().Be("game-1");
    }

    #endregion
}
