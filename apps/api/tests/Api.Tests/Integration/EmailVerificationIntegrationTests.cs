using Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for Email Verification flow end-to-end.
/// ISSUE-3071: Email verification backend implementation.
/// Uses SharedTestcontainersFixture for optimized performance.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3071")]
public class EmailVerificationIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;
    public EmailVerificationIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing Email Verification integration test infrastructure...");

        _databaseName = $"test_email_verification_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _output($"Isolated database created: {_databaseName}");

        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register DbContext with PostgreSQL
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(enforcedBuilder.ConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register mock services
        var mockEmailService = new Mock<IEmailService>();
        mockEmailService
            .Setup(x => x.SendVerificationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRateLimitService = new Mock<IRateLimitService>();
        mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 1, RetryAfterSeconds: 0));

        services.AddSingleton(mockEmailService.Object);
        services.AddSingleton(mockRateLimitService.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();

        // MediatR dependencies
        services.AddSingleton(new Mock<MediatR.IMediator>().Object);

        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        services.AddSingleton(mockEventCollector.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Run migrations
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        _output("Database migrations applied successfully");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task FullVerificationFlow_RegisterToVerify_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = $"flow-test-{Guid.NewGuid():N}@test.meepleai.dev";

        // Create user
        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Flow Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var service = _serviceProvider!.GetRequiredService<IEmailVerificationService>();

        // Act 1: Send verification email
        var sendResult = await service.SendVerificationEmailAsync(userId, email, "Flow Test User", TestCancellationToken);
        Assert.True(sendResult, "Should successfully send verification email");

        // Get the token from database (in real flow, this would be in the email)
        var verification = await _dbContext.EmailVerifications.FirstOrDefaultAsync(v => v.UserId == userId, TestCancellationToken);
        Assert.NotNull(verification);

        // Since we store the hash, we need to create a new test token and store its hash
        var testToken = "test-verification-token-for-flow";
        verification.TokenHash = CryptographyHelper.ComputeSha256HashBase64(testToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act 2: Verify email
        var verifyResult = await service.VerifyEmailAsync(testToken, TestCancellationToken);
        Assert.True(verifyResult, "Should successfully verify email");

        // Assert: User is now verified
        var updatedUser = await _dbContext.Users.FindAsync([userId], TestCancellationToken);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.EmailVerified);
        Assert.NotNull(updatedUser.EmailVerifiedAt);
    }

    [Fact]
    public async Task ResendVerification_CreatesNewToken_InvalidatesOldToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = $"resend-test-{Guid.NewGuid():N}@test.meepleai.dev";

        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Resend Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var service = _serviceProvider!.GetRequiredService<IEmailVerificationService>();

        // Act 1: Send first verification email
        await service.SendVerificationEmailAsync(userId, email, "Resend Test User", TestCancellationToken);

        var firstToken = await _dbContext.EmailVerifications.FirstOrDefaultAsync(v => v.UserId == userId && v.InvalidatedAt == null && v.VerifiedAt == null, TestCancellationToken);
        Assert.NotNull(firstToken);
        var firstTokenId = firstToken.Id;

        // Act 2: Resend verification email
        await service.ResendVerificationEmailAsync(email, TestCancellationToken);

        // Assert: Old token is invalidated (via InvalidatedAt, not VerifiedAt), new token created
        var oldToken = await _dbContext.EmailVerifications.FindAsync([firstTokenId], TestCancellationToken);
        Assert.NotNull(oldToken);
        Assert.NotNull(oldToken.InvalidatedAt); // Old token marked as superseded
        Assert.Null(oldToken.VerifiedAt); // Not marked as verified (different semantic)

        var newTokens = await _dbContext.EmailVerifications.Where(v => v.UserId == userId && v.InvalidatedAt == null && v.VerifiedAt == null).ToListAsync(TestCancellationToken);
        Assert.Single(newTokens); // Should have exactly one active token
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredToken_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = $"expired-test-{Guid.NewGuid():N}@test.meepleai.dev";

        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Expired Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        _dbContext!.Users.Add(user);

        // Create expired token
        var expiredToken = "expired-verification-token";
        var verification = new EmailVerificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = CryptographyHelper.ComputeSha256HashBase64(expiredToken),
            ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
            CreatedAt = DateTime.UtcNow.AddHours(-25)
        };
        _dbContext.EmailVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var service = _serviceProvider!.GetRequiredService<IEmailVerificationService>();

        // Act
        var result = await service.VerifyEmailAsync(expiredToken, TestCancellationToken);

        // Assert
        Assert.False(result, "Should fail with expired token");

        var updatedUser = await _dbContext.Users.FindAsync([userId], TestCancellationToken);
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.EmailVerified, "User should still be unverified");
    }

    [Fact]
    public async Task IsEmailVerified_ReturnsCorrectStatus()
    {
        // Arrange
        var unverifiedUserId = Guid.NewGuid();
        var verifiedUserId = Guid.NewGuid();

        var unverifiedUser = new UserEntity
        {
            Id = unverifiedUserId,
            Email = $"unverified-{Guid.NewGuid():N}@test.meepleai.dev",
            DisplayName = "Unverified User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };

        var verifiedUser = new UserEntity
        {
            Id = verifiedUserId,
            Email = $"verified-{Guid.NewGuid():N}@test.meepleai.dev",
            DisplayName = "Verified User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow.AddDays(-1)
        };

        _dbContext!.Users.AddRange(unverifiedUser, verifiedUser);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var service = _serviceProvider!.GetRequiredService<IEmailVerificationService>();

        // Act & Assert
        var unverifiedResult = await service.IsEmailVerifiedAsync(unverifiedUserId, TestCancellationToken);
        Assert.False(unverifiedResult, "Unverified user should return false");

        var verifiedResult = await service.IsEmailVerifiedAsync(verifiedUserId, TestCancellationToken);
        Assert.True(verifiedResult, "Verified user should return true");
    }

    [Fact]
    public async Task MultipleVerificationAttempts_OnlySingleActiveToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = $"multiple-{Guid.NewGuid():N}@test.meepleai.dev";

        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Multiple Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false
        };
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var service = _serviceProvider!.GetRequiredService<IEmailVerificationService>();

        // Act: Send multiple verification emails
        await service.SendVerificationEmailAsync(userId, email, "Multiple Test User", TestCancellationToken);
        await service.SendVerificationEmailAsync(userId, email, "Multiple Test User", TestCancellationToken);
        await service.SendVerificationEmailAsync(userId, email, "Multiple Test User", TestCancellationToken);

        // Assert: Only one active (unused) token exists
        var activeTokens = await _dbContext.EmailVerifications
            .Where(v => v.UserId == userId && v.InvalidatedAt == null && v.VerifiedAt == null)
            .ToListAsync(TestCancellationToken);

        Assert.Single(activeTokens);

        // All previous tokens should be invalidated (via InvalidatedAt, not VerifiedAt)
        var allTokens = await _dbContext.EmailVerifications
            .Where(v => v.UserId == userId)
            .ToListAsync(TestCancellationToken);

        // Should have 3 tokens total, 2 invalidated, 1 active
        Assert.Equal(3, allTokens.Count);
        Assert.Equal(2, allTokens.Count(t => t.InvalidatedAt != null));
        Assert.Equal(0, allTokens.Count(t => t.VerifiedAt != null)); // None verified yet
    }
}
