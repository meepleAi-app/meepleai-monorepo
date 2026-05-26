using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Unit tests for the SP5 S2 D-S2-4 auto-expiry detection in <see cref="ValidateSessionQueryHandler"/>.
/// When an impersonation session's window has elapsed, the query must surface
/// <c>WasImpersonationAutoEnded</c> + the subject/actor ids so the auth middleware can emit a
/// 401 + ImpersonationAutoEnded audit. The query itself stays side-effect-free.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ValidateSessionQueryImpersonationTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ValidateSessionQueryHandler _handler;

    public ValidateSessionQueryImpersonationTests()
    {
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-26T12:00:00Z"));

        _handler = new ValidateSessionQueryHandler(
            _mockSessionRepository.Object,
            _mockUserRepository.Object,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_ExpiredImpersonationSession_FlagsAutoEndedWithSubjectAndActor()
    {
        // Arrange — an impersonation session whose window elapsed 1 minute ago.
        var subjectId = Guid.NewGuid();   // the impersonated target (session.UserId)
        var actorId = Guid.NewGuid();     // the admin who started the impersonation
        var token = SessionToken.Generate();
        var pastUntil = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1);

        // lifetime negative → ExpiresAt in the past → session is invalid (auto-expired).
        var expiredSession = new Session(
            id: Guid.NewGuid(),
            userId: subjectId,
            token: token,
            lifetime: TimeSpan.FromMinutes(-1),
            impersonatedByUserId: actorId,
            impersonatedUntil: pastUntil,
            timeProvider: _timeProvider);

        _mockSessionRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSession);

        // Act
        var result = await _handler.Handle(new ValidateSessionQuery(token.Value), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse("the impersonation window has elapsed");
        result.WasImpersonationAutoEnded.Should().BeTrue(
            "an expired IMPERSONATION session must be distinguished from a plain invalid session");
        result.ImpersonationSubjectUserId.Should().Be(subjectId,
            "user_id of the ImpersonationAutoEnded audit = the impersonated subject");
        result.ImpersonationActorUserId.Should().Be(actorId,
            "impersonated_user_id of the audit = the admin actor");
    }

    [Fact]
    public async Task Handle_ActiveImpersonationSession_PopulatesPrincipalActor()
    {
        // Arrange — a VALID impersonation session (lifetime > 0, impersonatedUntil > now).
        // This exercises the REAL production pipeline that AuditLoggingBehavior.ExtractImpersonationActorId
        // depends on; the T8 acceptance scenarios construct SessionStatusDto manually and bypass
        // this handler. Regression guard for PR #1555 code-review issue #1.
        var subjectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var token = SessionToken.Generate();
        var futureUntil = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(10);

        var activeSession = new Session(
            id: Guid.NewGuid(),
            userId: subjectId,
            token: token,
            lifetime: TimeSpan.FromMinutes(10),
            impersonatedByUserId: actorId,
            impersonatedUntil: futureUntil,
            timeProvider: _timeProvider);

        var subjectUser = CreateUser(subjectId, "bob@user.test", "user");
        var actorUser = CreateUser(actorId, "alice@admin.test", "superadmin");

        _mockSessionRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSession);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subjectUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actorUser);

        // Act
        var result = await _handler.Handle(new ValidateSessionQuery(token.Value), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Subject.Id.Should().Be(subjectId, "Subject = the impersonated target");
        result.Principal.Actor.Should().NotBeNull("Principal.Actor must be loaded for active impersonations");
        result.Principal.Actor!.Id.Should().Be(actorId, "Actor = the impersonating admin");
        result.Principal.IsImpersonating.Should().BeTrue();
        result.Principal.EffectiveActor.Id.Should().Be(actorId,
            "EffectiveActor returns Actor when impersonating; restores admin privileges for /admin/impersonation/end");
    }

    [Fact]
    public async Task Handle_ActiveImpersonationSession_ActorMissing_DegradesToActorNull()
    {
        // Arrange — impersonation session where the admin user has since been deleted.
        // Graceful degrade: Principal.Actor null, but the session itself remains valid for the
        // impersonated subject (audit_logs.impersonated_user_id will be null on this row — a
        // documented trade-off vs failing the whole session).
        var subjectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var token = SessionToken.Generate();

        var activeSession = new Session(
            id: Guid.NewGuid(),
            userId: subjectId,
            token: token,
            lifetime: TimeSpan.FromMinutes(10),
            impersonatedByUserId: actorId,
            impersonatedUntil: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(10),
            timeProvider: _timeProvider);

        _mockSessionRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSession);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(subjectId, "bob@user.test", "user"));
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new ValidateSessionQuery(token.Value), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue("subject still exists; the missing admin is not session-fatal");
        result.Principal!.Actor.Should().BeNull("admin no longer exists — degrade to actor-null");
        result.Principal.EffectiveActor.Id.Should().Be(subjectId, "EffectiveActor falls back to Subject when Actor is null");
    }

    [Fact]
    public async Task Handle_RegularSession_PrincipalActorIsNull()
    {
        // Arrange — a plain login session (no impersonation marker).
        var userId = Guid.NewGuid();
        var token = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: token,
            lifetime: TimeSpan.FromMinutes(15),
            timeProvider: _timeProvider);
        var user = CreateUser(userId, "user@test.test", "user");

        _mockSessionRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new ValidateSessionQuery(token.Value), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Principal!.Actor.Should().BeNull("regular sessions are mono-principal");
        result.Principal.IsImpersonating.Should().BeFalse();
        result.Principal.EffectiveActor.Id.Should().Be(userId, "EffectiveActor = Subject when no Actor");
    }

    private static User CreateUser(Guid id, string email, string role)
    {
        var userRole = role.ToLowerInvariant() switch
        {
            "admin" => Role.Admin,
            "superadmin" => Role.SuperAdmin,
            _ => Role.User
        };
        return new User(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("UnusualPwd123!"),
            role: userRole);
    }

    [Fact]
    public async Task Handle_ExpiredRegularSession_DoesNotFlagAutoEnded()
    {
        // Arrange — a plain (non-impersonation) expired session.
        var token = SessionToken.Generate();
        var expiredSession = new Session(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            token: token,
            lifetime: TimeSpan.FromMinutes(-1),
            timeProvider: _timeProvider);

        _mockSessionRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSession);

        // Act
        var result = await _handler.Handle(new ValidateSessionQuery(token.Value), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.WasImpersonationAutoEnded.Should().BeFalse(
            "a regular expired session must NOT trigger the impersonation-auto-ended path");
        result.ImpersonationSubjectUserId.Should().BeNull();
        result.ImpersonationActorUserId.Should().BeNull();
    }
}
