using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Unit tests for the cache-aside layer added to <see cref="GenerateToolkitFromKbHandler"/> (ADR-069 #2383).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public sealed class GenerateToolkitFromKbHandlerCacheTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid UserId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid KbCardId = Guid.Parse("33333333-0000-0000-0000-000000000002");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // ─── Cache HIT ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedDtoWithoutLlmCall()
    {
        // Arrange
        var cachedDto = BuildSampleDto();
        var cachedJson = JsonSerializer.Serialize(cachedDto, JsonOptions);
        var cacheEntry = AiToolkitSuggestionCacheEntry.Create(GameId, cachedJson, kbVersion: null);

        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(cacheEntry);

        // Strict mock: any LLM call is unexpected and will throw
        var llmService = new Mock<ILlmService>(MockBehavior.Strict);

        var handler = BuildHandler(cacheRepo: cacheRepo, llmService: llmService);

        // Act
        var result = await handler.Handle(new GenerateToolkitFromKbCommand(GameId, UserId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ToolkitName.Should().Be(cachedDto.ToolkitName);
        llmService.VerifyNoOtherCalls();
        cacheRepo.Verify(r => r.UpsertAsync(It.IsAny<AiToolkitSuggestionCacheEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Cache MISS ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CacheMiss_RunsLlmAndPersistsCache()
    {
        // Arrange
        var expectedDto = BuildSampleDto();

        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((AiToolkitSuggestionCacheEntry?)null);
        cacheRepo.Setup(r => r.UpsertAsync(It.IsAny<AiToolkitSuggestionCacheEntry>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(1);

        var llmService = new Mock<ILlmService>();
        llmService.Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagPipeline, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = BuildHandler(cacheRepo: cacheRepo, llmService: llmService, unitOfWork: unitOfWork);

        // Act
        var result = await handler.Handle(new GenerateToolkitFromKbCommand(GameId, UserId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ToolkitName.Should().Be(expectedDto.ToolkitName);
        // Cache write-back must have been called exactly once
        cacheRepo.Verify(r => r.UpsertAsync(
            It.Is<AiToolkitSuggestionCacheEntry>(e => e.GameId == GameId),
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Cache GET failure → degraded (falls through to LLM) ────────────────────

    [Fact]
    public async Task Handle_CacheRepoGetThrows_FallsThroughToLlmDegraded()
    {
        // Arrange
        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("db down"));

        // UpsertAsync also fails (second degraded path)
        cacheRepo.Setup(r => r.UpsertAsync(It.IsAny<AiToolkitSuggestionCacheEntry>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("db still down"));

        var unitOfWork = new Mock<IUnitOfWork>();

        var llmService = new Mock<ILlmService>();
        llmService.Setup(l => l.GenerateJsonAsync<AiToolkitSuggestionDto>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagPipeline, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleDto());

        var handler = BuildHandler(cacheRepo: cacheRepo, llmService: llmService, unitOfWork: unitOfWork);

        // Act — must NOT throw even with cache failures
        var act = async () => await handler.Handle(new GenerateToolkitFromKbCommand(GameId, UserId), CancellationToken.None);

        // Assert
        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().NotBeNull();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static AiToolkitSuggestionDto BuildSampleDto() =>
        new(
            ToolkitName: "Catan Toolkit",
            DiceTools: [new("Dadi", DiceType.D6, 2, null, false, null)],
            CounterTools: [],
            TimerTools: [],
            ScoringTemplate: new(["VP"], "points", ScoreType.Points),
            TurnTemplate: new(TurnOrderType.RoundRobin, []),
            Overrides: new(false, false, false),
            Reasoning: "Uses 2xD6."
        );

    private static GenerateToolkitFromKbHandler BuildHandler(
        Mock<IAiToolkitSuggestionCacheRepository>? cacheRepo = null,
        Mock<ILlmService>? llmService = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        // Set up game core data (always succeeds)
        var gameCoreData = new Mock<IGameCoreDataProvider>();
        gameCoreData.Setup(r => r.GetCoreDataAsync(GameRef.Shared(GameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCoreData.Create("Catan", 1995, 3, 4, 90, 10));

        // RAG access (returns one card)
        var ragAccess = new Mock<Api.BoundedContexts.KnowledgeBase.Application.Services.IRagAccessService>();
        ragAccess.Setup(r => r.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { KbCardId });

        // Hybrid search (returns one chunk)
        var hybridSearch = new Mock<IHybridSearchService>();
        hybridSearch.Setup(s => s.SearchAsync(
                It.IsAny<string>(), GameId, SearchMode.Hybrid, It.IsAny<int>(),
                It.IsAny<List<Guid>?>(), It.IsAny<float>(), It.IsAny<float>(),
                It.IsAny<double>(), It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                new() {
                    ChunkId = "chunk-1",
                    Content = "Players roll two D6 dice.",
                    PdfDocumentId = KbCardId.ToString(),
                    GameId = GameId,
                    ChunkIndex = 0,
                    HybridScore = 0.85f,
                    Mode = SearchMode.Hybrid
                }
            });

        // LLM service default (may be overridden by caller)
        llmService ??= new Mock<ILlmService>();

        // Cache repo default (cache miss)
        cacheRepo ??= new Mock<IAiToolkitSuggestionCacheRepository>();

        // UoW default (succeeds)
        unitOfWork ??= new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new GenerateToolkitFromKbHandler(
            hybridSearch.Object,
            llmService.Object,
            ragAccess.Object,
            gameCoreData.Object,
            NullLogger<GenerateToolkitFromKbHandler>.Instance,
            suggestionValidator: null,
            cacheRepository: cacheRepo.Object,
            unitOfWork: unitOfWork.Object);
    }
}
