using System.Diagnostics.Metrics;

namespace Api.Tests.Observability;

/// <summary>
/// Cattura le misurazioni di uno strumento durante l'esecuzione di <paramref name="act"/>.
/// Estratto da <c>EgressMetricsTests</c> in #3583 perché i test di <c>decode_fail</c> /
/// <c>denylist_hit</c> vivono nei file dei rispettivi handler e validator.
/// <para>
/// ATTENZIONE: il <see cref="MeterListener"/> è process-wide e xUnit esegue le classi di test in
/// parallelo, per cui la finestra di cattura può contenere misurazioni emesse da altri test. Chi
/// asserisce DEVE selezionare la propria misurazione per tag e limitarsi a verificarne la presenza,
/// mai il conteggio esatto (regressione già occorsa in #3495 Slice D).
/// </para>
/// </summary>
internal static class MetricCapture
{
    public static List<(long Value, IReadOnlyDictionary<string, object?> Tags)> Capture(
        string instrumentName, Action act)
    {
        var captured = new List<(long, IReadOnlyDictionary<string, object?>)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var t in tags)
            {
                dict[t.Key] = t.Value;
            }
            captured.Add((value, dict));
        });
        listener.Start();

        act();

        return captured;
    }
}
