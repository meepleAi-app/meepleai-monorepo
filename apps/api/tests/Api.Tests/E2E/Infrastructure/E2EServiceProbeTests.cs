using System.Net;
using System.Text;
using Api.Tests.Architecture;
using Api.Tests.Constants;
using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Api.Tests.E2E.Infrastructure;

/// <summary>
/// #4023 — i test del meccanismo che sostituisce «un 500 significa che il servizio non c'è».
/// </summary>
/// <remarks>
/// Sono <c>Category=Unit</c> deliberatamente: la prova del comportamento deve costare secondi e
/// girare in <c>dev-fast</c> su ogni PR verso <c>main-dev</c>. Le suite E2E che usano questo
/// meccanismo girano invece solo su PR verso <c>main</c>/<c>main-staging</c>
/// (<c>backend-e2e-tests.yml</c>), quindi una PR verso <c>main-dev</c> non le esegue: senza questi
/// test, il meccanismo arriverebbe in <c>main-dev</c> senza che nessun gate lo abbia visto
/// funzionare.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class E2EServiceProbeTests
{
    [Fact]
    public async Task HealthyCheck_IsHealthy()
    {
        var client = ClientReturning(HttpStatusCode.OK, Health(("embedding", "Healthy")));

        var status = (await E2EServiceProbe.ReadAsync(client, "embedding")).Status;

        status.Should().Be(E2EServiceProbe.CheckStatus.Healthy);
    }

    /// <summary>
    /// Il caso che dà il nome a tutta la famiglia di difetti: un servizio che risponde male NON è un
    /// servizio assente. Saltare qui nasconderebbe il guasto, che è precisamente ciò che è successo
    /// con l'immagine MinIO (#3978) e con il ramo sul 500 che questo meccanismo rimpiazza.
    /// </summary>
    [Fact]
    public async Task DegradedCheck_IsDegraded_AndDoesNotSkip()
    {
        var client = ClientReturning(HttpStatusCode.OK, Health(("s3storage", "Degraded")));

        var status = (await E2EServiceProbe.ReadAsync(client, "s3storage")).Status;
        status.Should().Be(E2EServiceProbe.CheckStatus.Degraded);

        // Non deve lanciare: un degrado non è un'assenza, quindi il test prosegue e, se
        // l'asserzione cade, fallisce.
        var act = () => E2EServiceProbe.SkipUnlessHealthyAsync(
            client, E2EServiceProbe.Checks.S3Storage, "irrilevante qui");
        await act.Should().NotThrowAsync(
            "un servizio degradato deve lasciar proseguire il test: è l'unico modo perché il guasto " +
            "si manifesti come rosso invece che come giallo");
    }

    [Fact]
    public void UnhealthyCheck_SkipsWithHowToEnable()
    {
        var (outcome, message) = E2EServiceProbe.Decide(
            E2EServiceProbe.ProbeReading.Of(E2EServiceProbe.CheckStatus.Unhealthy),
            "ollama",
            "cd infra && make dev");

        outcome.Should().Be(E2EServiceProbe.ProbeOutcome.Skip,
            "un servizio non sano e un assenza prevista per una suite opzionale");
        message.Should().StartWith("PREVISTO:", "la classe del salto deve essere dichiarata");
        message.Should().Contain("cd infra && make dev",
            "uno skip che non dice cosa fare equivale a un test cancellato");
    }

    /// <summary>
    /// 🔴 Un check che non compare affatto <b>non</b> significa «questo ambiente non ha quel
    /// servizio»: significa che la sonda non può accertare nulla. Trattarlo come un'assenza è la
    /// stessa deduzione vietata di «500 ⇒ servizio giù», spostata di un livello.
    /// </summary>
    /// <remarks>
    /// Non è un'ipotesi: la prima stesura di questa classe saltava sull'assenza, e tre test di
    /// <c>ArbitroAgentE2ETests</c> hanno smesso di eseguire. Misurato confrontando due run dello
    /// stesso gate: <c>37108228227</c> (questo branch) li dava Ignorati, <c>37109312581</c>
    /// (controllo su main-dev) li dava Passati. Un difetto visibile solo incrociando due run è,
    /// operativamente, un difetto invisibile.
    /// </remarks>
    [Fact]
    public void AbsentCheck_FailsInsteadOfSkipping()
    {
        var (outcome, message) = E2EServiceProbe.Decide(
            new E2EServiceProbe.ProbeReading(
                E2EServiceProbe.CheckStatus.Absent,
                new[] { "postgres", "redis", "embedding" }),
            "orchestrator",
            "imposta OPENROUTER_API_KEY");

        outcome.Should().Be(E2EServiceProbe.ProbeOutcome.Fail,
            "saltare qui cancella il test in silenzio: l'unica cosa che l'assenza del check prova "
            + "e' che la sonda sta chiedendo di un nome che quest'host non registra");
        message.Should().Contain("DIFETTO DELLA SONDA");
        message.Should().Contain("orchestrator");
        message.Should().Contain("postgres, redis, embedding",
            "un errore che dice «non trovato» senza dire cosa c'era costringe chi legge a rifare "
            + "la misura a mano");
    }

    /// <summary>
    /// L'incidente nella sua forma originale, dalla lettura del payload fino alla decisione: se
    /// <c>/health</c> elenca dei check e quello richiesto non è fra loro, la sonda deve far
    /// fallire e dire quali c'erano.
    /// </summary>
    [Fact]
    public async Task CheckNotInThePayload_FailsAndNamesWhatWasThere()
    {
        var client = ClientReturning(
            HttpStatusCode.OK,
            Health(("postgres", "Healthy"), ("redis", "Healthy")));

        var reading = await E2EServiceProbe.ReadAsync(client, "orchestrator");

        reading.Status.Should().Be(E2EServiceProbe.CheckStatus.Absent);
        reading.PresentChecks.Should().Equal("postgres", "redis");

        var (outcome, message) = E2EServiceProbe.Decide(reading, "orchestrator", "irrilevante qui");
        outcome.Should().Be(E2EServiceProbe.ProbeOutcome.Fail);
        message.Should().Contain("postgres, redis");
    }

    /// <summary>
    /// Il buco che ha reso possibile l'incidente: <see cref="E2EServiceProbe.Checks"/> teneva i nomi
    /// allineati al prodotto, ma nessuno obbligava a <b>usarlo</b>. Le tre chiamate difettose
    /// passavano la stringa cruda <c>"orchestrator"</c>, che non è fra quelle costanti e non esiste
    /// come check di quest'app.
    /// </summary>
    [Fact]
    public void EveryProbeCallSiteUsesAChecksConstant()
    {
        var sites = ProbeCallSites();

        sites.Should().NotBeEmpty(
            "se la scansione non trova nessuna chiamata, questo gate passa senza verificare niente "
            + "e il buco torna aperto");

        foreach (var (file, argument) in sites)
        {
            argument.Should().Contain(
                "Checks.",
                $"{file} passa '{argument}' a SkipUnlessHealthyAsync: il nome del check deve venire "
                + "da E2EServiceProbe.Checks, che e' l'unico punto tenuto allineato al prodotto da "
                + $"{nameof(KnownCheckNamesExist)}. Una stringa cruda puo' nominare un check che "
                + "quest'host non registra, e allora il test non sa cosa sta aspettando");
        }
    }

    /// <summary>
    /// /health risponde <b>503</b> quando un check critico non è sano, e il corpo porta comunque
    /// l'elenco. Leggere solo lo status code qui sarebbe lo stesso errore di leggere un 500 come
    /// «servizio assente»: questo test fissa che la sonda guardi il corpo.
    /// </summary>
    [Fact]
    public async Task Status503WithABody_IsStillRead()
    {
        var client = ClientReturning(
            HttpStatusCode.ServiceUnavailable,
            Health(("embedding", "Healthy"), ("ollama", "Unhealthy")));

        var status = (await E2EServiceProbe.ReadAsync(client, "embedding")).Status;

        status.Should().Be(E2EServiceProbe.CheckStatus.Healthy,
            "il 503 riguarda un altro check: lo status complessivo non dice nulla su 'embedding'");
    }

    [Fact]
    public async Task UnreachableHealth_DoesNotClaimTheServiceIsAbsent()
    {
        var client = ClientThatThrows();

        var status = (await E2EServiceProbe.ReadAsync(client, "embedding")).Status;
        status.Should().Be(E2EServiceProbe.CheckStatus.Unreachable);

        var (outcome, message) = E2EServiceProbe.Decide(
            E2EServiceProbe.ProbeReading.Of(status), "embedding", "avvia l app");
        outcome.Should().Be(E2EServiceProbe.ProbeOutcome.Skip);
        message.Should().Contain("non è interrogabile",
            "la sonda deve dire che NON HA POTUTO accertare, non che il servizio e assente: sono " +
            "due cose diverse e inventare la seconda e la deduzione che tutto questo lavoro vieta");
    }

    [Fact]
    public async Task MalformedPayload_IsUnreachable_NotAbsent()
    {
        var client = ClientReturning(HttpStatusCode.OK, "{\"questo\":\"non e' un health payload\"}");

        var status = (await E2EServiceProbe.ReadAsync(client, "embedding")).Status;

        status.Should().Be(E2EServiceProbe.CheckStatus.Unreachable);
    }

    /// <summary>
    /// I nomi in <c>E2EServiceProbe.Checks</c> devono corrispondere a quelli che il prodotto
    /// registra. Se un nome cambia nel prodotto e qui no, la sonda degrada silenziosamente a
    /// «servizio assente» e le suite saltano per sempre — un modo elegante di cancellare dei test.
    /// </summary>
    [Fact]
    public void KnownCheckNamesExist()
    {
        var registered = File.ReadAllText(LocateHealthExtensions());

        foreach (var name in new[] { "embedding", "openrouter", "ollama", "reranker", "s3storage" })
        {
            registered.Should().Contain($"\"{name}\"",
                $"il check '{name}' è nominato da E2EServiceProbe.Checks ma non risulta registrato " +
                "in HealthCheckServiceExtensions: la sonda cercherebbe un nome che non esiste e " +
                "tradurrebbe l'assenza del CHECK in assenza del SERVIZIO");
        }
    }

    private static string Health(params (string Name, string Status)[] checks)
    {
        var items = checks.Select(c => $"{{\"name\":\"{c.Name}\",\"status\":\"{c.Status}\"}}");
        return $"{{\"status\":\"Healthy\",\"checks\":[{string.Join(",", items)}]}}";
    }

    private static HttpClient ClientReturning(HttpStatusCode code, string payload) =>
        new(new StubHandler(_ => new HttpResponseMessage(code)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        }))
        { BaseAddress = new Uri("http://localhost") };

    private static HttpClient ClientThatThrows() =>
        new(new StubHandler(_ => throw new HttpRequestException("connessione rifiutata")))
        { BaseAddress = new Uri("http://localhost") };

    /// <summary>
    /// Il secondo argomento di ogni chiamata a <c>SkipUnlessHealthyAsync</c> nelle sorgenti dei
    /// test, com'è scritto.
    /// </summary>
    /// <remarks>
    /// Usa <see cref="SourceScanner.FindCodeOccurrences"/>, che salta commenti e letterali: senza
    /// questo, la stringa di istruzioni dentro <c>E2ETestBase.AssertSuccessAsync</c> — che nomina il
    /// metodo per spiegare come usarlo — verrebbe contata come una chiamata.
    /// </remarks>
    private static List<(string File, string Argument)> ProbeCallSites()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull();
        var testsRoot = Path.Combine(dir!.FullName, "apps", "api", "tests", "Api.Tests");
        Directory.Exists(testsRoot).Should().BeTrue($"attese le sorgenti dei test in {testsRoot}");

        var sites = new List<(string, string)>();

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, "E2EServiceProbe.cs", StringComparison.Ordinal))
            {
                continue; // e' la definizione, non una chiamata
            }

            var text = File.ReadAllText(path);
            foreach (var offset in SourceScanner.FindCodeOccurrences(text, "SkipUnlessHealthyAsync("))
            {
                var open = text.IndexOf('(', offset);
                var firstComma = open < 0 ? -1 : text.IndexOf(',', open);
                if (firstComma < 0)
                {
                    continue;
                }

                var secondStart = SourceScanner.SkipTrivia(text, firstComma + 1);
                var secondEnd = text.IndexOf(',', secondStart);
                if (secondEnd > secondStart)
                {
                    sites.Add((name, text[secondStart..secondEnd].Trim()));
                }
            }
        }

        return sites;
    }

    private static string LocateHealthExtensions()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull();
        var path = Path.Combine(
            dir!.FullName, "apps", "api", "src", "Api", "Infrastructure", "Health", "Extensions",
            "HealthCheckServiceExtensions.cs");
        File.Exists(path).Should().BeTrue($"atteso il registro dei check in {path}");
        return path;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
