using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #3978 / REQ-AI-TEST-001 — architecture gate: <b>ogni test factory che costruisce un host deve
/// installare la guardia fail-closed sugli host AI a pagamento</b>.
/// <para>
/// <c>PaidAiHostGuardHandler</c> blocca a livello HTTP, prima della rete, ogni richiesta verso
/// openrouter.ai, deepseek.com, openai.com e anthropic.com — anche con una chiave vera configurata.
/// Ma vive nelle factory, non nel prodotto: una factory che non lo installa lascia uscire la
/// richiesta dalla macchina, e il divieto vale per fortuna invece che per costruzione.
/// </para>
/// <para>
/// Misurato il 2026-10-02, prima di questo gate: <b>due</b> delle <b>otto</b> superfici che
/// costruiscono un host installavano la guardia. Quattro delle sei scoperte mitigavano iniettando
/// una chiave finta in configurazione — che NON impedisce la chiamata: la richiesta parte e muore
/// con 401 lato provider, cioe' esce comunque dalla macchina. Due non avevano nemmeno quello.
/// </para>
/// <para>
/// Perche' per scansione dei sorgenti e non per riflessione: la chiamata e' un'istruzione dentro un
/// corpo di metodo (<c>ConfigureWebHost</c>), quindi <c>GetCustomAttributesData()</c> non la vede —
/// lo stesso limite che rende inapplicabile il modello di <c>TestCategoryGateArchitectureTests</c> a
/// questo tipo di invariante. Il modello riusato qui e'
/// <c>EgressHttpClientPinArchitectureTests</c>, che scansiona il testo.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class PaidAiGuardCoverageArchitectureTests
{
    /// <summary>
    /// Le due forme accettate. <c>AddFailClosedAiTestDoubles</c> include la guardia piu' il fake di
    /// <c>ILlmService</c>; <c>AddPaidAiHostGuard</c> installa la sola guardia HTTP, che e' quanto
    /// basta a questo invariante — una factory che ha bisogno di un <c>ILlmService</c> funzionante
    /// non deve essere costretta a sostituirlo per ottenere la protezione di rete.
    /// </summary>
    private static readonly string[] AcceptedInstallers =
    [
        "AddPaidAiHostGuard",
        "AddFailClosedAiTestDoubles",
    ];

    [Fact]
    public void EveryTestHostFactory_InstallsThePaidAiGuard()
    {
        var testsRoot = LocateTestsRoot();
        var offenders = new List<string>();
        var covered = new List<string>();

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);

            // Una superficie e' un file che dichiara una classe derivata da WebApplicationFactory<Program>.
            // Non basta citarlo: la dichiarazione ha la forma ": WebApplicationFactory<Program>" oppure
            // ": WebApplicationFactory<Program>, IAsyncLifetime" — cercare il tipo nudo matcherebbe anche
            // un campo o un using.
            if (!text.Contains(": WebApplicationFactory<Program>", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Path.GetFileName(path);

            if (AcceptedInstallers.Any(i => text.Contains(i, StringComparison.Ordinal)))
            {
                covered.Add(name);
            }
            else
            {
                offenders.Add(name);
            }
        }

        covered.Should().NotBeEmpty(
            "il gate deve trovare almeno una factory conforme, altrimenti sta scansionando il posto " +
            "sbagliato e passerebbe verde senza misurare niente");

        offenders.Should().BeEmpty(
            "ogni factory che costruisce un host deve installare la guardia AI a pagamento " +
            $"({string.Join(" oppure ", AcceptedInstallers)}). Senza, una richiesta verso un provider " +
            "a consumo esce dalla macchina e muore con 401 lato provider invece di essere bloccata " +
            "prima della rete. Aggiungi in ConfigureWebHost: " +
            "builder.ConfigureServices(services => services.AddPaidAiHostGuard());");
    }

    /// <summary>
    /// Contro-prova del gate stesso: se il marcatore di superficie non trovasse nulla, il test sopra
    /// passerebbe vacuamente. Questo fissa il numero minimo di superfici conosciute, cosi' una
    /// modifica che rompesse la scansione si manifesta come un fallimento qui invece che come un
    /// verde silenzioso. Il limite inferiore e' deliberatamente lasco: cresce solo se qualcuno
    /// cancella factory, e in quel caso va aggiornato con la ragione.
    /// </summary>
    [Fact]
    public void TheScanActuallyFindsTheKnownSurfaces()
    {
        var testsRoot = LocateTestsRoot();

        var surfaces = Directory
            .EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
            .Count(p => File.ReadAllText(p).Contains(": WebApplicationFactory<Program>", StringComparison.Ordinal));

        surfaces.Should().BeGreaterThanOrEqualTo(7,
            "il 2026-10-02 le superfici erano 7 (piu' l'helper IntegrationWebApplicationFactory). " +
            "Se questo conteggio crolla, il marcatore di scansione non combacia piu' e il gate " +
            "principale sta passando senza guardare niente");
    }

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
