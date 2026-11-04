using System.Globalization;
using Api.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

/// <summary>
/// La riga <c>host-phases</c> che le fixture con host mandano come diagnostica xUnit (#4572): la
/// fase <c>host=</c> di <c>fixture-timing</c> divisa nei suoi passi.
/// </summary>
/// <remarks>
/// <para>
/// Formato, pensato per grep: <c>host-phases &lt;Fixture&gt; clone=0.2 create=0.0 services=45.1 |
/// build=8.2 … health-check=1.0 resto=3.1</c>, secondi con un decimale. Prima della barra i passi
/// della fixture; dopo, le fasi di Program prese da <see cref="StartupPhaseTimings"/>, e
/// <c>resto</c>, cioè la parte di <c>services</c> che quelle fasi non coprono: l'avvio del server di
/// test dopo RunAsync e ciò che WebApplicationFactory fa prima di eseguire Program.
/// </para>
/// <para>
/// Se l'host non registra <see cref="StartupPhaseTimings"/>, dopo la barra c'è
/// <c>fasi-assenti</c>, e la riga resta utile per i soli passi della fixture.
/// </para>
/// </remarks>
internal static class HostStartupDiagnostics
{
    public static string Format(
        string fixture,
        TimeSpan services,
        IServiceProvider provider,
        params (string Name, TimeSpan Elapsed)[] fixtureSteps)
    {
        var steps = fixtureSteps
            .Append(("services", services))
            .Select(s => Seconds(s.Item1, s.Item2.TotalSeconds));

        var timings = provider.GetService<StartupPhaseTimings>();
        string phases;
        if (timings is null)
        {
            phases = "fasi-assenti";
        }
        else
        {
            var list = timings.Phases;
            var rest = services.TotalSeconds - list.Sum(p => p.Seconds);
            phases = string.Join(' ', list.Select(p => Seconds(p.Phase, p.Seconds)).Append(Seconds("resto", rest)));
        }

        return $"host-phases {fixture} {string.Join(' ', steps)} | {phases}";
    }

    private static string Seconds(string name, double seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}={seconds:F1}");
}
