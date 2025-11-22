using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Tests for RevokeSessionCommandHandler focusing on authorization.
/// </summary>
public class RevokeSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RevokeSessionCommandHandler>> _loggerMock;
    private readonly RevokeSessionCommandHandler _handler;

    public RevokeSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RevokeSessionCommandHandler>>();

        _handler = new RevokeSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_OwnerRevokesOwnSession_Succeeds()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = new Session(sessionId, userId, SessionToken.Generate(), TimeSpan.FromDays(30));

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new RevokeSessionCommand(sessionId, userId, false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, default), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminRevokesAnySession_Succeeds()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionOwnerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var session = new Session(sessionId, sessionOwnerId, SessionToken.Generate(), TimeSpan.FromDays(30));

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new RevokeSessionCommand(sessionId, adminUserId, true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, default), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ReturnsUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionOwnerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var session = new Session(sessionId, sessionOwnerId, SessionToken.Generate(), TimeSpan.FromDays(30));

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new RevokeSessionCommand(sessionId, differentUserId, false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage);
        // Session should NOT be updated when unauthorized (early return)
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new RevokeSessionCommand(sessionId, userId, false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Session not found", result.ErrorMessage);
    }
}