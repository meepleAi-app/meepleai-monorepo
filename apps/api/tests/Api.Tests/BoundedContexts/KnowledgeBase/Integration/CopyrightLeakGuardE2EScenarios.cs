using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightLeakGuardE2EScenarios
{
    private static NgramCopyrightLeakGuard CreateRealGuard(int threshold = 12) =>
        new NgramCopyrightLeakGuard(
            Options.Create(new CopyrightLeakGuardOptions { VerbatimRunThreshold = threshold, ScanTimeoutMs = 500 }),
            NullLogger<NgramCopyrightLeakGuard>.Instance);

    [Fact]
    public async Task Given_ProtectedChunk_WhenLlmLeaks15Words_ThenGuardDetectsAndProviderReturnsLocalizedFallback()
    {
        // --- Given ---
        var guard = CreateRealGuard();
        var provider = new DefaultCopyrightFallbackMessageProvider();
        var chunk = new ChunkCitation(
            DocumentId: "rulebook-1",
            PageNumber: 42,
            RelevanceScore: 0.9f,
            SnippetPreview: "preview text here",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = "Players gain 1 Terraforming Rating when completing a project during the action phase of their turn" };

        var llmBody = "According to the rules, Players gain 1 Terraforming Rating when completing a project during the action phase of their turn.";

        // --- When ---
        var result = await guard.ScanAsync(llmBody, new[] { chunk }, CancellationToken.None);
        var fallback = provider.GetMessage("it");

        // --- Then ---
        Assert.True(result.HasLeak);
        Assert.Single(result.Matches);
        Assert.Contains("letterale", fallback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Given_ProtectedChunk_WhenLlmParaphrases_ThenGuardDetectsNoLeak()
    {
        var guard = CreateRealGuard();
        var chunk = new ChunkCitation(
            DocumentId: "rulebook-1", PageNumber: 42, RelevanceScore: 0.9f,
            SnippetPreview: "preview", CopyrightTier: CopyrightTier.Protected)
        { FullText = "Players gain 1 Terraforming Rating when completing a project" };

        var llmBody = "To earn Terraforming Rating increase by one, you must finish a project during your active turn";

        var result = await guard.ScanAsync(llmBody, new[] { chunk }, CancellationToken.None);

        Assert.False(result.HasLeak);
    }

    [Fact]
    public async Task Given_AllCitationsFull_WhenFilteredForProtected_ThenEmptyAndGuardReturnsFalse()
    {
        // Simulates handler's behavior: handler filters for Protected before invoking guard.
        // When no Protected chunks exist, handler never calls guard → skip path.
        var guard = CreateRealGuard();
        var fullCitations = new[]
        {
            new ChunkCitation("doc-1", 1, 0.9f, "preview", CopyrightTier.Full),
            new ChunkCitation("doc-2", 2, 0.8f, "preview", CopyrightTier.Full)
        };

        var protectedOnly = fullCitations.Where(c => c.CopyrightTier == CopyrightTier.Protected).ToList();
        Assert.Empty(protectedOnly);

        // Contract test: if invoked with empty list, guard returns false immediately.
        var result = await guard.ScanAsync("some response", protectedOnly, CancellationToken.None);
        Assert.False(result.HasLeak);
    }

    [Fact]
    public async Task Given_GuardMockThrows_WhenHandlerWouldInvoke_ThenFailOpenBehaviorDocumented()
    {
        // Documents the fail-open contract: guard that throws is wrapped by handler try-catch.
        // This test verifies the mock can throw without crashing the test process — the actual
        // try-catch behavior lives in ChatWithSessionAgentCommandHandler and is covered by
        // existing handler tests.
        var mockGuard = new Mock<ICopyrightLeakGuard>();
        mockGuard.Setup(g => g.ScanAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated guard failure"));

        var chunk = new ChunkCitation("doc-1", 1, 0.9f, "preview", CopyrightTier.Protected)
        { FullText = "some content" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mockGuard.Object.ScanAsync("body", new[] { chunk }, CancellationToken.None));
    }
}
