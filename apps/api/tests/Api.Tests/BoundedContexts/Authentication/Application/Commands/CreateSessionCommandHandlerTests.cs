using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Comprehensive tests for CreateSessionCommandHandler.
/// Tests session creation after OAuth callback or 2FA verification.
/// </summary>
public class CreateSessionCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly TimeProvider _timeProvider;
    private readonly CreateSessionCommandHandler _handler;

    public CreateSessionCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProvider = TimeProvider.System;

        _handler = new CreateSessionCommandHandler(
            _userRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider
        );
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidUserId_CreatesSession()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.User);
        Assert.NotNull(result.SessionToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("user@example.com", result.User.Email);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesSessionWithMetadata()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: "192.168.1.100",
            UserAgent: "Mozilla/5.0"
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Session? capturedSession = null;
        _sessionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((session, _) => capturedSession = session);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedSession);
        Assert.Equal(user.Id, capturedSession.UserId);
    }

    [Fact]
    public async Task Handle_WithNullIpAddress_CreatesSession()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: null,
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SessionToken);
    }

    [Fact]
    public async Task Handle_WithNullUserAgent_CreatesSession()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: "127.0.0.1",
            UserAgent: null
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region User Not Found Tests

    [Fact]
    public async Task Handle_WithNonExistentUser_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateSessionCommand(
            UserId: userId,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains(userId.ToString(), exception.Message);
        Assert.Contains("not found", exception.Message);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_MapsDtoCorrectly()
    {
        // Arrange
        var user = CreateTestUser("test@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("test@example.com", result.User.Email);
        Assert.Equal(user.DisplayName, result.User.DisplayName);
        Assert.Equal(Role.User.Value, result.User.Role);
        Assert.False(result.User.IsTwoFactorEnabled);
        Assert.Null(result.User.TwoFactorEnabledAt);
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task Handle_CallsSaveChangesOnce()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var command = new CreateSessionCommand(
            UserId: user.Id,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, token))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByIdAsync(user.Id, token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), token), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(token), Times.Once);
    }

    #endregion

    #region Helper Methods

    private User CreateTestUser(string email)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("SecurePassword123!"),
            role: Role.User
        );
    }

    #endregion
}

