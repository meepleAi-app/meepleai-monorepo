using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Handlers;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class GenerateToolkitFromKbHandlerTests
{
    private readonly Mock<ILlmService> _llmMock;
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IQdrantService> _qdrantMock;
    private readonly Mock<ILogger<GenerateToolkitFromKbHandler>> _loggerMock;
    private readonly GenerateToolkitFromKbHandler _handler;

    public GenerateToolkitFromKbHandlerTests()
    {
        _llmMock = new Mock<ILlmService>();
        _embeddingMock = new Mock<IEmbeddingService>();
        _qdrantMock = new Mock<IQdrantService>();
        _loggerMock = new Mock<ILogger<GenerateToolkitFromKbHandler>>();

        _handler = new GenerateToolkitFromKbHandler(
            _llmMock.Object,
            _embeddingMock.Object,
            _qdrantMock.Object,
            _loggerMock.Object);
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
    // Embedding failure → default suggestion
    // ========================================================================

    [Fact]
    public async Task Handle_WhenEmbeddingFails_ReturnsDefaultSuggestion()
    {
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("service unavailable"));

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        AssertIsDefaultSuggestion(result);
        Assert.Contains("Embedding generation failed", result.Reasoning);
        _qdrantMock.Verify(q => q.SearchAsync(
            It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(),
            It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmbeddingReturnsEmptyList_ReturnsDefaultSuggestion()
    {
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>()));

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        AssertIsDefaultSuggestion(result);
        Assert.Contains("Embedding generation failed", result.Reasoning);
    }

    // ========================================================================
    // Qdrant search returns no results → default suggestion
    // ========================================================================

    [Fact]
    public async Task Handle_WhenQdrantSearchFails_ReturnsDefaultSuggestion()
    {
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());
        SetupSuccessfulEmbedding();

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(),
                It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("collection not found"));

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        AssertIsDefaultSuggestion(result);
        Assert.Contains("No game rules found", result.Reasoning);
    }

    [Fact]
    public async Task Handle_WhenQdrantSearchReturnsEmptyResults_ReturnsDefaultSuggestion()
    {
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());
        SetupSuccessfulEmbedding();

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(),
                It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        AssertIsDefaultSuggestion(result);
        Assert.Contains("No game rules found", result.Reasoning);
    }

    // ========================================================================
    // LLM returns null → default suggestion
    // ========================================================================

    [Fact]
    public async Task Handle_WhenLlmReturnsNull_ReturnsDefaultSuggestion()
    {
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());
        SetupSuccessfulEmbedding();
        SetupSuccessfulQdrantSearch();

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiToolkitSuggestionDto?)null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        AssertIsDefaultSuggestion(result);
        Assert.Contains("AI generation failed", result.Reasoning);
    }

    // ========================================================================
    // Successful generation
    // ========================================================================

    [Fact]
    public async Task Handle_WhenAllServicesSucceed_ReturnsLlmSuggestion()
    {
        var gameId = Guid.NewGuid();
        var command = new GenerateToolkitFromKbCommand(gameId, Guid.NewGuid());
        SetupSuccessfulEmbedding();
        SetupSuccessfulQdrantSearch();

        var expected = new AiToolkitSuggestionDto(
            ToolkitName: "Catan Toolkit",
            DiceTools: [new("Resource Dice", DiceType.D6, 2, null, true, "#FF0000")],
            CounterTools: [new("Wood", 0, 99, 0, true, null, "#8B4513")],
            TimerTools: [new("Turn Timer", 120, TimerType.CountDown, true, null, false, 30)],
            ScoringTemplate: new(["Victory Points", "Longest Road"], "VP", ScoreType.Points),
            TurnTemplate: new(TurnOrderType.RoundRobin, ["Roll", "Trade", "Build"]),
            Overrides: new(true, true, true),
            Reasoning: "Catan uses 2d6 for resources, multiple trackable resources, and point-based scoring."
        );

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal("Catan Toolkit", result.ToolkitName);
        Assert.Single(result.DiceTools);
        Assert.Equal("Resource Dice", result.DiceTools[0].Name);
        Assert.Single(result.CounterTools);
        Assert.Equal("Wood", result.CounterTools[0].Name);
        Assert.Single(result.TimerTools);
        Assert.NotNull(result.ScoringTemplate);
        Assert.NotNull(result.TurnTemplate);
        Assert.Contains("Catan", result.Reasoning);
    }

    [Fact]
    public async Task Handle_PassesGameIdToQdrantSearch()
    {
        var gameId = Guid.NewGuid();
        var command = new GenerateToolkitFromKbCommand(gameId, Guid.NewGuid());
        SetupSuccessfulEmbedding();
        SetupSuccessfulQdrantSearch();

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMinimalSuggestion());

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        _qdrantMock.Verify(q => q.SearchAsync(
            gameId.ToString(),
            It.IsAny<float[]>(),
            30,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UsesAgentTaskRequestSource()
    {
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());
        SetupSuccessfulEmbedding();
        SetupSuccessfulQdrantSearch();

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMinimalSuggestion());

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        _llmMock.Verify(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
            It.IsAny<string>(), It.IsAny<string>(),
            RequestSource.AgentTask,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private void SetupSuccessfulEmbedding()
    {
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));
    }

    private void SetupSuccessfulQdrantSearch()
    {
        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(),
                It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Score = 0.95f, Text = "Each player rolls 2 dice to gather resources.", PdfId = "pdf1", Page = 1, ChunkIndex = 0 },
                new() { Score = 0.88f, Text = "Victory points are earned by building settlements and cities.", PdfId = "pdf1", Page = 2, ChunkIndex = 1 }
            }));
    }

    private static AiToolkitSuggestionDto CreateMinimalSuggestion() => new(
        ToolkitName: "Test Toolkit",
        DiceTools: [],
        CounterTools: [],
        TimerTools: [],
        ScoringTemplate: null,
        TurnTemplate: null,
        Overrides: null,
        Reasoning: "Test reasoning"
    );

    private static void AssertIsDefaultSuggestion(AiToolkitSuggestionDto result)
    {
        Assert.NotNull(result);
        Assert.Equal("Game Toolkit", result.ToolkitName);
        Assert.Single(result.DiceTools);
        Assert.Equal(DiceType.D6, result.DiceTools[0].DiceType);
        Assert.Equal(2, result.DiceTools[0].Quantity);
        Assert.NotNull(result.ScoringTemplate);
        Assert.NotNull(result.TurnTemplate);
    }
}
