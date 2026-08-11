using System.Diagnostics.Metrics;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor;

/// <summary>
/// Issue #2494 AC: assert the guardrail-chain orchestrator (<see cref="MechanicOutputValidator"/>)
/// emits the two counters introduced in #2493 / PR ME-M1.3 AC-7 with the expected
/// tag shape (validator, outcome) and (rule), captured via System.Diagnostics.Metrics
/// MeterListener — same pattern as <c>GlobalKbSearchFacetMetricsTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicValidatorMetricsTests
{
    private const string InvocationsCounter = "mechanic_validator_invocations_total";
    private const string ViolationsCounter = "mechanic_validator_violations_total";

    private static MechanicGuardrailContext Ctx(string json) => new(
        MechanicSection.Mechanics,
        JsonDocument.Parse(json).RootElement,
        Array.Empty<MechanicSourceChunk>(),
        PdfPageCount: 50,
        new MechanicGuardrailOptions());

    private static MechanicOutputValidator BuildValidator(params IMechanicGuardrail[] guardrails) =>
        new(guardrails, NullLogger<MechanicOutputValidator>.Instance);

    [Fact]
    public async Task ValidateAsync_PassingGuardrail_EmitsInvocationWithPassOutcome()
    {
        using var capture = new InvocationCapture();

        // QuoteCapGuardrail on a tiny quote → passes T1.
        var validator = BuildValidator(new QuoteCapGuardrail());
        var payload = "{\"citations\":[{\"quote\":\"two words\"}]}";

        var result = await validator.ValidateAsync(Ctx(payload), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        capture.Events.Should().ContainSingle()
            .Which.Should().Be(("T1", "pass"));
    }

    [Fact]
    public async Task ValidateAsync_FailingGuardrail_EmitsInvocationFailAndViolation()
    {
        using var inv = new InvocationCapture();
        using var vio = new ViolationCapture();

        // QuoteCapGuardrail on a 26-word quote → fails T1_quote_cap.
        var validator = BuildValidator(new QuoteCapGuardrail());
        var quote = string.Join(' ', Enumerable.Range(1, 26).Select(i => "w" + i));
        var payload = "{\"citations\":[{\"quote\":\"" + quote + "\"}]}";

        var result = await validator.ValidateAsync(Ctx(payload), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        inv.Events.Should().ContainSingle()
            .Which.Should().Be(("T1", "fail"));
        vio.Events.Should().ContainSingle().Which.Should().Be("T1_quote_cap");
    }

    [Fact]
    public async Task ValidateAsync_FailFastChain_EmitsOnlyUpToFirstFailingGuardrail()
    {
        using var inv = new InvocationCapture();

        // Two guardrails: a failing T1 first, then a passing CitationPresenceGuardrail.
        // Chain order is by IMechanicGuardrail.Order — T1=10, T3a=20, so T1 runs first
        // and the chain must short-circuit after its failure (fail-fast). No event for T3.
        var validator = BuildValidator(new QuoteCapGuardrail(), new CitationPresenceGuardrail());
        var quote = string.Join(' ', Enumerable.Range(1, 26).Select(i => "w" + i));
        var payload = "{\"citations\":[{\"quote\":\"" + quote + "\"}]}";

        await validator.ValidateAsync(Ctx(payload), CancellationToken.None);

        inv.Events.Should().ContainSingle("the second guardrail must not run after a fail-fast exit")
            .Which.Should().Be(("T1", "fail"));
    }

    /// <summary>
    /// Captures mechanic_validator_invocations_total measurements as (validator, outcome) pairs.
    /// </summary>
    private sealed class InvocationCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public List<(string Validator, string Outcome)> Events { get; } = new();

        public InvocationCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == InvocationsCounter)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string validator = "<missing>";
                string outcome = "<missing>";
                foreach (var tag in tags)
                {
                    if (tag.Key == "validator" && tag.Value is string v) validator = v;
                    else if (tag.Key == "outcome" && tag.Value is string o) outcome = o;
                }
                Events.Add((validator, outcome));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    /// <summary>
    /// Captures mechanic_validator_violations_total measurements as rule names.
    /// </summary>
    private sealed class ViolationCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public List<string> Events { get; } = new();

        public ViolationCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == ViolationsCounter)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string rule = "<missing>";
                foreach (var tag in tags)
                {
                    if (tag.Key == "rule" && tag.Value is string r) rule = r;
                }
                Events.Add(rule);
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
