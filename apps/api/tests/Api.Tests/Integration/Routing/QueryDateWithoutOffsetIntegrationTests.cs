using System.Net;
using System.Reflection;
using System.Text.Json;
using Api.Filters;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// La fixture di <see cref="QueryDateWithoutOffsetIntegrationTests"/>: host e database per classe.
/// </summary>
/// <remarks>
/// Condividere il database è sicuro qui: nessun test semina dati di dominio, ognuno crea un utente
/// nuovo (id e email casuali) e asserisce solo status code e messaggio d'errore, mai conteggi o liste.
/// </remarks>
public sealed class QueryDateWithoutOffsetHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "query_date_utc");

/// <summary>
/// #4063 — una data nella query string scritta <b>senza designatore di fuso</b> si intende UTC.
/// </summary>
/// <remarks>
/// <para>
/// Il difetto: il binding di ASP.NET legge <c>2026-01-01</c> come <c>DateTime</c> con
/// <see cref="DateTimeKind.Unspecified"/>; Npgsql rifiuta quel valore su una colonna
/// <c>timestamp with time zone</c>, e l'<see cref="ArgumentException"/> diventa un 400
/// «Invalid request parameters». La forma con <c>Z</c> o con un offset arrivava già
/// <see cref="DateTimeKind.Utc"/>, quindi rispondeva 200 anche a difetto presente: per questo il test
/// fissa la forma <b>nuda</b>, e tiene le altre due solo come controllo.
/// </para>
/// <para>
/// Il controllo opposto è <c>notadate</c>: deve restare 400, e rifiutato dal <b>binding</b>, non dal
/// livello dati. Senza quel controllo, un 400 sulla forma nuda non distinguerebbe «valore rifiutato»
/// da «query fallita».
/// </para>
/// <para>
/// I dieci endpoint sono quelli misurati nel commento di #4063. La correzione non è per sito:
/// è il filtro <c>UtcDateTimeArguments</c> sul gruppo <c>/api/v1</c>, e l'ultimo test di questa
/// classe controlla che ogni endpoint con un argomento <c>DateTime</c> ci passi.
/// </para>
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class QueryDateWithoutOffsetIntegrationTests
    : IClassFixture<QueryDateWithoutOffsetHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public QueryDateWithoutOffsetIntegrationTests(QueryDateWithoutOffsetHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    private enum Caller
    {
        User,
        Admin,
    }

    private sealed record Probe(string Path, string Parameter, Caller Caller, string ExtraQuery = "");

    /// <summary>
    /// I dieci endpoint del commento di #4063, con il parametro data sondato. <c>ExtraQuery</c> porta
    /// i parametri obbligatori che non sono oggetto della sonda, già in forma con <c>Z</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Probe> Probes = new Dictionary<string, Probe>(StringComparer.Ordinal)
    {
        ["activity"] = new("/api/v1/activity", "since", Caller.User),
        ["sessions/history"] = new("/api/v1/sessions/history", "startDate", Caller.User),
        ["sessions/statistics"] = new("/api/v1/sessions/statistics", "startDate", Caller.User, "topPlayersLimit=5"),
        ["admin/audit-log"] = new("/api/v1/admin/audit-log/", "startDate", Caller.Admin),
        ["admin/service-calls"] = new("/api/v1/admin/service-calls/", "from", Caller.Admin),
        ["admin/agents/chat-history"] = new("/api/v1/admin/agents/chat-history", "dateFrom", Caller.Admin),
        ["admin/openrouter/requests"] = new("/api/v1/admin/openrouter/requests", "from", Caller.Admin),
        ["admin/financial-ledger"] = new("/api/v1/admin/financial-ledger/", "dateFrom", Caller.Admin),
        ["admin/financial-ledger/summary"] = new(
            "/api/v1/admin/financial-ledger/summary", "dateFrom", Caller.Admin, "dateTo=2026-12-31T00:00:00Z"),
        ["admin/pdfs"] = new("/api/v1/admin/pdfs", "uploadedAfter", Caller.Admin),
    };

    /// <summary>Le due forme senza fuso: solo data, e data con ora.</summary>
    private static readonly string[] WithoutOffset = ["2026-01-01", "2026-01-01T10:00:00"];

    /// <summary>Le due forme con fuso: già corrette prima di #4063, qui come controllo.</summary>
    private static readonly string[] WithOffset = ["2026-01-01T00:00:00Z", "2026-01-01T10:00:00+01:00"];

    public static TheoryData<string, string> EndpointsTimesFormsWithoutOffset() => Cross(WithoutOffset);

    public static TheoryData<string, string> EndpointsTimesFormsWithOffset() => Cross(WithOffset);

    public static TheoryData<string> Endpoints()
    {
        var data = new TheoryData<string>();
        foreach (var key in Probes.Keys)
        {
            data.Add(key);
        }

        return data;
    }

    private static TheoryData<string, string> Cross(string[] forms)
    {
        var data = new TheoryData<string, string>();
        foreach (var key in Probes.Keys)
        {
            foreach (var form in forms)
            {
                data.Add(key, form);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EndpointsTimesFormsWithoutOffset))]
    public async Task DateWithoutOffset_IsReadAsUtc_AndAnswers200(string endpoint, string value)
    {
        var (status, body) = await GetAsync(Probes[endpoint], value);

        status.Should().Be(HttpStatusCode.OK,
            $"«{value}» has no offset, so it is read as UTC (#4063); before the fix it reached Npgsql as " +
            $"Kind=Unspecified and came back 400 «Invalid request parameters». Body: {body}");
    }

    [Theory]
    [MemberData(nameof(EndpointsTimesFormsWithOffset))]
    public async Task DateWithOffset_StillAnswers200(string endpoint, string value)
    {
        var (status, body) = await GetAsync(Probes[endpoint], value);

        status.Should().Be(HttpStatusCode.OK,
            $"«{value}» carries its offset and was already bound as Kind=Utc: if this fails, the 400 on the " +
            $"bare form has another cause. Body: {body}");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task NotADate_IsStillRejectedByBinding(string endpoint)
    {
        var (status, body) = await GetAsync(Probes[endpoint], "notadate");

        status.Should().Be(HttpStatusCode.BadRequest, $"a value that is not a date stays rejected. Body: {body}");

        // Il rifiuto deve venire dal binding, non dal livello dati. Fuori da Development il binding
        // fallito risponde 400 a corpo VUOTO (RouteHandlerOptions.ThrowOnBadRequest è attivo solo in
        // Development, dove il corpo porta «Failed to bind parameter»); il livello dati risponde
        // invece {"message":"Invalid request parameters"}. Accettare le due forme del binding e
        // nessun'altra tiene il test valido in entrambi gli ambienti.
        var message = ErrorMessage(body);
        (message.Length == 0 || message.Contains("Failed to bind parameter", StringComparison.Ordinal))
            .Should().BeTrue(
                "the rejection must come from binding (empty body, or «Failed to bind parameter»), not from the " +
                $"data layer («Invalid request parameters»): the fix must not turn a non-date into a value. Body: {body}");
    }

    /// <summary>
    /// DoD di #4063: «un intervallo invertito resta rifiutato dove la regola esiste». Le due regole
    /// esistenti sono il validator del riepilogo del registro e quello delle esecuzioni RAG; tutte e due
    /// confrontano i due confini, quindi devono continuare a farlo anche sulla forma nuda.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/admin/financial-ledger/summary?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/v1/admin/rag-executions/?skip=0&take=10&dateFrom=2026-02-01&dateTo=2026-01-01")]
    public async Task InvertedWindowWithoutOffset_IsStillRejected(string pathAndQuery)
    {
        var token = await CreateSessionAsync(Caller.Admin);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, pathAndQuery, token);
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"an inverted window stays rejected by its validator, not accepted nor turned into a 400. Body: {body}");
    }

    /// <summary>
    /// Il gate contro il quarto giro (dopo #255, #4055, #4064): ogni endpoint con un argomento
    /// <c>DateTime</c> deve passare da <see cref="UtcDateTimeArguments"/>.
    /// </summary>
    /// <remarks>
    /// Il filtro sta sul gruppo <c>/api/v1</c>, quindi un endpoint nuovo lo eredita da solo. Questo
    /// test intercetta i due modi di restarne fuori: registrare l'endpoint direttamente su <c>app</c>
    /// (come fanno alcuni file in <c>Program.cs</c>), oppure usare una forma che il filtro non sa
    /// normalizzare (un array di date, una proprietà <c>[AsParameters]</c> senza setter).
    /// </remarks>
    [Fact]
    public void EveryEndpointWithADateTimeArgument_ReadsItAsUtc()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var withDates = new List<string>();
        var outsideTheFilter = new List<string>();
        var notNormalizable = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var handler = endpoint.Metadata.GetMetadata<MethodInfo>();
            if (handler is null)
            {
                continue;
            }

            var sites = UtcDateTimeArguments.FindSites(handler);
            if (sites.Count == 0)
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"];
            var name = $"{string.Join(",", methods)} {(endpoint.RoutePattern.RawText ?? string.Empty).Trim('/')}";
            var described = $"{name} ({string.Join(", ", sites.Select(s => s.Property is null ? s.Parameter : $"{s.Parameter}.{s.Property}"))})";

            withDates.Add(name);
            if (endpoint.Metadata.GetMetadata<UtcDateTimeArgumentsMetadata>() is null)
            {
                outsideTheFilter.Add(described);
            }

            notNormalizable.AddRange(sites
                .Where(s => !s.IsNormalized)
                .Select(s => $"{name}: {(s.Property is null ? s.Parameter : $"{s.Parameter}.{s.Property}")}"));
        }

        // Guardia di non-vacuità: se la scansione non trovasse più i dieci endpoint di #4063 (metadata
        // cambiata, MethodInfo assente), le due asserzioni sotto passerebbero su una lista vuota.
        withDates.Should().Contain(
            Probes.Values.Select(p => $"GET {p.Path.Trim('/')}"),
            "the inventory must see the ten endpoints measured in #4063, or it proves nothing");

        outsideTheFilter.Should().BeEmpty(
            "a DateTime argument outside UtcDateTimeArguments reaches Npgsql as Kind=Unspecified and answers 400 " +
            "(#4063). Map the endpoint under the /api/v1 group, or call .WithUtcDateTimeArguments() on its group. " +
            "Found: " + string.Join(" ;; ", outsideTheFilter));

        notNormalizable.Should().BeEmpty(
            "the filter can only rewrite a top-level DateTime/DateTime? argument or a writable property of an " +
            "[AsParameters] argument. Found: " + string.Join(" ;; ", notNormalizable));
    }

    private async Task<(HttpStatusCode Status, string Body)> GetAsync(Probe probe, string value)
    {
        var token = await CreateSessionAsync(probe.Caller);

        var query = $"{probe.Parameter}={Uri.EscapeDataString(value)}";
        if (probe.ExtraQuery.Length > 0)
        {
            query = $"{probe.ExtraQuery}&{query}";
        }

        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, $"{probe.Path}?{query}", token);
        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<string> CreateSessionAsync(Caller caller)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (_, token) = caller == Caller.Admin
            ? await TestSessionHelper.CreateAdminSessionAsync(db)
            : await TestSessionHelper.CreateUserSessionAsync(db);
        return token;
    }

    private static string ErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() ?? string.Empty
                : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
