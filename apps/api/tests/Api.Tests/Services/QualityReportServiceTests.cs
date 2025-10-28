using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// BDD tests for QualityReportService background service.
/// These tests verify periodic quality report generation (TDD RED phase).
/// </summary>
public class QualityReportServiceTests
{
    /// <summary>
    /// Scenario: Report generated after configured interval
    /// Given service configured with 100ms interval
    /// When service runs for 250ms
    /// Then report should be generated at least twice
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AfterInterval_GeneratesReport()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();
        var mockConfiguration = CreateMockConfiguration(intervalMinutes: 0.001667, initialDelayMinutes: 0.0005); // 30ms initial delay

        var reportGenerationCount = 0;

        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Callback(() => reportGenerationCount++)
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Returns(db);

        var service = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(400); // Wait for initial delay (30ms) + 3+ intervals (300ms) with buffer
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.True(reportGenerationCount >= 2,
            $"Report should be generated at least twice after 400ms (actual: {reportGenerationCount})");
    }

    /// <summary>
    /// Scenario: Initial delay before first report
    /// Given service configured with 1-minute initial delay
    /// When service starts
    /// Then first report should be delayed by 1 minute
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_InitialDelay_WaitsBeforeFirstReport()
    {
        // Arrange
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockReportService = new Mock<IQualityReportService>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();
        var mockConfiguration = CreateMockConfiguration(intervalMinutes: 0.001667, initialDelayMinutes: 0.00833);

        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(IQualityReportService)))
            .Returns(mockReportService.Object);

        var service = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        using var cts = new CancellationTokenSource();

        // Act
        var startTime = DateTime.UtcNow;
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200); // Wait 200ms (less than 500ms delay)
        cts.Cancel();
        await executeTask;

        // Assert
        mockReportService.Verify(
            s => s.GenerateReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never,
            "Report should not be generated before initial delay completes");
    }

    /// <summary>
    /// Scenario: Report includes statistics
    /// Given 10 responses logged (3 low-quality, averages: RAG 0.75, LLM 0.70, Citation 0.80)
    /// When report is generated
    /// Then report should include total count, low-quality count, and averages
    /// </summary>
    [Fact]
    public async Task GenerateReportAsync_WithData_IncludesStatistics()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();
        var mockConfiguration = CreateMockConfiguration();

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Add 10 AI request logs to database
        var logs = Enumerable.Range(1, 10).Select(i => new AiRequestLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = startDate.AddDays(i / 2.0),
            RagConfidence = i <= 3 ? 0.50 : 0.80, // 3 low, 7 high
            LlmConfidence = i <= 3 ? 0.45 : 0.75,
            CitationQuality = i <= 3 ? 0.55 : 0.85,
            OverallConfidence = i <= 3 ? 0.50 : 0.80,
            IsLowQuality = i <= 3,
            // Required fields
            GameId = Guid.NewGuid().ToString(),
            Query = "Test query",
            LatencyMs = 100,
            TokenCount = 50,
            Endpoint = "qa",
            Status = "Success"
        }).ToList();

        db.AiRequestLogs.AddRange(logs);
        await db.SaveChangesAsync();

        // Setup service scope factory chain
        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Returns(db);

        var reportService = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        Assert.Equal(10, report.TotalResponses);
        Assert.Equal(3, report.LowQualityCount);
        Assert.NotNull(report.AverageRagConfidence);
        Assert.InRange(report.AverageRagConfidence.Value, 0.70, 0.80);
        Assert.NotNull(report.AverageLlmConfidence);
        Assert.InRange(report.AverageLlmConfidence.Value, 0.65, 0.75);
        Assert.NotNull(report.AverageCitationQuality);
        Assert.InRange(report.AverageCitationQuality.Value, 0.75, 0.85);
        Assert.NotNull(report.AverageOverallConfidence);
        Assert.InRange(report.AverageOverallConfidence.Value, 0.70, 0.80);
    }

    /// <summary>
    /// Scenario: Empty period handled gracefully
    /// Given no responses logged in the time period
    /// When report is generated
    /// Then report should indicate 0 responses and null averages
    /// </summary>
    [Fact]
    public async Task GenerateReportAsync_EmptyPeriod_HandlesGracefully()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();
        var mockConfiguration = CreateMockConfiguration();

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // No AI request logs added (empty database)

        // Setup service scope factory chain
        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Returns(db);

        var reportService = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        Assert.Equal(0, report.TotalResponses);
        Assert.Equal(0, report.LowQualityCount);
        Assert.Null(report.AverageRagConfidence);
        Assert.Null(report.AverageLlmConfidence);
        Assert.Null(report.AverageCitationQuality);
        Assert.Null(report.AverageOverallConfidence);
    }

    /// <summary>
    /// Scenario: IServiceScopeFactory creates scopes correctly
    /// Given service uses scoped database context
    /// When report generation is triggered
    /// Then new scope should be created and disposed
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ServiceScope_CreatesAndDisposesCorrectly()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();

        var scopeCreatedCount = 0;
        var scopeDisposedCount = 0;

        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Callback(() => scopeCreatedCount++)
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceScope
            .Setup(s => s.Dispose())
            .Callback(() => scopeDisposedCount++);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Returns(db);

        var mockConfiguration = CreateMockConfiguration(intervalMinutes: 0.001667, initialDelayMinutes: 0.0005); // 30ms initial delay

        var service = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(400); // Wait for initial delay (30ms) + 3+ intervals (300ms) with buffer
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.True(scopeCreatedCount >= 2,
            $"Expected at least 2 scopes created, got {scopeCreatedCount}");
        Assert.Equal(scopeCreatedCount, scopeDisposedCount);
    }

    /// <summary>
    /// Scenario: Graceful shutdown on cancellation
    /// Given service is running
    /// When cancellation token is triggered
    /// Then service should stop without exceptions
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ShutdownsGracefully()
    {
        // Arrange
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockReportService = new Mock<IQualityReportService>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();

        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(IQualityReportService)))
            .Returns(mockReportService.Object);

        var mockConfiguration = CreateMockConfiguration(intervalMinutes: 1);

        var service = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Let service start
        cts.Cancel();

        // Assert
        await executeTask; // Should complete without exception
        Assert.True(executeTask.IsCompleted);
    }

    /// <summary>
    /// Scenario: Exception during report generation logged
    /// Given report service throws exception
    /// When report generation is attempted
    /// Then exception should be caught and logged, service continues
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ReportServiceThrows_LogsAndContinues()
    {
        // Arrange
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();

        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Throws(new Exception("Database connection failed"));

        var mockConfiguration = CreateMockConfiguration(intervalMinutes: 0.001667, initialDelayMinutes: 0.0005); // 30ms initial delay

        var service = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(250); // Wait for initial delay (30ms) + 2+ intervals (200ms)
        cts.Cancel();

        // Assert
        await executeTask; // Should complete without propagating exception
        mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error generating scheduled quality report")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Exception should be logged");
    }

    /// <summary>
    /// Scenario: Report includes time period
    /// Given report generated for 2025-01-01 to 2025-01-07
    /// When report is created
    /// Then report should include StartDate and EndDate properties
    /// </summary>
    [Fact]
    public async Task GenerateReportAsync_WithTimePeriod_IncludesDates()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();
        var mockConfiguration = CreateMockConfiguration();

        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        // No AI request logs added (empty database)

        // Setup service scope factory chain
        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Returns(db);

        var reportService = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        Assert.Equal(startDate, report.StartDate);
        Assert.Equal(endDate, report.EndDate);
    }

    /// <summary>
    /// Scenario: Report calculates low-quality percentage
    /// Given 10 total responses with 3 low-quality
    /// When report is generated
    /// Then low-quality percentage should be 30%
    /// </summary>
    [Fact]
    public async Task GenerateReportAsync_CalculatesPercentage_ReturnsCorrectValue()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<QualityReportService>>();
        var mockConfiguration = CreateMockConfiguration();

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Add 10 AI request logs to database
        var logs = Enumerable.Range(1, 10).Select(i => new AiRequestLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = startDate.AddDays(i / 2.0),
            IsLowQuality = i <= 3,
            // Required fields
            GameId = Guid.NewGuid().ToString(),
            Query = "Test query",
            LatencyMs = 100,
            TokenCount = 50,
            Endpoint = "qa",
            Status = "Success"
        }).ToList();

        db.AiRequestLogs.AddRange(logs);
        await db.SaveChangesAsync();

        // Setup service scope factory chain
        mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockServiceScope.Object);
        mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(MeepleAiDbContext)))
            .Returns(db);

        var reportService = new QualityReportService(
            mockServiceScopeFactory.Object,
            mockLogger.Object,
            mockConfiguration);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        Assert.Equal(30.0, report.LowQualityPercentage);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a SQLite in-memory database context for testing.
    /// </summary>
    private static async Task<MeepleAiDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    /// <summary>
    /// Creates a mock IConfiguration with quality reporting settings.
    /// </summary>
    private static IConfiguration CreateMockConfiguration(
        double intervalMinutes = 60,
        double initialDelayMinutes = 1,
        double reportWindowDays = 7)
    {
        var config = new Dictionary<string, string>
        {
            ["QualityReporting:IntervalMinutes"] = intervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["QualityReporting:InitialDelayMinutes"] = initialDelayMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["QualityReporting:ReportWindowDays"] = reportWindowDays.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config!)
            .Build();
    }

    #endregion
}

