using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TranslateGamebookSegmentQueryHandler BuildHandler(
        FakeCampaignRepo campaignRepo,
        FakeArtifactRepo artifactRepo,
        FakeParagraphRepo paragraphRepo,
        FakeGlossaryRepo glossaryRepo,
        FakeProgressRepo progressRepo,
        ILlmService? llm = null) =>
        new(
            campaignRepo,
            artifactRepo,
            paragraphRepo,
            glossaryRepo,
            progressRepo,
            llm ?? new FakeLlmService(),
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
}
