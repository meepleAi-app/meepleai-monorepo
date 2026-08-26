using System.Diagnostics.Metrics;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Models;
using Api.Observability;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// #3817 — la strumentazione di qualita' esiste ma nessun percorso di produzione la invocava:
/// <c>RecordQualityScores</c> era chiamato solo dai test. Questi test verificano le misurazioni
/// EMESSE (via <see cref="MeterListener"/>), non i valori dell'arrange: se la chiamata sparisse
/// dall'handler devono fallire.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class LogAiRequestCommandHandlerQualityMetricsTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly RecordingMeterFactory _meterFactory;
    private readonly LogAiRequestCommandHandler _handler;

    public LogAiRequestCommandHandlerQualityMetricsTests()
    {
        _db = new MeepleAiDbContext(
            new DbContextOptionsBuilder<MeepleAiDbContext>()
                .UseInMemoryDatabase($"quality_metrics_{Guid.NewGuid()}").Options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());

        _meterFactory = new RecordingMeterFactory();

        _handler = new LogAiRequestCommandHandler(
            _db,
            Mock.Of<ILogger<LogAiRequestCommandHandler>>(),
            new QualityMetrics(_meterFactory));
    }

    [Fact]
    public async Task Handle_WithQualityScores_EmitsOneMeasurementPerDimension()
    {
        // Arrange
        var scores = new QualityScores
        {
            RagConfidence = 0.81,
            LlmConfidence = 0.72,
            CitationQuality = 0.64,
            OverallConfidence = 0.75,
            IsLowQuality = false
        };
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);

        // Assert — una misurazione per dimensione, col valore della dimensione corrispondente
        var byDimension = capture.Doubles
            .Where(m => m.Instrument == "meepleai.quality.score")
            .ToDictionary(m => m.Tag("dimension"), m => m.Value);

        byDimension.Should().HaveCount(4);
        byDimension["rag_confidence"].Should().Be(0.81);
        byDimension["llm_confidence"].Should().Be(0.72);
        byDimension["citation_quality"].Should().Be(0.64);
        byDimension["overall_confidence"].Should().Be(0.75);
    }

    [Fact]
    public async Task Handle_WithQualityScores_TagsAgentTypeAndOperationFromEndpoint()
    {
        // Arrange
        var scores = BuildScores(overall: 0.90, isLowQuality: false);
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);

        // Assert — l'endpoint identifica l'agente; "quality_tier" e' derivato dalla confidenza
        var measurement = capture.Doubles.Should().NotBeEmpty().And.Subject.First();
        measurement.Tag("agent.type").Should().Be("qa");
        measurement.Tag("operation").Should().Be("answer");
        measurement.Tag("quality_tier").Should().Be("high");
    }

    [Fact]
    public async Task Handle_StreamEndpoint_SplitsAgentTypeFromOperation()
    {
        // Arrange — "qa-stream" e' un endpoint di log, non un tipo di agente
        var scores = BuildScores(overall: 0.50, isLowQuality: true);
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("qa-stream", scores), CancellationToken.None);

        // Assert
        var measurement = capture.Doubles.Should().NotBeEmpty().And.Subject.First();
        measurement.Tag("agent.type").Should().Be("qa");
        measurement.Tag("operation").Should().Be("stream");
        measurement.Tag("quality_tier").Should().Be("low");
    }

    [Fact]
    public async Task Handle_LowQualityResponse_IncrementsCounter()
    {
        // Arrange
        var scores = BuildScores(overall: 0.42, isLowQuality: true);
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);

        // Assert
        var counter = capture.Longs
            .Where(m => m.Instrument == "meepleai.quality.low_quality_responses.total")
            .ToList();

        counter.Should().ContainSingle();
        counter[0].Value.Should().Be(1);
        counter[0].Tag("agent.type").Should().Be("qa");
    }

    [Fact]
    public async Task Handle_HighQualityResponse_DoesNotIncrementCounter()
    {
        // Arrange
        var scores = BuildScores(overall: 0.95, isLowQuality: false);
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);

        // Assert
        capture.Longs
            .Where(m => m.Instrument == "meepleai.quality.low_quality_responses.total")
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.95, "high")]   // >= 0.80
    [InlineData(0.70, "medium")] // >= 0.60 e < 0.80
    [InlineData(0.42, "low")]    // < 0.60
    public async Task Handle_ClassifiesQualityTier(double overall, string expectedTier)
    {
        // Arrange
        var scores = BuildScores(overall, isLowQuality: overall < 0.60);
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);

        // Assert — il tier e' un tag: sbagliarlo rende inservibile il raggruppamento su Grafana
        capture.Doubles.Should().OnlyContain(m => m.Tag("quality_tier") == expectedTier);
    }

    [Fact]
    public async Task Handle_MultipleRequests_EmitsOneSetPerRequest()
    {
        // Arrange — 4 risposte, una sola sotto soglia
        var responses = new[]
        {
            BuildScores(0.87, isLowQuality: false),
            BuildScores(0.72, isLowQuality: false),
            BuildScores(0.50, isLowQuality: true),
            BuildScores(0.93, isLowQuality: false)
        };
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        foreach (var scores in responses)
        {
            await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);
        }

        // Assert — 4 dimensioni x 4 richieste, e il counter solo per quella sotto soglia
        capture.Doubles.Should().HaveCount(16);
        capture.Longs.Should().ContainSingle().Which.Value.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutQualityScores_EmitsNothing()
    {
        // Arrange — explain, setup-stream e completion loggano senza punteggi di qualita'
        using var capture = MeasurementCapture.Start(_meterFactory.Meter);

        // Act
        await _handler.Handle(BuildCommand("explain", qualityScores: null), CancellationToken.None);

        // Assert — una serie assente e' informativa; una serie a zero mentirebbe (#3814)
        capture.Doubles.Should().BeEmpty();
        capture.Longs.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithQualityScores_StillPersistsTheLogRow()
    {
        // Arrange — l'emissione della metrica non deve sostituire la persistenza esistente
        var scores = BuildScores(overall: 0.75, isLowQuality: false);

        // Act
        await _handler.Handle(BuildCommand("qa", scores), CancellationToken.None);

        // Assert
        var log = await _db.AiRequestLogs.SingleAsync(TestContext.Current.CancellationToken);
        log.OverallConfidence.Should().Be(0.75);
        log.Endpoint.Should().Be("qa");
    }

    private static QualityScores BuildScores(double overall, bool isLowQuality) => new()
    {
        RagConfidence = overall,
        LlmConfidence = overall,
        CitationQuality = overall,
        OverallConfidence = overall,
        IsLowQuality = isLowQuality
    };

    private static LogAiRequestCommand BuildCommand(string endpoint, QualityScores? qualityScores) =>
        new(
            UserId: Guid.NewGuid().ToString(),
            GameId: Guid.NewGuid().ToString(),
            Endpoint: endpoint,
            Query: "quante carte pesco?",
            ResponseSnippet: "Ne peschi due.",
            LatencyMs: 120,
            QualityScores: qualityScores);

    public void Dispose()
    {
        _db.Dispose();
        _meterFactory.Dispose();
    }

    /// <summary>IMeterFactory di test che espone il Meter creato, per isolare le misurazioni.</summary>
    private sealed class RecordingMeterFactory : IMeterFactory
    {
        private Meter? _meter;

        public Meter Meter => _meter ?? throw new InvalidOperationException("Nessun Meter creato.");

        public Meter Create(MeterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _meter = new Meter(options.Name, options.Version, options.Tags, scope: this);
            return _meter;
        }

        public void Dispose() => _meter?.Dispose();
    }

    /// <summary>Cattura le misurazioni emesse da un Meter specifico.</summary>
    private sealed class MeasurementCapture : IDisposable
    {
        private readonly MeterListener _listener;

        private MeasurementCapture(MeterListener listener) => _listener = listener;

        public List<Measurement<double>> Doubles { get; } = [];

        public List<Measurement<long>> Longs { get; } = [];

        public static MeasurementCapture Start(Meter meter)
        {
            var listener = new MeterListener();
            var capture = new MeasurementCapture(listener);

            listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };

            listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                capture.Doubles.Add(new Measurement<double>(instrument.Name, value, ToDictionary(tags))));

            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                capture.Longs.Add(new Measurement<long>(instrument.Name, value, ToDictionary(tags))));

            listener.Start();
            return capture;
        }

        private static Dictionary<string, string> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                result[tag.Key] = tag.Value?.ToString() ?? string.Empty;
            }

            return result;
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed record Measurement<T>(string Instrument, T Value, Dictionary<string, string> Tags)
    {
        public string Tag(string key) => Tags.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
