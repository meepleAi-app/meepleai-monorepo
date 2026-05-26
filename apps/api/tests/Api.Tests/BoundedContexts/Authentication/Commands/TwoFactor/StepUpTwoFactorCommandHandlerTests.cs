using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.TwoFactor;

/// <summary>
/// Unit tests for <see cref="StepUpTwoFactorCommandHandler"/> (SP5 Admin Security S3 — T5).
/// Covers the handler-level slices of acceptance scenarios S3-4 (step-up success / invalid code) and
/// S3-5 (lockout → 429). The full HTTP request→pipeline acceptance (real audit_outbox row + re-POST
/// of the previously-blocked sensitive command) runs in the T8 integration suite.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class StepUpTwoFactorCommandHandlerTests
{
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<AuditService> _audit;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-05-26T12:00:00Z"));
    private readonly StepUpTwoFactorCommandHandler _handler;

    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private const string ValidCode = "123456";

    public StepUpTwoFactorCommandHandlerTests()
    {
        // AuditService has virtual methods — mock with a satisfied base ctor (InMemory ctx, unused
        // because LogAsync is overridden by the mock and never hits the real DbContext body).
        _audit = new Mock<AuditService>(
            new MeepleAiDbContext(
                new DbContextOptionsBuilder<MeepleAiDbContext>()
                    .UseInMemoryDatabase($"stepup_audit_{Guid.NewGuid()}").Options,
                Mock.Of<IMediator>(),
                Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>()),
            Mock.Of<ILogger<AuditService>>(),
            null!);

        // LogAsync returns a Task; without a setup Moq yields a null Task → NRE on await.
        _audit
            .Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new StepUpTwoFactorCommandHandler(
            _totp.Object,
            _sessions.Object,
            _audit.Object,
            _time,
            Mock.Of<ILogger<StepUpTwoFactorCommandHandler>>());
    }

    private StepUpTwoFactorCommand Command(string code = ValidCode) => new(SessionId, ActorId, code);

    [Fact]
    public async Task Handle_ValidCode_RefreshesRecencyAndAuditsSuccess()
    {
        // Arrange — S3-4 happy path.
        _totp.Setup(t => t.IsLockedOutAsync(ActorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _totp.Setup(t => t.VerifyCodeAsync(ActorId, ValidCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sessions
            .Setup(r => r.UpdateLastTotpVerifiedAtAsync(SessionId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(StepUpOutcome.Success);
        result.LastTotpVerifiedAt.Should().Be(_time.GetUtcNow().UtcDateTime);
        result.RetryAfterSeconds.Should().BeNull();

        _sessions.Verify(r => r.UpdateLastTotpVerifiedAtAsync(
            SessionId, _time.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.LogAsync(
            ActorId.ToString(), "TwoFactorStepUp", "TwoFactor", SessionId.ToString(), "Success",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsInvalidCode_WithoutRefreshing()
    {
        // Arrange — S3-4 negative: VerifyCodeAsync already audited the failed attempt + advanced lockout.
        _totp.Setup(t => t.IsLockedOutAsync(ActorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _totp.Setup(t => t.VerifyCodeAsync(ActorId, ValidCode, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(StepUpOutcome.InvalidCode);
        result.LastTotpVerifiedAt.Should().BeNull();

        _sessions.Verify(r => r.UpdateLastTotpVerifiedAtAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.LogAsync(
            It.IsAny<string>(), "TwoFactorStepUp", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_LockedOut_Returns429Semantics_WithoutVerifying()
    {
        // Arrange — S3-5: lockout pre-check short-circuits before any TOTP verification.
        _totp.Setup(t => t.IsLockedOutAsync(ActorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(StepUpOutcome.LockedOut);
        result.RetryAfterSeconds.Should().Be(900);
        result.LastTotpVerifiedAt.Should().BeNull();

        _totp.Verify(t => t.VerifyCodeAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(r => r.UpdateLastTotpVerifiedAtAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.LogAsync(
            ActorId.ToString(), "TwoFactorStepUpLockout", "TwoFactor", SessionId.ToString(), "Blocked",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SessionVanishedAfterVerify_ReturnsInvalidCode_WithoutSuccessAudit()
    {
        // Arrange — verified, but the session was revoked/expired between auth and step-up
        // (ExecuteUpdate affected 0 rows). Treated as a failed step-up.
        _totp.Setup(t => t.IsLockedOutAsync(ActorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _totp.Setup(t => t.VerifyCodeAsync(ActorId, ValidCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sessions
            .Setup(r => r.UpdateLastTotpVerifiedAtAsync(SessionId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(StepUpOutcome.InvalidCode);
        result.LastTotpVerifiedAt.Should().BeNull();
        _audit.Verify(a => a.LogAsync(
            It.IsAny<string>(), "TwoFactorStepUp", It.IsAny<string>(), It.IsAny<string>(), "Success",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act = async () => await _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
