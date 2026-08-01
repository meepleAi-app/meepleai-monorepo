using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class SeedPdfImageRegionsCommandHandlerTests
{
    private const string HiResJson = """
    {"elements":[
      {"text":"","page_number":4,"category":"Image","bbox":{"x":0.1,"y":0.5,"width":0.8,"height":0.3}},
      {"text":"t","page_number":1,"category":"Title","bbox":{"x":0.0,"y":0.0,"width":0.1,"height":0.1}}
    ]}
    """;

    private static void SeedPdf(MeepleAiDbContext db, Guid pdfId)
    {
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
        });
    }

    [Fact]
    public async Task Handle_InsertsParsedRegions_AndIsIdempotent()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"seedimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId);
        await db.SaveChangesAsync();

        var handler = new SeedPdfImageRegionsCommandHandler(db, NullLogger<SeedPdfImageRegionsCommandHandler>.Instance);

        var count1 = await handler.Handle(new SeedPdfImageRegionsCommand(pdfId, HiResJson), CancellationToken.None);
        count1.Should().Be(1); // only the Image; Title dropped

        // idempotent: seeding again replaces, does not duplicate
        var count2 = await handler.Handle(new SeedPdfImageRegionsCommand(pdfId, HiResJson), CancellationToken.None);
        count2.Should().Be(1);
        (await db.PdfImageRegions.CountAsync(r => r.PdfDocumentId == pdfId)).Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownPdf_ThrowsNotFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"seedimg_{Guid.NewGuid():N}");
        var handler = new SeedPdfImageRegionsCommandHandler(db, NullLogger<SeedPdfImageRegionsCommandHandler>.Instance);

        var act = () => handler.Handle(new SeedPdfImageRegionsCommand(Guid.NewGuid(), HiResJson), CancellationToken.None);

        // Guards the FK: a non-existent PDF is a clean 404, not a SaveChanges FK 500.
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
