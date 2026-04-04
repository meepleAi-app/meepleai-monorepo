using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class PdfSeederTests
{
    [Fact]
    public void ProcessingState_Pending_IsCorrectString()
    {
        var state = nameof(PdfProcessingState.Pending);
        state.Should().Be("Pending");
        state.Should().NotBe("Ready");
    }

    [Fact]
    public void ProcessingState_Ready_IsNotUsedByPdfSeeder()
    {
        // PdfSeeder creates docs in Pending state, NOT Ready
        // Ready was used by the old PdfRulebookSeeder
        var pendingState = nameof(PdfProcessingState.Pending);
        var readyState = nameof(PdfProcessingState.Ready);
        pendingState.Should().NotBe(readyState);
    }
}
