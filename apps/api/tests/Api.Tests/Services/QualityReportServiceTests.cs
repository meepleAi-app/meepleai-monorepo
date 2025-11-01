using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Helpers;
using Api.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// BDD tests for QualityReportService background service.
/// These tests verify periodic quality report generation (TDD RED phase).
/// </summary>
public class QualityReportServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly TestTimeProvider _timeProvider;

    public QualityReportServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _timeProvider = new TestTimeProvider();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

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
        await using var db = await CreateContextAsync();

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
            mockConfiguration,
            _timeProvider);

        // Act: Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<QualityReportService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(5)
        );

        await helper.StartAsync();

        // Advance time past initial delay and trigger multiple intervals
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(50)); // Past initial delay (30ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past first interval (100ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past second interval (100ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past third interval (100ms)

        await helper.StopAsync();

        // Assert
        reportGenerationCount >= 2,
            $"Report should be generated at least twice after 400ms (actual: {reportGenerationCount})".Should().BeTrue();
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
            mockConfiguration,
            _timeProvider);

        // Act: Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<QualityReportService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(5)
        );

        await helper.StartAsync();

        // Wait 200ms (less than 500ms delay)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(200));

        await helper.StopAsync();

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
        await using var db = await CreateContextAsync();

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
            mockConfiguration,
            _timeProvider);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        report.TotalResponses.Should().Be(10);
        report.LowQualityCount.Should().Be(3);
        report.AverageRagConfidence.Should().NotBeNull();
        report.AverageRagConfidence.Value.Should().BeInRange(0.70, 0.80);
        report.AverageLlmConfidence.Should().NotBeNull();
        report.AverageLlmConfidence.Value.Should().BeInRange(0.65, 0.75);
        report.AverageCitationQuality.Should().NotBeNull();
        report.AverageCitationQuality.Value.Should().BeInRange(0.75, 0.85);
        report.AverageOverallConfidence.Should().NotBeNull();
        report.AverageOverallConfidence.Value.Should().BeInRange(0.70, 0.80);
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
        await using var db = await CreateContextAsync();

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
            mockConfiguration,
            _timeProvider);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        report.TotalResponses.Should().Be(0);
        report.LowQualityCount.Should().Be(0);
        report.AverageRagConfidence.Should().BeNull();
        report.AverageLlmConfidence.Should().BeNull();
        report.AverageCitationQuality.Should().BeNull();
        report.AverageOverallConfidence.Should().BeNull();
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
        await using var db = await CreateContextAsync();

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
            mockConfiguration,
            _timeProvider);

        // Act: Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<QualityReportService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(5)
        );

        await helper.StartAsync();

        // Advance time past initial delay and trigger multiple intervals
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(50)); // Past initial delay (30ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past first interval (100ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past second interval (100ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past third interval (100ms)

        await helper.StopAsync();

        // Assert
        scopeCreatedCount >= 2,
            $"Expected at least 2 scopes created, got {scopeCreatedCount}".Should().BeTrue();
        scopeDisposedCount.Should().Be(scopeCreatedCount);
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
            mockConfiguration,
            _timeProvider);

        // Act: Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<QualityReportService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(5)
        );

        await helper.StartAsync();

        // Let service start
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(100));

        // Assert: Stop should complete without exception
        await helper.StopAsync();
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
            mockConfiguration,
            _timeProvider);

        // Act: Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<QualityReportService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(5)
        );

        await helper.StartAsync();

        // Advance time past initial delay and trigger multiple intervals
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(50)); // Past initial delay (30ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past first interval (100ms)
        await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120)); // Past second interval (100ms)

        await helper.StopAsync();

        // Assert: Exception should be logged
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
        await using var db = await CreateContextAsync();

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
            mockConfiguration,
            _timeProvider);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        report.StartDate.Should().Be(startDate);
        report.EndDate.Should().Be(endDate);
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
        await using var db = await CreateContextAsync();

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
            mockConfiguration,
            _timeProvider);

        // Act
        var report = await reportService.GenerateReportAsync(startDate, endDate);

        // Assert
        report.LowQualityPercentage.Should().Be(30.0);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a SQLite in-memory database context for testing.
    /// </summary>
    private async Task<MeepleAiDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
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

