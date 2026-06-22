using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.BoundedContexts.Authentication.Application.Behaviors;
using Api.BoundedContexts.Authentication.Application.Configuration;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Behaviors;

/// <summary>
/// Unit tests for the SP5 S3 T4 strict path of <see cref="TwoFactorEnforcementBehavior{TRequest,TResponse}"/>.
/// Covers the unit-level slices of acceptance scenarios S3-1 (happy), S3-2 (stale → step-up),
/// S3-3 (not enrolled → enroll), S3-7 (non-decorated regression), S3-8 (per-command MaxAge), plus
/// shadow-mode passthrough and the no-session defensive case. The full request→pipeline acceptance
/// runs in T8 integration tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TwoFactorEnforcementBehaviorTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<ITwoFactorEnforcementConfiguration> _config = new();
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.Parse("2026-05-26T12:00:00Z"));
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();

    public TwoFactorEnforcementBehaviorTests()
    {
        // Fresh-scope audit emission resolves AuditService; returning null makes GetRequiredService
        // throw, which the behavior swallows (forensic audit must never suppress the security gate).
        // This lets the unit tests exercise the THROW path without a real AuditService/DbContext.
        var scopeProvider = new Mock<IServiceProvider>();
        scopeProvider.Setup(p => p.GetService(typeof(Api.Services.AuditService))).Returns(null!);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopeProvider.Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
    }

    // ── Test command types ─────────────────────────────────────────────────────────────────────
    [RequireTwoFactor(Reason = "test 30min")]
    private sealed record Decorated30MinCommand : IRequest<string>;

    [RequireTwoFactor(MaxAgeMinutes = 5, Reason = "test 5min")]
    private sealed record Decorated5MinCommand : IRequest<string>;

    private sealed record PlainCommand : IRequest<string>;   // NOT decorated → S3-7

    private TwoFactorEnforcementBehavior<TRequest, string> CreateBehavior<TRequest>()
        where TRequest : IRequest<string>
        => new(
            _httpContextAccessor.Object,
            _config.Object,
            _timeProvider,
            _scopeFactory.Object,
            NullLogger<TwoFactorEnforcementBehavior<TRequest, string>>.Instance);

    private void SetupSession(bool isTwoFactorEnabled, DateTime? lastTotpVerifiedAt, bool impersonating = false)
    {
        var subject = MakeUser("bob@user.test", isTwoFactorEnabled: false);   // target — never the gate
        var actor = impersonating
            ? MakeUser("alice@admin.test", isTwoFactorEnabled)
            : null;
        // When not impersonating, the subject IS the actor — so the subject carries the 2FA flag.
        var effectiveSubject = impersonating ? subject : MakeUser("alice@admin.test", isTwoFactorEnabled);
        var principal = new Principal(effectiveSubject, actor);

        var sessionStatus = new SessionStatusDto(
            IsValid: true,
            Principal: principal,
            ExpiresAt: _timeProvider.GetUtcNow().UtcDateTime.AddHours(1),
            LastSeenAt: _timeProvider.GetUtcNow().UtcDateTime,
            LastTotpVerifiedAt: lastTotpVerifiedAt);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[nameof(SessionStatusDto)] = sessionStatus;
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private static UserDto MakeUser(string email, bool isTwoFactorEnabled)
        => new(
            Id: Guid.NewGuid(),
            Email: email,
            DisplayName: email.Split('@')[0],
            Role: "superadmin",
            Tier: "free",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: isTwoFactorEnabled,
            TwoFactorEnabledAt: isTwoFactorEnabled ? DateTime.UtcNow : null,
            Level: 1,
            ExperiencePoints: 0);

    [Fact]
    public async Task StrictMode_FreshTotp_AllowsCommand()
    {
        // S3-1: 2FA enabled + verified 5min ago (< 30min MaxAge) → proceeds.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: true, lastTotpVerifiedAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-5));
        var behavior = CreateBehavior<Decorated30MinCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var result = await behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task StrictMode_StaleTotp_ThrowsStepUpRequired()
    {
        // S3-2: verified 45min ago (> 30min MaxAge) → blocked with step_up_required.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: true, lastTotpVerifiedAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-45));
        var behavior = CreateBehavior<Decorated30MinCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var act = () => behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TwoFactorRequiredException>();
        ex.Which.Subcode.Should().Be(TwoFactorRequiredSubcode.StepUpRequired);
        called.Should().BeFalse("the command handler must not run when 2FA is stale");
    }

    [Fact]
    public async Task StrictMode_NotEnrolled_ThrowsEnrollRequired()
    {
        // S3-3: actor has no 2FA → hard block with enroll_required (even if LastTotpVerifiedAt set).
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: false, lastTotpVerifiedAt: _timeProvider.GetUtcNow().UtcDateTime);
        var behavior = CreateBehavior<Decorated30MinCommand>();

        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        var act = () => behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TwoFactorRequiredException>();
        ex.Which.Subcode.Should().Be(TwoFactorRequiredSubcode.EnrollRequired);
    }

    [Fact]
    public async Task StrictMode_NullLastTotp_ThrowsStepUpRequired()
    {
        // 2FA enrolled but never step-up'd on this session (LastTotpVerifiedAt null) → step_up_required.
        // This is the post-cutover state of every pre-existing admin session (the mass-lockout case
        // D-S3-1 mitigates with default=OFF + ops sweep).
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: true, lastTotpVerifiedAt: null);
        var behavior = CreateBehavior<Decorated30MinCommand>();

        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        var act = () => behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TwoFactorRequiredException>();
        ex.Which.Subcode.Should().Be(TwoFactorRequiredSubcode.StepUpRequired);
    }

    [Fact]
    public async Task StrictMode_NonDecoratedCommand_AlwaysProceeds()
    {
        // S3-7 regression guard: a command without [RequireTwoFactor] is never gated, even in
        // strict mode with no 2FA and no recency.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: false, lastTotpVerifiedAt: null);
        var behavior = CreateBehavior<PlainCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var result = await behavior.Handle(new PlainCommand(), next, CancellationToken.None);

        called.Should().BeTrue("non-decorated commands must short-circuit before any 2FA check");
        result.Should().Be("ok");
        _config.Verify(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>()), Times.Never,
            "the strict-mode flag should not even be read for non-decorated commands");
    }

    [Theory]
    [InlineData(6, false)]   // 6min recency vs 5min MaxAge → blocked
    public async Task StrictMode_PerCommandMaxAge_5MinCommand_BlocksAt6Min(int ageMinutes, bool expectAllowed)
    {
        // S3-8a: ImpersonationStart-style 5-min MaxAge rejects a 6-min-old verification...
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: true, lastTotpVerifiedAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-ageMinutes));
        var behavior = CreateBehavior<Decorated5MinCommand>();

        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        var act = () => behavior.Handle(new Decorated5MinCommand(), next, CancellationToken.None);

        expectAllowed.Should().BeFalse();
        await act.Should().ThrowAsync<TwoFactorRequiredException>();
    }

    [Fact]
    public async Task StrictMode_PerCommandMaxAge_30MinCommand_AllowsAt6Min()
    {
        // S3-8b: ...while the same 6-min-old verification satisfies a 30-min MaxAge command.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(isTwoFactorEnabled: true, lastTotpVerifiedAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-6));
        var behavior = CreateBehavior<Decorated30MinCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var result = await behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task ShadowMode_NotEnrolled_ProceedsWithoutThrowing()
    {
        // Strict OFF (default at deploy): the legacy shadow behavior logs + proceeds, never blocks.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        SetupSession(isTwoFactorEnabled: false, lastTotpVerifiedAt: null);
        var behavior = CreateBehavior<Decorated30MinCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var result = await behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        called.Should().BeTrue("shadow mode never blocks");
        result.Should().Be("ok");
    }

    [Fact]
    public async Task NoSession_ProceedsDefensively()
    {
        // No SessionStatusDto in HttpContext (internal/test caller) → don't block.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
        var behavior = CreateBehavior<Decorated30MinCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var result = await behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task StrictMode_Impersonation_GatesOnActorNotSubject()
    {
        // S3-6 (unit slice): during impersonation, the gate reads EffectiveActor (alice, the admin)
        // — NOT Subject (bob, the target who has no 2FA). Inherited LastTotpVerifiedAt is fresh, so
        // the command proceeds even though the subject has no 2FA.
        _config.Setup(c => c.GetStrictModeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupSession(
            isTwoFactorEnabled: true,   // applies to the ACTOR (alice) when impersonating
            lastTotpVerifiedAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-3),
            impersonating: true);
        var behavior = CreateBehavior<Decorated30MinCommand>();

        var called = false;
        RequestHandlerDelegate<string> next = (ct) => { called = true; return Task.FromResult("ok"); };

        var result = await behavior.Handle(new Decorated30MinCommand(), next, CancellationToken.None);

        called.Should().BeTrue("gate reads the actor's 2FA (alice), not the subject's (bob)");
        result.Should().Be("ok");
    }
}
