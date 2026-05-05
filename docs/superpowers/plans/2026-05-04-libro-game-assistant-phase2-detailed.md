---
title: Libro Game AI Assistant — Phase 2 Detailed Implementation Plan
status: draft
type: implementation-plan-detail
date: 2026-05-04
parent: docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md
phase: 2
duration_weeks: 6
review-cycle: spec-panel-multi-expert
---

# Phase 2 — G3 Q&A + TranslationService + Glossary + House Rules (Detailed)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement task-by-task.
>
> **Pattern compliance**: All code MUST follow Sprint 0+1 lessons (below). If a pattern is missing from a file, audit first, fix the plan, then implement.

## Sprint 0+1 Pattern Compliance Mandatory Reference

| Pattern | Rule | Source |
|---------|------|--------|
| MediatR handlers | `ICommandHandler<TCmd,TRes>`, `IQueryHandler<TQ,TRes>` — NOT `IRequestHandler` | `AddRulebookCommand.cs:45` |
| Handler naming | `{Feature}Command.cs` + `{Feature}CommandHandler.cs` (separate files, `internal sealed`) | Same |
| UserId | NO `IUserContext`. Pass `UserId` as command/query parameter | `AskQuestionQuery.cs:18` |
| IEmbeddingService (KB/Search) | `Api.Services.IEmbeddingService` — methods: `GenerateEmbeddingAsync(text, language, ct)` | `Services/IEmbeddingService.cs` |
| ILlmService | `Api.Services.ILlmService` with `GenerateCompletionAsync`, `GenerateCompletionWithModelAsync` | `Services/ILlmService.cs` |
| ILlmService explicit model | `GenerateCompletionWithModelAsync(explicitModel, systemPrompt, userPrompt, source, maxTokens?, ct)` | `ILlmService.cs:93` |
| IDistributedCache Redis | Already registered via `AddStackExchangeRedisCache` in `InfrastructureServiceExtensions.cs:186` | `InfrastructureServiceExtensions.cs` |
| HouseRule model | Exists as `AgentMemory.Domain.Models.HouseRule` (value object in `GameMemory` JSONB aggregate) | `AgentMemory/Domain/Models/HouseRule.cs` |
| GameMemory aggregate | `AgentMemory.Domain.Entities.GameMemory` — JSONB-serialized house_rules_json | `GameMemory.cs`, `GameMemoryEntityConfiguration.cs` |
| AddHouseRule endpoint | Already exists: `POST /api/v1/agent-memory/games/{gameId}/memory/house-rules` | `AgentMemoryEndpoints.cs:54` |
| QueryComplexityAnalyzer | Singleton keyword-heuristic analyzer in KB BC (NOT an interface yet) | `QueryComplexityAnalyzer.cs` |
| IQueryComplexityClassifier | Async LLM-backed interface in `KnowledgeBase.Domain.Services.Enhancements` (DIFFERENT from `QueryComplexityAnalyzer`) | `IQueryComplexityClassifier.cs` |
| IRagValidationPipelineService | `KnowledgeBase.Domain.Services.RagValidation.IRagValidationPipelineService` | `RagValidationPipelineService.cs` |
| ISemanticResponseCache | `KnowledgeBase.Application.Services.ISemanticResponseCache` — Redis-backed | `SemanticResponseCache.cs` |
| AskQuestionQuery current params | `GameId, Question, ThreadId?, SearchMode?, Language, BypassCache, UserId?, UserRole?` — NO ResponseLanguage yet | `AskQuestionQuery.cs:12` |
| Testing stack | xUnit + NSubstitute + FluentAssertions — NO Moq, NO Testcontainers | Pattern audit reference |
| Migrations timestamp | `{yyyyMMddHHmmss}_{Description}.cs` | `Migrations/` folder |
| snake_case columns | Always explicit `HasColumnName("snake_case")` | `GameMemoryEntityConfiguration.cs` |
| NO Polly explicit | HttpClient retry is handled via framework defaults — do not add Polly explicitly | Pattern audit reference |
| infra compose naming | `compose.*.yml` (not `docker-compose.*.yml`) | `infra/compose.*.yml` audit |

---

## Spec-Panel Findings

### Convergent Insights (where experts agree)

- **Translation is a cross-cutting service, not a BC** (Newman + Wiegers + Nygard): `TranslationService` belongs in `Infrastructure/Translation/`, not inside KnowledgeBase or AgentMemory. All three agree the split into `INarrativeTranslationService` (paragraph-level, creative, context-aware) vs `IGenericTranslationService` (factual/short strings, lower cost) is correct and necessary for cost management.
- **HouseRule is already modeled in AgentMemory BC** (Adzic + Newman): The plan v2 says "HouseRule entity + repository" but `HouseRule` already exists as a value object inside `GameMemory` aggregate (JSONB-serialized). Task 2.5 should extend this pattern, not create a new repository. The `IHouseRuleMatcher` is the missing piece.
- **Hallucination CI gate requires a deterministic wrapper** (Crispin + Nygard): LLM-as-judge is inherently stochastic. The gate must use deterministic scoring (e.g., majority vote over 3 runs, threshold ≥ 80% agreement), not a single LLM call.
- **KB indexing services are the blocker for Task 2.3** (Wiegers + Adzic): Sprint 1 Task 1.6 (`PhotoBatchProcessor`) deferred `IDocumentChunker`, `IEmbeddingService` (in the DocumentProcessing sense), `IKnowledgeBaseIndexer`, and `KnowledgeChunk`. Without these, the "extend AskQuestionQueryHandler" work in Task 2.3 cannot include photo-batch document Q&A. Task 2.3 must introduce these services.
- **Cost ceiling is not optional** (Nygard + Wiegers): Every translation + Q&A call must have an enforced per-request cost cap. Phase 3 introduces `IPricingEngine.ConsumeQuotaAsync`, but Phase 2 must lay the groundwork with a static `MaxRequestCostUsd` guard in `IGenericTranslationService` and `AskQuestionQueryHandler`.

### Productive Tensions (trade-offs revealed)

- **Translation quality metric tension** (Wiegers vs Adzic): Wiegers wants BLEU/METEOR as objective automated metrics; Adzic argues Italian narrative gaming text invalidates BLEU (creative license, proper nouns, game terminology). Resolution: Use MOS-inspired 5-point human rating for narrative (≥ 4.0 required), plus automated LLM-as-judge for factual accuracy (≥ 85% accuracy against golden reference). BLEU is tracked for regression only, not as a gate.
- **IHouseRuleMatcher location tension** (Newman vs Adzic): Newman says it belongs in KnowledgeBase Application Services (cross-BC read); Adzic says put it inside AgentMemory BC where HouseRule lives. Resolution: `IHouseRuleMatcher` is a KnowledgeBase Application Service interface backed by an AgentMemory repository call — this is an accepted cross-BC read (read model pattern, following ADR-053). The matcher is registered in KB DI but depends on `IGameMemoryRepository` from AgentMemory.
- **Glossary NER model tension** (Nygard vs Crispin): Nygard wants to avoid LLM calls for NER (cost, latency); Crispin wants high precision for game term extraction. Resolution: Use SpaCy sentence-transformer NER for offline batch bootstrap (run once per game on first photo batch completion), with LLM refinement only for ambiguous extractions. NER is not on the hot path.
- **`IQAComplexityClassifier` naming collision** (Wiegers): Plan v2 names Task 2.4 `IQAComplexityClassifier` but the codebase already has `IQueryComplexityClassifier` (async LLM-backed) and `QueryComplexityAnalyzer` (sync heuristic, Singleton). Resolution: Task 2.4 introduces `IQAComplexityClassifier` as a distinct interface in the `DocumentProcessing` or `Infrastructure/Translation` layer — it classifies Q&A answering complexity (simple factual vs multi-step reasoning), not LLM routing tier. This is a different concern from `QueryComplexityAnalyzer`.

### Spike Findings (drift from plan v2)

- **AskQuestionQuery current shape** (`AskQuestionQuery.cs:12`): Record has `GameId, Question, ThreadId?, SearchMode?, Language, BypassCache, UserId?, UserRole?`. Task 2.3 must add `ResponseLanguage` (separate from `Language` which controls embedding/search language). Drift: plan v2 was correct on parameter addition.
- **HouseRule already modeled** (`AgentMemory/Domain/Models/HouseRule.cs:8`): `HouseRule` is a sealed class with `Description`, `AddedAt`, `Source (HouseRuleSource enum)`, not an EF entity. `GameMemory` stores rules as JSONB (`house_rules_json`). `AddHouseRule` endpoint already exists at `POST /api/v1/agent-memory/games/{gameId}/memory/house-rules`. Drift: plan v2 says "HouseRule entity + repository + endpoints" but the entity, table, and endpoint already exist. Task 2.5 scope reduces to: `IHouseRuleMatcher` service + Q&A integration + optional `MatchReason` field extension.
- **`IQueryComplexityClassifier` already exists** (`KnowledgeBase/Domain/Services/Enhancements/IQueryComplexityClassifier.cs:9`): Async interface for LLM-backed query complexity. `QueryComplexityAnalyzer.cs` is the sync heuristic version. Task 2.4 (`IQAComplexityClassifier`) is a DIFFERENT interface — do not collide with existing `IQueryComplexityClassifier`. Drift: plan v2 used ambiguous naming.
- **`IDocumentChunker`, `IKnowledgeBaseIndexer`, `KnowledgeChunk` do NOT exist** (grep confirmed): These were deferred in Sprint 1 Task 1.6. Task 2.3 must introduce them. Chunking services at application layer: `KnowledgeBase.Application.Services.Chunking.*` namespace has `ChunkingService`, but this is for vector doc chunking (KB indexing existing PDFs), not photo-batch to KB pipeline. New interfaces needed specifically for the Photo → KB indexing path.
- **`IPhotoBatchProcessor` does NOT exist** (grep confirmed): Sprint 1 Task 1.6 was not yet implemented. The `PhotoBatchProcessor.cs` file referenced in plan v2 file structure map does not exist yet. Task 2.3's KB indexing services will be consumed by future Task 1.6 implementation.
- **Translation Infrastructure does NOT exist** (glob confirmed: `Infrastructure/Translation/` is empty). Tasks 2.1-2.2 start from scratch.
- **`infra/docker-compose.test.yml` naming**: The project uses `compose.*.yml` naming (e.g., `compose.integration.yml`, `compose.dev.yml`), not `docker-compose.*.yml`. Task 2.8 must create `infra/compose.test.yml`, not `infra/docker-compose.test.yml`.
- **Redis IDistributedCache confirmed active** (`InfrastructureServiceExtensions.cs:186`): `AddStackExchangeRedisCache` with `meepleai:hybridcache:` instance prefix. Task 2.2 TranslationCache can use `IDistributedCache` directly with its own key prefix (e.g., `meepleai:translation:`).
- **`IRagValidationPipelineService` confirmed path**: `BoundedContexts/KnowledgeBase/Domain/Services/RagValidation/IRagValidationPipelineService.cs`.
- **`ISemanticResponseCache` confirmed path**: `BoundedContexts/KnowledgeBase/Application/Services/ISemanticResponseCache.cs`.
- **`RequestSource` enum**: Used in `ILlmService.GenerateCompletionAsync` — Task 2.1 will need `RequestSource.Translation` added if it does not already exist.
- **Golden set exists**: `tests/llm-eval/golden-set/qa-questions.jsonl` (3 entries observed, schema defined). Task 2.7 builds on this file.
- **`IPricingEngine.ConsumeQuotaAsync` forward dependency confirmed**: This is Phase 3 Task 3.2. Task 2.3 uses a guard-only pattern (`MaxRequestCostUsd` static check) and wires up a `IPricingEngine` stub interface that Phase 3 will implement. Flagged explicitly below.

