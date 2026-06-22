using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Events;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfDeletedDomainEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var pdfId = Guid.NewGuid();
        const string coverR2Key = "pdf-cover-abc123";

        var evt = new PdfDeletedDomainEvent(pdfId, coverR2Key);

        evt.PdfDocumentId.Should().Be(pdfId);
        evt.CoverR2Key.Should().Be(coverR2Key);
    }

    [Fact]
    public void Constructor_AcceptsNullCoverR2Key()
    {
        var evt = new PdfDeletedDomainEvent(Guid.NewGuid(), null);

        evt.CoverR2Key.Should().BeNull();
    }
}
