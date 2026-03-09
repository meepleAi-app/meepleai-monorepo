using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for VersionLabel and Reclassify functionality (Issue #5447).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class VersionLabelAndReclassifyTests
{
    #region SetVersionLabel

    [Fact]
    public void SetVersionLabel_WithValidLabel_SetsValue()
    {
        var document = CreateDocument();

        document.SetVersionLabel("2nd Edition");

        Assert.Equal("2nd Edition", document.VersionLabel);
    }

    [Fact]
    public void SetVersionLabel_WithNull_ClearsLabel()
    {
        var document = CreateDocument();
        document.SetVersionLabel("2nd Edition");

        document.SetVersionLabel(null);

        Assert.Null(document.VersionLabel);
    }

    [Fact]
    public void SetVersionLabel_TrimsWhitespace()
    {
        var document = CreateDocument();

        document.SetVersionLabel("  v1.3 Errata  ");

        Assert.Equal("v1.3 Errata", document.VersionLabel);
    }

    [Fact]
    public void SetVersionLabel_ExceedsMaxLength_ThrowsArgumentException()
    {
        var document = CreateDocument();
        var longLabel = new string('x', 101);

        Assert.Throws<ArgumentException>(() => document.SetVersionLabel(longLabel));
    }

    [Fact]
    public void SetVersionLabel_ExactMaxLength_Succeeds()
    {
        var document = CreateDocument();
        var label = new string('x', 100);

        document.SetVersionLabel(label);

        Assert.Equal(label, document.VersionLabel);
    }

    [Fact]
    public void VersionLabel_DefaultIsNull()
    {
        var document = CreateDocument();

        Assert.Null(document.VersionLabel);
    }

    #endregion

    #region Reclassify

    [Fact]
    public void Reclassify_SetsAllFields()
    {
        var document = CreateDocument();
        var baseDocId = Guid.NewGuid();

        document.Reclassify(DocumentCategory.Expansion, baseDocId, "Expansion v2");

        Assert.Equal(DocumentCategory.Expansion, document.DocumentCategory);
        Assert.Equal(baseDocId, document.BaseDocumentId);
        Assert.Equal("Expansion v2", document.VersionLabel);
    }

    [Fact]
    public void Reclassify_WithNullBaseDocument_UnlinksBase()
    {
        var document = CreateDocument();
        document.LinkToBaseDocument(Guid.NewGuid());

        document.Reclassify(DocumentCategory.Rulebook, null, "v1.0");

        Assert.Null(document.BaseDocumentId);
        Assert.Equal("v1.0", document.VersionLabel);
    }

    [Fact]
    public void Reclassify_WithNullVersionLabel_ClearsLabel()
    {
        var document = CreateDocument();
        document.SetVersionLabel("old label");

        document.Reclassify(DocumentCategory.QuickStart, null, null);

        Assert.Equal(DocumentCategory.QuickStart, document.DocumentCategory);
        Assert.Null(document.VersionLabel);
    }

    [Fact]
    public void Reclassify_WithSelfReference_ThrowsArgumentException()
    {
        var docId = Guid.NewGuid();
        var document = new PdfDocument(
            docId,
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid());

        Assert.Throws<ArgumentException>(() =>
            document.Reclassify(DocumentCategory.Expansion, docId, "v1"));
    }

    [Fact]
    public void Reclassify_MultipleTimesUpdatesAllFields()
    {
        var document = CreateDocument();
        var baseDocId1 = Guid.NewGuid();
        var baseDocId2 = Guid.NewGuid();

        document.Reclassify(DocumentCategory.Expansion, baseDocId1, "v1");
        document.Reclassify(DocumentCategory.Errata, baseDocId2, "v2 Errata");

        Assert.Equal(DocumentCategory.Errata, document.DocumentCategory);
        Assert.Equal(baseDocId2, document.BaseDocumentId);
        Assert.Equal("v2 Errata", document.VersionLabel);
    }

    #endregion

    #region Reconstitute

    [Fact]
    public void Reconstitute_WithVersionLabel_PreservesValue()
    {
        var document = PdfDocument.Reconstitute(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processingStatus: "pending",
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English,
            versionLabel: "3rd Edition");

        Assert.Equal("3rd Edition", document.VersionLabel);
    }

    [Fact]
    public void Reconstitute_WithoutVersionLabel_DefaultsToNull()
    {
        var document = PdfDocument.Reconstitute(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processingStatus: "pending",
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English);

        Assert.Null(document.VersionLabel);
    }

    #endregion

    private static PdfDocument CreateDocument()
    {
        return new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid());
    }
}
