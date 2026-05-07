using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for the C6 split of <see cref="TempSessionService"/>.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C6).
///
/// Pre-fix the only operation was <c>ValidateAndConsumeTempSessionAsync</c> —
/// validate and mark-as-used in a single atomic step. That left no room to
/// distinguish "wrong code, try again" from "wrong code, brute-force lock":
/// the temp session was either already consumed (legitimate verify path) or
/// still alive (every wrong code path), so brute-forcing the 5-minute TTL
/// was bounded only by the per-session-token rate limit (3/min). Re-login
/// minted a fresh temp token and reset the budget.
///
/// Post-fix the service exposes three operations:
///   - <c>ValidateTempSessionAsync</c>: validate WITHOUT consuming.
///   - <c>RecordFailedAttemptAsync</c>: bumps FailedAttemptCount; at 5 the
///     session is invalidated (IsUsed=true) so further valid codes are
///     rejected and the user must log in again.
///   - <c>ConsumeTempSessionAsync</c>: marks IsUsed (success path).
///
/// These tests run against the InMemory provider via TestDbContextFactory —
/// transactions are no-op there, but the failure-counter semantics are
/// independent of isolation level and are what we assert on.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class TempSessionServiceLockoutTests : IDisposable
{
    private const int MaxFailedAttempts = 5;

    private readonly MeepleAiDbContext _db;
    private readonly FakeTimeProvider _time;
    private readonly TempSessionService _service;

    public TempSessionServiceLockoutTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _service = new TempSessionService(
            _db,
            NullLogger<TempSessionService>.Instance,
            _time);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ValidateTempSession_FreshToken_ReturnsUserId_DoesNotConsume()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);

        var resolved = await _service.ValidateTempSessionAsync(token);

        resolved.Should().Be(userId);

        // Critically: validation must NOT mark the temp session as used.
        var entity = await _db.TempSessions.AsNoTracking().SingleAsync();
        entity.IsUsed.Should().BeFalse(
            "ValidateTempSessionAsync must read-only-check the session — " +
            "consuming is the explicit ConsumeTempSessionAsync step on the " +
            "success path.");
    }

    [Fact]
    public async Task RecordFailedAttempt_IncrementsCounter_WithoutInvalidating_BelowThreshold()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);

        for (var i = 1; i < MaxFailedAttempts; i++)
        {
            var invalidated = await _service.RecordFailedAttemptAsync(token);
            invalidated.Should().BeFalse(
                $"the {i}th failed attempt is below the {MaxFailedAttempts}-attempt threshold; " +
                "the session must remain valid so the user can retry.");
        }

        var entity = await _db.TempSessions.AsNoTracking().SingleAsync();
        entity.FailedAttemptCount.Should().Be(MaxFailedAttempts - 1);
        entity.IsUsed.Should().BeFalse();

        // The session is still resolvable as long as we're under threshold.
        var stillValid = await _service.ValidateTempSessionAsync(token);
        stillValid.Should().Be(userId);
    }

    [Fact]
    public async Task RecordFailedAttempt_AtThreshold_InvalidatesSession()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);

        bool? lastResult = null;
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            lastResult = await _service.RecordFailedAttemptAsync(token);
        }

        lastResult.Should().BeTrue(
            $"the {MaxFailedAttempts}th failed attempt must invalidate the session — " +
            "this is the C6 brute-force gate.");

        var entity = await _db.TempSessions.AsNoTracking().SingleAsync();
        entity.FailedAttemptCount.Should().Be(MaxFailedAttempts);
        entity.IsUsed.Should().BeTrue(
            "invalidation flips IsUsed so the temp session is no longer a " +
            "live target for further verify attempts (DoS-aware: the user " +
            "just has to re-login; there is no permanent lockout).");

        // Validation now fails — even a *valid* TOTP code at this point must
        // be rejected because the temp session is gone.
        var rejected = await _service.ValidateTempSessionAsync(token);
        rejected.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTempSession_AfterInvalidation_ReturnsNull_EvenIfTokenWouldOtherwiseBeFresh()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            await _service.RecordFailedAttemptAsync(token);
        }

        // Time hasn't advanced; the underlying token bytes are still valid;
        // only the FailedAttemptCount + IsUsed flag should keep us out.
        var resolved = await _service.ValidateTempSessionAsync(token);
        resolved.Should().BeNull(
            "an invalidated session must fail closed — pre-fix " +
            "ValidateAndConsumeTempSessionAsync only checked IsUsed, which " +
            "left a window for brute force as long as the temp session was " +
            "fresh and unused.");
    }

    [Fact]
    public async Task ConsumeTempSession_MarksUsed_OnSuccessPath()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);

        await _service.ConsumeTempSessionAsync(token);

        var entity = await _db.TempSessions.AsNoTracking().SingleAsync();
        entity.IsUsed.Should().BeTrue();
        entity.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordFailedAttempt_AfterInvalidation_DoesNotExtendLockoutOrUnflagSession()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            await _service.RecordFailedAttemptAsync(token);
        }

        var snapshotCount = (await _db.TempSessions.AsNoTracking().SingleAsync()).FailedAttemptCount;

        // A persistent attacker keeps hammering — the counter must NOT keep
        // climbing on an already-invalidated session, and IsUsed must stay
        // true. Returning true (=invalidated) is fine; the contract is just
        // "don't reopen the door".
        var afterPostInvalidation = await _service.RecordFailedAttemptAsync(token);
        afterPostInvalidation.Should().BeTrue();

        var entity = await _db.TempSessions.AsNoTracking().SingleAsync();
        entity.IsUsed.Should().BeTrue();
        entity.FailedAttemptCount.Should().Be(snapshotCount,
            "post-invalidation attempts must not keep mutating the row — " +
            "otherwise the row keeps churning and the cleanup background " +
            "job could mistime its eviction window.");
    }

    [Fact]
    public async Task ValidateTempSession_ExpiredToken_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var token = await _service.CreateTempSessionAsync(userId);

        // Default TempSessionLifetimeMinutes is 5; advance 6 minutes.
        _time.Advance(TimeSpan.FromMinutes(6));

        var resolved = await _service.ValidateTempSessionAsync(token);
        resolved.Should().BeNull();
    }
}
