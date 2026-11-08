using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>La fixture di <see cref="AgentProfileAdminEndpointsTests"/>: l'host vero, nessun doppio.</summary>
public sealed class AgentProfileAdminHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "profile_admin");

/// <summary>
/// Le rotte della pagina admin «Agente» (ADR-095 D8, #4169 6c): leggere il profilo, salvare la bozza,
/// pubblicarla, tornare a una versione archiviata. Che la versione pubblicata cambi davvero la risposta
/// lo dice <c>AgentProfileFieldEffectTests.Temperature_PublishedFromTheAgentPage_IsTheOneSentToTheGateway</c>.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4169")]
public sealed class AgentProfileAdminEndpointsTests : IClassFixture<AgentProfileAdminHostFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/admin/agent-profile";

    private readonly AgentProfileAdminHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _adminToken = null!;

    public AgentProfileAdminEndpointsTests(AgentProfileAdminHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        // La pubblicazione vuole i prezzi dei modelli (D5): il catalogo seminato li ha.
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SeedAiModelsCommand(), Ct);
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Chi ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AUserWhoIsNotAnAdmin_CanNeitherReadNorChangeTheProfile()
    {
        string userToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, userToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var read = await SendAsync(HttpMethod.Get, Route, userToken);
        var save = await SendAsync(HttpMethod.Put, $"{Route}/draft", userToken, await EditableSectionsAsync());
        var publish = await SendAsync(HttpMethod.Post, $"{Route}/draft/publish", userToken);
        var rollback = await SendAsync(HttpMethod.Post, $"{Route}/versions/1/rollback", userToken);

        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        save.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rollback.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProfileAsync()).Draft.Should().BeNull("nessuna richiesta rifiutata ha lasciato una bozza");
    }

    // ─── Leggere ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_ReturnsThePublishedVersion_NoDraft_AndTheHistory()
    {
        var expected = await ProfileAsync();

        using var body = await OkJsonAsync(await SendAsync(HttpMethod.Get, Route, _adminToken));

        var published = body.RootElement.GetProperty("published");
        published.GetProperty("versionNumber").GetInt32().Should().Be(expected.Published.VersionNumber);
        published.GetProperty("status").GetString().Should().Be("Published");
        published.GetProperty("content").GetProperty("primaryModelId").GetString().Should().Be(expected.Published.Content.PrimaryModelId);
        body.RootElement.GetProperty("draft").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("versions").GetArrayLength().Should().Be(expected.Versions.Count);
    }

    // ─── La bozza ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveDraft_ChangesTheEditableSections_AndKeepsTheOthersFromThePublishedVersion()
    {
        // Il corpo porta anche campi di Recupero e Fonti: la rotta li ignora, perché sono in sola
        // lettura fino alla fetta 7 e una pagina che non li mostra non deve poterli cambiare.
        var draft = await EditableSectionsAsync();
        draft["temperature"] = 0.9m;
        draft["minScore"] = 0.99m;
        draft["allowedCategories"] = new[] { "Errata" };
        draft["rerankerEnabled"] = false;

        using var body = await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));

        body.RootElement.GetProperty("draft").GetProperty("status").GetString().Should().Be("Draft");
        var profile = await ProfileAsync();
        var saved = profile.Draft!.Content;
        var published = profile.Published.Content;
        saved.Temperature.Should().Be(0.9m);
        saved.MinScore.Should().Be(published.MinScore);
        saved.AllowedCategories.Should().Equal(published.AllowedCategories);
        saved.RerankerEnabled.Should().Be(published.RerankerEnabled);
        saved.CandidatePoolSize.Should().Be(published.CandidatePoolSize);
        saved.FinalTopK.Should().Be(published.FinalTopK);
        saved.VectorWeight.Should().Be(published.VectorWeight);
        saved.KeywordWeight.Should().Be(published.KeywordWeight);
        saved.SessionAssistant.Should().Be(published.SessionAssistant);
    }

    [Fact]
    public async Task SaveDraft_Twice_UpdatesTheSameDraft()
    {
        var draft = await EditableSectionsAsync();
        draft["temperature"] = 0.5m;
        await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));
        var first = (await ProfileAsync()).Draft!.VersionNumber;

        draft["temperature"] = 0.7m;
        await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));

        var profile = await ProfileAsync();
        profile.Draft!.VersionNumber.Should().Be(first, "al più una bozza (D2): la seconda volta si aggiorna");
        profile.Draft.Content.Temperature.Should().Be(0.7m);
    }

    [Fact]
    public async Task SaveDraft_WithATemperatureOutOfRange_IsABadRequest()
    {
        var draft = await EditableSectionsAsync();
        draft["temperature"] = 3.5m;

        var response = await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
        (await ProfileAsync()).Draft.Should().BeNull();
    }

    /// <summary>
    /// Un campo omesso non vale zero: senza la temperatura la bozza si salverebbe a 0, un valore
    /// valido che nessuno ha scelto, e senza lo stile delle citazioni col primo dell'enum.
    /// </summary>
    [Theory]
    [InlineData("temperature")]
    [InlineData("citationStyle")]
    [InlineData("maxResponseTokens")]
    public async Task SaveDraft_WithoutARequiredField_IsABadRequest(string omitted)
    {
        var draft = await EditableSectionsAsync();
        draft.Remove(omitted);

        var response = await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
        (await ProfileAsync()).Draft.Should().BeNull();
    }

    // ─── Pubblicare ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_ArchivesThePreviousVersion_AndPublishesTheDraft()
    {
        var before = (await ProfileAsync()).Published.VersionNumber;
        var draft = await EditableSectionsAsync();
        draft["temperature"] = 0.6m;
        await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));

        await OkJsonAsync(await SendAsync(HttpMethod.Post, $"{Route}/draft/publish", _adminToken));

        var profile = await ProfileAsync();
        profile.Draft.Should().BeNull();
        profile.Published.VersionNumber.Should().Be(before + 1);
        profile.Published.Content.Temperature.Should().Be(0.6m);
        profile.Versions.Single(v => v.VersionNumber == before).Status.Should().Be(AgentProfileVersionStatus.Archived);
    }

    [Fact]
    public async Task Publish_AModelWithoutAPriceInTheCatalog_IsAConflict_AndThePublishedVersionStays()
    {
        var before = (await ProfileAsync()).Published.VersionNumber;
        var draft = await EditableSectionsAsync();
        draft["primaryModelId"] = "test/modello-senza-prezzo";
        await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));

        var response = await SendAsync(HttpMethod.Post, $"{Route}/draft/publish", _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain(AgentProfile.ModelWithoutPriceCode);
        var profile = await ProfileAsync();
        profile.Published.VersionNumber.Should().Be(before);
        profile.Draft.Should().NotBeNull("la bozza resta, da correggere");
    }

    [Fact]
    public async Task Rollback_RepublishesTheArchivedContent_AsANewVersion()
    {
        var original = (await ProfileAsync()).Published;
        var draft = await EditableSectionsAsync();
        draft["temperature"] = 0.8m;
        await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));
        await OkJsonAsync(await SendAsync(HttpMethod.Post, $"{Route}/draft/publish", _adminToken));

        await OkJsonAsync(await SendAsync(HttpMethod.Post, $"{Route}/versions/{original.VersionNumber}/rollback", _adminToken));

        var profile = await ProfileAsync();
        profile.Published.VersionNumber.Should().Be(original.VersionNumber + 2, "il rollback è una versione nuova (D2)");
        profile.Published.Content.Should().Be(original.Content);
    }

    /// <summary>
    /// La riserva è l'unico campo del profilo tenuto in cache, un minuto, dal gateway in degrado. Senza
    /// invalidazione, per un minuto dopo la pubblicazione il degrado risponderebbe col modello di prima.
    /// </summary>
    [Fact]
    public async Task Publish_ANewReserveModel_ReachesTheBudgetDegradeAtOnce()
    {
        var reserve = _hostFixture.Factory.Services.GetRequiredService<IAgentReserveModelProvider>();
        var before = await reserve.GetReserveModelAsync(Ct);
        var draft = await EditableSectionsAsync();
        draft["fallbackModelId"] = "deepseek-flash";
        before.Should().NotBe("deepseek-flash", "il test cambia la riserva");
        await OkJsonAsync(await SendAsync(HttpMethod.Put, $"{Route}/draft", _adminToken, draft));

        await OkJsonAsync(await SendAsync(HttpMethod.Post, $"{Route}/draft/publish", _adminToken));

        (await reserve.GetReserveModelAsync(Ct)).Should().Be("deepseek-flash");
    }

    // ─── Aiuti ──────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token, object? body = null) =>
        await _client.SendAsync(
            body is null
                ? TestSessionHelper.CreateAuthenticatedRequest(method, uri, token)
                : TestSessionHelper.CreateAuthenticatedRequest(method, uri, token, body),
            Ct);

    private static async Task<JsonDocument> OkJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text);
    }

    private async Task<AgentProfile> ProfileAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(Ct);
        return profile!;
    }

    /// <summary>Il corpo della bozza com'è nella versione pubblicata: solo le sezioni che la pagina modifica.</summary>
    private async Task<Dictionary<string, object?>> EditableSectionsAsync()
    {
        var c = (await ProfileAsync()).Published.Content;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = c.Name,
            ["description"] = c.Description,
            ["persona"] = c.Persona,
            ["primaryModelId"] = c.PrimaryModelId,
            ["fallbackModelId"] = c.FallbackModelId,
            ["temperature"] = c.Temperature,
            ["maxResponseTokens"] = c.MaxResponseTokens,
            ["citationStyle"] = c.CitationStyle.ToString(),
            ["systemPrompts"] = c.SystemPrompts,
            ["fallbackLanguage"] = c.FallbackLanguage,
        };
    }
}
