using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Additional comprehensive tests for LogoutCommandHandler.
/// Tests session revocation and security scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LogoutCommandHandlerAdditionalTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerAdditionalTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new LogoutCommandHandler(_sessionRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidSession_RevokesSessionSuccessfully()
    {
        // Arrange
        var sessionToken = SessionToken.Generate();
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), sessionToken, null, "127.0.0.1", "TestAgent");
        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(session.IsRevoked());
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsDomainException()
    {
        // Arrange - Use valid base64 token that doesn't exist in DB
        var validToken = SessionToken.Generate().Value;
        var command = new LogoutCommand(validToken);
        _sessionRepositoryMock.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Session?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Equal("Invalid session token", exception.Message);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyRevokedSession_ThrowsDomainException()
    {
        // Arrange
        var sessionToken = SessionToken.Generate();
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), sessionToken, null, "127.0.0.1", "TestAgent");
        session.Revoke(); // Already revoked
        var command = new LogoutCommand(sessionToken.Value);

        _sessionRepositoryMock.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        // Act & Assert - Session.Revoke() throws when already revoked
        await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        // Arrange
        var sessionToken = SessionToken.Generate();
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), sessionToken, null, "127.0.0.1", "TestAgent");
        var command = new LogoutCommand(sessionToken.Value);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), token)).ReturnsAsync(session);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _sessionRepositoryMock.Verify(x => x.GetByTokenHashAsync(It.IsAny<string>(), token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), token), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(token), Times.Once);
    }
}
