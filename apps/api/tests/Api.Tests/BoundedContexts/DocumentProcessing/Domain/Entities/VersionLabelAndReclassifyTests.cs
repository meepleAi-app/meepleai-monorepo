using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        document.VersionLabel.Should().Be("2nd Edition");
    }

    [Fact]
    public void SetVersionLabel_WithNull_ClearsLabel()
    {
        var document = CreateDocument();
        document.SetVersionLabel("2nd Edition");

        document.SetVersionLabel(null);

        document.VersionLabel.Should().BeNull();
    }

    [Fact]
    public void SetVersionLabel_TrimsWhitespace()
    {
        var document = CreateDocument();

        document.SetVersionLabel("  v1.3 Errata  ");

        document.VersionLabel.Should().Be("v1.3 Errata");
    }

    [Fact]
    public void SetVersionLabel_ExceedsMaxLength_ThrowsArgumentException()
    {
        var document = CreateDocument();
        var longLabel = new string('x', 101);

        ((Action)(() => document.SetVersionLabel(longLabel))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetVersionLabel_ExactMaxLength_Succeeds()
    {
        var document = CreateDocument();
        var label = new string('x', 100);

        document.SetVersionLabel(label);

        document.VersionLabel.Should().Be(label);
    }

    [Fact]
    public void VersionLabel_DefaultIsNull()
    {
        var document = CreateDocument();

        document.VersionLabel.Should().BeNull();
    }

    #endregion

    #region Reclassify

    [Fact]
    public void Reclassify_SetsAllFields()
    {
        var document = CreateDocument();
        var baseDocId = Guid.NewGuid();

        document.Reclassify(DocumentCategory.Expansion, baseDocId, "Expansion v2");

        document.DocumentCategory.Should().Be(DocumentCategory.Expansion);
        document.BaseDocumentId.Should().Be(baseDocId);
        document.VersionLabel.Should().Be("Expansion v2");
    }

    [Fact]
    public void Reclassify_WithNullBaseDocument_UnlinksBase()
    {
        var document = CreateDocument();
        document.LinkToBaseDocument(Guid.NewGuid());

        document.Reclassify(DocumentCategory.Rulebook, null, "v1.0");

        document.BaseDocumentId.Should().BeNull();
        document.VersionLabel.Should().Be("v1.0");
    }

    [Fact]
    public void Reclassify_WithNullVersionLabel_ClearsLabel()
    {
        var document = CreateDocument();
        document.SetVersionLabel("old label");

        document.Reclassify(DocumentCategory.QuickStart, null, null);

        document.DocumentCategory.Should().Be(DocumentCategory.QuickStart);
        document.VersionLabel.Should().BeNull();
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

        document.DocumentCategory.Should().Be(DocumentCategory.Errata);
        document.BaseDocumentId.Should().Be(baseDocId2);
        document.VersionLabel.Should().Be("v2 Errata");
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
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English,
            versionLabel: "3rd Edition");

        document.VersionLabel.Should().Be("3rd Edition");
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
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English);

        document.VersionLabel.Should().BeNull();
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
