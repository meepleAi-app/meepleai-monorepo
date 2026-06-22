using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Events;

public class PdfCoverGeneratedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var pdfId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        const string coverR2Key = "pdf-cover-abc123";
        const int pageIndex = 0;

        var evt = new PdfCoverGeneratedEvent(pdfId, sharedGameId, coverR2Key, pageIndex);

        evt.PdfDocumentId.Should().Be(pdfId);
        evt.SharedGameId.Should().Be(sharedGameId);
        evt.CoverR2Key.Should().Be(coverR2Key);
        evt.CoverPageIndex.Should().Be(pageIndex);
    }

    [Fact]
    public void Constructor_AcceptsNullSharedGameId()
    {
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), null, "key", 2);

        evt.SharedGameId.Should().BeNull();
    }
}