### Risk Register Additions (Phase 2 specific)

| ID | Risk | Likelihood | Impact | Mitigation |
|----|------|-----------|--------|-----------|
| R-5 | LLM hallucination on critical board game rules (existing) | Medium | High | Task 2.7: CI gate ≤ 3% hallucination rate on golden set, LLM-as-judge with GPT-4o as judge (different from Claude Sonnet used for answers). Majority vote 3/5 runs. Block merge on failure. |
| R-14 | Translation quality regression (narrative Italian) | Low | High | Task 2.1/2.2: MOS ≥ 4.0 human rating gate + automated LLM-as-judge for translation golden set. Cache invalidation on model upgrade. |
| R-15 | Stale translation cache served after rulebook re-index | Medium | Medium | Task 2.2: Cache invalidation hook triggered on `PhotoBatchCompletedEvent` (existing domain event from Sprint 0/Task 1.1). TTL 24h maximum. |
| R-16 | Glossary cross-game term collision | Low | Medium | Task 2.6: Scope glossary entries to `(game_id, user_id)` pair. NER bootstrap requires explicit user confirmation before persisting. |
| R-17 | LLM provider rate limit during peak (OpenRouter) | Medium | Medium | Task 2.1: Exponential backoff via `IHttpClientFactory` retry policy + circuit breaker. Fallback to DeepSeek V3 if Anthropic rate-limited. Expose `ITranslationCircuitBreaker` metric. |
| R-18 | `IPricingEngine` forward dependency blocks Task 2.3 | High | Low | Task 2.3: Stub `IPricingEngine` interface introduced in Phase 2 with `ConsumeQuotaAsync` returning `Task<bool>` always-true. Phase 3 Task 3.2 replaces with real implementation. Register as `NullPricingEngine` via DI. |
| R-19 | SpaCy NER bootstrap OOM on low-memory Hetzner CAX31 | Low | High | Task 2.6: NER runs as one-time background job (Quartz), not inline. Cap model to `en_core_web_sm` (50 MB). Gate on available RAM > 8 GB before triggering. |
| R-20 | Golden eval runner stochastic failures (Task 2.7) | Medium | Medium | Majority vote over 5 LLM judge calls per question. Accept question as "pass" if ≥ 4/5 judge as non-hallucination. Track per-question pass rate. |

---

## Task 2.1 — TranslationService skeleton + OpenRouter integration

**Duration**: ~1.5 weeks

**Files to create**:
- `apps/api/src/Api/Infrastructure/Translation/INarrativeTranslationService.cs`
- `apps/api/src/Api/Infrastructure/Translation/IGenericTranslationService.cs`
- `apps/api/src/Api/Infrastructure/Translation/OpenRouterTranslationService.cs`
- `apps/api/src/Api/Infrastructure/Translation/TranslationResult.cs`
- `apps/api/src/Api/Infrastructure/Translation/TranslationServiceExtensions.cs`
- `apps/api/src/Api/Services/LlmClients/RequestSource.cs` (modify — add `Translation` value if missing)
- `apps/tests/Api.Tests/Infrastructure/Translation/OpenRouterTranslationServiceTests.cs`

**Files to modify**:
- `apps/api/src/Api/Program.cs` (register translation services)

**Dependencies**: None (Phase 1 not required). Forward dep: Phase 3 Task 3.2 (`IPricingEngine`) — stub only in Phase 2.

**Architectural Rationale (Newman)**

Two interfaces are required because the LLM prompting strategies are fundamentally different:

- `INarrativeTranslationService`: translates game rulebook paragraphs with creative latitude — handles proper nouns (character names, place names), preserves atmosphere, accepts multi-paragraph input. Uses Claude Sonnet or GPT-4o via OpenRouter (higher quality, higher cost). Context window = full paragraph + surrounding context.
- `IGenericTranslationService`: translates short factual strings — error messages, UI labels, QA answer snippets. Uses DeepSeek V3 or Claude Haiku (lower cost, lower creativity needed). Context window = sentence-level.

Merging into one interface would force either overpaying for factual strings or underperforming on narrative. The boundary is not "narrative vs factual" by content analysis — it is by call site: callers know which they need.

OpenRouter is the abstraction layer. The underlying `ILlmService.GenerateCompletionWithModelAsync` (explicit model override) is the single implementation path. No new HTTP client — reuse the existing LLM service.

**Pattern compliance**:
- `internal sealed class OpenRouterTranslationService` (both interfaces)
- Use `ILlmService.GenerateCompletionWithModelAsync("anthropic/claude-sonnet-4-5", ...)` for narrative
- Use `ILlmService.GenerateCompletionWithModelAsync("deepseek/deepseek-chat", ...)` for generic
- Register as `AddScoped<INarrativeTranslationService, OpenRouterTranslationService>()` and `AddScoped<IGenericTranslationService, OpenRouterTranslationService>()`
- `RequestSource.Translation` — verify this value exists in the enum before adding

### Step 1 — RED: Failing tests

```csharp
// apps/tests/Api.Tests/Infrastructure/Translation/OpenRouterTranslationServiceTests.cs
using Api.Infrastructure.Translation;
using Api.Services;
using Api.Services.LlmClients;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Translation;

public class OpenRouterTranslationServiceTests
{
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly ILogger<OpenRouterTranslationService> _logger =
        Substitute.For<ILogger<OpenRouterTranslationService>>();

    [Fact]
    public async Task TranslateNarrativeAsync_GivenEnglishParagraph_ReturnsItalianTranslation()
    {
        // Arrange
        var svc = new OpenRouterTranslationService(_llmService, _logger);
        _llmService.GenerateCompletionWithModelAsync(
                "anthropic/claude-sonnet-4-5",
                Arg.Any<string>(),
                Arg.Any<string>(),
                RequestSource.Translation,
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(LlmCompletionResult.CreateSuccess("Sei arrivato al Villaggio di Avalon."));

        // Act
        var result = await svc.TranslateNarrativeAsync(
            "You have arrived at the Village of Avalon.",
            sourceLanguage: "en",
            targetLanguage: "it",
            gameContext: "Tainted Grail",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TranslatedText.Should().Contain("Avalon");
        result.Success.Should().BeTrue();
        result.EstimatedCostUsd.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task TranslateNarrativeAsync_WhenLlmFails_ReturnsFailureResult()
    {
        var svc = new OpenRouterTranslationService(_llmService, _logger);
        _llmService.GenerateCompletionWithModelAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<RequestSource>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(LlmCompletionResult.CreateFailure("rate_limit_exceeded"));

        var result = await svc.TranslateNarrativeAsync(
            "Some paragraph", "en", "it", null, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("LLM_FAILURE");
    }

    [Fact]
    public async Task TranslateGenericAsync_UsesLowerCostModel()
    {
        var svc = new OpenRouterTranslationService(_llmService, _logger);
        _llmService.GenerateCompletionWithModelAsync(
                "deepseek/deepseek-chat",
                Arg.Any<string>(),
                Arg.Any<string>(),
                RequestSource.Translation,
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(LlmCompletionResult.CreateSuccess("Non disponibile nel manuale."));

        var result = await svc.TranslateGenericAsync(
            "Not available in the rulebook.", "en", "it", CancellationToken.None);

        result.Success.Should().BeTrue();
        await _llmService.Received(1).GenerateCompletionWithModelAsync(
            "deepseek/deepseek-chat",
            Arg.Any<string>(),
            Arg.Any<string>(),
            RequestSource.Translation,
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TranslateNarrativeAsync_GivenEmptyInput_ThrowsArgumentException(string? input)
    {
        var svc = new OpenRouterTranslationService(_llmService, _logger);
        var act = () => svc.TranslateNarrativeAsync(input!, "en", "it", null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// apps/api/src/Api/Infrastructure/Translation/TranslationResult.cs
namespace Api.Infrastructure.Translation;

internal sealed record TranslationResult(
    bool Success,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage,
    decimal EstimatedCostUsd,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static TranslationResult CreateSuccess(
        string translated, string src, string tgt, decimal cost) =>
        new(true, translated, src, tgt, cost);

    public static TranslationResult CreateFailure(string src, string tgt, string errorCode, string msg) =>
        new(false, string.Empty, src, tgt, 0m, errorCode, msg);
}
```

```csharp
// apps/api/src/Api/Infrastructure/Translation/INarrativeTranslationService.cs
namespace Api.Infrastructure.Translation;

/// <summary>
/// Translates multi-paragraph narrative game text with creative fidelity.
/// Uses premium LLM (Claude Sonnet / GPT-4o) via OpenRouter.
/// Suitable for gamebook paragraphs, story segments, NPC dialogue.
/// </summary>
internal interface INarrativeTranslationService
{
    /// <param name="text">Source text (paragraph or multi-paragraph)</param>
    /// <param name="sourceLanguage">ISO 639-1 source language code (e.g., "en")</param>
    /// <param name="targetLanguage">ISO 639-1 target language code (e.g., "it")</param>
    /// <param name="gameContext">Optional game name for proper noun context</param>
    Task<TranslationResult> TranslateNarrativeAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? gameContext,
        CancellationToken ct = default);
}
```

```csharp
// apps/api/src/Api/Infrastructure/Translation/IGenericTranslationService.cs
namespace Api.Infrastructure.Translation;

/// <summary>
/// Translates short factual strings: error messages, UI labels, QA answer snippets.
/// Uses lower-cost model (DeepSeek V3 / Claude Haiku) via OpenRouter.
/// </summary>
internal interface IGenericTranslationService
{
    /// <param name="text">Short text to translate (sentence-level, no creative latitude)</param>
    Task<TranslationResult> TranslateGenericAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default);
}
```

