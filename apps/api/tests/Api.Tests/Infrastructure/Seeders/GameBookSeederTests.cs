using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

/// <summary>
/// Unit tests for <see cref="GameBookSeeder.ResolveGameBookPdfsAsync"/> (#3085): the PDF-selection
/// logic must exclude Badsworm demo mock placeholders (seed/ prefix, no real blob/chunks) so the
/// GameBook aggregate is wired to the real, content-backed rulebook — never a content-less mock.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GameBookSeederTests
{
    private static Guid SeedPdf(MeepleAiDbContext db, Guid gameId, string fileName, string filePath)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = fileName,
            FilePath = filePath,
            ContentHash = "hash",
            UploadedByUserId = Guid.NewGuid(),
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = "Ready",
        });
        return pdfId;
    }

    [Fact]
    [Trait("Issue", "3085")]
    public async Task ResolveGameBookPdfs_ExcludesDemoMock_PicksRealRulebook()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = Guid.NewGuid();
        // Both filenames match "rule"; without the seed/ exclusion the unordered FirstOrDefault
        // could pick the content-less mock over the real rulebook.
        var mockRules = SeedPdf(db, gameId, "rulebook-nanolith.pdf",
            $"{PdfDocumentEntity.DemoMockFilePathPrefix}badsworm/nanolith/rulebook.pdf");
        var realRules = SeedPdf(db, gameId, "nanolith_rules.pdf", $"pdfs/{Guid.NewGuid():N}/rules.pdf");
        var realPressStart = SeedPdf(db, gameId, "nanolith_press-start.pdf", $"pdfs/{Guid.NewGuid():N}/ps.pdf");
        await db.SaveChangesAsync();

        var (pressStartId, rulesId) = await GameBookSeeder.ResolveGameBookPdfsAsync(db, gameId, CancellationToken.None);

        rulesId.Should().Be(realRules, because: "the demo mock (seed/ prefix) must be excluded (#3085)");
        rulesId.Should().NotBe(mockRules);
        pressStartId.Should().Be(realPressStart);
    }

    [Fact]
    [Trait("Issue", "3085")]
    public async Task ResolveGameBookPdfs_OnlyMockRulebook_ReturnsNullRules()
    {
        // If the ONLY "rule" PDF is a demo mock, resolution returns null → the caller skips
        // gracefully instead of wiring the GameBook to a content-less placeholder.
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = Guid.NewGuid();
        SeedPdf(db, gameId, "rulebook-nanolith.pdf",
            $"{PdfDocumentEntity.DemoMockFilePathPrefix}badsworm/nanolith/rulebook.pdf");
        var realPressStart = SeedPdf(db, gameId, "nanolith_press-start.pdf", $"pdfs/{Guid.NewGuid():N}/ps.pdf");
        await db.SaveChangesAsync();

        var (pressStartId, rulesId) = await GameBookSeeder.ResolveGameBookPdfsAsync(db, gameId, CancellationToken.None);

        rulesId.Should().BeNull("the only 'rule' PDF is a demo mock, which is excluded (#3085)");
        pressStartId.Should().Be(realPressStart);
    }
}
