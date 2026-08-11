using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

[Trait("Category", TestCategories.Unit)]
public class OrchestratedPdfTextExtractorTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Stub Stage-1 extractor: returns a paged result carrying StructuredElements, with
    // ≥800 chars/page so the Stage-1 quality gate (CalculatePagedQualityScore ≥ 0.80) accepts it.
    private sealed class StubStage1Extractor : IPdfTextExtractor
    {
        private readonly IReadOnlyList<ExtractedElement> _elements;
        public StubStage1Extractor(IReadOnlyList<ExtractedElement> elements) => _elements = elements;

        public Task<TextExtractionResult> ExtractTextAsync(Stream s, bool ocr = true, CancellationToken ct = default) =>
            Task.FromResult(TextExtractionResult.CreateSuccess(new string('x', 800), 1, 800, false, ExtractionQuality.High));

        public Task<PagedTextExtractionResult> ExtractPagedTextAsync(Stream s, bool ocr = true, CancellationToken ct = default) =>
            Task.FromResult(PagedTextExtractionResult.CreateSuccess(
                new List<PageTextChunk> { new(1, new string('x', 800), 0, 799) },
                totalPages: 1, totalCharacters: 800, ocrTriggered: false, structuredElements: _elements));
    }

    /// <summary>#3589 follow-up: every stage fails, the final (Stage 3) failure is permanent.</summary>
    private sealed class AlwaysPermanentlyFailingExtractor : IPdfTextExtractor
    {
        public Task<TextExtractionResult> ExtractTextAsync(Stream s, bool ocr = true, CancellationToken ct = default) =>
            Task.FromResult(TextExtractionResult.CreateFailure("permanent", isPermanentFailure: true));

        public Task<PagedTextExtractionResult> ExtractPagedTextAsync(Stream s, bool ocr = true, CancellationToken ct = default) =>
            Task.FromResult(PagedTextExtractionResult.CreateFailure("permanent", isPermanentFailure: true));
    }

    private static Stream DummyPdf() =>
        new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest\n%%EOF"));

    [Fact]
    public async Task ExtractPagedTextAsync_PropagatesStructuredElements_ThroughRealOrchestratorPath()
    {
        var elements = new List<ExtractedElement> { new("Preparazione", 1, "Title") };
        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            new StubStage1Extractor(elements),
            Mock.Of<IPdfTextExtractor>(),
            Mock.Of<IPdfTextExtractor>(),
            Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new PdfProcessingOptions { LargePdfThresholdBytes = 52428800, UseTempFileForLargePdfs = true }),
            Mock.Of<ITextChunkingService>());
        var adapter = new OrchestratedPdfTextExtractor(orchestrator);

        await using var pdf = DummyPdf();
        var result = await adapter.ExtractPagedTextAsync(pdf, cancellationToken: TestCancellationToken);

        result.Success.Should().BeTrue();
        result.StructuredElements.Should().NotBeNull();
        result.StructuredElements!.Single().Text.Should().Be("Preparazione");
    }

    /// <summary>
    /// #3589 follow-up: IsPermanentFailure must survive the full Orchestrator round-trip
    /// (IPdfTextExtractor → EnhancedPdfProcessingOrchestrator → OrchestratedPdfTextExtractor)
    /// so a downstream PdfProcessingPipelineService.ExtractTextAsync sees the same signal it
    /// gets from the direct (non-orchestrated) UnstructuredPdfTextExtractor.
    /// </summary>
    [Fact]
    public async Task ExtractTextAsync_AllStagesPermanentlyFail_PropagatesIsPermanentFailure()
    {
        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            new AlwaysPermanentlyFailingExtractor(),
            new AlwaysPermanentlyFailingExtractor(),
            new AlwaysPermanentlyFailingExtractor(),
            Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new PdfProcessingOptions { LargePdfThresholdBytes = 52428800, UseTempFileForLargePdfs = true }),
            Mock.Of<ITextChunkingService>());
        var adapter = new OrchestratedPdfTextExtractor(orchestrator);

        await using var pdf = DummyPdf();
        var result = await adapter.ExtractTextAsync(pdf, cancellationToken: TestCancellationToken);

        result.Success.Should().BeFalse();
        result.IsPermanentFailure.Should().BeTrue();
    }

    /// <summary>Paged counterpart of <see cref="ExtractTextAsync_AllStagesPermanentlyFail_PropagatesIsPermanentFailure"/>.</summary>
    [Fact]
    public async Task ExtractPagedTextAsync_AllStagesPermanentlyFail_PropagatesIsPermanentFailure()
    {
        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            new AlwaysPermanentlyFailingExtractor(),
            new AlwaysPermanentlyFailingExtractor(),
            new AlwaysPermanentlyFailingExtractor(),
            Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new PdfProcessingOptions { LargePdfThresholdBytes = 52428800, UseTempFileForLargePdfs = true }),
            Mock.Of<ITextChunkingService>());
        var adapter = new OrchestratedPdfTextExtractor(orchestrator);

        await using var pdf = DummyPdf();
        var result = await adapter.ExtractPagedTextAsync(pdf, cancellationToken: TestCancellationToken);

        result.Success.Should().BeFalse();
        result.IsPermanentFailure.Should().BeTrue();
    }
}
