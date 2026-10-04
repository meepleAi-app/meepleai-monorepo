using System;
using System.Threading;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Il contratto del framework su cui poggia il timeout per chunk di
/// <c>ChatWithSessionAgentCommandHandler</c>: un <see cref="CancellationTokenSource"/> costruito con
/// un <see cref="TimeProvider"/> deve far scadere anche le deadline armate dopo, con
/// <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>, su QUELL'orologio e non sul
/// wall-clock.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 Perché è un test e non un commento. Se una versione futura di .NET facesse interpretare
/// <c>CancelAfter</c> dal timer di sistema invece che dal <c>TimeProvider</c> del costruttore, il
/// timeout per chunk tornerebbe a dipendere dal wall-clock <b>senza che niente lo dica</b>: i test
/// del handler continuerebbero a passare in locale e ricomincerebbero a fallire a caso sotto carico
/// in CI — cioè esattamente il difetto di #3601, ricomparso con la causa nascosta sotto un
/// <c>FakeTimeProvider</c> che sembra controllare l'orologio e non lo controlla.
/// </para>
/// <para>
/// La storia: #3601 era stata chiusa allargando il margine (da 120 ms a 975 ms di headroom per
/// chunk), e il commento nel test dichiarava che la mitigazione non rendeva impossibile il
/// fallimento. E' ricomparso il 2026-10-04 su una suite cresciuta da 21.365 a 23.680 test, perché il
/// carico contro cui quel test gareggia e' la suite stessa: il margine compra tempo in proporzione
/// inversa alla crescita della suite.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Area", "Reliability")]
[Trait("Issue", "3601")]
public sealed class CancellationTokenSourceTimeProviderContractTests
{
    [Fact(DisplayName = "CancelAfter su una CTS costruita con un TimeProvider usa QUELL'orologio")]
    public void CancelAfter_OnASourceBuiltWithATimeProvider_FiresOnThatClock()
    {
        var clock = new FakeTimeProvider();
        using var cts = new CancellationTokenSource(Timeout.InfiniteTimeSpan, clock);

        cts.CancelAfter(TimeSpan.FromSeconds(1));

        cts.IsCancellationRequested.Should().BeFalse(
            "nessun tempo e' passato sull'orologio finto");

        clock.Advance(TimeSpan.FromMilliseconds(999));
        cts.IsCancellationRequested.Should().BeFalse(
            "un millisecondo prima della scadenza la deadline non deve essere scattata");

        clock.Advance(TimeSpan.FromMilliseconds(1));
        cts.IsCancellationRequested.Should().BeTrue(
            "alla scadenza deve scattare — e deve essere l'avanzamento dell'orologio a farla " +
            "scattare, non il passare del tempo reale");
    }

    [Fact(DisplayName = "CancelAfter(InfiniteTimeSpan) disarma una deadline sull'orologio finto")]
    public void CancelAfterInfinite_DisarmsTheDeadline()
    {
        var clock = new FakeTimeProvider();
        using var cts = new CancellationTokenSource(Timeout.InfiniteTimeSpan, clock);

        cts.CancelAfter(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromMilliseconds(500));

        // È il riarmo fra i chunk: il handler disarma appena un chunk arriva, così il tempo speso a
        // processarlo non conta sulla deadline del chunk successivo.
        cts.CancelAfter(Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromHours(1));
        cts.IsCancellationRequested.Should().BeFalse(
            "disarmata, nessun avanzamento la fa scattare");
    }

    [Fact(DisplayName = "Un riarmo riparte dalla scadenza piena, non dal residuo")]
    public void ReArming_RestartsTheFullDeadline()
    {
        var clock = new FakeTimeProvider();
        using var cts = new CancellationTokenSource(Timeout.InfiniteTimeSpan, clock);

        // Questo e' l'invariante di T3-AC-2: la durata TOTALE supera una deadline mentre ogni
        // singolo intervallo resta sotto. Senza il riarmo, dopo 4 avanzamenti da 300 ms la somma
        // (1200 ms) avrebbe superato la deadline da 1 s e la cancellazione sarebbe scattata.
        for (var i = 0; i < 4; i++)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            clock.Advance(TimeSpan.FromMilliseconds(300));
            cts.IsCancellationRequested.Should().BeFalse(
                $"al passo {i} sono passati 300 ms da quest'armo, meno della scadenza");
        }

        cts.CancelAfter(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        cts.IsCancellationRequested.Should().BeTrue(
            "senza un chunk che la disarmi, la deadline scatta");
    }
}
