using System.Collections;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// #4568 — nessuna rotta di amministrazione deve rispondere a un utente normale. «Di amministrazione» vuol dire con
/// un segmento <c>admin</c> nel modello: tutto <c>/api/v1/admin/**</c>, e anche rotte come
/// <c>/api/v1/auth/admin/2fa/disable</c>. #4584: più le rotte da amministratore che quel segmento non ce l'hanno,
/// elencate per prefisso in <see cref="AdminRoutePrefixesWithoutAdminSegment"/>.
/// </summary>
/// <remarks>
/// <para>
/// Il gruppo <c>/admin/alert-configuration</c> era protetto solo da <c>.RequireAuthorization()</c>: bastava
/// essere autenticati. Nessun avviso all'avvio, nessun test: la rotta sembrava protetta perché un utente anonimo
/// riceveva 401.
/// </para>
/// <para>
/// <b>Perché una sonda HTTP e non la lettura dei metadati.</b> Il meccanismo più diffuso nel repo è il filtro
/// <c>RequireAdminSessionFilter</c> (per endpoint con <c>.RequireAdminSession()</c> o sul gruppo con
/// <c>AddEndpointFilter</c>), e un filtro di endpoint <b>non</b> lascia traccia nei metadati della rotta: entra
/// nel <c>RequestDelegate</c> compilato. Ci sono poi i controlli imperativi dentro l'handler. Un test sui
/// metadati vedrebbe protette solo le rotte con una policy ASP.NET. Questa sonda chiede invece all'host vero
/// come risponde: per ogni rotta, una richiesta con la sessione di un utente normale, i parametri di rotta e di
/// query riempiti con valori del tipo giusto, e un corpo vuoto del tipo che la rotta accetta.
/// </para>
/// <para>
/// Lettura delle risposte: 401 o 403 vuol dire protetta, comunque sia implementata la protezione (policy,
/// filtro, controllo nell'handler o nel comando). Ogni altro stato vuol dire che l'handler è stato raggiunto. Fa
/// eccezione la lettura del corpo: se il corpo non si deserializza, o il tipo di contenuto non è quello atteso,
/// ASP.NET risponde 400 o 415 <b>prima</b> dei filtri di endpoint, e la sonda non sa dire se la rotta è protetta.
/// Per questo prova due corpi JSON (vedi <see cref="ProbeAdminRouteAsync"/>). Se nessuno dei due passa, la rotta
/// fallisce comunque: o si rende leggibile il corpo, o la si dichiara in <see cref="AdminAuthorizationExempt"/>
/// con il motivo.
/// </para>
/// <para>
/// Le rotte esenti non vengono chiamate. Una voce esente che non corrisponde più a nessuna rotta fa fallire il
/// test, come una rotta nuova non protetta.
/// </para>
/// </remarks>
public sealed partial class NoDuplicateRouteTests
{
    /// <summary>
    /// Le rotte admin che un utente normale può raggiungere, con il motivo. La chiave è «METODO modello», con il
    /// modello come lo scrive la tabella di routing, senza la barra iniziale.
    /// </summary>
    private static readonly Dictionary<string, string> AdminAuthorizationExempt = new(StringComparer.Ordinal)
    {
        // Vuoto: #4579 ne ha tolto le due voci (una rotta registrata due volte, che la sonda non poteva leggere).
        // Una voce nuova deve dire perché la rotta non si può sondare e quale issue la chiude.
    };

    /// <summary>
    /// Prefissi di rotte da amministratore che non hanno un segmento <c>admin</c> nel percorso, e che la sonda
    /// quindi non vedrebbe (#4584: le metriche di <c>rag-dashboard</c> chiedevano solo una sessione). Una rotta nuova
    /// sotto questi prefissi entra nella sonda da sola.
    /// <para>
    /// Non c'è il prefisso del gruppo <c>rag-dashboard</c> intero: <c>/config</c> è la configurazione dell'utente e
    /// <c>/options</c> è anonima di proposito, quindi un utente normale le raggiunge per scelta. Un prefisso va
    /// aggiunto qui solo se tutto ciò che sta sotto è da amministratore; un prefisso che non corrisponde più a
    /// nessuna rotta fa fallire il test.
    /// </para>
    /// </summary>
    private static readonly string[] AdminRoutePrefixesWithoutAdminSegment =
    [
        "api/v1/rag-dashboard/overview",
        "api/v1/rag-dashboard/strategies",
        "api/v1/rag-dashboard/query",
    ];