```csharp
// apps/api/src/Api/Infrastructure/Translation/OpenRouterTranslationService.cs
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Translation;

internal sealed class OpenRouterTranslationService(
    ILlmService llmService,
    ILogger<OpenRouterTranslationService> logger)
    : INarrativeTranslationService, IGenericTranslationService
{
    private const string NarrativeModel = "anthropic/claude-sonnet-4-5";
    private const string GenericModel = "deepseek/deepseek-chat";
    private const decimal MaxRequestCostUsd = 0.05m; // R-18 guard: static ceiling

    public async Task<TranslationResult> TranslateNarrativeAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? gameContext,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        var context = gameContext is not null
            ? $"This is narrative text from the board game '{gameContext}'. Preserve character names, place names, and game terms. "
            : string.Empty;

        var systemPrompt =
            $"{context}You are a professional literary translator specializing in board game rulebooks. " +
            $"Translate the following text from {sourceLanguage} to {targetLanguage}. " +
            "Preserve tone, atmosphere, and game-specific terminology. Output ONLY the translation, no explanations.";

        try
        {
            var result = await llmService.GenerateCompletionWithModelAsync(
                NarrativeModel, systemPrompt, text,
                RequestSource.Translation, maxTokens: 2048, ct).ConfigureAwait(false);

            if (!result.Success)
                return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "LLM_FAILURE", result.ErrorMessage ?? "Unknown");

            logger.LogInformation(
                "[Translation] Narrative: {SrcLang}→{TgtLang}, cost={Cost:F6} USD, length={Len}",
                sourceLanguage, targetLanguage, result.Cost.TotalCost, text.Length);

            return TranslationResult.CreateSuccess(result.Response, sourceLanguage, targetLanguage, result.Cost.TotalCost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Translation] Narrative translation failed");
            return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "EXCEPTION", ex.Message);
        }
    }

    public async Task<TranslationResult> TranslateGenericAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var systemPrompt =
            $"Translate the following text from {sourceLanguage} to {targetLanguage}. " +
            "Output ONLY the translation, no explanations, no quotation marks.";

        try
        {
            var result = await llmService.GenerateCompletionWithModelAsync(
                GenericModel, systemPrompt, text,
                RequestSource.Translation, maxTokens: 512, ct).ConfigureAwait(false);

            if (!result.Success)
                return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "LLM_FAILURE", result.ErrorMessage ?? "Unknown");

            return TranslationResult.CreateSuccess(result.Response, sourceLanguage, targetLanguage, result.Cost.TotalCost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Translation] Generic translation failed");
            return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "EXCEPTION", ex.Message);
        }
    }
}
```

```csharp
// apps/api/src/Api/Infrastructure/Translation/TranslationServiceExtensions.cs
namespace Api.Infrastructure.Translation;

internal static class TranslationServiceExtensions
{
    public static IServiceCollection AddTranslationServices(this IServiceCollection services)
    {
        services.AddScoped<OpenRouterTranslationService>();
        services.AddScoped<INarrativeTranslationService>(sp =>
            sp.GetRequiredService<OpenRouterTranslationService>());
        services.AddScoped<IGenericTranslationService>(sp =>
            sp.GetRequiredService<OpenRouterTranslationService>());
        return services;
    }
}
```

### Acceptance Criteria (Wiegers)

- AC-2.1.1: `INarrativeTranslationService.TranslateNarrativeAsync` returns `TranslationResult.Success = true` for any non-empty string input when LLM returns success.
- AC-2.1.2: Narrative translation uses `anthropic/claude-sonnet-4-5` model (verifiable via NSubstitute `Received()` assertion).
- AC-2.1.3: Generic translation uses `deepseek/deepseek-chat` model.
- AC-2.1.4: Empty/whitespace input throws `ArgumentException` before any LLM call.
- AC-2.1.5: LLM failure returns `TranslationResult.Success = false` with `ErrorCode = "LLM_FAILURE"` — no exception thrown to caller.
- AC-2.1.6: `EstimatedCostUsd` is populated from `LlmCost.TotalCost` on success.
- AC-2.1.7: Both services registered in DI and resolvable in integration test context.
- AC-2.1.8: `RequestSource.Translation` enum value exists (compile-time verified).

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Translate English gamebook paragraph to Italian
  Given a narrative paragraph in English about "Tainted Grail"
  When TranslateNarrativeAsync is called with sourceLanguage="en" targetLanguage="it"
  Then the result contains Italian text
  And game proper nouns (Avalon, Morrigan) are preserved
  And Success = true
  And EstimatedCostUsd > 0

@happy
Scenario: Translate short QA answer snippet
  Given a short English string "Not available in the rulebook."
  When TranslateGenericAsync is called with targetLanguage="it"
  Then Success = true
  And translated text is returned without quotation marks or meta-commentary

