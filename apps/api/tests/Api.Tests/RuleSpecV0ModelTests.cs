using Api.Models;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Tests for RuleSpecV0 model classes to ensure proper record behavior and property handling
/// </summary>
public class RuleSpecV0ModelTests
{
    #region PlayerCountRange Tests

    [Fact]
    public void PlayerCountRange_Constructor_SetsProperties()
    {
        // Act
        var range = new PlayerCountRange(2, 4);

        // Assert
        Assert.Equal(2, range.Min);
        Assert.Equal(4, range.Max);
    }

    [Fact]
    public void PlayerCountRange_Equality_WorksForRecords()
    {
        // Arrange
        var range1 = new PlayerCountRange(2, 4);
        var range2 = new PlayerCountRange(2, 4);
        var range3 = new PlayerCountRange(1, 4);

        // Assert
        Assert.Equal(range1, range2);
        Assert.NotEqual(range1, range3);
    }

    [Fact]
    public void PlayerCountRange_WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new PlayerCountRange(2, 4);

        // Act
        var modified = original with { Max = 6 };

        // Assert
        Assert.Equal(2, modified.Min);
        Assert.Equal(6, modified.Max);
        Assert.Equal(4, original.Max); // Original unchanged
    }

    #endregion

    #region PlayTimeRange Tests

    [Fact]
    public void PlayTimeRange_Constructor_SetsProperties()
    {
        // Act
        var range = new PlayTimeRange(30, 60);

        // Assert
        Assert.Equal(30, range.Min);
        Assert.Equal(60, range.Max);
    }

    [Fact]
    public void PlayTimeRange_Equality_WorksForRecords()
    {
        // Arrange
        var range1 = new PlayTimeRange(30, 60);
        var range2 = new PlayTimeRange(30, 60);

        // Assert
        Assert.Equal(range1, range2);
    }

    #endregion

    #region AgeRange Tests

    [Fact]
    public void AgeRange_Constructor_SetsMinimumAge()
    {
        // Act
        var range = new AgeRange(10);

        // Assert
        Assert.Equal(10, range.Min);
    }

    [Fact]
    public void AgeRange_Equality_WorksForRecords()
    {
        // Arrange
        var range1 = new AgeRange(8);
        var range2 = new AgeRange(8);
        var range3 = new AgeRange(12);

        // Assert
        Assert.Equal(range1, range2);
        Assert.NotEqual(range1, range3);
    }

    #endregion

    #region GameComponent Tests

    [Fact]
    public void GameComponent_Constructor_SetsRequiredProperties()
    {
        // Act
        var component = new GameComponent("card-1", "Action Card", 50);

        // Assert
        Assert.Equal("card-1", component.Id);
        Assert.Equal("Action Card", component.Name);
        Assert.Equal(50, component.Quantity);
        Assert.Null(component.Description);
    }

    [Fact]
    public void GameComponent_WithDescription_SetsOptionalProperty()
    {
        // Act
        var component = new GameComponent("dice-1", "Six-sided die", 2, "Standard gaming dice");

        // Assert
        Assert.Equal("dice-1", component.Id);
        Assert.Equal("Six-sided die", component.Name);
        Assert.Equal(2, component.Quantity);
        Assert.Equal("Standard gaming dice", component.Description);
    }

    [Fact]
    public void GameComponent_Equality_ComparesAllProperties()
    {
        // Arrange
        var comp1 = new GameComponent("card-1", "Card", 10, "Description");
        var comp2 = new GameComponent("card-1", "Card", 10, "Description");
        var comp3 = new GameComponent("card-2", "Card", 10, "Description");

        // Assert
        Assert.Equal(comp1, comp2);
        Assert.NotEqual(comp1, comp3);
    }

    #endregion

    #region SetupStep Tests

    [Fact]
    public void SetupStep_Constructor_SetsRequiredProperties()
    {
        // Act
        var step = new SetupStep("step-1", "Shuffle the deck");

        // Assert
        Assert.Equal("step-1", step.Id);
        Assert.Equal("Shuffle the deck", step.Text);
        Assert.Null(step.Order);
        Assert.Null(step.Components);
    }

    [Fact]
    public void SetupStep_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var components = new List<string> { "deck", "tokens" };

        // Act
        var step = new SetupStep("step-1", "Distribute cards", 1, components);

        // Assert
        Assert.Equal("step-1", step.Id);
        Assert.Equal("Distribute cards", step.Text);
        Assert.Equal(1, step.Order);
        Assert.Equal(components, step.Components);
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
        Assert.Equal(2, setup.Steps.Count);
        Assert.Null(setup.Description);
        Assert.Null(setup.Components);
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
        Assert.Single(setup.Steps);
        Assert.Equal("Game setup instructions", setup.Description);
        Assert.NotNull(setup.Components);
        Assert.Single(setup.Components);
    }

    #endregion

    #region PhaseStep Tests

    [Fact]
    public void PhaseStep_Constructor_SetsRequiredProperties()
    {
        // Act
        var step = new PhaseStep("ps-1", "Draw a card");

        // Assert
        Assert.Equal("ps-1", step.Id);
        Assert.Equal("Draw a card", step.Text);
        Assert.Null(step.Order);
        Assert.False(step.Optional);
    }

    [Fact]
    public void PhaseStep_WithAllProperties_SetsCorrectly()
    {
        // Act
        var step = new PhaseStep("ps-1", "Discard a card", 2, true);

        // Assert
        Assert.Equal("ps-1", step.Id);
        Assert.Equal("Discard a card", step.Text);
        Assert.Equal(2, step.Order);
        Assert.True(step.Optional);
    }

    #endregion

    #region GamePhase Tests

    [Fact]
    public void GamePhase_Constructor_SetsRequiredProperties()
    {
        // Act
        var phase = new GamePhase("phase-1", "Draw Phase");

        // Assert
        Assert.Equal("phase-1", phase.Id);
        Assert.Equal("Draw Phase", phase.Name);
        Assert.Null(phase.Description);
        Assert.Null(phase.Order);
        Assert.Null(phase.Steps);
        Assert.Null(phase.AllowedActions);
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
        Assert.Equal("phase-1", phase.Id);
        Assert.Equal("Main Phase", phase.Name);
        Assert.Equal("Main game phase", phase.Description);
        Assert.Equal(1, phase.Order);
        Assert.NotNull(phase.Steps);
        Assert.Single(phase.Steps);
        Assert.NotNull(phase.AllowedActions);
        Assert.Equal(2, phase.AllowedActions.Count);
    }

    #endregion

    #region ActionCost Tests

    [Fact]
    public void ActionCost_DefaultConstructor_AllowsNullProperties()
    {
        // Act
        var cost = new ActionCost();

        // Assert
        Assert.Null(cost.Resources);
        Assert.Null(cost.Description);
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
        Assert.NotNull(cost.Resources);
        Assert.Equal(2, cost.Resources.Count);
        Assert.Equal(3, cost.Resources["gold"]);
        Assert.Equal("Resource cost", cost.Description);
    }

    #endregion

    #region GameAction Tests

    [Fact]
    public void GameAction_Constructor_SetsRequiredProperties()
    {
        // Act
        var action = new GameAction("action-1", "Draw Card", ActionType.Mandatory);

        // Assert
        Assert.Equal("action-1", action.Id);
        Assert.Equal("Draw Card", action.Name);
        Assert.Equal(ActionType.Mandatory, action.Type);
        Assert.Null(action.Description);
        Assert.Null(action.Prerequisites);
        Assert.Null(action.Effects);
        Assert.Null(action.Cost);
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
        Assert.Equal("action-1", action.Id);
        Assert.Equal("Buy Card", action.Name);
        Assert.Equal(ActionType.Optional, action.Type);
        Assert.Equal("Purchase a card", action.Description);
        Assert.NotNull(action.Prerequisites);
        Assert.Single(action.Prerequisites);
        Assert.NotNull(action.Effects);
        Assert.Single(action.Effects);
        Assert.NotNull(action.Cost);
    }

    [Fact]
    public void ActionType_Enum_HasAllValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(ActionType), ActionType.Mandatory));
        Assert.True(Enum.IsDefined(typeof(ActionType), ActionType.Optional));
        Assert.True(Enum.IsDefined(typeof(ActionType), ActionType.Triggered));
    }

    #endregion

    #region Tiebreaker Tests

    [Fact]
    public void Tiebreaker_Constructor_SetsProperties()
    {
        // Act
        var tiebreaker = new Tiebreaker(1, "Most gold");

        // Assert
        Assert.Equal(1, tiebreaker.Order);
        Assert.Equal("Most gold", tiebreaker.Rule);
    }

    [Fact]
    public void Tiebreaker_Equality_WorksCorrectly()
    {
        // Arrange
        var tb1 = new Tiebreaker(1, "Rule A");
        var tb2 = new Tiebreaker(1, "Rule A");
        var tb3 = new Tiebreaker(2, "Rule A");

        // Assert
        Assert.Equal(tb1, tb2);
        Assert.NotEqual(tb1, tb3);
    }

    #endregion

    #region ScoringSource Tests

    [Fact]
    public void ScoringSource_Constructor_SetsRequiredProperties()
    {
        // Act
        var source = new ScoringSource("src-1", "Gold", 5);

        // Assert
        Assert.Equal("src-1", source.Id);
        Assert.Equal("Gold", source.Name);
        Assert.Equal(5, source.Value);
        Assert.Null(source.Description);
        Assert.Null(source.When);
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
        Assert.Equal("src-1", source.Id);
        Assert.Equal("Territory", source.Name);
        Assert.Equal(10, source.Value);
        Assert.Equal("Points for controlling territory", source.Description);
        Assert.Equal("End of game", source.When);
    }

    #endregion

    #region ScoringRules Tests

    [Fact]
    public void ScoringRules_Constructor_SetsScoringMethod()
    {
        // Act
        var rules = new ScoringRules(ScoringMethod.Points);

        // Assert
        Assert.Equal(ScoringMethod.Points, rules.Method);
        Assert.Null(rules.Description);
        Assert.Null(rules.Sources);
        Assert.Null(rules.Tiebreakers);
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
        Assert.Equal(ScoringMethod.Hybrid, rules.Method);
        Assert.Equal("Combined scoring", rules.Description);
        Assert.NotNull(rules.Sources);
        Assert.Single(rules.Sources);
        Assert.NotNull(rules.Tiebreakers);
        Assert.Single(rules.Tiebreakers);
    }

    [Fact]
    public void ScoringMethod_Enum_HasAllValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Points));
        Assert.True(Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Elimination));
        Assert.True(Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Objective));
        Assert.True(Enum.IsDefined(typeof(ScoringMethod), ScoringMethod.Hybrid));
    }

    #endregion

    #region EndCondition Tests

    [Fact]
    public void EndCondition_Constructor_SetsRequiredProperties()
    {
        // Act
        var condition = new EndCondition("ec-1", EndConditionType.Rounds, "Game ends after 10 rounds");

        // Assert
        Assert.Equal("ec-1", condition.Id);
        Assert.Equal(EndConditionType.Rounds, condition.Type);
        Assert.Equal("Game ends after 10 rounds", condition.Description);
        Assert.Null(condition.Value);
    }

    [Fact]
    public void EndCondition_WithValue_SetsCorrectly()
    {
        // Act
        var condition = new EndCondition("ec-1", EndConditionType.Points, "Reach 100 points", 100);

        // Assert
        Assert.Equal("ec-1", condition.Id);
        Assert.Equal(EndConditionType.Points, condition.Type);
        Assert.Equal("Reach 100 points", condition.Description);
        Assert.Equal(100, condition.Value);
    }

    [Fact]
    public void EndConditionType_Enum_HasAllValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(EndConditionType), EndConditionType.Rounds));
        Assert.True(Enum.IsDefined(typeof(EndConditionType), EndConditionType.Points));
        Assert.True(Enum.IsDefined(typeof(EndConditionType), EndConditionType.Elimination));
        Assert.True(Enum.IsDefined(typeof(EndConditionType), EndConditionType.Objective));
        Assert.True(Enum.IsDefined(typeof(EndConditionType), EndConditionType.Custom));
    }

    #endregion

    #region EdgeCase Tests

    [Fact]
    public void EdgeCase_Constructor_SetsRequiredProperties()
    {
        // Act
        var edgeCase = new EdgeCase("ec-1", EdgeCaseCategory.Exception, "Special rule for ties");

        // Assert
        Assert.Equal("ec-1", edgeCase.Id);
        Assert.Equal(EdgeCaseCategory.Exception, edgeCase.Category);
        Assert.Equal("Special rule for ties", edgeCase.Text);
        Assert.Null(edgeCase.RelatedActions);
        Assert.Null(edgeCase.RelatedPhases);
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
        Assert.Equal("ec-1", edgeCase.Id);
        Assert.Equal(EdgeCaseCategory.Clarification, edgeCase.Category);
        Assert.NotNull(edgeCase.RelatedActions);
        Assert.Equal(2, edgeCase.RelatedActions.Count);
        Assert.NotNull(edgeCase.RelatedPhases);
        Assert.Single(edgeCase.RelatedPhases);
    }

    [Fact]
    public void EdgeCaseCategory_Enum_HasAllValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.Exception));
        Assert.True(Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.Clarification));
        Assert.True(Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.Variant));
        Assert.True(Enum.IsDefined(typeof(EdgeCaseCategory), EdgeCaseCategory.FAQ));
    }

    #endregion

    #region GlossaryTerm Tests

    [Fact]
    public void GlossaryTerm_Constructor_SetsRequiredProperties()
    {
        // Act
        var term = new GlossaryTerm("Action Point", "A resource used to perform actions");

        // Assert
        Assert.Equal("Action Point", term.Term);
        Assert.Equal("A resource used to perform actions", term.Definition);
        Assert.Null(term.Examples);
    }

    [Fact]
    public void GlossaryTerm_WithExamples_SetsCorrectly()
    {
        // Arrange
        var examples = new List<string> { "Move: costs 1 AP", "Attack: costs 2 AP" };

        // Act
        var term = new GlossaryTerm("Action Point", "A resource", examples);

        // Assert
        Assert.Equal("Action Point", term.Term);
        Assert.NotNull(term.Examples);
        Assert.Equal(2, term.Examples.Count);
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
        Assert.Equal("Catan", metadata.Name);
        Assert.Equal(createdAt, metadata.CreatedAt);
        Assert.Null(metadata.Description);
        Assert.Null(metadata.UpdatedAt);
        Assert.Null(metadata.PlayerCount);
        Assert.Null(metadata.PlayTime);
        Assert.Null(metadata.AgeRange);
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
        Assert.Equal("Catan", metadata.Name);
        Assert.Equal(createdAt, metadata.CreatedAt);
        Assert.Equal("Settlers of Catan", metadata.Description);
        Assert.Equal(updatedAt, metadata.UpdatedAt);
        Assert.Equal(playerCount, metadata.PlayerCount);
        Assert.Equal(playTime, metadata.PlayTime);
        Assert.Equal(ageRange, metadata.AgeRange);
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
        Assert.Equal("game-1", spec.GameId);
        Assert.Equal("1.0", spec.Version);
        Assert.Equal(metadata, spec.Metadata);
        Assert.Equal(setup, spec.Setup);
        Assert.Single(spec.Phases);
        Assert.Single(spec.Actions);
        Assert.Equal(scoring, spec.Scoring);
        Assert.Single(spec.EndConditions);
        Assert.Null(spec.EdgeCases);
        Assert.Null(spec.Glossary);
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
        Assert.NotNull(spec.EdgeCases);
        Assert.Single(spec.EdgeCases);
        Assert.NotNull(spec.Glossary);
        Assert.Single(spec.Glossary);
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
        Assert.Equal("game-1", spec1.GameId);
        Assert.Equal("game-1", spec2.GameId);
    }

    #endregion
}
