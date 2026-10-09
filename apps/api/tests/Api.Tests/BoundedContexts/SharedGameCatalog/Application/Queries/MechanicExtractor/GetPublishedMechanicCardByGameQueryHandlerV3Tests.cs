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
public sealed class GetPublishedMechanicCardByGameQueryHandlerV3Tests
{
    [Fact]
    public async Task Handle_V3Snapshot_ProjectsKindPriorityOverridesAndTrigger()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var general = Guid.NewGuid();
        var exception = Guid.NewGuid();
        var content = new MechanicCardContent
        {
            SnapshotAt = DateTime.UtcNow,
            SourceAnalysisId = Guid.NewGuid(),
            SourcePromptVersion = "v1.2.0",
            Metadata = new MechanicCardMetadata { SharedGameId = gameId, SharedGameName = "G", Language = "it" },
            Claims = new[]
            {
                new MechanicCardClaimSnapshot { Id = general, Section = "Phases", Ordinal = 0, Claim = "regola" },
                new MechanicCardClaimSnapshot
                {
                    Id = exception, Section = "Phases", Ordinal = 1, Claim = "eccezione",
                    Kind = "Exception", Priority = "Card", Overrides = new[] { general },
                    Trigger = new MechanicCardTriggerSnapshot { Phase = "azione" },
                    Citations = new[] { new MechanicCardCitationSnapshot { PdfId = pdfId, PdfPage = 3, Quote = "q" } }
                }
            }
        };
        var card = MechanicCard.Reconstitute(
            Guid.NewGuid(), gameId, Guid.NewGuid(), MechanicCardOrigin.AiReviewed, "T", content.ToJson(), 1,
            false, null, null, null, 0, null, DateTime.UtcNow, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow);
        var repo = new Mock<IMechanicCardRepository>();
        repo.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        repo.Setup(r => r.GetApaContextAsync(gameId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MechanicCardApaContext(null, null));
        var handler = new GetPublishedMechanicCardByGameQueryHandler(repo.Object);

        var dto = await handler.Handle(new GetPublishedMechanicCardByGameQuery(gameId), CancellationToken.None);

        var claim = dto!.Sections.Single().Claims.Single(c => c.Id == exception);
        claim.Kind.Should().Be(MechanicClaimKind.Exception);
        claim.Priority.Should().Be(MechanicRulePriority.Card);
        claim.Overrides.Should().Equal(general);
        claim.Trigger!.Phase.Should().Be("azione");
        var plain = dto.Sections.Single().Claims.Single(c => c.Id == general);
        plain.Kind.Should().Be(MechanicClaimKind.Rule);
        plain.Trigger.Should().BeNull();
    }
}
