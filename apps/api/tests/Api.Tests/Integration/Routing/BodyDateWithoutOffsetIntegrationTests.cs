using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Serialization;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Xunit;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// La fixture di <see cref="BodyDateWithoutOffsetIntegrationTests"/>: host e database per classe.
/// </summary>
/// <remarks>
/// Condividere il database è sicuro qui: ogni test crea il proprio utente e le proprie righe, e asserisce
/// solo lo status code o la riga che ha creato (per id), mai conteggi o liste. L'unica riga condivisa è
/// il singleton del banner di stato, che i test scrivono e non leggono.
/// </remarks>
public sealed class BodyDateWithoutOffsetHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "body_date_utc")
{
    /// <summary>
    /// <c>POST /library/share</c> compone l'URL del link da <c>App:BaseUrl</c>, che la configurazione
    /// d'integrazione non porta: senza, risponde 500 anche con una data corretta.
    /// </summary>
    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        ["App:BaseUrl"] = "http://localhost:3000",
    };
}

/// <summary>
/// #4519 — una data nel <b>corpo JSON</b> scritta senza designatore di fuso si intende UTC; una data con
/// un offset diverso da zero nomina un istante, e arriva al database come quell'istante.
/// </summary>
/// <remarks>
/// <para>
/// Il difetto: System.Text.Json legge <c>"2026-01-01"</c> come <see cref="DateTimeKind.Unspecified"/> e
/// <c>"…+01:00"</c> come <see cref="DateTimeKind.Local"/>; Npgsql rifiuta entrambi su una colonna
/// <c>timestamp with time zone</c>, <c>SaveChanges</c> lancia una <c>DbUpdateException</c> e la richiesta
/// risponde <b>500</b>. La correzione sono i due convertitori di <see cref="UtcDateTimeJson"/>, nelle
/// opzioni JSON delle minimal API.
/// </para>
/// <para>
/// Gli endpoint del test parametrico sono quelli del censimento in cui la data arriva a una colonna
/// <c>timestamptz</c> o a un filtro su una colonna <c>timestamptz</c>. La forma con <c>Z</c> è il
/// controllo: passava anche prima, quindi un rosso sulle altre forme viene dalla data e non da un corpo
/// sbagliato. L'ultimo test elenca ogni data di ogni corpo e controlla che passi dai convertitori.
/// </para>
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class BodyDateWithoutOffsetIntegrationTests
    : IClassFixture<BodyDateWithoutOffsetHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BodyDateWithoutOffsetIntegrationTests(BodyDateWithoutOffsetHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // ── Il registro: il caso della issue, con il valore salvato ─────────────────

    /// <summary>
    /// Il valore scritto nel corpo e l'istante che deve arrivare in <c>ledger_entries."Date"</c>.
    /// </summary>
    public static TheoryData<string, string> LedgerDates() => new()
    {
        // Senza fuso: l'orologio scritto resta quello, il fuso è UTC (la regola di #4063).
        { "2026-01-01", "2026-01-01T00:00:00.0000000Z" },
        { "2026-01-01T10:00:00", "2026-01-01T10:00:00.0000000Z" },
        // Con offset non nullo: System.Text.Json lo legge Kind=Local; l'istante è 09:00Z.
        { "2026-01-01T10:00:00+01:00", "2026-01-01T09:00:00.0000000Z" },
        // Controllo: già UTC prima della correzione.
        { "2026-01-01T00:00:00Z", "2026-01-01T00:00:00.0000000Z" },
    };

    [Theory]
    [MemberData(nameof(LedgerDates))]
    public async Task CreateLedgerEntry_DateInBody_IsStoredAsTheUtcInstant(string written, string expectedUtc)
    {
        var (_, token) = await CreateSessionAsync(admin: true);

        var (status, body) = await SendJsonAsync(HttpMethod.Post, "/api/v1/admin/financial-ledger", token,
            $$"""{"date":"{{written}}","type":"Expense","category":"Operational","amount":12.5,"currency":"EUR","description":"#4519"}""");

        status.Should().Be(HttpStatusCode.Created,
            $"«{written}» is a valid date: without an offset it is read as UTC, with an offset it names an " +
            $"instant. Before #4519 it reached Npgsql as Unspecified/Local and answered 500. Body: {body}");

        var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var stored = await db.LedgerEntries.AsNoTracking().SingleAsync(e => e.Id == id);

        stored.Date.Kind.Should().Be(DateTimeKind.Utc);
        stored.Date.ToString("O", CultureInfo.InvariantCulture).Should().Be(expectedUtc);
    }

    // ── Ogni endpoint del censimento che porta la data al database ──────────────

    private enum When
    {
        Past,
        Future,
    }

    /// <summary>
    /// Gli endpoint, e se la data deve stare nel passato (un giorno di gioco, una registrazione) o nel
    /// futuro (una scadenza, una pianificazione) per superare i validatori. L'anno è calcolato da oggi:
    /// un anno fisso invecchierebbe e farebbe fallire il test per la ragione sbagliata.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, When> Endpoints = new Dictionary<string, When>(StringComparer.Ordinal)
    {
        ["PUT admin/status-banner"] = When.Future,
        ["POST admin/users/{id}/rate-limit-override"] = When.Future,
        ["POST admin/analytics/export"] = When.Past,
        ["POST admin/mechanic-analyses/{id}/suppress"] = When.Past,
        ["POST library/share"] = When.Future,
        ["PATCH library/share/{shareToken}"] = When.Future,
        ["POST library/games/{gameId}/sessions"] = When.Past,
        ["POST play-records"] = When.Past,
        ["PUT play-records/{recordId}"] = When.Past,
        ["POST game-sessions"] = When.Past,
        ["POST playlists"] = When.Future,
        ["PUT playlists/{id}"] = When.Future,
    };

    /// <summary>
    /// Le forme: solo data, data e ora senza fuso, con offset, e il controllo con <c>Z</c>. <c>{0}</c> è
    /// l'anno.
    /// </summary>
    private static readonly string[] Forms = ["{0}-01-01", "{0}-01-01T10:00:00", "{0}-01-01T10:00:00+01:00", "{0}-01-01T00:00:00Z"];

    public static TheoryData<string, string> EndpointsTimesForms()
    {
        var data = new TheoryData<string, string>();
        foreach (var endpoint in Endpoints.Keys)
        {
            foreach (var form in Forms)
            {
                data.Add(endpoint, form);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EndpointsTimesForms))]
    public async Task DateInBody_AnyForm_IsAccepted(string endpoint, string form)
    {
        var year = Endpoints[endpoint] == When.Past ? DateTime.UtcNow.Year - 1 : DateTime.UtcNow.Year + 2;
        var date = string.Format(CultureInfo.InvariantCulture, form, year);

        var (status, body) = await SendProbeAsync(endpoint, date);

        ((int)status).Should().BeInRange(200, 299,
            $"«{date}» in the body of {endpoint} is a valid date: without an offset it is read as UTC, with an " +
            $"offset it names an instant. Before #4519 the bare forms reached Npgsql as Kind=Unspecified and the " +
            $"offset form as Kind=Local. Status {(int)status}, body: {body}");
    }

    private async Task<(HttpStatusCode Status, string Body)> SendProbeAsync(string endpoint, string date)
    {
        switch (endpoint)
        {
            case "PUT admin/status-banner":
                {
                    var (_, token) = await CreateSessionAsync(admin: true);

                    // Il banner è una riga sola, e EF scrive una colonna solo se il valore cambia, confrontando
                    // i DateTime per tick e non per Kind. Senza l'azzeramento, una forma con lo stesso orologio
                    // della sonda precedente non arriverebbe a Npgsql e passerebbe per la ragione sbagliata.
                    var (reset, resetBody) = await SendJsonAsync(HttpMethod.Put, "/api/v1/admin/status-banner", token,
                        """{"message":"#4519","severity":"Info","isActive":false,"startsAt":null,"endsAt":null}""");
                    ((int)reset).Should().BeInRange(200, 299, $"setup: the banner must be reset. Body: {resetBody}");

                    return await SendJsonAsync(HttpMethod.Put, "/api/v1/admin/status-banner", token,
                        $$"""{"message":"#4519","severity":"Info","isActive":false,"startsAt":"{{date}}","endsAt":null}""");
                }

            case "POST admin/users/{id}/rate-limit-override":
                {
                    var (_, token) = await CreateSessionAsync(admin: true);
                    var (target, _) = await CreateSessionAsync(admin: false);
                    return await SendJsonAsync(HttpMethod.Post, $"/api/v1/admin/users/{target}/rate-limit-override", token,
                        $$"""{"maxPendingRequests":5,"expiresAt":"{{date}}","reason":"#4519 date without offset"}""");
                }

            case "POST admin/analytics/export":
                {
                    var (_, token) = await CreateSessionAsync(admin: true);

                    // Un gameId nuovo a ogni richiesta: la chiave di cache di AdminStatsService non contiene
                    // le date, quindi dopo il primo 200 le richieste successive non toccherebbero il database
                    // e la sonda passerebbe senza misurare nulla. Il gameId fa parte della chiave.
                    return await SendJsonAsync(HttpMethod.Post, "/api/v1/admin/analytics/export", token,
                        $$"""{"format":"csv","fromDate":"{{date}}","gameId":"{{Guid.NewGuid()}}"}""");
                }

            case "POST admin/mechanic-analyses/{id}/suppress":
                {
                    var (_, token) = await CreateSessionAsync(admin: true);
                    var analysisId = await SeedMechanicAnalysisAsync();
                    return await SendJsonAsync(HttpMethod.Post, $"/api/v1/admin/mechanic-analyses/{analysisId}/suppress", token,
                        $$"""{"reason":"#4519 takedown request received by email","requestSource":"Email","requestedAt":"{{date}}"}""");
                }

            case "POST library/share":
                {
                    var (_, token) = await CreateSessionAsync(admin: false);
                    return await SendJsonAsync(HttpMethod.Post, "/api/v1/library/share", token,
                        $$"""{"privacyLevel":"unlisted","expiresAt":"{{date}}"}""");
                }

            case "PATCH library/share/{shareToken}":
                {
                    var (_, token) = await CreateSessionAsync(admin: false);
                    var (created, createdBody) = await SendJsonAsync(HttpMethod.Post, "/api/v1/library/share", token,
                        """{"privacyLevel":"unlisted"}""");
                    created.Should().Be(HttpStatusCode.Created, $"setup: the share link must exist. Body: {createdBody}");
                    var shareToken = JsonDocument.Parse(createdBody).RootElement.GetProperty("shareToken").GetString();
                    return await SendJsonAsync(HttpMethod.Patch, $"/api/v1/library/share/{shareToken}", token,
                        $$"""{"expiresAt":"{{date}}"}""");
                }

            case "POST library/games/{gameId}/sessions":
                {
                    var (userId, token) = await CreateSessionAsync(admin: false);
                    var gameId = await SeedOwnedGameAsync(userId);
                    return await SendJsonAsync(HttpMethod.Post, $"/api/v1/library/games/{gameId}/sessions", token,
                        $$"""{"playedAt":"{{date}}","durationMinutes":60}""");
                }

            case "POST play-records":
                {
                    var (_, token) = await CreateSessionAsync(admin: false);
                    return await SendJsonAsync(HttpMethod.Post, "/api/v1/play-records", token,
                        $$"""{"gameName":"#4519","sessionDate":"{{date}}","visibility":"Private"}""");
                }

            case "PUT play-records/{recordId}":
                {
                    var (_, token) = await CreateSessionAsync(admin: false);
                    var (created, createdBody) = await SendJsonAsync(HttpMethod.Post, "/api/v1/play-records", token,
                        $$"""{"gameName":"#4519","sessionDate":"{{DateTime.UtcNow.Year - 1}}-06-01T00:00:00Z","visibility":"Private"}""");
                    created.Should().Be(HttpStatusCode.Created, $"setup: the play record must exist. Body: {createdBody}");
                    var recordId = JsonSerializer.Deserialize<Guid>(createdBody);
                    return await SendJsonAsync(HttpMethod.Put, $"/api/v1/play-records/{recordId}", token,
                        $$"""{"sessionDate":"{{date}}"}""");
                }

            case "POST game-sessions":
                {
                    var (userId, token) = await CreateSessionAsync(admin: false);
                    var gameId = await SeedKbReadyGameAsync(userId);
                    return await SendJsonAsync(HttpMethod.Post, "/api/v1/game-sessions", token,
                        $$"""{"gameId":"{{gameId}}","sessionType":"Generic","sessionDate":"{{date}}","participants":[{"displayName":"#4519","isOwner":true}]}""");
                }

            case "POST playlists":
                {
                    var (_, token) = await CreateSessionAsync(admin: false);
                    return await SendJsonAsync(HttpMethod.Post, "/api/v1/playlists", token,
                        $$"""{"name":"#4519","scheduledDate":"{{date}}"}""");
                }

            case "PUT playlists/{id}":
                {
                    var (_, token) = await CreateSessionAsync(admin: false);
                    var (created, createdBody) = await SendJsonAsync(HttpMethod.Post, "/api/v1/playlists", token,
                        """{"name":"#4519"}""");
                    created.Should().Be(HttpStatusCode.Created, $"setup: the playlist must exist. Body: {createdBody}");
                    var playlistId = JsonDocument.Parse(createdBody).RootElement.GetProperty("id").GetGuid();
                    return await SendJsonAsync(HttpMethod.Put, $"/api/v1/playlists/{playlistId}", token,
                        $$"""{"scheduledDate":"{{date}}"}""");
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "no probe for this endpoint");
        }
    }

    // ── La scrittura non cambia: risposta vera e documento OpenAPI vero ─────────

    /// <summary>
    /// Una risposta esistente con date: la forma serializzata resta quella del convertitore di serie.
    /// </summary>
    [Fact]
    public async Task LedgerEntryResponse_DatesKeepTheBuiltInShape()
    {
        var (_, token) = await CreateSessionAsync(admin: true);
        // Postgres conserva i microsecondi, non i tick: il valore di prova ne ha sei, non sette.
        var date = new DateTime(2026, 1, 1, 10, 30, 15, 123, DateTimeKind.Utc).AddTicks(4560);

        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entry = Api.BoundedContexts.BusinessSimulations.Domain.Entities.LedgerEntry.CreateManualEntry(
                date,
                Api.BoundedContexts.BusinessSimulations.Domain.Enums.LedgerEntryType.Expense,
                Api.BoundedContexts.BusinessSimulations.Domain.Enums.LedgerCategory.Operational,
                12.5m,
                Guid.NewGuid(),
                "EUR",
                "#4519 response shape");
            db.LedgerEntries.Add(entry);
            await db.SaveChangesAsync();
            id = entry.Id;
        }

        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/admin/financial-ledger/{id}", token);
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var written = json.RootElement.GetProperty("date").GetRawText();
        var createdAt = json.RootElement.GetProperty("createdAt").GetString()!;

        written.Should().Be(JsonSerializer.Serialize(date),
            "the response keeps the shape of System.Text.Json's own DateTime converter (round-trip, Z, trimmed fraction)");
        written.Should().Be("\"2026-01-01T10:30:15.123456Z\"");
        createdAt.Should().EndWith("Z", "a UTC value read back from timestamptz is still written with its designator");
    }

    /// <summary>
    /// Il documento OpenAPI vero dell'API deve restare identico a quello che l'API produrrebbe senza i
    /// convertitori: lo leggono Scalar e lo script degli schemi zod del frontend.
    /// </summary>
    /// <remarks>
    /// Costruisce un secondo host (la stessa configurazione, meno i convertitori): una volta sola, e
    /// solo in questo test.
    /// </remarks>
    [Fact]
    public async Task OpenApiDocument_IsTheSameWithoutTheConverters()
    {
        var actual = await OpenApiDocumentAsync(_factory);

        using var withoutConverters = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<HttpJsonOptions>(options =>
            {
                var ours = options.SerializerOptions.Converters
                    .Where(c => c is UtcDateTimeJsonConverter or UtcDateTimeOffsetJsonConverter)
                    .ToList();
                ours.Should().HaveCount(2, "guard: the converters must be registered, or the comparison proves nothing");
                foreach (var converter in ours)
                {
                    options.SerializerOptions.Converters.Remove(converter);
                }
            })));
        var expected = await OpenApiDocumentAsync(withoutConverters);

        expected.Should().Contain("\"format\": \"date-time\"", "guard: the document must contain date schemas at all");
        (actual == expected).Should().BeTrue(
            "the converters must not change the OpenAPI document. First difference near: " + FirstDifference(actual, expected));
    }

    private static async Task<string> OpenApiDocumentAsync(WebApplicationFactory<Program> factory)
    {
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync();
        return await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1);
    }

    private static string FirstDifference(string a, string b)
    {
        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }

        var start = Math.Max(0, i - 300);
        return $"\n--- with converters ---\n{a.Substring(start, Math.Min(600, a.Length - start))}" +
               $"\n--- without ---\n{b.Substring(start, Math.Min(600, b.Length - start))}";
    }

    // ── L'inventario ────────────────────────────────────────────────────────────

    /// <summary>
    /// Il gate contro il prossimo giro: ogni data in ogni corpo JSON deve passare dai convertitori di
    /// <see cref="UtcDateTimeJson"/>.
    /// </summary>
    /// <remarks>
    /// I convertitori stanno nelle opzioni, quindi un DTO nuovo li eredita da solo. Questo test
    /// intercetta i modi di restarne fuori: un <see cref="JsonConverterAttribute"/> su una proprietà o su
    /// un tipo (vince sulle opzioni), un corpo che non è JSON (form), una chiave di dizionario
    /// <see cref="DateTimeOffset"/>, e i convertitori tolti dalle opzioni. I corpi letti a mano con
    /// <c>Request.ReadFromJsonAsync</c> non lasciano metadata, ma usano le stesse opzioni.
    /// <see cref="DateOnly"/> e <see cref="TimeOnly"/> non hanno né <see cref="DateTimeKind"/> né offset:
    /// fuori dal censimento.
    /// </remarks>
    [Fact]
    public void EveryDateInAJsonBody_GoesThroughTheUtcConverters()
    {
        var options = _factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Le opzioni vere dell'API, sul comportamento e non sui tipi registrati.
        JsonSerializer.Deserialize<DateTime>("\"2026-01-01T10:00:00\"", options).Kind.Should().Be(DateTimeKind.Utc,
            "the minimal API JSON options must read a bare DateTime as UTC (UtcDateTimeJson.AddConverters in Program.cs)");
        JsonSerializer.Deserialize<DateTime?>("\"2026-01-01T10:00:00+01:00\"", options)!.Value.Kind.Should().Be(DateTimeKind.Utc);
        JsonSerializer.Deserialize<DateTimeOffset>("\"2026-01-01T10:00:00+01:00\"", options).Offset.Should().Be(TimeSpan.Zero,
            "the minimal API JSON options must move a DateTimeOffset to offset zero");
        JsonSerializer.Deserialize<DateTimeOffset?>("\"2026-01-01T10:00:00\"", options)!.Value.Offset.Should().Be(TimeSpan.Zero);

        var sites = new List<string>();
        var uncovered = new List<string>();

        foreach (var endpoint in _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            foreach (var accepts in endpoint.Metadata.GetOrderedMetadata<IAcceptsMetadata>())
            {
                if (accepts.RequestType is null)
                {
                    continue;
                }

                var found = new List<BodyDateSite>();
                BodyDateSite.Walk(accepts.RequestType, accepts.RequestType.Name, overridden: false, found, [], depth: 0);
                if (found.Count == 0)
                {
                    continue;
                }

                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"];
                var name = $"{string.Join(",", methods)} {(endpoint.RoutePattern.RawText ?? string.Empty).Trim('/')}";
                var isJson = accepts.ContentTypes.Any(c => c.Contains("json", StringComparison.OrdinalIgnoreCase));

                foreach (var site in found)
                {
                    sites.Add($"{name} {site.Path}");
                    if (!isJson)
                    {
                        uncovered.Add($"{name} {site.Path}: body is not JSON ({string.Join("|", accepts.ContentTypes)})");
                    }
                    else if (site.Problem is { } problem)
                    {
                        uncovered.Add($"{name} {site.Path}: {problem}");
                    }
                }
            }
        }

        // Guardia di non-vacuità: se la scansione non vedesse più i corpi del censimento (metadata
        // cambiata), l'asserzione sotto passerebbe su una lista vuota.
        sites.Should().Contain(
            [
                "POST api/v1/admin/financial-ledger CreateLedgerEntryRequest.Date",
                "POST api/v1/library/share CreateLibraryShareRequest.ExpiresAt",
                "POST api/v1/game-nights CreateGameNightRequest.ScheduledAt",
                "POST api/v1/alerts/prometheus PrometheusAlertWebhook.Alerts[].StartsAt",
            ],
            "the inventory must see the request bodies of the #4519 census, or it proves nothing. Seen: " +
            string.Join(" ;; ", sites));

        uncovered.Should().BeEmpty(
            "a date in a request body that bypasses UtcDateTimeJson reaches Npgsql as Kind=Unspecified/Local or " +
            "with a non-zero offset, and answers 500 (#4519). Remove the [JsonConverter], or normalize it and " +
            "explain here why. Found: " + string.Join(" ;; ", uncovered));
    }

    /// <summary>Una data trovata in un tipo di corpo, e perché sfugge ai convertitori (o <c>null</c>).</summary>
    private sealed record BodyDateSite(string Path, string? Problem)
    {
        private static readonly Type[] Dates =
            [typeof(DateTime), typeof(DateTime?), typeof(DateTimeOffset), typeof(DateTimeOffset?)];

        public static void Walk(Type type, string path, bool overridden, List<BodyDateSite> found, HashSet<Type> visiting, int depth)
        {
            if (depth > 10)
            {
                return;
            }

            if (Dates.Contains(type))
            {
                found.Add(new BodyDateSite(path, overridden ? "a [JsonConverter] attribute wins over the options" : null));
                return;
            }

            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (IsLeaf(underlying))
            {
                return;
            }

            if (underlying.IsDefined(typeof(JsonConverterAttribute), inherit: true))
            {
                overridden = true;
            }

            if (underlying.IsArray)
            {
                Walk(underlying.GetElementType()!, path + "[]", overridden, found, visiting, depth + 1);
                return;
            }

            var dictionary = GenericInterface(underlying, typeof(IDictionary<,>)) ?? GenericInterface(underlying, typeof(IReadOnlyDictionary<,>));
            if (dictionary is not null)
            {
                var key = dictionary.GetGenericArguments()[0];
                if (key == typeof(DateTimeOffset) || key == typeof(DateTimeOffset?))
                {
                    found.Add(new BodyDateSite(path + "{key}", "a DateTimeOffset dictionary key is read by the built-in converter"));
                }
                else
                {
                    Walk(key, path + "{key}", overridden, found, visiting, depth + 1);
                }

                Walk(dictionary.GetGenericArguments()[1], path + "{}", overridden, found, visiting, depth + 1);
                return;
            }

            if (GenericInterface(underlying, typeof(IEnumerable<>)) is { } enumerable)
            {
                Walk(enumerable.GetGenericArguments()[0], path + "[]", overridden, found, visiting, depth + 1);
                return;
            }

            if (typeof(IEnumerable).IsAssignableFrom(underlying) || !visiting.Add(underlying))
            {
                return;
            }

            foreach (var property in underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || property.IsDefined(typeof(JsonIgnoreAttribute)))
                {
                    continue;
                }

                Walk(property.PropertyType, $"{path}.{property.Name}",
                    overridden || property.IsDefined(typeof(JsonConverterAttribute)), found, visiting, depth + 1);
            }

            visiting.Remove(underlying);
        }

        private static bool IsLeaf(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid)
            || type == typeof(TimeSpan) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(Uri)
            || type == typeof(object) || typeof(Stream).IsAssignableFrom(type)
            || type.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true
            || type.Namespace?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true;

        private static Type? GenericInterface(Type type, Type definition) =>
            (type.IsGenericType && type.GetGenericTypeDefinition() == definition)
                ? type
                : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);
    }

    // ── Aiuti ───────────────────────────────────────────────────────────────────

    private async Task<(HttpStatusCode Status, string Body)> SendJsonAsync(HttpMethod method, string path, string token, string json)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(method, path, token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<(Guid UserId, string Token)> CreateSessionAsync(bool admin)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return admin
            ? await TestSessionHelper.CreateAdminSessionAsync(db)
            : await TestSessionHelper.CreateUserSessionAsync(db);
    }

    private async Task<Guid> SeedOwnedGameAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"#4519 {Guid.NewGuid():N}");
        await TestSessionHelper.SeedOwnedLibraryGameAsync(db, userId, gameId);
        return gameId;
    }

    /// <summary>
    /// Un gioco con la KB pronta: la creazione di una sessione risponde 422 altrimenti. Lo stesso seeding
    /// di <c>CreateSessionEndpointIdorIntegrationTests</c>.
    /// </summary>
    private async Task<Guid> SeedKbReadyGameAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"#4519 {Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            IsActiveForRag = true,
        });
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            SharedGameId = gameId,
            IndexingStatus = "completed",
            ChunkCount = 1,
            IndexedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return gameId;
    }

    private async Task<Guid> SeedMechanicAnalysisAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"#4519 {Guid.NewGuid():N}");
        var analysis = MechanicAnalysis.Create(
            sharedGameId: gameId,
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);
        await scope.ServiceProvider.GetRequiredService<IMechanicAnalysisRepository>().AddAsync(analysis);
        await db.SaveChangesAsync();
        return analysis.Id;
    }
}
