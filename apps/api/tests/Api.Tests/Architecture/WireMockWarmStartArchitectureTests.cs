using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4298 — architecture gate: <b>nessun test avvia WireMock senza il primo scambio</b>.
/// <para>
/// Alla prima richiesta che riceve in un processo, WireMock.Net cerca i suoi plugin caricando ogni
/// assembly della cartella dei binari. Misurato il 2026-10-13 su questa suite: 150 assembly caricati
/// dentro quella sola richiesta, che dura da 1,3 a 2,2 s su una macchina di sviluppo a riposo contro
/// i 33 ms di un Kestrel nudo nello stesso processo, e più di 5 s sul runner. Il costo lo paga una
/// volta sola il primo test che parla con WireMock, chiunque sia.
/// </para>
/// <para>
/// Quel test perde ogni scadenza reale che il codice sotto esame mette sulla chiamata. La sonda dei
/// provider rinuncia dopo 5 secondi, e <c>Probe_Provider401_Returns200WithUnauthorized</c> — primo
/// della sua classe per l'ordinamento di xUnit — riceveva <c>timeout</c> al posto di
/// <c>unauthorized</c> nella maggior parte dei run dello shard Games, mentre i fratelli passavano
/// tutti. Per mesi è stato letto come un intermittente.
/// </para>
/// <para>
/// 🔴 Perché è un gate e non una riga di commento: il difetto non sta in un test ma nella posizione.
/// Una classe nuova che avvia WireMock per conto suo eredita il costo appena l'ordinamento la mette
/// per prima, e niente nel suo codice lo dice. <see cref="WarmWireMockServer"/> fa avvenire lo
/// scambio prima di restituire il server, senza una scadenza da battere.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class WireMockWarmStartArchitectureTests
{
    /// <summary>Copre <c>Start(</c>, <c>StartWithAdminInterface(</c> e le altre fabbriche statiche.</summary>
    private const string DirectStart = "WireMockServer.Start";

    private const string Helper = "TestHelpers/WarmWireMockServer.cs";

    /// <summary>
    /// File che avviano WireMock da soli per una ragione scritta. Ogni voce porta il perché: senza,
    /// non è un'esenzione, è il difetto che questo gate esiste per fermare.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        // Vuoto per costruzione. La prima voce qui dentro deve portare un motivo scritto.
    };

    [Fact]
    public void NoTestStartsWireMockWithoutTheFirstExchange()
    {
        var testsRoot = LocateTestsRoot();
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(testsRoot, path).Replace('\\', '/');
            if (string.Equals(relative, Helper, StringComparison.Ordinal) || Exempt.ContainsKey(relative))
            {
                continue;
            }

            if (CountDirectStarts(File.ReadAllText(path)) > 0)
            {
                offenders.Add(relative);
            }
        }

        offenders.Should().BeEmpty(
            "la prima richiesta a WireMock nel processo carica ogni assembly della cartella dei " +
            "binari, e il test che la fa perde le scadenze reali del codice sotto esame (#4298). " +
            $"Avvia il server con `await {nameof(WarmWireMockServer)}.{nameof(WarmWireMockServer.StartAsync)}()`, " +
            "che fa avvenire quello scambio prima di restituirlo, oppure aggiungi il file a Exempt " +
            $"con il motivo.{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", offenders));
    }

    /// <summary>
    /// Contro-prova: se l'ago non combaciasse più con come il repo scrive quella chiamata, il test
    /// sopra passerebbe su zero candidati. L'helper è l'unico posto dove la chiamata deve restare, e
    /// qualcuno deve usarlo: altrimenti il gate misura il vuoto.
    /// </summary>
    [Fact]
    public void TheHelperIsWhereWireMockStartsAndTestsUseIt()
    {
        var testsRoot = LocateTestsRoot();
        var helperPath = Path.Combine(testsRoot, Helper);
        File.Exists(helperPath).Should().BeTrue($"l'helper deve esistere in {helperPath}");

        var helper = File.ReadAllText(helperPath);
        CountDirectStarts(helper).Should().Be(1,
            "l'helper avvia il server una volta: se la scansione non trova quella chiamata, non " +
            "troverebbe nemmeno quelle che deve vietare");

        var users = Directory
            .EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
            .Count(p => SourceScanner
                .FindCodeOccurrences(File.ReadAllText(p), $"{nameof(WarmWireMockServer)}.{nameof(WarmWireMockServer.StartAsync)}")
                .Count > 0);

        users.Should().BePositive("le classi che usano WireMock devono passare dall'helper");

        helper.Should().Contain("wiremock-first-exchange",
            "i tempi citati in questo gate si rimisurano dalla riga `wiremock-first-exchange` che " +
            "l'helper scrive nel log del job: senza, il costo torna invisibile e il numero invecchia");
    }

    /// <summary>
    /// Le chiamate dirette nel codice, esclusi commenti e letterali. Il carattere che precede non
    /// deve far parte di un identificatore: <c>WarmWireMockServer.StartAsync</c> contiene l'ago ma
    /// è la via corretta, non una chiamata diretta.
    /// </summary>
    private static int CountDirectStarts(string text) =>
        SourceScanner.FindCodeOccurrences(text, DirectStart)
            .Count(offset => offset == 0 || !(char.IsLetterOrDigit(text[offset - 1]) || text[offset - 1] == '_'));

    /// <summary>
    /// Risale alla radice del repo. In un worktree <c>.git</c> è un file e non una cartella, quindi
    /// valgono entrambi.
    /// </summary>
    private static string LocateTestsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
               && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("il binario dei test deve stare dentro il repo meepleai-monorepo");
        var testsRoot = Path.Combine(dir!.FullName, "apps", "api", "tests", "Api.Tests");
        Directory.Exists(testsRoot).Should().BeTrue($"i sorgenti dei test devono esistere in {testsRoot}");
        return testsRoot;
    }
}
