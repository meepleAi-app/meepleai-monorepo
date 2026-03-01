using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Tests.Constants;
using Xunit;

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

        Assert.Equal(id, toolkit.Id);
        Assert.Equal(GameId, toolkit.GameId);
        Assert.Equal("Test Toolkit", toolkit.Name);
        Assert.Equal(UserId, toolkit.CreatedByUserId);
        Assert.Equal(1, toolkit.Version);
        Assert.False(toolkit.IsPublished);
        Assert.Empty(toolkit.DiceTools);
        Assert.Empty(toolkit.CardTools);
        Assert.Empty(toolkit.TimerTools);
        Assert.Empty(toolkit.CounterTools);
        Assert.Null(toolkit.ScoringTemplate);
        Assert.Null(toolkit.TurnTemplate);
    }

    [Fact]
    public void Constructor_RaisesToolkitCreatedEvent()
    {
        var toolkit = CreateToolkit();
        var events = toolkit.DomainEvents;

        Assert.Single(events);
        var created = Assert.IsType<ToolkitCreatedEvent>(events.First());
        Assert.Equal(toolkit.Id, created.ToolkitId);
        Assert.Equal(GameId, created.GameId);
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

        Assert.Null(toolkit.GameId);
        Assert.Equal(privateGameId, toolkit.PrivateGameId);
        Assert.False(toolkit.OverridesTurnOrder);
        Assert.False(toolkit.OverridesScoreboard);
        Assert.False(toolkit.OverridesDiceSet);
    }

    [Fact]
    public void Constructor_WithOverrideFlags_SetsFlags()
    {
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), GameId, "Flagged Toolkit", UserId,
            overridesTurnOrder: true, overridesScoreboard: true, overridesDiceSet: false);

        Assert.True(toolkit.OverridesTurnOrder);
        Assert.True(toolkit.OverridesScoreboard);
        Assert.False(toolkit.OverridesDiceSet);
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

        Assert.True(toolkit.OverridesTurnOrder);
        Assert.True(toolkit.OverridesScoreboard);
        Assert.True(toolkit.OverridesDiceSet);
        Assert.True(toolkit.UpdatedAt >= before);
    }

    [Fact]
    public void UpdateOverrideFlags_CanClearFlags()
    {
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), GameId, "Test", UserId,
            overridesTurnOrder: true, overridesScoreboard: true, overridesDiceSet: true);

        toolkit.UpdateOverrideFlags(false, false, false);

        Assert.False(toolkit.OverridesTurnOrder);
        Assert.False(toolkit.OverridesScoreboard);
        Assert.False(toolkit.OverridesDiceSet);
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

        Assert.Equal("Updated Name", toolkit.Name);
        Assert.True(toolkit.UpdatedAt >= before);
    }

    [Fact]
    public void UpdateDetails_WithNullName_IsNoOp()
    {
        var toolkit = CreateToolkit();
        var originalName = toolkit.Name;
        var originalUpdatedAt = toolkit.UpdatedAt;
        toolkit.ClearDomainEvents();

        toolkit.UpdateDetails(null);

        Assert.Equal(originalName, toolkit.Name);
        Assert.Equal(originalUpdatedAt, toolkit.UpdatedAt);
        Assert.Empty(toolkit.DomainEvents);
    }

    // ========================================================================
    // Publish
    // ========================================================================

    [Fact]
    public void Publish_SetsIsPublishedAndIncrementsVersion()
    {
        var toolkit = CreateToolkit();

        toolkit.Publish();

        Assert.True(toolkit.IsPublished);
        Assert.Equal(2, toolkit.Version);
    }

    [Fact]
    public void Publish_RaisesToolkitPublishedEvent()
    {
        var toolkit = CreateToolkit();
        toolkit.ClearDomainEvents();

        toolkit.Publish();

        var events = toolkit.DomainEvents;
        Assert.Single(events);
        var published = Assert.IsType<ToolkitPublishedEvent>(events.First());
        Assert.Equal(toolkit.Id, published.ToolkitId);
        Assert.Equal(2, published.Version);
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

        Assert.Single(toolkit.DiceTools);
        Assert.Equal("Attack", toolkit.DiceTools[0].Name);
        Assert.Equal(DiceType.D6, toolkit.DiceTools[0].DiceType);
        Assert.Equal(2, toolkit.DiceTools[0].Quantity);
    }

    [Fact]
    public void AddDiceTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        Assert.Throws<ArgumentNullException>(() => toolkit.AddDiceTool(null!));
    }

    [Fact]
    public void AddDiceTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddDiceTool(new DiceToolConfig($"Dice_{i}", DiceType.D6, 1, null, true, null));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddDiceTool(new DiceToolConfig("TooMany", DiceType.D6, 1, null, true, null)));

        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void RemoveDiceTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddDiceTool(new DiceToolConfig("Attack", DiceType.D6, 1, null, true, null));

        var removed = toolkit.RemoveDiceTool("Attack");

        Assert.True(removed);
        Assert.Empty(toolkit.DiceTools);
    }

    [Fact]
    public void RemoveDiceTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveDiceTool("NonExistent");

        Assert.False(removed);
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

        Assert.Single(toolkit.CounterTools);
        Assert.Equal("Health", toolkit.CounterTools[0].Name);
        Assert.Equal(50, toolkit.CounterTools[0].DefaultValue);
        Assert.True(toolkit.CounterTools[0].IsPerPlayer);
    }

    [Fact]
    public void AddCounterTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        Assert.Throws<ArgumentNullException>(() => toolkit.AddCounterTool(null!));
    }

    [Fact]
    public void AddCounterTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddCounterTool(new CounterToolConfig($"Counter_{i}", 0, 100, 0, false, null, null));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddCounterTool(new CounterToolConfig("TooMany", 0, 100, 0, false, null, null)));

        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void RemoveCounterTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddCounterTool(new CounterToolConfig("Health", 0, 100, 50, true, null, null));

        var removed = toolkit.RemoveCounterTool("Health");

        Assert.True(removed);
        Assert.Empty(toolkit.CounterTools);
    }

    [Fact]
    public void RemoveCounterTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveCounterTool("NonExistent");

        Assert.False(removed);
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

        Assert.Single(toolkit.CardTools);
        Assert.Equal("Main Deck", toolkit.CardTools[0].Name);
        Assert.Equal("standard", toolkit.CardTools[0].DeckType);
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

        Assert.Single(toolkit.CardTools);
        Assert.Equal(2, toolkit.CardTools[0].CardCount); // Derived from entries
        Assert.Equal(2, toolkit.CardTools[0].CardEntries.Count);
        Assert.False(toolkit.CardTools[0].Shuffleable);
        Assert.Equal(CardZone.TableArea, toolkit.CardTools[0].DefaultZone);
        Assert.Equal(CardOrientation.FaceUp, toolkit.CardTools[0].DefaultOrientation);
        Assert.True(toolkit.CardTools[0].AllowPeek);
        Assert.True(toolkit.CardTools[0].AllowReturnToDeck);
    }

    [Fact]
    public void AddCardTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        Assert.Throws<ArgumentNullException>(() => toolkit.AddCardTool(null!));
    }

    [Fact]
    public void AddCardTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddCardTool(new CardToolConfig($"Deck_{i}", "standard"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddCardTool(new CardToolConfig("TooMany", "standard")));

        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void RemoveCardTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddCardTool(new CardToolConfig("Main Deck", "standard"));

        var removed = toolkit.RemoveCardTool("Main Deck");

        Assert.True(removed);
        Assert.Empty(toolkit.CardTools);
    }

    [Fact]
    public void RemoveCardTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveCardTool("NonExistent");

        Assert.False(removed);
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

        Assert.Single(toolkit.TimerTools);
        Assert.Equal("Round Timer", toolkit.TimerTools[0].Name);
        Assert.Equal(60, toolkit.TimerTools[0].DurationSeconds);
        Assert.Equal(TimerType.CountDown, toolkit.TimerTools[0].TimerType);
    }

    [Fact]
    public void AddTimerTool_ChessTimer_AddsTool()
    {
        var toolkit = CreateToolkit();
        var config = new TimerToolConfig("Chess Clock", 300, TimerType.Chess, true, "#FF0000", true, 30);

        toolkit.AddTimerTool(config);

        Assert.Single(toolkit.TimerTools);
        Assert.Equal(TimerType.Chess, toolkit.TimerTools[0].TimerType);
        Assert.True(toolkit.TimerTools[0].IsPerPlayer);
        Assert.Equal(30, toolkit.TimerTools[0].WarningThresholdSeconds);
    }

    [Fact]
    public void AddTimerTool_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        Assert.Throws<ArgumentNullException>(() => toolkit.AddTimerTool(null!));
    }

    [Fact]
    public void AddTimerTool_WhenLimitReached_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        for (int i = 0; i < 20; i++)
            toolkit.AddTimerTool(new TimerToolConfig($"Timer_{i}", 60));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            toolkit.AddTimerTool(new TimerToolConfig("TooMany", 60)));

        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void RemoveTimerTool_ExistingTool_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        toolkit.AddTimerTool(new TimerToolConfig("Round Timer", 60));

        var removed = toolkit.RemoveTimerTool("Round Timer");

        Assert.True(removed);
        Assert.Empty(toolkit.TimerTools);
    }

    [Fact]
    public void RemoveTimerTool_NonExistingTool_ReturnsFalse()
    {
        var toolkit = CreateToolkit();

        var removed = toolkit.RemoveTimerTool("NonExistent");

        Assert.False(removed);
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

        Assert.NotNull(toolkit.ScoringTemplate);
        Assert.Equal(2, toolkit.ScoringTemplate.Dimensions.Length);
        Assert.Equal("VP", toolkit.ScoringTemplate.DefaultUnit);
        Assert.Equal(ScoreType.Points, toolkit.ScoringTemplate.ScoreType);
    }

    [Fact]
    public void SetScoringTemplate_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        Assert.Throws<ArgumentNullException>(() => toolkit.SetScoringTemplate(null!));
    }

    [Fact]
    public void SetTurnTemplate_WithValidConfig_SetsTemplate()
    {
        var toolkit = CreateToolkit();
        var template = new TurnTemplateConfig(TurnOrderType.RoundRobin, ["Draw", "Play", "Discard"]);

        toolkit.SetTurnTemplate(template);

        Assert.NotNull(toolkit.TurnTemplate);
        Assert.Equal(TurnOrderType.RoundRobin, toolkit.TurnTemplate.TurnOrderType);
        Assert.Equal(3, toolkit.TurnTemplate.Phases.Length);
    }

    [Fact]
    public void SetTurnTemplate_WhenNull_ThrowsArgumentNullException()
    {
        var toolkit = CreateToolkit();

        Assert.Throws<ArgumentNullException>(() => toolkit.SetTurnTemplate(null!));
    }

    [Fact]
    public void SetStateTemplate_WithValidDefinition_SetsTemplate()
    {
        var toolkit = CreateToolkit();
        var definition = new StateTemplateDefinition("Chess Setup", TemplateCategory.Strategy, "{\"tools\":[]}");

        toolkit.SetStateTemplate(definition);

        Assert.NotNull(toolkit.StateTemplate);
        Assert.Equal("Chess Setup", toolkit.StateTemplate.Name);
        Assert.Equal(TemplateCategory.Strategy, toolkit.StateTemplate.Category);
        Assert.Equal("{\"tools\":[]}", toolkit.StateTemplate.SchemaJson);
    }

    [Fact]
    public void SetStateTemplate_WithDescription_SetsFullTemplate()
    {
        var toolkit = CreateToolkit();
        var definition = new StateTemplateDefinition(
            "Party Game", TemplateCategory.Party, "{}", "A fun party game template");

        toolkit.SetStateTemplate(definition);

        Assert.NotNull(toolkit.StateTemplate);
        Assert.Equal("A fun party game template", toolkit.StateTemplate.Description);
    }

    [Fact]
    public void SetStateTemplate_WithNull_ClearsTemplate()
    {
        var toolkit = CreateToolkit();
        toolkit.SetStateTemplate(new StateTemplateDefinition("Test", TemplateCategory.Strategy, "{}"));

        toolkit.SetStateTemplate(null);

        Assert.Null(toolkit.StateTemplate);
    }

    // ========================================================================
    // ClearDomainEvents
    // ========================================================================

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var toolkit = CreateToolkit();
        Assert.NotEmpty(toolkit.DomainEvents);

        toolkit.ClearDomainEvents();

        Assert.Empty(toolkit.DomainEvents);
    }

    // ========================================================================
    // Value Object Equality
    // ========================================================================

    [Fact]
    public void DiceToolConfig_WithCustomFaces_PreservesData()
    {
        var faces = new[] { "Hit", "Miss", "Critical" };
        var config = new DiceToolConfig("Custom", DiceType.Custom, 1, faces, true, "#000");

        Assert.Equal(DiceType.Custom, config.DiceType);
        Assert.Equal(3, config.CustomFaces!.Length);
        Assert.Equal("Critical", config.CustomFaces[2]);
    }

    [Fact]
    public void CounterToolConfig_DefaultValues_SetCorrectly()
    {
        var config = new CounterToolConfig("Gold", 0, 9999, 100, false, "coin", "#FFD700");

        Assert.Equal(0, config.MinValue);
        Assert.Equal(9999, config.MaxValue);
        Assert.Equal(100, config.DefaultValue);
        Assert.False(config.IsPerPlayer);
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
