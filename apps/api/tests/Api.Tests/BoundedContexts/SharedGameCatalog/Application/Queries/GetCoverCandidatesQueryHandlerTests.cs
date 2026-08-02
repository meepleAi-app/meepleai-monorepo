using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Epic #3470 Slice 1c-3 — the read-side that feeds the admin cover picker: the
/// MATERIALIZED source candidates (each with a preview URL) plus which source is
/// currently pinned per UI context. Distinct from the winner-only DTO the public
/// catalog surfaces expose.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetCoverCandidatesQueryHandlerTests
{
    private readonly Mock<IBlobStorageService> _blob = new();

    private GetCoverCandidatesQueryHandler CreateHandler(MeepleAiDbContext ctx) => new(ctx, _blob.Object);

    private static SharedGameEntity SeedGame(MeepleAiDbContext ctx, Action<SharedGameEntity> configure)
    {
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Catan",
            Description = "desc",
            Status = 1,
        };
        configure(game);
        ctx.SharedGames.Add(game);
        ctx.SaveChanges();
        return game;
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsNull()
    {
        using var ctx = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetCoverCandidatesQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExposesMaterializedSourcesOnly_WithPreviewsAndLicense()
    {
        using var ctx = TestDbContextFactory.CreateInMemoryDbContext();
        var game = SeedGame(ctx, g =>
        {
            g.PdfCoverR2Key = "covers/pdf/x/cover";
            g.WikidataCoverR2Key = "covers/y/cover";
            g.WikidataCoverLicense = "CC BY-SA 4.0";
            g.WikidataCoverAttribution = "Artist";
            g.WikidataCoverSourceUrl = "https://www.wikidata.org/wiki/Q1";
            // BGG + Manual keys deliberately null → not materialized → not candidates.
        });
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/pdf/x/cover-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/y/cover.webp", null))
             .ReturnsAsync("https://r2/wiki.webp");
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetCoverCandidatesQuery(game.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.GameId.Should().Be(game.Id);
        result.Candidates.Select(c => c.Source)
            .Should().BeEquivalentTo(new[] { CoverAssignmentSource.Pdf, CoverAssignmentSource.Wikidata });

        var pdf = result.Candidates.Single(c => c.Source == CoverAssignmentSource.Pdf);
        pdf.PreviewUrl.Should().Be("https://r2/pdf.webp");
        pdf.License.Should().BeNull();

        var wiki = result.Candidates.Single(c => c.Source == CoverAssignmentSource.Wikidata);
        wiki.PreviewUrl.Should().Be("https://r2/wiki.webp");
        wiki.License.Should().Be("CC BY-SA 4.0");
        wiki.Attribution.Should().Be("Artist");
        wiki.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q1");
    }

    [Fact]
    public async Task Handle_UnresolvableKey_OmitsThatCandidate()
    {
        using var ctx = TestDbContextFactory.CreateInMemoryDbContext();
        var game = SeedGame(ctx, g =>
        {
            g.PdfCoverR2Key = "covers/pdf/x/cover";
            g.WikidataCoverR2Key = "covers/y/cover";
        });
        // PDF preview resolves; Wikidata blob returns null (unreachable) → omitted.
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/pdf/x/cover-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/y/cover.webp", null))
             .ReturnsAsync((string?)null);
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetCoverCandidatesQuery(game.Id), CancellationToken.None);

        result!.Candidates.Should().ContainSingle()
            .Which.Source.Should().Be(CoverAssignmentSource.Pdf);
    }

    [Fact]
    public async Task Handle_ReportsPerContextAssignments()
    {
        using var ctx = TestDbContextFactory.CreateInMemoryDbContext();
        var game = SeedGame(ctx, g =>
        {
            g.WikidataCoverR2Key = "covers/y/cover";
            g.CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                new() { Id = Guid.NewGuid(), Context = CoverContext.Card, Source = CoverAssignmentSource.Wikidata, CreatedBy = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), Context = CoverContext.Hero, Source = CoverAssignmentSource.Pdf, CreatedBy = Guid.NewGuid() },
            };
        });
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), null))
             .ReturnsAsync("https://r2/x.webp");
        var handler = CreateHandler(ctx);

        var result = await handler.Handle(new GetCoverCandidatesQuery(game.Id), CancellationToken.None);

        result!.Assignments.Card.Should().Be(CoverAssignmentSource.Wikidata);
        result.Assignments.Hero.Should().Be(CoverAssignmentSource.Pdf);
        result.Assignments.Social.Should().BeNull("no assignment pins the Social context");
    }
}
