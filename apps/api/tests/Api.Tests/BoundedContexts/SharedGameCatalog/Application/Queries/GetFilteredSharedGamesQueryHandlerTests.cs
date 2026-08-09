using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Epic #3470 Slice 2b — the filtered/admin catalog list is a Card surface, so its
/// cover resolution must honor an admin per-context (Card) override.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetFilteredSharedGamesQueryHandlerTests
{
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<ILogger<GetFilteredSharedGamesQueryHandler>> _logger = new();
    private readonly Mock<IGameTitleResolver> _titleResolver = CreateIdentityResolverMock();

    private static Mock<IGameTitleResolver> CreateIdentityResolverMock()
    {
        var mock = new Mock<IGameTitleResolver>();
        mock.Setup(r => r.EnrichAsync(It.IsAny<IReadOnlyList<SharedGameDto>>(), It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<SharedGameDto>, CancellationToken>((games, _) => Task.FromResult(games));
        return mock;
    }

    [Fact]
    public async Task Handle_ResolvesCardContextCover_HonoringAdminOverride()
    {
        // Slice 2b AC-1: the admin/filtered catalog list renders Card. An admin Card
        // override pinning Wikidata must win over the implicit precedence (PDF).
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Catan",
            Description = "desc",
            Status = (int)GameStatus.Published,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            PdfCoverR2Key = "pdf-key",        // would win the implicit precedence
            WikidataCoverR2Key = "wiki-key",  // pinned by the Card override
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Context = CoverContext.Card,
                    Source = CoverAssignmentSource.Wikidata,
                    CreatedBy = Guid.NewGuid(),
                },
            },
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/wiki-card.webp");
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/pdf.webp");

        var handler = new GetFilteredSharedGamesQueryHandler(db, _blob.Object, _logger.Object, _titleResolver.Object);

        var result = await handler.Handle(new GetFilteredSharedGamesQuery(), TestContext.Current.CancellationToken);

        result.Items.Should().ContainSingle();
        result.Items[0].CoverUrl.Should().Be(
            "https://r2/wiki-card.webp",
            "the Card admin override pins Wikidata; the PDF implicit precedence must NOT win");
    }
}
