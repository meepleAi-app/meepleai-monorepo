using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class ApplyAiToolkitSuggestionHandlerTests
{
    private readonly Mock<IGameToolkitRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly ApplyAiToolkitSuggestionHandler _handler;

    public ApplyAiToolkitSuggestionHandlerTests()
    {
        _repoMock = new Mock<IGameToolkitRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _handler = new ApplyAiToolkitSuggestionHandler(_repoMock.Object, _uowMock.Object);
    }

    // ========================================================================
    // Null / argument validation
    // ========================================================================

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Create new toolkit (ToolkitId is null)
    // ========================================================================

    [Fact]
    public async Task Handle_WhenToolkitIdIsNull_CreatesNewToolkit()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var suggestion = CreateFullSuggestion();
        var command = new ApplyAiToolkitSuggestionCommand(gameId, userId, null, suggestion);

        Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit? captured = null;
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Catan Toolkit", result.Name);
        Assert.Equal(gameId, result.GameId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // Update existing toolkit (ToolkitId is provided)
    // ========================================================================

    [Fact]
    public async Task Handle_WhenToolkitIdIsProvided_UpdatesExistingToolkit()
    {
        var existingToolkit = CreateTestToolkit();
        var suggestion = CreateFullSuggestion();
        var command = new ApplyAiToolkitSuggestionCommand(
            existingToolkit.GameId!.Value, Guid.NewGuid(), existingToolkit.Id, suggestion);

        _repoMock.Setup(r => r.GetByIdAsync(existingToolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToolkit);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Catan Toolkit", result.Name);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenToolkitIdNotFound_ThrowsNotFoundException()
    {
        var toolkitId = Guid.NewGuid();
        var suggestion = CreateFullSuggestion();
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), toolkitId, suggestion);

        _repoMock.Setup(r => r.GetByIdAsync(toolkitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Maps DiceTools from suggestion
    // ========================================================================

    [Fact]
    public async Task Handle_MapsDiceToolsFromSuggestion()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Dice Game",
            DiceTools:
            [
                new("Attack Die", DiceType.D20, 1, null, true, "#FF0000"),
                new("Custom Die", DiceType.Custom, 1, new[] { "A", "B", "C" }, false, null)
            ],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: new(false, false, true),
            Reasoning: "Two custom dice for combat"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.DiceTools.Count);
        Assert.Equal("Attack Die", result.DiceTools[0].Name);
        Assert.Equal(DiceType.D20, result.DiceTools[0].DiceType);
        Assert.Equal("Custom Die", result.DiceTools[1].Name);
        Assert.Equal(DiceType.Custom, result.DiceTools[1].DiceType);
    }

    // ========================================================================
    // Maps CounterTools from suggestion
    // ========================================================================

    [Fact]
    public async Task Handle_MapsCounterToolsFromSuggestion()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Resource Game",
            DiceTools: [],
            CounterTools:
            [
                new("Wood", 0, 99, 0, true, "tree", "#8B4513"),
                new("Gold", 0, 999, 10, false, "coin", "#FFD700")
            ],
            TimerTools: [],
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: new(false, false, false),
            Reasoning: "Resource tracking"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.CounterTools.Count);
        Assert.Equal("Wood", result.CounterTools[0].Name);
        Assert.True(result.CounterTools[0].IsPerPlayer);
        Assert.Equal("Gold", result.CounterTools[1].Name);
        Assert.Equal(10, result.CounterTools[1].DefaultValue);
    }

    // ========================================================================
    // Maps TimerTools from suggestion
    // ========================================================================

    [Fact]
    public async Task Handle_MapsTimerToolsFromSuggestion()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Timed Game",
            DiceTools: [],
            CounterTools: [],
            TimerTools:
            [
                new("Turn Timer", 60, TimerType.CountDown, true, "#00FF00", false, 10),
                new("Chess Clock", 300, TimerType.Chess, false, null, true, null)
            ],
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: new(false, false, false),
            Reasoning: "Time management"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.TimerTools.Count);
        Assert.Equal("Turn Timer", result.TimerTools[0].Name);
        Assert.Equal(TimerType.CountDown, result.TimerTools[0].TimerType);
        Assert.Equal(60, result.TimerTools[0].DurationSeconds);
        Assert.Equal("Chess Clock", result.TimerTools[1].Name);
        Assert.Equal(TimerType.Chess, result.TimerTools[1].TimerType);
        Assert.True(result.TimerTools[1].IsPerPlayer);
    }

    // ========================================================================
    // Sets ScoringTemplate when provided
    // ========================================================================

    [Fact]
    public async Task Handle_SetsScoringTemplateWhenProvided()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Scoring Game",
            DiceTools: [],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: new(["VP", "Gold", "Army"], "VP", ScoreType.Points),
            TurnTemplate: null,
            Overrides: new(false, true, false),
            Reasoning: "Point-based scoring"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result.ScoringTemplate);
        Assert.Equal(3, result.ScoringTemplate.Dimensions.Length);
        Assert.Contains("VP", result.ScoringTemplate.Dimensions);
        Assert.Equal("VP", result.ScoringTemplate.DefaultUnit);
        Assert.Equal(ScoreType.Points, result.ScoringTemplate.ScoreType);
    }

    [Fact]
    public async Task Handle_LeavesNullScoringTemplateWhenNotProvided()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "No Scoring",
            DiceTools: [],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: new(false, false, false),
            Reasoning: "No scoring"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Null(result.ScoringTemplate);
    }

    // ========================================================================
    // Sets TurnTemplate when provided
    // ========================================================================

    [Fact]
    public async Task Handle_SetsTurnTemplateWhenProvided()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Turn Game",
            DiceTools: [],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: null,
            TurnTemplate: new(TurnOrderType.RoundRobin, ["Draw", "Play", "Discard"]),
            Overrides: new(true, false, false),
            Reasoning: "Round-robin with phases"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result.TurnTemplate);
        Assert.Equal(TurnOrderType.RoundRobin, result.TurnTemplate.TurnOrderType);
        Assert.Equal(3, result.TurnTemplate.Phases.Length);
        Assert.Contains("Draw", result.TurnTemplate.Phases);
    }

    // ========================================================================
    // Stores reasoning in AgentConfig
    // ========================================================================

    [Fact]
    public async Task Handle_StoresReasoningInAgentConfig()
    {
        var reasoning = "Catan uses 2d6 for resource production, has multiple trackable resources.";
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Test",
            DiceTools: [],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: new(false, false, false),
            Reasoning: reasoning
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(reasoning, result.AgentConfig);
    }

    // ========================================================================
    // Override flags are applied
    // ========================================================================

    [Fact]
    public async Task Handle_AppliesOverrideFlags()
    {
        var suggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Override Game",
            DiceTools: [],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: new(true, true, true),
            Reasoning: "Full override"
        );
        var command = new ApplyAiToolkitSuggestionCommand(Guid.NewGuid(), Guid.NewGuid(), null, suggestion);
        SetupAddCapture();

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.OverridesTurnOrder);
        Assert.True(result.OverridesScoreboard);
        Assert.True(result.OverridesDiceSet);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private void SetupAddCapture()
    {
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateTestToolkit()
    {
        return new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), Guid.NewGuid(), "Existing Toolkit", Guid.NewGuid());
    }

    private static AiToolkitSuggestionDto CreateFullSuggestion() => new(
        ToolkitName: "Catan Toolkit",
        DiceTools: [new("Resource Dice", DiceType.D6, 2, null, true, null)],
        CounterTools: [new("Wood", 0, 99, 0, true, "tree", "#8B4513")],
        TimerTools: [new("Turn Timer", 120, TimerType.CountDown, true, null, false, 30)],
        ScoringTemplate: new(["Victory Points"], "VP", ScoreType.Points),
        TurnTemplate: new(TurnOrderType.RoundRobin, ["Roll", "Trade", "Build"]),
        Overrides: new(true, true, true),
        Reasoning: "Catan requires 2d6 dice, multiple resource counters, and point-based scoring."
    );
}
