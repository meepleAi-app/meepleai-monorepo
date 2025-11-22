using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Tests for ExtendSessionCommandHandler focusing on rate limiting and authorization.
/// </summary>
public class ExtendSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRateLimitService> _rateLimitServiceMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly Mock<ILogger<ExtendSessionCommandHandler>> _loggerMock;
    private readonly ExtendSessionCommandHandler _handler;

    public ExtendSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _rateLimitServiceMock = new Mock<IRateLimitService>();
        _timeProvider = new FakeTimeProvider();
        _loggerMock = new Mock<ILogger<ExtendSessionCommandHandler>>();

        _handler = new ExtendSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _rateLimitServiceMock.Object,
            _timeProvider,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidRequest_ExtendsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = new Session(sessionId, userId, SessionToken.Generate(), TimeSpan.FromDays(30));

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 9, 0));

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ExtendSessionCommand(sessionId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.NewExpiresAt);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, default), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_RateLimitExceeded_ReturnsError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(false, 0, 120));

        var command = new ExtendSessionCommand(sessionId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Rate limit exceeded", result.ErrorMessage);
        _sessionRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 9, 0));

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new ExtendSessionCommand(sessionId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Session not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionOwnerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var session = new Session(sessionId, sessionOwnerId, SessionToken.Generate(), TimeSpan.FromDays(30));

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 9, 0));

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ExtendSessionCommand(sessionId, differentUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage);
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Session>(), default), Times.Never);
    }
}