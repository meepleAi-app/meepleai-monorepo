using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Moq;
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

    // #3435 §5quinquies: the handler now resolves the PDF copyright tier via IMediator and gates the
    // region overlay to Full-tier. The KB resolution is mocked here (Full by default) so the existing
    // owner/shared/admin SCOPING tests still exercise the return-regions path.
    private static GetPdfImageRegionsQueryHandler Handler(MeepleAiDbContext db, CopyrightTier tier = CopyrightTier.Full)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ResolvePdfCopyrightTierQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier);
        return new GetPdfImageRegionsQueryHandler(db, mediator.Object);
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

        var result = await Handler(db).Handle(new GetPdfImageRegionsQuery(pdfId, ownerId, IsAdmin: false), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.Page).Should().ContainInOrder(4, 5); // ordered by page
        result[0].ElementType.Should().Be("FigureCaption");
    }

    [Fact]
    public async Task Handle_UnknownPdf_ReturnsEmpty()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var result = await Handler(db).Handle(
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

        var result = await Handler(db).Handle(
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

        var result = await Handler(db).Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: false), CancellationToken.None);

        result.Should().HaveCount(1); // shared-game PDFs are public (citation viewer) + Full tier here
    }

    [Fact]
    public async Task Handle_AdminBypassesOwnership()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: Guid.NewGuid()); // private, owned by another user
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: true), CancellationToken.None);

        result.Should().HaveCount(1); // admin bypasses OWNERSHIP scoping (tier still Full here)
    }

    // ── #3435 §5quinquies: copyright-tier gate on the region overlay ──

    [Fact]
    [Trait("Issue", "3435")]
    public async Task Handle_ProtectedTier_ReturnsEmpty_NoRegionLeak()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: Guid.NewGuid(), sharedGameId: Guid.NewGuid()); // scoping passes (public)
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var result = await Handler(db, CopyrightTier.Protected).Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: false), CancellationToken.None);

        result.Should().BeEmpty(); // Protected region layout must not leak via the viewer overlay
    }

    [Fact]
    [Trait("Issue", "3435")]
    public async Task Handle_FullTier_ReturnsRegions()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId);
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var result = await Handler(db, CopyrightTier.Full).Handle(
            new GetPdfImageRegionsQuery(pdfId, ownerId, IsAdmin: false), CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Issue", "3435")]
    public async Task Handle_ProtectedTier_NotBypassedByAdmin()
    {
        // The copyright tier is NOT admin-bypassed (copyright, not access-control) — consistent with
        // the grounded-citation gate.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: Guid.NewGuid(), sharedGameId: Guid.NewGuid());
        db.PdfImageRegions.Add(new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 1, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" });
        await db.SaveChangesAsync();

        var result = await Handler(db, CopyrightTier.Protected).Handle(
            new GetPdfImageRegionsQuery(pdfId, Guid.NewGuid(), IsAdmin: true), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "3435")]
    public async Task Handle_ResolvesTier_WithThisPdfIdAndUserId()
    {
        // #3517-class guard: the tier must be resolved for THIS pdf + user, not a swapped id — a wrong
        // argument would gate the wrong document's copyright and silently leak/hide regions.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedPdf(db, pdfId, ownerId: userId); // owner -> scoping passes, tier gate is reached
        await db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ResolvePdfCopyrightTierQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CopyrightTier.Full);
        var handler = new GetPdfImageRegionsQueryHandler(db, mediator.Object);

        await handler.Handle(new GetPdfImageRegionsQuery(pdfId, userId, IsAdmin: false), CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<ResolvePdfCopyrightTierQuery>(q => q.DocumentId == pdfId.ToString() && q.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
