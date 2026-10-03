using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4022 / spec R3-A4 — architecture gate: <b>un servizio L1 non si salta</b>.
/// <para>
/// Per un servizio che deve esserci, un salto è un difetto mascherato. MinIO è L1 e le due suite S3
/// lo trattavano come opzionale: l'immagine sparita da Docker Hub ha prodotto un mese di gialli
/// invece di un rosso immediato (#3978). Questo gate impedisce il ritorno di quel pattern.
/// </para>
/// <para>
/// Non vieta di nominare un L1 in un motivo di salto — vieta di <i>saltare perché</i> un L1 non c'è.
/// La differenza sta nella forma: un prerequisito L1 non soddisfatto passa per
/// <see cref="L1Services.FailBecauseUnavailable"/>, che fallisce con un messaggio che distingue
/// «ambiente non pronto» da «asserzione violata».
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class L1NeverSkippedArchitectureTests
{
    /// <summary>
    /// Siti dove il nome di un L1 compare in un motivo di salto per una ragione che NON è «quel
    /// servizio non c'è». Ogni voce porta la sua ragione: un'esenzione senza motivo è un buco che
    /// nessuno sa più perché esiste.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["Integration/UploadPdfIntegrationTests.cs"] =
            "Il motivo nomina Postgres per dire che la simulazione di DB offline non è compatibile " +
            "con la fixture condivisa (#2031): il servizio C'È, è il test che non è esprimibile. " +
            "È un LIMITE, non l'assenza di un L1.",
        ["Integration/SmolDoclingIntegrationTests.cs"] =
            "Il motivo nomina i container condivisi per dire che un test di restart del servizio " +
            "non è esprimibile con container che altri test riusano. Il servizio c'è.",
        ["Helpers/E2ETestPrerequisites.cs"] =
            "In carico a #4023: è codice morto con zero chiamanti, e i suoi salti sono proprio il " +
            "difetto che quella issue corregge — sonda localhost:8080 e Qdrant :6333 (un servizio " +
            "che questo repo non ha più) e tratta ogni non-2xx come assenza. #4023 lo corregge o lo " +
            "deprecta; esentarlo qui evita di fare due volte lo stesso lavoro, ma l'esenzione DEVE " +
            "sparire con quella issue.",
    };

    [Fact]
    public void NoTestSkipsBecauseAnL1ServiceIsMissing()
    {
        var testsRoot = LocateTestsRoot();
        var offenders = new List<string>();

        // I verbi che indicano «non c'è», nelle forme in cui il repo li ha scritti davvero. Cercare
        // il solo nome del servizio darebbe falsi positivi su ogni motivo che lo nomina per contesto.
        string[] absenceWords =
        [
            "unavailable", "not available", "non raggiungibil", "non e partita", "non è partita",
            "could not start", "not running", "non risponde", "requires Docker", "require Docker",
        ];

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            var relative = Path.GetRelativePath(testsRoot, path).Replace('\\', '/');

            if (Exempt.ContainsKey(relative))
            {
                continue;
            }

            foreach (var offset in SourceScanner.FindCodeOccurrences(text, "Assert.Skip"))
            {
                var window = text.Substring(offset, Math.Min(500, text.Length - offset));
                var quoteEnd = window.IndexOf(')');
                var reason = quoteEnd > 0 ? window[..quoteEnd] : window;

                var namesAnL1 = L1Services.All.Any(s =>
                    reason.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
                var saysAbsent = absenceWords.Any(w =>
                    reason.Contains(w, StringComparison.OrdinalIgnoreCase));

                if (namesAnL1 && saysAbsent)
                {
                    offenders.Add($"{relative}: {Truncate(reason, 90)}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "un servizio L1 deve esserci, quindi la sua assenza è un GUASTO da riparare e non una " +
            "condizione da aggirare: va segnalata con L1Services.FailBecauseUnavailable(...), che " +
            "FALLISCE distinguendo «ambiente non pronto» da «asserzione violata». Un salto la " +
            $"nasconderebbe.{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", offenders));
    }

    /// <summary>
    /// Contro-prova: se i verbi di assenza non combaciassero più con come il repo li scrive, il test
    /// sopra passerebbe su zero candidati. Questo verifica che la scansione veda almeno i siti che
    /// usano la via corretta, cioè che il meccanismo sia vivo.
    /// </summary>
    [Fact]
    public void TheL1FailurePathIsActuallyUsed()
    {
        var testsRoot = LocateTestsRoot();
        var users = Directory
            .EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
            .Count(p => File.ReadAllText(p).Contains("L1Services.FailBecauseUnavailable", StringComparison.Ordinal));

        users.Should().BeGreaterThanOrEqualTo(5,
            "il 2026-10-03 sei file usavano la via del fallimento per un prerequisito L1. Se questo " +
            "conteggio crolla, qualcuno è tornato a saltare — oppure il gate principale sta " +
            "misurando il vuoto");
    }

    [Fact]
    public void EveryL1ServiceSaysHowToStartIt()
    {
        L1Services.All.Should().NotBeEmpty();

        foreach (var service in L1Services.All)
        {
            service.HowToStart.Should().NotBeNullOrWhiteSpace(
                $"'{service.Name}' è dichiarato L1, quindi chi trova un rosso deve poter leggere " +
                "come avviarlo senza cercare altrove");
            service.HowToStart.Should().Contain("make",
                $"il modo di avviare '{service.Name}' deve essere un comando eseguibile, non una " +
                "descrizione");
        }
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
}
