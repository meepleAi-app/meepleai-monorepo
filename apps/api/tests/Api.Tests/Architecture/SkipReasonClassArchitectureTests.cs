using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4021 / spec R1-A1 — architecture gate: <b>il motivo di ogni salto dichiara se qualcuno deve
/// agire</b>.
/// <para>
/// Un salto dice che un test non è stato eseguito. Non dice la cosa che serve a chi legge il report:
/// <i>devo fare qualcosa?</i> Senza quella informazione un guasto dell'ambiente e un'assenza prevista
/// hanno lo stesso aspetto — ed è così che l'immagine MinIO sparita ha prodotto un mese di gialli
/// invece di un rosso immediato (#3978).
/// </para>
/// <para>Le quattro classi, e chi agisce:</para>
/// <list type="table">
///   <item><term>PREVISTO</term><description>assenza normale di un servizio opzionale — nessuno
///     agisce. Il motivo deve dire COME abilitare il test.</description></item>
///   <item><term>GUASTO</term><description>un servizio che dovrebbe esserci non è sano — si agisce
///     subito. Il motivo riporta l'errore osservato, non la condizione generica.</description></item>
///   <item><term>DIFETTO</term><description>il prodotto è rotto e il test è corretto — il motivo
///     porta il numero della issue.</description></item>
///   <item><term>LIMITE</term><description>il test non è esprimibile sotto questo harness — il motivo
///     nomina la limitazione e dove il test andrebbe spostato.</description></item>
/// </list>
/// <para>
/// La quarta classe esiste perché una tassonomia a tre non era esaustiva sulla popolazione reale: dei
/// salti statici, diciassette dicono «EF Core InMemory provider cannot translate…», che non è
/// un'assenza, non è un guasto e non è un bug di prodotto.
/// </para>
/// <para>
/// Scansiona i SORGENTI tramite <see cref="SourceScanner"/>, non la riflessione: il motivo di un
/// <c>Assert.Skip</c> è un argomento dentro un corpo di metodo, dove
/// <c>GetCustomAttributesData()</c> non arriva.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SkipReasonClassArchitectureTests
{
    private static readonly string[] Classes = ["PREVISTO:", "GUASTO:", "DIFETTO:", "LIMITE:"];

    /// <summary>
    /// I marcatori che introducono un salto. <c>Assert.SkipUnless</c> e <c>Assert.SkipWhen</c> portano
    /// la condizione come primo argomento e il motivo come secondo; gli altri hanno il motivo per
    /// primo. Il gate non distingue i due casi: pretende che <b>il primo letterale stringa</b>
    /// dell'istruzione cominci con una classe, e per entrambe le forme quel letterale è il motivo.
    /// </summary>
    private static readonly string[] SkipMarkers =
    [
        "Assert.Skip(",
        "Assert.SkipUnless(",
        "Assert.SkipWhen(",
        "[Fact(Skip",
        "[Theory(Skip",
    ];

    /// <summary>
    /// Siti non ancora classificati, ciascuno con la ragione per cui è qui. Un numero non basta: una
    /// voce senza motivo è un debito che nessuno sa più perché esiste.
    /// <para>
    /// 🔴 Questo elenco deve SCENDERE. Ogni voce che resta è un salto su cui chi legge il report non
    /// sa se deve agire.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["E2E/SharedGameCatalog/AdminGameCreationJourneyE2ETests.cs"] =
            "In conversione in #4023 (T2): i motivi deducono l'assenza del servizio da una risposta " +
            "ricevuta («returned 500 — service likely unavailable»), che è il difetto che #4023 " +
            "corregge. Classificarli adesso significherebbe rifare il lavoro due volte.",
        ["E2E/KnowledgeBase/ChatE2ETests.cs"] =
            "In conversione in #4023 (T2), stessa causa: i motivi deducono l'assenza del servizio " +
            "dal codice di risposta ricevuto.",
        ["E2E/KnowledgeBase/ArbitroAgentE2ETests.cs"] =
            "In conversione in #4023 (T2), stessa causa: l'assenza è dedotta da una risposta.",
        ["E2E/SharedGameCatalog/ShareRequestE2ETests.cs"] =
            "In conversione in #4023 (T2), stessa causa: l'assenza è dedotta da una risposta.",
        ["E2E/UserNotifications/NotificationsE2ETests.cs"] =
            "In conversione in #4023 (T2), stessa causa: l'assenza è dedotta da una risposta.",
        ["E2E/UserLibrary/UserLibraryE2ETests.cs"] =
            "In conversione in #4023 (T2), stessa causa: l'assenza è dedotta da una risposta.",
        ["E2E/DocumentProcessing/DocumentProcessingE2ETests.cs"] =
            "In conversione in #4023 (T2), stessa causa: l'assenza è dedotta da una risposta.",
        ["E2E/Infrastructure/E2ETestBase.cs"] =
            "In conversione in #4023 (T2): è la base condivisa degli otto file, quindi va convertita " +
            "insieme a loro e non prima, o le suite figlie si troverebbero due comportamenti.",
        ["Helpers/E2ETestPrerequisites.cs"] =
            "Codice morto con zero chiamanti, e implementa il difetto che #4023 corregge: sonda " +
            "localhost:8080 e Qdrant :6333 (servizio che questo repo non ha più) e tratta ogni " +
            "non-2xx come assenza. #4023 lo corregge o lo deprecta; classificarne i motivi " +
            "prolungherebbe la vita di un helper da rimuovere.",
        ["Integration/PdfExtractionRealBackendValidationTests.cs"] =
            "I motivi uniscono due cause con un OR («Unstructured service not available OR gold " +
            "standard missing»), quindi non sono classificabili senza prima separarle: la prima è " +
            "PREVISTO, la seconda è un difetto del setup. Separazione in #4023.",
    };

    [Fact]
    public void EverySkipReason_DeclaresWhoMustAct()
    {
        var sites = ScanSkipSites();
        var offenders = sites
            .Where(s => !IsExempt(s.RelativePath))
            .Where(s => !Classes.Any(c => s.Reason.StartsWith(c, StringComparison.Ordinal)))
            .ToList();

        offenders.Should().BeEmpty(
            "il motivo di un salto deve cominciare con una delle quattro classi " +
            $"({string.Join(" ", Classes)}) perché chi legge il report sappia se deve agire. " +
            "Un sito che non può essere classificato ora va in Exempt CON LA SUA RAGIONE. " +
            "Siti non conformi:\n  " +
            string.Join("\n  ", offenders.Select(o => $"{o.RelativePath}: {Truncate(o.Reason, 70)}")));
    }

    /// <summary>
    /// Contro-prova del gate: se il marcatore di scansione smettesse di combaciare — un rinominio in
    /// xUnit, un formato diverso — il test sopra passerebbe vacuamente su zero siti. Questo fissa il
    /// limite inferiore della popolazione nota, così la rottura della scansione si manifesta come un
    /// fallimento invece che come un verde silenzioso.
    /// </summary>
    [Fact]
    public void TheScanFindsTheKnownPopulation()
    {
        var sites = ScanSkipSites();

        sites.Should().HaveCountGreaterThanOrEqualTo(100,
            "il 2026-10-03 i siti di salto erano 93 a runtime più 45 statici. Se questo conteggio " +
            "crolla, il gate principale sta misurando il vuoto: controlla SkipMarkers contro " +
            "`grep -rhoE 'Assert\\.Skip|\\[(Fact|Theory)\\(Skip' --include=*.cs apps/api/tests/`");

        sites.Select(s => s.RelativePath).Distinct().Should().HaveCountGreaterThanOrEqualTo(20,
            "i siti erano distribuiti su 23 file a runtime più quelli statici");
    }

    /// <summary>
    /// Ogni voce di <see cref="Exempt"/> deve corrispondere a un file che esiste e che contiene almeno
    /// un salto. Un'esenzione per un file cancellato o ripulito è debito fantasma: resta scritta,
    /// nessuno la rimuove, e fa credere che ci sia lavoro dove non ce n'è più.
    /// </summary>
    [Fact]
    public void EveryExemptionStillAppliesToSomething()
    {
        var scanned = ScanSkipSites().Select(s => s.RelativePath).ToHashSet(StringComparer.Ordinal);
        var stale = Exempt.Keys.Where(k => !scanned.Contains(k)).ToList();

        stale.Should().BeEmpty(
            "un'esenzione che non corrisponde più a nessun sito va RIMOSSA, non lasciata: " +
            "altrimenti l'elenco misura il passato. Voci da togliere: " + string.Join(", ", stale));

        foreach (var (path, reason) in Exempt)
        {
            reason.Should().NotBeNullOrWhiteSpace($"l'esenzione di {path} deve dire PERCHÉ");
            reason.Length.Should().BeGreaterThan(30,
                $"l'esenzione di {path} deve spiegare, non etichettare: «{reason}» è troppo corta " +
                "per dire a chi legge cosa aspettarsi e chi ci sta lavorando");
        }
    }

    private static bool IsExempt(string relativePath) =>
        Exempt.ContainsKey(relativePath);

    private static List<SkipSite> ScanSkipSites()
    {
        var testsRoot = LocateTestsRoot();
        var sites = new List<SkipSite>();

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            var relative = Path.GetRelativePath(testsRoot, path).Replace('\\', '/');

            foreach (var marker in SkipMarkers)
            {
                // FindCodeOccurrences salta commenti e letterali: una menzione di «Assert.Skip» dentro
                // una documentazione XML non conta come un sito. Senza questa proprietà il gate
                // conterebbe gli esempi nei commenti — ce ne sono, e uno è proprio in
                // TesseractOcrServiceTests, che documenta la convenzione dello skip osservabile.
                foreach (var offset in SourceScanner.FindCodeOccurrences(text, marker))
                {
                    // 🔴 NON usare SourceScanner.ReadStatement qui. Quel metodo salta i letterali
                    // (è fatto per analizzare la FORMA del codice, come il gate egress che cerca un
                    // nome di metodo), quindi restituisce l'istruzione PRIVATA delle stringhe — e il
                    // motivo di un salto È una stringa. Primo esito di questo gate prima della
                    // correzione: 0 siti trovati su 22 file che contengono `Assert.Skip(`, con il
                    // test principale che passava vacuamente. L'ha scoperto la contro-prova qui
                    // sotto, che esiste per questo.
                    var reason = ResolveReason(text, offset);
                    if (reason is not null)
                    {
                        sites.Add(new SkipSite(relative, reason));
                    }
                }
            }
        }

        return sites;
    }

    /// <summary>
    /// Il motivo del salto, che può essere un letterale inline oppure il nome di una costante.
    /// </summary>
    /// <remarks>
    /// La seconda forma esiste nel repo e il gate la sbagliava: <c>[Fact(Skip = SkipReason)]</c> in
    /// <c>BggRateLimitIntegrationTests</c> dichiara il motivo una volta come <c>const string</c> e lo
    /// riusa su quattro fatti. Cercando «il primo letterale dopo il marcatore» il gate pescava
    /// <c>"Free"</c>, <c>"Normal"</c>, <c>"Premium"</c> — stringhe di righe successive, del tutto
    /// estranee al salto. Attribuire a un sito il motivo di un altro è peggio di non trovarlo: il
    /// gate avrebbe chiesto di classificare un testo che non c'entra nulla.
    /// </remarks>
    private static string? ResolveReason(string text, int offset)
    {
        // `Skip = Identificatore` (nessuna virgoletta prima della parentesi di chiusura o della
        // virgola): il motivo è una costante dichiarata altrove nello stesso file.
        var eq = text.IndexOf('=', offset);
        if (eq > 0 && eq - offset < 24)
        {
            var i = eq + 1;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i < text.Length && (char.IsLetter(text[i]) || text[i] == '_'))
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                var identifier = text[start..i];
                var declaration = text.IndexOf($"{identifier} =", StringComparison.Ordinal);
                if (declaration > 0)
                {
                    return FirstStringLiteralAfter(text, declaration);
                }

                // Costante dichiarata fuori da questo file: il gate non la risolve, e lo dice invece
                // di indovinare un motivo sbagliato.
                return $"<costante non risolta: {identifier}>";
            }
        }

        return FirstStringLiteralAfter(text, offset);
    }

    /// <summary>
    /// Il primo letterale stringa che segue l'offset del marcatore, che per tutte le forme di salto in
    /// uso È il motivo: <c>Assert.Skip("…")</c> lo ha subito, <c>Assert.SkipUnless(cond, "…")</c> lo
    /// ha come primo letterale dopo una condizione che non ne contiene, e <c>[Fact(Skip = "…")]</c>
    /// idem. Regge su un motivo interpolato (<c>$"GUASTO: {err}"</c>) e su una concatenazione, dove il
    /// prefisso sta nel primo pezzo.
    /// </summary>
    /// <remarks>
    /// Lavora sul testo GREZZO, non sul risultato di <c>ReadStatement</c>: vedi il commento nel
    /// chiamante. La finestra di ricerca è limitata perché un marcatore senza letterale vicino non è
    /// un salto con motivo — e se la ricerca fosse illimitata prenderebbe la stringa di un'istruzione
    /// successiva, attribuendo a un sito il motivo di un altro.
    /// </remarks>
    private static string? FirstStringLiteralAfter(string text, int offset)
    {
        const int window = 600;
        var limit = Math.Min(text.Length, offset + window);

        for (var i = offset; i < limit; i++)
        {
            if (text[i] != '"')
            {
                continue;
            }

            // Un verbatim (@"…") raddoppia le virgolette per scaparle; il resto usa il backslash.
            var verbatim = i > 0 && text[i - 1] == '@';
            var j = i + 1;

            while (j < limit)
            {
                if (!verbatim && text[j] == '\\')
                {
                    j += 2;
                    continue;
                }

                if (text[j] == '"')
                {
                    if (verbatim && j + 1 < limit && text[j + 1] == '"')
                    {
                        j += 2;
                        continue;
                    }

                    return text.Substring(i + 1, j - i - 1);
                }

                j++;
            }

            return null;
        }

        return null;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");

    private static string LocateTestsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var testsRoot = Path.Combine(dir!.FullName, "apps", "api", "tests", "Api.Tests");
        Directory.Exists(testsRoot).Should().BeTrue($"test sources must exist at {testsRoot}");
        return testsRoot;
    }

    private sealed record SkipSite(string RelativePath, string Reason);
}