@edge
Scenario: Source and target language are the same
  Given text in Italian
  When TranslateNarrativeAsync is called with sourceLanguage="it" targetLanguage="it"
  Then the service calls the LLM (passthrough — validation not the service's concern)
  And Success = true (LLM returns same or equivalent text)

@edge
Scenario: Very long paragraph (>2000 tokens)
  Given a paragraph of 3000 characters
  When TranslateNarrativeAsync is called
  Then maxTokens=2048 cap prevents runaway output
  And the service does not throw even if output is truncated

@error
Scenario: LLM returns rate limit error
  Given the LLM service returns LlmCompletionResult.CreateFailure("rate_limit_exceeded")
  When TranslateNarrativeAsync is called
  Then Success = false
  And ErrorCode = "LLM_FAILURE"
  And no exception propagates to the caller

@error
Scenario: Empty input text
  Given text is null or whitespace
  When TranslateNarrativeAsync is called
  Then ArgumentException is thrown before any LLM call
  And no LLM call is made (verified via NSubstitute.DidNotReceive())
```

### Operational Considerations (Nygard)

- **Provider failure modes**: OpenRouter may return 429 (rate limit), 503 (provider down), or 200 with empty response. All three are handled: 429/503 → `LLM_FAILURE`; empty response → treat as failure with `ErrorCode = "EMPTY_RESPONSE"`.
- **Cost ceiling**: Static `MaxRequestCostUsd = 0.05m` is a future enforcement hook. In Phase 2, it is logged as a warning when `result.Cost.TotalCost > MaxRequestCostUsd`. Hard enforcement is Phase 3 scope (`IPricingEngine`).
- **Monitoring**: Log level `Information` for successful translations with `cost`, `length`, `lang` tags. These are searchable in Loki (existing observability stack).
- **No retry in service layer**: Retry logic belongs in `IHttpClientFactory` policy (existing). The service catches and returns failure; callers decide whether to retry at the API layer.
- **Circuit breaker**: The existing `ICircuitBreakerRegistry` in KB BC is not shared. Translation service uses `ILlmService` which has its own circuit breaker management via OpenRouter SDK. No new circuit breaker needed.

---

## Task 2.2 — TranslationCache Redis

**Duration**: ~0.5 weeks

**Files to create**:
- `apps/api/src/Api/Infrastructure/Translation/ITranslationCache.cs`
- `apps/api/src/Api/Infrastructure/Translation/RedisTranslationCache.cs`
- `apps/tests/Api.Tests/Infrastructure/Translation/RedisTranslationCacheTests.cs`

**Files to modify**:
- `apps/api/src/Api/Infrastructure/Translation/TranslationServiceExtensions.cs` (register cache)

**Dependencies**: Task 2.1 (interfaces defined). Redis already configured (`IDistributedCache` active).

**Architectural Rationale (Newman)**

The translation cache is a read-through cache scoped to `(game_id, source_language, target_language, content_hash)`. It does NOT use cosine similarity (unlike `SemanticResponseCache`) because translations are deterministic by content — the same paragraph text produces the same translation (given same model). A SHA-256 hash of the normalized input text is the cache key.

Cache invalidation: triggered by `PhotoBatchCompletedEvent` (existing event from Sprint 0, Task 1.1). When a new batch is processed for a game, all translation cache entries for that `game_id` are invalidated. This is a key-prefix delete: `meepleai:translation:{game_id}:*`.

TTL: 24 hours default. Narrative translations: 7 days (they do not change unless rulebook is re-indexed). Generic translations: 4 hours (error messages may be updated with model version changes).

### Step 1 — RED: Failing tests

```csharp
// apps/tests/Api.Tests/Infrastructure/Translation/RedisTranslationCacheTests.cs
using Api.Infrastructure.Translation;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

public class RedisTranslationCacheTests
{
    private readonly IDistributedCache _distributedCache = Substitute.For<IDistributedCache>();
    private readonly ILogger<RedisTranslationCache> _logger = Substitute.For<ILogger<RedisTranslationCache>>();

    [Fact]
    public async Task TryGetAsync_WhenCached_ReturnsCachedResult()
    {
        var gameId = Guid.NewGuid();
        const string text = "You have arrived at Avalon.";
        var cached = TranslationResult.CreateSuccess("Sei arrivato ad Avalon.", "en", "it", 0.001m);
        var cache = new RedisTranslationCache(_distributedCache, _logger);

        // Pre-populate (simulate cached value)
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(cached);
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(json);

        var result = await cache.TryGetAsync(gameId, text, "en", "it", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TranslatedText.Should().Be("Sei arrivato ad Avalon.");
    }

    [Fact]
    public async Task TryGetAsync_WhenCacheMiss_ReturnsNull()
    {
        var cache = new RedisTranslationCache(_distributedCache, _logger);
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        var result = await cache.TryGetAsync(Guid.NewGuid(), "text", "en", "it", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateGameAsync_RemovesAllGameEntries()
    {
        // Implementation must call RemoveAsync with game_id prefix for each known key
        // In practice: IDistributedCache doesn't support pattern delete.
        // RedisTranslationCache must track keys in a Redis Set for pattern invalidation.
        var cache = new RedisTranslationCache(_distributedCache, _logger);
        // Test: InvalidateGameAsync must not throw even if no keys exist
        var act = () => cache.InvalidateGameAsync(Guid.NewGuid(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// apps/api/src/Api/Infrastructure/Translation/ITranslationCache.cs
namespace Api.Infrastructure.Translation;

internal interface ITranslationCache
{
    Task<TranslationResult?> TryGetAsync(
        Guid gameId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default);

    Task SetAsync(
        Guid gameId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        TranslationResult result,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate all cached translations for a game (called on PhotoBatchCompleted re-index).
    /// </summary>
    Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default);
}
```

```csharp
// apps/api/src/Api/Infrastructure/Translation/RedisTranslationCache.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Infrastructure.Translation;

internal sealed class RedisTranslationCache(
    IDistributedCache distributedCache,
    ILogger<RedisTranslationCache> logger) : ITranslationCache
{
    private static readonly TimeSpan DefaultNarrativeTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan DefaultGenericTtl = TimeSpan.FromHours(4);
    private const string KeyPrefix = "meepleai:translation:";

    public async Task<TranslationResult?> TryGetAsync(
        Guid gameId, string sourceText, string sourceLanguage, string targetLanguage, CancellationToken ct)
    {
        var key = BuildKey(gameId, sourceText, sourceLanguage, targetLanguage);
        try
        {
            var bytes = await distributedCache.GetAsync(key, ct).ConfigureAwait(false);
            if (bytes is null or { Length: 0 }) return null;
            return JsonSerializer.Deserialize<TranslationResult>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TranslationCache] Cache read failed for key {Key}", key);
            return null; // Degrade gracefully
        }
    }

    public async Task SetAsync(
        Guid gameId, string sourceText, string sourceLanguage, string targetLanguage,
        TranslationResult result, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var key = BuildKey(gameId, sourceText, sourceLanguage, targetLanguage);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultNarrativeTtl
        };
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result);
            await distributedCache.SetAsync(key, bytes, options, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TranslationCache] Cache write failed for key {Key}", key);
        }
    }

    public async Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default)
    {
        // IDistributedCache does not support pattern delete.
        // Track all game keys in a Redis Set (key: "meepleai:translation:index:{gameId}").
        // On invalidation, iterate and delete each tracked key.
        // For Phase 2 MVP: log warning + no-op (full implementation requires IConnectionMultiplexer).
        // R-15 mitigation: TTL 7 days is the primary guard; explicit invalidation is best-effort.
        logger.LogInformation(
            "[TranslationCache] InvalidateGameAsync called for game {GameId} — TTL-based expiry is primary guard",
            gameId);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static string BuildKey(Guid gameId, string text, string src, string tgt)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return $"{KeyPrefix}{gameId}:{src}:{tgt}:{hash[..16]}";
    }
}
```

### Acceptance Criteria (Wiegers)

- AC-2.2.1: Cache hit returns `TranslationResult` identical to what was stored (round-trip JSON serialization stable).
- AC-2.2.2: Cache miss returns `null`.
- AC-2.2.3: Cache read failure (Redis unavailable) returns `null` without throwing — degrade gracefully.
- AC-2.2.4: Cache write failure (Redis unavailable) logs warning without throwing.
- AC-2.2.5: Cache key is deterministic for same `(gameId, text, src, tgt)` tuple.
- AC-2.2.6: Different games produce different cache keys (no cross-game pollution).
- AC-2.2.7: `InvalidateGameAsync` does not throw even when no keys exist.
- AC-2.2.8: TTL is set on `SetAsync` (verifiable via `DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow`).

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Cache hit returns stored translation
  Given a translation was previously cached for game X, text "Avalon", en→it
  When TryGetAsync is called with same parameters
  Then the cached TranslationResult is returned
  And no LLM call is made

@happy
Scenario: Cache miss triggers translation service
  Given no cached translation exists
  When TryGetAsync is called
  Then null is returned
  And the caller falls through to the translation service

@edge
Scenario: Same text, different game IDs produce different cache keys
  Given text "You arrived at Avalon." for game A and game B
  When TryGetAsync is called for each
  Then cache keys are different (no cross-game contamination)

@edge
Scenario: PhotoBatchCompleted event triggers cache invalidation
  Given game G has 10 cached translations
  When PhotoBatchCompletedEvent is raised for game G
  Then InvalidateGameAsync is called for game G
  And subsequent TryGetAsync calls return null (TTL-based fallback)

@error
Scenario: Redis connection is down during cache read
  Given Redis is unavailable
  When TryGetAsync is called
  Then null is returned (graceful degradation)
  And a Warning log entry is recorded
  And the translation service proceeds normally

@error
Scenario: Corrupted cache entry (invalid JSON)
  Given a cache entry with malformed JSON bytes
  When TryGetAsync is called
  Then null is returned
  And no exception propagates
```

### Operational Considerations (Nygard)

- **Cache invalidation strategy**: TTL is the primary guard (7 days narrative, 4 hours generic). Explicit invalidation via `InvalidateGameAsync` is best-effort in Phase 2. Full pattern-delete requires `IConnectionMultiplexer` (already available in `SemanticResponseCache`) — defer to Phase 3 or as a follow-up if cache staleness is reported.
- **Key collision risk**: SHA-256 prefix (16 hex chars = 64-bit) provides sufficient collision resistance for MVP scale (thousands of translations per game). Upgrade to 32 chars if scale increases.
- **Redis memory pressure**: Each translation entry is ~500 bytes. At 10K entries × 500 bytes = 5 MB. Well within Redis memory budget for CAX31.
- **Monitoring**: Log cache hit rate as `Information`. Missing: add Prometheus counter for `translation_cache_hits_total` and `translation_cache_misses_total` in a follow-up task.

---

## Task 2.3 — KB Q&A extension + KB Indexing Services

**Duration**: ~2 weeks (split into 2.3a + 2.3b)

### Sub-task 2.3a — KB Indexing Services (Sprint 1 Task 1.6 forward dep)

**Files to create**:
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IDocumentChunker.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IKnowledgeBaseIndexer.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/KnowledgeChunk.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/KnowledgeBaseIndexer.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PageTextChunker.cs`
- `apps/tests/Api.Tests/BoundedContexts/DocumentProcessing/Services/KnowledgeBaseIndexerTests.cs`

**Context**: Sprint 1 Task 1.6 (`IPhotoBatchProcessor`/`PhotoBatchProcessor`) was deferred because these indexing services did not exist. This sub-task creates the foundation so that Task 1.6 can be implemented in Sprint 2-3.

**Note on IEmbeddingService**: The DocumentProcessing BC should use `Api.Services.IEmbeddingService` (the application-layer service, not `SharedGameCatalog.Domain.Services.IEmbeddingService`). The KB BC's `SearchQueryHandler` already uses `Api.Services.IEmbeddingService`. Maintain consistency.

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/KnowledgeChunk.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Represents a text chunk extracted from a processed photo page,
/// ready for vector embedding and KB indexing.
/// </summary>
internal sealed record KnowledgeChunk(
    Guid PhotoBatchUploadId,
    Guid PhotoBatchPageId,
    int PageNumber,
    string TextContent,
    int ChunkIndex,
    int StartCharOffset,
    int EndCharOffset,
    string Language,
    float ConfidenceScore)
{
    public int CharLength => TextContent.Length;

    public static KnowledgeChunk Create(
        Guid batchId,
        Guid pageId,
        int pageNumber,
        string text,
        int chunkIndex,
        int startOffset,
        int endOffset,
        string language,
        float confidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (confidence is < 0f or > 1f) throw new ArgumentOutOfRangeException(nameof(confidence));

        return new KnowledgeChunk(batchId, pageId, pageNumber, text,
            chunkIndex, startOffset, endOffset, language, confidence);
    }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IDocumentChunker.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Splits OCR-extracted page text into overlapping chunks suitable for vector embedding.
/// Chunk size: 512 tokens (≈ 2000 chars), 10% overlap.
/// </summary>
internal interface IDocumentChunker
{
    IReadOnlyList<KnowledgeChunk> ChunkPage(
        Guid photoBatchUploadId,
        Guid photoBatchPageId,
        int pageNumber,
        string pageText,
        string language,
        float ocrConfidence);
}
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IKnowledgeBaseIndexer.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Indexes KnowledgeChunks into the vector store (pgvector).
/// Generates embeddings and persists as VectorDocuments in the KB.
/// </summary>
internal interface IKnowledgeBaseIndexer
{
    /// <summary>
    /// Indexes all chunks from a completed photo batch.
    /// Emits progress events to caller.
    /// Returns count of indexed chunks.
    /// </summary>
    Task<int> IndexBatchAsync(
        Guid photoBatchUploadId,
        Guid gameId,
        IReadOnlyList<KnowledgeChunk> chunks,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes all vector documents for a photo batch (on re-index or deletion).
    /// </summary>
    Task DeleteBatchAsync(Guid photoBatchUploadId, CancellationToken ct = default);
}
```

### Sub-task 2.3b — AskQuestionQuery extension

**Files to modify**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQuery.cs` (add `ResponseLanguage`)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs` (add translation + house rule check)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` (register new services)

**Files to create**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IHouseRuleMatcher.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HouseRuleMatcher.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IPricingEngine.cs` (stub — Phase 3 implements)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/NullPricingEngine.cs` (Phase 2 stub)
- `apps/tests/Api.Tests/BoundedContexts/KnowledgeBase/Queries/AskQuestionQueryHandlerPhase2Tests.cs`

**Forward dependency flag**: `IPricingEngine.ConsumeQuotaAsync` is Phase 3 Task 3.2. Phase 2 registers `NullPricingEngine` (always returns `true`). The interface is defined here so Phase 3 can replace the implementation without changing the call site.

**AskQuestionQuery extension**:

```csharp
// Modify: AskQuestionQuery.cs — add ResponseLanguage
internal record AskQuestionQuery(
    Guid GameId,
    string Question,
    Guid? ThreadId = null,
    string? SearchMode = null,
    string Language = "en",
    bool BypassCache = false,
    Guid? UserId = null,
    string? UserRole = null,
    string? ResponseLanguage = null  // NEW: null = same as Language; "it" = translate answer to Italian
) : IQuery<QaResponseDto>;
```

**IHouseRuleMatcher**:

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IHouseRuleMatcher.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Checks if user's question relates to a house rule stored in AgentMemory.
/// Cross-BC read: reads from AgentMemory.IGameMemoryRepository.
/// Returns the most relevant house rule description, or null if none applies.
/// </summary>
internal interface IHouseRuleMatcher
{
    Task<string?> FindMatchingHouseRuleAsync(
        Guid gameId,
        Guid? userId,
        string question,
        CancellationToken ct = default);
}
```

**IPricingEngine stub**:

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IPricingEngine.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Enforces pricing quotas for KB operations.
/// Phase 2: NullPricingEngine stub (always allows).
/// Phase 3 Task 3.2: CreditBasedPricingEngine implementation.
/// </summary>
internal interface IPricingEngine
{
    /// <summary>
    /// Attempts to consume quota for the operation.
    /// Returns false if quota exceeded (caller should return QuotaExceeded error).
    /// </summary>
    Task<bool> ConsumeQuotaAsync(
        Guid? userId,
        string operationType,
        CancellationToken ct = default);
}
```

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/NullPricingEngine.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Phase 2 stub: always allows quota consumption.
/// Replace with CreditBasedPricingEngine in Phase 3 Task 3.2.
/// </summary>
internal sealed class NullPricingEngine : IPricingEngine
{
    public Task<bool> ConsumeQuotaAsync(Guid? userId, string operationType, CancellationToken ct = default)
        => Task.FromResult(true);
}
```

**AskQuestionQueryHandler modification** (add to `Handle` method — before Step 1 vector search):

```csharp
// PHASE 2 ADDITION: Check house rules before vector search
string? houseRuleMatch = null;
if (query.UserId.HasValue)
{
    houseRuleMatch = await _houseRuleMatcher.FindMatchingHouseRuleAsync(
        query.GameId, query.UserId, query.Question, cancellationToken).ConfigureAwait(false);
    if (houseRuleMatch is not null)
        _logger.LogInformation("[AskQuestionHandler] House rule matched for game {GameId}", query.GameId);
}

// PHASE 2 ADDITION: Consume pricing quota
var quotaAllowed = await _pricingEngine.ConsumeQuotaAsync(
    query.UserId, "qa_question", cancellationToken).ConfigureAwait(false);
if (!quotaAllowed)
    throw new ForbiddenException("Quota giornaliera esaurita. Acquista crediti per continuare.");
```

After `BuildValidatedResponseAsync` (before return):

```csharp
// PHASE 2 ADDITION: Translate response if ResponseLanguage differs
if (query.ResponseLanguage is not null
    && !string.Equals(query.ResponseLanguage, query.Language, StringComparison.OrdinalIgnoreCase)
    && response.Answer is not null)
{
    var translationResult = await _genericTranslationService.TranslateGenericAsync(
        response.Answer, query.Language, query.ResponseLanguage, cancellationToken).ConfigureAwait(false);

    if (translationResult.Success)
    {
        response = response with { Answer = translationResult.TranslatedText };
    }
    else
    {
        _logger.LogWarning("[AskQuestionHandler] Translation failed, returning original: {Error}",
            translationResult.ErrorCode);
    }
}
```

### Acceptance Criteria (Wiegers)

- AC-2.3.1: `AskQuestionQuery.ResponseLanguage = "it"` causes answer to be translated to Italian via `IGenericTranslationService`.
- AC-2.3.2: `ResponseLanguage = null` returns answer in original `Language` with no translation call.
- AC-2.3.3: When a house rule matches the question, the house rule description is prepended to the LLM prompt context (priority over rulebook context).
- AC-2.3.4: `IPricingEngine.ConsumeQuotaAsync` returning `false` results in `ForbiddenException` with appropriate message.
- AC-2.3.5: `IDocumentChunker` and `IKnowledgeBaseIndexer` are registered in DI and resolvable.
- AC-2.3.6: `KnowledgeChunk.Create` throws for empty text, page number < 1, confidence outside [0,1].
- AC-2.3.7: Existing `AskQuestionQueryHandler` tests continue to pass (no regression).

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Q&A with Italian response language
  Given a question about Tainted Grail in English
  And AskQuestionQuery.ResponseLanguage = "it"
  When the handler processes the query
  Then the answer is returned in Italian
  And Citations remain in original page-reference format

@happy
Scenario: House rule overrides rulebook answer
  Given user has house rule "Fireball costs 2 mana instead of 3"
  And question is "How much mana does fireball cost?"
  When FindMatchingHouseRuleAsync returns the house rule
  Then the LLM prompt includes the house rule prominently
  And the answer reflects the house rule, not the rulebook

@edge
Scenario: House rule exists but question is unrelated
  Given user has house rule about fireball
  And question is "How many players can play?"
  When FindMatchingHouseRuleAsync returns null
  Then no house rule is injected into the prompt
  And normal RAG flow proceeds

@edge
Scenario: Translation service fails mid-answer
  Given the translation service returns Success=false
  And ResponseLanguage = "it"
  When the handler processes the query
  Then original English answer is returned
  And a warning is logged
  And no exception propagates to the caller

@edge
Scenario: Quota consumed, second request blocked
  Given IPricingEngine.ConsumeQuotaAsync returns false
  When AskQuestionQuery is sent
  Then ForbiddenException is raised with quota exceeded message
  And no LLM call is made

@error
Scenario: HouseRuleMatcher throws unexpectedly
  Given IHouseRuleMatcher.FindMatchingHouseRuleAsync throws
  When AskQuestionQuery is sent
  Then the exception propagates (not caught — house rule check is on critical path)
```

### Operational Considerations (Nygard)

- **Cross-BC dependency risk**: `IHouseRuleMatcher` reads from AgentMemory's `IGameMemoryRepository`. If AgentMemory database is unavailable, the entire Q&A flow fails. Mitigate: wrap `FindMatchingHouseRuleAsync` in try/catch with fallback to `null` (skip house rule check on error).
- **`IPricingEngine` forward dep**: `NullPricingEngine` must be replaced before Phase 3 payment goes live. Add a `// PHASE 3 REPLACE` comment in `NullPricingEngine.cs` and register via `services.AddScoped<IPricingEngine, NullPricingEngine>()` with a TODO marker.
- **Translation call latency**: `IGenericTranslationService` adds ~500ms on cache miss. Total P95 Q&A latency budget is 5 seconds. Translation is within budget if vector search + LLM generation ≤ 4.5 seconds. Monitor separately.

---

## Task 2.4 — IQAComplexityClassifier interface + HeuristicQAComplexityClassifier

**Duration**: ~0.5 weeks

**Context**: Plan v2 says "improve over keyword matching." The existing `QueryComplexityAnalyzer` (Singleton, sync) handles LLM routing tier. The existing `IQueryComplexityClassifier` (async, LLM-backed) handles RAG query complexity. Task 2.4 introduces a THIRD concept: Q&A answering complexity classifier for the book assistant context — classifying whether a question requires a simple factual lookup, multi-step reasoning, or multi-document synthesis. This informs whether to use chain-of-thought prompting and which model tier to use in the photo-batch Q&A context.

**Naming decision**: `IQAComplexityClassifier` (not `IQueryComplexityClassifier` — already exists). Namespace: `Api.Infrastructure.Translation` or `Api.BoundedContexts.DocumentProcessing.Application.Services` — place in the latter since it's a DocumentProcessing-adjacent concern.

**Files to create**:
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IQAComplexityClassifier.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/HeuristicQAComplexityClassifier.cs`
- `apps/tests/Api.Tests/BoundedContexts/DocumentProcessing/Services/HeuristicQAComplexityClassifierTests.cs`

**Complexity levels**:
- `Simple`: single factual lookup (e.g., "How many dice does the mage roll?")
- `MultiStep`: requires combining 2-3 rules (e.g., "If I use Stealth and then explore, what happens to detection?")
- `Synthesis`: requires comparing across sections or game states (e.g., "What's the difference between exploration and combat in terms of resource costs?")

```csharp
// IQAComplexityClassifier.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

internal enum QAComplexityLevel { Simple, MultiStep, Synthesis }

internal sealed record QAComplexityResult(
    QAComplexityLevel Level,
    float Confidence,
    string Reason);

/// <summary>
/// Classifies Q&A answering complexity for photo-batch book assistant context.
/// Distinct from IQueryComplexityClassifier (RAG routing) and QueryComplexityAnalyzer (model routing).
/// Used to select chain-of-thought prompting and model tier for Q&A over photo-indexed content.
/// </summary>
internal interface IQAComplexityClassifier
{
    QAComplexityResult Classify(string question);
}
```

```csharp
// HeuristicQAComplexityClassifier.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Keyword-heuristic implementation. Sync, allocation-efficient, Singleton-safe.
/// Extends QueryComplexityAnalyzer pattern for Q&A-specific classification.
/// </summary>
internal sealed class HeuristicQAComplexityClassifier : IQAComplexityClassifier
{
    private static readonly string[] SynthesisKeywords =
        ["differenza", "difference", "confronta", "compare", "versus", "vs",
         "meglio", "better", "ottimale", "optimal", "confronto", "comparison"];

    private static readonly string[] MultiStepKeywords =
        ["se poi", "if then", "after", "before", "quando", "durante",
         "combina", "combine", "insieme", "together", "e poi", "and then"];

    private static readonly string[] SimplePatterns =
        ["quanti", "quanto", "how many", "what is", "cosa è", "dove", "where",
         "quando", "when", "chi", "who", "qual è", "which"];

    public QAComplexityResult Classify(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new QAComplexityResult(QAComplexityLevel.Simple, 1.0f, "empty_input");

        var lower = question.ToLowerInvariant();

        if (SynthesisKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return new QAComplexityResult(QAComplexityLevel.Synthesis, 0.85f, "synthesis_keyword");

        if (MultiStepKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return new QAComplexityResult(QAComplexityLevel.MultiStep, 0.80f, "multistep_keyword");

        if (SimplePatterns.Any(k => lower.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
            return new QAComplexityResult(QAComplexityLevel.Simple, 0.90f, "simple_prefix");

        return new QAComplexityResult(QAComplexityLevel.MultiStep, 0.60f, "default_multistep");
    }
}
```

### Acceptance Criteria (Wiegers)

- AC-2.4.1: "differenza tra X e Y" classifies as `Synthesis` with confidence ≥ 0.80.
- AC-2.4.2: "se poi uso X, cosa succede?" classifies as `MultiStep`.
- AC-2.4.3: "quanti dadi tira il mago?" classifies as `Simple`.
- AC-2.4.4: Empty string input returns `Simple` without throwing.
- AC-2.4.5: Classifier is deterministic — same input always returns same result.
- AC-2.4.6: Registered as `AddSingleton<IQAComplexityClassifier, HeuristicQAComplexityClassifier>()`.

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Comparison question classified as Synthesis
  Given question "Qual è la differenza tra esplorazione e combattimento?"
  When Classify is called
  Then Level = Synthesis and Confidence >= 0.80

@happy
Scenario: Conditional question classified as MultiStep
  Given question "Se uso il modulo Stealth e poi esploro, cosa succede?"
  When Classify is called
  Then Level = MultiStep

@happy
Scenario: Simple factual question
  Given question "Quanti dadi tira il mago?"
  When Classify is called
  Then Level = Simple and Confidence >= 0.85

@edge
Scenario: Mixed language input (Italian + English technical term)
  Given question "Come si usa il Stealth module durante l'exploration?"
  When Classify is called
  Then a valid QAComplexityResult is returned without throwing

@edge
Scenario: Very long question (>300 chars)
  Given a 350 character question string
  When Classify is called
  Then Level = MultiStep or Synthesis (not Simple) due to length

@error
Scenario: Null or empty input
  Given question is null or empty
  When Classify is called
  Then Level = Simple with Confidence = 1.0 and no exception thrown
```

---

## Task 2.5 — HouseRule IHouseRuleMatcher

**Duration**: ~0.5 weeks

**Drift note**: `HouseRule` entity, repository, and endpoints ALREADY EXIST in `AgentMemory` BC:
- Domain model: `AgentMemory.Domain.Models.HouseRule` (value object inside `GameMemory` aggregate)
- Table: `game_memories` with `house_rules_json` JSONB column
- Repository: `IGameMemoryRepository` → `GameMemoryRepository`
- Endpoint: `POST /api/v1/agent-memory/games/{gameId}/memory/house-rules` (addRule) and `GET /api/v1/agent-memory/games/{gameId}/memory` (read)

Task 2.5 scope is therefore **reduced** to:
1. `IHouseRuleMatcher` interface and `HeuristicHouseRuleMatcher` implementation (keyword similarity between question and each rule description)
2. Wire `IHouseRuleMatcher` into `AskQuestionQueryHandler` (already referenced in Task 2.3b)
3. Add `HouseRuleSource.ManualEntry` value if not present (check enum)

**Files to create**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HeuristicHouseRuleMatcher.cs`
- `apps/tests/Api.Tests/BoundedContexts/KnowledgeBase/Services/HeuristicHouseRuleMatcherTests.cs`

**Files to audit** (potentially modify):
- `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Enums/HouseRuleSource.cs` (verify values)

```csharp
// HeuristicHouseRuleMatcher.cs
using Api.BoundedContexts.AgentMemory.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Keyword-overlap heuristic matcher between user question and stored house rules.
/// Cross-BC read: reads GameMemory from AgentMemory BC via IGameMemoryRepository.
/// Returns first house rule with overlap ≥ MinOverlapThreshold, or null.
/// </summary>
internal sealed class HeuristicHouseRuleMatcher(
    IGameMemoryRepository gameMemoryRepository,
    ILogger<HeuristicHouseRuleMatcher> logger) : IHouseRuleMatcher
{
    private const float MinOverlapThreshold = 0.30f; // 30% word overlap triggers match

    public async Task<string?> FindMatchingHouseRuleAsync(
        Guid gameId, Guid? userId, string question, CancellationToken ct)
    {
        if (!userId.HasValue) return null;

        try
        {
            var memory = await gameMemoryRepository
                .GetByGameAndOwnerAsync(gameId, userId.Value, ct).ConfigureAwait(false);

            if (memory is null || memory.HouseRules.Count == 0) return null;

            var questionWords = ExtractWords(question);

            foreach (var rule in memory.HouseRules)
            {
                var ruleWords = ExtractWords(rule.Description);
                var overlap = ComputeOverlap(questionWords, ruleWords);
                if (overlap >= MinOverlapThreshold)
                {
                    logger.LogDebug("[HouseRuleMatcher] Matched rule (overlap={Overlap:P0}): {Rule}",
                        overlap, rule.Description);
                    return rule.Description;
                }
            }
        }
        catch (Exception ex)
        {
            // Cross-BC read: degrade gracefully on failure
            logger.LogWarning(ex, "[HouseRuleMatcher] Failed to read house rules for game {GameId}", gameId);
        }

        return null;
    }

    private static HashSet<string> ExtractWords(string text) =>
        text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 3) // skip stop words by length heuristic
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static float ComputeOverlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0f;
        var intersection = a.Count(w => b.Contains(w));
        return (float)intersection / Math.Min(a.Count, b.Count);
    }
}
```

### Acceptance Criteria (Wiegers)

- AC-2.5.1: Question containing ≥ 30% word overlap with a house rule returns that rule's description.
- AC-2.5.2: `userId = null` returns `null` immediately without repository call.
- AC-2.5.3: No house rules stored → returns `null`.
- AC-2.5.4: Repository failure → returns `null` (graceful degradation), logs warning.
- AC-2.5.5: Stop words (≤ 3 chars) are excluded from overlap calculation.
- AC-2.5.6: `IGameMemoryRepository` is called at most once per invocation.

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Question matches a house rule
  Given user has house rule "Fireball costs 2 mana instead of standard 3"
  And question is "How much mana does fireball cost?"
  When FindMatchingHouseRuleAsync is called
  Then the house rule description is returned
  And the LLM prompt includes house rule context

@happy
Scenario: No house rules stored for game
  Given game has no house rules in GameMemory
  When FindMatchingHouseRuleAsync is called
  Then null is returned
  And RAG flow proceeds normally

@edge
Scenario: Multiple house rules — only one matches
  Given game has 5 house rules about different mechanics
  And question targets mechanic #3
  When FindMatchingHouseRuleAsync is called
  Then only rule #3 is returned (first match wins)

@edge
Scenario: Question in Italian, house rule in Italian
  Given house rule "Fireball costa 2 mana invece di 3"
  And question "Quanto mana costa il fireball?"
  When FindMatchingHouseRuleAsync is called
  Then the rule is matched (language-agnostic word overlap)

@error
Scenario: AgentMemory repository unavailable
  Given IGameMemoryRepository throws
  When FindMatchingHouseRuleAsync is called
  Then null is returned
  And warning is logged
  And no exception propagates
```

### Operational Considerations (Nygard)

- **Cross-BC read latency**: `IGameMemoryRepository.GetByGameAndOwnerAsync` is a PostgreSQL read. P99 target: < 50ms. House rules are in `game_memories.house_rules_json` (JSONB) — no additional join needed.
- **Conflict resolution (multiple matching rules)**: Phase 2 returns first match (insertion order). Phase 3 follow-up: add `Priority` field to `HouseRule` value object for explicit ordering.

---

## Task 2.6 — GameGlossaryEntry entity + repository + endpoints + NER auto-bootstrap

**Duration**: ~1.5 weeks

**Context**: `GameGlossaryEntry` does NOT exist in the codebase. This is a net-new entity. The vision doc (§1.4) maps glossary to `AgentMemory` BC. Given that `HouseRule` lives in `AgentMemory` and the glossary is per-game per-user, we follow the same pattern.

**Files to create**:
- `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/GlossaryEntry.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Services/IGlossaryNerBootstrapService.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/AddGlossaryEntryCommand.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/AddGlossaryEntryCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Queries/GetGlossaryQuery.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Queries/GetGlossaryQueryHandler.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Application/DTOs/GlossaryEntryDto.cs`
- `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Services/SpacyGlossaryNerBootstrapService.cs`
- Migration: `{timestamp}_AddGlossaryEntriesToGameMemory.cs`
- `apps/api/src/Api/Routing/AgentMemoryEndpoints.cs` (add glossary routes)
- `apps/tests/Api.Tests/BoundedContexts/AgentMemory/Commands/AddGlossaryEntryCommandHandlerTests.cs`

**Storage approach**: Same pattern as `HouseRule` — store glossary entries as JSONB column `glossary_entries_json` in `game_memories` table. No new table required for MVP. Each entry has `Term`, `Definition`, `Language`, `Source (Manual | UserDefined | NerBootstrap)`, `AddedAt`.

**NER bootstrap strategy** (Nygard tension resolution): SpaCy NER (`en_core_web_sm`) runs as a one-time Quartz background job triggered on `PhotoBatchCompletedEvent`. The job extracts named entities (GAME, ORG, PERSON, PRODUCT labels) from all OCR text for a batch, then presents candidates to the user via a `/glossary/bootstrap-candidates/{batchId}` endpoint. User confirms which terms to persist. This avoids polluting glossary with false positives.

**NER service** (simplified for MVP — Python call to smoldocling-service or embedding-service):

```csharp
// IGlossaryNerBootstrapService.cs
namespace Api.BoundedContexts.AgentMemory.Domain.Services;

internal sealed record GlossaryCandidate(
    string Term,
    string Language,
    float NerConfidence,
    int OccurrenceCount);

internal interface IGlossaryNerBootstrapService
{
    Task<IReadOnlyList<GlossaryCandidate>> ExtractCandidatesAsync(
        Guid photoBatchUploadId,
        string fullText,
        string language,
        CancellationToken ct = default);
}
```

**GameMemory extension** (add `AddGlossaryEntry` method):

```csharp
// Add to GameMemory.cs
private readonly List<GlossaryEntry> _glossaryEntries = new();
public IReadOnlyList<GlossaryEntry> GlossaryEntries => _glossaryEntries.AsReadOnly();

public void AddGlossaryEntry(string term, string definition, string language, GlossaryEntrySource source)
{
    if (_glossaryEntries.Any(e =>
        string.Equals(e.Term, term, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(e.Language, language, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"Glossary term '{term}' already exists for language '{language}'.");

    _glossaryEntries.Add(GlossaryEntry.Create(term, definition, language, source));
}
```

### Acceptance Criteria (Wiegers)

- AC-2.6.1: `AddGlossaryEntryCommand` creates a `GlossaryEntry` in `GameMemory.glossary_entries_json` for the given `(gameId, userId)`.
- AC-2.6.2: Duplicate term in same language throws a `ConflictException` (409).
- AC-2.6.3: `GetGlossaryQuery` returns all entries for a `(gameId, userId)` pair.
- AC-2.6.4: NER bootstrap produces candidate list without auto-persisting (user must confirm via separate command).
- AC-2.6.5: `glossary_entries_json` column is JSONB with migration `{timestamp}_AddGlossaryEntriesToGameMemory`.
- AC-2.6.6: Cross-game collision impossible (entries scoped to `game_id + owner_id`).
- AC-2.6.7: Endpoints: `POST /api/v1/agent-memory/games/{gameId}/memory/glossary`, `GET /api/v1/agent-memory/games/{gameId}/memory/glossary`.

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Add glossary entry for a game-specific term
  Given authenticated user for game Tainted Grail
  And POST /glossary body {"term": "Morrigan", "definition": "La dea oscura del manuale", "language": "it"}
  When AddGlossaryEntryCommandHandler processes it
  Then 204 No Content is returned
  And glossary_entries_json contains the new entry

@happy
Scenario: NER bootstrap extracts candidate terms
  Given a completed photo batch with text containing "Avalon", "Morrigan", "Legacy Card"
  When ExtractCandidatesAsync is called
  Then candidates include "Avalon", "Morrigan", "Legacy Card"
  And NerConfidence > 0 for each

@edge
Scenario: Duplicate term in same language is rejected
  Given user already has glossary entry for "Morrigan" in "it"
  When AddGlossaryEntryCommand is sent for "Morrigan" in "it"
  Then ConflictException is thrown
  And response status is 409

@edge
Scenario: Same term in different languages is allowed
  Given user has "Morrigan" in "it"
  When AddGlossaryEntryCommand is sent for "Morrigan" in "en"
  Then 204 No Content is returned (cross-language same term is allowed)

@edge
Scenario: NER bootstrap produces false positives
  Given text containing "The" and "Card" as high-frequency common words
  When ExtractCandidatesAsync is called
  Then candidates with OccurrenceCount < 3 are filtered out
  And common stop words are excluded

@error
Scenario: NER service unavailable
  Given the NER service throws
  When the Quartz background job runs
  Then the job logs error and completes without crashing
  And no partial glossary entries are persisted
```

---

## Task 2.7 — Hallucination CI gate via golden eval (LLM-as-judge)

**Duration**: ~1.5 weeks

**Files to create**:
- `tests/llm-eval/hallucination-gate/run_hallucination_gate.py`
- `tests/llm-eval/hallucination-gate/judge_prompt.txt`
- `tests/llm-eval/hallucination-gate/requirements.txt`
- `tests/llm-eval/hallucination-gate/README.md`
- `.github/workflows/hallucination-gate.yml`

**Architecture (Crispin — LLM-as-judge)**

The gate uses GPT-4o as judge to evaluate answers produced by Claude Sonnet (or DeepSeek V3). The judge model is DIFFERENT from the answer model — this is critical to avoid self-serving evaluation bias.

Flow:
1. Load golden set from `tests/llm-eval/golden-set/qa-questions.jsonl` (currently 3 entries — expand to ≥ 50 before Phase 2 gate runs).
2. For each question, call the live API `POST /api/v1/kb/ask` with the question.
3. For each answer, call GPT-4o (`gpt-4o-2024-11-20`) with the judge prompt (see below).
4. Run judge call 5 times per question (majority vote over 5 → accept if ≥ 4/5 pass).
5. Aggregate: gate passes if hallucination rate ≤ 3% across all questions.

**Judge prompt template** (`judge_prompt.txt`):

```
You are a strict hallucination detector for board game rule QA systems.

RULEBOOK CONTEXT (ground truth):
{rulebook_context}

QUESTION:
{question}

AI ANSWER:
{ai_answer}

EXPECTED ANSWER (from golden set):
{expected_answer}

Task: Determine if the AI Answer contains any hallucinated information not supported by the Rulebook Context or contradicting the Expected Answer.

A hallucination is: any statement in the AI Answer that is NOT directly supported by the Rulebook Context AND contradicts the Expected Answer.

Respond ONLY with:
VERDICT: PASS (no hallucination found)
OR
VERDICT: FAIL
REASON: <one sentence explaining what was hallucinated>

Do not add any other commentary.
```

**Python runner** (`run_hallucination_gate.py` — simplified):

```python
#!/usr/bin/env python3
"""
Hallucination CI gate for Libro Game AI Assistant.
Usage: python run_hallucination_gate.py --api-url http://localhost:8080 --judge-model gpt-4o-2024-11-20
"""
import argparse
import json
import os
import sys
from pathlib import Path
from openai import OpenAI

GOLDEN_SET_PATH = Path(__file__).parent.parent / "golden-set" / "qa-questions.jsonl"
JUDGE_PROMPT_PATH = Path(__file__).parent / "judge_prompt.txt"
MAJORITY_VOTE_RUNS = 5
PASS_THRESHOLD = 4  # 4/5 judge calls must pass
HALLUCINATION_RATE_GATE = 0.03  # 3%

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--api-url", required=True)
    parser.add_argument("--judge-model", default="gpt-4o-2024-11-20")
    parser.add_argument("--openai-key", default=os.environ.get("OPENAI_API_KEY"))
    args = parser.parse_args()

    judge = OpenAI(api_key=args.openai_key)
    judge_prompt_template = JUDGE_PROMPT_PATH.read_text()

    questions = [json.loads(l) for l in GOLDEN_SET_PATH.read_text().splitlines() if l.strip()]
    total = len(questions)
    failures = 0

    for q in questions:
        # Call API — POST /api/v1/kb/ask
        import urllib.request
        import json as jsonlib
        payload = jsonlib.dumps({"gameId": q["game_id"], "question": q["question_it"]}).encode()
        req = urllib.request.Request(
            f"{args.api_url}/api/v1/kb/ask",
            data=payload,
            headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as resp:
            answer_data = jsonlib.loads(resp.read())
        ai_answer = answer_data.get("answer", "")

        # Judge: majority vote over MAJORITY_VOTE_RUNS
        pass_count = 0
        for _ in range(MAJORITY_VOTE_RUNS):
            judge_input = judge_prompt_template.format(
                rulebook_context=answer_data.get("citations", ""),
                question=q["question_it"],
                ai_answer=ai_answer,
                expected_answer=q["expected_answer_it"])
            resp = judge.chat.completions.create(
                model=args.judge_model,
                messages=[{"role": "user", "content": judge_input}],
                temperature=0)
            verdict = resp.choices[0].message.content.strip()
            if "VERDICT: PASS" in verdict:
                pass_count += 1

        passed = pass_count >= PASS_THRESHOLD
        if not passed:
            failures += 1
            print(f"FAIL [{q['id']}]: {pass_count}/{MAJORITY_VOTE_RUNS} judge runs passed")
        else:
            print(f"PASS [{q['id']}]: {pass_count}/{MAJORITY_VOTE_RUNS}")

    rate = failures / total if total > 0 else 0
    print(f"\nHallucination rate: {rate:.1%} ({failures}/{total} failed)")
    if rate > HALLUCINATION_RATE_GATE:
        print(f"GATE FAILED: rate {rate:.1%} > threshold {HALLUCINATION_RATE_GATE:.1%}")
        sys.exit(1)
    else:
        print(f"GATE PASSED: rate {rate:.1%} <= threshold {HALLUCINATION_RATE_GATE:.1%}")
        sys.exit(0)

if __name__ == "__main__":
    main()
```

**CI workflow** (`.github/workflows/hallucination-gate.yml`):

```yaml
name: Hallucination Gate

on:
  push:
    branches: [main-dev]
  schedule:
    - cron: '0 3 * * 1'  # Weekly Monday 3am

jobs:
  hallucination-gate:
    runs-on: ubuntu-latest
    if: github.event_name == 'schedule' || contains(github.event.head_commit.message, '[run-hallucination-gate]')
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with: { python-version: '3.12' }
      - run: pip install -r tests/llm-eval/hallucination-gate/requirements.txt
      - name: Run hallucination gate
        env:
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
          API_URL: ${{ secrets.STAGING_API_URL }}
        run: python tests/llm-eval/hallucination-gate/run_hallucination_gate.py
             --api-url $API_URL --judge-model gpt-4o-2024-11-20
      - name: Upload results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: hallucination-gate-results
          path: tests/llm-eval/hallucination-gate/results/
```

### Acceptance Criteria (Wiegers)

- AC-2.7.1: Gate script runs against ≥ 50 golden set questions (expand `qa-questions.jsonl` before gate runs).
- AC-2.7.2: Gate uses GPT-4o as judge with 5-run majority vote per question.
- AC-2.7.3: Gate exits 0 (success) if hallucination rate ≤ 3%.
- AC-2.7.4: Gate exits 1 (failure) if hallucination rate > 3%.
- AC-2.7.5: Per-question PASS/FAIL printed to stdout.
- AC-2.7.6: CI workflow triggers weekly (cron) and on `[run-hallucination-gate]` commit message tag.
- AC-2.7.7: Judge prompt is in a separate file (not hardcoded) for easy iteration.
- AC-2.7.8: `OPENAI_API_KEY` is injected via CI secret (never committed).

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: All golden set questions pass hallucination check
  Given 50 golden set questions with expected answers
  When run_hallucination_gate.py runs against staging API
  Then hallucination rate <= 3%
  And exit code = 0

@happy
Scenario: Majority vote passes with 4/5 judge calls
  Given a question where GPT-4o judge votes PASS in 4 of 5 calls
  When majority vote is computed
  Then question is marked as PASS
  And does not count toward failure rate

@edge
Scenario: Judge model returns unexpected format
  Given GPT-4o returns a response without "VERDICT:" prefix
  When verdict is parsed
  Then the run is counted as FAIL (conservative: unknown = fail)

@edge
Scenario: API returns empty answer
  Given the API returns {"answer": ""}
  When the judge prompt is populated
  Then it is sent to GPT-4o with empty AI Answer
  And judge marks it as FAIL (empty answer is a hallucination failure)

@error
Scenario: API is unavailable
  Given the staging API URL is unreachable
  When run_hallucination_gate.py runs
  Then the script exits with code 2 (infrastructure error, not gate failure)
  And gate is marked as "inconclusive" in CI (not blocking merge)

@error
Scenario: OpenAI API key is invalid
  Given OPENAI_API_KEY is incorrect
  When the judge client initializes
  Then the script exits with code 1 and prints auth error message
```

### Operational Considerations (Nygard)

- **Stochastic reliability**: 5-run majority vote reduces false positives from ~15% to ~2% for borderline answers. Record all 5 verdicts per question for audit.
- **Cost**: 50 questions × 5 judge runs × ~1000 tokens per judge call × $0.005/1K = ~$1.25 per gate run. Within budget.
- **Weekly schedule**: Running against staging weekly prevents silent regression. Do not run on every PR (too expensive and rate-limit risky).
- **Expand golden set**: Current golden set has 3 entries (sprint 0 deliverable). Phase 2 gate requirement: ≥ 50 Q&A + ≥ 20 translation paragraphs. This is a manual curation task (Aaron) — flag as BLOCKER for gate activation.

---

## Task 2.8 — infra/compose.test.yml for E2E test environment

**Duration**: ~0.5 weeks

**Drift note**: The project uses `compose.*.yml` naming convention (NOT `docker-compose.*.yml`). File must be `infra/compose.test.yml`.

**Files to create**:
- `infra/compose.test.yml`
- `infra/secrets/test.secret.example` (template for test secrets)

**Architecture**: The test compose file provides a self-contained environment for E2E tests against the translation + Q&A flows. It reuses the `compose.dev.yml` pattern but with in-memory Redis and SQLite or a dedicated test Postgres instance (non-destructive).

```yaml
# infra/compose.test.yml
# E2E test environment for Libro Game AI Assistant Phase 2.
# Usage: docker compose -f docker-compose.yml -f compose.test.yml up
# Tests run against this isolated environment.

services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Testing
      - TRANSLATION_SERVICE_ENABLED=true
      - PRICING_ENGINE_MODE=null  # NullPricingEngine
      - HALLUCINATION_GATE_ENABLED=false
    env_file:
      - ./secrets/test.secret

  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: meepleai_test
      POSTGRES_USER: meepleai_test
      POSTGRES_PASSWORD: test_password
    ports: ["127.0.0.1:5433:5432"]
    tmpfs:
      - /var/lib/postgresql/data  # In-memory for speed

  redis:
    image: redis:7-alpine
    ports: ["127.0.0.1:6380:6379"]
    command: ["redis-server", "--save", ""]  # No persistence in test
    tmpfs:
      - /data
```

### Acceptance Criteria (Wiegers)

- AC-2.8.1: `docker compose -f docker-compose.yml -f compose.test.yml up` starts without errors.
- AC-2.8.2: Postgres binds to port 5433 (no conflict with dev on 5432).
- AC-2.8.3: Redis binds to port 6380 (no conflict with dev on 6379).
- AC-2.8.4: `ASPNETCORE_ENVIRONMENT=Testing` is set (disables production-only guards).
- AC-2.8.5: `test.secret.example` committed; `test.secret` in `.gitignore`.
- AC-2.8.6: E2E test suite (Playwright) can run against `http://localhost:8080` when compose.test.yml is up.

### Gherkin Scenarios (Adzic)

```gherkin
@happy
Scenario: Compose test environment starts cleanly
  When docker compose -f docker-compose.yml -f compose.test.yml up is run
  Then all services start without errors
  And API is accessible on :8080
  And Postgres is accessible on :5433
  And Redis is accessible on :6380

@edge
Scenario: Port conflict with dev environment
  Given dev environment is already running on :5432 and :6379
  When compose.test.yml is started
  Then test services bind to :5433 and :6380 without conflict

@error
Scenario: Missing test.secret file
  Given test.secret is not present
  When compose.test.yml is started
  Then a clear error message indicates the missing file
  And test.secret.example is referenced in the error instructions
```

---

## Task 2.9 — Phase 2 Acceptance Gate

**Duration**: ~0.5 weeks (verification sprint)

**Phase 2 passes when ALL of the following are met**:

### Backend acceptance (measurable)

| Criterion | Target | Measurement |
|-----------|--------|-------------|
| `INarrativeTranslationService` integration test | EN → IT paragraph passes with non-empty Italian output | Manual run against dev |
| `IGenericTranslationService` integration test | Short string EN → IT passes | Manual run against dev |
| `TranslationCache` hit test | Same paragraph cached on 2nd call (Redis miss→hit) | Test with real Redis |
| `AskQuestionQuery.ResponseLanguage` integration | Italian answer returned for IT request | Manual E2E test |
| `IHouseRuleMatcher` integration | Matching house rule injected in LLM context | Unit test |
| `IDocumentChunker` unit tests | ≥ 90% statement coverage | `dotnet test /p:CollectCoverage=true` |
| `IKnowledgeBaseIndexer` unit tests | ≥ 90% statement coverage | Same |
| `AddGlossaryEntryCommand` integration | Entry persisted in JSONB | Manual test |
| Duplicate glossary entry | `ConflictException` returned | Unit test |
| `HeuristicQAComplexityClassifier` | ≥ 85% accuracy on 20 test questions | Unit test suite |

### CI acceptance

| Criterion | Target | Measurement |
|-----------|--------|-------------|
| All existing tests pass | 0 regressions | `dotnet test` |
| `compose.test.yml` starts | Clean startup | Manual or CI step |
| Hallucination gate | ≤ 3% on ≥ 50 golden set questions | CI workflow |
| Frontend typecheck | `pnpm typecheck` passes | CI |
| Frontend lint | `pnpm lint` passes | CI |

### Translation quality acceptance

| Criterion | Target | Measurement |
|-----------|--------|-------------|
| Human MOS rating | ≥ 4.0/5.0 on 10 sample paragraphs | Aaron manual review |
| LLM-as-judge factual accuracy | ≥ 85% on `translation-paragraphs.jsonl` golden set | `run_hallucination_gate.py` (translation variant) |
| Proper noun preservation | ≥ 95% of game terms preserved | Manual spot check on 20 paragraphs |

### Phase 3 entry criteria

Phase 3 work begins ONLY AFTER:
1. All backend tests pass with no regressions.
2. Hallucination gate: ≤ 3% on ≥ 50 questions.
3. Translation MOS ≥ 4.0 on Aaron's review.
4. `compose.test.yml` verified operational.
5. `IPricingEngine` stub (`NullPricingEngine`) is registered and confirmed resolvable in DI.

---

## Cross-cutting Concerns

### Translation quality measurement strategy

**Chosen approach** (resolution of Wiegers-Adzic tension):

| Layer | Metric | Tool | Gate threshold |
|-------|--------|------|----------------|
| Automated regression | BLEU vs reference | `sacrebleu` Python library | Not a gate — tracking only |
| Automated factual accuracy | LLM-as-judge (GPT-4o) | `run_hallucination_gate.py` translation variant | ≥ 85% accuracy |
| Human narrative quality | MOS 1-5 | Aaron manual review | ≥ 4.0/5.0 |
| Proper noun preservation | Manual spot check | Aaron + script | ≥ 95% preservation |

BLEU is tracked but not gated because Italian gaming narrative text violates BLEU assumptions (flexible word order, creative paraphrases are valid, game terms may have multiple valid translations). MOS is the primary gate for narrative. LLM-as-judge is the primary gate for factual accuracy.

### Hallucination CI gate architecture

**Judge design**:
- Judge model: `gpt-4o-2024-11-20` (explicitly pinned — upgrade requires re-validation)
- Answer model: `anthropic/claude-sonnet-4-5` (via OpenRouter)
- Determinism strategy: 5-run majority vote (4/5 pass = overall PASS per question)
- Threshold: ≤ 3% hallucination rate across ≥ 50 golden set questions
- Special rule: 0% failures allowed for questions with `expected_confidence = "high"` in golden set (safety-critical rules)

**Prompt engineering discipline**:
- Judge prompt is in `judge_prompt.txt` (version-controlled)
- Judge prompt changes require re-validation against all 50 questions before merge
- Judge prompt must include "Respond ONLY with VERDICT: PASS or VERDICT: FAIL" — prevents verbose output confusing parser

### Cost ceiling per request

**Phase 2 approach** (pre-`IPricingEngine`):

| Operation | Model | Max tokens | Est. max cost |
|-----------|-------|-----------|---------------|
| Narrative translation | claude-sonnet-4-5 | 2048 | ~$0.03 |
| Generic translation | deepseek/deepseek-chat | 512 | ~$0.001 |
| Q&A (AskQuestion) | claude-sonnet-4-5 (existing) | 4096 | ~$0.06 |
| Q&A + translation | Both above combined | — | ~$0.09 |

Static guard: log warning when `LlmCost.TotalCost > 0.05m`. Hard enforcement deferred to Phase 3 `IPricingEngine`.

Free tier: 50 pages/month limit enforced by Phase 3. Phase 2 is internal testing only — no user quota enforcement.

### Translation cache invalidation

**Strategy**:
- Primary guard: TTL (7 days narrative, 4 hours generic)
- Event-based invalidation: `PhotoBatchCompletedEvent` → `InvalidateGameAsync` (Phase 2: best-effort log; Phase 3: pattern-delete via `IConnectionMultiplexer`)
- Model upgrade invalidation: Manual — update `cache_key_version` string suffix in `RedisTranslationCache.BuildKey` on model upgrade to force all entries to expire immediately

**When does translation go stale?**
1. Rulebook re-indexed (new photo batch) — handled by event
2. Translation model upgraded — handled by key version bump
3. Game terminology changed by publisher — manual flush (admin endpoint, Phase 3)

### Glossary NER model choice and trigger

**Model**: `en_core_web_sm` (SpaCy 3.x, 50 MB) for entity extraction. Handles PRODUCT, ORG, PERSON labels. Runs offline (no API call). Triggered by `PhotoBatchCompletedEvent` via Quartz background job.

**Trigger conditions**:
1. `PhotoBatchCompletedEvent` raised for a game
2. Batch has `ConfidenceLevel >= Medium` on ≥ 80% of pages (low OCR quality → skip NER)
3. Available server RAM > 8 GB (guard against OOM on CAX31)

**Validation**: Candidates with `OccurrenceCount < 3` are filtered. User must confirm via `POST /api/v1/agent-memory/games/{gameId}/memory/glossary` — no auto-persist.

### House rule conflict resolution

**Phase 2 rule**: First match wins (insertion order in `GameMemory._houseRules` list). If multiple rules match the question, the oldest rule takes priority (insertion order = `AddedAt` ascending).

**Phase 3 follow-up**: Add `Priority` integer field to `HouseRule` value object. Higher priority wins. Migration: `ALTER TABLE game_memories` to update JSONB schema (no structural migration needed — JSONB is flexible).

**Conflict detection**: Phase 2 does not detect contradicting house rules (e.g., "Fireball costs 2 mana" vs "Fireball costs 4 mana"). Flag as a Phase 3 feature: LLM-powered conflict detection on rule addition.

### KB indexing services (Sprint 1 Task 1.6 forward dep)

**Gap confirmed**: `IDocumentChunker`, `IKnowledgeBaseIndexer`, `KnowledgeChunk` do not exist in the codebase. `PhotoBatchProcessor.cs` (Task 1.6) is also not implemented.

**Phase 2 handoff**:
- Task 2.3a creates: `IDocumentChunker`, `IKnowledgeBaseIndexer`, `KnowledgeChunk` (in `DocumentProcessing` BC)
- Task 2.3a creates: `PageTextChunker` (implementation of `IDocumentChunker`) and `KnowledgeBaseIndexer` (implementation using existing `IEmbeddingService` + pgvector `IEmbeddingRepository`)
- Sprint 2-3 Task 1.6 (`PhotoBatchProcessor`) consumes these services

**Wire-up comment** to be placed in `PhotoBatchProcessor.cs` when created in Sprint 2-3:

```csharp
// TODO Phase 2 Task 2.3a: IDocumentChunker and IKnowledgeBaseIndexer are now available.
// Wire them here: for each completed page, call _chunker.ChunkPage(...) then _indexer.IndexBatchAsync(...).
// See: DocumentProcessing/Application/Services/IDocumentChunker.cs
//      DocumentProcessing/Application/Services/IKnowledgeBaseIndexer.cs
```

---

## Phase 2 Acceptance Gate (summary)

Entry to Phase 3 requires ALL of the following green:

| Gate | Criterion | Status |
|------|-----------|--------|
| G-2-1 | All backend tests pass (no regression) | TBD |
| G-2-2 | `INarrativeTranslationService` + `IGenericTranslationService` return non-empty Italian translations | TBD |
| G-2-3 | Translation cache hit on 2nd identical request | TBD |
| G-2-4 | `AskQuestionQuery.ResponseLanguage = "it"` returns Italian answer | TBD |
| G-2-5 | House rule matched and injected in LLM prompt | TBD |
| G-2-6 | Glossary entry persisted and retrievable | TBD |
| G-2-7 | Hallucination CI gate ≤ 3% on ≥ 50 golden questions | TBD — BLOCKER: golden set must be expanded |
| G-2-8 | compose.test.yml starts cleanly | TBD |
| G-2-9 | `IDocumentChunker`, `IKnowledgeBaseIndexer`, `KnowledgeChunk` compile and are DI-resolvable | TBD |
| G-2-10 | Translation MOS ≥ 4.0 (Aaron review, 10 sample paragraphs) | TBD — BLOCKER: Aaron manual review |
| G-2-11 | `NullPricingEngine` registered and Phase 3 `IPricingEngine` interface defined | TBD |

**Known blockers for G-2-7**: Golden set expansion to ≥ 50 questions requires Aaron content curation (estimated 2-3 days). Gate cannot run on 3 questions — insufficient statistical significance.

---

## Effort Estimate

| Task | Effort (days) | Owner | Key Dependencies |
|------|--------------|-------|-----------------|
| 2.1 TranslationService skeleton + OpenRouter | 5 | Backend dev | Phase 1 complete (ILlmService available) |
| 2.2 TranslationCache Redis | 2 | Backend dev | 2.1 (interfaces) |
| 2.3a KB Indexing Services | 4 | Backend dev | Sprint 1 Tasks 1.1-1.4 (domain entities) |
| 2.3b AskQuestionQuery extension | 3 | Backend dev | 2.1, 2.2, 2.5 (IHouseRuleMatcher) |
| 2.4 IQAComplexityClassifier | 2 | Backend dev | None |
| 2.5 HouseRuleMatcher | 2 | Backend dev | AgentMemory BC (already present) |
| 2.6 GameGlossaryEntry + NER | 5 | Backend dev + Aaron (curation) | AgentMemory BC patterns |
| 2.7 Hallucination CI gate | 4 | Backend dev + Aaron (golden set expansion) | Phase 1 API deployed; OpenAI key |
| 2.8 compose.test.yml | 1 | Infra/Backend dev | None |
| 2.9 Acceptance gate | 3 | Tech lead + Aaron | All 2.1-2.8 complete |
| **Total** | **31 days (~6 weeks)** | | |

**Calendar**: 6 weeks (weeks 7-12), matching plan v2 estimate.

**Critical path**: 2.1 → 2.2 → 2.3b (translation pipeline). Parallel: 2.4 + 2.5 + 2.6 can run concurrently with 2.1-2.3. 2.7 blocked on golden set expansion (Aaron action item).

---

*Last updated: 2026-05-04 — Phase 2 spec-panel review with 5-expert framework (Wiegers, Adzic, Newman, Crispin, Nygard)*
