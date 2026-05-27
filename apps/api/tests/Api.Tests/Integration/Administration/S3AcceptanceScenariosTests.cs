using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.Authentication.Application.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
using OtpNet;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// SP5 Admin Security S3 — acceptance scenarios (T8). Exercises the REAL HTTP pipeline
/// (WebApplicationFactory + Testcontainers Postgres + login + cookies) for the 8 scenarios
/// S3-1..S3-8 documented in <c>audits/2026-05-26-s3-three-amigos-kickoff.md</c>.
///
/// Critical lesson from S2: tests MUST exercise the real pipeline via
/// <c>ValidateSessionQueryHandler</c> + <c>TwoFactorEnforcementBehavior</c>, NOT construct
/// <c>SessionStatusDto</c> manually.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Sprint", "SP5-S3")]
public sealed class S3AcceptanceScenariosTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _dbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    // Toggled per-test; the mocked ITwoFactorEnforcementConfiguration reads this field so a single
    // factory instance can serve scenarios with strict ON or OFF (and the per-command MaxAge tests).
    private bool _strictModeOn;

    public S3AcceptanceScenariosTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _dbName = $"s3_accept_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Enable public registration so SeedAdminAsync can call /auth/register.
                    services.RemoveAll(typeof(IConfigurationService));
                    var configurationMock = new Mock<IConfigurationService>();
                    configurationMock
                        .Setup(c => c.GetValueAsync<bool?>("Registration:PublicEnabled", It.IsAny<bool?>(), It.IsAny<string?>()))
                        .ReturnsAsync(true);
                    services.AddSingleton<IConfigurationService>(configurationMock.Object);

                    // Strict 2FA mode reads _strictModeOn on every call, so flipping it mid-test
                    // takes effect on the next request without a factory rebuild.
                    services.RemoveAll(typeof(ITwoFactorEnforcementConfiguration));
                    var twoFactorConfigMock = new Mock<ITwoFactorEnforcementConfiguration>();
                    twoFactorConfigMock.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() => _strictModeOn);
                    services.AddSingleton(twoFactorConfigMock.Object);

                    // Redis counter simulation (pattern from TwoFactorSecurityPenetrationTests):
                    // the default factory's Redis mock returns nothing for StringIncrementAsync, so
                    // TotpService's lockout counter never trips. Wire a ConcurrentDictionary-backed
                    // mock that actually counts — required by S3-5 to reach the 5-fail threshold.
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var counters = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
                    var redisMock = new Mock<IConnectionMultiplexer>();
                    var dbMock = new Mock<IDatabase>();
                    redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
                    dbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                        .ReturnsAsync((RedisKey key, long inc, CommandFlags _) =>
                            counters.AddOrUpdate(key.ToString(), inc, (_, current) => current + inc));
                    dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                        .ReturnsAsync((RedisKey key, CommandFlags _) =>
                            counters.TryGetValue(key.ToString(), out var v) ? (RedisValue)v : RedisValue.Null);
                    dbMock.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
                        .ReturnsAsync(true);
                    services.AddSingleton(redisMock.Object);
                });
            });

        // Migrate the isolated database.
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
        try { await _fixture.DropIsolatedDatabaseAsync(_dbName); }
        catch { /* ignore cleanup */ }
    }

    // ================== HELPERS ==================

    /// <summary>
    /// Registers a user via the public HTTP endpoint, then raises the role on the entity. Returns
    /// the user id. The session created by registration is discarded (we log in fresh per test).
    /// </summary>
    private async Task<(Guid UserId, Guid SessionId)> SeedAdminAsync(string email, string password, string role = "admin")
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { Email = email, Password = password, DisplayName = email.Split('@')[0] });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"the test fixture expects a clean registration; got {await registerResponse.Content.ReadAsStringAsync()}");

        // WebApplicationFactory.CreateClient() does not auto-persist cookies across requests, so
        // extract the session cookie from Set-Cookie and pin it on DefaultRequestHeaders for every
        // subsequent call. Pattern lifted from AuthenticationEndpointsIntegrationTests.
        var setCookie = registerResponse.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains("meepleai_session"))
            .Split(';')[0];
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", setCookie);

        // Promote: registration creates a 'user'; bump the row directly for the scenarios that need
        // admin/superadmin role.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();

        var session = await db.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstAsync();
        return (user.Id, session.Id);
    }

    /// <summary>
    /// Creates a UserEntity directly in the DB (no HTTP) — used to seed impersonation targets and
    /// other "passive" users that don't need a session/cookie of their own.
    /// </summary>
    private async Task<Guid> SeedTargetUserAsync(string email, string role = "user")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var id = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = id,
            Email = email,
            DisplayName = email.Split('@')[0],
            Role = role,
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Seeds an admin with 2FA enabled, using the real TotpService so the encrypted secret in the DB
    /// matches what production stores. Returns the plaintext secret so the test can later mint valid
    /// codes via <see cref="GenerateTotpCode"/>.
    /// </summary>
    private async Task<(Guid UserId, Guid SessionId, string TotpSecret)> SeedAdminWith2FAAsync(
        string email, string password, string role = "admin")
    {
        var (userId, sessionId) = await SeedAdminAsync(email, password, role);

        using var scope = _factory.Services.CreateScope();
        var totpService = scope.ServiceProvider.GetRequiredService<ITotpService>();
        var setup = await totpService.GenerateSetupAsync(userId, email, CancellationToken.None);
        var validCode = GenerateTotpCode(setup.Secret);
        await totpService.EnableTwoFactorAsync(userId, validCode, CancellationToken.None);
        return (userId, sessionId, setup.Secret);
    }

    private static string GenerateTotpCode(string base32Secret)
        => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    /// <summary>
    /// Manipulates <c>LastTotpVerifiedAt</c> on a session row directly — the only way to model the
    /// "fresh" vs "stale" inputs without waiting wall-clock time.
    /// </summary>
    private async Task SetLastTotpVerifiedAtAsync(Guid sessionId, DateTime? when)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var session = await db.UserSessions.FirstAsync(s => s.Id == sessionId);
        session.LastTotpVerifiedAt = when;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Re-pins the HttpClient's Cookie header from the response's Set-Cookie. Required when an
    /// endpoint mints a new session token (e.g. impersonation start ⇒ new session row + new cookie).
    /// </summary>
    private void RefreshClientCookieFromResponse(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies)) return;
        var sessionCookie = setCookies.FirstOrDefault(c => c.Contains("meepleai_session"));
        if (sessionCookie is null) return;
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", sessionCookie.Split(';')[0]);
    }

    private static async Task<(string error, string? subcode, int? retryAfterSeconds)> ParseTwoFactorErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        var error = root.GetProperty("error").GetString() ?? "";
        var subcode = root.TryGetProperty("subcode", out var s) ? s.GetString() : null;
        int? retry = root.TryGetProperty("retryAfterSeconds", out var r) && r.ValueKind != System.Text.Json.JsonValueKind.Null
            ? r.GetInt32() : null;
        return (error, subcode, retry);
    }

    // ================== SCENARIOS ==================

    [Fact]
    public async Task S3_1_FreshTotp_AllowsDecoratedCommand_HappyPath()
    {
        // Given a superadmin with 2FA enabled, LastTotpVerifiedAt=now (well under
        // ImpersonationStart's MaxAge=5), and strict mode ON.
        var (_, sessionId, _) = await SeedAdminWith2FAAsync(
            $"s3-1-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "superadmin");
        await SetLastTotpVerifiedAtAsync(sessionId, DateTime.UtcNow);
        var targetId = await SeedTargetUserAsync($"s3-1-target-{Guid.NewGuid():N}@meepleai.test");
        _strictModeOn = true;

        // When the admin starts an impersonation (decorated, MaxAge=5min).
        var response = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = targetId, Reason = "S3-1 happy path", DurationMinutes = 15 });

        // Then the enforcement filter lets it through — fresh recency satisfies the gate.
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"fresh TOTP must allow the decorated command; body: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task S3_2_StaleTotp_BlockedWith_StepUpRequired()
    {
        // Given a superadmin with 2FA enabled but LastTotpVerifiedAt=now-1h (well past every
        // command's MaxAge), and strict mode ON.
        var (_, sessionId, _) = await SeedAdminWith2FAAsync(
            $"s3-2-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "superadmin");
        await SetLastTotpVerifiedAtAsync(sessionId, DateTime.UtcNow.AddHours(-1));
        var targetId = await SeedTargetUserAsync($"s3-2-target-{Guid.NewGuid():N}@meepleai.test");
        _strictModeOn = true;

        // When the admin starts an impersonation.
        var response = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = targetId, Reason = "S3-2 stale", DurationMinutes = 15 });

        // Then enforcement blocks → 401 + step_up_required + WWW-Authenticate.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("WWW-Authenticate").Should().ContainSingle()
            .Which.Should().Be("TOTP-StepUp realm=\"meepleai-admin\"");

        var (error, subcode, _) = await ParseTwoFactorErrorAsync(response);
        error.Should().Be("two_factor_required");
        subcode.Should().Be("step_up_required");
    }

    [Fact]
    public async Task S3_4_StepUpAndRetry_UnblocksTheDecoratedCommand()
    {
        // Given a superadmin with stale 2FA recency, strict mode ON, and a target to impersonate.
        var (_, sessionId, totpSecret) = await SeedAdminWith2FAAsync(
            $"s3-4-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "superadmin");
        await SetLastTotpVerifiedAtAsync(sessionId, DateTime.UtcNow.AddHours(-1));
        var targetId = await SeedTargetUserAsync($"s3-4-target-{Guid.NewGuid():N}@meepleai.test");
        _strictModeOn = true;

        // 1) The original command is blocked: 401 step_up_required.
        var blocked = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = targetId, Reason = "S3-4 first attempt", DurationMinutes = 15 });
        blocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ParseTwoFactorErrorAsync(blocked)).subcode.Should().Be("step_up_required");

        // 2) Step-up with a fresh, valid TOTP code → 200 refreshes LastTotpVerifiedAt.
        var validCode = GenerateTotpCode(totpSecret);
        var stepUp = await _client.PostAsJsonAsync("/api/v1/auth/2fa/step-up", new { Code = validCode });
        stepUp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"valid code should unblock; body: {await stepUp.Content.ReadAsStringAsync()}");

        // Side-effect verified on the row, not on a hand-built DTO (S2 lesson).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var session = await db.UserSessions.AsNoTracking().FirstAsync(s => s.Id == sessionId);
            session.LastTotpVerifiedAt.Should().NotBeNull("step-up writes session recency");
            session.LastTotpVerifiedAt!.Value.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        }

        // 3) Retry the originally-blocked command → 201, now allowed.
        var retry = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = targetId, Reason = "S3-4 retry", DurationMinutes = 15 });
        retry.StatusCode.Should().Be(HttpStatusCode.Created,
            $"fresh recency should pass enforcement on the retry; body: {await retry.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task S3_8_PerCommandMaxAge_FreshFor30ButStaleFor5()
    {
        // Given a superadmin with 2FA and LastTotpVerifiedAt=now-10min. ImpersonationStart has
        // MaxAge=5 → stale for it. DeleteUser (default MaxAge=30) → fresh for it.
        var (_, sessionId, _) = await SeedAdminWith2FAAsync(
            $"s3-8-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "superadmin");
        await SetLastTotpVerifiedAtAsync(sessionId, DateTime.UtcNow.AddMinutes(-10));
        var targetId = await SeedTargetUserAsync($"s3-8-target-{Guid.NewGuid():N}@meepleai.test");
        _strictModeOn = true;

        // 10min is STALE for ImpersonationStart (MaxAge=5) → 401 step_up_required.
        var stricterResponse = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = targetId, Reason = "S3-8 stricter", DurationMinutes = 15 });
        stricterResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "MaxAge=5 fires before MaxAge=30 — tighter ceiling on privileged commands (D-S3-7)");
        var (_, stricterSubcode, _) = await ParseTwoFactorErrorAsync(stricterResponse);
        stricterSubcode.Should().Be("step_up_required");

        // 10min is FRESH for DeleteUser (MaxAge=30) → 204 No Content (the route's success status).
        var lenientResponse = await _client.DeleteAsync($"/api/v1/admin/users/{targetId}");
        lenientResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"MaxAge=30 still tolerates 10min recency; got {await lenientResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task S3_3_NotEnrolledAdmin_BlockedWith_EnrollRequired()
    {
        // Given a superadmin with NO 2FA enabled and strict mode ON.
        var adminEmail = $"s3-3-admin-{Guid.NewGuid():N}@meepleai.test";
        await SeedAdminAsync(adminEmail, "UnusualPwd123!", role: "superadmin");
        var targetId = await SeedTargetUserAsync($"s3-3-target-{Guid.NewGuid():N}@meepleai.test");
        _strictModeOn = true;

        // When the admin tries to start an impersonation (decorated with [RequireTwoFactor]).
        var response = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = targetId, Reason = "S3-3 acceptance test", DurationMinutes = 15 });

        // Then the enforcement filter blocks BEFORE the command runs.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("WWW-Authenticate").Should().ContainSingle()
            .Which.Should().Be("TOTP-StepUp realm=\"meepleai-admin\"");

        var (error, subcode, _) = await ParseTwoFactorErrorAsync(response);
        error.Should().Be("two_factor_required");
        subcode.Should().Be("enroll_required",
            "the admin has IsTwoFactorEnabled=false → must enrol before strict can succeed (D-S3-5)");
    }

    [Fact]
    public async Task S3_5_FiveFailedStepUps_LeadsToLockout()
    {
        // Given an admin with 2FA enrolled and a stale recency (so step-up is the only path).
        var (_, sessionId, _) = await SeedAdminWith2FAAsync(
            $"s3-5-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "admin");
        await SetLastTotpVerifiedAtAsync(sessionId, DateTime.UtcNow.AddHours(-1));

        // 5 failed step-up attempts (wrong code).
        for (int i = 1; i <= 5; i++)
        {
            var fail = await _client.PostAsJsonAsync("/api/v1/auth/2fa/step-up", new { Code = "000000" });
            fail.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"failed attempt #{i}");
            var (_, failSubcode, _) = await ParseTwoFactorErrorAsync(fail);
            failSubcode.Should().Be("invalid_code", $"attempt #{i} should be invalid_code, not locked yet");
        }

        // 6th attempt: the lockout pre-check returns LockedOut → 401 with subcode locked_out
        // and retryAfterSeconds populated (D-S3-4b).
        var locked = await _client.PostAsJsonAsync("/api/v1/auth/2fa/step-up", new { Code = "000000" });
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (error, subcode, retry) = await ParseTwoFactorErrorAsync(locked);
        error.Should().Be("two_factor_required");
        subcode.Should().Be("locked_out", "5 failures within the window must trigger the lockout pre-check");
        retry.Should().Be(900, "retryAfterSeconds is a fixed 15-minute client hint");
    }

    [Fact]
    public async Task S3_6_ImpersonationContext_ActorsRecencyApplies()
    {
        // Alice (superadmin with 2FA fresh) starts impersonating Bob — that requires a fresh
        // ImpersonationStart MaxAge=5, so we set LastTotpVerifiedAt=now first.
        var (aliceId, _, _) = await SeedAdminWith2FAAsync(
            $"s3-6-alice-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "superadmin");
        var bobId = await SeedTargetUserAsync($"s3-6-bob-{Guid.NewGuid():N}@meepleai.test");
        var carolId = await SeedTargetUserAsync($"s3-6-carol-{Guid.NewGuid():N}@meepleai.test");

        // Refresh alice's recency (SeedAdminWith2FAAsync leaves it at the register-time default = null).
        // Capture the exact value so we can later assert the impersonate session inherited it.
        var aliceFreshTime = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var aliceSession = await db.UserSessions
                .Where(s => s.UserId == aliceId && s.RevokedAt == null && s.ImpersonatedByUserId == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync();
            aliceSession.LastTotpVerifiedAt = aliceFreshTime;
            await db.SaveChangesAsync();
        }
        _strictModeOn = true;

        var impStart = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = bobId, Reason = "S3-6 acceptance impersonation context", DurationMinutes = 15 });
        impStart.StatusCode.Should().Be(HttpStatusCode.Created,
            $"impersonation start should succeed; body: {await impStart.Content.ReadAsStringAsync()}");
        RefreshClientCookieFromResponse(impStart);

        // F3 pre-overwrite assertion: the impersonate session MUST inherit alice's recency from
        // the REAL HTTP pipeline (ValidateSessionQueryHandler → HttpContext.Items[SessionStatusDto]
        // → ImpersonationStartCommandHandler.ExtractCallerLastTotpVerifiedAt → impersonate row).
        // Without this, the overwrite below would mask a regression where the propagation silently
        // returns null. Companion regression-only test: Regression_ImpersonationInheritsActorRecency_ViaRealPipeline.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var impAfterStart = await db.UserSessions
                .Where(s => s.ImpersonatedByUserId == aliceId && s.RevokedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync();
            impAfterStart.LastTotpVerifiedAt.Should().BeCloseTo(aliceFreshTime, TimeSpan.FromSeconds(5),
                "ExtractCallerLastTotpVerifiedAt must propagate the actor's LastTotpVerifiedAt from "
                + "the live SessionStatusDto in HttpContext.Items — fixture-only injection would mask "
                + "a regression in the real ValidateSession pipeline (S2 lesson)");
        }

        // The impersonate session inherits alice's recency (T1 spike). Move that recency back to
        // -10min: STALE for ImpersonationStart (MaxAge=5) but FRESH for DeleteUser (MaxAge=30).
        Guid impSessionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var imp = await db.UserSessions
                .Where(s => s.ImpersonatedByUserId == aliceId && s.RevokedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync();
            imp.LastTotpVerifiedAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
            impSessionId = imp.Id;
        }

        // From the impersonate session, delete Carol — must succeed: the gate uses the ACTOR's
        // recency (alice's 10min, inherited at impersonation-start), and DeleteUser MaxAge=30 covers it.
        var delete = await _client.DeleteAsync($"/api/v1/admin/users/{carolId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"actor's 10min recency satisfies DeleteUser MaxAge=30 from the impersonate session; got {await delete.Content.ReadAsStringAsync()}");
        impSessionId.Should().NotBe(Guid.Empty);  // smoke the row exists; sanity for the SetLast above
    }

    [Fact]
    public async Task S3_7_NonDecoratedCommand_PassesEvenInStrictMode_RegressionGuard()
    {
        // Given strict 2FA enforcement is ON and a user with NO 2FA enrolled.
        var email = $"s3-7-{Guid.NewGuid():N}@meepleai.test";
        await SeedAdminAsync(email, "UnusualPwd123!", role: "user");
        _strictModeOn = true;

        // When the user calls an endpoint backed by a query/command NOT decorated with
        // [RequireTwoFactor] (here: GET /auth/me → GetCurrentUserQuery).
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Then the regression guard in TwoFactorEnforcementBehavior short-circuits and the request
        // succeeds — strict mode must NEVER touch non-decorated commands.
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "TwoFactorEnforcementBehavior.S3-7 regression guard: missing [RequireTwoFactor] = no gate");
    }

    /// <summary>
    /// F3 follow-up regression test (post-merge of PR #1597): isolates the propagation of the
    /// actor's <c>LastTotpVerifiedAt</c> through the REAL HTTP pipeline
    /// (<c>SessionAuthenticationMiddleware</c> → <c>ValidateSessionQueryHandler</c> →
    /// <c>HttpContext.Items[SessionStatusDto]</c> →
    /// <c>ImpersonationStartCommandHandler.ExtractCallerLastTotpVerifiedAt</c> → impersonate row).
    ///
    /// S3_6 exercises this propagation but then immediately overwrites the inherited value, so a
    /// regression where <c>ExtractCallerLastTotpVerifiedAt</c> silently returns <c>null</c> would
    /// still PASS S3_6's DeleteUser assertion. This test asserts ONLY the propagation, with no
    /// overwrite. Aligns with the S2 lesson <c>feedback_acceptance_tests_must_exercise_real_pipeline</c>
    /// — the unit test <c>Handle_HappyPath_InheritsActorLastTotpVerifiedAtFromHttpContext</c>
    /// constructs <c>SessionStatusDto</c> manually and would not catch a regression in the
    /// <c>ValidateSession</c> hydration step.
    /// </summary>
    [Fact]
    public async Task Regression_ImpersonationInheritsActorRecency_ViaRealPipeline()
    {
        var aliceFreshTime = DateTime.UtcNow;
        var (aliceId, _, _) = await SeedAdminWith2FAAsync(
            $"f3-alice-{Guid.NewGuid():N}@meepleai.test", "UnusualPwd123!", role: "superadmin");
        var bobId = await SeedTargetUserAsync($"f3-bob-{Guid.NewGuid():N}@meepleai.test");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var aliceSession = await db.UserSessions
                .Where(s => s.UserId == aliceId && s.RevokedAt == null && s.ImpersonatedByUserId == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync();
            aliceSession.LastTotpVerifiedAt = aliceFreshTime;
            await db.SaveChangesAsync();
        }
        _strictModeOn = true;

        var impStart = await _client.PostAsJsonAsync("/api/v1/admin/impersonation/start",
            new { TargetUserId = bobId, Reason = "F3 regression: real-pipeline recency inheritance", DurationMinutes = 15 });
        impStart.StatusCode.Should().Be(HttpStatusCode.Created,
            $"impersonation start should succeed; body: {await impStart.Content.ReadAsStringAsync()}");

        // Assert the impersonate session row carries alice's recency (no mutation between start and read).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var imp = await db.UserSessions
                .Where(s => s.ImpersonatedByUserId == aliceId && s.RevokedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync();

            imp.LastTotpVerifiedAt.Should().BeCloseTo(aliceFreshTime, TimeSpan.FromSeconds(5),
                "ImpersonationStartCommandHandler.ExtractCallerLastTotpVerifiedAt must read alice's "
                + "LastTotpVerifiedAt from HttpContext.Items[SessionStatusDto] (populated by the real "
                + "ValidateSessionQueryHandler) and write it into the new impersonate session row. "
                + "A null value here indicates ValidateSession is failing to hydrate "
                + "LastTotpVerifiedAt into SessionStatusDto exposed to the command handler.");
        }
    }
}
