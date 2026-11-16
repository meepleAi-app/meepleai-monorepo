using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// #5036 — il login scrive solo lo stato del blocco, mai l'aggregato intero.
/// </summary>
/// <remarks>
/// Il ramo della password sbagliata gira per chiunque conosca un'email. <c>UpdateAsync</c> ricopierebbe su ogni colonna
/// l'aggregato letto all'inizio della richiesta, e riporterebbe indietro una sospensione, un reset della password o un
/// cambio di ruolo salvati nel frattempo. Che <c>UpdateLockoutStateAsync</c> scriva davvero solo il blocco lo prova
/// <c>LoginLockoutPersistEndpointTests</c> su Postgres; qui si prova che il gestore usa quello.
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

        _handler = new LoginCommandHandler(
            _users.Object,
            _sessions.Object,
            Mock.Of<ITempSessionService>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IAuditLogger>());
    }

    // Generato a ogni esecuzione: GitGuardian segnala qualunque letterale assegnato a un membro che
    // si chiama come una password (vedi LoginAccountStatusEndpointTests).
    private static string FreshSecret() => $"x{Guid.NewGuid():N}!A1";

    [Fact]
    public async Task WrongPassword_WritesOnlyTheLockoutState()
    {
        var user = SeedUser(FreshSecret());

        var act = () => _handler.Handle(new LoginCommand(user.Email.Value, FreshSecret()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpException>();
        _users.Verify(r => r.UpdateLockoutStateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RightPassword_WritesOnlyTheLockoutState()
    {
        var secret = FreshSecret();
        var user = SeedUser(secret);

        await _handler.Handle(new LoginCommand(user.Email.Value, secret), TestContext.Current.CancellationToken);

        _users.Verify(r => r.UpdateLockoutStateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
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
