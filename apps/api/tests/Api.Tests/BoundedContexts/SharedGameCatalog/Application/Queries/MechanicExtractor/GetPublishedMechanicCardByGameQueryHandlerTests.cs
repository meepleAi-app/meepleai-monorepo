using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetPublishedMechanicCardByGameQueryHandlerTests
{
    private static MechanicCard BuildCard(Guid gameId, string contentJson, bool suppressed = false) =>
        MechanicCard.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: gameId,
            originAnalysisId: Guid.NewGuid(),
            origin: MechanicCardOrigin.AiReviewed,
            title: "Catan — Comprehension Card",
            content: contentJson,
            version: 3,
            isSuppressed: suppressed,
            suppressedReason: suppressed ? "test" : null,
            suppressedAt: suppressed ? DateTime.UtcNow : null,
            suppressedBy: suppressed ? Guid.NewGuid() : null,
            errorReportsCount: 0,
            feedbackScore: null,
            publishedAt: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            publishedBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow);

    private static string ContentWithMixedSections(Guid gameId, Guid pdfId)
    {
        // Sections deliberately OUT of enum order and ordinals shuffled within a section,
        // to prove the handler groups + orders by MechanicSection enum then by ordinal.
        var content = new MechanicCardContent
        {
            SnapshotAt = DateTime.UtcNow,
            SourceAnalysisId = Guid.NewGuid(),
            SourcePromptVersion = "mechanic-extractor-v1",
            Metadata = new MechanicCardMetadata
            {
                SharedGameId = gameId,
                SharedGameName = "Catan",
                Publisher = "Kosmos",
                Language = "en"
            },
            Claims = new[]
            {
                new MechanicCardClaimSnapshot { Id = Guid.NewGuid(), Section = "Faq", Ordinal = 0, Claim = "faq claim",
                    Citations = new[] { new MechanicCardCitationSnapshot { PdfId = pdfId, PdfPage = 12, Quote = "faq quote" } } },
                new MechanicCardClaimSnapshot { Id = Guid.NewGuid(), Section = "Summary", Ordinal = 1, Claim = "summary second",
                    Citations = Array.Empty<MechanicCardCitationSnapshot>() },
                new MechanicCardClaimSnapshot { Id = Guid.NewGuid(), Section = "Summary", Ordinal = 0, Claim = "summary first",
                    Citations = new[] { new MechanicCardCitationSnapshot { PdfId = pdfId, PdfPage = 1, Quote = "summary quote" } } },
                new MechanicCardClaimSnapshot { Id = Guid.NewGuid(), Section = "Mechanics", Ordinal = 0, Claim = "mechanics claim",
                    Citations = Array.Empty<MechanicCardCitationSnapshot>() },
            }
        };
        return content.ToJson();
    }

    [Fact]
    public async Task Handle_GroupsClaimsBySection_InEnumOrder_AndOrdinalWithinSection()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var card = BuildCard(gameId, ContentWithMixedSections(gameId, pdfId));

        var repo = new Mock<IMechanicCardRepository>();
        repo.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>())).ReturnsAsync(card);

        var handler = new GetPublishedMechanicCardByGameQueryHandler(repo.Object);
        var dto = await handler.Handle(new GetPublishedMechanicCardByGameQuery(gameId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.GameName.Should().Be("Catan");
        dto.Publisher.Should().Be("Kosmos");
        dto.Language.Should().Be("en");
        dto.Version.Should().Be(3);

        // Grouped into 3 sections, ordered by MechanicSection enum: Summary(0), Mechanics(1), Faq(5).
        dto.Sections.Select(s => s.Section).Should().Equal("Summary", "Mechanics", "Faq");
        // Summary claims ordered by ordinal (0 before 1), NOT input order.
        dto.Sections[0].Claims.Select(c => c.Claim).Should().Equal("summary first", "summary second");
        // Citations projected through.
        dto.Sections[0].Claims[0].Citations.Should().ContainSingle();
        dto.Sections[0].Claims[0].Citations[0].PdfPage.Should().Be(1);
        dto.Sections[2].Claims[0].Citations[0].PdfId.Should().Be(pdfId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoActiveCard()
    {
        var gameId = Guid.NewGuid();
        var repo = new Mock<IMechanicCardRepository>();
        // GetActiveByGameAsync honors the !IsSuppressed filter → suppressed/absent both surface as null.
        repo.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>())).ReturnsAsync((MechanicCard?)null);

        var handler = new GetPublishedMechanicCardByGameQueryHandler(repo.Object);
        var dto = await handler.Handle(new GetPublishedMechanicCardByGameQuery(gameId), CancellationToken.None);

        dto.Should().BeNull();
    }
}
