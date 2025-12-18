using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries.QualityReports;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for WeeklyEvaluationService.
/// BGAI-042: Weekly automated quality evaluation job.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class WeeklyEvaluationServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<WeeklyEvaluationService>> _loggerMock;
    private readonly WeeklyEvaluationConfiguration _config;
    private readonly FakeTimeProvider _timeProvider;
    private readonly CancellationTokenSource _cts;

    public WeeklyEvaluationServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<WeeklyEvaluationService>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
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
            _timeProvider);

        // Act
        var executeTask = service.StartAsync(_cts.Token);
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None); // Give it time to potentially run
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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);
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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);
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
            StartDate = new DateTime(2025, 1, 8, 12, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Advance fake clock past initial delay
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        // Wait for execution to complete in real time
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

        await _cts.CancelAsync();
        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<GenerateQualityReportQuery>(q =>
                    q.StartDate.Date == new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc).Date &&
                    q.EndDate.Date == new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc).Date &&
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
        _timeProvider.SetUtcNow(currentTime);

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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

        await _cts.CancelAsync();
        await service.StopAsync(_cts.Token);

        // Assert
        Assert.NotNull(capturedQuery);
        // Compare dates only to tolerate time shift from FakeTimeProvider.Advance
        Assert.Equal(new DateTime(2025, 2, 13, 0, 0, 0, DateTimeKind.Utc).Date, capturedQuery.StartDate.Date);
        Assert.Equal(new DateTime(2025, 2, 20, 0, 0, 0, DateTimeKind.Utc).Date, capturedQuery.EndDate.Date);
        Assert.Equal(7, capturedQuery.Days);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRagEvaluationDisabled_DoesNotRunRagEvaluation()
    {
        // Arrange
        _config.EnableRagEvaluation = false;
        _config.RagDatasetPath = "datasets/rag/evaluation.json";
        var options = Options.Create(_config);

        // When RAG evaluation is disabled, service won't be requested from DI
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IRagEvaluationService)))
            .Returns(null!);

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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Advance fake clock past initial delay (0.001 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        // Wait for execution to complete in real time
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

        await service.StopAsync(_cts.Token);

        // Assert
        // RAG evaluation should not be invoked when disabled
        _serviceProviderMock.Verify(
            x => x.GetService(typeof(IRagEvaluationService)),
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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Advance fake clock past initial delay (0.001 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        // Wait for first execution (which throws) to complete in real time
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

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
                _timeProvider));
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
                _timeProvider));
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
                _timeProvider));
    }

    [Fact]
    public async Task ExecuteAsync_LogsEvaluationSummary_WithCorrectMetrics()
    {
        // Arrange
        _config.InitialDelayMinutes = 0;
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

        var loggedMessages = new List<string>();
        _loggerMock
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var formatter = invocation.Arguments[4] as Delegate;
                var state = invocation.Arguments[2];
                var exception = invocation.Arguments[3] as Exception;
                var message = formatter?.DynamicInvoke(state, exception) as string;
                if (message != null)
                {
                    loggedMessages.Add(message);
                }
            }));

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Wait for execution to complete in real time
        await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None);

        await service.StopAsync(_cts.Token);

        // Assert - verify summary was logged
        Assert.Contains(loggedMessages, msg => msg.Contains("Weekly Evaluation Summary"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenQualityThresholdsBreached_SendsAlert()
    {
        // Arrange
        var options = Options.Create(_config);
        var expectedReport = new QualityReport
        {
            StartDate = new DateTime(2025, 1, 8),
            EndDate = new DateTime(2025, 1, 15),
            TotalResponses = 100,
            LowQualityCount = 15, // 15% exceeds default threshold of 10%
            LowQualityPercentage = 15.0,
            AverageOverallConfidence = 0.65, // Below default threshold of 0.70
            AverageRagConfidence = 0.60, // Below default threshold of 0.65
            AverageLlmConfidence = 0.88,
            AverageCitationQuality = 0.90
        };

        SendAlertCommand? capturedAlertCommand = null;
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AlertDto>, CancellationToken>((cmd, ct) =>
            {
                capturedAlertCommand = cmd as SendAlertCommand;
            })
            .ReturnsAsync(new AlertDto(
                Guid.NewGuid(),
                "QualityEvaluation",
                "Warning",
                "Alert sent",
                null,
                DateTime.UtcNow,
                null,
                true,
                new Dictionary<string, bool> { { "Email", true } }
            ));

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Advance fake clock past initial delay (0.001 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        // Wait for execution to complete in real time
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedAlertCommand);
        Assert.Equal("QualityEvaluation", capturedAlertCommand.AlertType);
        Assert.Equal("Warning", capturedAlertCommand.Severity);
        Assert.Contains("3 issue(s)", capturedAlertCommand.Message);
        Assert.NotNull(capturedAlertCommand.Metadata);
        Assert.True(capturedAlertCommand.Metadata.ContainsKey("Issues"));
        Assert.True(capturedAlertCommand.Metadata.ContainsKey("IssueCount"));
        Assert.Equal(3, capturedAlertCommand.Metadata["IssueCount"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllThresholdsPass_DoesNotSendAlert()
    {
        // Arrange
        var options = Options.Create(_config);
        var expectedReport = new QualityReport
        {
            StartDate = new DateTime(2025, 1, 8),
            EndDate = new DateTime(2025, 1, 15),
            TotalResponses = 100,
            LowQualityCount = 3, // 3% below default threshold of 10%
            LowQualityPercentage = 3.0,
            AverageOverallConfidence = 0.85, // Above default threshold of 0.70
            AverageRagConfidence = 0.82, // Above default threshold of 0.65
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
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Advance fake clock past initial delay (0.001 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        // Wait for execution to complete in real time
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify success log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All quality thresholds passed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact(Skip = "Issue #2185: Moq cannot proxy internal IRagEvaluationService - needs refactoring to use public interface or test fake")]
    public async Task ExecuteAsync_WhenRagQualityGatesFail_SendsAlert()
    {
        // Arrange
        _config.EnableRagEvaluation = true;
        _config.RagDatasetPath = "datasets/rag/evaluation.json";
        var options = Options.Create(_config);

        var qualityReport = new QualityReport
        {
            StartDate = new DateTime(2025, 1, 8),
            EndDate = new DateTime(2025, 1, 15),
            TotalResponses = 100,
            LowQualityCount = 3,
            LowQualityPercentage = 3.0,
            AverageOverallConfidence = 0.85,
            AverageRagConfidence = 0.82
        };

        var ragReport = new RagEvaluationReport
        {
            DatasetName = "Test Dataset",
            TotalQueries = 50,
            SuccessfulQueries = 45,
            PassedQualityGates = false, // Failed gates
            QualityGateFailures = new List<string> { "MRR below threshold", "P@5 below threshold" },
            MeanReciprocalRank = 0.45,
            AvgPrecisionAt5 = 0.50
        };

        var ragServiceMock = new Mock<IRagEvaluationService>();
        ragServiceMock
            .Setup(x => x.LoadDatasetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagEvaluationDataset { Queries = Array.Empty<RagEvaluationQuery>() });

        ragServiceMock
            .Setup(x => x.EvaluateAsync(
                It.IsAny<RagEvaluationDataset>(),
                It.IsAny<int>(),
                It.IsAny<RagQualityThresholds>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ragReport);

        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IRagEvaluationService)))
            .Returns(ragServiceMock.Object);

        SendAlertCommand? capturedAlertCommand = null;
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityReport);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AlertDto>, CancellationToken>((cmd, ct) =>
            {
                capturedAlertCommand = cmd as SendAlertCommand;
            })
            .ReturnsAsync(new AlertDto(
                Guid.NewGuid(),
                "QualityEvaluation",
                "Warning",
                "Alert sent",
                null,
                DateTime.UtcNow,
                null,
                true,
                new Dictionary<string, bool> { { "Email", true } }
            ));

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // Advance fake clock past initial delay (0.001 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.InitialDelayMinutes + 0.001));

        // Wait for execution to complete in real time
        await Task.Delay(TestConstants.Timing.SmallDelay, CancellationToken.None);

        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedAlertCommand);
        Assert.Equal("QualityEvaluation", capturedAlertCommand.AlertType);
        Assert.Equal("Warning", capturedAlertCommand.Severity);
        Assert.NotNull(capturedAlertCommand.Metadata);
        Assert.True(capturedAlertCommand.Metadata.ContainsKey("Issues"));

        // Metadata["Issues"] is a List<string>, so cast it properly
        var issuesList = capturedAlertCommand.Metadata["Issues"] as IEnumerable<string>;
        Assert.NotNull(issuesList);
        Assert.Contains(issuesList, issue => issue.Contains("RAG evaluation failed quality gates"));
    }
}
