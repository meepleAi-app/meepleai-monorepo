using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
/// Condividere il database è sicuro qui: ogni test crea i propri utenti e lavora su un nome proprio
/// (<c>Test4580.{guid}</c>). L'unica riga con chiave naturale è il canale <c>slack</c>, scritto da un solo test.
/// Il flag per tier che il test del tier cambia riguarda solo <c>premium</c>, e nessun altro test della classe
/// legge le funzioni di RAG.
/// </para>
/// </summary>
public sealed class SessionClaimConsumersHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "session_claims");

/// <summary>
/// #4580: il gestore della sessione emette solo <c>NameIdentifier</c>, <c>Email</c>, <c>Role</c> e <c>Name</c>.
/// Quattro rotte leggevano un claim che nessuno emette (<c>"userId"</c>, <c>"user_id"</c>, <c>"displayName"</c>,
/// <c>"email"</c>, <c>"tier"</c>) e prendevano ogni volta il ripiego:
/// <list type="bullet">
///   <item><c>PUT /admin/alert-channels/{type}</c>: l'autore era sempre <c>"system"</c>.</item>
///   <item><c>POST /admin/reports/schedule</c>: <c>?? throw UnauthorizedAccessException</c>, quindi 403 anche a un admin.</item>
///   <item><c>POST /admin/notifications/send</c>: il nome dell'admin nella notifica era sempre <c>"Admin"</c>.</item>
///   <item><c>GET /rag/enhancements/estimate</c>: il tier era sempre <c>Free</c>.</item>
/// </list>
/// Ogni test usa la sessione vera (cookie + utente nel database): un doppio che riempie a mano i claim
/// «giusti» non vedrebbe il difetto, perché il difetto è proprio che il gestore non li emette.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4580")]
public sealed class SessionClaimConsumersIntegrationTests
    : IClassFixture<SessionClaimConsumersHostFixture>
{
    private const string AdaptiveRoutingKey = "rag.enhancement.adaptive-routing";
    private const string CragEvaluationKey = "rag.enhancement.crag-evaluation";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SessionClaimConsumersIntegrationTests(SessionClaimConsumersHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task AlertChannelUpsert_RecordsTheAdminWhoSentIt_OnCreateAndOnUpdate()
    {
        var (firstAdminId, firstToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (secondAdminId, secondToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        const string config = """{"webhookUrl":"https://hooks.slack.example/T000/B000/XXXX"}""";

        var create = await SendAsync(HttpMethod.Put, "/api/v1/admin/alert-channels/slack", firstToken,
            new { configJson = config, isEnabled = true, xmin = (uint?)null });
        create.Status.Should().Be(HttpStatusCode.OK, $"setup: an admin may write a channel. Body: {create.Body}");

        var created = await ReadChannelAsync("slack");
        created.CreatedBy.Should().Be(firstAdminId.ToString(),
            "the creator is the admin who sent the PUT. Before #4580 the endpoint read the claim \"userId\", " +
            "which the session handler does not emit, and the row said \"system\"");
        created.UpdatedBy.Should().Be(firstAdminId.ToString());

        using var createdJson = JsonDocument.Parse(create.Body);
        var xmin = createdJson.RootElement.GetProperty("xmin").GetUInt32();

        var update = await SendAsync(HttpMethod.Put, "/api/v1/admin/alert-channels/slack", secondToken,
            new { configJson = config, isEnabled = false, xmin });
        update.Status.Should().Be(HttpStatusCode.OK, $"setup: a second admin may update the channel. Body: {update.Body}");

        var updated = await ReadChannelAsync("slack");
        updated.UpdatedBy.Should().Be(secondAdminId.ToString(), "the last author is the admin who sent the second PUT");
        updated.CreatedBy.Should().Be(firstAdminId.ToString(), "an update does not change the creator");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task ScheduleReport_AsAdmin_IsAccepted_AndRecordsTheAdminAsCreator(string role)
    {
        var (adminId, token) = await CreateSessionAsync(role == "Admin"
            ? TestSessionHelper.CreateAdminSessionAsync
            : TestSessionHelper.CreateSuperAdminSessionAsync);
        var name = $"Test4580.{Guid.NewGuid():N}";

        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/reports/schedule", token, ScheduleReportBody(name));

        response.Status.Should().Be(HttpStatusCode.OK,
            $"an {role} may schedule a report. Before #4580 the endpoint read the claim \"user_id\", which the " +
            $"session handler does not emit, and threw UnauthorizedAccessException (403). Body: {response.Body}");

        var report = await ReadReportAsync(name);
        report.CreatedBy.Should().Be(adminId.ToString(), "the creator of the schedule is the admin who sent the POST");
    }

    [Fact]
    public async Task ScheduleReport_AsRegularUser_IsForbidden_AndSchedulesNothing()
    {
        var (_, token) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var name = $"Test4580.{Guid.NewGuid():N}";

        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/reports/schedule", token, ScheduleReportBody(name));

        response.Status.Should().Be(HttpStatusCode.Forbidden,
            $"scheduling a report is for admins: the 403 of #4580 was right for a user and wrong for an admin. Body: {response.Body}");
        (await CountReportsAsync(name)).Should().Be(0, "a rejected request must not save a schedule");
    }

    [Fact]
    public async Task ManualNotification_NamesTheAdminWhoSentIt()
    {
        var (adminId, adminToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (recipientId, _) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);

        var send = await SendAsync(HttpMethod.Post, "/api/v1/admin/notifications/send", adminToken, new
        {
            title = $"Test4580.{Guid.NewGuid():N}",
            message = "test #4580",
            channels = new[] { "inapp" },
            recipientType = "users",
            recipientUserIds = new[] { recipientId },
        });
        send.Status.Should().Be(HttpStatusCode.OK, $"setup: an admin may send a notification. Body: {send.Body}");

        var metadata = await ReadNotificationMetadataAsync(recipientId);
        metadata.GetProperty("sentByAdminId").GetGuid().Should().Be(adminId);
        metadata.GetProperty("sentByAdminName").GetString().Should().Be(
            await ReadDisplayNameAsync(adminId),
            "the notification names the admin who sent it. Before #4580 the endpoint read the claims " +
            "\"displayName\" and \"email\", which the session handler does not emit, so it always said \"Admin\"");
    }

    [Fact]
    public async Task ManualNotification_DuringAnImpersonation_NamesTheRealAdmin_NotTheImpersonatedUser()
    {
        var (superAdminId, _) = await CreateSessionAsync(TestSessionHelper.CreateSuperAdminSessionAsync);
        var (impersonatedId, _) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var (recipientId, _) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);

        // L'impersonazione vera: il cookie è quello di una sessione del soggetto (UserId = utente X) con
        // ImpersonatedByUserId = il superadmin. È l'unico caso in cui Subject e EffectiveActor sono persone diverse.
        var impersonationToken = await CreateImpersonationSessionAsync(subjectId: impersonatedId, actorId: superAdminId);

        var send = await SendAsync(HttpMethod.Post, "/api/v1/admin/notifications/send", impersonationToken, new
        {
            title = $"Test4580.{Guid.NewGuid():N}",
            message = "test #4580",
            channels = new[] { "inapp" },
            recipientType = "users",
            recipientUserIds = new[] { recipientId },
        });
        send.Status.Should().Be(HttpStatusCode.OK,
            $"setup: the admin filter authorizes on the real actor, a superadmin. Body: {send.Body}");

        var metadata = await ReadNotificationMetadataAsync(recipientId);
        metadata.GetProperty("sentByAdminId").GetGuid().Should().Be(superAdminId,
            "the notification is attributed to the person who sent it (EffectiveActor), not to the user being impersonated");
        var name = metadata.GetProperty("sentByAdminName").GetString();
        name.Should().Be(await ReadDisplayNameAsync(superAdminId));
        name.Should().NotBe(await ReadDisplayNameAsync(impersonatedId),
            "the recipients must not read the name of a person who sent nothing");
    }

    [Fact]
    public async Task ManualNotification_WithABlankDisplayName_SaysAdmin_NotTheEmail()
    {
        var (adminId, adminToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (recipientId, _) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        await SetDisplayNameAsync(adminId, "   ");

        var send = await SendAsync(HttpMethod.Post, "/api/v1/admin/notifications/send", adminToken, new
        {
            title = $"Test4580.{Guid.NewGuid():N}",
            message = "test #4580",
            channels = new[] { "inapp" },
            recipientType = "users",
            recipientUserIds = new[] { recipientId },
        });
        send.Status.Should().Be(HttpStatusCode.OK, $"setup: an admin may send a notification. Body: {send.Body}");

        var metadata = await ReadNotificationMetadataAsync(recipientId);
        metadata.GetProperty("sentByAdminName").GetString().Should().Be("Admin",
            "with a blank display name the notification says \"Admin\": the admin's email address must not reach the recipients");
    }

    [Fact]
    public async Task RagEnhancementEstimate_UsesTheTierOfTheUser()
    {
        var (_, adminToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);
        var (premiumId, premiumToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        var (_, freeToken) = await CreateSessionAsync(TestSessionHelper.CreateUserSessionAsync);
        await SetTierAsync(premiumId, "premium");

        // Senza chiavi per tier ogni tier ha ogni funzione (il ripiego di IsEnabledForTierAsync è «attivo»).
        // Spegnerne una per «premium» separa i due tier: chi la legge come «free» la vede ancora accesa.
        await DisableForTierAsync(AdaptiveRoutingKey, "premium", adminToken);

        var premium = await SendAsync(HttpMethod.Get, "/api/v1/rag/enhancements/estimate", premiumToken);
        premium.Status.Should().Be(HttpStatusCode.OK, $"Body: {premium.Body}");
        ActiveFlags(premium.Body).Should().NotContain("AdaptiveRouting",
            "the user is on the premium tier, where the flag is off. Before #4580 the endpoint read the claim " +
            "\"tier\", which the session handler does not emit, so every user was estimated as Free");
        ActiveFlags(premium.Body).Should().Contain("CragEvaluation", "the other flags are still on for premium");

        var free = await SendAsync(HttpMethod.Get, "/api/v1/rag/enhancements/estimate", freeToken);
        free.Status.Should().Be(HttpStatusCode.OK, $"Body: {free.Body}");
        ActiveFlags(free.Body).Should().Contain("AdaptiveRouting", "a free user still has the flag: only premium was switched off");
    }

    [Fact]
    public async Task RagEnhancementEstimate_WithoutASession_IsEstimatedAsFree()
    {
        var (_, adminToken) = await CreateSessionAsync(TestSessionHelper.CreateAdminSessionAsync);

        // Il comportamento di chi non ha la sessione non deve cambiare: prima era «Free» perché mancava il claim.
        await DisableForTierAsync(CragEvaluationKey, "free", adminToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rag/enhancements/estimate");
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Nessuna policy di ripiego: la rotta è raggiungibile senza sessione, e la stima è quella del tier Free.
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Body: {body}");
        ActiveFlags(body).Should().NotContain("CragEvaluation", "an anonymous caller is estimated as Free");
    }

    /// <summary>
    /// Spegne una funzione di RAG per un tier dall'endpoint dell'admin,
    /// <c>POST /admin/rag-enhancements/{key}/tier/{tier}/toggle</c>. Senza riga ogni tier ha ogni funzione
    /// accesa, e il toggle inverte: la risposta è asserita, così un secondo toggle non la riaccende in silenzio.
    /// Fino a #4594 l'endpoint rispondeva 422 (<c>CreatedByUserId is required</c>) e questo passo usava
    /// <c>IFeatureFlagService</c> direttamente.
    /// </summary>
    private async Task DisableForTierAsync(string featureKey, string tier, string adminToken)
    {
        var toggle = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/rag-enhancements/{featureKey}/tier/{tier}/toggle", adminToken);

        toggle.Status.Should().Be(HttpStatusCode.OK, $"setup: an admin may toggle a RAG enhancement. Body: {toggle.Body}");
        using var json = JsonDocument.Parse(toggle.Body);
        json.RootElement.GetProperty("isNowEnabled").GetBoolean().Should().BeFalse(
            "setup: the flag was on for the tier (no row), and the toggle turned it off");
    }

    private static string[] ActiveFlags(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("activeFlags").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
    }

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

    private async Task SetTierAsync(Guid userId, string tier)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updated = await db.Set<UserEntity>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tier, tier));
        updated.Should().Be(1, "setup: the user exists");
    }

    private static object ScheduleReportBody(string name) => new
    {
        name,
        description = "test #4580",
        template = "SystemHealth",
        format = "Json",
        parameters = new Dictionary<string, object>(),
        scheduleExpression = "0 0 8 * * ?",
    };

    /// <summary>Una sessione di impersonazione: il cookie vale come <paramref name="subjectId"/>, ma l'attore è <paramref name="actorId"/>.</summary>
    private async Task<string> CreateImpersonationSessionAsync(Guid subjectId, Guid actorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateImpersonationSessionAsync(db, subjectId, actorId);
    }

    private async Task SetDisplayNameAsync(Guid userId, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updated = await db.Set<UserEntity>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, displayName));
        updated.Should().Be(1, "setup: the user exists");
    }

    private async Task<int> CountReportsAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AdminReports.AsNoTracking().CountAsync(r => r.Name == name);
    }

    private async Task<string> ReadDisplayNameAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<UserEntity>().AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync();
    }

    private async Task<Api.Infrastructure.Entities.Administration.AlertChannelEntity> ReadChannelAsync(string type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AlertChannels.AsNoTracking().SingleAsync(c => c.Type == type);
    }

    private async Task<AdminReportEntity> ReadReportAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AdminReports.AsNoTracking().SingleAsync(r => r.Name == name);
    }

    private async Task<JsonElement> ReadNotificationMetadataAsync(Guid recipientId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var metadata = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == recipientId)
            .Select(n => n.Metadata)
            .SingleAsync();
        metadata.Should().NotBeNull("the manual notification carries the sender in its metadata");

        using var json = JsonDocument.Parse(metadata!);
        return json.RootElement.Clone();
    }
}
