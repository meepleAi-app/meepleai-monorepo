using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Unit tests for CitationPriorityService domain service.
/// Issue #2051: Citation ordering and deduplication logic
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CitationPriorityServiceTests
{
    private readonly CitationPriorityService _service = new();

    [Fact]
    public void OrderCitations_HomeruleFirst_CorrectOrder()
    {
        // Arrange
        var baseDoc = CreatePdfDocument(DocumentType.Base, uploadedAt: DateTime.UtcNow.AddDays(-10));
        var homeruleDoc = CreatePdfDocument(DocumentType.Homerule, uploadedAt: DateTime.UtcNow.AddDays(-5));

        var citations = new List<Citation>
        {
            new(baseDoc.Id, "Citation from base", 1, "base.pdf"),
            new(homeruleDoc.Id, "Citation from homerule", 1, "homerule.pdf")
        };

        var documents = new List<PdfDocument> { baseDoc, homeruleDoc };

        // Act
        var ordered = _service.OrderCitations(citations, documents);

        // Assert
        ordered.Should().HaveCount(2);
        ordered[0].Citation.PdfDocumentId.Should().Be(homeruleDoc.Id); // Priority 3
        ordered[1].Citation.PdfDocumentId.Should().Be(baseDoc.Id); // Priority 0
    }

    [Fact]
    public void OrderCitations_SamePriority_NewerFirst()
    {
        // Arrange
        var oldExpansion = CreatePdfDocument(DocumentType.Expansion, uploadedAt: DateTime.UtcNow.AddDays(-10));
        var newErrata = CreatePdfDocument(DocumentType.Errata, uploadedAt: DateTime.UtcNow.AddDays(-1));

        // Both have priority 1 and 2, but errata has higher priority
        var citations = new List<Citation>
        {
            new(oldExpansion.Id, "Old expansion", 1, "expansion.pdf"),
            new(newErrata.Id, "New errata", 1, "errata.pdf")
        };

        var documents = new List<PdfDocument> { oldExpansion, newErrata };

        // Act
        var ordered = _service.OrderCitations(citations, documents);

        // Assert
        ordered[0].Citation.PdfDocumentId.Should().Be(newErrata.Id); // Higher priority (2 vs 1)
    }

    [Fact]
    public void OrderCitations_ExpansionVsErrata_SameDate_ErrataFirst()
    {
        // Arrange
        var uploadDate = DateTime.UtcNow.AddDays(-5);
        var expansion = CreatePdfDocument(DocumentType.Expansion, uploadedAt: uploadDate); // Priority 1
        var errata = CreatePdfDocument(DocumentType.Errata, uploadedAt: uploadDate); // Priority 2

        var citations = new List<Citation>
        {
            new(expansion.Id, "From expansion", 1, "expansion.pdf"),
            new(errata.Id, "From errata", 1, "errata.pdf")
        };

        var documents = new List<PdfDocument> { expansion, errata };

        // Act
        var ordered = _service.OrderCitations(citations, documents);

        // Assert
        ordered[0].Citation.PdfDocumentId.Should().Be(errata.Id); // Priority 2 > 1
    }

    [Fact]
    public void DeduplicateCitations_SameContent_KeepsHighestPriority()
    {
        // Arrange
        var baseCitation = new PrioritizedCitation(
            new Citation(Guid.NewGuid(), "Draw 2 cards", 1, "base.pdf"),
            0, // Base priority
            DateTime.UtcNow.AddDays(-10),
            DocumentType.Base);

        var homeruleCitation = new PrioritizedCitation(
            new Citation(Guid.NewGuid(), "Draw 2 cards", 1, "homerule.pdf"),
            3, // Homerule priority
            DateTime.UtcNow,
            DocumentType.Homerule);

        var citations = new List<PrioritizedCitation> { baseCitation, homeruleCitation };

        // Act
        var deduplicated = _service.DeduplicateCitations(citations);

        // Assert
        deduplicated.Should().HaveCount(1);
        deduplicated[0].Should().Be(baseCitation); // First in list (already ordered by priority)
    }

    [Fact]
    public void DeduplicateCitations_DifferentContent_KeepsBoth()
    {
        // Arrange
        var citation1 = new PrioritizedCitation(
            new Citation(Guid.NewGuid(), "Draw 2 cards", 1, "doc1.pdf"),
            0, DateTime.UtcNow, DocumentType.Base);

        var citation2 = new PrioritizedCitation(
            new Citation(Guid.NewGuid(), "Draw 3 cards", 1, "doc2.pdf"),
            1, DateTime.UtcNow, DocumentType.Expansion);

        var citations = new List<PrioritizedCitation> { citation1, citation2 };

        // Act
        var deduplicated = _service.DeduplicateCitations(citations);

        // Assert
        deduplicated.Should().HaveCount(2);
    }

    [Fact]
    public void FilterByDocuments_EmptySelection_ReturnsAll()
    {
        // Arrange
        var citation1 = CreatePrioritizedCitation(Guid.NewGuid());
        var citation2 = CreatePrioritizedCitation(Guid.NewGuid());

        var citations = new List<PrioritizedCitation> { citation1, citation2 };
        var selectedIds = new List<Guid>(); // Empty = all

        // Act
        var filtered = _service.FilterByDocuments(citations, selectedIds);

        // Assert
        filtered.Should().HaveCount(2);
    }

    [Fact]
    public void FilterByDocuments_SpecificDocuments_ReturnsOnlySelected()
    {
        // Arrange
        var pdfId1 = Guid.NewGuid();
        var pdfId2 = Guid.NewGuid();
        var pdfId3 = Guid.NewGuid();

        var citations = new List<PrioritizedCitation>
        {
            CreatePrioritizedCitation(pdfId1),
            CreatePrioritizedCitation(pdfId2),
            CreatePrioritizedCitation(pdfId3)
        };

        var selectedIds = new List<Guid> { pdfId1, pdfId3 };

        // Act
        var filtered = _service.FilterByDocuments(citations, selectedIds);

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(c => c.Citation.PdfDocumentId == pdfId1);
        filtered.Should().Contain(c => c.Citation.PdfDocumentId == pdfId3);
        filtered.Should().NotContain(c => c.Citation.PdfDocumentId == pdfId2);
    }

    [Fact]
    public void OrderCitations_NullArguments_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act1 = () => _service.OrderCitations(null!, new List<PdfDocument>());
        var act2 = () => _service.OrderCitations(new List<Citation>(), null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeduplicateCitations_NullArgument_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _service.DeduplicateCitations(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterByDocuments_NullArguments_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act1 = () => _service.FilterByDocuments(null!, new List<Guid>());
        var act2 = () => _service.FilterByDocuments(new List<PrioritizedCitation>(), null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
    }

    // Helper methods
    private static PdfDocument CreatePdfDocument(DocumentType type, DateTime uploadedAt)
    {
        var doc = new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/test.pdf",
            new FileSize(1000),
            Guid.NewGuid(),
            documentType: type);

        // Use reflection to set UploadedAt (private setter)
        var prop = typeof(PdfDocument).GetProperty("UploadedAt");
        prop?.SetValue(doc, uploadedAt);

        return doc;
    }

    private static PrioritizedCitation CreatePrioritizedCitation(Guid pdfDocumentId)
    {
        return new PrioritizedCitation(
            new Citation(pdfDocumentId, "Test citation", 1, "test.pdf"),
            1,
            DateTime.UtcNow,
            DocumentType.Base);
    }
}
