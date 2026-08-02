using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #3490 (ADR-090): the shared grounded finalize. Pins the trust-critical invariants that used to be
/// duplicated across the two in-session handlers — copyright leak guard (fail-open scan / fail-closed
/// sanitize), Full-gated tier-nulling, grounding from citation count (#3388), and cancellation propagation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3490")]
public sealed class GroundedAnswerServiceTests
{
    private readonly Mock<ICopyrightLeakGuard> _leakGuard = new();
    private readonly Mock<ICopyrightFallbackMessageProvider> _fallback = new();

    public GroundedAnswerServiceTests()
    {
        _fallback.Setup(f => f.GetMessage(It.IsAny<string>())).Returns("[copyright fallback]");
        // Default: no leak.
        _leakGuard
            .Setup(g => g.ScanAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CopyrightLeakResult(HasLeak: false, Matches: Array.Empty<LeakMatch>()));
    }

    private GroundedAnswerService BuildService() => new(
        _leakGuard.Object,
        _fallback.Object,
        Options.Create(new CopyrightLeakGuardOptions()),
        NullLogger<GroundedAnswerService>.Instance);

    private static ChunkCitation Cite(CopyrightTier tier, string doc = "doc-1", string preview = "snippet") =>
        new(DocumentId: doc, PageNumber: 3, RelevanceScore: 0.9f, SnippetPreview: preview, CopyrightTier: tier, IsPublic: true);

    [Fact]
    public async Task Finalize_FullTierCitation_MapsPreviewAndGrounded()
    {
        var svc = BuildService();

        var result = await svc.FinalizeAsync(
            "You score at game end. [Page 3]",
            new List<ChunkCitation> { Cite(CopyrightTier.Full, preview: "Score at game end.") },
            "How do I score?", "en", CancellationToken.None);

        result.GroundingStatus.Should().Be(GroundingStatus.Grounded);
        result.CitationDtos.Should().ContainSingle();
        result.CitationDtos[0].SnippetPreview.Should().Be("Score at game end.", "Full tier keeps the preview");
        result.Confidence.Should().NotBeNull();
        result.LeakMatchCount.Should().Be(0);
        result.ResponseText.Should().Contain("[Page 3]");
    }

    [Fact]
    public async Task Finalize_ProtectedTierCitation_NullsPreviewInDto()
    {
        var svc = BuildService();

        var result = await svc.FinalizeAsync(
            "answer", new List<ChunkCitation> { Cite(CopyrightTier.Protected, preview: "verbatim protected text") },
            "q", "en", CancellationToken.None);

        result.CitationDtos.Should().ContainSingle();
        result.CitationDtos[0].SnippetPreview.Should().BeNull("Protected tier must not leak the preview verbatim");
    }

    [Fact]
    public async Task Finalize_NoCitations_Ungrounded()
    {
        var svc = BuildService();

        var result = await svc.FinalizeAsync("no match", new List<ChunkCitation>(), "q", "en", CancellationToken.None);

        result.GroundingStatus.Should().Be(GroundingStatus.Ungrounded);
        result.CitationDtos.Should().BeEmpty();
        result.Confidence.Should().BeNull();
    }

    [Fact]
    public async Task Finalize_LeakDetected_SanitizesAndReportsMatchCount()
    {
        // Arrange — a Protected citation and a leak match.
        _leakGuard
            .Setup(g => g.ScanAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CopyrightLeakResult(
                HasLeak: true,
                Matches: new List<LeakMatch> { new("doc-1", 3, 12, 0, "verbatim run") }));
        var svc = BuildService();

        var result = await svc.FinalizeAsync(
            "a verbatim run leaked here",
            new List<ChunkCitation> { Cite(CopyrightTier.Protected) },
            "q", "en", CancellationToken.None);

        result.ResponseText.Should().Be("[copyright fallback]", "leaked text must be replaced by the fallback");
        result.LeakMatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Finalize_ScanThrowsNonCancel_FailsOpen()
    {
        // Arrange — the scan faults (not a cancellation). Must fail-open: allow the response, no sanitize.
        _leakGuard
            .Setup(g => g.ScanAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("guard boom"));
        var svc = BuildService();

        var result = await svc.FinalizeAsync(
            "original answer",
            new List<ChunkCitation> { Cite(CopyrightTier.Protected) },
            "q", "en", CancellationToken.None);

        result.ResponseText.Should().Be("original answer", "fail-open on a scan fault: ship the answer unsanitized");
        result.LeakMatchCount.Should().Be(0);
    }

    [Fact]
    public async Task Finalize_CallerCancelledDuringScan_Propagates()
    {
        // Arrange — the caller's token is cancelled and the scan throws OCE. Must propagate (so a
        // budget/client cancellation degrades) rather than fail-open into a possibly-unscanned answer.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _leakGuard
            .Setup(g => g.ScanAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var svc = BuildService();

        var act = () => svc.FinalizeAsync(
            "answer", new List<ChunkCitation> { Cite(CopyrightTier.Protected) }, "q", "en", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
