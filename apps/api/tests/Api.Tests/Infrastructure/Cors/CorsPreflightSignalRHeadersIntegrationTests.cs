using System.Net.Http;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Cors;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>Il database qui non viene nemmeno toccato: una richiesta <c>OPTIONS</c> di preflight si
/// ferma nel middleware CORS e non raggiunge alcun handler. La fixture serve solo perche' questi
/// test devono attraversare la <b>pipeline reale</b> — e' esattamente il punto: la policy e'
/// dichiarata inline in <c>Program.cs</c>, quindi un test che ne ricostruisse una copia
/// verificherebbe la copia.</para>
/// </summary>
public sealed class CorsPreflightHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "cors_preflight");

/// <summary>
/// Issue #4059 — la policy CORS <c>"web"</c> deve ammettere i due header che il client JS di
/// SignalR aggiunge alla richiesta di negotiate.
///
/// <para><b>Il difetto che questi test fissano.</b> L'allowlist di #1448 (deliberatamente non
/// <c>AllowAnyHeader()</c>) non conteneva <c>X-Requested-With</c> ne'
/// <c>X-SignalR-User-Agent</c>. Il preflight falliva, Chrome rifiutava la richiesta prima di
/// inviarla, e <c>/sessions/{id}/live</c> non riceveva un solo evento in tempo reale — con un
/// sintomo quasi invisibile, perche' il <c>catch</c> del hook registra solo
/// <c>setConnected(false)</c>.</para>
///
/// <para>⚠️ <b>Perche' nessuna sonda l'aveva visto.</b> <c>curl</c> e un <c>fetch</c> scritto a
/// mano non mandano quegli header: rispondevano <b>200 sullo stesso negotiate</b>. Il fallimento
/// e' comparso solo iniettando <c>@microsoft/signalr</c> in una pagina reale. Da qui la forma di
/// questi test: la richiesta dichiara gli header veri in
/// <c>Access-Control-Request-Headers</c>, cioe' fa quello che fa il client.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Concern", "Security")]
[Trait("Issue", "4059")]
public sealed class CorsPreflightSignalRHeadersIntegrationTests
    : IClassFixture<CorsPreflightHostFixture>
{
    private const string Origin = "http://localhost:3000";
    private const string HubNegotiatePath = "/hubs/gamestate/negotiate?negotiateVersion=1";

    private readonly HttpClient _client;

    public CorsPreflightSignalRHeadersIntegrationTests(CorsPreflightHostFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _client = fixture.Client;
    }

    private async Task<HttpResponseMessage> PreflightAsync(string requestedHeaders, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", requestedHeaders);
        return await _client.SendAsync(request).ConfigureAwait(true);
    }

    private static IReadOnlyCollection<string> AllowedHeaders(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Access-Control-Allow-Headers", out var values))
        {
            return Array.Empty<string>();
        }

        return values
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(h => h.ToLowerInvariant())
            .ToArray();
    }

    [Fact]
    public async Task Preflight_ForTheHubNegotiate_AllowsBothSignalRHeaders()
    {
        using var response = await PreflightAsync(
            "x-requested-with,x-signalr-user-agent",
            HubNegotiatePath).ConfigureAwait(true);

        var allowed = AllowedHeaders(response);

        // Entrambi, non uno: il browser riporta un header bloccato per volta, quindi ammetterne
        // solo il primo lasciava il difetto identico con un messaggio diverso.
        allowed.Should().Contain("x-requested-with");
        allowed.Should().Contain("x-signalr-user-agent");
    }

    [Fact]
    public async Task Preflight_ForAHeaderThatIsNotOnTheAllowlist_DoesNotAllowIt()
    {
        // 🔴 Il controllo di non-vacuita', e serve per un modo di fallire molto concreto: se
        // qualcuno «risolvesse» un problema CORS passando a `AllowAnyHeader()`, il test sopra
        // resterebbe verde e la decisione di sicurezza di #1448 sarebbe annullata in silenzio.
        // Questo test e' l'unico che se ne accorgerebbe.
        using var response = await PreflightAsync(
            "x-meepleai-not-an-allowed-header",
            HubNegotiatePath).ConfigureAwait(true);

        AllowedHeaders(response).Should().NotContain("x-meepleai-not-an-allowed-header");
    }

    [Fact]
    public async Task Preflight_StillAllowsThePreExistingHeaders()
    {
        // L'aggiunta non deve aver sostituito l'elenco: `WithHeaders` e' una chiamata unica, e
        // un errore di merge la riscriverebbe per intero senza che nulla fallisse altrove.
        using var response = await PreflightAsync(
            "content-type,authorization,x-correlation-id,traceparent,tracestate",
            "/api/v1/health").ConfigureAwait(true);

        var allowed = AllowedHeaders(response);

        allowed.Should().Contain("content-type");
        allowed.Should().Contain("authorization");
        allowed.Should().Contain("x-correlation-id");
        allowed.Should().Contain("traceparent");
        allowed.Should().Contain("tracestate");
    }
}
