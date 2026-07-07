using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using System.Diagnostics.Metrics;
using Api.Middleware.Exceptions;
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
public sealed class TranslateGamebookTextQueryHandlerTests
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

    private sealed class FakeGlossaryRepo : IGamebookGlossaryRepository
    {
        public List<GamebookGlossaryEntry> Store { get; } = new();

        public Task<IReadOnlyList<GamebookGlossaryEntry>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GamebookGlossaryEntry>>(Store.Where(x => x.CampaignId == campaignId).ToList());

        public Task<GamebookGlossaryEntry?> GetByTermAsync(Guid campaignId, string termEn, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.CampaignId == campaignId && x.TermEn == termEn));

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

        public Task AddAsync(GamebookGlossaryEntry entry, CancellationToken cancellationToken = default)
        { Store.Add(entry); return Task.CompletedTask; }

        public void Remove(GamebookGlossaryEntry entry) => Store.Remove(entry);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Fake LLM that yields configurable deltas and a final chunk with usage.
    /// </summary>
    private sealed class FakeLlmService : ILlmService
    {
        private readonly string[] _deltas;
        public string? CapturedSystemPrompt { get; private set; }

        public FakeLlmService(string[]? deltas = null)
        {
            _deltas = deltas ?? new[] { "Ciao mondo." };
        }

        public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
            string systemPrompt,
            string userPrompt,
            RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CapturedSystemPrompt = systemPrompt;
            foreach (var delta in _deltas)
            {
                yield return new StreamChunk(Content: delta, IsFinal: false);
                await Task.Yield();
            }
            yield return new StreamChunk(null, IsFinal: true, Usage: new LlmUsage(10, 5, 15));
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
            yield return new StreamChunk("Ciao mondo.", IsFinal: false);
            await Task.Yield();
            yield return new StreamChunk(null, Usage: new LlmUsage(10, 5, 15), Cost: _cost, IsFinal: true);
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
    /// Guard that always confirms ownership (for happy-path tests).
    /// </summary>
    private sealed class AlwaysOwnedGuard : ICampaignOwnershipGuard
    {
        public Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Guard that always throws ForbiddenException (for ownership-denial tests).
    /// </summary>
    private sealed class AlwaysDeniedGuard : ICampaignOwnershipGuard
    {
        public Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
            => throw new ForbiddenException("Not the owner.");
    }

    // ── Fixture IDs ───────────────────────────────────────────────────────────

    private static readonly Guid CampaignId = Guid.Parse("11111111-1111-4111-a111-111111111111");
    private static readonly Guid GameBookId = Guid.Parse("22222222-2222-4222-a222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-4333-a333-333333333333");
    private static readonly Guid SharedGameId = Guid.Parse("44444444-4444-4444-a444-444444444444");

    private static TranslateGamebookTextQuery DefaultQuery(string text = "Hello world.", string sourceLang = "EN") =>
        new(CampaignId, text, sourceLang, "IT", GameBookId, UserId);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TranslateGamebookTextQueryHandler BuildHandler(
        FakeCampaignRepo campaignRepo,
        FakeGlossaryRepo glossaryRepo,
        ILlmService? llm = null,
        ICampaignOwnershipGuard? guard = null) =>
        new(
            campaignRepo,
            glossaryRepo,
            llm ?? new FakeLlmService(),
            guard ?? new AlwaysOwnedGuard(),
            NullLogger<TranslateGamebookTextQueryHandler>.Instance);

    private static (FakeCampaignRepo campaigns, FakeGlossaryRepo glossary)
        BuildRepos()
    {
        var campaigns = new FakeCampaignRepo();
        var glossary = new FakeGlossaryRepo();

        // Seed campaign
        campaigns.Store.Add(
            GamebookCampaignSession.Create(GameRef.Shared(SharedGameId), UserId, "Test Campaign"));
        return (campaigns, glossary);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnershipDenied_ThrowsForbidden()
    {
        var (campaigns, glossary) = BuildRepos();
        var handler = BuildHandler(campaigns, glossary, guard: new AlwaysDeniedGuard());

        await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            await foreach (var _ in handler.Handle(DefaultQuery(), CancellationToken.None)) { }
        });
    }

    [Fact]
    public async Task Handle_HappyPath_EmitsDeltaChunks_AndFinalChunk()
    {
        var (campaigns, glossary) = BuildRepos();

        // Use the actual campaign Id from the store
        var campaign = campaigns.Store[0];
        var query = new TranslateGamebookTextQuery(campaign.Id, "Hello world.", "EN", "IT", GameBookId, UserId);
        var llm = new FakeLlmService(new[] { "Ciao ", "mondo." });
        var handler = BuildHandler(campaigns, glossary, llm: llm);

        var chunks = new List<TranslateChunk>();
        await foreach (var c in handler.Handle(query, CancellationToken.None))
            chunks.Add(c);

        chunks.Should().HaveCount(3, "two content deltas + one final");
        chunks[0].Delta.Should().Be("Ciao ");
        chunks[0].IsComplete.Should().BeFalse();
        chunks[1].Delta.Should().Be("mondo.");
        chunks[1].IsComplete.Should().BeFalse();
        chunks[2].IsComplete.Should().BeTrue();
        chunks[2].ParagraphId.Should().BeNull("DEC-BE-11: no TranslatedParagraph persisted");
        chunks[2].DetectedSourceLang.Should().Be("EN", "DEC-BE-13: echoes user-provided source lang");
        chunks[2].LangDetectionConfidence.Should().BeNull("no detection happened in manual mode");
    }

    [Fact]
    public async Task Handle_WithGlossaryMatchInSourceAndTranslation_PopulatesAppliedTerms()
    {
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];

        glossary.Store.Add(GamebookGlossaryEntry.Create(campaign.Id, "goblin", "goblin", GlossarySource.AutoBootstrap, UserId));
        glossary.Store.Add(GamebookGlossaryEntry.Create(campaign.Id, "sword", "spada", GlossarySource.AutoBootstrap, UserId));

        var llm = new FakeLlmService(new[] { "Vedi un goblin con una spada." });
        var handler = BuildHandler(campaigns, glossary, llm: llm);

        var query = new TranslateGamebookTextQuery(campaign.Id, "You see a goblin with a sword.", "EN", "IT", GameBookId, UserId);
        var chunks = new List<TranslateChunk>();
        await foreach (var c in handler.Handle(query, CancellationToken.None))
            chunks.Add(c);

        var final = chunks.Last();
        final.AppliedTerms.Should().Contain("goblin");
        final.AppliedTerms.Should().Contain("sword");
    }

    [Fact]
    public async Task Handle_DoesNotPersistTranslatedParagraph()
    {
        // DEC-BE-11: handler has no ITranslatedParagraphRepository dependency — verified at compile time.
        // This test confirms the happy path completes with ParagraphId=null in the final chunk.
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];
        var handler = BuildHandler(campaigns, glossary);

        var chunks = new List<TranslateChunk>();
        await foreach (var c in handler.Handle(
            new TranslateGamebookTextQuery(campaign.Id, "Hello.", "EN", "IT", GameBookId, UserId),
            CancellationToken.None))
            chunks.Add(c);

        chunks.Last().ParagraphId.Should().BeNull("DEC-BE-11: no TranslatedParagraph persisted, so ParagraphId is null");
    }

    [Fact]
    public async Task Handle_DoesNotUpdateSessionBookProgress()
    {
        // DEC-BE-12: handler has no ISessionBookProgressRepository dependency — verified at compile time.
        // This test confirms the happy path completes without touching progress (final chunk LangDetectionConfidence=null).
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];
        var handler = BuildHandler(campaigns, glossary);

        var chunks = new List<TranslateChunk>();
        await foreach (var c in handler.Handle(
            new TranslateGamebookTextQuery(campaign.Id, "Hello.", "EN", "IT", GameBookId, UserId),
            CancellationToken.None))
            chunks.Add(c);

        chunks.Last().LangDetectionConfidence.Should().BeNull("DEC-BE-12: manual mode, no detection confidence");
    }

    [Fact]
    public async Task Handle_DoesNotTouchCampaign()
    {
        // Verifies that no SaveChanges is called on campaigns repo (campaign.Touch() not invoked)
        var campaigns = new FakeCampaignRepoWithSaveTracking();
        campaigns.Store.Add(
            GamebookCampaignSession.Create(GameRef.Shared(SharedGameId), UserId, "Test"));
        var campaign = campaigns.Store[0];

        var glossary = new FakeGlossaryRepo();
        var handler = new TranslateGamebookTextQueryHandler(
            campaigns,
            glossary,
            new FakeLlmService(),
            new AlwaysOwnedGuard(),
            NullLogger<TranslateGamebookTextQueryHandler>.Instance);

        await foreach (var _ in handler.Handle(
            new TranslateGamebookTextQuery(campaign.Id, "Hello.", "EN", "IT", GameBookId, UserId),
            CancellationToken.None)) { }

        campaigns.SaveChangesCalled.Should().BeFalse("DEC-BE-12: manual mode must NOT call SaveChanges on campaigns (no Touch())");
    }

    [Fact]
    public async Task Handle_WithSourceLangFR_BuildsPromptWithFrench()
    {
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];
        var llm = new FakeLlmService(new[] { "Ciao." });
        var handler = BuildHandler(campaigns, glossary, llm: llm);

        var query = new TranslateGamebookTextQuery(campaign.Id, "Bonjour.", "FR", "IT", GameBookId, UserId);
        await foreach (var _ in handler.Handle(query, CancellationToken.None)) { }

        llm.CapturedSystemPrompt.Should().NotBeNull();
        llm.CapturedSystemPrompt.Should().Contain("French to Italian",
            "SourceLang=FR must drive prompt to 'French to Italian'");
    }

    [Fact]
    public async Task Handle_WithSourceLangCaseInsensitive_NormalizesToUpperCase()
    {
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];
        var llm = new FakeLlmService(new[] { "Ciao." });
        var handler = BuildHandler(campaigns, glossary, llm: llm);

        var query = new TranslateGamebookTextQuery(campaign.Id, "Bonjour.", "fr", "IT", GameBookId, UserId);
        var chunks = new List<TranslateChunk>();
        await foreach (var c in handler.Handle(query, CancellationToken.None))
            chunks.Add(c);

        llm.CapturedSystemPrompt.Should().Contain("French",
            "lowercase 'fr' must normalize to 'FR' → 'French' in prompt");
        chunks.Last().DetectedSourceLang.Should().Be("FR",
            "final chunk must echo normalized uppercase source lang code");
    }

    // ── #2752: translation_cost_eur wiring from StreamChunk.Cost ────────────────

    [Fact]
    public async Task Handle_FinalChunkCarriesCost_RecordsTranslationCostEur()
    {
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];

        // TotalCost = 0.0005 USD, distinctive provider isolates this test from concurrent meter emitters.
        var cost = new LlmCost { InputCost = 0.0003m, OutputCost = 0.0002m, ModelId = "deepseek-chat", Provider = "test-text-provider" };
        var handler = BuildHandler(campaigns, glossary, llm: new CostReportingFakeLlmService(cost));
        var query = new TranslateGamebookTextQuery(campaign.Id, "Hello world.", "EN", "IT", GameBookId, UserId);

        using var capture = new CostEurCapture();
        await foreach (var _ in handler.Handle(query, CancellationToken.None)) { }

        var recorded = capture.ForProvider("test-text-provider");
        recorded.Should().NotBeNull("StreamChunk.Cost carries a positive cost → translation_cost_eur must be recorded");
        recorded!.Value.Value.Should().BeApproximately(0.0005 * MeepleAiMetrics.UsdToEurRate, 1e-9);
    }

    [Fact]
    public async Task Handle_FinalChunkWithoutCost_DoesNotRecordCostEur()
    {
        var (campaigns, glossary) = BuildRepos();
        var campaign = campaigns.Store[0];

        // Default FakeLlmService yields a final chunk with Usage but no Cost.
        var handler = BuildHandler(campaigns, glossary);
        var query = new TranslateGamebookTextQuery(campaign.Id, "Hello world.", "EN", "IT", GameBookId, UserId);

        using var capture = new CostEurCapture();
        await foreach (var _ in handler.Handle(query, CancellationToken.None)) { }

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

    // ── Additional tracking fake ──────────────────────────────────────────────

    private sealed class FakeCampaignRepoWithSaveTracking : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();
        public bool SaveChangesCalled { get; private set; }

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default) { Store.Add(s); return Task.CompletedTask; }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }
    }
}
