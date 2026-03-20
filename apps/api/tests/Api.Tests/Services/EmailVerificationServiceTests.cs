using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for EmailVerificationService.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class EmailVerificationServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IRateLimitService> _mockRateLimitService;
    private readonly Mock<ILogger<EmailVerificationService>> _mockLogger;
    private readonly FakeTimeProvider _timeProvider;

    public EmailVerificationServiceTests()
    {
        _mockEmailService = new Mock<IEmailService>();
        _mockRateLimitService = new Mock<IRateLimitService>();
        _mockLogger = new Mock<ILogger<EmailVerificationService>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 28, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WithValidUser_SendsEmail()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        _mockEmailService
            .Setup(x => x.SendVerificationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(dbContext);

        // Act
        var result = await service.SendVerificationEmailAsync(
            userId,
            "test@example.com",
            "Test User",
            TestCancellationToken);

        // Assert
        result.Should().BeTrue();
        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            "test@example.com",
            "Test User",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify token was created in database
        var token = await dbContext.EmailVerifications.FirstOrDefaultAsync(TestCancellationToken);
        token.Should().NotBeNull();
        token.UserId.Should().Be(userId);
        token.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WithAlreadyVerifiedUser_ReturnsTrue()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var service = CreateService(dbContext);

        // Act
        var result = await service.SendVerificationEmailAsync(
            userId,
            "test@example.com",
            "Test User",
            TestCancellationToken);

        // Assert
        result.Should().BeTrue();
        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WithNonExistentUser_ReturnsFalse()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateService(dbContext);

        // Act
        var result = await service.SendVerificationEmailAsync(
            Guid.NewGuid(),
            "test@example.com",
            "Test User",
            TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_VerifiesEmail()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        dbContext.Users.Add(user);

        // Create a verification token manually
        var token = "test-verification-token-12345";
        var tokenHash = Api.Helpers.CryptographyHelper.ComputeSha256HashBase64(token);
        var verification = new EmailVerificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.EmailVerifications.Add(verification);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var service = CreateService(dbContext);

        // Act
        var result = await service.VerifyEmailAsync(token, TestCancellationToken);

        // Assert
        result.Should().BeTrue();

        // Verify user is now verified
        var updatedUser = await dbContext.Users.FindAsync([userId], TestCancellationToken);
        updatedUser.Should().NotBeNull();
        updatedUser.EmailVerified.Should().BeTrue();
        updatedUser.EmailVerifiedAt.Should().NotBeNull();

        // Verify token is marked as used
        var updatedVerification = await dbContext.EmailVerifications.FindAsync([verification.Id], TestCancellationToken);
        updatedVerification.Should().NotBeNull();
        updatedVerification.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithExpiredToken_ReturnsFalse()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        dbContext.Users.Add(user);

        var token = "test-verification-token-expired";
        var tokenHash = Api.Helpers.CryptographyHelper.ComputeSha256HashBase64(token);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var verification = new EmailVerificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = now.AddHours(-1), // Expired relative to fake time
            CreatedAt = now.AddHours(-25)
        };
        dbContext.EmailVerifications.Add(verification);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var service = CreateService(dbContext);

        // Act
        var result = await service.VerifyEmailAsync(token, TestCancellationToken);

        // Assert
        result.Should().BeFalse();

        // Verify user is still not verified
        var updatedUser = await dbContext.Users.FindAsync([userId], TestCancellationToken);
        updatedUser.Should().NotBeNull();
        updatedUser.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithAlreadyUsedToken_ReturnsFalse()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow.AddHours(-1)
        };
        dbContext.Users.Add(user);

        var token = "test-verification-token-used";
        var tokenHash = Api.Helpers.CryptographyHelper.ComputeSha256HashBase64(token);
        var verification = new EmailVerificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            VerifiedAt = DateTime.UtcNow.AddHours(-1), // Already used
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        dbContext.EmailVerifications.Add(verification);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var service = CreateService(dbContext);

        // Act
        var result = await service.VerifyEmailAsync(token, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithInvalidToken_ReturnsFalse()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateService(dbContext);

        // Act
        var result = await service.VerifyEmailAsync("invalid-token-does-not-exist", TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_WithUnverifiedUser_SendsEmail()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 1, RetryAfterSeconds: 0));

        _mockEmailService
            .Setup(x => x.SendVerificationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(dbContext);

        // Act
        var result = await service.ResendVerificationEmailAsync("test@example.com", TestCancellationToken);

        // Assert
        result.Should().BeTrue();
        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            "test@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_WhenRateLimited_ThrowsException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: false, TokensRemaining: 0, RetryAfterSeconds: 60));

        var service = CreateService(dbContext);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResendVerificationEmailAsync("test@example.com", TestCancellationToken));

        exception.Message.Should().Contain("Too many");
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_WithNonExistentEmail_ReturnsTrueForSecurity()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 1, RetryAfterSeconds: 0));

        var service = CreateService(dbContext);

        // Act
        var result = await service.ResendVerificationEmailAsync("nonexistent@example.com", TestCancellationToken);

        // Assert - should return true to prevent email enumeration
        result.Should().BeTrue();
        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsEmailVerifiedAsync_WithVerifiedUser_ReturnsTrue()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var service = CreateService(dbContext);

        // Act
        var result = await service.IsEmailVerifiedAsync(userId, TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailVerifiedAsync_WithUnverifiedUser_ReturnsFalse()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var service = CreateService(dbContext);

        // Act
        var result = await service.IsEmailVerifiedAsync(userId, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    private MeepleAiDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    private EmailVerificationService CreateService(MeepleAiDbContext dbContext)
    {
        return new EmailVerificationService(
            dbContext,
            _mockEmailService.Object,
            _mockRateLimitService.Object,
            _mockLogger.Object,
            _timeProvider);
    }
}
