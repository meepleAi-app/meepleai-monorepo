using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using System.Diagnostics.Metrics;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

[Collection("GamebookMeter")] // #2752: serialize with GamebookTranslationMetricsTests — shared static gamebook Meter
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class TranslateGamebookSegmentQueryHandlerTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default) { Store.Add(s); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeArtifactRepo : IGamebookPhotoArtifactRepository
    {
        public List<GamebookPhotoArtifact> Store { get; } = new();

        public Task<GamebookPhotoArtifact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookPhotoArtifact>> ListExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GamebookPhotoArtifact>>(new List<GamebookPhotoArtifact>());

        public Task AddAsync(GamebookPhotoArtifact artifact, CancellationToken cancellationToken = default) { Store.Add(artifact); return Task.CompletedTask; }
        public Task RemoveAsync(GamebookPhotoArtifact artifact, CancellationToken cancellationToken = default) { Store.Remove(artifact); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeParagraphRepo : ITranslatedParagraphRepository
    {
        public List<TranslatedParagraph> Store { get; } = new();

        public Task<IReadOnlyList<TranslatedParagraph>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TranslatedParagraph>>(Store.Where(x => x.CampaignId == campaignId).ToList());

        public Task AddAsync(TranslatedParagraph paragraph, CancellationToken cancellationToken = default) { Store.Add(paragraph); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProgressRepo : ISessionBookProgressRepository
    {
        public List<SessionBookProgress> Store { get; } = new();

        public Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken cancellationToken)
            => Task.FromResult(Store.FirstOrDefault(p =>
                p.CampaignSessionId == campaignSessionId && p.GameBookId == gameBookId));

        public Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SessionBookProgress>>(
                Store.Where(p => p.CampaignSessionId == campaignSessionId).ToList());

        public Task<SessionBookProgress?> GetMostRecentByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
            => Task.FromResult(Store
                .Where(p => p.CampaignSessionId == campaignSessionId)
                .OrderByDescending(p => p.LastVisitedAt)
                .FirstOrDefault());

        public Task AddAsync(SessionBookProgress progress, CancellationToken cancellationToken)
        {
            Store.Add(progress);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SessionBookProgress progress, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
        {
            Store.RemoveAll(p => p.CampaignSessionId == campaignSessionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGlossaryRepo : IGamebookGlossaryRepository
    {
        public List<GamebookGlossaryEntry> Store { get; } = new();

        public Task<IReadOnlyList<GamebookGlossaryEntry>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GamebookGlossaryEntry>>(Store.Where(x => x.CampaignId == campaignId).ToList());

        public Task<GamebookGlossaryEntry?> GetByTermAsync(Guid campaignId, string termEn, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.CampaignId == campaignId && x.TermEn == termEn));

        // Issue #1312: cross-entry termIt collision lookup. Case-insensitive + trimmed
        // to mirror the production EF Core ILIKE-based implementation.
        public Task<GamebookGlossaryEntry?> GetByTermItAsync(Guid campaignId, string termIt, CancellationToken cancellationToken = default)
        {
            var needle = (termIt ?? string.Empty).Trim();
            return Task.FromResult(Store.FirstOrDefault(x =>
                x.CampaignId == campaignId
                && string.Equals(x.TermIt.Trim(), needle, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<GamebookGlossaryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task AddRangeAsync(IEnumerable<GamebookGlossaryEntry> entries, CancellationToken cancellationToken = default)
        { Store.AddRange(entries); return Task.CompletedTask; }

        public Task AddAsync(GamebookGlossaryEntry entry, CancellationToken cancellationToken = default) { Store.Add(entry); return Task.CompletedTask; }
        public void Remove(GamebookGlossaryEntry entry) => Store.Remove(entry);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Fake LLM that yields two content chunks then a final chunk with usage,
    /// producing the Italian text "L'Alveare si risveglia."
    /// </summary>
    private sealed class FakeLlmService : ILlmService
    {
        public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
            string systemPrompt,
            string userPrompt,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamChunk("L'", IsFinal: false);
            await Task.Yield(); // allow async continuation
            yield return new StreamChunk("Alveare si risveglia.", IsFinal: false);
            yield return new StreamChunk(null, IsFinal: true, Usage: new LlmUsage(50, 10, 60));
        }

        public Task<LlmCompletionResult> GenerateCompletionAsync(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public Task<T?> GenerateJsonAsync<T>(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
            IReadOnlyList<LlmMessage> messages,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamChunk(null, IsFinal: true);
            await Task.CompletedTask;
        }

        public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(string explicitModel, string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));
    }

    /// <summary>
    /// Fake guard that always confirms ownership. The handler delegates ownership
    /// enforcement to this collaborator after Issue #1415, so unit tests for the
    /// handler's translation/persistence path use a no-op guard. Authorization
    /// semantics are covered by CampaignOwnershipGuardTests + integration tests.
    /// </summary>
    private sealed class AlwaysOwnedGuard : ICampaignOwnershipGuard
    {
        public Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Fake LLM that captures the systemPrompt argument and yields a fixed Italian translation.
    /// Used by T7 tests to verify the dynamic prompt content.
    /// </summary>
    private sealed class CapturingFakeLlmService : ILlmService
    {
        public string? CapturedSystemPrompt { get; private set; }

        public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
            string systemPrompt,
            string userPrompt,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CapturedSystemPrompt = systemPrompt;
            yield return new StreamChunk("L'Alveare si risveglia.", IsFinal: false);
            await Task.Yield();
            yield return new StreamChunk(null, IsFinal: true, Usage: new LlmUsage(50, 10, 60));
        }

        public Task<LlmCompletionResult> GenerateCompletionAsync(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public Task<T?> GenerateJsonAsync<T>(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
            IReadOnlyList<LlmMessage> messages,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamChunk(null, IsFinal: true);
            await Task.CompletedTask;
        }

        public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(string explicitModel, string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));
    }

    /// <summary>
    /// Fake IHybridCacheService for T7 tests.
    /// If CachedValue is set, GetOrCreateAsync returns it directly (cache hit).
    /// If CachedValue is null, GetOrCreateAsync invokes the factory (cache miss).
    /// </summary>
    private sealed class FakeHybridCache : IHybridCacheService
    {
        /// <summary>
        /// Pre-set to simulate a cache hit. Null = cache miss (factory is invoked).
        /// </summary>
        public PhotoOcrCacheValue? CachedValue { get; set; }

        public Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            string[]? tags = null,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class
        {
            if (CachedValue is T hit)
                return Task.FromResult(hit);

            // Cache miss: invoke the factory (simulates repo fallback path).
            return factory(ct);
        }

        public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);
        public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default)
            => Task.FromResult(new HybridCacheStats());
    }

    /// <summary>
    /// #2752: Fake LLM whose final chunk carries BOTH Usage and Cost, mirroring what
    /// OpenRouterLlmClient / DeepSeekLlmClient emit. Drives the translation_cost_eur wiring.
    /// </summary>
    private sealed class CostReportingFakeLlmService : ILlmService
    {
        private readonly LlmCost _cost;
        public CostReportingFakeLlmService(LlmCost cost) => _cost = cost;

        public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
            string systemPrompt,
            string userPrompt,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamChunk("L'Alveare si risveglia.", IsFinal: false);
            await Task.Yield();
            yield return new StreamChunk(null, Usage: new LlmUsage(50, 10, 60), Cost: _cost, IsFinal: true);
        }

        public Task<LlmCompletionResult> GenerateCompletionAsync(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public Task<T?> GenerateJsonAsync<T>(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
            IReadOnlyList<LlmMessage> messages,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamChunk(null, IsFinal: true);
            await Task.CompletedTask;
        }

        public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(string explicitModel, string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TranslateGamebookSegmentQueryHandler BuildHandler(
        FakeCampaignRepo campaignRepo,
        FakeArtifactRepo artifactRepo,
        FakeParagraphRepo paragraphRepo,
        FakeGlossaryRepo glossaryRepo,
        FakeProgressRepo progressRepo,
        ILlmService? llm = null,
        ICampaignOwnershipGuard? ownershipGuard = null,
        IHybridCacheService? cache = null) =>
        new(
            campaignRepo,
            artifactRepo,
            paragraphRepo,
            glossaryRepo,
            progressRepo,
            llm ?? new FakeLlmService(),
            ownershipGuard ?? new AlwaysOwnedGuard(),
            cache ?? new FakeHybridCache(),
            NullLogger<TranslateGamebookSegmentQueryHandler>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StreamsAndPersistsTranslation()
    {
        // Arrange
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Nanolith Campaign");
        campaignRepo.Store.Add(campaign);

        var bookId = Guid.NewGuid();

        // Build artifact in Segmented state with §47
        var artifact = GamebookPhotoArtifact.Create(campaign.Id, bookId, $"gamebook-photos/{campaign.Id}/photo1");
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(47, "The Hive awakens.", null) },
            "The Hive awakens.");
        artifactRepo.Store.Add(artifact);

        // Glossary: Hive → Alveare
        var glossaryEntry = GamebookGlossaryEntry.Create(campaign.Id, "Hive", "Alveare", GlossarySource.AutoBootstrap, ownerId);
        glossaryRepo.Store.Add(glossaryEntry);

        var handler = BuildHandler(campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo);

        var query = new TranslateGamebookSegmentQuery(campaign.Id, artifact.Id, 47, ownerId, bookId);

        // Act — collect all streamed chunks
        var chunks = new List<TranslateChunk>();
        await foreach (var chunk in handler.Handle(query, CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert streaming shape: 2 delta chunks + 1 complete chunk
        chunks.Should().HaveCount(3, "two content deltas + one final complete chunk");

        var contentChunks = chunks.Where(c => !c.IsComplete).ToList();
        contentChunks.Should().HaveCount(2);
        contentChunks[0].Delta.Should().Be("L'");
        contentChunks[1].Delta.Should().Be("Alveare si risveglia.");

        var finalChunk = chunks.Single(c => c.IsComplete);
        finalChunk.ParagraphId.Should().NotBeNull();
        finalChunk.ParagraphId.Should().NotBe(Guid.Empty);
        finalChunk.AppliedTerms.Should().Contain("Hive",
            "the EN term 'Hive' appeared in source and 'Alveare' appeared in translation");

        // Assert persistence
        paragraphRepo.Store.Should().HaveCount(1, "one TranslatedParagraph should be persisted");
        var persisted = paragraphRepo.Store[0];
        persisted.CampaignId.Should().Be(campaign.Id);
        persisted.GameBookId.Should().Be(bookId);
        persisted.PhotoArtifactId.Should().Be(artifact.Id);
        persisted.ParagraphNumber.Should().Be(47);
        persisted.SourceTextEn.Should().Be("The Hive awakens.");
        persisted.TranslatedTextIt.Should().Be("L'Alveare si risveglia.");
        persisted.AppliedGlossaryTerms.Should().Contain("Hive");

        // C2 (2026-05-19): per-book progress advanced via SessionBookProgress.
        progressRepo.Store.Should().HaveCount(1);
        var progressRow = progressRepo.Store[0];
        progressRow.CampaignSessionId.Should().Be(campaign.Id);
        progressRow.GameBookId.Should().Be(bookId);
        progressRow.LastLocation.Should().Be("§47");
    }

    // ── T7: DEC-13 cache-first + DEC-14 dynamic prompt + DEC-15 SourceLangOverride ──

    [Fact]
    public async Task Handle_CacheHit_UsesDetectedLangInPrompt()
    {
        // Arrange
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo(); // not consulted on cache hit
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();
        var capturingLlm = new CapturingFakeLlmService();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "EN Campaign");
        campaignRepo.Store.Add(campaign);
        var bookId = Guid.NewGuid();

        // Pre-seed cache with EN-detected photo OCR (cache hit — artifact repo empty).
        var photoId = Guid.NewGuid();
        var cachedOcr = new PhotoOcrCacheValue(
            FullText: "The Hive awakens.",
            Paragraphs: new List<OcrParagraphCacheEntry> { new(1, "The Hive awakens.") },
            AverageConfidence: 95.0,
            DetectedSourceLang: "EN",
            LangDetectionConfidence: 0.92);

        var fakeCache = new FakeHybridCache { CachedValue = cachedOcr };

        var handler = BuildHandler(
            campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo,
            llm: capturingLlm, cache: fakeCache);

        var query = new TranslateGamebookSegmentQuery(campaign.Id, photoId, 1, ownerId, bookId);

        // Act
        var chunks = new List<TranslateChunk>();
        await foreach (var chunk in handler.Handle(query, CancellationToken.None))
            chunks.Add(chunk);

        // Assert — system prompt mentions "English to Italian"
        capturingLlm.CapturedSystemPrompt.Should().NotBeNull();
        capturingLlm.CapturedSystemPrompt.Should().Contain("English to Italian",
            "detected lang EN must drive prompt language direction");

        // Final chunk carries audit fields from cache
        var finalChunk = chunks.Single(c => c.IsComplete);
        finalChunk.DetectedSourceLang.Should().Be("EN");
        finalChunk.LangDetectionConfidence.Should().BeApproximately(0.92, 1e-9);
    }

    [Fact]
    public async Task Handle_SourceLangOverride_UsesOverrideInPromptButPreservesDetectedAuditField()
    {
        // Arrange
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();
        var capturingLlm = new CapturingFakeLlmService();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "FR Override Campaign");
        campaignRepo.Store.Add(campaign);
        var bookId = Guid.NewGuid();

        // Cache says EN detected, but query overrides to FR.
        var photoId = Guid.NewGuid();
        var cachedOcr = new PhotoOcrCacheValue(
            FullText: "The Hive awakens.",
            Paragraphs: new List<OcrParagraphCacheEntry> { new(1, "The Hive awakens.") },
            AverageConfidence: 90.0,
            DetectedSourceLang: "EN",
            LangDetectionConfidence: 0.80);

        var fakeCache = new FakeHybridCache { CachedValue = cachedOcr };
        var handler = BuildHandler(
            campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo,
            llm: capturingLlm, cache: fakeCache);

        // SourceLangOverride = "FR"
        var query = new TranslateGamebookSegmentQuery(
            campaign.Id, photoId, 1, ownerId, bookId, SourceLangOverride: "FR");

        // Act
        var chunks = new List<TranslateChunk>();
        await foreach (var chunk in handler.Handle(query, CancellationToken.None))
            chunks.Add(chunk);

        // Assert — override drives prompt to French
        capturingLlm.CapturedSystemPrompt.Should().Contain("French to Italian",
            "SourceLangOverride='FR' must override prompt lang direction");
        capturingLlm.CapturedSystemPrompt.Should().NotContain("English to Italian");

        // Final chunk audit field preserves cache-detected EN (NOT the override)
        var finalChunk = chunks.Single(c => c.IsComplete);
        finalChunk.DetectedSourceLang.Should().Be("EN",
            "audit field must reflect what OCR detected, not the caller override");
        finalChunk.LangDetectionConfidence.Should().BeApproximately(0.80, 1e-9);
    }

    [Fact]
    public async Task Handle_CacheMiss_FallsBackToRepository()
    {
        // Arrange
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();
        var capturingLlm = new CapturingFakeLlmService();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "FR Repo Campaign");
        campaignRepo.Store.Add(campaign);
        var bookId = Guid.NewGuid();

        // Artifact in repo with DetectedSourceLang = "FR".
        var artifact = GamebookPhotoArtifact.Create(campaign.Id, bookId, $"gamebook-photos/{campaign.Id}/photo-fr");
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(3, "La Ruche s'éveille.", null) },
            "La Ruche s'éveille.",
            detectedSourceLang: "FR",
            langDetectionConfidence: 0.97);
        artifactRepo.Store.Add(artifact);

        // FakeHybridCache with no CachedValue → invokes factory → repo fallback.
        var fakeCache = new FakeHybridCache(); // CachedValue = null (default)
        var handler = BuildHandler(
            campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo,
            llm: capturingLlm, cache: fakeCache);

        var query = new TranslateGamebookSegmentQuery(campaign.Id, artifact.Id, 3, ownerId, bookId);

        // Act
        var chunks = new List<TranslateChunk>();
        await foreach (var chunk in handler.Handle(query, CancellationToken.None))
            chunks.Add(chunk);

        // Assert — repo-loaded FR drives prompt
        capturingLlm.CapturedSystemPrompt.Should().Contain("French to Italian",
            "repo-loaded DetectedSourceLang='FR' must drive prompt direction on cache miss");

        var finalChunk = chunks.Single(c => c.IsComplete);
        finalChunk.DetectedSourceLang.Should().Be("FR");
        finalChunk.LangDetectionConfidence.Should().BeApproximately(0.97, 1e-9);
    }

    [Fact]
    public async Task Handle_CacheMissAndNullDetectedLang_FallsBackToEnglishDefault()
    {
        // Arrange
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();
        var capturingLlm = new CapturingFakeLlmService();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Legacy Campaign");
        campaignRepo.Store.Add(campaign);
        var bookId = Guid.NewGuid();

        // Legacy artifact: no DetectedSourceLang (pre-#1559 photo).
        var artifact = GamebookPhotoArtifact.Create(campaign.Id, bookId, $"gamebook-photos/{campaign.Id}/photo-legacy");
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(1, "The ancient hall.", null) },
            "The ancient hall.");
        // DetectedSourceLang is null by default (not set on artifact).
        artifactRepo.Store.Add(artifact);

        var fakeCache = new FakeHybridCache(); // cache miss → factory invoked
        var handler = BuildHandler(
            campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo,
            llm: capturingLlm, cache: fakeCache);

        var query = new TranslateGamebookSegmentQuery(campaign.Id, artifact.Id, 1, ownerId, bookId);

        // Act
        var chunks = new List<TranslateChunk>();
        await foreach (var chunk in handler.Handle(query, CancellationToken.None))
            chunks.Add(chunk);

        // Assert — defaults to English when detection unavailable
        capturingLlm.CapturedSystemPrompt.Should().Contain("English to Italian",
            "null DetectedSourceLang must fall back to 'EN' default (legacy pre-#1559 photos)");

        var finalChunk = chunks.Single(c => c.IsComplete);
        finalChunk.DetectedSourceLang.Should().BeNull(
            "no lang was detected — audit field must remain null");
        finalChunk.LangDetectionConfidence.Should().BeNull();
    }

    // ── #2752: translation_cost_eur wiring from StreamChunk.Cost ────────────────

    [Fact]
    public async Task Handle_FinalChunkCarriesCost_RecordsTranslationCostEur()
    {
        // Arrange
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Cost Campaign");
        campaignRepo.Store.Add(campaign);
        var bookId = Guid.NewGuid();

        var artifact = GamebookPhotoArtifact.Create(campaign.Id, bookId, $"gamebook-photos/{campaign.Id}/photo-cost");
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(47, "The Hive awakens.", null) },
            "The Hive awakens.");
        artifactRepo.Store.Add(artifact);

        // TotalCost = 0.0005 USD, distinctive provider isolates this test from concurrent meter emitters.
        var cost = new LlmCost { InputCost = 0.0003m, OutputCost = 0.0002m, ModelId = "deepseek-chat", Provider = "test-seg-provider" };
        var handler = BuildHandler(
            campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo,
            llm: new CostReportingFakeLlmService(cost));

        var query = new TranslateGamebookSegmentQuery(campaign.Id, artifact.Id, 47, ownerId, bookId);

        // Act
        using var capture = new CostEurCapture();
        await foreach (var _ in handler.Handle(query, CancellationToken.None)) { }

        // Assert — cost_eur = TotalCost x UsdToEurRate, tagged with the chunk's provider
        var recorded = capture.ForProvider("test-seg-provider");
        recorded.Should().NotBeNull("StreamChunk.Cost carries a positive cost → translation_cost_eur must be recorded");
        recorded!.Value.Value.Should().BeApproximately(0.0005 * MeepleAiMetrics.UsdToEurRate, 1e-9);
    }

    [Fact]
    public async Task Handle_FinalChunkWithoutCost_DoesNotRecordCostEur()
    {
        // Arrange — default FakeLlmService yields a final chunk with Usage but no Cost.
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var paragraphRepo = new FakeParagraphRepo();
        var glossaryRepo = new FakeGlossaryRepo();
        var progressRepo = new FakeProgressRepo();

        var ownerId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "No-Cost Campaign");
        campaignRepo.Store.Add(campaign);
        var bookId = Guid.NewGuid();

        var artifact = GamebookPhotoArtifact.Create(campaign.Id, bookId, $"gamebook-photos/{campaign.Id}/photo-nocost");
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(47, "The Hive awakens.", null) },
            "The Hive awakens.");
        artifactRepo.Store.Add(artifact);

        var handler = BuildHandler(campaignRepo, artifactRepo, paragraphRepo, glossaryRepo, progressRepo);
        var query = new TranslateGamebookSegmentQuery(campaign.Id, artifact.Id, 47, ownerId, bookId);

        // Act
        using var capture = new CostEurCapture();
        await foreach (var _ in handler.Handle(query, CancellationToken.None)) { }

        // Assert — no cost means no EUR record (provider stays "unknown", helper skips the emit)
        capture.ForProvider("unknown").Should().BeNull("null Cost → handler must not record translation_cost_eur");
    }

    /// <summary>
    /// #2752: captures meepleai.gamebook.translation_cost_eur measurements, keyed by provider tag,
    /// so assertions filter to this test's distinctive provider and stay robust under parallel test runs.
    /// </summary>
    private sealed class CostEurCapture : IDisposable
    {
        private const string CostName = "meepleai.gamebook.translation_cost_eur";
        private readonly MeterListener _listener;
        private readonly List<(double Value, string? Provider)> _captured = new();
        private readonly object _gate = new();

        public CostEurCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName && instrument.Name == CostName)
                        l.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            {
                string? provider = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "provider")
                        provider = tag.Value as string;
                }
                lock (_gate)
                {
                    _captured.Add((value, provider));
                }
            });
            _listener.Start();
        }

        public (double Value, string? Provider)? ForProvider(string provider)
        {
            lock (_gate)
            {
                return _captured
                    .Where(m => string.Equals(m.Provider, provider, StringComparison.Ordinal))
                    .Select(m => ((double Value, string? Provider)?)m)
                    .LastOrDefault();
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
