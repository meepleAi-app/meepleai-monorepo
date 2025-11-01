using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// BDD-style unit tests for PasswordResetService (TEST-02, AUTH-05).
///
/// Feature: AUTH-05 - Password Reset Service
/// As a user who has forgotten their password
/// I want to request a password reset via email
/// So that I can securely regain access to my account
///
/// Security Requirements:
/// - No user enumeration (always return success even if email doesn't exist)
/// - Rate limiting (3 requests per hour)
/// - Token expiration (30 minutes)
/// - One-time use tokens
/// - Session revocation on password reset
/// - Strong password requirements
/// </summary>
public class PasswordResetServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private const string ValidEmail = "user@example.com";
    private const string ValidPassword = "Password123";
    private static readonly DateTime FixedNow = DateTime.Parse("2024-01-15T12:00:00Z");

    public PasswordResetServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    #region RequestPasswordResetAsync Tests

    /// <summary>
    /// Scenario: Request password reset with valid email
    ///   Given a user exists with the email
    ///   When RequestPasswordResetAsync is called
    ///   Then a reset token is created in the database
    ///   And the token expires in 30 minutes
    ///   And an email is sent to the user
    ///   And the method returns true
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithValidEmail_CreatesTokenAndSendsEmail()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.RequestPasswordResetAsync(ValidEmail);

        // Assert
        result.Should().BeTrue();

        var token = await db.PasswordResetTokens.FirstOrDefaultAsync();
        token.Should().NotBeNull();
        token.UserId.Should().Be("user1");
        token.IsUsed.Should().BeFalse();
        token.CreatedAt.Should().Be(FixedNow);
        token.ExpiresAt.Should().Be(FixedNow.AddMinutes(30));

        mockEmail.Verify(x => x.SendPasswordResetEmailAsync(
            ValidEmail,
            "Test User",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Scenario: Request password reset with non-existent email
    ///   Given no user exists with the email
    ///   When RequestPasswordResetAsync is called
    ///   Then no token is created
    ///   And no email is sent
    ///   And the method returns true (to prevent email enumeration)
    ///   And an information log is written
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithNonExistentEmail_ReturnsTrue()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.RequestPasswordResetAsync("nonexistent@example.com");

        // Assert
        result.Should().BeTrue();

        var tokenCount = await db.PasswordResetTokens.CountAsync();
        tokenCount.Should().Be(0);

        mockEmail.Verify(x => x.SendPasswordResetEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify information log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Password reset requested for non-existent email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Request password reset with null email
    ///   Given a null email
    ///   When RequestPasswordResetAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithNullEmail_ThrowsArgumentException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.RequestPasswordResetAsync(null!);
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Email is required");
    }

    /// <summary>
    /// Scenario: Request password reset with empty email
    ///   Given an empty string email
    ///   When RequestPasswordResetAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithEmptyEmail_ThrowsArgumentException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.RequestPasswordResetAsync(string.Empty);
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Email is required");
    }

    /// <summary>
    /// Scenario: Request password reset when rate limit is exceeded
    ///   Given a user has exceeded rate limit (3 requests per hour)
    ///   When RequestPasswordResetAsync is called
    ///   Then an InvalidOperationException is thrown
    ///   And the error message indicates too many requests
    ///   And a warning is logged
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithRateLimitExceeded_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: false, retryAfter: 3600);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.RequestPasswordResetAsync(ValidEmail);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Too many password reset requests");

        // Verify warning log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Password reset rate limit exceeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Request password reset with existing unused token
    ///   Given a user has an existing valid token
    ///   When RequestPasswordResetAsync is called
    ///   Then the old token is marked as used
    ///   And a new token is created
    ///   And an email is sent with the new token
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithExistingUnusedToken_InvalidatesOldToken()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var existingToken = new PasswordResetTokenEntity
        {
            Id = "existing-token",
            UserId = "user1",
            TokenHash = "existing-hash",
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-15)
        };
        db.PasswordResetTokens.Add(existingToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.RequestPasswordResetAsync(ValidEmail);

        // Assert
        result.Should().BeTrue();

        var allTokens = await db.PasswordResetTokens.ToListAsync();
        allTokens.Count.Should().Be(2);

        var oldToken = allTokens.First(t => t.Id == "existing-token");
        oldToken.IsUsed.Should().BeTrue();

        var newToken = allTokens.First(t => t.Id != "existing-token");
        newToken.IsUsed.Should().BeFalse();

        mockEmail.Verify(x => x.SendPasswordResetEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Scenario: Request password reset when email service fails
    ///   Given the email service throws an exception
    ///   When RequestPasswordResetAsync is called
    ///   Then the method still returns true (doesn't throw)
    ///   And the token is still created in the database
    ///   And an error is logged
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithEmailServiceFailure_ReturnsTrue()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var mockEmail = new Mock<IEmailService>();
        mockEmail.Setup(x => x.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP server unavailable"));

        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.RequestPasswordResetAsync(ValidEmail);

        // Assert
        result.Should().BeTrue();

        var token = await db.PasswordResetTokens.FirstOrDefaultAsync();
        token.Should().NotBeNull();

        // Verify error was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send password reset email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Request password reset generates URL-safe token
    ///   Given a valid user
    ///   When RequestPasswordResetAsync is called
    ///   Then the token sent to email does not contain +, /, or = characters
    ///   (These are replaced with -, _, and removed respectively for URL safety)
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_GeneratesUrlSafeToken()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        string? capturedToken = null;
        var mockEmail = new Mock<IEmailService>();
        mockEmail.Setup(x => x.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((email, name, token, ct) => capturedToken = token)
            .Returns(Task.CompletedTask);

        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        await service.RequestPasswordResetAsync(ValidEmail);

        // Assert
        capturedToken.Should().NotBeNull();
        capturedToken.Should().NotContain("+");
        capturedToken.Should().NotContain("/");
        capturedToken.Should().NotContain("=");
    }

    /// <summary>
    /// Scenario: Request password reset with email case variation
    ///   Given a user with email "user@example.com"
    ///   When requesting reset with "USER@EXAMPLE.COM"
    ///   Then the token is created for the user (email is normalized to lowercase)
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithEmailCaseVariation_NormalizesEmail()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var user = new UserEntity
        {
            Id = "user1",
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.RequestPasswordResetAsync("USER@EXAMPLE.COM");

        // Assert
        result.Should().BeTrue();

        var token = await db.PasswordResetTokens.FirstOrDefaultAsync();
        token.Should().NotBeNull();
        token.UserId.Should().Be("user1");
    }

    /// <summary>
    /// Scenario: Request password reset with whitespace in email
    ///   Given an email with leading and trailing whitespace
    ///   When RequestPasswordResetAsync is called
    ///   Then the email is trimmed before processing
    /// </summary>
    [Fact]
    public async Task RequestPasswordResetAsync_WithWhitespaceInEmail_TrimsEmail()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var user = new UserEntity
        {
            Id = "user1",
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.RequestPasswordResetAsync("  user@example.com  ");

        // Assert
        result.Should().BeTrue();

        var token = await db.PasswordResetTokens.FirstOrDefaultAsync();
        token.Should().NotBeNull();
        token.UserId.Should().Be("user1");
    }

    #endregion

    #region ValidateResetTokenAsync Tests

    /// <summary>
    /// Scenario: Validate valid reset token
    ///   Given a valid, unused, non-expired token
    ///   When ValidateResetTokenAsync is called
    ///   Then the method returns true
    /// </summary>
    [Fact]
    public async Task ValidateResetTokenAsync_WithValidToken_ReturnsTrue()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "valid-token-123";
        var tokenHash = HashToken(token);

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-15)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ValidateResetTokenAsync(token);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Validate null token
    ///   Given a null token
    ///   When ValidateResetTokenAsync is called
    ///   Then the method returns false (not throws)
    /// </summary>
    [Fact]
    public async Task ValidateResetTokenAsync_WithNullToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.ValidateResetTokenAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: Validate empty token
    ///   Given an empty string token
    ///   When ValidateResetTokenAsync is called
    ///   Then the method returns false
    /// </summary>
    [Fact]
    public async Task ValidateResetTokenAsync_WithEmptyToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.ValidateResetTokenAsync(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: Validate invalid token (not in database)
    ///   Given a token that doesn't exist in the database
    ///   When ValidateResetTokenAsync is called
    ///   Then the method returns false
    /// </summary>
    [Fact]
    public async Task ValidateResetTokenAsync_WithInvalidToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.ValidateResetTokenAsync("invalid-token-that-does-not-exist");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: Validate used token
    ///   Given a token that has already been used
    ///   When ValidateResetTokenAsync is called
    ///   Then the method returns false
    ///   And a warning is logged
    /// </summary>
    [Fact]
    public async Task ValidateResetTokenAsync_WithUsedToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "used-token-123";
        var tokenHash = HashToken(token);

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = true,
            CreatedAt = FixedNow.AddMinutes(-15),
            UsedAt = FixedNow.AddMinutes(-5)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ValidateResetTokenAsync(token);

        // Assert
        result.Should().BeFalse();

        // Verify warning log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempt to reuse password reset token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Validate expired token
    ///   Given a token that has passed its expiration time
    ///   When ValidateResetTokenAsync is called
    ///   Then the method returns false
    ///   And an information log is written
    /// </summary>
    [Fact]
    public async Task ValidateResetTokenAsync_WithExpiredToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "expired-token-123";
        var tokenHash = HashToken(token);

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "existing-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(-5), // Expired 5 minutes ago
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-35)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ValidateResetTokenAsync(token);

        // Assert
        result.Should().BeFalse();

        // Verify information log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Expired password reset token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region ResetPasswordAsync Tests

    /// <summary>
    /// Scenario: Reset password with valid token and password
    ///   Given a valid token and strong password
    ///   When ResetPasswordAsync is called
    ///   Then the method returns (true, userId)
    ///   And the user's password hash is updated
    ///   And the token is marked as used
    ///   And UsedAt timestamp is set
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithValidTokenAndPassword_ResetsPassword()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "valid-token-123";
        var tokenHash = HashToken(token);
        var originalPasswordHash = "original-hash";

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = originalPasswordHash,
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-15)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ResetPasswordAsync(token, "NewPassword123");

        // Assert
        result.Success.Should().BeTrue();
        result.UserId.Should().BeEquivalentTo("user1");

        var updatedUser = await db.Users.FindAsync("user1");
        updatedUser!.PasswordHash.Should().NotBe(originalPasswordHash);
        updatedUser.PasswordHash.Should().StartWith("v1.210000."); // PBKDF2 format

        var updatedToken = await db.PasswordResetTokens.FindAsync("token1");
        updatedToken!.IsUsed.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Reset password revokes all active sessions
    ///   Given a user with multiple active sessions
    ///   When ResetPasswordAsync is called successfully
    ///   Then all active sessions are revoked
    ///   And RevokedAt timestamp is set to current time
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithValidReset_RevokesAllSessions()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "valid-token-123";
        var tokenHash = HashToken(token);

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "original-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var session1 = new UserSessionEntity
        {
            Id = "session1",
            UserId = "user1",
            TokenHash = "session-hash-1",
            CreatedAt = FixedNow.AddDays(-5),
            ExpiresAt = FixedNow.AddDays(85),
            IpAddress = "127.0.0.1",
            UserAgent = "test-agent",
            User = user
        };
        var session2 = new UserSessionEntity
        {
            Id = "session2",
            UserId = "user1",
            TokenHash = "session-hash-2",
            CreatedAt = FixedNow.AddDays(-3),
            ExpiresAt = FixedNow.AddDays(87),
            IpAddress = "127.0.0.1",
            UserAgent = "test-agent",
            User = user
        };
        db.UserSessions.AddRange(session1, session2);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-15)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ResetPasswordAsync(token, "NewPassword123");

        // Assert
        result.Success.Should().BeTrue();

        var sessions = await db.UserSessions.Where(s => s.UserId == "user1").ToListAsync();
        sessions.Should().OnlyContain(s => s.RevokedAt != null);
        sessions.Should().OnlyContain(s => s.RevokedAt!.Value == FixedNow);
    }

    /// <summary>
    /// Scenario: Reset password with null token
    ///   Given a null token
    ///   When ResetPasswordAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithNullToken_ThrowsArgumentException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.ResetPasswordAsync(null!, ValidPassword);
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Token is required");
    }

    /// <summary>
    /// Scenario: Reset password with empty token
    ///   Given an empty string token
    ///   When ResetPasswordAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithEmptyToken_ThrowsArgumentException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.ResetPasswordAsync(string.Empty, ValidPassword);
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Token is required");
    }

    /// <summary>
    /// Scenario: Reset password with null password
    ///   Given a null password
    ///   When ResetPasswordAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithNullPassword_ThrowsArgumentException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.ResetPasswordAsync("some-token", null!);
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Password must be at least 8 characters");
    }

    /// <summary>
    /// Scenario: Reset password with short password (less than 8 characters)
    ///   Given a password with only 7 characters
    ///   When ResetPasswordAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithShortPassword_ThrowsArgumentException()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.ResetPasswordAsync("some-token", "Pass12"); // 6 chars
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Password must be at least 8 characters");
    }

    /// <summary>
    /// Scenario: Reset password with weak passwords
    ///   Given passwords that don't meet complexity requirements
    ///   When ResetPasswordAsync is called
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Theory]
    [InlineData("12345678")] // No letters
    [InlineData("abcdefgh")] // No uppercase or numbers
    [InlineData("ABCDEFGH")] // No lowercase or numbers
    [InlineData("Abcdefgh")] // No numbers
    public async Task ResetPasswordAsync_WithWeakPassword_ThrowsArgumentException(string weakPassword)
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await service.ResetPasswordAsync("some-token", weakPassword);
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Password must contain at least one uppercase letter, one lowercase letter, and one number");
    }

    /// <summary>
    /// Scenario: Reset password with strong passwords
    ///   Given passwords that meet all complexity requirements
    ///   When ResetPasswordAsync is called with an invalid token
    ///   Then validation passes (failure is due to invalid token, not password)
    /// </summary>
    [Theory]
    [InlineData("Password1")]
    [InlineData("MyP@ssw0rd!")]
    [InlineData("C0mplexPwd")]
    [InlineData("Tr0ng!Pass")]
    public async Task ResetPasswordAsync_WithStrongPassword_PassesValidation(string strongPassword)
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.ResetPasswordAsync("invalid-token", strongPassword);

        // Assert - Should fail because token is invalid, not because password is weak
        result.Success.Should().BeFalse();
        result.UserId.Should().BeNull();
    }

    /// <summary>
    /// Scenario: Reset password with invalid token
    ///   Given a token that doesn't exist in the database
    ///   When ResetPasswordAsync is called
    ///   Then the method returns (false, null)
    ///   And a warning is logged
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithInvalidToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object);

        // Act
        var result = await service.ResetPasswordAsync("invalid-token-xyz", ValidPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.UserId.Should().BeNull();

        // Verify warning log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid password reset token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Reset password with used token
    ///   Given a token that has already been used
    ///   When ResetPasswordAsync is called
    ///   Then the method returns (false, null)
    ///   And the password is not changed
    ///   And a warning is logged
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithUsedToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "used-token-123";
        var tokenHash = HashToken(token);
        var originalPasswordHash = "original-hash";

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = originalPasswordHash,
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = true,
            CreatedAt = FixedNow.AddMinutes(-15),
            UsedAt = FixedNow.AddMinutes(-5)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ResetPasswordAsync(token, "NewPassword123");

        // Assert
        result.Success.Should().BeFalse();
        result.UserId.Should().BeNull();

        var updatedUser = await db.Users.FindAsync("user1");
        updatedUser!.PasswordHash.Should().Be(originalPasswordHash); // Password unchanged

        // Verify warning log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempt to reuse password reset token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Reset password with expired token
    ///   Given a token that has expired
    ///   When ResetPasswordAsync is called
    ///   Then the method returns (false, null)
    ///   And the password is not changed
    ///   And an information log is written
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_ReturnsFalse()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "expired-token-123";
        var tokenHash = HashToken(token);
        var originalPasswordHash = "original-hash";

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = originalPasswordHash,
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(-5), // Expired 5 minutes ago
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-35)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ResetPasswordAsync(token, "NewPassword123");

        // Assert
        result.Success.Should().BeFalse();
        result.UserId.Should().BeNull();

        var updatedUser = await db.Users.FindAsync("user1");
        updatedUser!.PasswordHash.Should().Be(originalPasswordHash); // Password unchanged

        // Verify information log was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Expired password reset token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: Reset password with unicode characters
    ///   Given a password with unicode characters that meets complexity requirements
    ///   When ResetPasswordAsync is called
    ///   Then the password is successfully reset
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithUnicodePassword_Succeeds()
    {
        // Arrange
        await using var db = await CreateInMemoryContextAsync();

        var token = "valid-token-123";
        var tokenHash = HashToken(token);

        var user = new UserEntity
        {
            Id = "user1",
            Email = ValidEmail,
            DisplayName = "Test User",
            PasswordHash = "original-hash",
            Role = UserRole.User,
            CreatedAt = FixedNow
        };
        db.Users.Add(user);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = "token1",
            UserId = "user1",
            TokenHash = tokenHash,
            ExpiresAt = FixedNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = FixedNow.AddMinutes(-15)
        };
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var mockEmail = CreateEmailServiceMock();
        var mockRateLimit = CreateRateLimitMock(allowed: true);
        var mockLogger = new Mock<ILogger<PasswordResetService>>();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service = new PasswordResetService(db, mockEmail.Object, mockRateLimit.Object, mockLogger.Object, timeProvider);

        // Act
        var result = await service.ResetPasswordAsync(token, "Pássw0rd"); // Unicode characters

        // Assert
        result.Success.Should().BeTrue();
        result.UserId.Should().BeEquivalentTo("user1");

        var updatedUser = await db.Users.FindAsync("user1");
        updatedUser!.PasswordHash.Should().NotBe("original-hash");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an in-memory SQLite database context for testing.
    /// </summary>
    private async Task<MeepleAiDbContext> CreateInMemoryContextAsync()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    /// <summary>
    /// Creates a mock rate limit service with configurable behavior.
    /// </summary>
    private static Mock<IRateLimitService> CreateRateLimitMock(bool allowed, int retryAfter = 0)
    {
        var mock = new Mock<IRateLimitService>();
        mock.Setup(x => x.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(allowed, allowed ? 2 : 0, retryAfter));
        return mock;
    }

    /// <summary>
    /// Creates a mock email service that succeeds by default.
    /// </summary>
    private static Mock<IEmailService> CreateEmailServiceMock()
    {
        var mock = new Mock<IEmailService>();
        mock.Setup(x => x.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// Hashes a token using SHA256 (replicates PasswordResetService.HashToken).
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Fixed time provider for deterministic time-based testing.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime now)
        {
            // Ensure the DateTime is treated as UTC
            var utcNow = DateTime.SpecifyKind(now, DateTimeKind.Utc);
            _now = new DateTimeOffset(utcNow);
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    #endregion
}
