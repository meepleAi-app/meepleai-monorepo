using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers.OAuth;

/// <summary>
/// Tests for HandleOAuthCallbackCommandHandler.
/// Validates OAuth callback processing including CSRF validation, token exchange, and session creation.
/// </summary>
public class HandleOAuthCallbackCommandHandlerTests
{
    private readonly Mock<IOAuthService> _oauthServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOAuthAccountRepository> _oauthAccountRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<HandleOAuthCallbackCommandHandler>> _loggerMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly HandleOAuthCallbackCommandHandler _handler;

    public HandleOAuthCallbackCommandHandlerTests()
    {
        _oauthServiceMock = new Mock<IOAuthService>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _oauthAccountRepositoryMock = new Mock<IOAuthAccountRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<HandleOAuthCallbackCommandHandler>>();
        _encryptionServiceMock = new Mock<IEncryptionService>();

        _handler = new HandleOAuthCallbackCommandHandler(
            _oauthServiceMock.Object,
            _userRepositoryMock.Object,
            _oauthAccountRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object,
            _encryptionServiceMock.Object);
    }

    #region Success Cases - New User

    [Fact]
    public async Task Handle_NewUser_CreatesUserLinksOAuthAndCreatesSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var callbackResult = CreateCallbackResult(userId, isNewUser: true);
        var sessionResponse = CreateSessionResponse(userId);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(userId, result.UserId);
        Assert.True(result.IsNewUser);
        Assert.NotNull(result.SessionToken);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingUserByEmail_LinksOAuthAndCreatesSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("discord");
        var callbackResult = CreateCallbackResult(userId, isNewUser: false);
        var sessionResponse = CreateSessionResponse(userId);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(userId, result.UserId);
        Assert.False(result.IsNewUser);
        Assert.NotNull(result.SessionToken);
    }

    [Fact]
    public async Task Handle_ExistingOAuthAccount_UpdatesTokensAndCreatesSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("github");
        var callbackResult = CreateCallbackResult(userId, isNewUser: false);
        var sessionResponse = CreateSessionResponse(userId);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.SessionToken);
        Assert.Equal(sessionResponse.SessionToken, result.SessionToken);
    }

    [Fact]
    public async Task Handle_ValidCallback_ReturnsSessionTokenInResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var callbackResult = CreateCallbackResult(userId, isNewUser: true);
        var sessionToken = "test_session_token_12345";
        var sessionResponse = new CreateSessionResponse(
            User: CreateUserDto(userId),
            SessionToken: sessionToken,
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(sessionToken, result.SessionToken);
    }

    [Fact]
    public async Task Handle_ValidCallback_SetsIsNewUserFlagCorrectly()
    {
        // Arrange - New user scenario
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var callbackResult = CreateCallbackResult(userId, isNewUser: true);
        var sessionResponse = CreateSessionResponse(userId);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsNewUser);
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsCreateSessionCommandViaMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google", "192.168.1.1", "Mozilla/5.0");
        var callbackResult = CreateCallbackResult(userId, isNewUser: true);
        var sessionResponse = CreateSessionResponse(userId);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateSessionCommand>(c =>
                    c.UserId == userId &&
                    c.IpAddress == "192.168.1.1" &&
                    c.UserAgent == "Mozilla/5.0"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCallback_LogsAllMajorSteps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var callbackResult = CreateCallbackResult(userId, isNewUser: true);
        var sessionResponse = CreateSessionResponse(userId);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OAuth callback successful")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Handle_InvalidState_ReturnsErrorResult()
    {
        // Arrange
        var command = CreateTestCommand("google");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(false); // Invalid CSRF state

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid state", result.ErrorMessage);
        Assert.Contains("CSRF", result.ErrorMessage);
        _oauthServiceMock.Verify(
            s => s.HandleCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TokenExchangeFails_ReturnsErrorResult()
    {
        // Arrange
        var command = CreateTestCommand("google");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ThrowsAsync(new UnauthorizedAccessException("Token exchange failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Token exchange failed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UserInfoRetrievalFails_ReturnsErrorResult()
    {
        // Arrange
        var command = CreateTestCommand("google");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ThrowsAsync(new InvalidOperationException("Failed to retrieve user info"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Failed to retrieve user info", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_SessionCreationFails_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var callbackResult = CreateCallbackResult(userId, isNewUser: true);

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ReturnsAsync(callbackResult);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Session creation failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_InvalidState_LogsWarning()
    {
        // Arrange
        var command = CreateTestCommand("google");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid OAuth state")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TokenExchangeFails_LogsErrorWithException()
    {
        // Arrange
        var command = CreateTestCommand("google");
        var exception = new UnauthorizedAccessException("Token exchange failed");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.HandleCallbackAsync(command.Provider, command.Code, command.State))
            .ThrowsAsync(exception);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OAuth callback failed")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static HandleOAuthCallbackCommand CreateTestCommand(
        string provider,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new HandleOAuthCallbackCommand
        {
            Provider = provider,
            Code = "auth_code_12345",
            State = "csrf_state_token",
            IpAddress = ipAddress ?? "127.0.0.1",
            UserAgent = userAgent ?? "TestAgent/1.0"
        };
    }

    private static OAuthCallbackResult CreateCallbackResult(Guid userId, bool isNewUser)
    {
        return new OAuthCallbackResult(
            User: new AuthUser(
                Id: userId.ToString(),
                Email: "test@example.com",
                DisplayName: "Test User",
                Role: "User"
            ),
            IsNewUser: isNewUser
        );
    }

    private static CreateSessionResponse CreateSessionResponse(Guid userId)
    {
        return new CreateSessionResponse(
            User: CreateUserDto(userId),
            SessionToken: "session_token_12345",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );
    }

    private static Api.BoundedContexts.Authentication.Application.DTOs.UserDto CreateUserDto(Guid userId)
    {
        return new Api.BoundedContexts.Authentication.Application.DTOs.UserDto(
            Id: userId,
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null
        );
    }

    #endregion
}
