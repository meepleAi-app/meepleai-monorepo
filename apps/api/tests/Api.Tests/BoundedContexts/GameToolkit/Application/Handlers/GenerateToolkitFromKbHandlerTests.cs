using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class GenerateToolkitFromKbHandlerTests
{
    private readonly Mock<IHybridSearchService> _hybridSearchMock;
    private readonly Mock<ILlmService> _llmMock;
    private readonly Mock<Api.BoundedContexts.KnowledgeBase.Application.Services.IRagAccessService> _ragAccessMock;
    private readonly Mock<IGameRepository> _gameRepoMock;
    private readonly Mock<ILogger<GenerateToolkitFromKbHandler>> _loggerMock;
    private readonly GenerateToolkitFromKbHandler _handler;

    private static readonly Guid GameId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid KbCardId = Guid.Parse("33333333-0000-0000-0000-000000000001");

    public GenerateToolkitFromKbHandlerTests()
    {
        _hybridSearchMock = new Mock<IHybridSearchService>();
        _llmMock = new Mock<ILlmService>();
        _ragAccessMock = new Mock<Api.BoundedContexts.KnowledgeBase.Application.Services.IRagAccessService>();
        _gameRepoMock = new Mock<IGameRepository>();
        _loggerMock = new Mock<ILogger<GenerateToolkitFromKbHandler>>();

        _handler = new GenerateToolkitFromKbHandler(
            _hybridSearchMock.Object,
            _llmMock.Object,
            _ragAccessMock.Object,
            _gameRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        _gameRepoMock
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var command = new GenerateToolkitFromKbCommand(GameId, UserId);
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoAccessibleKbCards_ThrowsInvalidOperationException()
    {
        SetupGameRepo("Catan");
        _ragAccessMock
            .Setup(r => r.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var command = new GenerateToolkitFromKbCommand(GameId, UserId);
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*knowledge base*");
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsSuggestionWithConfidenceFields()
    {
        SetupGameRepo("Catan");
        SetupRagAccess([KbCardId]);
        SetupHybridSearch(new HybridSearchResult
        {
            ChunkId = "chunk-1",
            Content = "Players roll two D6 dice on their turn.",
            PdfDocumentId = KbCardId.ToString(),
            GameId = GameId,
            ChunkIndex = 0,
            HybridScore = 0.85f,
            Mode = SearchMode.Hybrid
        });

        var expectedSuggestion = new AiToolkitSuggestionDto(
            ToolkitName: "Catan Toolkit",
            DiceTools: [new("Dadi", DiceType.D6, 2, null, false, null)],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: new(["VP"], "points", ScoreType.Points),
            TurnTemplate: new(TurnOrderType.RoundRobin, []),
            Overrides: new(false, false, false),
            Reasoning: "Players use 2xD6 as stated in the rules."
        );

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagPipeline, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSuggestion);

        var command = new GenerateToolkitFromKbCommand(GameId, UserId);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.ToolkitName.Should().Be("Catan Toolkit");
        result.DiceTools.Should().ContainSingle();
        result.ConfidenceScore.Should().BeGreaterThan(0f);
        result.ChunksAnalyzed.Should().BeGreaterThan(0);
        result.KbCoveragePercent.Should().BeGreaterThanOrEqualTo(0f);
        // 1 chunk < 5 threshold → confidence scaled down → RequiresHumanReview = true
        result.RequiresHumanReview.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LlmReturnsNull_RetriesAndThrowsIfBothFail()
    {
        SetupGameRepo("Catan");
        SetupRagAccess([KbCardId]);
        SetupHybridSearch(new HybridSearchResult
        {
            ChunkId = "chunk-1",
            Content = "rule text",
            PdfDocumentId = KbCardId.ToString(),
            GameId = GameId,
            ChunkIndex = 0,
            HybridScore = 0.8f,
            Mode = SearchMode.Hybrid
        });

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagPipeline, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiToolkitSuggestionDto?)null);

        var command = new GenerateToolkitFromKbCommand(GameId, UserId);
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LLM*");

        // Verify retry happened (LLM called twice — primary + retry)
        _llmMock.Verify(
            l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagPipeline, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_HighConfidence_SetsRequiresHumanReviewFalse()
    {
        SetupGameRepo("Catan");
        SetupRagAccess([KbCardId]);

        // 8 unique chunks with high scores → confidence >= 0.6
        var chunks = Enumerable.Range(1, 8).Select(i => new HybridSearchResult
        {
            ChunkId = $"chunk-{i}",
            Content = $"rule text {i}",
            PdfDocumentId = KbCardId.ToString(),
            GameId = GameId,
            ChunkIndex = i - 1,
            HybridScore = 0.82f,
            Mode = SearchMode.Hybrid
        }).ToList();

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), GameId, SearchMode.Hybrid, It.IsAny<int>(),
                It.IsAny<List<Guid>?>(), It.IsAny<float>(), It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _llmMock
            .Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagPipeline, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiToolkitSuggestionDto(
                "Catan", [], [], [], null, null, null, "High coverage."));

        var result = await _handler.Handle(
            new GenerateToolkitFromKbCommand(GameId, UserId),
            TestContext.Current.CancellationToken);

        result.RequiresHumanReview.Should().BeFalse();
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.6f);
    }

    // ---- Helpers ----

    private void SetupGameRepo(string title)
    {
        var game = new Game(GameId, new GameTitle(title));
        _gameRepoMock
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
    }

    private void SetupRagAccess(List<Guid> cardIds)
    {
        _ragAccessMock
            .Setup(r => r.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardIds);
    }

    private void SetupHybridSearch(HybridSearchResult chunk)
    {
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), GameId, SearchMode.Hybrid, It.IsAny<int>(),
                It.IsAny<List<Guid>?>(), It.IsAny<float>(), It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult> { chunk });
    }
}
