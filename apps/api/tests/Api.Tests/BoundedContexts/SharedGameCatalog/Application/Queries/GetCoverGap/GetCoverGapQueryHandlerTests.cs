using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;

/// <summary>
/// #3590 — la vista cover-gap elenca i giochi SENZA alcuna cover, con la causa per cui la pipeline
/// cover-da-PDF non li copre. Il collo di bottiglia non era risolverli (il picker manuale esiste da
/// #3545) ma TROVARLI: non esisteva alcuna vista dei giochi senza cover.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetCoverGapQueryHandlerTests
{
    private static GetCoverGapQueryHandler Sut(MeepleAiDbContext db) =>
        new(db, NullLogger<GetCoverGapQueryHandler>.Instance);

    private static SharedGameEntity NewGame(string title, Action<SharedGameEntity>? tweak = null)
    {
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "desc",
            Status = 2, // Published
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
        };
        tweak?.Invoke(game);
        return game;
    }

    private static PdfDocumentEntity NewPdf(Action<PdfDocumentEntity>? tweak = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        };
        tweak?.Invoke(pdf);
        return pdf;
    }

    private static SharedGameDocumentEntity Link(Guid gameId, Guid pdfId) => new()
    {
        Id = Guid.NewGuid(),
        SharedGameId = gameId,
        PdfDocumentId = pdfId,
        DocumentType = 0,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = Guid.NewGuid(),
    };

    [Fact]
    public async Task Handle_GameWithNoCoverAndNoPdf_ClassifiedAsNoSource()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var game = NewGame("Senza Sorgente");
        db.SharedGames.Add(game);
        await db.SaveChangesAsync();

        var result = await Sut(db).Handle(new GetCoverGapQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].GameId.Should().Be(game.Id);
        result.Items[0].Cause.Should().Be(CoverGapCauses.NoSource);
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_GameWithSkippedCover_ClassifiedAsHeuristicRejected()
    {
        // Gotcha: 'Skipped' è scritto direttamente sul campo da BackfillPdfCoversJob,
        // non via PdfDocument.MarkCoverSkipped() (metodo morto).
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var game = NewGame("Euristica Rifiuta");
        var pdf = NewPdf(p =>
        {
            p.ProcessingState = "Ready";
            p.CoverGenerationStatus = "Skipped";
        });
        db.SharedGames.Add(game);
        db.PdfDocuments.Add(pdf);
        db.SharedGameDocuments.Add(Link(game.Id, pdf.Id));
        await db.SaveChangesAsync();

        var result = await Sut(db).Handle(new GetCoverGapQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Cause.Should().Be(CoverGapCauses.HeuristicRejected);
        result.Items[0].PdfFileName.Should().Be("rulebook.pdf");
    }

    [Fact]
    public async Task Handle_GameWithPayloadTooLargePdf_ClassifiedAsPdfTooLarge()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var game = NewGame("PDF Enorme");
        var pdf = NewPdf(p =>
        {
            p.ProcessingState = "Failed";
            p.ErrorCategory = "PayloadTooLarge";
            p.FileSizeBytes = 65_000_000;
        });
        db.SharedGames.Add(game);
        db.PdfDocuments.Add(pdf);
        db.SharedGameDocuments.Add(Link(game.Id, pdf.Id));
        await db.SaveChangesAsync();

        var result = await Sut(db).Handle(new GetCoverGapQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Cause.Should().Be(CoverGapCauses.PdfTooLarge);
        result.Items[0].PdfSizeBytes.Should().Be(65_000_000);
    }

    [Fact]
    public async Task Handle_LegacyLargeFailureWithoutCategory_ClassifiedAsPdfTooLarge()
    {
        // I fallimenti precedenti al fix #3589 hanno ErrorCategory "Service" e un messaggio
        // fuorviante ("Failed to connect"): la sola categoria non basta, serve la dimensione.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var game = NewGame("Legacy 413");
        var pdf = NewPdf(p =>
        {
            p.ProcessingState = "Failed";
            p.ErrorCategory = "Service";
            p.FileSizeBytes = 62_000_000;
        });
        db.SharedGames.Add(game);
        db.PdfDocuments.Add(pdf);
        db.SharedGameDocuments.Add(Link(game.Id, pdf.Id));
        await db.SaveChangesAsync();

        var result = await Sut(db).Handle(new GetCoverGapQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Cause.Should().Be(CoverGapCauses.PdfTooLarge);
    }

    [Fact]
    public async Task Handle_GameWithAnyCoverKey_IsExcluded()
    {
        // Contratto centrale: la vista elenca SOLO chi non ha NESSUNA delle quattro chiavi.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            NewGame("Con PDF", g => g.PdfCoverR2Key = "covers/pdf/x"),
            NewGame("Con BGG", g => g.BggCoverR2Key = "bgg-covers/1/cover"),
            NewGame("Con Wikidata", g => g.WikidataCoverR2Key = "covers/y"),
            NewGame("Con Manuale", g => g.ManualCoverR2Key = "covers/manual/z/cover"));
        await db.SaveChangesAsync();

        var result = await Sut(db).Handle(new GetCoverGapQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty("avere una qualsiasi cover esclude dal gap");
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FiltersByCause_WhenCauseProvided()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var senzaSorgente = NewGame("Senza Sorgente");
        var euristica = NewGame("Euristica");
        var pdf = NewPdf(p =>
        {
            p.ProcessingState = "Ready";
            p.CoverGenerationStatus = "Skipped";
        });
        db.SharedGames.AddRange(senzaSorgente, euristica);
        db.PdfDocuments.Add(pdf);
        db.SharedGameDocuments.Add(Link(euristica.Id, pdf.Id));
        await db.SaveChangesAsync();

        var result = await Sut(db)
            .Handle(new GetCoverGapQuery(Cause: CoverGapCauses.NoSource), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].GameId.Should().Be(senzaSorgente.Id);
        result.Total.Should().Be(1);
    }
}
