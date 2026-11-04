using System.Reflection;
using System.Text;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// La fixture di <see cref="NoDuplicateRouteTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class NoDuplicateRouteTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "dup_routes");

/// <summary>
/// Regression guard for duplicate route registrations.
///
/// Two endpoints registered on the same (method, template) do not fail at startup:
/// ASP.NET throws <c>AmbiguousMatchException</c> at REQUEST time, so the endpoint
/// looks healthy until someone calls it and gets a 500. It happened to
/// <c>POST /admin/impersonation/end</c>, registered both by the dedicated
/// impersonation file and by the user-activity file composed into
/// MapAdminUserEndpoints — leaving admins unable to end an impersonation session.
///
/// <see cref="GameNightDiaryRouteUniquenessTests"/> guards one specific route;
/// this one covers the whole table, so the next duplicate fails in CI instead of
/// in production.
///
/// The class also hosts the route-parameter binding guard of #4543: it reads the same
/// routing table, so it rides on this host instead of paying for another one. For the same
/// reason it hosts the admin-authorization probe of #4568 (NoDuplicateRouteTests.AdminAuthorization.cs).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed partial class NoDuplicateRouteTests
    : IClassFixture<NoDuplicateRouteTestsHostFixture>, IAsyncLifetime
{
    private readonly NoDuplicateRouteTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public NoDuplicateRouteTests(NoDuplicateRouteTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        _ = _factory.Services; // force host build so the EndpointDataSource is populated
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// #4579 — il confronto è sul modello <b>senza i nomi dei parametri</b>, con i vincoli tenuti. Prima usava
    /// <c>RoutePattern.RawText</c>, che contiene il nome: <c>…/{gameId:guid}/documents/overview</c> e
    /// <c>…/{sharedGameId:guid}/documents/overview</c> risultavano due modelli diversi, mentre per il routing
    /// sono lo stesso (stessi segmenti, stesso vincolo <c>guid</c>) e ogni richiesta finiva in
    /// <c>AmbiguousMatchException</c>. Il vincolo resta nella chiave: <c>{id}</c> e <c>{id:guid}</c> hanno chiavi
    /// diverse e la guardia non le segnala. Che il routing le consideri distinte non è provato qui; sulla tabella
    /// reale la scelta non cambia l'esito, perché non ci sono coppie di questo tipo.
    /// </summary>
    [Fact]
    public void NoRouteTemplate_IsRegisteredTwiceForTheSameHttpMethod()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var duplicates = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e =>
                (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "*" })
                    .Select(method => new
                    {
                        Key = $"{method} {TemplateWithoutParameterNames(e.RoutePattern)}",
                        // Il testo grezzo nel resoconto: è lì che si vedono i due nomi diversi.
                        Endpoint = $"{e.DisplayName ?? "(senza nome)"} [{e.RoutePattern.RawText}]"
                    }))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} → {g.Count()} registrazioni: {string.Join(" | ", g.Select(x => x.Endpoint))}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // The duplicates are listed in the failure reason on purpose: the assertion
        // message truncates the collection, and a guard that says "there is a
        // duplicate" without naming it sends the reader back to square one.
        duplicates.Should().BeEmpty(
            "a route registered twice for the same method throws AmbiguousMatchException (HTTP 500) " +
            "on every request, while the endpoint looks correctly registered. The comparison ignores " +
            "parameter names ({gameId:guid} and {sharedGameId:guid} are the same route) and keeps the " +
            "constraints. Duplicates found: " +
            string.Join(" ;; ", duplicates));
    }

    /// <summary>
    /// La chiave di confronto di una rotta: segmenti letterali e separatori com'è, e di ogni parametro solo
    /// catch-all, vincoli, opzionalità e valore predefinito — senza il nome. Senza la barra iniziale, che dipende
    /// da come è stato composto il gruppo. Due modelli con la stessa chiave differiscono al più per i nomi.
    /// </summary>
    internal static string TemplateWithoutParameterNames(RoutePattern pattern)
    {
        var template = new StringBuilder();
        foreach (var segment in pattern.PathSegments)
        {
            template.Append('/');
            foreach (var part in segment.Parts)
            {
                switch (part)
                {
                    case RoutePatternLiteralPart literal:
                        template.Append(literal.Content);
                        break;
                    case RoutePatternSeparatorPart separator:
                        template.Append(separator.Content);
                        break;
                    case RoutePatternParameterPart parameter:
                        template.Append('{');
                        if (parameter.IsCatchAll)
                        {
                            template.Append('*');
                        }

                        foreach (var policy in parameter.ParameterPolicies)
                        {
                            template.Append(':').Append(policy.Content);
                        }

                        if (parameter.IsOptional)
                        {
                            template.Append('?');
                        }

                        if (parameter.Default is not null)
                        {
                            template.Append('=').Append(parameter.Default);
                        }

                        template.Append('}');
                        break;
                }
            }
        }

        return template.ToString().TrimStart('/');
    }

    /// <summary>
    /// Pins the #3840 decision, as it stands after #4448: the host ratifying another player's
    /// proposal lives on <c>/scores/proposals/confirm</c> and broadcasts over SignalR.
    ///
    /// <c>/scores/confirm</c> used to confirm a dictated score reading; #4448 (A7) retired it with
    /// the dictated-score flow, and <c>EndpointContractTests.RetiredRoutes</c> keeps it unmounted.
    /// Moving the ratification back onto <c>/scores/confirm</c> would undo #3840 under a retired path.
    /// </summary>
    [Fact]
    public void TheHostRatification_IsRegisteredExactlyOnce()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        // RawText may or may not carry the leading slash depending on how the group was
        // composed; normalising here keeps the assertion about the route, not its spelling.
        var postTemplates = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true)
            .Select(e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .ToList();

        postTemplates.Should().ContainSingle(
            t => string.Equals(t, "api/v1/live-sessions/{sessionId}/scores/proposals/confirm", StringComparison.OrdinalIgnoreCase),
            "the host-facing proposal ratification (ConfirmScoreProposalCommand) must stay reachable exactly once");
    }

    /// <summary>
    /// #4543 — ogni parametro di una rotta deve avere nel metodo un parametro che lo legge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le minimal API legano un parametro alla rotta per <b>nome</b>. Se il metodo chiama
    /// <c>Guid _</c> quello che la rotta chiama <c>{id:guid}</c>, il binding lo cerca nella query
    /// string, non lo trova e risponde 400 a corpo vuoto prima dell'handler. Nessun avviso
    /// all'avvio: l'endpoint sembra registrato bene. Così erano rotti PUT e DELETE delle errata e
    /// delle FAQ del catalogo, l'attivazione di un documento e due rotte dei template di stato.
    /// </para>
    /// <para>
    /// La regola letta qui è quella del binding: un parametro senza attributo con lo stesso nome
    /// del segmento, oppure <c>[FromRoute]</c> (con o senza <c>Name</c>), anche dentro un
    /// <c>[AsParameters]</c>. Un parametro con un'altra sorgente esplicita (<c>[FromQuery]</c>,
    /// <c>[FromBody]</c>, …) non conta, anche se il nome coincide. Un segmento che nessun
    /// parametro legge non è sempre un 400, ma è sempre un valore che l'handler non vede: se è
    /// voluto, il metodo lo dichiara e lo ignora.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryRouteParameter_IsBoundByAHandlerParameter()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var inspected = new List<string>();
        var unbound = new List<string>();

        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var handler = endpoint.Metadata.GetMetadata<MethodInfo>();
            if (handler is null || endpoint.RoutePattern.Parameters.Count == 0)
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"];
            var name = $"{string.Join(",", methods)} {(endpoint.RoutePattern.RawText ?? string.Empty).Trim('/')}";
            inspected.Add(name);

            var bound = RouteBoundNames(handler);
            var missing = endpoint.RoutePattern.Parameters
                .Select(p => p.Name)
                .Where(n => !bound.Contains(n))
                .ToList();

            if (missing.Count > 0)
            {
                var signature = string.Join(", ", handler.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                unbound.Add(
                    $"{name} → {handler.DeclaringType?.Name}.{handler.Name}({signature}) non legge {{{string.Join("}, {", missing)}}}");
            }
        }

        // Guardia di non-vacuità: se la metadata MethodInfo sparisse, o la scansione non vedesse più
        // le rotte del catalogo, l'asserzione sotto passerebbe su una lista vuota.
        inspected.Should().Contain(
            [
                "PUT api/v1/admin/shared-games/{id:guid}/errata/{errataId:guid}",
                "DELETE api/v1/admin/shared-games/{id:guid}/errata/{errataId:guid}",
            ],
            "the scan must see the routes of #4543, or it proves nothing");

        unbound.Sort(StringComparer.Ordinal);
        unbound.Should().BeEmpty(
            "a route segment that no handler parameter binds by name is looked up in the query string, and a " +
            "required one answers 400 before the handler (#4543). Rename the parameter after the segment. Found: " +
            string.Join(" ;; ", unbound));
    }

    /// <summary>
    /// I nomi di segmento che i parametri di <paramref name="handler"/> leggono dalla rotta.
    /// </summary>
    private static HashSet<string> RouteBoundNames(MethodInfo handler)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in handler.GetParameters())
        {
            if (parameter.GetCustomAttribute<AsParametersAttribute>() is not null)
            {
                foreach (var property in parameter.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    AddIfFromRoute(names, property.Name, property.GetCustomAttributes(inherit: true));
                }

                // Un record posizionale porta gli attributi sul parametro del costruttore.
                var constructor = parameter.ParameterType.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();
                foreach (var constructorParameter in constructor?.GetParameters() ?? [])
                {
                    AddIfFromRoute(names, constructorParameter.Name!, constructorParameter.GetCustomAttributes(inherit: true));
                }

                continue;
            }

            AddIfFromRoute(names, parameter.Name!, parameter.GetCustomAttributes(inherit: true));
        }

        return names;
    }

    private static void AddIfFromRoute(HashSet<string> names, string name, object[] attributes)
    {
        var fromRoute = attributes.OfType<IFromRouteMetadata>().FirstOrDefault();
        if (fromRoute is not null)
        {
            names.Add(string.IsNullOrEmpty(fromRoute.Name) ? name : fromRoute.Name);
            return;
        }

        var otherSource = attributes.Any(a => a is IFromQueryMetadata
            or IFromBodyMetadata
            or IFromHeaderMetadata
            or IFromFormMetadata
            or IFromServiceMetadata);
        if (!otherSource)
        {
            names.Add(name);
        }
    }
}
