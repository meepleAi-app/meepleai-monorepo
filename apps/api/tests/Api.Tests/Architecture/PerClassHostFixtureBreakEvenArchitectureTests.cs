using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #4050 — una fixture con host per classe conviene solo da DUE test in su.
///
/// <para>
/// <see cref="SharedHostPerTestDatabaseFixture"/> sposta il costo dell'host da «una volta per
/// metodo di test» a «una volta per classe». Il guadagno e' grande e misurato: sulle prime sei
/// classi convertite, 80 test sono passati da 28 m 27 s a 2 m 6 s. Ma non e' gratuito, e il
/// punto di pareggio non e' a zero.
/// </para>
/// <para>
/// I numeri, dalla diagnostica della fixture stessa (<c>fixture-timing</c> e <c>per-test-db</c>):
/// la fixture costa ~25,7s per classe — ~1,5s per creare e migrare il database template, ~24,2s
/// per costruire l'host — e poi ~0,15s per test. Prima della conversione un test costava ~21s.
/// Il pareggio e' quindi a <c>25,7 + 0,15n = 21n</c>, cioe' <b>n ≈ 1,2</b>:
/// </para>
/// <list type="bullet">
///   <item>1 test: prima ~21s, dopo ~25,9s — la conversione PEGGIORA.</item>
///   <item>2 test: prima ~42s, dopo ~26,0s.</item>
///   <item>9 test: prima ~189s, dopo ~27,1s.</item>
/// </list>
/// <para>
/// 🔴 Perche' questo e' un test e non una riga di documentazione. Durante #4050 uno script ha
/// convertito 25 classi di colpo, e otto di quelle avevano un solo metodo di test: tutte
/// compilavano, tutte passavano, e il risultato era piu' lento di prima. Un test verde non dice
/// nulla sulla direzione del cambiamento, e la convenienza di una fixture non ha nessun altro
/// posto dove manifestarsi. Senza questa asserzione, la prossima conversione di massa rifara'
/// lo stesso errore, con la stessa innocenza.
/// </para>
/// <para>
/// La regola non e' assoluta: una classe con un solo test potrebbe usare questa fixture per
/// l'isolamento che offre e non per la velocita'. In quel caso si aggiunge qui, con il motivo —
/// che e' esattamente la decisione deliberata che questo test pretende.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public sealed class PerClassHostFixtureBreakEvenArchitectureTests
{
    /// <summary>
    /// Classi con un solo test a cui la fixture serve per un motivo diverso dalla velocita'.
    /// Ogni voce porta il perche': se non c'e' un perche', non e' un'esenzione, e' una svista.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        // Vuoto per costruzione. La prima voce qui dentro deve portare un motivo scritto.
    };

    [Fact]
    public void NoSingleTestClassPaysForAPerClassHost()
    {
        var offenders = new List<string>();

        foreach (var type in typeof(PerClassHostFixtureBreakEvenArchitectureTests).Assembly.GetTypes())
        {
            if (!UsesPerClassHostFixture(type))
            {
                continue;
            }

            var testMethods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Count(m => m.GetCustomAttributes()
                    .Any(a => a is FactAttribute || a is TheoryAttribute));

            if (testMethods <= 1 && !Exempt.ContainsKey(type.Name))
            {
                offenders.Add($"{type.Name} ({testMethods} test)");
            }
        }

        offenders.Should().BeEmpty(
            "una fixture con host per classe costa ~25,7s e ne risparmia ~21 per test: sotto i " +
            "due test e' piu' lenta di cio' che sostituisce. Togli IClassFixture da queste classi, " +
            "oppure aggiungile a Exempt con il motivo per cui la usano nonostante il costo");
    }

    /// <summary>
    /// Le due fixture che costruiscono un host una volta per classe. Il conto del pareggio e' lo
    /// stesso per entrambe, perche' il termine che domina — i ~24,2s dell'host — e' lo stesso:
    /// differiscono su cosa fanno del database, non su quanto costa l'host.
    /// </summary>
    private static readonly Type[] PerClassHostFixtures =
    [
        typeof(SharedHostPerTestDatabaseFixture),
        typeof(IntegrationHostFixture),
    ];

    /// <summary>
    /// Vero se <paramref name="type"/> riceve, via <c>IClassFixture&lt;T&gt;</c>, una fixture che
    /// deriva da una di <see cref="PerClassHostFixtures"/>.
    /// </summary>
    private static bool UsesPerClassHostFixture(Type type) =>
        type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IClassFixture<>))
            .Select(i => i.GetGenericArguments()[0])
            .Any(f => PerClassHostFixtures.Any(b => b.IsAssignableFrom(f)));

    /// <summary>
    /// La misura su cui poggia la soglia non deve restare solo nel commento: se la fixture smette
    /// di esporre la diagnostica da cui viene, il numero qui sopra diventa indifendibile.
    /// </summary>
    [Fact]
    public void TheFixtureStillReportsTheTimingThisThresholdComesFrom()
    {
        var path = Path.Combine(
            LocateTestsRoot(), "Infrastructure", "SharedHostPerTestDatabaseFixture.cs");
        File.Exists(path).Should().BeTrue($"la fixture deve esistere in {path}");
        var source = File.ReadAllText(path);

        source.Should().Contain("fixture-timing",
            "la soglia di questo test viene dalla diagnostica `fixture-timing host=…`: senza di " +
            "essa nessuno puo' rimisurare il pareggio, e il numero invecchia come la prosa");
        source.Should().Contain("per-test-db",
            "il costo per test viene dalla diagnostica `per-test-db`, l'altra meta' del conto");
    }

    /// <summary>Risale alla radice del repo come fanno gli altri test di architettura qui.</summary>
    private static string LocateTestsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("il binario dei test deve stare dentro il repo meepleai-monorepo");
        return Path.Combine(dir!.FullName, "apps", "api", "tests", "Api.Tests");
    }
}
