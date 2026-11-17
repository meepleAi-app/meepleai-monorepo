using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Endpoints;

/// <summary>
/// La fixture di <see cref="AdminUserWritesPersistEndpointTests"/>: host per classe, database per test.
/// </summary>
public sealed class AdminUserWritesHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "admin_user_writes");

/// <summary>
/// #4907 — tre azioni dell'admin su un utente rispondevano 200 senza scrivere niente.
/// </summary>
/// <remarks>
/// <c>UserRepository.GetByIdAsync</c> restituisce un aggregato staccato (<c>AsNoTracking</c>). I gestori
/// del reset della password, della modifica e dello sblocco lo cambiavano nel dominio e chiamavano solo
/// <c>SaveChangesAsync</c>, che non aveva niente da salvare: serve <c>UpdateAsync</c> prima. Un test con un
/// repository finto che conta le chiamate a <c>SaveChangesAsync</c> passerebbe anche col difetto, quindi
/// qui ogni caso rilegge la riga dal database, da un altro scope.
/// <para>
/// Salvare davvero ha acceso un rischio che il difetto teneva spento: reset della password ed email nuova consegnano
/// l'account. Le due azioni valgono ora solo su un bersaglio con un ruolo sotto quello di chi agisce; i due casi
/// rifiutati sono qui.
/// </para>
/// <para>
/// #5040: un reset dell'admin chiude anche le sessioni aperte dell'utente, e uno rifiutato non chiude niente.
/// </para>
/// <para>
/// #5042: un reset dell'admin sblocca anche l'account, così la password nuova entra subito.
/// </para>
/// <para>
/// #5037, #5045: il cambio di ruolo e il reset della password in blocco non esistono più.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4907")]
public sealed class AdminUserWritesPersistEndpointTests
    : IClassFixture<AdminUserWritesHostFixture>, IAsyncLifetime
{
    private readonly AdminUserWritesHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _adminId;
    private string _adminToken = null!;

    public AdminUserWritesPersistEndpointTests(AdminUserWritesHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory (#4050).
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_adminId, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db);

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // Generato a ogni esecuzione: GitGuardian segnala qualunque letterale assegnato a un membro che
    // si chiama come una password (vedi LoginAccountStatusEndpointTests).
    private static string FreshSecret() => $"x{Guid.NewGuid():N}!A1";

    [Fact]
    public async Task ResetPassword_SavesTheNewPassword_AndTheOldOneStopsWorking()
    {
        var before = FreshSecret();
        var after = FreshSecret();
        var (userId, email) = await SeedUserAsync(before);

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/reset-password", _adminToken, new { newPassword = after }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var stored = PasswordHash.FromStored((await ReadUserAsync(userId)).PasswordHash!);
        stored.Verify(after).Should().BeTrue("the reset must be written, not only answered");
        stored.Verify(before).Should().BeFalse("after a reset the old credential must stop working");

        // Lo stesso, visto da chi fa il login.
        (await LoginAsync(email, after)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(email, before)).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Issue", "5040")]
    public async Task ResetPassword_RevokesTheUsersOpenSessions_AndKeepsTheAdminsOwn()
    {
        // Un reset dell'admin serve quando l'account è compromesso: chi ha già un cookie dell'utente deve uscire.
        var (userId, _) = await SeedUserAsync(FreshSecret());
        var userToken = await OpenSessionAsync(userId, "user");
        (await AuthMeAsync(userToken)).StatusCode.Should().Be(HttpStatusCode.OK, "the session is open before the reset");

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/reset-password", _adminToken, new { newPassword = FreshSecret() }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await AuthMeAsync(userToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a password reset by an admin closes every session the user had open");
        (await AuthMeAsync(_adminToken)).StatusCode.Should().Be(HttpStatusCode.OK, "the admin's own session stays open");
    }

    [Fact]
    [Trait("Issue", "5040")]
    public async Task ResetPassword_Forbidden_RevokesNothing()
    {
        var (userId, _) = await SeedUserAsync(FreshSecret(), role: "superadmin");
        var superAdminToken = await OpenSessionAsync(userId, "superadmin");

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/reset-password", _adminToken, new { newPassword = FreshSecret() }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await AuthMeAsync(superAdminToken)).StatusCode.Should().Be(HttpStatusCode.OK,
            "a refused reset must not log the target out");
    }

    [Fact]
    [Trait("Issue", "5042")]
    public async Task ResetPassword_OfALockedAccount_UnlocksIt_SoTheNewPasswordLogsIn()
    {
        // Chi chiede una password nuova all'admin spesso ha bloccato l'account provando la vecchia.
        var after = FreshSecret();
        var (userId, email) = await SeedUserAsync(FreshSecret(), lockedUntil: DateTime.UtcNow.AddMinutes(30), failedAttempts: 3);

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/reset-password", _adminToken, new { newPassword = after }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = await ReadUserAsync(userId);
        saved.FailedLoginAttempts.Should().Be(0);
        saved.LockedUntil.Should().BeNull();
        (await LoginAsync(email, after)).StatusCode.Should().Be(HttpStatusCode.OK, "the new password logs in right away");

        // Lo sblocco va nell'outbox con l'admin, come quello di «sblocca»: da lì nasce la riga di audit.
        (await AccountUnlockedPayloadsAsync(userId)).Should().ContainSingle()
            .Which.Should().Contain(_adminId.ToString());
    }

    [Fact]
    [Trait("Issue", "5042")]
    public async Task ResetPassword_DuringAnImpersonation_RecordsTheUnlockByTheRealAdmin()
    {
        // Durante un'impersonazione il cookie è dell'utente impersonato, ma chi agisce è l'admin: l'audit va a lui.
        Guid superAdminId, impersonatedId;
        string impersonationToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (superAdminId, _) = await TestSessionHelper.CreateSuperAdminSessionAsync(db);
            (impersonatedId, _) = await TestSessionHelper.CreateUserSessionAsync(db);
            impersonationToken = await TestSessionHelper.CreateImpersonationSessionAsync(db, impersonatedId, superAdminId);
        }
        var (userId, _) = await SeedUserAsync(FreshSecret(), lockedUntil: DateTime.UtcNow.AddMinutes(30), failedAttempts: 3);

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/reset-password", impersonationToken, new { newPassword = FreshSecret() }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var payload = (await AccountUnlockedPayloadsAsync(userId)).Should().ContainSingle().Subject;
        payload.Should().Contain(superAdminId.ToString());
        payload.Should().NotContain(impersonatedId.ToString(), "the impersonated user unlocked nothing");
    }

    [Fact]
    public async Task UpdateUser_SavesTheNewEmailAndDisplayName()
    {
        var (userId, _) = await SeedUserAsync(FreshSecret());
        var newEmail = $"updated-{Guid.NewGuid():N}@test.local";

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/admin/users/{userId}", _adminToken,
            new { email = newEmail, displayName = "Nome cambiato dall'admin" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var saved = await ReadUserAsync(userId);
        saved.Email.Should().Be(newEmail);
        saved.DisplayName.Should().Be("Nome cambiato dall'admin");
    }

    [Fact]
    public async Task UnlockAccount_ClearsTheLockoutInTheDatabase()
    {
        var secret = FreshSecret();
        var (userId, email) = await SeedUserAsync(secret, lockedUntil: DateTime.UtcNow.AddMinutes(15), failedAttempts: 5);

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/unlock", _adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var saved = await ReadUserAsync(userId);
        saved.LockedUntil.Should().BeNull();
        saved.FailedLoginAttempts.Should().Be(0);

        // Il login, che prima rifiutava l'account bloccato, ora passa.
        (await LoginAsync(email, secret)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_OfASuperAdmin_ByAnAdmin_IsForbidden_AndTheOldPasswordStillWorks()
    {
        // Con il reset che salva, un admin si prenderebbe l'account di un superadmin: la regola di ADM-002 vale
        // anche per il ruolo del bersaglio.
        var before = FreshSecret();
        var (userId, _) = await SeedUserAsync(before, role: "superadmin");

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/reset-password", _adminToken, new { newPassword = FreshSecret() }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        PasswordHash.FromStored((await ReadUserAsync(userId)).PasswordHash!).Verify(before).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_EmailOfAnotherAdmin_ByAnAdmin_IsForbidden_AndNothingChanges()
    {
        var (userId, email) = await SeedUserAsync(FreshSecret(), role: "admin");

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/admin/users/{userId}", _adminToken,
            new { email = $"taken-over-{Guid.NewGuid():N}@test.local" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await ReadUserAsync(userId)).Email.Should().Be(email);
    }

    /// <summary>
    /// #5037, #5045: le due rotte in blocco non hanno più una pagina dal 2026-02-23 e saltavano le regole delle rotte
    /// singole. Il reset in blocco lasciava a un SuperAdmin l'account di un altro SuperAdmin, senza chiudere le sessioni né
    /// sbloccare; il cambio di ruolo rispondeva 422 a ogni richiesta, e riparato avrebbe lasciato a un Admin declassare un
    /// altro Admin. Se tornano, tornano con la guardia di ruolo su ogni bersaglio e con le regole del reset singolo.
    /// </summary>
    [Theory]
    [Trait("Issue", "5037")]
    [Trait("Issue", "5045")]
    [InlineData("/api/v1/admin/users/bulk/role-change")]
    [InlineData("/api/v1/admin/users/bulk/password-reset")]
    public async Task BulkRoleChangeAndBulkPasswordReset_AreGone(string route)
    {
        string superAdminToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, superAdminToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(db);
        }
        var (userId, _) = await SeedUserAsync(FreshSecret());

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, route, superAdminToken,
            new { userIds = new[] { userId }, newRole = "editor", newPassword = FreshSecret() }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
        (await ReadUserAsync(userId)).Role.Should().Be("user");
    }

    private async Task<(Guid UserId, string Email)> SeedUserAsync(
        string secret, DateTime? lockedUntil = null, int failedAttempts = 0, string role = "user")
    {
        var userId = Guid.NewGuid();
        var email = $"admin-writes-{Guid.NewGuid():N}@test.local";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Utente della prova",
            Role = role,
            PasswordHash = PasswordHash.Create(secret).Value,
            Tier = "free",
            EmailVerified = true, // altrimenti il login si ferma alla verifica (#4985)
            CreatedAt = DateTime.UtcNow,
            FailedLoginAttempts = failedAttempts,
            LockedUntil = lockedUntil,
        });
        await db.SaveChangesAsync();

        return (userId, email);
    }

    private async Task<UserEntity> ReadUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<UserEntity>().AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    /// <summary>I payload delle righe di outbox di <see cref="AccountUnlockedEvent"/> per l'utente.</summary>
    private async Task<List<string>> AccountUnlockedPayloadsAsync(Guid userId)
    {
        var eventType = EventTypeRegistry.AliasByType.TryGetValue(typeof(AccountUnlockedEvent), out var alias)
            ? alias
            : typeof(AccountUnlockedEvent).FullName!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var payloads = await db.Set<DomainEventOutboxEntity>().AsNoTracking()
            .Where(r => r.EventType == eventType)
            .Select(r => r.PayloadJson)
            .ToListAsync();
        return payloads.Where(p => p.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string secret) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = secret });

    /// <summary>Apre una sessione per un utente già seminato, col ruolo che ha.</summary>
    private async Task<string> OpenSessionAsync(Guid userId, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = role == "superadmin"
            ? await TestSessionHelper.CreateSuperAdminSessionAsync(db, userId)
            : await TestSessionHelper.CreateUserSessionAsync(db, userId);
        return token;
    }

    /// <summary>
    /// Chiede <c>/auth/me</c> con un token, da un client senza cookie: quello della classe ne conserva, e mescolerebbe
    /// la sessione sondata con quella dell'admin.
    /// </summary>
    private async Task<HttpResponseMessage> AuthMeAsync(string token)
    {
        using var probe = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        return await probe.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/auth/me", token));
    }
}
