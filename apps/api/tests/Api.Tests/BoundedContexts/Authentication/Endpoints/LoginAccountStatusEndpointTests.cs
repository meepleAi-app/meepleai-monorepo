using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// HTTP integration tests for POST /api/v1/auth/login covering account-status gating.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C2 — Login
/// Status Check + Null-Safe VerifyPassword).
///
/// Pre-fix the login handler only short-circuits on <c>IsSuspended</c>, so:
///   - Pending users (PasswordHash == null) bleed through to <c>VerifyPassword</c>,
///     which now returns false (commit #01 null-safe), but the failure path still
///     runs <c>RecordFailedLogin</c> and reports "Invalid email or password",
///     amplifying lockout pressure on accounts that should never authenticate.
///   - Banned users carry <c>IsSuspended == true</c> for back-compat and surface as
///     "Account is suspended" rather than the unified "Account is not available".
///
/// Post-fix the handler must call <see cref="User.CanAuthenticate"/> *before* the
/// lockout check, so all three non-Active states return the same neutral response
/// without touching the failed-attempts counter.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class LoginAccountStatusEndpointTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auth/login";
    private const string AccountUnavailableMessage = "Account is not available";

    // A fresh value per call, generated at run time: the GitGuardian app flags any string literal
    // assigned to a password-named member, on every commit of a PR. Two calls never match, so a
    // second call is always a wrong credential for a user seeded with the first.
    private static string FreshValue() => $"x{Guid.NewGuid():N}!A1";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public LoginAccountStatusEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"login_status_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Login_PendingUser_ReturnsAccountNotAvailable()
    {
        // Arrange — admin-provisioned pending user with no password hash.
        var email = $"pending-{Guid.NewGuid():N}@test.local";
        await SeedPendingUserAsync(email);
        // NB: a "FailedLoginAttempts must not increment" assertion is intentionally
        // omitted — UserRepository.GetByEmailAsync returns a detached aggregate
        // (AsNoTracking), so RecordFailedLogin already cannot persist today,
        // making such an assertion vacuous regardless of the C2 fix.

        // Act
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = "AnyUnusualPwd123!"
        });

        // Assert — DomainException → 400. Message must be the unified "Account is not
        // available" so the handler doesn't leak that the account exists in a
        // non-active state via the old "Invalid email or password" path.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString()
            .Should().Be(AccountUnavailableMessage,
                "pending users must surface as Account is not available, not Invalid email or password.");
    }

    [Fact]
    public async Task Login_SuspendedUser_ReturnsAccountNotAvailable()
    {
        var email = $"suspended-{Guid.NewGuid():N}@test.local";
        const string password = "ValidUnusualPwd123!";
        await SeedActiveUserAsync(email, password, applyStatus: u => u.Suspend("test"));

        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString()
            .Should().Be(AccountUnavailableMessage,
                "suspended users must surface the unified Account is not available message; " +
                "the legacy 'Account is suspended' wording is replaced post-fix.");
    }

    [Fact]
    public async Task Login_BannedUser_ReturnsAccountNotAvailable()
    {
        var email = $"banned-{Guid.NewGuid():N}@test.local";
        const string password = "ValidUnusualPwd123!";
        await SeedActiveUserAsync(email, password, applyStatus: u => u.Ban("test"));

        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString()
            .Should().Be(AccountUnavailableMessage,
                "banned users must NOT leak the back-compat 'Account is suspended' message; " +
                "the unified Account is not available response is required by the C2 fix.");
    }

    [Fact]
    public async Task Login_ActiveUser_StillAuthenticates()
    {
        // Regression guard: Active users must keep passing the new CanAuthenticate gate.
        var email = $"active-{Guid.NewGuid():N}@test.local";
        const string password = "ValidUnusualPwd123!";
        await SeedActiveUserAsync(email, password);

        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Active users must still log in after the CanAuthenticate gate is wired in.");
    }

    // #4991: every login failure arrived as 400 "domain_error", so the client could not tell a wrong
    // password from a locked account except by reading the English sentence — and showed it. Each
    // case now has a stable code; status and message (for the logs) stay what they were.
    private static async Task<(string? Code, string? Message)> ErrorOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("error").GetString(), body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentialsCode()
    {
        var email = $"wrongpwd-{Guid.NewGuid():N}@test.local";
        await SeedActiveUserAsync(email, FreshValue());

        var response = await _client.PostAsJsonAsync(Endpoint, new { Email = email, Password = FreshValue() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(response)).Should().Be(("invalid_credentials", "Invalid email or password"));
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsInvalidCredentialsCode()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = $"nobody-{Guid.NewGuid():N}@test.local",
            Password = FreshValue()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(response)).Should().Be(("invalid_credentials", "Invalid email or password"));
    }

    [Fact]
    public async Task Login_AlreadyLockedUser_ReturnsAccountLockedCode()
    {
        var email = $"locked-{Guid.NewGuid():N}@test.local";
        var password = FreshValue();
        await SeedActiveUserAsync(email, password, applyStatus: u =>
        {
            for (var i = 0; i < User.DefaultMaxFailedAttempts; i++) u.RecordFailedLogin();
        });

        var response = await _client.PostAsJsonAsync(Endpoint, new { Email = email, Password = password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, message) = await ErrorOfAsync(response);
        code.Should().Be("account_locked");
        message.Should().StartWith("Account is locked. Please try again in ");
    }

    [Fact]
    public async Task Login_AttemptThatLocksTheAccount_ReturnsAccountLockedCode()
    {
        var email = $"locking-{Guid.NewGuid():N}@test.local";
        await SeedActiveUserAsync(email, FreshValue(), applyStatus: u =>
        {
            for (var i = 0; i < User.DefaultMaxFailedAttempts - 1; i++) u.RecordFailedLogin();
        });

        var response = await _client.PostAsJsonAsync(Endpoint, new { Email = email, Password = FreshValue() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, message) = await ErrorOfAsync(response);
        code.Should().Be("account_locked");
        message.Should().StartWith("Account has been locked due to too many failed login attempts.");
    }

    [Fact]
    public async Task Login_SuspendedUser_ReturnsAccountUnavailableCode()
    {
        var email = $"suspended-code-{Guid.NewGuid():N}@test.local";
        var password = FreshValue();
        await SeedActiveUserAsync(email, password, applyStatus: u => u.Suspend("test"));

        var response = await _client.PostAsJsonAsync(Endpoint, new { Email = email, Password = password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(response)).Should().Be(("account_unavailable", AccountUnavailableMessage));
    }

    private async Task SeedPendingUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var user = User.CreatePending(
            email: new Email(email),
            displayName: email.Split('@')[0],
            role: Role.User,
            tier: UserTier.Free,
            invitedByUserId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            timeProvider: TimeProvider.System);

        await users.AddAsync(user, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedActiveUserAsync(
        string email,
        string password,
        Action<User>? applyStatus = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var user = new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: email.Split('@')[0],
            passwordHash: PasswordHash.Create(password),
            role: Role.User);

        applyStatus?.Invoke(user);

        await users.AddAsync(user, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);
    }

}
