using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola (vedi <see cref="IntegrationHostFixture"/>).
/// <para>
/// Condividere il database è sicuro qui: ogni test crea i propri utenti e lavora su una chiave o un nome di
/// regola propri (<c>Test4568.{guid}</c>), e le asserzioni leggono solo le righe di quella chiave o di quel
/// nome. Le letture non filtrate (<c>GET</c> degli elenchi e di una categoria) sono controllate solo nello
/// stato, e per la categoria dopo aver scritto una riga in quella categoria.
/// </para>
/// </summary>
public sealed class AlertAdminRoutesAuthorizationHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "alert_admin_authz");

/// <summary>
/// #4568: le rotte di amministrazione degli alert erano protette solo da <c>.RequireAuthorization()</c>, cioè
/// bastava essere autenticati.
/// <list type="bullet">
///   <item><c>/admin/alert-configuration</c>: un utente normale leggeva e scriveva host SMTP, webhook Slack e
///     chiavi PagerDuty.</item>
///   <item><c>/admin/alert-rules</c> (tranne <c>/{id}/test</c>), <c>/admin/alert-templates</c> e
///     <c>/admin/alert-test</c>: lo stesso, per le regole di alert e per l'invio di un alert di prova.</item>
/// </list>
/// E le stesse rotte salvavano come autore sempre <c>"system"</c>: leggevano il claim <c>"userId"</c>, che il
/// gestore della sessione non emette.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4568")]
public sealed class AlertAdminRoutesAuthorizationIntegrationTests
    : IClassFixture<AlertAdminRoutesAuthorizationHostFixture>
{
    private const string ConfigurationPath = "api/v1/admin/alert-configuration";
    private const string RulesPath = "api/v1/admin/alert-rules";

    /// <summary>I prefissi delle rotte di #4568. Le rotte nuove sotto questi prefissi entrano nel test da sole.</summary>
    private static readonly string[] AlertAdminPrefixes =
    [
        ConfigurationPath,
        RulesPath,
        "api/v1/admin/alert-templates",
        "api/v1/admin/alert-test",
    ];

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AlertAdminRoutesAuthorizationIntegrationTests(AlertAdminRoutesAuthorizationHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task EveryAlertAdminRoute_RejectsWhoIsNotAdmin()
    {
        var routes = AlertAdminRoutes();

        // Guardia di non-vacuità: se la lettura della tabella di routing non vedesse più queste rotte, il ciclo
        // sotto non proverebbe nulla.
        routes.Select(r => $"{r.Method} {r.Template}").Should().Contain(
            [
                $"GET {ConfigurationPath}",
                $"GET {ConfigurationPath}/{{category}}",
                $"PUT {ConfigurationPath}",
                $"GET {RulesPath}",
                $"GET {RulesPath}/{{id:guid}}",
                $"POST {RulesPath}",
                $"PUT {RulesPath}/{{id:guid}}",
                $"DELETE {RulesPath}/{{id:guid}}",
                $"PATCH {RulesPath}/{{id:guid}}/toggle",
                $"POST {RulesPath}/{{id:guid}}/test",
                "GET api/v1/admin/alert-templates",
                "POST api/v1/admin/alert-test",
            ],
            "the scan must see the routes of #4568, or it proves nothing");

        var (_, adminToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (_, userToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var (_, editorToken) = await CreateSessionAsync(TestSessionHelper.CreateEditorSessionAsync);
        var name = $"Test4568.{Guid.NewGuid():N}";

        // Una regola vera, così una DELETE o un PATCH che passassero lascerebbero una traccia controllabile.
        var ruleId = await CreateRuleAsync(adminToken, name);

        var failures = new List<string>();
        foreach (var (method, template) in routes)
        {
            failures.Add(await ProbeAsync(method, template, ruleId, name, "anonymous", token: null, HttpStatusCode.Unauthorized));
            failures.Add(await ProbeAsync(method, template, ruleId, name, "user", userToken, HttpStatusCode.Forbidden));
            failures.Add(await ProbeAsync(method, template, ruleId, name, "editor", editorToken, HttpStatusCode.Forbidden));
        }

        failures.RemoveAll(f => f.Length == 0);
        failures.Should().BeEmpty(
            "alert configurations hold SMTP hosts, Slack webhooks and PagerDuty keys, and alert rules decide who " +
            "gets paged: only an admin may read or change them. Before #4568 any authenticated user got through. " +
            "Found: " + string.Join(" ;; ", failures));

        (await ReadConfigurationRowsAsync(name)).Should().BeEmpty("a rejected PUT must not write the configuration");
        var rules = await ReadRuleRowsAsync(name);
        rules.Should().ContainSingle("a rejected POST must not add a rule, and a rejected DELETE must not remove it");
        rules[0].IsEnabled.Should().BeTrue("a rejected PATCH /toggle must not disable the rule");
        rules[0].Severity.Should().Be("Warning", "a rejected PUT must not change the rule");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task Admin_CanReadAndWriteTheConfiguration(string role)
    {
        var (_, token) = await CreateSessionAsync(role == "Admin"
            ? TestSessionHelper.CreateAdminSessionAsync
            : TestSessionHelper.CreateSuperAdminSessionAsync);
        var key = $"Test4568.{Guid.NewGuid():N}";

        var put = await SendAsync(HttpMethod.Put, $"/{ConfigurationPath}", token, ConfigurationBody(key));
        put.Status.Should().Be(HttpStatusCode.OK, $"an {role} may write a configuration. Body: {put.Body}");

        var all = await SendAsync(HttpMethod.Get, $"/{ConfigurationPath}", token);
        all.Status.Should().Be(HttpStatusCode.OK, $"an {role} may read every configuration. Body: {all.Body}");
        all.Body.Should().Contain(key, "the list includes the configuration just written");

        var byCategory = await SendAsync(HttpMethod.Get, $"/{ConfigurationPath}/Slack", token);
        byCategory.Status.Should().Be(HttpStatusCode.OK,
            $"an {role} may read a category that has a configuration. Body: {byCategory.Body}");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task Admin_CanUseTheAlertRules(string role)
    {
        var (_, token) = await CreateSessionAsync(role == "Admin"
            ? TestSessionHelper.CreateAdminSessionAsync
            : TestSessionHelper.CreateSuperAdminSessionAsync);
        var name = $"Test4568.{Guid.NewGuid():N}";

        var id = await CreateRuleAsync(token, name);

        (await SendAsync(HttpMethod.Get, $"/{RulesPath}", token)).Status.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/{RulesPath}/{id}", token)).Status.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Put, $"/{RulesPath}/{id}", token, RuleUpdateBody(name))).Status
            .Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Patch, $"/{RulesPath}/{id}/toggle", token)).Status
            .Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, "/api/v1/admin/alert-templates", token)).Status
            .Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, $"/{RulesPath}/{id}", token)).Status
            .Should().Be(HttpStatusCode.NoContent);

        (await ReadRuleRowsAsync(name)).Should().BeEmpty("the admin deleted the rule");
    }

    [Fact]
    public async Task ConfigurationPut_RecordsTheAdminAsUpdatedBy()
    {
        var (adminId, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var key = $"Test4568.{Guid.NewGuid():N}";

        var put = await SendAsync(HttpMethod.Put, $"/{ConfigurationPath}", token, ConfigurationBody(key));
        put.Status.Should().Be(HttpStatusCode.OK, $"setup: an admin may write. Body: {put.Body}");

        var row = (await ReadConfigurationRowsAsync(key)).Should().ContainSingle().Subject;
        row.UpdatedBy.Should().Be(adminId.ToString(),
            "the author is the admin who sent the PUT. Before #4568 the endpoint read the claim \"userId\", which " +
            "the session handler does not emit, and every row said \"system\"");
    }

    [Fact]
    public async Task AlertRules_RecordTheAdminWhoActedAsAuthor()
    {
        var (creatorId, creatorToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (editorId, editorToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (togglerId, togglerToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var name = $"Test4568.{Guid.NewGuid():N}";

        var id = await CreateRuleAsync(creatorToken, name);
        var created = (await ReadRuleRowsAsync(name)).Should().ContainSingle().Subject;
        created.CreatedBy.Should().Be(creatorId.ToString(),
            "the creator is the admin who sent the POST. Before #4568 every rule said \"system\"");
        created.UpdatedBy.Should().Be(creatorId.ToString());

        var update = await SendAsync(HttpMethod.Put, $"/{RulesPath}/{id}", editorToken, RuleUpdateBody(name));
        update.Status.Should().Be(HttpStatusCode.NoContent, $"setup: an admin may edit. Body: {update.Body}");
        var updated = (await ReadRuleRowsAsync(name)).Should().ContainSingle().Subject;
        updated.UpdatedBy.Should().Be(editorId.ToString(), "the last author is the admin who sent the PUT");
        updated.CreatedBy.Should().Be(creatorId.ToString(), "an edit does not change the creator");

        var toggle = await SendAsync(HttpMethod.Patch, $"/{RulesPath}/{id}/toggle", togglerToken);
        toggle.Status.Should().Be(HttpStatusCode.NoContent, $"setup: an admin may toggle. Body: {toggle.Body}");
        var toggled = (await ReadRuleRowsAsync(name)).Should().ContainSingle().Subject;
        toggled.IsEnabled.Should().BeFalse("setup: the toggle disabled the rule");
        toggled.UpdatedBy.Should().Be(togglerId.ToString(), "the last author is the admin who sent the PATCH");
    }

    /// <summary>Le rotte di #4568 lette dalla tabella di routing dell'host vero, come «METODO modello».</summary>
    private List<(string Method, string Template)> AlertAdminRoutes() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Template: (e.RoutePattern.RawText ?? string.Empty).Trim('/')))
            .Where(x => AlertAdminPrefixes.Any(p =>
                x.Template.Equals(p, StringComparison.OrdinalIgnoreCase)
                || x.Template.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(x => (x.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => (Method: m, x.Template)))
            .OrderBy(r => r.Template, StringComparer.Ordinal)
            .ThenBy(r => r.Method, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Chiama una rotta e restituisce una riga di errore se lo stato non è quello atteso, altrimenti stringa vuota.
    /// I parametri di queste rotte sono <c>{category}</c> e <c>{id:guid}</c>: il primo prende una categoria valida,
    /// il secondo la regola vera del test. Il corpo porta i campi di tutte le richieste del gruppo: ognuna legge i
    /// suoi e ignora gli altri, così ogni rotta riceve una richiesta che passerebbe la validazione.
    /// </summary>
    private async Task<string> ProbeAsync(
        string method, string template, Guid ruleId, string name, string who, string? token, HttpStatusCode expected)
    {
        var path = "/" + Regex.Replace(
            Regex.Replace(template, @"\{id(:[^}]*)?\}", ruleId.ToString()), @"\{category\}", "Slack");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        }

        if (method is "PUT" or "POST" or "PATCH")
        {
            request.Content = JsonContent.Create(new
            {
                configKey = name,
                configValue = "https://hooks.slack.example/T000/B000/XXXX",
                category = "Slack",
                description = "test #4568",
                name,
                alertType = "HighErrorRate",
                severity = "Critical",
                thresholdValue = 50.0,
                thresholdUnit = "%",
                durationMinutes = 10,
                channel = "email",
            });
        }

        using var response = await _client.SendAsync(request);
        return response.StatusCode == expected
            ? string.Empty
            : $"{method} {path} as {who}: expected {(int)expected}, got {(int)response.StatusCode}";
    }

    private async Task<Guid> CreateRuleAsync(string token, string name)
    {
        var created = await SendAsync(HttpMethod.Post, $"/{RulesPath}", token, new
        {
            name,
            alertType = "HighErrorRate",
            severity = "Warning",
            thresholdValue = 5.0,
            thresholdUnit = "%",
            durationMinutes = 5,
            description = "test #4568",
        });
        created.Status.Should().Be(HttpStatusCode.Created, $"setup: an admin may create a rule. Body: {created.Body}");

        using var json = JsonDocument.Parse(created.Body);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static object ConfigurationBody(string key) => new
    {
        configKey = key,
        configValue = "https://hooks.slack.example/T000/B000/XXXX",
        category = "Slack",
        description = "test #4568",
    };

    private static object RuleUpdateBody(string name) => new
    {
        name,
        severity = "Error",
        thresholdValue = 10.0,
        thresholdUnit = "%",
        durationMinutes = 15,
        description = "test #4568, edited",
    };

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        HttpMethod method, string path, string token, object? body = null)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(method, path, token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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

    private async Task<List<AlertConfigurationEntity>> ReadConfigurationRowsAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AlertConfigurations.AsNoTracking().Where(c => c.ConfigKey == key).ToListAsync();
    }

    private async Task<List<AlertRuleEntity>> ReadRuleRowsAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AlertRules.AsNoTracking().Where(r => r.Name == name).ToListAsync();
    }
}
