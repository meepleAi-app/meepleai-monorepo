using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class NgramCopyrightLeakGuardScenarios
{
    private static NgramCopyrightLeakGuard CreateGuard(int threshold = 12, int timeoutMs = 500) =>
        new NgramCopyrightLeakGuard(
            Options.Create(new CopyrightLeakGuardOptions
            {
                VerbatimRunThreshold = threshold,
                ScanTimeoutMs = timeoutMs
            }),
            NullLogger<NgramCopyrightLeakGuard>.Instance);

    private static ChunkCitation MakeProtectedChunk(string? fullText, string preview = "preview") =>
        new ChunkCitation(
            DocumentId: "doc-1",
            PageNumber: 42,
            RelevanceScore: 0.9f,
            SnippetPreview: preview,
            CopyrightTier: CopyrightTier.Protected)
        { FullText = fullText };

    [Fact]
    public async Task Given_EmptyBody_WhenScanned_ThenReturnsNoLeak()
    {
        var guard = CreateGuard();
        var chunk = MakeProtectedChunk("Players gain 1 Terraforming Rating when completing a project during the action phase of their turn");

        var result = await guard.ScanAsync("", new[] { chunk }, CancellationToken.None);

        Assert.False(result.HasLeak);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Given_15VerbatimWords_WhenScanned_ThenLeakDetected()
    {
        var guard = CreateGuard(threshold: 12);
        var chunk = MakeProtectedChunk(
            "Players gain 1 Terraforming Rating when completing a project during the action phase of their turn");
        var body = "According to the rules, Players gain 1 Terraforming Rating when completing a project during the action phase of their turn, which is the core mechanic.";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.True(result.HasLeak);
        Assert.Single(result.Matches);
        Assert.Equal("doc-1", result.Matches[0].DocumentId);
        // RunLength reports actual extended match length (16 tokens), not the threshold (12).
        Assert.Equal(16, result.Matches[0].RunLength);
    }

    [Fact]
    public async Task Given_10ConsecutiveWords_WhenScanned_ThenNoLeakDetected()
    {
        var guard = CreateGuard(threshold: 12);
        var chunk = MakeProtectedChunk("one two three four five six seven eight nine ten eleven twelve");
        var body = "Here are one two three four five six seven eight nine ten but then something different";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.False(result.HasLeak);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Given_WordsInReorderedSentence_WhenScanned_ThenNoLeakDetected()
    {
        var guard = CreateGuard(threshold: 5);
        var chunk = MakeProtectedChunk("alpha beta gamma delta epsilon zeta eta theta");
        var body = "epsilon delta gamma beta alpha zeta eta theta";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.False(result.HasLeak);
    }

    [Fact]
    public async Task Given_CaseAndPunctuationDifferent_WhenScanned_ThenLeakDetected()
    {
        var guard = CreateGuard(threshold: 5);
        var chunk = MakeProtectedChunk("Roll two dice and sum the values together");
        var body = "ROLL, TWO; DICE. AND SUM! THE VALUES: TOGETHER";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.True(result.HasLeak);
    }

    [Fact]
    public async Task Given_FullTextAvailable_WhenScanned_ThenScansFullText()
    {
        var guard = CreateGuard(threshold: 8);
        var chunk = new ChunkCitation(
            DocumentId: "doc-1", PageNumber: 1, RelevanceScore: 0.9f,
            SnippetPreview: "short five word preview text",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = "this is the complete full text containing many tokens for comprehensive leak detection scans" };

        var body = "Response mentions containing many tokens for comprehensive leak detection scans in the response";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.True(result.HasLeak);
    }

    [Fact]
    public async Task Given_FullTextNull_WhenScanned_ThenFallsBackToPreview()
    {
        var guard = CreateGuard(threshold: 5);
        var chunk = new ChunkCitation(
            DocumentId: "doc-1", PageNumber: 1, RelevanceScore: 0.9f,
            SnippetPreview: "alpha beta gamma delta epsilon zeta eta",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = null };

        var body = "text contains alpha beta gamma delta epsilon zeta eta in preview";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.True(result.HasLeak);
    }

    [Fact]
    public async Task Given_PreCancelledToken_WhenScanned_ThenThrowsCancellation()
    {
        var guard = CreateGuard(threshold: 3);
        var longText = string.Join(' ', Enumerable.Range(0, 10000).Select(i => $"tok{i}"));
        var chunk = MakeProtectedChunk(longText);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => guard.ScanAsync(longText, new[] { chunk }, cts.Token));
    }

    [Fact]
    public async Task Given_5ChunksOf1500Words_WhenScanned_ThenCompletesUnder50ms()
    {
        var guard = CreateGuard(threshold: 12);
        var chunks = Enumerable.Range(0, 5).Select(i =>
            MakeProtectedChunk(
                string.Join(' ', Enumerable.Range(0, 1500).Select(j => $"word{i}_{j}")))).ToArray();
        var body = string.Join(' ', Enumerable.Range(0, 500).Select(i => $"bodytok{i}"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await guard.ScanAsync(body, chunks, CancellationToken.None);
        sw.Stop();

        Assert.False(result.HasLeak);
        // CI tolerance: 500ms allowance on shared ARM64 runners (target <50ms locally)
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Scan took {sw.ElapsedMilliseconds}ms, expected <500ms on CI (target <50ms local)");
    }

    [Fact]
    public async Task Given_NonEmptyBodyAndEmptyCitations_WhenScanned_ThenReturnsNoLeak()
    {
        var guard = CreateGuard();

        var result = await guard.ScanAsync("This is a non-empty response body", Array.Empty<ChunkCitation>(), CancellationToken.None);

        Assert.False(result.HasLeak);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Given_ChunkWithEmptyFullTextAndPreview_WhenScanned_ThenSkipsCitation()
    {
        var guard = CreateGuard(threshold: 3);
        var chunkWithContent = new ChunkCitation(
            DocumentId: "doc-has-content", PageNumber: 1, RelevanceScore: 0.9f,
            SnippetPreview: "alpha beta gamma delta epsilon",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = "alpha beta gamma delta epsilon" };
        var emptyChunk = new ChunkCitation(
            DocumentId: "doc-empty", PageNumber: 2, RelevanceScore: 0.9f,
            SnippetPreview: "",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = null };

        var result = await guard.ScanAsync("alpha beta gamma delta epsilon", new[] { emptyChunk, chunkWithContent }, CancellationToken.None);

        // The empty chunk is skipped; the content chunk matches.
        Assert.True(result.HasLeak);
        Assert.Single(result.Matches);
        Assert.Equal("doc-has-content", result.Matches[0].DocumentId);
    }

    [Fact]
    public async Task Given_MultipleProtectedChunksAllMatch_WhenScanned_ThenAllRecorded()
    {
        var guard = CreateGuard(threshold: 4);
        var chunk1 = new ChunkCitation(
            DocumentId: "doc-1", PageNumber: 1, RelevanceScore: 0.9f,
            SnippetPreview: "alpha beta gamma delta",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = "alpha beta gamma delta" };
        var chunk2 = new ChunkCitation(
            DocumentId: "doc-2", PageNumber: 5, RelevanceScore: 0.8f,
            SnippetPreview: "omega sigma rho pi",
            CopyrightTier: CopyrightTier.Protected)
        { FullText = "omega sigma rho pi" };

        var body = "begins with alpha beta gamma delta and later mentions omega sigma rho pi at end";

        var result = await guard.ScanAsync(body, new[] { chunk1, chunk2 }, CancellationToken.None);

        Assert.True(result.HasLeak);
        Assert.Equal(2, result.Matches.Count);
        Assert.Contains(result.Matches, m => m.DocumentId == "doc-1");
        Assert.Contains(result.Matches, m => m.DocumentId == "doc-2");
    }

    [Fact]
    public async Task Given_ActualRunLongerThanThreshold_WhenScanned_ThenRunLengthReportsActualLength()
    {
        // 20-word verbatim match, threshold 12 — expect RunLength=20 (actual) not 12 (threshold)
        var guard = CreateGuard(threshold: 12);
        var longMatch = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty";
        var chunk = MakeProtectedChunk(longMatch);
        var body = $"Here: {longMatch} end.";

        var result = await guard.ScanAsync(body, new[] { chunk }, CancellationToken.None);

        Assert.True(result.HasLeak);
        Assert.Single(result.Matches);
        Assert.Equal(20, result.Matches[0].RunLength);
    }
}
