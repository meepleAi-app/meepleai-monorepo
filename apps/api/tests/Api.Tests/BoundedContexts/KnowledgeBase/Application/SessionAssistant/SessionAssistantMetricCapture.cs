using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Api.Observability;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// #4453: le misure di un contatore dell'assistente, per una sola versione del profilo. Un
/// <see cref="MeterListener"/> vede le misure di tutto il processo, anche quelle di altre classi di test
/// che girano in parallelo: ogni test usa una versione nuova e tiene solo le misure con quel tag.
/// </summary>
internal sealed class SessionAssistantMetricCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string?>> _measurements = new();

    public SessionAssistantMetricCapture(string instrumentName, Guid profileVersionId)
    {
        var version = profileVersionId.ToString();
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName && instrument.Name == instrumentName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var dictionary = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                dictionary[tag.Key] = tag.Value?.ToString();
            }

            if (dictionary.TryGetValue("profile_version_id", out var tagged) && tagged == version)
            {
                for (var i = 0; i < value; i++)
                {
                    _measurements.Enqueue(dictionary);
                }
            }
        });
        _listener.Start();
    }

    /// <summary>Una voce per unità contata, con i suoi tag.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string?>> Measurements => [.. _measurements];

    public void Dispose() => _listener.Dispose();
}
