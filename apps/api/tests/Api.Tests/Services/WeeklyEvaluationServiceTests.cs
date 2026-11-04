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
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for WeeklyEvaluationService.
/// BGAI-042: Weekly automated quality evaluation job.
/// </summary>
/// <remarks>
/// <para>
/// <b>I test si sincronizzano su un osservabile, non aspettano un tempo fisso.</b>
/// <c>ExecuteAsync</c> e' un <c>BackgroundService</c>: dopo <c>StartAsync</c> il lavoro prosegue
/// sul thread pool, e quanto ci mette non dipende dal test. Aspettare 100 ms e poi asserire e' una
/// scommessa che su un runner carico si perde — cosi'
/// <c>ExecuteAsync_HandlesExceptionsGracefully_ContinuesExecution</c> e' caduto in CI il
/// 2026-08-23, unico rosso su 22.571 test.
/// </para>
/// <para>
/// Il <see cref="FakeTimeProvider"/> non toglieva l'incertezza, e anzi la nascondeva: il servizio
/// usa l'orologio iniettato solo per <c>GetUtcNow</c>, mentre i suoi <c>Task.Delay</c> restano sul
/// tempo reale. Le chiamate ad <c>Advance</c> sparse in questi test non facevano scattare nulla —
/// spostavano solo l'orologio logico, al punto che un test confrontava le date «per tollerare lo
/// scostamento» che si era procurato da solo. Sono state rimosse.
/// </para>
/// <para>
/// Il precedente e' #3711: davanti a un'attesa fissa in un test, l'osservabile su cui sincronizzarsi
/// viene prima; allargare il ritardo e' la sconfitta, non il fix. Qui l'osservabile e' il mock che
/// l'asserzione gia' interroga — mediator, alert o log — e <see cref="ObservableTimeout"/> e' solo
/// il limite del caso di fallimento.
/// </para>
/// <para>
/// Restano tre attese fisse, nei test che asseriscono che il servizio <b>non</b> parte
/// (disabilitato, intervallo o finestra non validi). Li' non esiste un osservabile su cui
/// sincronizzarsi — si verifica un'assenza — e un'attesa corta puo' solo far passare il test a
/// torto, mai farlo fallire a torto: l'errore, se c'e', e' silenzioso e non intermittente.
/// </para>
/// </remarks>
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

    /// <summary>
    /// Limite superiore per il caso di fallimento, non un'attesa: sul percorso felice il segnale
    /// arriva in millisecondi e il test prosegue subito.
    /// </summary>
    private static readonly TimeSpan ObservableTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Un segnale da completare dentro un mock quando l'osservabile atteso si verifica.</summary>
    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Attende l'osservabile; scaduto il tempo fallisce con TimeoutException.</summary>
    private static Task Wait(TaskCompletionSource signal) => signal.Task.WaitAsync(ObservableTimeout);

    /// <summary>
    /// Un segnale che si completa quando il logger emette, al livello dato, un messaggio che
    /// contiene <paramref name="fragment"/>. Serve quando l'osservabile del test e' il log stesso.
    /// </summary>
    private TaskCompletionSource SignalOnLog(LogLevel level, string fragment)
    {
        var signal = NewSignal();
        _loggerMock
            .Setup(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var formatter = invocation.Arguments[4] as Delegate;
                var message = formatter?.DynamicInvoke(
                    invocation.Arguments[2], invocation.Arguments[3] as Exception) as string;
                if (message?.Contains(fragment, StringComparison.Ordinal) == true)
                {
                    signal.TrySetResult();
                }
            }));
        return signal;
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

        var mediatorCalled = new TaskCompletionSource<GenerateQualityReportQuery>();
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<QualityReport>, CancellationToken>((q, _) =>
                mediatorCalled.TrySetResult((GenerateQualityReportQuery)q))
            .ReturnsAsync(expectedReport);

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        var calledQuery = await mediatorCalled.Task.WaitAsync(ObservableTimeout);

        await _cts.CancelAsync();
        await service.StopAsync(_cts.Token);

        // Assert
        calledQuery.StartDate.Date.Should().Be(new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc).Date);
        calledQuery.EndDate.Date.Should().Be(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc).Date);
        calledQuery.Days.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidConfiguration_UsesCorrectDateRange()
    {
        // Arrange
        var options = Options.Create(_config);
        var currentTime = new DateTimeOffset(2025, 2, 20, 15, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(currentTime);

        var reportRequested = NewSignal();
        var capturedQuery = (GenerateQualityReportQuery?)null;
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<QualityReport>, CancellationToken>((q, ct) =>
            {
                capturedQuery = q as GenerateQualityReportQuery;
                reportRequested.TrySetResult();
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

        await Wait(reportRequested);

        await _cts.CancelAsync();
        await service.StopAsync(_cts.Token);

        // Assert
        // Senza il vecchio Advance l'orologio resta fermo su `currentTime`, quindi la finestra e'
        // esatta: prima si poteva confrontare solo la data, per tollerare lo scostamento che
        // l'Advance introduceva mentre credeva di far scattare il ritardo iniziale.
        capturedQuery.Should().NotBeNull();
        capturedQuery.StartDate.Should().Be(currentTime.AddDays(-7).UtcDateTime);
        capturedQuery.EndDate.Should().Be(currentTime.UtcDateTime);
        capturedQuery.Days.Should().Be(7);
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

        var reportRequested = NewSignal();
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback(() => reportRequested.TrySetResult())
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

        // L'assenza si verifica dopo un evento che DEVE essere accaduto: senza, un'attesa troppo
        // corta farebbe passare il test perche' il servizio non era ancora partito.
        await Wait(reportRequested);

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
        var errorLogged = SignalOnLog(LogLevel.Error, "Error running weekly evaluation");

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

        await Wait(errorLogged);

        // Il servizio deve restare vivo dopo l'eccezione: StopAsync lo ferma in modo pulito.
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
        var act = () =>
            new WeeklyEvaluationService(
                null!,
                _loggerMock.Object,
                Options.Create(_config),
                _timeProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act2 = () =>
            new WeeklyEvaluationService(
                _scopeFactoryMock.Object,
                null!,
                Options.Create(_config),
                _timeProvider);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act3 = () =>
            new WeeklyEvaluationService(
                _scopeFactoryMock.Object,
                _loggerMock.Object,
                null!,
                _timeProvider);
        act3.Should().Throw<ArgumentNullException>();
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

        var summaryLogged = NewSignal();
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
                    if (message.Contains("Weekly Evaluation Summary", StringComparison.Ordinal))
                    {
                        summaryLogged.TrySetResult();
                    }
                }
            }));

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        await Wait(summaryLogged);

        await service.StopAsync(_cts.Token);

        // Assert - verify summary was logged
        loggedMessages.Should().Contain(msg => msg.Contains("Weekly Evaluation Summary"));
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

        var alertSent = NewSignal();
        SendAlertCommand? capturedAlertCommand = null;
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AlertDto>, CancellationToken>((cmd, ct) =>
            {
                capturedAlertCommand = cmd as SendAlertCommand;
                alertSent.TrySetResult();
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

        await Wait(alertSent);

        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedAlertCommand.Should().NotBeNull();
        capturedAlertCommand.AlertType.Should().Be("QualityEvaluation");
        capturedAlertCommand.Severity.Should().Be("Warning");
        capturedAlertCommand.Message.Should().Contain("3 issue(s)");
        capturedAlertCommand.Metadata.Should().NotBeNull();
        capturedAlertCommand.Metadata.ContainsKey("Issues").Should().BeTrue();
        capturedAlertCommand.Metadata.ContainsKey("IssueCount").Should().BeTrue();
        capturedAlertCommand.Metadata["IssueCount"].Should().Be(3);
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

        var thresholdsPassed = SignalOnLog(LogLevel.Information, "All quality thresholds passed");

        var service = new WeeklyEvaluationService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            options,
            _timeProvider);

        // Act
        await service.StartAsync(_cts.Token);

        // L'assenza dell'alert si verifica dopo l'evento che DEVE precederla, altrimenti un'attesa
        // troppo corta farebbe passare il test per il motivo sbagliato.
        await Wait(thresholdsPassed);

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

    [Fact]
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

        var alertSent = NewSignal();
        SendAlertCommand? capturedAlertCommand = null;
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<GenerateQualityReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityReport);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AlertDto>, CancellationToken>((cmd, ct) =>
            {
                capturedAlertCommand = cmd as SendAlertCommand;
                alertSent.TrySetResult();
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

        await Wait(alertSent);

        await service.StopAsync(_cts.Token);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.IsAny<SendAlertCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedAlertCommand.Should().NotBeNull();
        capturedAlertCommand.AlertType.Should().Be("QualityEvaluation");
        capturedAlertCommand.Severity.Should().Be("Warning");
        capturedAlertCommand.Metadata.Should().NotBeNull();
        capturedAlertCommand.Metadata.ContainsKey("Issues").Should().BeTrue();

        // Metadata["Issues"] is a List<string>, so cast it properly
        var issuesList = capturedAlertCommand.Metadata["Issues"] as IEnumerable<string>;
        issuesList.Should().NotBeNull();
        issuesList.Should().Contain(issue => issue.Contains("RAG evaluation failed quality gates"));
    }
}
