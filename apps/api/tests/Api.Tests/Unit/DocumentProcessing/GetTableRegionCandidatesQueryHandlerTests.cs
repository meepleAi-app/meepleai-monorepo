using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// #3435 SP2 (router DC-B): the scoped selector returns Ready, in-corpus, non-demo PDFs whose
/// image-region count reaches the (config- or query-overridable) threshold, most-dense first.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class GetTableRegionCandidatesQueryHandlerTests
{
    private static PdfDocumentEntity Pdf(
        Guid id, string state = "Ready", string? indexerVersion = "v1", string? filePath = null)
        => new()
        {
            Id = id,
            FileName = "t.pdf",
            FilePath = filePath ?? $"/tmp/{id:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };

    private static PdfImageRegionEntity Region(Guid pdfId, int page)
        => new() { PdfDocumentId = pdfId, PageNumber = page, X = 0, Y = 0, Width = 0.5, Height = 0.5, ElementType = "Image" };

    private static IConfiguration Config(int? min = null)
    {
        var dict = new Dictionary<string, string?>();
        if (min.HasValue)
        {
            dict[GetTableRegionCandidatesQueryHandler.MinImageRegionsConfigKey] =
                min.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static GetTableRegionCandidatesQueryHandler Handler(MeepleAiDbContext db, int? configMin = null)
        => new(db, Config(configMin));

    [Fact]
    public async Task Handle_ReturnsCandidate_WithRegionCountAndDistinctPages()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(Pdf(pdfId));
        db.PdfImageRegions.AddRange(Region(pdfId, 1), Region(pdfId, 1), Region(pdfId, 2)); // 3 regions, 2 pages
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PdfDocumentId.Should().Be(pdfId);
        result[0].ImageRegionCount.Should().Be(3);
        result[0].DistinctPageCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_PdfWithNoRegions_NotReturned()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        db.PdfDocuments.Add(Pdf(Guid.NewGuid())); // Ready, in-corpus, but zero regions
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonReadyPdf_Excluded_EvenWithRegions()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(Pdf(pdfId, state: "Processing"));
        db.PdfImageRegions.Add(Region(pdfId, 1));
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NullIndexerVersion_Excluded_EvenWithRegions()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(Pdf(pdfId, indexerVersion: null));
        db.PdfImageRegions.Add(Region(pdfId, 1));
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DemoMockPdf_Excluded_EvenWithRegions()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(Pdf(pdfId, filePath: PdfDocumentEntity.DemoMockFilePathPrefix + "badsworm/rulebook.pdf"));
        db.PdfImageRegions.Add(Region(pdfId, 1));
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RespectsMinImageRegions_FromQuery()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var two = Guid.NewGuid();
        var three = Guid.NewGuid();
        db.PdfDocuments.AddRange(Pdf(two), Pdf(three));
        db.PdfImageRegions.AddRange(Region(two, 1), Region(two, 2));
        db.PdfImageRegions.AddRange(Region(three, 1), Region(three, 2), Region(three, 3));
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(
            new GetTableRegionCandidatesQuery(MinImageRegions: 3), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PdfDocumentId.Should().Be(three); // only the 3-region PDF clears the threshold
    }

    [Fact]
    public async Task Handle_QueryThreshold_OverridesConfig()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(Pdf(pdfId));
        db.PdfImageRegions.Add(Region(pdfId, 1)); // single region
        await db.SaveChangesAsync();

        // config demands 5, but the explicit query override of 1 wins
        var result = await Handler(db, configMin: 5).Handle(
            new GetTableRegionCandidatesQuery(MinImageRegions: 1), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ConfigThreshold_AppliedWhenQueryNull()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(Pdf(pdfId));
        db.PdfImageRegions.Add(Region(pdfId, 1)); // single region, below config threshold of 2
        await db.SaveChangesAsync();

        var result = await Handler(db, configMin: 2).Handle(
            new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrdersByRegionCount_Descending()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        var small = Guid.NewGuid();
        var big = Guid.NewGuid();
        db.PdfDocuments.AddRange(Pdf(small), Pdf(big));
        db.PdfImageRegions.Add(Region(small, 1));
        db.PdfImageRegions.AddRange(Region(big, 1), Region(big, 2), Region(big, 3));
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetTableRegionCandidatesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].PdfDocumentId.Should().Be(big); // most-dense first
        result[1].PdfDocumentId.Should().Be(small);
    }

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"trc_{Guid.NewGuid():N}");
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.NewGuid();
            db.PdfDocuments.Add(Pdf(id));
            db.PdfImageRegions.Add(Region(id, 1));
        }
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(
            new GetTableRegionCandidatesQuery(Limit: 2), CancellationToken.None);

        result.Should().HaveCount(2);
    }
}
