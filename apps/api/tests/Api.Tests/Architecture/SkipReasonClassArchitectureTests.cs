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
        // Due esenzioni RIMOSSE, e tenute qui come nota perché il perché non si perda.
        //
        // E2E/SharedGameCatalog/AdminGameCreationJourneyE2ETests.cs — via in #4033: quel file non ha
        // più siti di salto. Il dubbio che la teneva («BGG API disabled in E2E, quindi lì un 500 può
        // essere il comportamento ATTESO») si è risolto misurando invece di decidere: nessuna delle
        // asserzioni del file ammette un 500, quindi togliere i rami non inventa un contratto nuovo,
        // fa rispettare quello già scritto.
        //
        // E2E/Infrastructure/E2EServiceProbe.cs — via in #4023: il gate l'ha dichiarata stale e
        // aveva ragione. Quel file chiama Assert.Skip con una VARIABILE, e dopo la riscrittura di
        // Decide non c'è più un letterale entro la finestra di ricerca, così il sito non viene
        // nemmeno rilevato. Vale come limite dichiarato di questo gate, non come copertura:
        // 🔴 un motivo di salto costruito in una variabile è INVISIBILE a una scansione dei
        // sorgenti. Dove serve, il prefisso va garantito da un test del costruttore del motivo —
        // per la sonda lo fa E2EServiceProbeTests.UnhealthyCheck_SkipsWithHowToEnable.
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
    /// <remarks>
    /// 🔴 Il confronto è **relativo**, non un numero fisso, e la ragione è un difetto misurato.
    /// La prima stesura pretendeva «almeno 100 siti», con il valore preso da una misura del
    /// 2026-10-03. Due giorni di lavoro che <i>rimuovono</i> salti — #4023 e #4033 — l'hanno portata
    /// a 82, e il test è diventato rosso per una riduzione <b>legittima</b>: la cosa che il lavoro
    /// doveva ottenere. Un pavimento assoluto in un test è lo stesso difetto del totale in prosa in
    /// un documento: invecchia da solo, e chi lo incontra lo alza senza chiedersi perché.
    /// <para>
    /// Quello che va sorvegliato non è il numero di salti, che deve poter scendere: è che lo
    /// <b>scanner veda ciò che c'è</b>. Quindi si confronta con un conteggio grezzo dei marcatori —
    /// un `IndexOf` che non salta né commenti né letterali — e si pretende che lo scanner ne trovi
    /// almeno la metà. Se smettesse di combaciare, i siti crollerebbero verso zero mentre i
    /// marcatori restano, e questo test lo direbbe. Se i salti scendono per lavoro fatto, i due
    /// conteggi scendono insieme e nessuno deve ritarare niente.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheScanFindsWhatARawGrepFinds()
    {
        var sites = ScanSkipSites();
        var raw = CountRawMarkers();

        raw.Should().BeGreaterThan(0,
            "senza nemmeno un marcatore nei sorgenti non c'è niente da sorvegliare, e il gate " +
            "principale passerebbe sul vuoto: controlla LocateTestsRoot() e SkipMarkers");

        sites.Should().HaveCountGreaterThanOrEqualTo(
            raw / 2,
            $"lo scanner ha trovato {sites.Count} siti dove un conteggio grezzo dei marcatori ne "
            + $"vede {raw}. Un divario così ampio non è una riduzione dei salti — quelli calano in "
            + "entrambi i conteggi — ma uno scanner che non combacia più: un formato nuovo, o un "
            + "helper che consuma il testo prima della ricerca (è esattamente così che la prima "
            + "stesura trovava 0 siti su 22 file). Il gate principale starebbe misurando il vuoto.");

        // 🔴 Il confronto qui sopra ha un limite che va detto: `raw` usa la STESSA
        // <see cref="SkipMarkers"/> dello scanner, quindi un marcatore che smette di combaciare —
        // un rinominio in xUnit — abbassa i due conteggi insieme e il test passerebbe. Serve una
        // fonte che non passi da quella lista: la RIFLESSIONE sugli attributi, che vede
        // `[Fact(Skip = …)]` senza sapere come è scritto nel sorgente.
        var reflected = CountStaticSkipsByReflection();

        reflected.Should().BeGreaterThan(0,
            "nessun [Fact(Skip)]/[Theory(Skip)] nell'assembly: o sono spariti tutti — e allora "
            + "questo confronto non serve più — o la riflessione sta guardando nel posto sbagliato");

        sites.Should().HaveCountGreaterThanOrEqualTo(
            reflected,
            $"la riflessione vede {reflected} salti statici, lo scanner dei sorgenti ne trova "
            + $"{sites.Count} in tutto (statici E a runtime). Se il totale sta sotto i soli "
            + "statici, i marcatori non combaciano più col modo in cui i salti sono scritti.");
    }

    /// <summary>
    /// I salti statici visti dalla <b>riflessione</b>, cioè senza passare da
    /// <see cref="SkipMarkers"/>.
    /// </summary>
    /// <remarks>
    /// È la fonte indipendente del confronto. La riflessione non può fare il lavoro del gate — non
    /// vede l'argomento di un <c>Assert.Skip</c> dentro un corpo di metodo, che è la ragione per cui
    /// questo gate scansiona i sorgenti — ma per gli attributi è autorevole, e basta a smascherare
    /// una lista di marcatori che non combacia più.
    /// </remarks>
    private static int CountStaticSkipsByReflection()
    {
        var count = 0;

        foreach (var type in typeof(SkipReasonClassArchitectureTests).Assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.DeclaredOnly))
            {
                foreach (var attribute in method.GetCustomAttributesData())
                {
                    var name = attribute.AttributeType.Name;
                    if (name is not ("FactAttribute" or "TheoryAttribute"))
                    {
                        continue;
                    }

                    if (attribute.NamedArguments.Any(a =>
                            string.Equals(a.MemberName, "Skip", StringComparison.Ordinal)))
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Conteggio grezzo dei marcatori: <b>include</b> commenti e letterali, deliberatamente.
    /// </summary>
    /// <remarks>
    /// È il termine di confronto di <see cref="TheScanFindsWhatARawGrepFinds"/> e deve essere
    /// ingenuo: se usasse <see cref="SourceScanner.FindCodeOccurrences"/> misurerebbe la stessa cosa
    /// dello scanner e il confronto non proverebbe nulla. Lo scarto fisiologico fra i due — gli
    /// esempi nelle documentazioni XML, che nel repo esistono — è assorbito dalla soglia a metà.
    /// </remarks>
    private static int CountRawMarkers()
    {
        var testsRoot = LocateTestsRoot();
        var total = 0;

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (var marker in SkipMarkers)
            {
                var at = 0;
                while ((at = text.IndexOf(marker, at, StringComparison.Ordinal)) >= 0)
                {
                    total++;
                    at += marker.Length;
                }
            }
        }

        return total;
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
