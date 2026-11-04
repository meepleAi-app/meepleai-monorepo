using System.Globalization;

namespace Api.Observability;

/// <summary>
/// Durate delle fasi dell'avvio di un host, da quando Program comincia a quando chiama RunAsync
/// (#4572).
/// </summary>
/// <remarks>
/// <para>
/// Ogni chiamata a <see cref="Mark"/> chiude una fase: la sua durata è il tempo trascorso dalla
/// chiamata precedente, o dalla costruzione dell'istanza per la prima. Program la crea come prima
/// istruzione, quindi la prima fase comprende la registrazione dei servizi e la costruzione del
/// container.
/// </para>
/// <para>
/// 🔴 Un'istanza per host, registrata nel container, mai uno stato statico: WebApplicationFactory
/// esegue Program più volte nello stesso processo, anche in parallelo fra collection di test, e
/// tempi statici mescolerebbero gli host.
/// </para>
/// </remarks>
internal sealed class StartupPhaseTimings
{
    private readonly Lock _gate = new();
    private readonly List<(string Phase, double Seconds)> _phases = [];
    private readonly TimeProvider _time;
    private long _last;

    public StartupPhaseTimings()
        : this(TimeProvider.System)
    {
    }

    public StartupPhaseTimings(TimeProvider time)
    {
        _time = time;
        _last = time.GetTimestamp();
    }

    /// <summary>Chiude la fase in corso dandole questo nome.</summary>
    public void Mark(string phase)
    {
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            _phases.Add((phase, _time.GetElapsedTime(_last, now).TotalSeconds));
            _last = now;
        }
    }

    /// <summary>Le fasi chiuse finora, nell'ordine in cui sono state chiuse.</summary>
    public IReadOnlyList<(string Phase, double Seconds)> Phases
    {
        get
        {
            lock (_gate)
            {
                return [.. _phases];
            }
        }
    }

    /// <summary>
    /// <c>build=12.3 seeding=40.1 …</c>: secondi con un decimale e il punto come separatore, pensato
    /// per essere cercato con grep.
    /// </summary>
    public override string ToString() =>
        string.Join(' ', Phases.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Phase}={p.Seconds:F1}")));
}
