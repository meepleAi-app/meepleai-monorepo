using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class GameToolkitDomainTests
{
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // ========================================================================
    // Constructor & Basic Properties
    // ========================================================================

    [Fact]
    public void Constructor_WithValidParams_CreatesToolkit()
    {
        var id = Guid.NewGuid();
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(id, GameId, "Test Toolkit", UserId);

        toolkit.Id.Should().Be(id);
        toolkit.GameId.Should().Be(GameId);
        toolkit.Name.Should().Be("Test Toolkit");
        toolkit.CreatedByUserId.Should().Be(UserId);
        toolkit.Version.Should().Be(1);
        toolkit.IsPublished.Should().BeFalse();
        toolkit.DiceTools.Should().BeEmpty();
        toolkit.CardTools.Should().BeEmpty();
        toolkit.TimerTools.Should().BeEmpty();
        toolkit.CounterTools.Should().BeEmpty();
        toolkit.ScoringTemplate.Should().BeNull();
        toolkit.TurnTemplate.Should().BeNull();
    }

    [Fact]
    public void Constructor_RaisesToolkitCreatedEvent()
    {
        var toolkit = CreateToolkit();
        var events = toolkit.DomainEvents;

        events.Should().ContainSingle();
        var created = events.First().Should().BeOfType<ToolkitCreatedEvent>().Subject;
        created.ToolkitId.Should().Be(toolkit.Id);
        created.GameId.Should().Be(GameId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(Guid.NewGuid(), GameId, name, UserId));
    }

    // ========================================================================
    // PrivateGameId + Override Flags (Issue #4972)
    // ========================================================================

    [Fact]
    public void Constructor_WithPrivateGameId_CreatesToolkit()
    {
        var privateGameId = Guid.NewGuid();
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), null, "Private Toolkit", UserId, privateGameId);

        toolkit.GameId.Should().BeNull();
        toolkit.PrivateGameId.Should().Be(privateGameId);
        toolkit.OverridesTurnOrder.Should().BeFalse();
        toolkit.OverridesScoreboard.Should().BeFalse();
        toolkit.OverridesDiceSet.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithOverrideFlags_SetsFlags()
    {
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), GameId, "Flagged Toolkit", UserId,
            overridesTurnOrder: true, overridesScoreboard: true, overridesDiceSet: false);

        toolkit.OverridesTurnOrder.Should().BeTrue();
        toolkit.OverridesScoreboard.Should().BeTrue();
        toolkit.OverridesDiceSet.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithBothGameIdAndPrivateGameId_ThrowsArgumentException()
    {
        var privateGameId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
                Guid.NewGuid(), GameId, "Bad Toolkit", UserId, privateGameId));
    }

    [Fact]
    public void Constructor_WithNeitherGameIdNorPrivateGameId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
                Guid.NewGuid(), null, "Bad Toolkit", UserId, null));
    }

    [Fact]
    public void UpdateOverrideFlags_UpdatesAllFlags()
    {
        var toolkit = CreateToolkit();
        var before = toolkit.UpdatedAt;

        toolkit.UpdateOverrideFlags(true, true, true);

        toolkit.OverridesTurnOrder.Should().BeTrue();
        toolkit.OverridesScoreboard.Should().BeTrue();
        toolkit.OverridesDiceSet.Should().BeTrue();
        (toolkit.UpdatedAt >= before).Should().BeTrue();
    }

    [Fact]
    public void UpdateOverrideFlags_CanClearFlags()
    {
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), GameId, "Test", UserId,
            overridesTurnOrder: true, overridesScoreboard: true, overridesDiceSet: true);

        toolkit.UpdateOverrideFlags(false, false, false);

        toolkit.OverridesTurnOrder.Should().BeFalse();
        toolkit.OverridesScoreboard.Should().BeFalse();
        toolkit.OverridesDiceSet.Should().BeFalse();
    }

    // ========================================================================
    // UpdateDetails
    // ========================================================================

    [Fact]
    public void UpdateDetails_WithNewName_UpdatesNameAndTimestamp()
    {
        var toolkit = CreateToolkit();
        var before = toolkit.UpdatedAt;

        toolkit.UpdateDetails("Updated Name");

        toolkit.Name.Should().Be("Updated Name");
        (toolkit.UpdatedAt >= before).Should().BeTrue();
    }

    [Fact]
    public void UpdateDetails_WithNullName_IsNoOp()
    {
        var toolkit = CreateToolkit();
        var originalName = toolkit.Name;
        var originalUpdatedAt = toolkit.UpdatedAt;
        toolkit.ClearDomainEvents();

        toolkit.UpdateDetails(null);

        toolkit.Name.Should().Be(originalName);
        toolkit.UpdatedAt.Should().Be(originalUpdatedAt);
        toolkit.DomainEvents.Should().BeEmpty();
    }

    // ========================================================================
    // Publish
    // ========================================================================

    [Fact]
    public void Publish_SetsIsPublishedAndIncrementsVersion()
    {
        var toolkit = CreateToolkit();

        toolkit.Publish();

        toolkit.IsPublished.Should().BeTrue();
        toolkit.Version.Should().Be(2);
    }

    [Fact]
    public void Publish_RaisesToolkitPublishedEvent()
    {
        var toolkit = CreateToolkit();
        toolkit.ClearDomainEvents();

        toolkit.Publish();

        var events = toolkit.DomainEvents;
        events.Should().ContainSingle();
        var published = events.First().Should().BeOfType<ToolkitPublishedEvent>().Subject;
        published.ToolkitId.Should().Be(toolkit.Id);
        published.Version.Should().Be(2);
    }

    // ========================================================================
    // Dice Tools
    // ========================================================================

    [Fact]
    public void AddDiceTool_WithValidConfig_AddsTool()
    {
        var toolkit = CreateToolkit();
        var config = new DiceToolConfig("Attack", DiceType.D6, 2, null, true, "#FF0000");

        toolkit.AddDiceTool(config);

        toolkit.DiceTools.Should().ContainSingle();
        toolkit.DiceTools[0].Name.Should().Be("Attack");
        toolkit.DiceTools[0].DiceType.Should().Be(DiceType.D6);
        toolkit.DiceTools[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void AddDiceTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        ((Action)(() => toolkit.AddDiceTool(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDiceTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddDiceTool(new DiceToolConfig($"Dice_{i}", DiceType.D6, 1, null, true, null));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddDiceTool(new DiceToolConfig("TooMany", DiceType.D6, 1, null, true, null)));

        ex.Message.Should().Contain("20");
    }

    [Fact]
    public void RemoveDiceTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddDiceTool(new DiceToolConfig("Attack", DiceType.D6, 1, null, true, null));

        var removed = toolkit.RemoveDiceTool("Attack");

        removed.Should().BeTrue();
        toolkit.DiceTools.Should().BeEmpty();
    }

    [Fact]
    public void RemoveDiceTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveDiceTool("NonExistent");

        removed.Should().BeFalse();
    }

    // ========================================================================
    // Counter Tools
    // ========================================================================

    [Fact]
    public void AddCounterTool_WithValidConfig_AddsTool()
    {
        var toolkit = CreateToolkit();
        var config = new CounterToolConfig("Health", 0, 100, 50, true, "heart", "#00FF00");

        toolkit.AddCounterTool(config);

        toolkit.CounterTools.Should().ContainSingle();
        toolkit.CounterTools[0].Name.Should().Be("Health");
        toolkit.CounterTools[0].DefaultValue.Should().Be(50);
        toolkit.CounterTools[0].IsPerPlayer.Should().BeTrue();
    }

    [Fact]
    public void AddCounterTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        ((Action)(() => toolkit.AddCounterTool(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCounterTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddCounterTool(new CounterToolConfig($"Counter_{i}", 0, 100, 0, false, null, null));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddCounterTool(new CounterToolConfig("TooMany", 0, 100, 0, false, null, null)));

        ex.Message.Should().Contain("20");
    }

    [Fact]
    public void RemoveCounterTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddCounterTool(new CounterToolConfig("Health", 0, 100, 50, true, null, null));

        var removed = toolkit.RemoveCounterTool("Health");

        removed.Should().BeTrue();
        toolkit.CounterTools.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCounterTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveCounterTool("NonExistent");

        removed.Should().BeFalse();
    }

    // ========================================================================
    // Card Tools
    // ========================================================================

    [Fact]
    public void AddCardTool_WithValidConfig_AddsTool()
    {
        var toolkit = CreateToolkit();
        var config = new CardToolConfig("Main Deck", "standard");

        toolkit.AddCardTool(config);

        toolkit.CardTools.Should().ContainSingle();
        toolkit.CardTools[0].Name.Should().Be("Main Deck");
        toolkit.CardTools[0].DeckType.Should().Be("standard");
    }

    [Fact]
    public void AddCardTool_WithFullConfig_AddsTool()
    {
        var toolkit = CreateToolkit();
        var entries = new List<CardEntry>
        {
            new("Ace of Spades", "Spades", "A"),
            new("King of Hearts", "Hearts", "K"),
        };
        var config = new CardToolConfig(
            "Custom Deck", "custom", 52, false,
            CardZone.TableArea, CardOrientation.FaceUp, entries,
            true, false, true, true);

        toolkit.AddCardTool(config);

        toolkit.CardTools.Should().ContainSingle();
        toolkit.CardTools[0].CardCount.Should().Be(2); // Derived from entries
        toolkit.CardTools[0].CardEntries.Count.Should().Be(2);
        toolkit.CardTools[0].Shuffleable.Should().BeFalse();
        toolkit.CardTools[0].DefaultZone.Should().Be(CardZone.TableArea);
        toolkit.CardTools[0].DefaultOrientation.Should().Be(CardOrientation.FaceUp);
        toolkit.CardTools[0].AllowPeek.Should().BeTrue();
        toolkit.CardTools[0].AllowReturnToDeck.Should().BeTrue();
    }

    [Fact]
    public void AddCardTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        ((Action)(() => toolkit.AddCardTool(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCardTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddCardTool(new CardToolConfig($"Deck_{i}", "standard"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddCardTool(new CardToolConfig("TooMany", "standard")));

        ex.Message.Should().Contain("20");
    }

    [Fact]
    public void RemoveCardTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddCardTool(new CardToolConfig("Main Deck", "standard"));

        var removed = toolkit.RemoveCardTool("Main Deck");

        removed.Should().BeTrue();
        toolkit.CardTools.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCardTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveCardTool("NonExistent");

        removed.Should().BeFalse();
    }

    // ========================================================================
    // Timer Tools
    // ========================================================================

    [Fact]
    public void AddTimerTool_WithValidConfig_AddsTool()
    {
        var toolkit = CreateToolkit();
        var config = new TimerToolConfig("Round Timer", 60);

        toolkit.AddTimerTool(config);

        toolkit.TimerTools.Should().ContainSingle();
        toolkit.TimerTools[0].Name.Should().Be("Round Timer");
        toolkit.TimerTools[0].DurationSeconds.Should().Be(60);
        toolkit.TimerTools[0].TimerType.Should().Be(TimerType.CountDown);
    }

    [Fact]
    public void AddTimerTool_ChessTimer_AddsTool()
    {
        var toolkit = CreateToolkit();
        var config = new TimerToolConfig("Chess Clock", 300, TimerType.Chess, true, "#FF0000", true, 30);

        toolkit.AddTimerTool(config);

        toolkit.TimerTools.Should().ContainSingle();
        toolkit.TimerTools[0].TimerType.Should().Be(TimerType.Chess);
        toolkit.TimerTools[0].IsPerPlayer.Should().BeTrue();
        toolkit.TimerTools[0].WarningThresholdSeconds.Should().Be(30);
    }

    [Fact]
    public void AddTimerTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        ((Action)(() => toolkit.AddTimerTool(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTimerTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddTimerTool(new TimerToolConfig($"Timer_{i}", 60));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddTimerTool(new TimerToolConfig("TooMany", 60)));

        ex.Message.Should().Contain("20");
    }

    [Fact]
    public void RemoveTimerTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddTimerTool(new TimerToolConfig("Round Timer", 60));

        var removed = toolkit.RemoveTimerTool("Round Timer");

        removed.Should().BeTrue();
        toolkit.TimerTools.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTimerTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveTimerTool("NonExistent");

        removed.Should().BeFalse();
    }

    // ========================================================================
    // Templates
    // ========================================================================

    [Fact]
    public void SetScoringTemplate_WithValidConfig_SetsTemplate()
    {
        var toolkit = CreateToolkit();
        var template = new ScoringTemplateConfig(["Victory Points", "Coins"], "VP", ScoreType.Points);

        toolkit.SetScoringTemplate(template);

        toolkit.ScoringTemplate.Should().NotBeNull();
        toolkit.ScoringTemplate.Dimensions.Length.Should().Be(2);
        toolkit.ScoringTemplate.DefaultUnit.Should().Be("VP");
        toolkit.ScoringTemplate.ScoreType.Should().Be(ScoreType.Points);
    }

    [Fact]
    public void SetScoringTemplate_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        ((Action)(() => toolkit.SetScoringTemplate(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetTurnTemplate_WithValidConfig_SetsTemplate()
    {
        var toolkit = CreateToolkit();
        var template = new TurnTemplateConfig(TurnOrderType.RoundRobin, ["Draw", "Play", "Discard"]);

        toolkit.SetTurnTemplate(template);

        toolkit.TurnTemplate.Should().NotBeNull();
        toolkit.TurnTemplate.TurnOrderType.Should().Be(TurnOrderType.RoundRobin);
        toolkit.TurnTemplate.Phases.Length.Should().Be(3);
    }

    [Fact]
    public void SetTurnTemplate_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        ((Action)(() => toolkit.SetTurnTemplate(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetStateTemplate_WithValidDefinition_SetsTemplate()
    {
        var toolkit = CreateToolkit();
        var definition = new StateTemplateDefinition("Chess Setup", TemplateCategory.Strategy, "{\"tools\":[]}");

        toolkit.SetStateTemplate(definition);

        toolkit.StateTemplate.Should().NotBeNull();
        toolkit.StateTemplate.Name.Should().Be("Chess Setup");
        toolkit.StateTemplate.Category.Should().Be(TemplateCategory.Strategy);
        toolkit.StateTemplate.SchemaJson.Should().Be("{\"tools\":[]}");
    }

    [Fact]
    public void SetStateTemplate_WithDescription_SetsFullTemplate()
    {
        var toolkit = CreateToolkit();
        var definition = new StateTemplateDefinition(
            "Party Game", TemplateCategory.Party, "{}", "A fun party game template");

        toolkit.SetStateTemplate(definition);

        toolkit.StateTemplate.Should().NotBeNull();
        toolkit.StateTemplate.Description.Should().Be("A fun party game template");
    }

    [Fact]
    public void SetStateTemplate_WithNull_ClearsTemplate()
    {
        var toolkit = CreateToolkit();
        toolkit.SetStateTemplate(new StateTemplateDefinition("Test", TemplateCategory.Strategy, "{}"));

        toolkit.SetStateTemplate(null);

        toolkit.StateTemplate.Should().BeNull();
    }

    // ========================================================================
    // ClearDomainEvents
    // ========================================================================

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var toolkit = CreateToolkit();
        toolkit.DomainEvents.Should().NotBeEmpty();

        toolkit.ClearDomainEvents();

        toolkit.DomainEvents.Should().BeEmpty();
    }

    // ========================================================================
    // Value Object Equality
    // ========================================================================

    [Fact]
    public void DiceToolConfig_WithCustomFaces_PreservesData()
    {
        var faces = new[] { "Hit", "Miss", "Critical" };
        var config = new DiceToolConfig("Custom", DiceType.Custom, 1, faces, true, "#000");

        config.DiceType.Should().Be(DiceType.Custom);
        config.CustomFaces!.Length.Should().Be(3);
        config.CustomFaces[2].Should().Be("Critical");
    }

    [Fact]
    public void CounterToolConfig_DefaultValues_SetCorrectly()
    {
        var config = new CounterToolConfig("Gold", 0, 9999, 100, false, "coin", "#FFD700");

        config.MinValue.Should().Be(0);
        config.MaxValue.Should().Be(9999);
        config.DefaultValue.Should().Be(100);
        config.IsPerPlayer.Should().BeFalse();
    }

    // ========================================================================
    // Helper
    // ========================================================================

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateToolkit(string name = "Test Toolkit")
    {
        return new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), GameId, name, UserId);
    }
}
