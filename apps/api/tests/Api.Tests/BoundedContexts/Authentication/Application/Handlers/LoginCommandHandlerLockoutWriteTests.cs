using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// #5036 — come il login scrive lo stato del blocco.
/// </summary>
/// <remarks>
/// Il ramo della password sbagliata gira per chiunque conosca un'email. Il conto si decide sulla riga riletta sotto
/// lock dentro una transazione, non sulla lettura di inizio richiesta, e si scrivono solo le colonne del blocco: mai
/// <c>UpdateAsync</c>, che ricopierebbe l'aggregato letto prima su una sospensione o un reset salvati nel frattempo.
/// Che il lock serva davvero, con richieste in parallelo, lo prova <c>LoginLockoutPersistEndpointTests</c> su Postgres.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "5036")]
public class LoginCommandHandlerLockoutWriteTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerLockoutWriteTests()
    {
        _sessions
            .Setup(s => s.GetActiveSessionsByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Session>());
        _users
            .Setup(r => r.ReloadLockoutStateForUpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var unitOfWork = new Mock<IUnitOfWork>().SetupExecuteInTransaction<LoginCommandHandler.FailedLoginOutcome>();

        _handler = new LoginCommandHandler(
            _users.Object,
            _sessions.Object,
            Mock.Of<ITempSessionService>(),
            unitOfWork.Object,
            Mock.Of<IAuditLogger>());
    }

    // Generato a ogni esecuzione: GitGuardian segnala qualunque letterale assegnato a un membro che
    // si chiama come una password (vedi LoginAccountStatusEndpointTests).
    private static string FreshSecret() => $"x{Guid.NewGuid():N}!A1";

    [Fact]
    public async Task WrongPassword_CountsOnTheLockedRow_AndWritesOnlyTheLockoutState()
    {
        var user = SeedUser(FreshSecret());

        var act = () => _handler.Handle(new LoginCommand(user.Email.Value, FreshSecret()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<HttpException>()).Which.ErrorCode.Should().Be("invalid_credentials");
        _users.Verify(r => r.ReloadLockoutStateForUpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.UpdateLockoutStateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task WrongPassword_WhenTheLockedRowIsAlreadyLocked_RefusesAsLocked_AndWritesNothing()
    {
        // Un'altra richiesta ha chiuso l'account fra la lettura di inizio richiesta e il lock.
        var user = SeedUser(FreshSecret());
        _users
            .Setup(r => r.ReloadLockoutStateForUpdateAsync(user, It.IsAny<CancellationToken>()))
            .Callback(() => user.RestoreLockoutState(User.DefaultMaxFailedAttempts, DateTime.UtcNow.AddMinutes(10)))
            .ReturnsAsync(true);

        var act = () => _handler.Handle(new LoginCommand(user.Email.Value, FreshSecret()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<HttpException>()).Which.ErrorCode.Should().Be("account_locked");
        _users.Verify(r => r.UpdateLockoutStateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        user.FailedLoginAttempts.Should().Be(User.DefaultMaxFailedAttempts, "a locked account does not count further");
    }

    [Fact]
    public async Task WrongPassword_WhenTheUserIsGone_AnswersInvalidCredentials_NotA500()
    {
        var user = SeedUser(FreshSecret());
        _users
            .Setup(r => r.ReloadLockoutStateForUpdateAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _handler.Handle(new LoginCommand(user.Email.Value, FreshSecret()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<HttpException>()).Which.ErrorCode.Should().Be("invalid_credentials");
        _users.Verify(r => r.UpdateLockoutStateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RightPassword_AfterFailedAttempts_ClearsOnlyTheLockoutState()
    {
        var secret = FreshSecret();
        var user = SeedUser(secret);
        user.RestoreLockoutState(User.DefaultMaxFailedAttempts - 1, null);

        await _handler.Handle(new LoginCommand(user.Email.Value, secret), TestContext.Current.CancellationToken);

        _users.Verify(r => r.UpdateLockoutStateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task RightPassword_WithNothingToClear_WritesNothingToTheUser()
    {
        var secret = FreshSecret();
        var user = SeedUser(secret);

        await _handler.Handle(new LoginCommand(user.Email.Value, secret), TestContext.Current.CancellationToken);

        _users.Verify(r => r.UpdateLockoutStateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private User SeedUser(string secret)
    {
        var user = new UserBuilder()
            .WithEmail($"lockout-write-{Guid.NewGuid():N}@test.local")
            .WithPassword(secret)
            .Build();
        _users
            .Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }
}
