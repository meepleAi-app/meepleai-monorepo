using System;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

public class PdfStorageKeyTests
{
    [Fact]
    public void ForPdf_UsesPdfIdNotGameId()
    {
        var pdfId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        PdfStorageKey.ForPdf(pdfId).Should().Be("11111111111111111111111111111111");
    }
}
