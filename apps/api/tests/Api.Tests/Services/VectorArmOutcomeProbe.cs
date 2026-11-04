using System.Diagnostics.Metrics;

namespace Api.Tests.Services;

/// <summary>
/// Ascolta il contatore reale <c>meepleai.rag.vector_arm.outcomes</c> (#3786) e somma per valore del
/// tag <c>outcome</c>.
/// </summary>
/// <remarks>
/// Il <c>Meter</c> è di processo: chi lo usa sta nella collection <c>VectorArmMetrics</c>, che gira
/// da sola (vedi <see cref="VectorArmMetricsCollection"/>).
/// </remarks>
internal sealed class VectorArmOutcomeProbe : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Dictionary<string, long> _byOutcome = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public VectorArmOutcomeProbe()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Name, "meepleai.rag.vector_arm.outcomes", StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            var outcome = "?";
            foreach (var tag in tags)
            {
                if (string.Equals(tag.Key, "outcome", StringComparison.Ordinal))
                {
                    outcome = tag.Value?.ToString() ?? "?";
                }
            }

            lock (_gate)
            {
                _byOutcome[outcome] = _byOutcome.GetValueOrDefault(outcome) + measurement;
            }
        });
        _listener.Start();
    }

    public long Count(string outcome)
    {
        lock (_gate)
        {
            return _byOutcome.GetValueOrDefault(outcome);
        }
    }

    public long Total
    {
        get { lock (_gate) { return _byOutcome.Values.Sum(); } }
    }

    public void Dispose() => _listener.Dispose();
}
