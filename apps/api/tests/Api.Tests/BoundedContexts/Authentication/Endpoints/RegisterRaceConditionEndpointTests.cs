using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// HTTP integration tests for POST /api/v1/auth/register covering the C5 fix.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C5).
///
/// Pre-fix RegisterCommandHandler had two TOCTOU bugs:
///   1. <c>HasAnyUsersAsync</c> + first-user-is-admin: two concurrent first
///      registrations both observe an empty users table and both become Admin.
///   2. <c>GetByEmailAsync</c> followed by insert: two concurrent registrations
///      with the same email both pass the existence check; the second insert
///      then fails the DB unique index with a 500.
///
/// Post-fix:
///   - Role assignment is driven by an explicit BootstrapAdminToken from
///     configuration; no race-prone user-count check.
///   - SaveChanges wraps the unique violation as a 409 Conflict.
///
/// We exercise the real PostgreSQL unique constraint via Testcontainers — an
/// InMemory provider can't reproduce the email race.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class RegisterRaceConditionEndpointTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auth/register";
    private const string BootstrapToken = "test-bootstrap-token-for-c5-12345";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public RegisterRaceConditionEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"register_race_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var extraConfig = new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminToken"] = BootstrapToken,
        };

        _factory = IntegrationWebApplicationFactory.Create(connectionString, extraConfig: extraConfig)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Public registration must be enabled for /auth/register to
                    // skip the 403 RequirePublicRegistration filter.
                    services.RemoveAll(typeof(IConfigurationService));
                    var mockConfig = new Mock<IConfigurationService>();
                    mockConfig
                        .Setup(c => c.GetValueAsync<bool?>(
                            "Registration:PublicEnabled",
                            It.IsAny<bool?>(),
                            It.IsAny<string?>()))
                        .ReturnsAsync(true);
                    services.AddSingleton<IConfigurationService>(mockConfig.Object);
                });
            });

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
    public async Task Register_DuplicateEmail_5ConcurrentRequests_ExactlyOneSuccess()
    {
        // 5 concurrent registrations with the same email. The DB unique
        // index on Users.Email guarantees exactly one row commits; the
        // post-fix handler must surface the other four as 409 Conflict
        // rather than letting the unique violation propagate as 500.
        var email = $"race-{Guid.NewGuid():N}@test.local";

        var tasks = Enumerable.Range(0, 5).Select(i => Task.Run(() => _client.PostAsJsonAsync(
            Endpoint,
            new
            {
                Email = email,
                Password = "ValidPassword123!",
                DisplayName = $"Racer {i}"
            }))).ToArray();

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var nonSuccessCount = responses.Count(r => r.StatusCode != HttpStatusCode.OK);

        successCount.Should().Be(1,
            "the database unique index on email allows exactly one row to commit; " +
            "all other concurrent registrations must fail.");
        nonSuccessCount.Should().Be(4);

        // No 5xx — the unique violation must be translated to a 4xx (409 ideally,
        // 400 acceptable as a transitional pre-flight DomainException).
        responses
            .Where(r => r.StatusCode != HttpStatusCode.OK)
            .Should()
            .OnlyContain(r => (int)r.StatusCode >= 400 && (int)r.StatusCode < 500,
                "a unique-violation race must NEVER surface as 5xx — the C5 fix " +
                "wraps DbUpdateException as ConflictException (409) instead of " +
                "letting it bubble up as 500.");

        // DB invariant: exactly one user row for the contested email.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var rowCount = await db.Users.AsNoTracking().CountAsync(u => u.Email == email);
        rowCount.Should().Be(1);
    }

    [Fact]
    public async Task Register_WithBootstrapAdminToken_AssignsAdminRole()
    {
        var email = $"first-admin-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = "ValidPassword123!",
            DisplayName = "First Admin",
            BootstrapToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var role = await ReadRoleAsync(response);
        role.Should().Be("admin",
            "supplying the configured BootstrapAdminToken is the only path to " +
            "the first admin (replaces HasAnyUsersAsync first-user-is-admin).");
    }

    [Fact]
    public async Task Register_WithoutBootstrapToken_AssignsUserRole()
    {
        var email = $"plain-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = "ValidPassword123!",
            DisplayName = "Plain User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var role = await ReadRoleAsync(response);
        role.Should().Be("user");
    }

    [Fact]
    public async Task Register_WithWrongBootstrapToken_AssignsUserRole_NotAdmin()
    {
        var email = $"wrong-token-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = "ValidPassword123!",
            DisplayName = "Wrong Token",
            BootstrapToken = BootstrapToken + "-tampered"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var role = await ReadRoleAsync(response);
        role.Should().Be("user",
            "an invalid token must NEVER escalate privileges — the registration " +
            "still succeeds but as plain User.");
    }

    [Fact]
    public async Task Register_FirstUser_WithoutBootstrapToken_AssignsUserRole_NotAdmin()
    {
        // Regression guard for the removed "first user is admin" logic. The
        // isolated test DB is empty, so this registration IS the first one;
        // pre-fix it would land Role=Admin via HasAnyUsersAsync.
        var email = $"first-no-token-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Email = email,
            Password = "ValidPassword123!",
            DisplayName = "First User No Token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var role = await ReadRoleAsync(response);
        role.Should().Be("user",
            "the first registration without a bootstrap token must NOT inherit " +
            "Admin; the legacy HasAnyUsersAsync race path is removed by C5.");
    }

    [Fact]
    public async Task Register_5ConcurrentBootstrapTokenRegistrations_ProducesExactlyOneAdmin()
    {
        // F1 (auth security review): the original C5 implementation read the
        // SystemConfiguration BootstrapAdminCreated flag via FirstOrDefaultAsync
        // and wrote it back with the user UoW commit. Two concurrent valid-
        // token registrations could both observe flag=false and both produce
        // an Admin. After the F1 fix, exactly ONE caller wins the atomic
        // INSERT-or-UPDATE-WHERE-flag=false flip; the others fall back to
        // Role.User.
        var tasks = Enumerable.Range(0, 5).Select(i => Task.Run(() =>
            _client.PostAsJsonAsync(Endpoint, new
            {
                Email = $"bootstrap-race-{i}-{Guid.NewGuid():N}@test.local",
                Password = "ValidPassword123!",
                DisplayName = $"Bootstrap Racer {i}",
                BootstrapToken
            }))).ToArray();

        var responses = await Task.WhenAll(tasks);

        // All five HTTP calls succeed (each creates a user — only the role
        // differs); race resolution does NOT bubble up as 4xx/5xx for the
        // losers.
        responses.Should().OnlyContain(
            r => r.StatusCode == HttpStatusCode.OK,
            "every concurrent valid-token registration should still create " +
            "the user account; only the role differs.");

        // Exactly one of the five users is Admin.
        var adminCount = 0;
        foreach (var r in responses)
        {
            var role = await ReadRoleAsync(r);
            if (role == "admin") adminCount++;
        }

        adminCount.Should().Be(1,
            "the bootstrap-admin guard is single-use under concurrent load; " +
            "more than one Admin would mean the TOCTOU race called out by " +
            "Finding 1 in the security review is still open.");
    }

    [Fact]
    public async Task BootstrapAdminToken_ConstantTimeCompare_NoTimingLeak()
    {
        // Smoke test only. Construct a near-miss token (same length, last
        // char different) and assert the wall-clock difference between a
        // matching compare and a near-miss compare is small enough that an
        // attacker can't trivially distinguish them by remote timing.
        // Coarse threshold (75ms) absorbs HTTP + Mediator noise; a regression
        // to plain-string compare would still register as 0ms vs ~PBKDF2-time
        // because the bootstrap path runs first.
        const int Iterations = 8;
        var nearMiss = BootstrapToken[..^1] + "X"; // same length

        var matchTimings = new List<long>(Iterations);
        var missTimings = new List<long>(Iterations);

        for (var i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _client.PostAsJsonAsync(Endpoint, new
            {
                Email = $"timing-match-{Guid.NewGuid():N}@test.local",
                Password = "ValidPassword123!",
                DisplayName = $"Match {i}",
                BootstrapToken
            });
            sw.Stop();
            matchTimings.Add(sw.ElapsedMilliseconds);

            sw.Restart();
            await _client.PostAsJsonAsync(Endpoint, new
            {
                Email = $"timing-miss-{Guid.NewGuid():N}@test.local",
                Password = "ValidPassword123!",
                DisplayName = $"Miss {i}",
                BootstrapToken = nearMiss
            });
            sw.Stop();
            missTimings.Add(sw.ElapsedMilliseconds);
        }

        var matchMedian = Median(matchTimings);
        var missMedian = Median(missTimings);
        var diff = Math.Abs(matchMedian - missMedian);

        diff.Should().BeLessThan(75,
            $"match-median={matchMedian}ms vs miss-median={missMedian}ms — a >75ms " +
            "delta hints the token comparison is not constant-time. " +
            "Reach for CryptographicOperations.FixedTimeEquals.");
    }

    private static async Task<string?> ReadRoleAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!body.TryGetProperty("user", out var user)) return null;
        if (!user.TryGetProperty("role", out var role)) return null;
        return role.GetString();
    }

    private static long Median(List<long> values)
    {
        values.Sort();
        return values.Count % 2 == 1
            ? values[values.Count / 2]
            : (values[(values.Count / 2) - 1] + values[values.Count / 2]) / 2;
    }
}
