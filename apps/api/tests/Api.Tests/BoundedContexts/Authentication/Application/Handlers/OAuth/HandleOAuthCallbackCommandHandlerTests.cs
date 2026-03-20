using System.Threading;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Exceptions;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers.OAuth;

/// <summary>
/// Tests for HandleOAuthCallbackCommandHandler (CQRS refactored version).
/// Validates OAuth callback processing with full business logic in handler.
/// Uses real InMemoryDatabase for DbContext operations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class HandleOAuthCallbackCommandHandlerTests : IDisposable
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IOAuthService> _oauthServiceMock;

    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<HandleOAuthCallbackCommandHandler>> _loggerMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly HandleOAuthCallbackCommandHandler _handler;

    public HandleOAuthCallbackCommandHandlerTests()
    {
        _oauthServiceMock = new Mock<IOAuthService>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<HandleOAuthCallbackCommandHandler>>();
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _timeProviderMock = new Mock<TimeProvider>();

        // Create real InMemoryDatabase for testing DbContext operations
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Setup TimeProvider default
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        _handler = new HandleOAuthCallbackCommandHandler(
            _oauthServiceMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object,
            _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
    [Fact]
    public async Task Handle_NewUser_CreatesUserLinksOAuthAndCreatesSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.UserId.Should().NotBeNull();
        result.IsNewUser.Should().BeTrue();
        result.SessionToken.Should().NotBeNull();

        // Verify user was created in database
        var createdUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email.ToLowerInvariant(), TestCancellationToken);
        createdUser.Should().NotBeNull();
        createdUser.Email.Should().Be(userInfo.Email.ToLowerInvariant());

        // Verify OAuth account was created
        var createdAccount = await _dbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.UserId == createdUser.Id, TestCancellationToken);
        createdAccount.Should().NotBeNull();
        createdAccount.Provider.Should().Be(command.Provider.ToLowerInvariant());
    }

    [Fact]
    public async Task Handle_ExistingUserByEmail_LinksOAuthAndCreatesSession()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Existing User",
            PasswordHash = "existing_hash",
            Role = UserRole.User.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = CreateTestCommand("discord");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("provider_123", existingUser.Email, "Provider Name");
        var sessionResponse = CreateSessionResponse(existingUser.Id);

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.UserId.Should().Be(existingUser.Id);
        result.IsNewUser.Should().BeFalse(); // User already existed

        // Verify OAuth account was linked
        var linkedAccount = await _dbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.UserId == existingUser.Id, TestCancellationToken);
        linkedAccount.Should().NotBeNull();
        linkedAccount.Provider.Should().Be(command.Provider.ToLowerInvariant());
    }

    [Fact]
    public async Task Handle_ExistingOAuthAccount_UpdatesTokensAndCreatesSession()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(existingUser);

        var existingAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            Provider = "github",
            ProviderUserId = "github_user_123",
            AccessTokenEncrypted = "old_encrypted_token",
            RefreshTokenEncrypted = null,
            TokenExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30),
            User = existingUser
        };
        _dbContext.OAuthAccounts.Add(existingAccount);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = CreateTestCommand("github");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("github_user_123", existingUser.Email, "GitHub User");
        var sessionResponse = CreateSessionResponse(existingUser.Id);

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("new_encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.UserId.Should().Be(existingUser.Id);
        result.IsNewUser.Should().BeFalse();

        // Verify token was updated
        var updatedAccount = await _dbContext.OAuthAccounts.FirstAsync(oa => oa.Id == existingAccount.Id, TestCancellationToken);
        updatedAccount.AccessTokenEncrypted.Should().Be("new_encrypted_token");
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsExchangeCodeForTokenAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _oauthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsGetUserInfoAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _oauthServiceMock.Verify(
            s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsCreateSessionCommandViaMediator()
    {
        // Arrange
        var command = CreateTestCommand("google", "192.168.1.1", "Mozilla/5.0");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        var sessionResponse = CreateSessionResponse(Guid.NewGuid());
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateSessionCommand>(c =>
                    c.IpAddress == "192.168.1.1" &&
                    c.UserAgent == "Mozilla/5.0"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCallback_LogsSuccessInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await _handler.Handle(command, TestCancellationToken);

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
    [Fact]
    public async Task Handle_InvalidState_ReturnsErrorResult()
    {
        // Arrange
        var command = CreateTestCommand("google");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(false); // Invalid CSRF state

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid state");
        result.ErrorMessage.Should().Contain("CSRF");

        // Should not proceed to token exchange
        _oauthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
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
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ThrowsAsync(new OAuthTokenExchangeException("google", "Token exchange failed"));

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("OAuth token exchange failed");
    }

    [Fact]
    public async Task Handle_UserInfoRetrievalFails_ReturnsErrorResult()
    {
        // Arrange
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        _oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ThrowsAsync(new OAuthUserInfoException("google", "Failed to retrieve user info"));

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to get user information");
    }

    [Fact]
    public async Task Handle_NoEmailFromProvider_ReturnsErrorResult()
    {
        // Arrange
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("provider_user_123", string.Empty, "Test User"); // No email

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        _oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ReturnsAsync(userInfo);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("email");
    }

    [Fact]
    public async Task Handle_SessionCreationFails_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Session creation failed"));

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
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
        await _handler.Handle(command, TestCancellationToken);

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
        var exception = new OAuthTokenExchangeException("google", "Token exchange failed");

        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ThrowsAsync(exception);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to exchange OAuth code")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
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

    private static OAuthTokenResponse CreateTokenResponse()
    {
        return new OAuthTokenResponse(
            AccessToken: "access_token_12345",
            RefreshToken: "refresh_token_12345",
            ExpiresIn: 3600,
            TokenType: "Bearer"
        );
    }

    private static OAuthUserInfo CreateUserInfo()
    {
        return new OAuthUserInfo(
            Id: "provider_user_123",
            Email: "test@example.com",
            Name: "Test User"
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
            Tier: "normal",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null,
            Level: 1,
            ExperiencePoints: 0
        );
    }

    private void SetupSuccessfulOAuthFlow(
        HandleOAuthCallbackCommand command,
        OAuthTokenResponse tokenResponse,
        OAuthUserInfo userInfo)
    {
        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        _oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ReturnsAsync(userInfo);
    }
}