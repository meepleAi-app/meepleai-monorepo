using System.Net;
using System.Text;
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

        var status = await E2EServiceProbe.GetCheckStatusAsync(client, "embedding");

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

        var status = await E2EServiceProbe.GetCheckStatusAsync(client, "s3storage");
        status.Should().Be(E2EServiceProbe.CheckStatus.Degraded);

        // Non deve lanciare: un degrado non è un'assenza, quindi il test prosegue e, se
        // l'asserzione cade, fallisce.
        var act = () => E2EServiceProbe.SkipUnlessHealthyAsync(client, "s3storage", "irrilevante qui");
        await act.Should().NotThrowAsync(
            "un servizio degradato deve lasciar proseguire il test: è l'unico modo perché il guasto " +
            "si manifesti come rosso invece che come giallo");
    }

    [Fact]
    public void UnhealthyCheck_SkipsWithHowToEnable()
    {
        var reason = E2EServiceProbe.BuildSkipReason(
            E2EServiceProbe.CheckStatus.Unhealthy, "ollama", "cd infra && make dev");

        reason.Should().NotBeNull("un servizio non sano e un assenza prevista per una suite opzionale");
        reason.Should().StartWith("PREVISTO:", "la classe del salto deve essere dichiarata");
        reason.Should().Contain("cd infra && make dev",
            "uno skip che non dice cosa fare equivale a un test cancellato");
    }

    /// <summary>
    /// Un check che non compare affatto significa «questo ambiente non ha quel servizio registrato»,
    /// che è diverso da «ce l'ha e sta male». Distinguerli serve a chi legge: nel primo caso manca
    /// una configurazione, nel secondo c'è qualcosa da riparare.
    /// </summary>
    [Fact]
    public void AbsentCheck_SaysItIsNotConfigured()
    {
        var reason = E2EServiceProbe.BuildSkipReason(
            E2EServiceProbe.CheckStatus.Absent, "openrouter", "imposta OPENROUTER_API_KEY");

        reason.Should().Contain("non è configurato");
        reason.Should().Contain("imposta OPENROUTER_API_KEY");
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

        var status = await E2EServiceProbe.GetCheckStatusAsync(client, "embedding");

        status.Should().Be(E2EServiceProbe.CheckStatus.Healthy,
            "il 503 riguarda un altro check: lo status complessivo non dice nulla su 'embedding'");
    }

    [Fact]
    public async Task UnreachableHealth_DoesNotClaimTheServiceIsAbsent()
    {
        var client = ClientThatThrows();

        var status = await E2EServiceProbe.GetCheckStatusAsync(client, "embedding");
        status.Should().Be(E2EServiceProbe.CheckStatus.Unreachable);

        var reason = E2EServiceProbe.BuildSkipReason(status, "embedding", "avvia l app");
        reason.Should().Contain("non è interrogabile",
            "la sonda deve dire che NON HA POTUTO accertare, non che il servizio e assente: sono " +
            "due cose diverse e inventare la seconda e la deduzione che tutto questo lavoro vieta");
    }

    [Fact]
    public async Task MalformedPayload_IsUnreachable_NotAbsent()
    {
        var client = ClientReturning(HttpStatusCode.OK, "{\"questo\":\"non e' un health payload\"}");

        var status = await E2EServiceProbe.GetCheckStatusAsync(client, "embedding");

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
