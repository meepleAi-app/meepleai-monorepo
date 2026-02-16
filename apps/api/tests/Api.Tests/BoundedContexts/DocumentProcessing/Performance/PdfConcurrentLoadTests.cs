using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Performance;

/// <summary>
/// Load testing for PDF extraction pipeline under concurrent access.
/// Validates no degradation, no deadlocks, and correct isolation between sessions.
/// Issue #4143: Performance Testing - PDF Wizard
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4143")]
public class PdfConcurrentLoadTests
{
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<PdfProcessingOptions> _options;

    public PdfConcurrentLoadTests()
    {
        _logger = Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>();
        _configuration = new ConfigurationBuilder().Build();
        _options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });
    }

    #region Concurrent Extraction Tests

    [Fact]
    public async Task ConcurrentExtractions_5Sessions_AllSucceedAtStage1()
    {
        const int sessionCount = 5;
        var tasks = new List<Task<EnhancedExtractionResult>>();

        for (var i = 0; i < sessionCount; i++)
        {
            var sessionId = i;
            tasks.Add(Task.Run(async () =>
            {
                var stage1 = new ConcurrentFakeExtractor(
                    delayMs: 20 + sessionId * 10, // Stagger delays slightly
                    success: true, quality: ExtractionQuality.High,
                    text: $"Session {sessionId} extracted text");
                var stage2 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Medium);
                var stage3 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Low);

                var orchestrator = new EnhancedPdfProcessingOrchestrator(
                    stage1, stage2, stage3, _logger, _configuration, _options);

                await using var pdfStream = CreateDummyPdfStream();
                return await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
            }));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(sessionCount);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue("All concurrent extractions should succeed");
            r.StageUsed.Should().Be(1, "All should resolve at Stage 1");
            r.StageName.Should().Be("Unstructured");
        });

        // Verify session isolation: each has unique text
        var uniqueTexts = results.Select(r => r.ExtractedText).Distinct().Count();
        uniqueTexts.Should().Be(sessionCount, "Each session should produce unique output");
    }

    [Fact]
    public async Task ConcurrentExtractions_MixedStages_NoInterference()
    {
        const int sessionCount = 10;
        var tasks = new List<Task<EnhancedExtractionResult>>();

        for (var i = 0; i < sessionCount; i++)
        {
            var sessionId = i;
            tasks.Add(Task.Run(async () =>
            {
                IPdfTextExtractor stage1, stage2, stage3;

                // Mix of quality levels to trigger different stages
                if (sessionId % 3 == 0)
                {
                    // Stage 1 success (High quality)
                    stage1 = new ConcurrentFakeExtractor(delayMs: 30, success: true, quality: ExtractionQuality.High,
                        text: $"S1-{sessionId}");
                    stage2 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Medium);
                    stage3 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Low);
                }
                else if (sessionId % 3 == 1)
                {
                    // Stage 2 fallback (Low quality at Stage 1)
                    stage1 = new ConcurrentFakeExtractor(delayMs: 20, success: true, quality: ExtractionQuality.Low);
                    stage2 = new ConcurrentFakeExtractor(delayMs: 40, success: true, quality: ExtractionQuality.Medium,
                        text: $"S2-{sessionId}");
                    stage3 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Low);
                }
                else
                {
                    // Stage 3 fallback (both fail)
                    stage1 = new ConcurrentFakeExtractor(delayMs: 10, success: false, errorMsg: "Stage 1 fail");
                    stage2 = new ConcurrentFakeExtractor(delayMs: 10, success: false, errorMsg: "Stage 2 fail");
                    stage3 = new ConcurrentFakeExtractor(delayMs: 20, success: true, quality: ExtractionQuality.Low,
                        text: $"S3-{sessionId}");
                }

                var orchestrator = new EnhancedPdfProcessingOrchestrator(
                    stage1, stage2, stage3, _logger, _configuration, _options);

                await using var pdfStream = CreateDummyPdfStream();
                return await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
            }));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(sessionCount);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue("All sessions should succeed"));

        // Verify correct stage routing
        for (var i = 0; i < sessionCount; i++)
        {
            var expectedStage = (i % 3) switch
            {
                0 => 1, // High quality → Stage 1
                1 => 2, // Low quality → Stage 2
                _ => 3  // Both fail → Stage 3
            };
            results[i].StageUsed.Should().Be(expectedStage, $"Session {i} should use Stage {expectedStage}");
        }
    }

    [Fact]
    public async Task ConcurrentPagedExtractions_5Sessions_AllComplete()
    {
        const int sessionCount = 5;
        var tasks = new List<Task<EnhancedPagedExtractionResult>>();

        for (var i = 0; i < sessionCount; i++)
        {
            var pageCount = 10 + i * 20; // 10, 30, 50, 70, 90 pages
            tasks.Add(Task.Run(async () =>
            {
                var stage1 = new ConcurrentFakeExtractor(delayMs: 30, success: true,
                    pageCount: pageCount, charsPerPage: 1000);
                var stage2 = new ConcurrentFakeExtractor(success: true, pageCount: pageCount, charsPerPage: 700);
                var stage3 = new ConcurrentFakeExtractor(success: true, pageCount: pageCount, charsPerPage: 300);

                var orchestrator = new EnhancedPdfProcessingOrchestrator(
                    stage1, stage2, stage3, _logger, _configuration, _options);

                await using var pdfStream = CreateDummyPdfStream();
                return await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);
            }));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(sessionCount);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.StageUsed.Should().Be(1, "1000 cpp → score 1.0 ≥ 0.80 threshold");
        });

        // Verify page counts are preserved correctly per session
        for (var i = 0; i < sessionCount; i++)
        {
            var expectedPages = 10 + i * 20;
            results[i].TotalPages.Should().Be(expectedPages);
            results[i].PageChunks.Count.Should().Be(expectedPages);
        }
    }

    #endregion

    #region No Degradation Under Load

    [Fact]
    public async Task HighLoad_20ConcurrentExtractions_NoDeadlocks()
    {
        const int sessionCount = 20;
        var tasks = new List<Task<EnhancedExtractionResult>>();

        for (var i = 0; i < sessionCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stage1 = new ConcurrentFakeExtractor(delayMs: 10, success: true,
                    quality: ExtractionQuality.High, text: "Load test text");
                var stage2 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Medium);
                var stage3 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Low);

                var orchestrator = new EnhancedPdfProcessingOrchestrator(
                    stage1, stage2, stage3, _logger, _configuration, _options);

                await using var pdfStream = CreateDummyPdfStream();
                return await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
            }));
        }

        var allCompleted = Task.WhenAll(tasks);
        var completedInTime = await Task.WhenAny(allCompleted, Task.Delay(TimeSpan.FromSeconds(30)));

        completedInTime.Should().Be(allCompleted,
            "20 concurrent extractions should complete without deadlocks within 30s");

        var results = await allCompleted;
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task MixedOperations_TextAndPaged_NoCrossContamination()
    {
        const int operationCount = 10;
        var textTasks = new List<Task<EnhancedExtractionResult>>();
        var pagedTasks = new List<Task<EnhancedPagedExtractionResult>>();

        for (var i = 0; i < operationCount; i++)
        {
            var idx = i;
            if (i % 2 == 0)
            {
                textTasks.Add(Task.Run(async () =>
                {
                    var stage1 = new ConcurrentFakeExtractor(delayMs: 20, success: true,
                        quality: ExtractionQuality.High, text: $"Text-{idx}");
                    var stage2 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Medium);
                    var stage3 = new ConcurrentFakeExtractor(success: true, quality: ExtractionQuality.Low);

                    var orch = new EnhancedPdfProcessingOrchestrator(
                        stage1, stage2, stage3, _logger, _configuration, _options);
                    await using var stream = CreateDummyPdfStream();
                    return await orch.ExtractTextWithFallbackAsync(stream);
                }));
            }
            else
            {
                pagedTasks.Add(Task.Run(async () =>
                {
                    var stage1 = new ConcurrentFakeExtractor(delayMs: 20, success: true,
                        pageCount: 10, charsPerPage: 1000);
                    var stage2 = new ConcurrentFakeExtractor(success: true, pageCount: 10, charsPerPage: 700);
                    var stage3 = new ConcurrentFakeExtractor(success: true, pageCount: 10, charsPerPage: 300);

                    var orch = new EnhancedPdfProcessingOrchestrator(
                        stage1, stage2, stage3, _logger, _configuration, _options);
                    await using var stream = CreateDummyPdfStream();
                    return await orch.ExtractPagedTextWithFallbackAsync(stream);
                }));
            }
        }

        await Task.WhenAll(
            Task.WhenAll(textTasks),
            Task.WhenAll(pagedTasks));

        var textResults = await Task.WhenAll(textTasks);
        var pagedResults = await Task.WhenAll(pagedTasks);

        textResults.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.StageUsed.Should().Be(1);
        });

        pagedResults.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.StageUsed.Should().Be(1);
            r.TotalPages.Should().Be(10);
        });
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateDummyPdfStream()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        return new MemoryStream(pdfBytes);
    }

    private sealed class ConcurrentFakeExtractor : IPdfTextExtractor
    {
        private readonly int _delayMs;
        private readonly bool _success;
        private readonly ExtractionQuality _quality;
        private readonly string _text;
        private readonly string? _errorMsg;
        private readonly int _pageCount;
        private readonly int _charsPerPage;

        public ConcurrentFakeExtractor(
            int delayMs = 0,
            bool success = true,
            ExtractionQuality quality = ExtractionQuality.High,
            string? text = null,
            string? errorMsg = null,
            int pageCount = 10,
            int charsPerPage = 1000)
        {
            _delayMs = delayMs;
            _success = success;
            _quality = quality;
            _text = text ?? "Concurrent extraction text";
            _errorMsg = errorMsg;
            _pageCount = pageCount;
            _charsPerPage = charsPerPage;
        }

        public async Task<TextExtractionResult> ExtractTextAsync(
            Stream pdfStream, bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            return _success
                ? TextExtractionResult.CreateSuccess(_text, _pageCount, _text.Length, false, _quality)
                : TextExtractionResult.CreateFailure(_errorMsg ?? "Concurrent extraction failed");
        }

        public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
            Stream pdfStream, bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            if (!_success)
                return PagedTextExtractionResult.CreateFailure(_errorMsg ?? "Concurrent paged failed");

            var chunks = Enumerable.Range(1, _pageCount)
                .Select(i => new PageTextChunk(i, new string('X', _charsPerPage),
                    (i - 1) * _charsPerPage, i * _charsPerPage))
                .ToList();

            return PagedTextExtractionResult.CreateSuccess(
                chunks, _pageCount, _pageCount * _charsPerPage, false);
        }
    }

    #endregion
}
