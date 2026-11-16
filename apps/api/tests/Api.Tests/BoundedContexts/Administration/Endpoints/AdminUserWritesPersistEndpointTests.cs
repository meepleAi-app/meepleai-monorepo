using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
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
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db);

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

    private Task<HttpResponseMessage> LoginAsync(string email, string secret) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = secret });
}
