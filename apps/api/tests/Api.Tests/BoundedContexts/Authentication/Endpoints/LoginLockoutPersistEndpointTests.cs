using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// La fixture di <see cref="LoginLockoutPersistEndpointTests"/>: host per classe, database per test.
/// </summary>
public sealed class LoginLockoutHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "login_lockout");

/// <summary>
/// #5036 — il blocco dopo i login falliti non veniva mai salvato.
/// </summary>
/// <remarks>
/// <c>UserRepository.GetByEmailAsync</c> restituisce un aggregato staccato. Il ramo della password sbagliata di
/// <c>LoginCommandHandler</c> chiamava <c>RecordFailedLogin</c> e poi solo <c>SaveChangesAsync</c>, che non aveva
/// niente da scrivere: il contatore restava a zero e l'account non si chiudeva mai. Ogni caso qui rilegge la riga
/// dal database, da un altro scope.
/// <para>
/// I parametri sono la decisione dell'utente del 2026-11-16: tre tentativi, trenta minuti. Sono scritti qui come
/// numeri, non letti dalle costanti del dominio, perché questo test è la specifica: se le costanti cambiano, deve
/// diventare rosso.
/// </para>
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "5036")]
public sealed class LoginLockoutPersistEndpointTests
    : IClassFixture<LoginLockoutHostFixture>, IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auth/login";
    private const int DecidedAttempts = 3;
    private const int DecidedMinutes = 30;

    private readonly LoginLockoutHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public LoginLockoutPersistEndpointTests(LoginLockoutHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory (#4050).
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
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
    public async Task ThreeWrongPasswords_LockTheAccountForThirtyMinutes_AndTheRightOneIsRefused()
    {
        var secret = FreshSecret();
        var (userId, email) = await SeedUserAsync(secret);

        for (var i = 1; i < DecidedAttempts; i++)
        {
            var refused = await LoginAsync(email, FreshSecret());
            (await ErrorCodeOfAsync(refused)).Should().Be("invalid_credentials", $"attempt {i} is below the threshold");
        }

        var before = DateTime.UtcNow;
        var locking = await LoginAsync(email, FreshSecret());
        var after = DateTime.UtcNow;

        locking.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeOfAsync(locking)).Should().Be("account_locked", "the third wrong password locks the account");

        var saved = await ReadUserAsync(userId);
        saved.FailedLoginAttempts.Should().Be(DecidedAttempts);
        saved.LockedUntil.Should().NotBeNull("the lock must be written, not only answered");
        saved.LockedUntil!.Value.Should().BeOnOrAfter(before.AddMinutes(DecidedMinutes).AddSeconds(-1))
            .And.BeOnOrBefore(after.AddMinutes(DecidedMinutes).AddSeconds(1));

        // La password giusta non apre un account bloccato.
        (await ErrorCodeOfAsync(await LoginAsync(email, secret))).Should().Be("account_locked");
    }

    [Fact]
    public async Task TheLockMessage_StatesTheDecidedDuration()
    {
        var (_, email) = await SeedUserAsync(FreshSecret(), failedAttempts: DecidedAttempts - 1);

        var response = await LoginAsync(email, FreshSecret());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().EndWith($"Please try again in {DecidedMinutes} minutes.");
    }

    [Fact]
    public async Task AfterTheLockExpires_OneWrongPassword_DoesNotLockAgain()
    {
        // Scaduto il blocco, il conto riparte: ogni blocco richiede di nuovo tre tentativi. Senza azzerarlo, il
        // primo errore dopo la scadenza (quarto tentativo, sopra la soglia) richiuderebbe l'account per mezz'ora.
        var (userId, email) = await SeedUserAsync(
            FreshSecret(), lockedUntil: DateTime.UtcNow.AddMinutes(-1), failedAttempts: DecidedAttempts);

        var response = await LoginAsync(email, FreshSecret());

        (await ErrorCodeOfAsync(response)).Should().Be("invalid_credentials");
        var saved = await ReadUserAsync(userId);
        saved.FailedLoginAttempts.Should().Be(1);
        saved.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task AfterTheLockExpires_TheRightPassword_LogsIn_AndClearsTheCounter()
    {
        var secret = FreshSecret();
        var (userId, email) = await SeedUserAsync(
            secret, lockedUntil: DateTime.UtcNow.AddMinutes(-1), failedAttempts: DecidedAttempts);

        (await LoginAsync(email, secret)).StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await ReadUserAsync(userId);
        saved.FailedLoginAttempts.Should().Be(0);
        saved.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task OneLock_RaisesAccountLockedEventOnce_EvenWithMoreAttemptsWhileLocked()
    {
        // L'email di blocco la manda il gestore di AccountLockedEvent. Ogni evento sollevato al salvataggio lascia una
        // riga nell'outbox, quindi le righe contano le email che partono: una per blocco.
        var secret = FreshSecret();
        var (userId, email) = await SeedUserAsync(secret);

        for (var i = 0; i < DecidedAttempts; i++)
            await LoginAsync(email, FreshSecret());

        // Altri tentativi ad account chiuso, sbagliati e giusti.
        await LoginAsync(email, FreshSecret());
        await LoginAsync(email, secret);

        (await CountAccountLockedEventsAsync(userId)).Should().Be(1);
    }

    [Fact]
    public async Task ALockoutSavedFromAStaleRead_KeepsWhatAnAdminSavedInTheMeantime()
    {
        // Il ramo della password sbagliata gira per chiunque conosca l'email, e fra la lettura dell'utente e il
        // salvataggio passa almeno la verifica della password. Se nel frattempo un admin cambia ruolo e password,
        // salvare il blocco non deve riscrivere quei campi con i valori letti prima.
        var (userId, email) = await SeedUserAsync(FreshSecret());
        var hashSavedByTheAdmin = PasswordHash.Create(FreshSecret()).Value;

        using var requestScope = _factory.Services.CreateScope();
        var users = requestScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = requestScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var staleUser = (await users.GetByEmailAsync(new Email(email)))!;

        using (var adminScope = _factory.Services.CreateScope())
        {
            var db = adminScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.Users.AsTracking().SingleAsync(u => u.Id == userId);
            row.Role = "editor";
            row.PasswordHash = hashSavedByTheAdmin;
            await db.SaveChangesAsync();
        }

        staleUser.RecordFailedLogin();
        await users.UpdateLockoutStateAsync(staleUser);
        await unitOfWork.SaveChangesAsync();

        var saved = await ReadUserAsync(userId);
        saved.FailedLoginAttempts.Should().Be(1, "the failed attempt is written");
        saved.Role.Should().Be("editor", "a role saved after the read must survive the lockout write");
        saved.PasswordHash.Should().Be(hashSavedByTheAdmin, "a password saved after the read must survive the lockout write");
    }

    private async Task<int> CountAccountLockedEventsAsync(Guid userId)
    {
        var eventType = EventTypeRegistry.AliasByType.TryGetValue(typeof(AccountLockedEvent), out var alias)
            ? alias
            : typeof(AccountLockedEvent).FullName!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var rows = await db.Set<DomainEventOutboxEntity>().AsNoTracking()
            .Where(r => r.EventType == eventType)
            .Select(r => r.PayloadJson)
            .ToListAsync();
        return rows.Count(payload => payload.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(Guid UserId, string Email)> SeedUserAsync(
        string secret, DateTime? lockedUntil = null, int failedAttempts = 0)
    {
        var userId = Guid.NewGuid();
        var email = $"lockout-{Guid.NewGuid():N}@test.local";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Utente della prova",
            Role = "user",
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
        _client.PostAsJsonAsync(Endpoint, new { Email = email, Password = secret });

    private static async Task<string?> ErrorCodeOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("error", out var error) ? error.GetString() : null;
    }
}