    private static readonly TimeSpan AdminProbeTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task EveryAdminRoute_RejectsAPlainUser()
    {
        var services = _factory.Services;
        var isService = services.GetRequiredService<IServiceProviderIsService>();
        var dataSource = services.GetRequiredService<EndpointDataSource>();

        string userToken;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, userToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        }

        using var client = _factory.CreateClient();

        // La sonda legge 401 e 403 allo stesso modo. Se il cookie non fosse riconosciuto, ogni rotta darebbe 401 e
        // il test passerebbe a vuoto, cioè proprio nella forma di #4568 (RequireAuthorization() risponde 401 a un
        // anonimo). Prima di tutto, quindi, la sessione dell'utente normale deve essere una sessione valida.
        using (var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            me.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={userToken}");
            using var meResponse = await client.SendAsync(me);
            meResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                "the probe session must be recognised, or every admin route would answer 401 and the probe would prove nothing");
        }

        var protectedRoutes = new List<string>();
        var reached = new List<string>();
        var undecided = new List<string>();
        var exemptSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var template = (endpoint.RoutePattern.RawText ?? string.Empty).Trim('/');
            if (!IsAdminRoute(template))
            {
                continue;
            }

            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
            {
                var key = $"{method} {template}";
                if (AdminAuthorizationExempt.ContainsKey(key))
                {
                    exemptSeen.Add(key);
                    continue;
                }

                var outcome = await ProbeAdminRouteAsync(client, endpoint, method, isService, userToken);
                switch (outcome.Kind)
                {
                    case AdminProbeKind.Protected:
                        protectedRoutes.Add(key);
                        break;
                    case AdminProbeKind.Undecided:
                        undecided.Add($"{key} → {outcome.Detail}");
                        break;
                    default:
                        reached.Add($"{key} → {outcome.Detail}");
                        break;
                }
            }
        }

        // Guardia di non-vacuità: la sonda deve vedere le rotte admin e distinguerle. Il gruppo di #4568 è
        // protetto dal filtro di endpoint, cioè proprio dal meccanismo che i metadati non mostrano.
        protectedRoutes.Should().Contain(
            [
                "GET api/v1/admin/alert-configuration",
                "PUT api/v1/admin/alert-configuration",
                // #4579: la rotta che era registrata due volte e per questo esente. Rimasta una sola, la sonda la legge.
                "GET api/v1/admin/shared-games/{gameId:guid}/documents/overview",
                // #4584: rotte da amministratore senza il segmento admin nel percorso, che la sonda legge per prefisso.
                "GET api/v1/rag-dashboard/overview",
                "GET api/v1/rag-dashboard/strategies/{strategyId}/metrics",
                "GET api/v1/rag-dashboard/strategies/{strategyId}/time-series",
                "GET api/v1/rag-dashboard/strategies/compare",
                "POST api/v1/rag-dashboard/query/stream",
            ],
            "the probe must see the routes of #4568, #4579 and #4584 and read them as protected, or it proves nothing");

        var staleExemptions = AdminAuthorizationExempt.Keys.Where(k => !exemptSeen.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var routeTemplates = dataSource.Endpoints.OfType<RouteEndpoint>()
            .Select(e => (e.RoutePattern.RawText ?? string.Empty).Trim('/'))
            .ToList();
        var stalePrefixes = AdminRoutePrefixesWithoutAdminSegment
            .Where(prefix => !routeTemplates.Any(template => MatchesPrefix(template, prefix)))
            .Order(StringComparer.Ordinal)
            .ToList();

        reached.Sort(StringComparer.Ordinal);
        undecided.Sort(StringComparer.Ordinal);
        reached.Should().BeEmpty(
            "an admin route must answer 401/403 to a user who is not an admin (#4568). Protect it " +
            "(.RequireAdminSession(), or AddEndpointFilter<RequireAdminSessionFilter>() on the group), or declare it " +
            "in AdminAuthorizationExempt with the reason. Reached by a plain user: " + string.Join(" ;; ", reached));
        undecided.Should().BeEmpty(
            "the probe could not tell whether these routes are protected: ASP.NET rejected the body before the " +
            "endpoint filters ran. Make the probe body readable for them, or declare them in AdminAuthorizationExempt " +
            "with the reason. Undecided: " + string.Join(" ;; ", undecided));
        staleExemptions.Should().BeEmpty(
            "an exemption that matches no route is stale and would hide the next route registered under that name");
        stalePrefixes.Should().BeEmpty(
            "a prefix in AdminRoutePrefixesWithoutAdminSegment that matches no route is stale: the routes it was " +
            "meant to cover were renamed or removed, and the probe no longer sees them");
    }

    private enum AdminProbeKind
    {
        Protected,
        Reached,
        Undecided,
    }

    /// <summary>
    /// Chiama la rotta con la sessione dell'utente normale. Un corpo JSON si prova in due forme: prima con ogni
    /// proprietà valorizzata (serve ai membri <c>required</c>), poi, se ASP.NET lo rifiuta, vuoto (serve ai value
    /// object che validano nel costruttore: un campione generico non rispetta i loro intervalli, un oggetto assente
    /// sì). Basta una risposta 401/403 per dire «protetta»; basta uno stato diverso da 400/415 per dire «raggiunta».
    /// </summary>
    private static async Task<(AdminProbeKind Kind, string Detail)> ProbeAdminRouteAsync(
        HttpClient client, RouteEndpoint endpoint, string method, IServiceProviderIsService isService, string userToken)
    {
        var details = new List<string>();
        foreach (var emptyJson in new[] { false, true })
        {
            using var request = BuildProbeRequest(endpoint, method, isService, emptyJson);
            if (emptyJson && request.Content is not StringContent { Headers.ContentType.MediaType: "application/json" })
            {
                break;
            }

            request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={userToken}");
            using var timeout = new CancellationTokenSource(AdminProbeTimeout);
            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        return (AdminProbeKind.Protected, string.Empty);
                    case HttpStatusCode.BadRequest:
                    case HttpStatusCode.UnsupportedMediaType:
                        details.Add($"{(int)response.StatusCode} ({request.RequestUri})");
                        continue;
                    default:
                        return (AdminProbeKind.Reached, $"{(int)response.StatusCode} ({request.RequestUri})");
                }
            }
            catch (OperationCanceledException)
            {
                return (AdminProbeKind.Reached, $"nessuna risposta in {AdminProbeTimeout.TotalSeconds:0}s ({request.RequestUri})");
            }
