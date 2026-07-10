using System.Text.Json;

using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for the <see cref="MechanicCard"/> aggregate (#527, M1.5): publish factory,
/// snapshot immutability (AD-1), suppress/unsuppress (T5), and the
/// <see cref="MechanicAnalysis.MarkPublished"/> publish-once invariant.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardTests
{
    private static readonly DateTime Now = new(2026, 07, 10, 12, 0, 0, DateTimeKind.Utc);

    // ============================================================
    // PublishFromAnalysis — happy path
    // ============================================================

    [Fact]
    public void PublishFromAnalysis_WithApprovedAnalysis_ProducesCardV1()
    {
        var analysis = PublishedAnalysis();
        var publisher = Guid.NewGuid();

        var card = MechanicCard.PublishFromAnalysis(
            analysis, overrideTitle: "Catan: Mechanics", GameContext("Catan"), publisher, version: 1, Now);

        card.Id.Should().NotBe(Guid.Empty);
        card.SharedGameId.Should().Be(analysis.SharedGameId);
        card.OriginAnalysisId.Should().Be(analysis.Id);
        card.Origin.Should().Be(MechanicCardOrigin.AiReviewed);
        card.Title.Should().Be("Catan: Mechanics");
        card.Version.Should().Be(1);
        card.IsSuppressed.Should().BeFalse();
        card.ErrorReportsCount.Should().Be(0);
        card.PublishedBy.Should().Be(publisher);
        card.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void PublishFromAnalysis_WithoutTitle_UsesDefaultFromGameName()
    {
        var analysis = PublishedAnalysis();

        var card = MechanicCard.PublishFromAnalysis(
            analysis, overrideTitle: null, GameContext("7 Wonders"), Guid.NewGuid(), version: 1, Now);

        card.Title.Should().Be("7 Wonders — Mechanics Card");
    }

    [Fact]
    public void PublishFromAnalysis_RaisesMechanicCardPublishedEvent()
    {
        var analysis = PublishedAnalysis();

        var card = MechanicCard.PublishFromAnalysis(
            analysis, "Catan: Mechanics", GameContext("Catan"), Guid.NewGuid(), version: 1, Now);

        card.DomainEvents.Should().ContainSingle(e => e is MechanicCardPublishedEvent);
    }

    [Fact]
    public void PublishFromAnalysis_SnapshotsClaimsAndMetadataIntoContent()
    {
        var analysis = PublishedAnalysis();
        // The handler resolves the game from analysis.SharedGameId, so the context mirrors it.
        var gameContext = new MechanicCardGameContext
        {
            SharedGameId = analysis.SharedGameId,
            SharedGameName = "Catan",
            Publisher = "Kosmos",
            Language = "it"
        };

        var card = MechanicCard.PublishFromAnalysis(
            analysis, "Catan: Mechanics", gameContext, Guid.NewGuid(), 1, Now);

        using var doc = JsonDocument.Parse(card.Content);
        var root = doc.RootElement;

        root.GetProperty("schema_version").GetInt32().Should().Be(2); // #2782 D6: bumped for real validations projection.
        root.GetProperty("source_analysis_id").GetGuid().Should().Be(analysis.Id);
        root.GetProperty("source_prompt_version").GetString().Should().Be("v1");

        var claims = root.GetProperty("claims");
        claims.GetArrayLength().Should().Be(1);
        var claim = claims[0];
        claim.GetProperty("claim").GetString().Should().Be("Draw phase");
        claim.GetProperty("section").GetString().Should().Be("Mechanics");
        claim.GetProperty("citations").GetArrayLength().Should().Be(1);
        claim.GetProperty("citations")[0].GetProperty("pdf_id").GetGuid().Should().Be(analysis.PdfDocumentId);
        claim.GetProperty("citations")[0].GetProperty("pdf_page").GetInt32().Should().Be(1);

        var metadata = root.GetProperty("metadata");
        metadata.GetProperty("shared_game_id").GetGuid().Should().Be(analysis.SharedGameId);
        metadata.GetProperty("shared_game_name").GetString().Should().Be("Catan");
        metadata.GetProperty("publisher").GetString().Should().Be("Kosmos");
        metadata.GetProperty("language").GetString().Should().Be("it");
    }

    // ============================================================
    // PublishFromAnalysis — preconditions
    // ============================================================

    [Fact]
    public void PublishFromAnalysis_WithNonPublishedAnalysis_Throws()
    {
        var analysis = NewAnalysisWithClaim(); // Draft

        var act = () => MechanicCard.PublishFromAnalysis(
            analysis, null, GameContext("Catan"), Guid.NewGuid(), 1, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Published*");
    }

    [Fact]
    public void PublishFromAnalysis_WhenAnalysisAlreadyPublished_Throws()
    {
        var analysis = PublishedAnalysis();
        analysis.MarkPublished(Guid.NewGuid());

        var act = () => MechanicCard.PublishFromAnalysis(
            analysis, null, GameContext("Catan"), Guid.NewGuid(), 1, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already published*");
    }

    [Fact]
    public void PublishFromAnalysis_WithEmptyPublisher_Throws()
    {
        var analysis = PublishedAnalysis();

        var act = () => MechanicCard.PublishFromAnalysis(
            analysis, null, GameContext("Catan"), Guid.Empty, 1, Now);

        act.Should().Throw<ArgumentException>().WithMessage("*PublisherId*");
    }

    [Fact]
    public void PublishFromAnalysis_WithVersionBelowOne_Throws()
    {
        var analysis = PublishedAnalysis();

        var act = () => MechanicCard.PublishFromAnalysis(
            analysis, null, GameContext("Catan"), Guid.NewGuid(), version: 0, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("ABC")] // < 5
    public void PublishFromAnalysis_WithTooShortTitle_Throws(string title)
    {
        var analysis = PublishedAnalysis();

        var act = () => MechanicCard.PublishFromAnalysis(
            analysis, title, GameContext("Catan"), Guid.NewGuid(), 1, Now);

        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void PublishFromAnalysis_WithTooLongTitle_Throws()
    {
        var analysis = PublishedAnalysis();

        var act = () => MechanicCard.PublishFromAnalysis(
            analysis, new string('x', 201), GameContext("Catan"), Guid.NewGuid(), 1, Now);

        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    // ============================================================
    // Suppress / Unsuppress (T5)
    // ============================================================

    [Fact]
    public void Suppress_MarksSuppressedAndRaisesEvent()
    {
        var card = NewCard();
        var actor = Guid.NewGuid();

        card.Suppress(actor, "Publisher DMCA takedown request received via legal.", Now);

        card.IsSuppressed.Should().BeTrue();
        card.SuppressedBy.Should().Be(actor);
        card.SuppressedReason.Should().StartWith("Publisher DMCA");
        card.DomainEvents.Should().ContainSingle(e => e is MechanicCardSuppressedEvent);
    }

    [Fact]
    public void Suppress_WhenAlreadySuppressed_Throws()
    {
        var card = NewCard();
        card.Suppress(Guid.NewGuid(), "Publisher DMCA takedown request received via legal.", Now);

        var act = () => card.Suppress(Guid.NewGuid(), "Publisher DMCA takedown request received via legal.", Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already suppressed*");
    }

    [Theory]
    [InlineData("short")] // < 20
    public void Suppress_WithInvalidReasonLength_Throws(string reason)
    {
        var card = NewCard();

        var act = () => card.Suppress(Guid.NewGuid(), reason, Now);

        act.Should().Throw<ArgumentException>().WithMessage("*20..500*");
    }

    [Fact]
    public void Unsuppress_ClearsSuppressionState()
    {
        var card = NewCard();
        card.Suppress(Guid.NewGuid(), "Publisher DMCA takedown request received via legal.", Now);

        card.Unsuppress(Guid.NewGuid(), Now);

        card.IsSuppressed.Should().BeFalse();
        card.SuppressedReason.Should().BeNull();
        card.SuppressedAt.Should().BeNull();
        card.SuppressedBy.Should().BeNull();
    }

    [Fact]
    public void Unsuppress_WhenNotSuppressed_Throws()
    {
        var card = NewCard();

        var act = () => card.Unsuppress(Guid.NewGuid(), Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not suppressed*");
    }

    [Fact]
    public void MarkErrorReport_IncrementsCounter()
    {
        var card = NewCard();

        card.MarkErrorReport(Now);
        card.MarkErrorReport(Now);

        card.ErrorReportsCount.Should().Be(2);
    }

    // ============================================================
    // MechanicAnalysis.MarkPublished (publish-once invariant)
    // ============================================================

    [Fact]
    public void MarkPublished_OnPublishedAnalysis_SetsPublishedCardId()
    {
        var analysis = PublishedAnalysis();
        var cardId = Guid.NewGuid();

        analysis.MarkPublished(cardId);

        analysis.PublishedCardId.Should().Be(cardId);
    }

    [Fact]
    public void MarkPublished_WhenAlreadyPublished_Throws()
    {
        var analysis = PublishedAnalysis();
        analysis.MarkPublished(Guid.NewGuid());

        var act = () => analysis.MarkPublished(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>().WithMessage("*already published*");
    }

    [Fact]
    public void MarkPublished_WithEmptyCardId_Throws()
    {
        var analysis = PublishedAnalysis();

        var act = () => analysis.MarkPublished(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*CardId*");
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static MechanicCardGameContext GameContext(string name, string? publisher = null, string language = "en") =>
        new() { SharedGameId = Guid.NewGuid(), SharedGameName = name, Publisher = publisher, Language = language };

    private static MechanicCard NewCard()
    {
        var analysis = PublishedAnalysis();
        return MechanicCard.PublishFromAnalysis(
            analysis, "Catan: Mechanics", GameContext("Catan"), Guid.NewGuid(), version: 1, Now);
    }

    private static MechanicAnalysis NewAnalysisWithClaim()
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: Now,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        var claim = MechanicClaim.Create(
            analysisId: analysis.Id,
            section: MechanicSection.Mechanics,
            text: "Draw phase",
            displayOrder: 0,
            citations: new[]
            {
                MechanicCitation.Create(analysis.Id, pdfPage: 1, quote: "Each turn draw one card.", chunkId: null, displayOrder: 0)
            });

        analysis.AddClaim(claim);
        return analysis;
    }

    private static MechanicAnalysis PublishedAnalysis()
    {
        var analysis = NewAnalysisWithClaim();
        var reviewer = Guid.NewGuid();
        analysis.SubmitForReview(reviewer, Now);
        analysis.ApproveClaim(analysis.Claims[0].Id, reviewer, Now);
        analysis.Approve(reviewer, Now);
        return analysis;
    }
}
