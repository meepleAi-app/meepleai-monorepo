using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public sealed class ProcessingMetricsServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly PdfDocumentRepository _pdfRepository;
    private readonly ProcessingMetricsService _service;

    public ProcessingMetricsServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();
        _pdfRepository = new PdfDocumentRepository(_context, mockEventCollector.Object);
        _service = new ProcessingMetricsService(
            _context,
            _pdfRepository,
            NullLogger<ProcessingMetricsService>.Instance);
    }

    [Fact]
    public async Task RecordStepDurationAsync_ValidData_PersistsToDatabase()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var step = PdfProcessingState.Extracting;
        var duration = TimeSpan.FromSeconds(45.2);
        var sizeBytes = 1024000L;
        var pageCount = 10;

        // Act
        await _service.RecordStepDurationAsync(pdfId, step, duration, sizeBytes, pageCount);

        // Assert
        var metric = await _context.ProcessingMetrics
            .FirstOrDefaultAsync(m => m.PdfDocumentId == pdfId);

        metric.Should().NotBeNull();
        metric!.Step.Should().Be("Extracting");
        metric.DurationSeconds.Should().Be(45.2m);
        metric.PdfSizeBytes.Should().Be(sizeBytes);
        metric.PageCount.Should().Be(pageCount);
        metric.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordStepDurationAsync_NegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var duration = TimeSpan.FromSeconds(-10);

        // Act
        var act = async () => await _service.RecordStepDurationAsync(
            pdfId,
            PdfProcessingState.Chunking,
            duration,
            1024,
            5);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Duration cannot be negative*");
    }

    [Fact]
    public async Task GetAverageDurationAsync_NoData_ReturnsZeroStatistics()
    {
        // Arrange
        var step = PdfProcessingState.Embedding;

        // Act
        var stats = await _service.GetAverageDurationAsync(step);

        // Assert
        stats.Should().NotBeNull();
        stats.Step.Should().Be("Embedding");
        stats.AverageDurationSeconds.Should().Be(0);
        stats.MedianDurationSeconds.Should().Be(0);
        stats.P95DurationSeconds.Should().Be(0);
        stats.SampleSize.Should().Be(0);
    }

    [Fact]
    public async Task GetAverageDurationAsync_WithData_ReturnsCorrectStatistics()
    {
        // Arrange
        var step = PdfProcessingState.Chunking;
        var pdfId = Guid.NewGuid();

        // Insert 10 metrics with known durations
        var durations = new[] { 10.0, 12.0, 15.0, 18.0, 20.0, 22.0, 25.0, 30.0, 35.0, 40.0 };
        foreach (var duration in durations)
        {
            await _service.RecordStepDurationAsync(
                pdfId,
                step,
                TimeSpan.FromSeconds(duration),
                1024000,
                10);
        }

        // Act
        var stats = await _service.GetAverageDurationAsync(step);

        // Assert
        stats.Should().NotBeNull();
        stats.Step.Should().Be("Chunking");
        stats.SampleSize.Should().Be(10);

        // Average: (10+12+15+18+20+22+25+30+35+40) / 10 = 22.7
        stats.AverageDurationSeconds.Should().BeApproximately(22.7, 0.1);

        // Median (P50): between 20 and 22
        stats.MedianDurationSeconds.Should().BeInRange(20, 22);

        // P95: should be close to 40 (95th percentile of sorted list)
        stats.P95DurationSeconds.Should().BeGreaterThan(35);
    }

    [Fact]
    public async Task CalculateETAAsync_TerminalState_ReturnsZero()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdf(pdfId, PdfProcessingState.Ready);
        await _pdfRepository.AddAsync(pdf);
        await _context.SaveChangesAsync();

        // Act
        var eta = await _service.CalculateETAAsync(pdfId, PdfProcessingState.Ready);

        // Assert
        eta.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task CalculateETAAsync_InsufficientHistoricalData_UsesFallback()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdf(pdfId, PdfProcessingState.Extracting);
        await _pdfRepository.AddAsync(pdf);
        await _context.SaveChangesAsync();

        // Act (no historical data, should use fallback: 2s/page * remaining steps)
        var eta = await _service.CalculateETAAsync(pdfId, PdfProcessingState.Extracting);

        // Assert
        eta.Should().NotBeNull();
        // 4 remaining steps (Chunking, Embedding, Indexing) * 2s/page * 10 pages = ~80s
        eta!.Value.TotalSeconds.Should().BeGreaterThan(60); // Approximate check
    }

    [Fact]
    public async Task CleanupOldMetricsAsync_RetainsCorrectCount()
    {
        // Arrange
        var step = PdfProcessingState.Indexing;
        var pdfId = Guid.NewGuid();

        // Insert 50 metrics
        for (int i = 0; i < 50; i++)
        {
            await _service.RecordStepDurationAsync(
                pdfId,
                step,
                TimeSpan.FromSeconds(10 + i),
                1024000,
                10);

            // Small delay to ensure different timestamps
            await Task.Delay(10);
        }

        // Act - Retain only 30
        var deleted = await _service.CleanupOldMetricsAsync(retainPerStep: 30);

        // Assert
        deleted.Should().Be(20); // 50 - 30 = 20 deleted

        var remaining = await _context.ProcessingMetrics
            .Where(m => m.Step == "Indexing")
            .CountAsync();

        remaining.Should().Be(30);
    }

    private static Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument CreateTestPdf(
        Guid id,
        PdfProcessingState state)
    {
        var pdf = new Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument(
            id,
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/tmp/test.pdf",
            new FileSize(1024000),
            Guid.NewGuid(),
            LanguageCode.English);

        // Transition to target state
        while (pdf.ProcessingState != state && pdf.ProcessingState != PdfProcessingState.Ready)
        {
            var nextState = pdf.ProcessingState switch
            {
                PdfProcessingState.Pending => PdfProcessingState.Uploading,
                PdfProcessingState.Uploading => PdfProcessingState.Extracting,
                PdfProcessingState.Extracting => PdfProcessingState.Chunking,
                PdfProcessingState.Chunking => PdfProcessingState.Embedding,
                PdfProcessingState.Embedding => PdfProcessingState.Indexing,
                PdfProcessingState.Indexing => PdfProcessingState.Ready,
                _ => state
            };

            pdf.TransitionTo(nextState);

            if (nextState == state)
                break;
        }

        return pdf;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
