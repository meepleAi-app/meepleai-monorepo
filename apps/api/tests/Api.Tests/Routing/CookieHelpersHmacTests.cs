using Api.Models;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Tests for C4 — Cookie Role HMAC + Versioning.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C4).
///
/// Pre-fix the role cookie (<c>meepleai_user_role</c>) is written as a plain
/// string so any client with cookie-edit ability — a malicious browser
/// extension, a misconfigured forward proxy, the user themselves — can flip
/// "user" to "admin" and gate around the Next.js middleware admin guard.
///
/// Post-fix the cookie is HMAC-protected by ASP.NET Data Protection and named
/// <c>meepleai_user_role_v2</c>. Reads accept both formats during a 7-day
/// grace period so existing user sessions don't get broken on deploy day.
///
/// These tests use <see cref="EphemeralDataProtectionProvider"/> so each test
/// gets its own short-lived key — no Docker, no shared state.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CookieHelpersHmacTests
{
    private const string V1Name = "meepleai_user_role";
    private const string V2Name = "meepleai_user_role_v2";

    [Fact]
    public void WriteUserRoleCookie_WritesV2_WithProtectedValue()
    {
        var ctx = CreateContext(out var protector);

        CookieHelpers.WriteUserRoleCookie(ctx, "admin", DateTime.UtcNow.AddDays(30));

        var setCookies = ctx.Response.Headers.SetCookie.ToArray();
        var v2Header = setCookies.SingleOrDefault(c =>
            c is not null && c.StartsWith(V2Name + "=", StringComparison.Ordinal));
        v2Header.Should().NotBeNull(
            $"the v2 cookie '{V2Name}' must be issued on every login / role change.");

        var v2Value = ExtractCookieValue(v2Header!, V2Name);
        v2Value.Should().NotBe("admin",
            "the role cookie value must be HMAC-protected, not plaintext " +
            "— a plaintext cookie can be edited client-side to escalate privilege.");

        // Round-trip the protected value to prove it actually decrypts to "admin".
        protector.Unprotect(v2Value).Should().Be("admin",
            "the protected value must round-trip via the same DataProtection purpose.");
    }

    [Fact]
    public void WriteUserRoleCookie_DeletesV1_LazyMigration()
    {
        var ctx = CreateContext(out _);

        CookieHelpers.WriteUserRoleCookie(ctx, "user", DateTime.UtcNow.AddDays(30));

        var setCookies = ctx.Response.Headers.SetCookie.ToArray();
        var v1Header = setCookies.SingleOrDefault(c =>
            c is not null && c.StartsWith(V1Name + "=", StringComparison.Ordinal));
        v1Header.Should().NotBeNull(
            $"every WriteUserRoleCookie must also emit a Set-Cookie that *expires* the " +
            $"legacy plaintext '{V1Name}' so the browser cleans it up — without this " +
            $"lazy-migration step the v1 cookie remains usable until natural expiration.");

        v1Header!.Should().Contain("expires=Thu, 01 Jan 1970", "v1 deletion uses the Unix-epoch expires header.");
    }

    [Fact]
    public void ReadUserRoleCookie_ValidV2_RoundTripsRole()
    {
        var ctx = CreateContext(out var protector);

        CookieHelpers.WriteUserRoleCookie(ctx, "admin", DateTime.UtcNow.AddDays(30));

        // Carry the protected v2 value over to a fresh request scope.
        var v2Value = ExtractCookieValue(
            ctx.Response.Headers.SetCookie.Single(c =>
                c is not null && c.StartsWith(V2Name + "=", StringComparison.Ordinal))!,
            V2Name);

        var readCtx = CreateContextWithProtector(protector);
        readCtx.Request.Headers.Cookie = $"{V2Name}={v2Value}";

        var role = CookieHelpers.ReadUserRoleCookie(readCtx);

        role.Should().Be("admin");
    }

    [Fact]
    public void ReadUserRoleCookie_TamperedV2_ReturnsNull()
    {
        var ctx = CreateContext(out _);
        ctx.Request.Headers.Cookie = $"{V2Name}=clearly-not-a-valid-protected-payload";

        var role = CookieHelpers.ReadUserRoleCookie(ctx);

        role.Should().BeNull(
            "an unparseable / tampered v2 payload must fail closed — never fall back " +
            "to a default role and never throw.");
    }

    [Fact]
    public void ReadUserRoleCookie_WrongPurpose_ReturnsNull()
    {
        // Encrypt with a *different* DataProtection purpose to simulate either a
        // cross-purpose confused-deputy attack or a stale cookie minted by another
        // application. Either way, the read path must reject it.
        var provider = new EphemeralDataProtectionProvider();
        var attackerProtector = provider.CreateProtector("MeepleAi.SomeOtherCookie.v1");
        var bogus = attackerProtector.Protect("admin");

        var readCtx = CreateContextWithProvider(provider);
        readCtx.Request.Headers.Cookie = $"{V2Name}={bogus}";

        var role = CookieHelpers.ReadUserRoleCookie(readCtx);

        role.Should().BeNull(
            "a payload protected by a different DataProtection purpose must be rejected, " +
            "not silently accepted.");
    }

    [Fact]
    public void ReadUserRoleCookie_FallbackV1_ReturnsRole_DuringGracePeriod()
    {
        // Only a v1 cookie is present (legacy session pre-deploy). During the
        // grace window we must still authorise the user to avoid logging out
        // every existing session on deploy day.
        var ctx = CreateContext(out _);
        ctx.Request.Headers.Cookie = $"{V1Name}=admin";

        var role = CookieHelpers.ReadUserRoleCookie(ctx);

        role.Should().Be("admin",
            "during the 7-day v1 grace period a legacy plaintext cookie must still " +
            "yield the role so existing sessions survive deploy.");
    }

    // -------------------------------------------------------------------------
    // Test scaffolding
    // -------------------------------------------------------------------------

    private const string DataProtectionPurpose = "MeepleAi.UserRoleCookie.v2";

    private static HttpContext CreateContext(out IDataProtector protector)
    {
        var provider = new EphemeralDataProtectionProvider();
        protector = provider.CreateProtector(DataProtectionPurpose);
        return CreateContextWithProvider(provider);
    }

    private static HttpContext CreateContextWithProtector(IDataProtector protector)
    {
        // Reuse the *same* protector across two contexts by wrapping a provider
        // that returns it. This mirrors the production case where the same
        // DataProtection key ring serves multiple requests.
        var providerMock = new Mock<IDataProtectionProvider>();
        providerMock.Setup(p => p.CreateProtector(DataProtectionPurpose)).Returns(protector);
        return CreateContextWithProvider(providerMock.Object);
    }

    private static HttpContext CreateContextWithProvider(IDataProtectionProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(provider);
        services.AddSingleton<IOptions<SessionCookieConfiguration>>(
            Options.Create(new SessionCookieConfiguration { Secure = false }));

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Production"); // bypass dev SameSite=None workaround
        services.AddSingleton(envMock.Object);

        var sp = services.BuildServiceProvider();
        var ctx = new DefaultHttpContext { RequestServices = sp };
        return ctx;
    }

    private static string ExtractCookieValue(string setCookieHeader, string cookieName)
    {
        // "name=value; Path=/; HttpOnly; ..." → "value"
        var prefix = cookieName + "=";
        var startIdx = setCookieHeader.IndexOf(prefix, StringComparison.Ordinal);
        startIdx.Should().BeGreaterThanOrEqualTo(0);
        var valueStart = startIdx + prefix.Length;
        var semiIdx = setCookieHeader.IndexOf(';', valueStart);
        return semiIdx < 0
            ? setCookieHeader[valueStart..]
            : setCookieHeader[valueStart..semiIdx];
    }
}
