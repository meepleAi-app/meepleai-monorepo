using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola (vedi <see cref="IntegrationHostFixture"/>).
/// <para>
/// Condividere il database è sicuro qui perché ogni test, prima di cominciare, azzera le righe (e la cache)
/// delle funzioni che legge o scrive: <c>ResetAsync</c>.
/// </para>
/// </summary>
public sealed class RagEnhancementAdminHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "rag_admin");

/// <summary>
/// #4594: le due rotte che accendono e spengono le funzioni di RAG non passavano l'id dell'admin.
/// <list type="bullet">
///   <item><c>POST /api/v1/admin/rag-enhancements/{key}/toggle</c></item>
///   <item><c>POST /api/v1/admin/rag-enhancements/{key}/tier/{tierName}/toggle</c></item>
/// </list>
/// Senza id, <c>FeatureFlagService</c> usa <c>Guid.Empty</c> come autore, e il validatore di
/// <c>CreateConfigurationCommand</c> / <c>UpdateConfigValueCommand</c> lo rifiuta: 422
/// (<c>CreatedByUserId is required</c>). Ogni test usa la sessione vera (cookie + utente nel database) e
/// legge lo stato finale, e l'autore salvato, dalla riga di <c>system_configurations</c>.
/// <para>
/// In fondo, i test delle due rotte di lettura (<c>GET /api/v1/admin/rag-enhancements</c> e
/// <c>GET /api/v1/rag/enhancements/estimate</c>) fissano la forma della risposta: servono a vedere che spostarle
/// dietro <c>IMediator</c> non cambia nulla per chi le chiama.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4594")]
public sealed class RagEnhancementAdminEndpointsIntegrationTests
    : IClassFixture<RagEnhancementAdminHostFixture>
{
    private const string AdaptiveRouting = "rag.enhancement.adaptive-routing";
    private const string CragEvaluation = "rag.enhancement.crag-evaluation";
    private const string RaptorRetrieval = "rag.enhancement.raptor-retrieval";
    private const string RagFusionQueries = "rag.enhancement.rag-fusion-queries";
    private const string GraphTraversal = "rag.enhancement.graph-traversal";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RagEnhancementAdminEndpointsIntegrationTests(RagEnhancementAdminHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // ---------------------------------------------------------------- globale

    [Fact]
    public async Task Toggle_OnAKeyWithNoRowYet_TurnsItOn_AndRecordsTheAdminAsCreator()
    {
        await ResetAsync(RaptorRetrieval);
        var (adminId, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        (await RowsAsync(RaptorRetrieval)).Should().BeEmpty("setup: the flag has no row yet");
        (await IsEnabledAsync(RaptorRetrieval)).Should().BeFalse("setup: without a row a global flag is off");

        var isNowEnabled = await ToggleAsync(GlobalPath(RaptorRetrieval), token);

        isNowEnabled.Should().BeTrue("a flag that was off is now on");
        var row = (await RowsAsync(RaptorRetrieval)).Should().ContainSingle().Subject;
        row.Value.Should().Be("true");
        row.CreatedByUserId.Should().Be(adminId,
            "the row is created by the admin who sent the POST. Before #4594 the endpoint passed no id, " +
            "the author was Guid.Empty and the request was rejected with 422");
        (await IsEnabledAsync(RaptorRetrieval)).Should().BeTrue("the service reads the new state, not the cached \"no row\"");
    }

    [Fact]
    public async Task Toggle_OnAnExistingRow_FlipsItBothWays_AndRecordsTheLastAdmin()
    {
        await ResetAsync(RagFusionQueries);
        var (firstAdminId, firstToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (secondAdminId, secondToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);

        (await ToggleAsync(GlobalPath(RagFusionQueries), firstToken)).Should().BeTrue("setup: the first toggle creates the row, on");

        // Spento dal secondo admin: è l'aggiornamento di una riga che esiste (UpdateConfigValueCommand).
        (await ToggleAsync(GlobalPath(RagFusionQueries), secondToken)).Should().BeFalse("on becomes off");
        var off = (await RowsAsync(RagFusionQueries)).Should().ContainSingle().Subject;
        off.Value.Should().Be("false");
        off.UpdatedByUserId.Should().Be(secondAdminId, "the last author is the admin who sent the second POST");
        off.CreatedByUserId.Should().Be(firstAdminId, "an update does not change the creator");
        (await IsEnabledAsync(RagFusionQueries)).Should().BeFalse();

        // E riacceso dal primo: l'altro verso.
        (await ToggleAsync(GlobalPath(RagFusionQueries), firstToken)).Should().BeTrue("off becomes on");
        var on = (await RowsAsync(RagFusionQueries)).Should().ContainSingle().Subject;
        on.Value.Should().Be("true");
        on.UpdatedByUserId.Should().Be(firstAdminId);
        (await IsEnabledAsync(RagFusionQueries)).Should().BeTrue();
    }

    [Fact]
    public async Task Toggle_OnARowSeededForAllEnvironments_UpdatesItInPlace()
    {
        // La forma di produzione: il seeder scrive la riga con Environment = "All", e il servizio la cerca per
        // il nome dell'ambiente dell'host. Qui si aggiorna una riga che esiste già, scritta da un altro utente.
        await ResetAsync(GraphTraversal);
        var (adminId, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (seederId, _) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        await SeedRowAsync(GraphTraversal, "false", seederId);

        var isNowEnabled = await ToggleAsync(GlobalPath(GraphTraversal), token);

        isNowEnabled.Should().BeTrue();
        var row = (await RowsAsync(GraphTraversal)).Should().ContainSingle("the seeded row is updated, a second one is not created").Subject;
        row.Value.Should().Be("true");
        row.UpdatedByUserId.Should().Be(adminId);
        row.CreatedByUserId.Should().Be(seederId);
        (await IsEnabledAsync(GraphTraversal)).Should().BeTrue();
    }

    // ---------------------------------------------------------------- per tier

    [Fact]
    public async Task ToggleForTier_OnAKeyWithNoRowYet_TurnsItOff_AndRecordsTheAdminAsCreator()
    {
        await ResetAsync(AdaptiveRouting);
        var (adminId, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        (await RowsAsync(TierKey(AdaptiveRouting, "premium"))).Should().BeEmpty("setup: the flag has no row for the tier yet");
        (await IsEnabledForTierAsync(AdaptiveRouting, "premium")).Should().BeTrue("setup: without a row a tier has the flag on");

        var isNowEnabled = await ToggleAsync(TierPath(AdaptiveRouting, "premium"), token);

        isNowEnabled.Should().BeFalse("a flag that was on for the tier is now off");
        var row = (await RowsAsync(TierKey(AdaptiveRouting, "premium"))).Should().ContainSingle().Subject;
        row.Value.Should().Be("false");
        row.CreatedByUserId.Should().Be(adminId,
            "this is the 422 of #4580: the tier toggle on a key with no row failed with \"CreatedByUserId is required\"");
        (await IsEnabledForTierAsync(AdaptiveRouting, "premium")).Should().BeFalse();
        (await IsEnabledForTierAsync(AdaptiveRouting, "free")).Should().BeTrue("only the tier that was toggled changes");
        (await RowsAsync(TierKey(AdaptiveRouting, "free"))).Should().BeEmpty("no row is written for the other tiers");
    }

    [Fact]
    public async Task ToggleForTier_OnAnExistingRow_FlipsItBothWays_AndRecordsTheLastAdmin()
    {
        await ResetAsync(CragEvaluation);
        var (firstAdminId, firstToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (secondAdminId, secondToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var key = TierKey(CragEvaluation, "normal");

        (await ToggleAsync(TierPath(CragEvaluation, "normal"), firstToken)).Should().BeFalse("setup: the first toggle creates the row, off");

        (await ToggleAsync(TierPath(CragEvaluation, "normal"), secondToken)).Should().BeTrue("off becomes on");
        var on = (await RowsAsync(key)).Should().ContainSingle().Subject;
        on.Value.Should().Be("true");
        on.UpdatedByUserId.Should().Be(secondAdminId, "the last author is the admin who sent the second POST");
        on.CreatedByUserId.Should().Be(firstAdminId, "an update does not change the creator");
        (await IsEnabledForTierAsync(CragEvaluation, "normal")).Should().BeTrue();

        (await ToggleAsync(TierPath(CragEvaluation, "normal"), firstToken)).Should().BeFalse("on becomes off");
        var off = (await RowsAsync(key)).Should().ContainSingle().Subject;
        off.Value.Should().Be("false");
        off.UpdatedByUserId.Should().Be(firstAdminId);
        (await IsEnabledForTierAsync(CragEvaluation, "normal")).Should().BeFalse();
    }

    [Fact]
    public async Task ToggleForTier_OnARowSeededForAllEnvironments_UpdatesItInPlace()
    {
        await ResetAsync(GraphTraversal);
        var (adminId, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (seederId, _) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var key = TierKey(GraphTraversal, "free");
        await SeedRowAsync(key, "false", seederId);

        var isNowEnabled = await ToggleAsync(TierPath(GraphTraversal, "free"), token);

        isNowEnabled.Should().BeTrue();
        var row = (await RowsAsync(key)).Should().ContainSingle("the seeded row is updated, a second one is not created").Subject;
        row.Value.Should().Be("true");
        row.UpdatedByUserId.Should().Be(adminId);
        row.CreatedByUserId.Should().Be(seederId);
        (await IsEnabledForTierAsync(GraphTraversal, "free")).Should().BeTrue();
    }

    // ---------------------------------------------------------------- impersonazione

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Toggle_DuringAnImpersonation_RecordsTheRealAdmin_NotTheImpersonatedUser(bool perTier)
    {
        await ResetAsync(CragEvaluation);
        var (superAdminId, _) = await CreateSessionAsync(TestSessionHelper.CreateSuperAdminSessionAsync);
        var (impersonatedId, _) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var impersonationToken = await CreateImpersonationSessionAsync(subjectId: impersonatedId, actorId: superAdminId);
        var (path, key) = perTier
            ? (TierPath(CragEvaluation, "free"), TierKey(CragEvaluation, "free"))
            : (GlobalPath(CragEvaluation), CragEvaluation);

        await ToggleAsync(path, impersonationToken);

        var row = (await RowsAsync(key)).Should().ContainSingle().Subject;
        row.CreatedByUserId.Should().Be(superAdminId,
            "the change is attributed to the person who made it (EffectiveActor), as for the alert channels (#4568)");
        row.CreatedByUserId.Should().NotBe(impersonatedId, "the impersonated user changed nothing");
    }

    // ---------------------------------------------------------------- ingressi non validi

    [Theory]
    [InlineData("/api/v1/admin/rag-enhancements/rag.enhancement.not-a-real-one/toggle")]
    [InlineData("/api/v1/admin/rag-enhancements/rag.enhancement.not-a-real-one/tier/premium/toggle")]
    public async Task Toggle_WithAnUnknownKey_Returns400_AndWritesNothing(string path)
    {
        const string unknownKey = "rag.enhancement.not-a-real-one";
        await ResetAsync(unknownKey);
        var (_, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);

        var response = await PostAsync(path, token);

        response.Status.Should().Be(HttpStatusCode.BadRequest, $"Body: {response.Body}");
        response.Body.Should().Contain("Unknown enhancement key");
        (await RowsWithPrefixAsync(unknownKey)).Should().BeEmpty("a key outside the five RAG enhancements is never written");
    }

    [Theory]
    [InlineData("platinum")]
    [InlineData("not-a-tier")]
    public async Task ToggleForTier_WithAnUnknownTier_Returns400_NotA500_AndWritesNothing(string tier)
    {
        await ResetAsync(RagFusionQueries);
        var (_, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);

        var response = await PostAsync(TierPath(RagFusionQueries, tier), token);

        response.Status.Should().Be(HttpStatusCode.BadRequest,
            $"UserTier.Parse throws the domain ValidationException, which the middleware maps to 400. Body: {response.Body}");
        (await RowsWithPrefixAsync(RagFusionQueries)).Should().BeEmpty("an unknown tier writes no row");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Toggle_WithoutAnAdminSession_IsRejected_AndWritesNothing(bool perTier)
    {
        await ResetAsync(AdaptiveRouting);
        var (_, userToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var path = perTier ? TierPath(AdaptiveRouting, "premium") : GlobalPath(AdaptiveRouting);

        var asUser = await PostAsync(path, userToken);
        var anonymous = await PostAsync(path, token: null);

        asUser.Status.Should().Be(HttpStatusCode.Forbidden, $"a regular user is not an admin. Body: {asUser.Body}");
        anonymous.Status.Should().Be(HttpStatusCode.Unauthorized, $"no session. Body: {anonymous.Body}");
        (await RowsWithPrefixAsync(AdaptiveRouting)).Should().BeEmpty("a rejected request writes nothing");
    }

    // ---------------------------------------------------------------- rotte di lettura (forma della risposta)

    [Fact]
    public async Task List_ReturnsEveryEnhancement_WithItsGlobalState_AndItsCredits()
    {
        foreach (var key in AllKeys) await ResetAsync(key);
        var (_, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        await ToggleAsync(GlobalPath(RagFusionQueries), token);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/rag-enhancements", token);

        response.Status.Should().Be(HttpStatusCode.OK, $"Body: {response.Body}");
        using var json = JsonDocument.Parse(response.Body);
        var items = json.RootElement.EnumerateArray().ToList();
        items.Should().OnlyContain(
            e => e.EnumerateObject().Select(p => p.Name).SequenceEqual(
                new[] { "name", "flagKey", "isGloballyEnabled", "extraCreditsBalanced", "extraCreditsFast" }),
            "the field names and their order are the contract of the admin tab");
        items.Select(e => (
                Name: e.GetProperty("name").GetString(),
                FlagKey: e.GetProperty("flagKey").GetString(),
                IsGloballyEnabled: e.GetProperty("isGloballyEnabled").GetBoolean(),
                Balanced: e.GetProperty("extraCreditsBalanced").GetInt32(),
                Fast: e.GetProperty("extraCreditsFast").GetInt32()))
            .Should().Equal(
                ("AdaptiveRouting", AdaptiveRouting, false, 0, 0),
                ("CragEvaluation", CragEvaluation, false, 40, 0),
                ("RaptorRetrieval", RaptorRetrieval, false, 0, 0),
                ("RagFusionQueries", RagFusionQueries, true, 60, 0),
                ("GraphTraversal", GraphTraversal, false, 0, 0));
    }

    [Fact]
    public async Task List_WithoutAnAdminSession_IsRejected()
    {
        var (_, userToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);

        var asUser = await SendAsync(HttpMethod.Get, "/api/v1/admin/rag-enhancements", userToken);
        var anonymous = await SendAsync(HttpMethod.Get, "/api/v1/admin/rag-enhancements", token: null);

        asUser.Status.Should().Be(HttpStatusCode.Forbidden, $"Body: {asUser.Body}");
        anonymous.Status.Should().Be(HttpStatusCode.Unauthorized, $"Body: {anonymous.Body}");
    }

    [Fact]
    public async Task Estimate_ReturnsTheActiveFlags_TheExtraCredits_AndTheAuxModel()
    {
        foreach (var key in AllKeys) await ResetAsync(key);
        await ResetAsync(FeatureFlagConstants.RagAuxModelKey);
        var (adminId, token) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);

        // Nessuna sessione = tier Free. Senza righe ogni funzione è accesa per il tier: se ne spengono tre.
        foreach (var key in new[] { AdaptiveRouting, RaptorRetrieval, GraphTraversal })
        {
            (await ToggleAsync(TierPath(key, "free"), token)).Should().BeFalse("setup: on becomes off for the free tier");
        }

        var withoutAuxModel = await SendAsync(HttpMethod.Get, "/api/v1/rag/enhancements/estimate", token: null);

        withoutAuxModel.Status.Should().Be(HttpStatusCode.OK, $"Body: {withoutAuxModel.Body}");
        using (var json = JsonDocument.Parse(withoutAuxModel.Body))
        {
            json.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal(
                "activeEnhancements", "activeFlags", "extraCreditsPerQuery", "useBalancedAuxModel");
            json.RootElement.GetProperty("activeEnhancements").GetString().Should().Be("CragEvaluation, RagFusionQueries");
            json.RootElement.GetProperty("activeFlags").EnumerateArray().Select(e => e.GetString())
                .Should().Equal("CragEvaluation", "RagFusionQueries");
            json.RootElement.GetProperty("extraCreditsPerQuery").GetInt32().Should().Be(0, "the fast model costs nothing extra");
            json.RootElement.GetProperty("useBalancedAuxModel").GetBoolean().Should().BeFalse();
        }

        await EnableGlobalAsync(FeatureFlagConstants.RagAuxModelKey, adminId);

        var withAuxModel = await SendAsync(HttpMethod.Get, "/api/v1/rag/enhancements/estimate", token: null);

        withAuxModel.Status.Should().Be(HttpStatusCode.OK, $"Body: {withAuxModel.Body}");
        using (var json = JsonDocument.Parse(withAuxModel.Body))
        {
            json.RootElement.GetProperty("extraCreditsPerQuery").GetInt32().Should().Be(100, "40 (CRAG) + 60 (RAG-Fusion) with the balanced model");
            json.RootElement.GetProperty("useBalancedAuxModel").GetBoolean().Should().BeTrue();
        }
    }

    // ---------------------------------------------------------------- aiuti

    private static readonly string[] AllKeys =
        [AdaptiveRouting, CragEvaluation, RaptorRetrieval, RagFusionQueries, GraphTraversal];

    private static string GlobalPath(string key) => $"/api/v1/admin/rag-enhancements/{key}/toggle";

    private static string TierPath(string key, string tier) => $"/api/v1/admin/rag-enhancements/{key}/tier/{tier}/toggle";

    /// <summary>La chiave della riga per tier, nella forma che <c>FeatureFlagService</c> scrive.</summary>
    private static string TierKey(string key, string tier) => $"{key}.Tier.{tier}";

    /// <summary>
    /// Azzera la funzione: cancella la riga globale e quelle per tier, e svuota la cache di lettura. Il database
    /// e la cache sono dell'host, che vive quanto la classe: senza questo un test vedrebbe le righe di un altro.
    /// </summary>
    private async Task ResetAsync(string featureKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.SystemConfigurations
            .Where(c => c.Key == featureKey || c.Key.StartsWith(featureKey + ".Tier."))
            .ExecuteDeleteAsync();

        var config = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
        await config.InvalidateAsync(featureKey);
        foreach (var tier in UserTier.All)
        {
            await config.InvalidateAsync(TierKey(featureKey, tier));
        }
    }

    /// <summary>Spedisce il POST, richiede 200 e restituisce <c>isNowEnabled</c> della risposta.</summary>
    private async Task<bool> ToggleAsync(string path, string token)
    {
        var response = await PostAsync(path, token);
        response.Status.Should().Be(HttpStatusCode.OK, $"POST {path}. Body: {response.Body}");
        using var json = JsonDocument.Parse(response.Body);
        return json.RootElement.GetProperty("isNowEnabled").GetBoolean();
    }

    private Task<(HttpStatusCode Status, string Body)> PostAsync(string path, string? token) =>
        SendAsync(HttpMethod.Post, path, token);

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpMethod method, string path, string? token)
    {
        using var request = token is null
            ? new HttpRequestMessage(method, path)
            : TestSessionHelper.CreateAuthenticatedRequest(method, path, token);

        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Accende un flag globale dal servizio, con l'id di un admin vero come autore (per chiavi fuori dall'elenco delle cinque).</summary>
    private async Task EnableGlobalAsync(string key, Guid adminId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFeatureFlagService>()
            .EnableFeatureAsync(key, userId: adminId.ToString());
    }

    private async Task<(Guid UserId, string Token)> CreateSessionAsync(
        Func<MeepleAiDbContext, Guid?, CancellationToken, Task<(Guid UserId, string RawToken)>> create)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await create(db, null, CancellationToken.None);
    }

    private async Task<string> CreateImpersonationSessionAsync(Guid subjectId, Guid actorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateImpersonationSessionAsync(db, subjectId, actorId);
    }

    private async Task<bool> IsEnabledAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IFeatureFlagService>().IsEnabledAsync(key);
    }

    private async Task<bool> IsEnabledForTierAsync(string key, string tier)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IFeatureFlagService>()
            .IsEnabledForTierAsync(key, UserTier.Parse(tier));
    }

    private async Task<List<SystemConfigurationEntity>> RowsAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.SystemConfigurations.AsNoTracking().Where(c => c.Key == key).ToListAsync();
    }

    private async Task<List<SystemConfigurationEntity>> RowsWithPrefixAsync(string featureKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.SystemConfigurations.AsNoTracking().Where(c => c.Key.StartsWith(featureKey)).ToListAsync();
    }

    /// <summary>Una riga come la scrive il seeder dei flag: <c>Environment = "All"</c>, categoria <c>FeatureFlags</c>.</summary>
    private async Task SeedRowAsync(string key, string value, Guid createdBy)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = value,
            ValueType = "Boolean",
            Description = "Seed di prova (#4594)",
            Category = "FeatureFlags",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = createdBy,
        });
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IConfigurationService>().InvalidateAsync(key);
    }
}
