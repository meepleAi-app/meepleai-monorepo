using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #1520 — handler unit tests for the synchronous encounter cheatsheet
/// parse query. Authorization is enforced via <see cref="ICampaignOwnershipGuard"/>
/// (covered in <c>CampaignOwnershipGuardTests</c>); these tests use a no-op
/// guard and focus on the photo+segment lookup → LLM JSON extraction path.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class ParseEncounterQueryHandlerTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

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

    private sealed class AlwaysOwnedGuard : ICampaignOwnershipGuard
    {
        public int CallCount { get; private set; }

        public Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Guard that always denies — used to confirm the handler propagates the
    /// authorization failure before touching any repository.
    /// </summary>
    private sealed class DenyingGuard : ICampaignOwnershipGuard
    {
        public Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
            => throw new ForbiddenException("denied by test guard");
    }

    /// <summary>
    /// LLM fake that only implements <see cref="ILlmService.GenerateJsonAsync{T}"/>
    /// via a caller-provided factory; everything else is a benign no-op so the
    /// fake is reusable for negative scenarios that should never reach the LLM.
    /// </summary>
    private sealed class FakeLlmJsonService : ILlmService
    {
        public Func<object?>? JsonResultFactory { get; set; }
        public int JsonCallCount { get; private set; }
        public string? LastSystemPrompt { get; private set; }
        public string? LastUserPrompt { get; private set; }

        public Task<T?> GenerateJsonAsync<T>(string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, CancellationToken ct = default) where T : class
        {
            JsonCallCount++;
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            var raw = JsonResultFactory?.Invoke();
            return Task.FromResult((T?)raw);
        }

        public Task<LlmCompletionResult> GenerateCompletionAsync(string s, string u, RequestSource r = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

#pragma warning disable CS1998 // async method lacks await — fake intentionally non-async
        public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
            string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield break;
        }

        public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
            IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield break;
        }
#pragma warning restore CS1998

        public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));

        public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(string explicitModel, string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
            => Task.FromResult(LlmCompletionResult.CreateSuccess(string.Empty));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ParseEncounterQueryHandler BuildHandler(
        FakeArtifactRepo artifactRepo,
        ILlmService llm,
        ICampaignOwnershipGuard? guard = null) =>
        new(
            artifactRepo,
            llm,
            guard ?? new AlwaysOwnedGuard(),
            NullLogger<ParseEncounterQueryHandler>.Instance);

    private static GamebookPhotoArtifact CreateSegmentedArtifact(
        Guid campaignId,
        Guid bookId,
        int paragraphNumber,
        string sourceText)
    {
        var artifact = GamebookPhotoArtifact.Create(campaignId, bookId, $"gamebook-photos/{campaignId}/photo-encounter");
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(paragraphNumber, sourceText, null) },
            sourceText);
        return artifact;
    }

    private static EncounterCheatsheetDto SampleCheatsheet() => new(
        Enemies: new[]
        {
            new EncounterEnemyDto(
                Name: "Goblin Scout",
                Icon: "👹",
                ParagraphMarker: "§218",
                Hp: "8",
                Atk: "+2",
                Def: "10",
                Mov: "6"),
        },
        Options: new[]
        {
            new EncounterOptionDto(
                Label: "Attack head-on",
                DiceRoll: new EncounterDiceRollDto(Sides: 6, Count: 1, Modifier: 1, Threshold: 4),
                Outcome: "→ §219"),
            new EncounterOptionDto(
                Label: "Flee",
                DiceRoll: null,
                Outcome: "→ §147"),
        },
        Conditions: new EncounterConditionsDto(
            Win: "Goblin defeated, gain 1 trinket",
            Loss: "Lose 2 HP, return to §147"),
        Confidence: new EncounterConfidenceDto(
            Enemies: 0.92,
            Options: 0.81,
            Conditions: 0.78));

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_ReturnsCheatsheetFromLlm()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var artifact = CreateSegmentedArtifact(campaignId, bookId, paragraphNumber: 218, sourceText: "Goblin Scout HP 8 ATK +2 DEF 10 MOV 6. (1) Attack head-on (1d6+1 ≥4). (2) Flee → §147.");
        var artifactRepo = new FakeArtifactRepo();
        artifactRepo.Store.Add(artifact);

        var expected = SampleCheatsheet();
        var llm = new FakeLlmJsonService { JsonResultFactory = () => expected };

        var guard = new AlwaysOwnedGuard();
        var handler = BuildHandler(artifactRepo, llm, guard);
        var query = new ParseEncounterQuery(campaignId, artifact.Id, ParagraphNumber: 218, CallerUserId: userId, GameBookId: bookId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
        llm.JsonCallCount.Should().Be(1, "the handler must invoke the LLM exactly once per parse request");
        llm.LastUserPrompt.Should().Contain("Goblin Scout", "the user prompt must contain the source text from the segment");
        llm.LastSystemPrompt.Should().NotBeNullOrWhiteSpace("a system prompt describing the JSON schema must be sent");
        guard.CallCount.Should().Be(1, "ownership must be enforced exactly once before any photo lookup");
    }

    [Fact]
    public async Task Handle_PhotoNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var artifactRepo = new FakeArtifactRepo(); // empty
        var llm = new FakeLlmJsonService { JsonResultFactory = () => SampleCheatsheet() };
        var handler = BuildHandler(artifactRepo, llm);
        var query = new ParseEncounterQuery(Guid.NewGuid(), Guid.NewGuid(), 218, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Photo*not found*");
        llm.JsonCallCount.Should().Be(0, "the LLM must not be invoked when the photo lookup fails");
    }

    [Fact]
    public async Task Handle_PhotoBelongsToDifferentCampaign_ThrowsConflictException()
    {
        // Arrange
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var artifact = CreateSegmentedArtifact(campaignA, bookId, 218, "...");
        var artifactRepo = new FakeArtifactRepo();
        artifactRepo.Store.Add(artifact);

        var llm = new FakeLlmJsonService { JsonResultFactory = () => SampleCheatsheet() };
        var handler = BuildHandler(artifactRepo, llm);

        // Caller asks for the artifact but under campaignB, which does NOT own it
        var query = new ParseEncounterQuery(campaignB, artifact.Id, 218, Guid.NewGuid(), bookId);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*does not belong*");
        llm.JsonCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SegmentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var artifact = CreateSegmentedArtifact(campaignId, bookId, paragraphNumber: 218, sourceText: "...");
        var artifactRepo = new FakeArtifactRepo();
        artifactRepo.Store.Add(artifact);

        var llm = new FakeLlmJsonService { JsonResultFactory = () => SampleCheatsheet() };
        var handler = BuildHandler(artifactRepo, llm);

        // Photo has §218 but caller asks for §999
        var query = new ParseEncounterQuery(campaignId, artifact.Id, ParagraphNumber: 999, CallerUserId: Guid.NewGuid(), GameBookId: bookId);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Segment*999*not found*");
        llm.JsonCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_LlmReturnsNull_ThrowsConflictException()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var artifact = CreateSegmentedArtifact(campaignId, bookId, 218, "Encounter text the LLM cannot parse.");
        var artifactRepo = new FakeArtifactRepo();
        artifactRepo.Store.Add(artifact);

        var llm = new FakeLlmJsonService { JsonResultFactory = () => null };
        var handler = BuildHandler(artifactRepo, llm);
        var query = new ParseEncounterQuery(campaignId, artifact.Id, 218, Guid.NewGuid(), bookId);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        // ConflictException (HTTP 409) — the photo is fine, the LLM just failed to
        // produce a usable structured cheatsheet for this paragraph. The FE will
        // surface a "retry with another photo / manual input" affordance.
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cheatsheet*could not be extracted*");
        llm.JsonCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OwnershipDenied_PropagatesBeforeAnyRepoAccess()
    {
        // Arrange — even a perfectly good artifact must not be touched when the
        // ownership guard rejects the caller.
        var campaignId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var artifact = CreateSegmentedArtifact(campaignId, bookId, 218, "...");
        var artifactRepo = new FakeArtifactRepo();
        artifactRepo.Store.Add(artifact);

        var llm = new FakeLlmJsonService { JsonResultFactory = () => SampleCheatsheet() };
        var handler = BuildHandler(artifactRepo, llm, new DenyingGuard());
        var query = new ParseEncounterQuery(campaignId, artifact.Id, 218, Guid.NewGuid(), bookId);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*denied by test guard*");
        llm.JsonCallCount.Should().Be(0, "the LLM must not be invoked when ownership is denied");
    }
}
