using Api.BoundedContexts.Administration.Application.Queries.QualityReports;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for WeeklyEvaluationService.
/// BGAI-042: Weekly automated quality evaluation job.
/// </summary>
public class WeeklyEvaluationServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<WeeklyEvaluationService>> _loggerMock;
    private readonly WeeklyEvaluationConfiguration _config;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CancellationTokenSource _cts;

    public WeeklyEvaluationServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<WeeklyEvaluationService>>();
        _timeProviderMock = new Mock<TimeProvider>();
        _cts = new CancellationTokenSource();

        // Default configuration
        _config = new WeeklyEvaluationConfiguration
        {
            Enabled = true,
            IntervalDays = 7,
            InitialDelayMinutes = 0.001, // Very short delay for tests
            ReportWindowDays = 7,
            EnableRagEvaluation = false
        };

        // Setup service provider chain
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMediator))).Returns(_mediatorMock.Object);

        // Setup time provider
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
    }

    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotRun()
    {
        // Arrange
        _config.Enabled = false;
        var options = Options.Create(_config);
        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        var executeTask = service.StartAsync(_cts.Token);
        await Task.Delay(100); // Give it time to potentially run
        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidIntervalDays_DoesNotRun()
    {
        // Arrange
        _config.IntervalDays = 0;
        var options = Options.Create(_config);
        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(100);
        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidReportWindowDays_DoesNotRun()
    {
        // Arrange
        _config.ReportWindowDays = -1;
        var options = Options.Create(_config);
        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(100);
        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesQualityReport_Successfully()
    {
        // Arrange
        var options = Options.Create(_config);
        var expectedReport = new QualityReport
        {
            StartDate = new DateTime(2025, 1, 8, 12, 0, 0),
            EndDate = new DateTime(2025, 1, 15, 12, 0, 0),
            TotalResponses = 100,
            LowQualityCount = 5,
            LowQualityPercentage = 5.0,
            AverageOverallConfidence = 0.85,
            AverageRagConfidence = 0.82,
            AverageLlmConfidence = 0.88,
            AverageCitationQuality = 0.90
        };

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(200); // Wait for initial delay + execution
        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<GenerateQualityReportQuery>(q =>
                    q.StartDate.Date == new DateTime(2025, 1, 8).Date &&
                    q.EndDate.Date == new DateTime(2025, 1, 15).Date &&
                    q.Days == 7),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidConfiguration_UsesCorrectDateRange()
    {
        // Arrange
        var options = Options.Create(_config);
        var currentTime = new DateTimeOffset(2025, 2, 20, 15, 30, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(currentTime);

        var capturedQuery = (GenerateQualityReportQuery?)null;
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<QualityReport>, CancellationToken>((q, ct) =>
            {
                capturedQuery = q as GenerateQualityReportQuery;
            })
            .ReturnsAsync(new QualityReport
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
                TotalResponses = 0,
                LowQualityCount = 0,
                LowQualityPercentage = 0
            });

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(200);
        await service.StopAsync(_cts.Token);

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Equal(new DateTime(2025, 2, 13, 15, 30, 0), capturedQuery.StartDate);
        Assert.Equal(new DateTime(2025, 2, 20, 15, 30, 0), capturedQuery.EndDate);
        Assert.Equal(7, capturedQuery.Days);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRagEvaluationDisabled_DoesNotRunRagEvaluation()
    {
        // Arrange
        _config.EnableRagEvaluation = false;
        _config.RagDatasetPath = "datasets/rag/evaluation.json";
        var options = Options.Create(_config);

        var ragServiceMock = new Mock<IRagEvaluationService>();
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IRagEvaluationService)))
            .Returns(ragServiceMock.Object);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QualityReport
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
                TotalResponses = 0,
                LowQualityCount = 0,
                LowQualityPercentage = 0
            });

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(200);
        await service.StopAsync(_cts.Token);

        // Assert
        ragServiceMock.Verify(
            x => x.LoadDatasetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesExceptionsGracefully_ContinuesExecution()
    {
        // Arrange
        var options = Options.Create(_config);
        var callCount = 0;

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Test exception");
                }
            })
            .ReturnsAsync(new QualityReport
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
                TotalResponses = 0,
                LowQualityCount = 0,
                LowQualityPercentage = 0
            });

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(200); // Wait for first execution (which throws)

        // Service should continue running despite exception
        // We can verify by checking that it didn't crash

        await service.StopAsync(_cts.Token);

        // Assert - verify exception was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error running weekly evaluation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WeeklyEvaluationService(
                null!,
                _loggerMock.Object,
                Options.Create(_config),
                _timeProviderMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WeeklyEvaluationService(
                _scopeFactoryMock.Object,
                null!,
                Options.Create(_config),
                _timeProviderMock.Object));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WeeklyEvaluationService(
                _scopeFactoryMock.Object,
                _loggerMock.Object,
                null!,
                _timeProviderMock.Object));
    }

    [Fact]
    public async Task ExecuteAsync_LogsEvaluationSummary_WithCorrectMetrics()
    {
        // Arrange
        var options = Options.Create(_config);
        var expectedReport = new QualityReport
        {
            StartDate = new DateTime(2025, 1, 8),
            EndDate = new DateTime(2025, 1, 15),
            TotalResponses = 200,
            LowQualityCount = 10,
            LowQualityPercentage = 5.0,
            AverageOverallConfidence = 0.85,
            AverageRagConfidence = 0.82,
            AverageLlmConfidence = 0.88,
            AverageCitationQuality = 0.90
        };

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProviderMock.Object);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(200);
        await service.StopAsync(_cts.Token);

        // Assert - verify summary was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Weekly Evaluation Summary")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
