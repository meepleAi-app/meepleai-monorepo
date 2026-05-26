using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
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
