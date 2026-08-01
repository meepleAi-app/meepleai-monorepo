using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class GetPdfImageRegionsQueryHandlerTests
{
    private static void SeedPdf(MeepleAiDbContext db, Guid pdfId, Guid ownerId, Guid? sharedGameId = null)
    {
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = ownerId,
            ProcessingState = "Ready",
            SharedGameId = sharedGameId,
        });
    }

    [Fact]
    public async Task Handle_ReturnsRegionsForOwner_OrderedByPage()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId);
        db.PdfImageRegions.AddRange(
            new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 5, X = 0.1, Y = 0.2, Width = 0.3, Height = 0.1, ElementType = "Image" },
            new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 4, X = 0.1, Y = 0.5, Width = 0.8, Height = 0.3, ElementType = "FigureCaption" },
            new PdfImageRegionEntity { PdfDocumentId = Guid.NewGuid(), PageNumber = 1, X = 0, Y = 0, Width = 1, Height = 1, ElementType = "Image" });
        await db.SaveChangesAsync();

        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(new GetPdfImageRegionsQuery(pdfId, ownerId, IsAdmin: false), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.Page).Should().ContainInOrder(4, 5); // ordered by page
        result[0].ElementType.Should().Be("FigureCaption");
    }

    [Fact]
    public async Task Handle_UnknownPdf_ReturnsEmpty()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(
            new GetPdfImageRegionsQuery(Guid.NewGuid(), Guid.NewGuid(), IsAdmin: false), CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonOwnerPrivatePdf_ReturnsEmpty_NoLeak()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: Guid.NewGuid()); // private, owned by another user
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: false), CancellationToken.None);

        result.Should().BeEmpty(); // non-owner of a private PDF gets an empty overlay, no existence leak
    }

    [Fact]
    public async Task Handle_SharedGamePdf_VisibleToAnyUser()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: Guid.NewGuid(), sharedGameId: Guid.NewGuid()); // public shared-game PDF
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: false), CancellationToken.None);

        result.Should().HaveCount(1); // shared-game PDFs are public (citation viewer)
    }

    [Fact]
    public async Task Handle_AdminBypassesOwnership()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: Guid.NewGuid()); // private, owned by another user
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: true), CancellationToken.None);

        result.Should().HaveCount(1); // admin bypasses ownership
    }
}
