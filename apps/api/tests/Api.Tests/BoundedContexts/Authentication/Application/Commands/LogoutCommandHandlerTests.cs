using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Comprehensive tests for LogoutCommandHandler.
/// Tests session revocation, token validation, and error handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LogoutCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new LogoutCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object
        );
    }
    [Fact]
    public async Task Handle_WithValidSession_RevokesSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(session.RevokedAt);
        Assert.True(session.IsRevoked());

        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdatesSessionBeforeSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var callOrder = new List<string>();

        _sessionRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("update"));

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"));

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("update", callOrder[0]);
        Assert.Equal("save", callOrder[1]);
    }
    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsDomainException()
    {
        // Arrange
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();
        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Invalid session token", exception.Message);

        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidTokenFormat_ThrowsValidationException()
    {
        // Arrange
        var command = new LogoutCommand("not-a-valid-base64-token");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyRevokedSession_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        // Revoke the session first
        session.Revoke();

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Session is already revoked", exception.Message);

        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Handle_UsesTokenHashForLookup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var expectedTokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(expectedTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(
            x => x.GetByTokenHashAsync(expectedTokenHash, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
    [Fact]
    public async Task Handle_CallsSaveChangesOnce()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task Handle_WithExpiredSession_StillRevokes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromSeconds(-1), // Already expired
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(session.RevokedAt);
        Assert.True(session.IsRevoked());

        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, token))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _sessionRepositoryMock.Verify(x => x.GetByTokenHashAsync(tokenHash, token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, token), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task Handle_SetsRevokedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var beforeRevoke = DateTime.UtcNow;

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        var afterRevoke = DateTime.UtcNow;

        // Assert
        Assert.NotNull(session.RevokedAt);
        Assert.True(session.RevokedAt >= beforeRevoke);
        Assert.True(session.RevokedAt <= afterRevoke);
    }
}