using System.Net;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola (vedi <see cref="IntegrationHostFixture"/>).
/// <para>
/// Condividere il database è sicuro qui: ogni test crea i propri utenti e le asserzioni leggono solo lo
/// stato HTTP, mai un conteggio o un elenco prodotto dalle metriche.
/// </para>
/// </summary>
public sealed class RagDashboardMetricsAuthorizationHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "rag_dashboard_authz");

/// <summary>
/// #4584: le rotte di metriche di <c>/api/v1/rag-dashboard</c> chiedevano solo una sessione valida
/// (<c>TryGetActiveSession</c>), quindi le leggeva ogni utente autenticato. Restituiscono metriche aggregate su
/// tutte le strategie, senza nessun <c>UserId</c> nella query: sono dati da amministratore, come
/// <c>POST /query/stream</c> nello stesso gruppo.
/// <para>
/// La sonda di #4568 non le vedeva: guarda solo le rotte con un segmento <c>admin</c> nel percorso.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4584")]
public sealed class RagDashboardMetricsAuthorizationIntegrationTests
    : IClassFixture<RagDashboardMetricsAuthorizationHostFixture>
{
    private const string GroupPath = "api/v1/rag-dashboard";

    /// <summary>
    /// I prefissi delle rotte di metriche. Non c'è il prefisso del gruppo intero perché <c>/config</c> è per
    /// utente e <c>/options</c> è anonima di proposito (vedi <see cref="TheUserScopedRoutes_StayOpen"/>).
    /// Le rotte nuove sotto questi prefissi entrano nel test da sole.
    /// </summary>
    private static readonly string[] MetricsPrefixes =
    [
        $"{GroupPath}/overview",
        $"{GroupPath}/strategies",
    ];

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RagDashboardMetricsAuthorizationIntegrationTests(RagDashboardMetricsAuthorizationHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task EveryMetricsRoute_RejectsWhoIsNotAdmin()
    {
        var routes = MetricsRoutes();
        AssertTheScanSeesTheMetricsRoutes(routes);

        var (_, userToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var (_, editorToken) = await CreateSessionAsync(TestSessionHelper.CreateEditorSessionAsync);

        var failures = new List<string>();
        foreach (var (method, template) in routes)
        {
            failures.Add(await ProbeAsync(method, template, "anonymous", token: null, HttpStatusCode.Unauthorized));
            failures.Add(await ProbeAsync(method, template, "user", userToken, HttpStatusCode.Forbidden));
            failures.Add(await ProbeAsync(method, template, "editor", editorToken, HttpStatusCode.Forbidden));
        }

        failures.RemoveAll(f => f.Length == 0);
        failures.Should().BeEmpty(
            "these metrics aggregate every strategy and carry no UserId: only an admin may read them. Before " +
            "#4584 any authenticated user got through to the handler; a 200 here (a 400 before #4623, for the " +
            "reason described on Admin_ReadsEveryMetricsRoute) is a user who was not stopped. " +
            "Found: " + string.Join(" ;; ", failures));
    }

    /// <summary>
    /// L'admin legge le metriche: 200 su ogni rotta, senza parametri. Fino a #4623 rispondevano 400 a chiunque, admin
    /// compreso, perché <c>RagExecutionRepository</c> passava a Npgsql date con <c>DateTimeKind.Unspecified</c>
    /// (<c>ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time
    /// zone'</c>, che il middleware mostra come «Invalid request parameters»), e questo test pretendeva solo
    /// «né 401 né 403».
    /// </summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task Admin_ReadsEveryMetricsRoute(string role)
    {
        var (_, token) = await CreateSessionAsync(role == "Admin"
            ? TestSessionHelper.CreateAdminSessionAsync
            : TestSessionHelper.CreateSuperAdminSessionAsync);

        var routes = MetricsRoutes();
        AssertTheScanSeesTheMetricsRoutes(routes);

        var notOk = new List<string>();
        foreach (var (method, template) in routes)
        {
            var path = "/" + template.Replace("{strategyId}", "Hybrid", StringComparison.Ordinal);
            var (status, body) = await SendAsync(new HttpMethod(method), path, token);
            if (status != HttpStatusCode.OK)
            {
                notOk.Add($"{method} {path} as {role}: {(int)status} {body[..Math.Min(body.Length, 200)]}");
            }
        }

        notOk.Should().BeEmpty($"an {role} reads the metrics. Found: " + string.Join(" ;; ", notOk));
    }

    /// <summary>
    /// L'issue nota che il resto del gruppo non è un difetto: <c>/config</c> è la configurazione dell'utente e
    /// <c>/options</c> è anonima di proposito. Restringere il gruppo intero invece delle sole rotte di metriche le
    /// chiuderebbe a chi le usa davvero (<c>knowledgeBaseClient.ts</c> chiama <c>/config</c>).
    /// </summary>
    [Fact]
    public async Task TheUserScopedRoutes_StayOpen()
    {
        var (_, userToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);

        var config = await SendAsync(HttpMethod.Get, $"/{GroupPath}/config", userToken);
        config.Status.Should().Be(HttpStatusCode.OK, $"the RAG configuration is per user: a plain user reads their own. Body: {config.Body}");

        var options = await SendAsync(HttpMethod.Get, $"/{GroupPath}/options", token: null);
        options.Status.Should().Be(HttpStatusCode.OK, $"the options are public by design (AllowAnonymous). Body: {options.Body}");
    }

    /// <summary>
    /// Guardia di non-vacuità, comune ai test che scorrono le rotte: se la lettura della tabella di routing non
    /// vedesse più queste rotte, il ciclo del test non proverebbe nulla.
    /// </summary>
    private static void AssertTheScanSeesTheMetricsRoutes(IEnumerable<(string Method, string Template)> routes) =>
        routes.Select(r => $"{r.Method} {r.Template}").Should().Contain(
            [
                $"GET {GroupPath}/overview",
                $"GET {GroupPath}/strategies/{{strategyId}}/metrics",
                $"GET {GroupPath}/strategies/{{strategyId}}/time-series",
                $"GET {GroupPath}/strategies/compare",
            ],
            "the scan must see the routes of #4584, or it proves nothing");

    /// <summary>Le rotte di #4584 lette dalla tabella di routing dell'host vero, come «METODO modello».</summary>
    private List<(string Method, string Template)> MetricsRoutes() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Template: (e.RoutePattern.RawText ?? string.Empty).Trim('/')))
            .Where(x => MetricsPrefixes.Any(p =>
                x.Template.Equals(p, StringComparison.OrdinalIgnoreCase)
                || x.Template.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(x => (x.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => (Method: m, x.Template)))
            .OrderBy(r => r.Template, StringComparer.Ordinal)
            .ThenBy(r => r.Method, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Chiama una rotta e restituisce una riga di errore se lo stato non è quello atteso, altrimenti stringa
    /// vuota. L'unico parametro di rotta è <c>{strategyId}</c>: prende una strategia valida.
    /// </summary>
    private async Task<string> ProbeAsync(
        string method, string template, string who, string? token, HttpStatusCode expected)
    {
        var path = "/" + template.Replace("{strategyId}", "Hybrid", StringComparison.Ordinal);
        var (status, body) = await SendAsync(new HttpMethod(method), path, token);
        return status == expected
            ? string.Empty
            : $"{method} {path} as {who}: expected {(int)expected}, got {(int)status} {body[..Math.Min(body.Length, 200)]}";
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpMethod method, string path, string? token)
    {
        using var request = token is null
            ? new HttpRequestMessage(method, path)
            : TestSessionHelper.CreateAuthenticatedRequest(method, path, token);
        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<(Guid UserId, string Token)> CreateSessionAsync(
        Func<MeepleAiDbContext, Guid?, CancellationToken, Task<(Guid UserId, string RawToken)>> create)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await create(db, null, CancellationToken.None);
    }
}
