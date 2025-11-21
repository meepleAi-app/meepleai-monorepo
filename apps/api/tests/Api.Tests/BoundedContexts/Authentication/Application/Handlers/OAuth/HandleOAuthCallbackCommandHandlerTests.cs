using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers.OAuth;

/// <summary>
/// Tests for HandleOAuthCallbackCommandHandler (CQRS refactored version).
/// Validates OAuth callback processing with full business logic in handler.
/// Uses real InMemoryDatabase for DbContext operations.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
public class HandleOAuthCallbackCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        return new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
    }

    /// <summary>
    /// Creates fresh mocks for each test
    /// </summary>
    private static (Mock<IOAuthService>, Mock<IUserRepository>, Mock<IOAuthAccountRepository>, Mock<IUnitOfWork>, Mock<IMediator>, Mock<ILogger<HandleOAuthCallbackCommandHandler>>, Mock<IEncryptionService>, Mock<TimeProvider>) CreateMocks()
    {
        var oauthServiceMock = new Mock<IOAuthService>();
        var userRepositoryMock = new Mock<IUserRepository>();
        var oauthAccountRepositoryMock = new Mock<IOAuthAccountRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<HandleOAuthCallbackCommandHandler>>();
        var encryptionServiceMock = new Mock<IEncryptionService>();
        var timeProviderMock = new Mock<TimeProvider>();

        // Setup TimeProvider default
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        return (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock);
    }

    #region Success Cases - New User

    [Fact]
    public async Task Handle_NewUser_CreatesUserLinksOAuthAndCreatesSession()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.UserId);
        Assert.True(result.IsNewUser);
        Assert.NotNull(result.SessionToken);

        // Verify user was created in database
        var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email.ToLowerInvariant());
        Assert.NotNull(createdUser);
        Assert.Equal(userInfo.Email.ToLowerInvariant(), createdUser.Email);

        // Verify OAuth account was created
        var createdAccount = await context.OAuthAccounts.FirstOrDefaultAsync(oa => oa.UserId == createdUser.Id);
        Assert.NotNull(createdAccount);
        Assert.Equal(command.Provider.ToLowerInvariant(), createdAccount.Provider);
    }

    [Fact]
    public async Task Handle_ExistingUserByEmail_LinksOAuthAndCreatesSession()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Existing User",
            PasswordHash = "existing_hash",
            Role = UserRole.User.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var command = CreateTestCommand("discord");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("provider_123", existingUser.Email, "Provider Name");
        var sessionResponse = CreateSessionResponse(existingUser.Id);

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(existingUser.Id, result.UserId);
        Assert.False(result.IsNewUser); // User already existed

        // Verify OAuth account was linked
        var linkedAccount = await context.OAuthAccounts.FirstOrDefaultAsync(oa => oa.UserId == existingUser.Id);
        Assert.NotNull(linkedAccount);
        Assert.Equal(command.Provider.ToLowerInvariant(), linkedAccount.Provider);
    }

    [Fact]
    public async Task Handle_ExistingOAuthAccount_UpdatesTokensAndCreatesSession()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(existingUser);

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
        context.OAuthAccounts.Add(existingAccount);
        await context.SaveChangesAsync();

        var command = CreateTestCommand("github");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("github_user_123", existingUser.Email, "GitHub User");
        var sessionResponse = CreateSessionResponse(existingUser.Id);

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("new_encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(existingUser.Id, result.UserId);
        Assert.False(result.IsNewUser);

        // Verify token was updated
        var updatedAccount = await context.OAuthAccounts.FirstAsync(oa => oa.Id == existingAccount.Id);
        Assert.Equal("new_encrypted_token", updatedAccount.AccessTokenEncrypted);
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsExchangeCodeForTokenAsync()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        oauthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsGetUserInfoAsync()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        oauthServiceMock.Verify(
            s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCallback_CallsCreateSessionCommandViaMediator()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google", "192.168.1.1", "Mozilla/5.0");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        var sessionResponse = CreateSessionResponse(Guid.NewGuid());
        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mediatorMock.Verify(
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
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();
        var sessionResponse = CreateSessionResponse(userId);

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        loggerMock.Verify(
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
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google");

        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(false); // Invalid CSRF state

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid state", result.ErrorMessage);
        Assert.Contains("CSRF", result.ErrorMessage);

        // Should not proceed to token exchange
        oauthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TokenExchangeFails_ReturnsErrorResult()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google");

        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ThrowsAsync(new InvalidOperationException("Token exchange failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("OAuth token exchange failed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UserInfoRetrievalFails_ReturnsErrorResult()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();

        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ThrowsAsync(new InvalidOperationException("Failed to retrieve user info"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to get user information", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_NoEmailFromProvider_ReturnsErrorResult()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("provider_user_123", string.Empty, "Test User"); // No email

        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ReturnsAsync(userInfo);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("email", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_SessionCreationFails_ReturnsErrorResult()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var userId = Guid.NewGuid();
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = CreateUserInfo();

        SetupSuccessfulOAuthFlow(oauthServiceMock, command, tokenResponse, userInfo);

        encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Session creation failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_InvalidState_LogsWarning()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google");

        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        loggerMock.Verify(
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
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (oauthServiceMock, userRepositoryMock, oauthAccountRepositoryMock, unitOfWorkMock, mediatorMock, loggerMock, encryptionServiceMock, timeProviderMock) = CreateMocks();

        var handler = new HandleOAuthCallbackCommandHandler(
            oauthServiceMock.Object,
            userRepositoryMock.Object,
            oauthAccountRepositoryMock.Object,
            unitOfWorkMock.Object,
            mediatorMock.Object,
            loggerMock.Object,
            encryptionServiceMock.Object,
            timeProviderMock.Object,
            context);

        var command = CreateTestCommand("google");
        var exception = new InvalidOperationException("Token exchange failed");

        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ThrowsAsync(exception);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to exchange OAuth code")),
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
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null
        );
    }

    private static void SetupSuccessfulOAuthFlow(
        Mock<IOAuthService> oauthServiceMock,
        HandleOAuthCallbackCommand command,
        OAuthTokenResponse tokenResponse,
        OAuthUserInfo userInfo)
    {
        oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ReturnsAsync(userInfo);
    }

    #endregion
}
