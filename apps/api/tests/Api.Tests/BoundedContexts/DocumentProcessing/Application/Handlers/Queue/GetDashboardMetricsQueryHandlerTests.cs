using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Tests for GetDashboardMetricsQueryHandler.
/// Issue #5459: Per-phase timing + metrics dashboard.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetDashboardMetricsQueryHandlerTests
{
    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new GetDashboardMetricsQueryHandler(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("db");
    }

    [Fact]
    public void Constructor_WithValidDbContext_CreatesInstance()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Act
        var handler = new GetDashboardMetricsQueryHandler(dbContext);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeros()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PhaseTimings.Should().BeEmpty();
        result.TotalProcessed.Should().Be(0);
        result.TotalFailed.Should().Be(0);
        result.FailureRatePercent.Should().Be(0);
        result.AvgTotalDurationSeconds.Should().Be(0);
        result.Period.Should().Be("24h");
    }

    [Fact]
    public async Task Handle_DefaultPeriod_FiltersLast24Hours()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Document within 24h window
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddHours(-12),
            userId: userId));

        // Document outside 24h window — should be excluded
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddHours(-48),
            userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(1);
        result.Period.Should().Be("24h");
    }

    [Fact]
    public async Task Handle_7dPeriod_FiltersLast7Days()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Document within 7d window
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddDays(-3),
            userId: userId));

        // Document within 7d window
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "failed",
            uploadedAt: DateTime.UtcNow.AddDays(-5),
            userId: userId));

        // Document outside 7d window
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddDays(-10),
            userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("7d");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(1);
        result.TotalFailed.Should().Be(1);
        result.Period.Should().Be("7d");
    }

    [Fact]
    public async Task Handle_30dPeriod_FiltersLast30Days()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // 3 docs within 30d, 1 outside
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddDays(-5),
            userId: userId));

        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddDays(-20),
            userId: userId));

        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "failed",
            uploadedAt: DateTime.UtcNow.AddDays(-25),
            userId: userId));

        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddDays(-60),
            userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("30d");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(2);
        result.TotalFailed.Should().Be(1);
        result.Period.Should().Be("30d");
    }

    [Fact]
    public async Task Handle_UnknownPeriod_DefaultsTo24h()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Document within 24h
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddHours(-6),
            userId: userId));

        // Document outside 24h
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "completed",
            uploadedAt: DateTime.UtcNow.AddDays(-3),
            userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("unknown_period");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(1);
        result.Period.Should().Be("unknown_period");
    }

    [Fact]
    public async Task Handle_PhaseTimingAggregation_GroupsByStep()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfId1 = Guid.NewGuid();
        var pdfId2 = Guid.NewGuid();

        // Two metrics for "extraction" step
        dbContext.Set<ProcessingMetricEntity>().AddRange(
            CreateMetric(pdfId1, "extraction", durationSeconds: 10.0m, createdAt: DateTime.UtcNow.AddHours(-1)),
            CreateMetric(pdfId2, "extraction", durationSeconds: 20.0m, createdAt: DateTime.UtcNow.AddHours(-2)));

        // One metric for "chunking" step
        dbContext.Set<ProcessingMetricEntity>().Add(
            CreateMetric(pdfId1, "chunking", durationSeconds: 5.0m, createdAt: DateTime.UtcNow.AddHours(-1)));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PhaseTimings.Should().HaveCount(2);

        var extractionTiming = result.PhaseTimings.Single(p => p.Phase == "extraction");
        extractionTiming.AvgDurationSeconds.Should().Be(15.0); // (10 + 20) / 2
        extractionTiming.MinDurationSeconds.Should().Be(10.0);
        extractionTiming.MaxDurationSeconds.Should().Be(20.0);
        extractionTiming.SampleCount.Should().Be(2);

        var chunkingTiming = result.PhaseTimings.Single(p => p.Phase == "chunking");
        chunkingTiming.AvgDurationSeconds.Should().Be(5.0);
        chunkingTiming.MinDurationSeconds.Should().Be(5.0);
        chunkingTiming.MaxDurationSeconds.Should().Be(5.0);
        chunkingTiming.SampleCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PhaseTimings_ExcludesMetricsOutsidePeriod()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfId = Guid.NewGuid();

        // Metric within 24h
        dbContext.Set<ProcessingMetricEntity>().Add(
            CreateMetric(pdfId, "extraction", durationSeconds: 10.0m, createdAt: DateTime.UtcNow.AddHours(-6)));

        // Metric outside 24h — should be excluded
        dbContext.Set<ProcessingMetricEntity>().Add(
            CreateMetric(pdfId, "extraction", durationSeconds: 100.0m, createdAt: DateTime.UtcNow.AddDays(-3)));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PhaseTimings.Should().HaveCount(1);
        var timing = result.PhaseTimings.Single();
        timing.AvgDurationSeconds.Should().Be(10.0);
        timing.SampleCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FailureRateCalculation_ReturnsCorrectPercentage()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // 3 completed, 1 failed = 25% failure rate
        dbContext.Set<PdfDocumentEntity>().AddRange(
            CreatePdfDocument(ProcessingStatus: "completed", uploadedAt: DateTime.UtcNow.AddHours(-1), userId: userId),
            CreatePdfDocument(ProcessingStatus: "completed", uploadedAt: DateTime.UtcNow.AddHours(-2), userId: userId),
            CreatePdfDocument(ProcessingStatus: "completed", uploadedAt: DateTime.UtcNow.AddHours(-3), userId: userId),
            CreatePdfDocument(ProcessingStatus: "failed", uploadedAt: DateTime.UtcNow.AddHours(-4), userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(3);
        result.TotalFailed.Should().Be(1);
        result.FailureRatePercent.Should().Be(25.0);
    }

    [Fact]
    public async Task Handle_NoCompletedOrFailed_FailureRateIsZero()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Only "pending" documents — neither completed nor failed
        dbContext.Set<PdfDocumentEntity>().Add(CreatePdfDocument(
            ProcessingStatus: "pending",
            uploadedAt: DateTime.UtcNow.AddHours(-1),
            userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(0);
        result.TotalFailed.Should().Be(0);
        result.FailureRatePercent.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AllFailed_FailureRateIs100Percent()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        dbContext.Set<PdfDocumentEntity>().AddRange(
            CreatePdfDocument(ProcessingStatus: "failed", uploadedAt: DateTime.UtcNow.AddHours(-1), userId: userId),
            CreatePdfDocument(ProcessingStatus: "failed", uploadedAt: DateTime.UtcNow.AddHours(-2), userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalProcessed.Should().Be(0);
        result.TotalFailed.Should().Be(2);
        result.FailureRatePercent.Should().Be(100.0);
    }

    [Fact]
    public async Task Handle_AvgTotalDuration_SumsPhaseAverages()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfId = Guid.NewGuid();

        // Three phases: extraction=10s, chunking=5s, embedding=15s → avg total = 30s
        dbContext.Set<ProcessingMetricEntity>().AddRange(
            CreateMetric(pdfId, "extraction", durationSeconds: 10.0m, createdAt: DateTime.UtcNow.AddHours(-1)),
            CreateMetric(pdfId, "chunking", durationSeconds: 5.0m, createdAt: DateTime.UtcNow.AddHours(-1)),
            CreateMetric(pdfId, "embedding", durationSeconds: 15.0m, createdAt: DateTime.UtcNow.AddHours(-1)));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvgTotalDurationSeconds.Should().Be(30.0);
    }

    [Fact]
    public async Task Handle_FailureRateRounding_RoundsToOneDecimal()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // 2 completed, 1 failed = 33.333...% → should round to 33.3%
        dbContext.Set<PdfDocumentEntity>().AddRange(
            CreatePdfDocument(ProcessingStatus: "completed", uploadedAt: DateTime.UtcNow.AddHours(-1), userId: userId),
            CreatePdfDocument(ProcessingStatus: "completed", uploadedAt: DateTime.UtcNow.AddHours(-2), userId: userId),
            CreatePdfDocument(ProcessingStatus: "failed", uploadedAt: DateTime.UtcNow.AddHours(-3), userId: userId));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.FailureRatePercent.Should().Be(33.3);
    }

    [Theory]
    [InlineData("24h")]
    [InlineData("7d")]
    [InlineData("30d")]
    public async Task Handle_ValidPeriods_ReturnsPeriodInResult(string period)
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery(period);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Period.Should().Be(period);
    }

    [Fact]
    public async Task Handle_MultipleMetricsSameStep_CalculatesCorrectAggregates()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // 4 metrics for same step with known values: 2, 4, 6, 8
        dbContext.Set<ProcessingMetricEntity>().AddRange(
            CreateMetric(Guid.NewGuid(), "extraction", durationSeconds: 2.0m, createdAt: DateTime.UtcNow.AddHours(-1)),
            CreateMetric(Guid.NewGuid(), "extraction", durationSeconds: 4.0m, createdAt: DateTime.UtcNow.AddHours(-2)),
            CreateMetric(Guid.NewGuid(), "extraction", durationSeconds: 6.0m, createdAt: DateTime.UtcNow.AddHours(-3)),
            CreateMetric(Guid.NewGuid(), "extraction", durationSeconds: 8.0m, createdAt: DateTime.UtcNow.AddHours(-4)));

        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardMetricsQueryHandler(dbContext);
        var query = new GetDashboardMetricsQuery("24h");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var timing = result.PhaseTimings.Single();
        timing.Phase.Should().Be("extraction");
        timing.AvgDurationSeconds.Should().Be(5.0); // (2+4+6+8)/4
        timing.MinDurationSeconds.Should().Be(2.0);
        timing.MaxDurationSeconds.Should().Be(8.0);
        timing.SampleCount.Should().Be(4);
    }

    #region Helper Methods

    /// <summary>
    /// Maps legacy test status values to the authoritative ProcessingState values.
    /// Handler queries ProcessingState ("Ready"/"Failed"), not ProcessingStatus.
    /// </summary>
    private static string MapToProcessingState(string status) => status switch
    {
        "completed" => "Ready",
        "failed" => "Failed",
        "pending" => "Pending",
        _ => "Pending"
    };

    private static PdfDocumentEntity CreatePdfDocument(
        string ProcessingStatus,
        DateTime uploadedAt,
        Guid userId,
        Guid? id = null)
    {
        return new PdfDocumentEntity
        {
            Id = id ?? Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/test/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = uploadedAt,
            ProcessingStatus = ProcessingStatus,
            ProcessingState = MapToProcessingState(ProcessingStatus)
        };
    }

    private static ProcessingMetricEntity CreateMetric(
        Guid pdfDocumentId,
        string step,
        decimal durationSeconds,
        DateTime createdAt)
    {
        return new ProcessingMetricEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfDocumentId,
            Step = step,
            DurationSeconds = durationSeconds,
            PdfSizeBytes = 1024,
            PageCount = 10,
            CreatedAt = createdAt
        };
    }

    #endregion
}
