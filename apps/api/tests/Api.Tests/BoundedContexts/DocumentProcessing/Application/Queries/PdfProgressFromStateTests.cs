using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #5106: the progress of a PDF without a recorded one, derived from <c>pdf_documents.processing_state</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProgressFromStateTests
{
    private static readonly DateTime UploadedAt = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AReadyPdf_IsCompleted_AtItsProcessingTime()
    {
        var processedAt = UploadedAt.AddMinutes(4);

        var progress = PdfProgressFromState.Derive("Ready", processingError: null, processedAt, UploadedAt);

        progress.CurrentStep.Should().Be(ProcessingStep.Completed);
        progress.PercentComplete.Should().Be(100);
        progress.StartedAt.Should().Be(UploadedAt);
        progress.CompletedAt.Should().Be(processedAt);
        progress.ElapsedTime.Should().Be(TimeSpan.FromMinutes(4));
        progress.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void AFailedPdf_IsFailed_WithItsError()
    {
        var progress = PdfProgressFromState.Derive("Failed", "Extraction failed", processedAt: null, UploadedAt);

        progress.CurrentStep.Should().Be(ProcessingStep.Failed);
        progress.PercentComplete.Should().Be(0);
        progress.CompletedAt.Should().BeNull();
        progress.ErrorMessage.Should().Be("Extraction failed");
    }

    [Theory]
    [InlineData("Extracting", ProcessingStep.Extracting)]
    [InlineData("Chunking", ProcessingStep.Chunking)]
    [InlineData("Embedding", ProcessingStep.Embedding)]
    [InlineData("Indexing", ProcessingStep.Indexing)]
    [InlineData("Uploading", ProcessingStep.Uploading)]
    [InlineData("Pending", ProcessingStep.Uploading)]
    public void APdfStillInThePipeline_IsAtItsStep_WithNothingDone(string state, ProcessingStep expected)
    {
        var progress = PdfProgressFromState.Derive(state, processingError: "stale", processedAt: null, UploadedAt);

        progress.CurrentStep.Should().Be(expected);
        progress.PercentComplete.Should().Be(0);
        progress.CompletedAt.Should().BeNull();
        progress.ErrorMessage.Should().BeNull("only a failed PDF reports its error");
    }
}
