using Api.BoundedContexts.DocumentProcessing.Application.Queries;
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
    [Fact]
    public async Task Handle_ReturnsRegionsForPdf_OrderedByPage()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfImageRegions.AddRange(
            new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 5, X = 0.1, Y = 0.2, Width = 0.3, Height = 0.1, ElementType = "Image" },
            new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 4, X = 0.1, Y = 0.5, Width = 0.8, Height = 0.3, ElementType = "FigureCaption" },
            new PdfImageRegionEntity { PdfDocumentId = Guid.NewGuid(), PageNumber = 1, X = 0, Y = 0, Width = 1, Height = 1, ElementType = "Image" });
        await db.SaveChangesAsync();

        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(new GetPdfImageRegionsQuery(pdfId), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.Page).Should().ContainInOrder(4, 5); // ordered by page
        result[0].ElementType.Should().Be("FigureCaption");
    }

    [Fact]
    public async Task Handle_UnknownPdf_ReturnsEmpty()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var handler = new GetPdfImageRegionsQueryHandler(db);
        var result = await handler.Handle(new GetPdfImageRegionsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Should().BeEmpty();
    }
}