#pragma warning disable CA1031 // Una rotta che fa esplodere la richiesta è stata raggiunta: va nel resoconto, non deve fermare la sonda.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                return (AdminProbeKind.Reached, $"{ex.GetType().Name} ({request.RequestUri})");
            }
        }

        return (AdminProbeKind.Undecided, string.Join(", then ", details));
    }

    private static bool IsAdminRoute(string template) =>
        template.Split('/').Any(segment => string.Equals(segment, "admin", StringComparison.OrdinalIgnoreCase))
        || AdminRoutePrefixesWithoutAdminSegment.Any(prefix => MatchesPrefix(template, prefix));

    private static bool MatchesPrefix(string template, string prefix) =>
        template.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || template.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// La richiesta della sonda: parametri di rotta e di query del tipo che l'handler dichiara, e un corpo vuoto
    /// del tipo che la rotta accetta.
    /// </summary>
    private static HttpRequestMessage BuildProbeRequest(
        RouteEndpoint endpoint, string method, IServiceProviderIsService isService, bool emptyJson)
    {
        var handler = endpoint.Metadata.GetMetadata<MethodInfo>();
        var handlerParameters = handler?.GetParameters() ?? [];

        var path = new StringBuilder();
        foreach (var segment in endpoint.RoutePattern.PathSegments)
        {
            path.Append('/');
            foreach (var part in segment.Parts)
            {
                switch (part)
                {
                    case Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal:
                        path.Append(literal.Content);
                        break;
                    case Microsoft.AspNetCore.Routing.Patterns.RoutePatternSeparatorPart separator:
                        path.Append(separator.Content);
                        break;
                    case Microsoft.AspNetCore.Routing.Patterns.RoutePatternParameterPart parameter:
                        var declared = handlerParameters.FirstOrDefault(p =>
                            string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))?.ParameterType;
                        path.Append(Uri.EscapeDataString(SampleValue(declared, parameter.ParameterPolicies.Select(pp => pp.Content ?? string.Empty), parameter.Name)));
                        break;
                }
            }
        }

        var routeNames = new HashSet<string>(
            endpoint.RoutePattern.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var query = new List<string>();
        foreach (var parameter in handlerParameters)
        {
            AddQueryValues(query, parameter.Name!, parameter.ParameterType, parameter.GetCustomAttributes(inherit: true), routeNames, isService);
        }

        var uri = path.ToString() + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        var request = new HttpRequestMessage(new HttpMethod(method), uri);

        var accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
        if (accepts is not null && accepts.ContentTypes.Count > 0)
        {
            request.Content = ProbeBody(accepts, handlerParameters, emptyJson);
        }

        return request;
    }

    /// <summary>
    /// Un corpo che ASP.NET riesce a leggere, così la richiesta arriva ai filtri di endpoint: un oggetto JSON con
    /// ogni proprietà valorizzata (i membri <c>required</c> mancanti fanno fallire la deserializzazione), un form
    /// con un file per ogni parametro <see cref="IFormFile"/>, oppure testo nel tipo di contenuto dichiarato.
    /// </summary>
    private static HttpContent ProbeBody(IAcceptsMetadata accepts, ParameterInfo[] handlerParameters, bool emptyJson)
    {
        var contentType = accepts.ContentTypes[0];
        if (accepts.ContentTypes.Any(c => c.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            var json = emptyJson ? "{}" : SampleJson(accepts.RequestType, depth: 0);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (accepts.ContentTypes.Any(c => c.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)))
        {
            var form = new MultipartFormDataContent { { new StringContent("x"), "probe" } };
            foreach (var parameter in handlerParameters.Where(p =>
                typeof(IFormFile).IsAssignableFrom(p.ParameterType) || typeof(IFormFileCollection).IsAssignableFrom(p.ParameterType)))
            {
                var name = parameter.GetCustomAttributes(inherit: true).OfType<IFromFormMetadata>().FirstOrDefault()?.Name
                    ?? parameter.Name!;
                form.Add(new ByteArrayContent([0x50, 0x4B]), name, "probe.bin");
            }

            return form;
        }

        if (accepts.ContentTypes.Any(c => c.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)))
        {
            return new FormUrlEncodedContent([new KeyValuePair<string, string>("probe", "x")]);
        }

        return new StringContent("x", Encoding.UTF8, contentType);
    }

    /// <summary>Un valore JSON del tipo dato, con ogni proprietà valorizzata fino a una profondità limitata.</summary>
    private static string SampleJson(Type? type, int depth, string? name = null)
    {
        if (type is null || type == typeof(object))
        {
            return "{}";
        }

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(bool))
        {
            return "true";
        }

        if (underlying.IsPrimitive || underlying == typeof(decimal))
        {
            return "1";
        }

        if (IsSimple(underlying))
        {
            return JsonSerializer.Serialize(SampleValue(underlying, [], name));
        }

        if (IsDictionary(underlying))
        {
            return "{}";
        }

        if (typeof(IEnumerable).IsAssignableFrom(underlying))
        {
            return "[]";
        }

        if (depth >= 3)
        {
            return "{}";
        }

        var members = underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => $"{JsonSerializer.Serialize(p.Name)}:{SampleJson(p.PropertyType, depth + 1, p.Name)}");
        return "{" + string.Join(",", members) + "}";
    }

    private static bool IsDictionary(Type type) =>
        type.GetInterfaces().Append(type).Any(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    private static void AddQueryValues(
        List<string> query, string name, Type type, object[] attributes, HashSet<string> routeNames, IServiceProviderIsService isService)
    {
        if (attributes.OfType<AsParametersAttribute>().Any())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                AddQueryValues(query, property.Name, property.PropertyType, property.GetCustomAttributes(inherit: true), routeNames, isService);
            }

            return;
        }

        var fromQuery = attributes.OfType<IFromQueryMetadata>().FirstOrDefault();
        if (fromQuery is null && attributes.Any(a => a is IFromRouteMetadata or IFromBodyMetadata or IFromHeaderMetadata
                or IFromFormMetadata or IFromServiceMetadata))
        {
            return;
        }

        var queryName = string.IsNullOrEmpty(fromQuery?.Name) ? name : fromQuery!.Name!;
        if (fromQuery is null && routeNames.Contains(name))
        {
            return;
        }

        var element = type.IsArray ? type.GetElementType()! : type;
        if (element.IsGenericType && element.GetGenericTypeDefinition() == typeof(List<>))
        {
            element = element.GetGenericArguments()[0];
        }

        var underlying = Nullable.GetUnderlyingType(element) ?? element;
        if (!IsSimple(underlying) || IsSpecial(underlying) || (fromQuery is null && isService.IsService(underlying)))
        {
            return;
        }

        query.Add($"{Uri.EscapeDataString(queryName)}={Uri.EscapeDataString(SampleValue(underlying, [], queryName))}");
    }

    private static bool IsSpecial(Type type) =>
        type == typeof(HttpContext) || type == typeof(HttpRequest) || type == typeof(HttpResponse)
        || type == typeof(CancellationToken) || type == typeof(ClaimsPrincipal)
        || typeof(IFormFile).IsAssignableFrom(type) || typeof(IFormFileCollection).IsAssignableFrom(type)
        || typeof(IFormCollection).IsAssignableFrom(type) || typeof(Stream).IsAssignableFrom(type);

    private static bool IsSimple(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly)
        || type == typeof(TimeOnly) || type == typeof(TimeSpan) || type == typeof(Uri);

    /// <summary>Un valore che il binding del tipo dichiarato accetta. Senza tipo, decidono i vincoli di rotta.</summary>
    private static string SampleValue(Type? declared, IEnumerable<string> constraints, string? name = null)
    {
        var type = declared is null ? null : Nullable.GetUnderlyingType(declared) ?? declared;
        var constraintList = constraints.ToList();

        // Una stringa che si chiama «…Id» di solito viene letta come Guid dall'handler.
        if (type == typeof(Guid) || constraintList.Contains("guid")
            || (type == typeof(string) && name is not null && name.EndsWith("Id", StringComparison.Ordinal)))
        {
            return Guid.NewGuid().ToString();
        }

        if (type == typeof(bool) || constraintList.Contains("bool"))
        {
            return "true";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || constraintList.Contains("datetime"))
        {
            return "2026-01-01T00:00:00Z";
        }

        if (type == typeof(DateOnly))
        {
            return "2026-01-01";
        }

        if (type == typeof(TimeOnly))
        {
            return "00:00";
        }

        if (type == typeof(TimeSpan))
        {
            return "00:01:00";
        }

        if (type is { IsEnum: true })
        {
            return Enum.GetNames(type).First();
        }

        if ((type is not null && (type.IsPrimitive || type == typeof(decimal)))
            || constraintList.Any(c => c is "int" or "long" or "decimal" or "double" or "float"
                || c.StartsWith("min(", StringComparison.Ordinal) || c.StartsWith("range(", StringComparison.Ordinal)))
        {
            return "1";
        }

        return "x";
    }
}
