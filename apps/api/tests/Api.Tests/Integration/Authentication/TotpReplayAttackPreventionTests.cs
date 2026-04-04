using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Infrastructure;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using OtpNet;
using Polly;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Security penetration tests for TOTP Replay Attack Prevention (Issue #1787).
/// Tests OWASP ASVS 2.8.3 compliance: OTP codes must not be reusable.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
/// <remarks>
/// SECURITY: Issue #1787 - SEC-07 TOTP Replay Attack Prevention
/// OWASP: A07:2021 - Identification and Authentication Failures
/// ASVS: 2.8.3 - Verifiable that OTP values are used only once
///
/// Tests Cover:
/// 1. Replay Attack Prevention: same TOTP code cannot be used twice
/// 2. Nonce validation with database persistence
/// 3. Code expiry cleanup (TTL 2 minutes)
/// 4. Audit logging for replay attack attempts
/// 5. Normal TOTP verification still works
///
/// Pattern: AAA (Arrange-Act-Assert), SharedTestcontainersFixture (Issue #2031)
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Security)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "1787")]
[Trait("Issue", "2031")]
[Trait("OWASP", "ASVS-2.8.3")]
public sealed class TotpReplayAttackPreventionTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private ITotpService? _totpService;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private const string TestUserEmail = "security-test@meepleai.dev";
    private const string TestPassword = "SecurePassword123!";

    public TotpReplayAttackPreventionTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    // Helper to check if tests can run
    private void EnsureTestInfrastructureAvailable()
    {
        if (_totpService == null || _dbContext == null)
        {
            Assert.Skip("Shared Testcontainers fixture not available. TOTP replay attack tests require PostgreSQL.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing TOTP Replay Attack Prevention test infrastructure...");

        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        try
        {
            // Create isolated database for this test class
            _databaseName = $"test_totp_replay_{Guid.NewGuid():N}";
            _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

            _output($"Isolated database created: {_databaseName}");

            var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

            // Register dependencies for TotpService
            services.AddScoped<ITotpService, TotpService>();
            services.AddScoped<IPasswordHashingService, PasswordHashingService>();

            // Mock EncryptionService (not critical for replay attack testing)
            var mockEncryptionService = new Mock<IEncryptionService>();
            mockEncryptionService.Setup(x => x.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string plaintext, string purpose) => $"encrypted_{plaintext}");
            mockEncryptionService.Setup(x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string ciphertext, string purpose) => ciphertext.Replace("encrypted_", ""));
            services.AddScoped(_ => mockEncryptionService.Object);

            // Mock RateLimitService (allow all requests for replay testing focus)
            var mockRateLimitService = new Mock<IRateLimitService>();
            mockRateLimitService.Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 5, RetryAfterSeconds: 0)); // Always allow
            services.AddScoped(_ => mockRateLimitService.Object);

            // Mock AlertingService (no-op for replay testing)
            var mockAlertingService = new Mock<IAlertingService>();
            mockAlertingService.Setup(x => x.SendAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AlertDto(
                    Id: Guid.NewGuid(),
                    AlertType: "TEST",
                    Severity: "INFO",
                    Message: "Test",
                    Metadata: null,
                    TriggeredAt: DateTime.UtcNow,
                    ResolvedAt: null,
                    IsActive: true,
                    ChannelSent: null));
            services.AddScoped(_ => mockAlertingService.Object);

            // Mock Redis ConnectionMultiplexer (minimal mock for lockout tracking)
            var mockRedis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
            var mockDatabase = new Mock<StackExchange.Redis.IDatabase>();
            mockDatabase.Setup(x => x.StringIncrementAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<long>(), It.IsAny<StackExchange.Redis.CommandFlags>()))
                .ReturnsAsync(1); // Return count 1 (below lockout threshold)
            mockDatabase.Setup(x => x.KeyExpireAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<StackExchange.Redis.ExpireWhen>(), It.IsAny<StackExchange.Redis.CommandFlags>()))
                .ReturnsAsync(true);
            mockDatabase.Setup(x => x.StringGetAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<StackExchange.Redis.CommandFlags>()))
                .ReturnsAsync(StackExchange.Redis.RedisValue.Null); // No lockout active
            mockDatabase.Setup(x => x.KeyDeleteAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<StackExchange.Redis.CommandFlags>()))
                .ReturnsAsync(true);
            mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            services.AddSingleton(_ => mockRedis.Object);

            // Register real AuditService - will use same DbContext as TotpService
            services.AddScoped<AuditService>();

            // TimeProvider for testing
            services.AddSingleton<TimeProvider>(TimeProvider.System);

            // Logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
            _totpService = _serviceProvider.GetRequiredService<ITotpService>();

            // Debug: Verify connection string
            var actualConnString = _dbContext.Database.GetConnectionString();
            _output($"DEBUG: DbContext connection string: {actualConnString}");
            _output($"DEBUG: Expected connection contains database: {_databaseName}");

            // Apply EF Core migrations with retry policy (Issue #2005: Testcontainers race condition)
            var retryPolicy = Policy
                .Handle<Npgsql.NpgsqlException>()
                .Or<System.IO.EndOfStreamException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _output($"⚠️ Migration attempt {retryCount} failed: {exception.Message}. Retrying in {timeSpan.TotalSeconds}s...");
                    });

            _output($"DEBUG: About to run Database.MigrateAsync()...");
            await retryPolicy.ExecuteAsync(async () =>
                await _dbContext.Database.MigrateAsync(TestCancellationToken));
            _output("✓ Database migrations applied successfully");

            // Debug: Verify tables exist
            var tableCount = await _dbContext.Database.ExecuteSqlRawAsync(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'",
                TestCancellationToken);
            _output($"DEBUG: Tables in database: {tableCount}");

            // Create test user with 2FA enabled
            await SeedTestUserAsync();
        }
        catch (Exception ex)
        {
            _output($"Database initialization failed: {ex.Message}. Tests will be skipped.");
            return; // Skip initialization - tests will be skipped
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        // Issue #2031: Use SharedTestcontainersFixture for cleanup instead of individual container disposal
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
                _output($"Isolated database dropped: {_databaseName}");
            }
            catch (Exception ex)
            {
                _output($"Warning: Failed to drop database {_databaseName}: {ex.Message}");
            }
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task SeedTestUserAsync()
    {
        var passwordHashingService = _serviceProvider!.GetRequiredService<IPasswordHashingService>();
        var passwordHash = passwordHashingService.HashSecret(TestPassword);

        // Generate TOTP secret for test user
        var secret = GenerateTotpSecret();
        var encryptedSecret = $"encrypted_{secret}";

        var testUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = TestUserEmail,
            DisplayName = "Security Test User",
            PasswordHash = passwordHash,
            Role = UserRole.User.ToString(), // Convert enum to string
            IsTwoFactorEnabled = true,
            TotpSecretEncrypted = encryptedSecret,
            TwoFactorEnabledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Users.Add(testUser);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        _output($"Test user created: {TestUserEmail} (UserId: {testUser.Id})");
    }

    private string GenerateTotpSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160 bits
        return Base32Encoding.ToString(key);
    }

    private string GenerateValidTotpCode(string secret)
    {
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes, step: 30);
        return totp.ComputeTotp();
    }

    /// <summary>
    /// TEST 1: ✅ SECURITY - TOTP code works on first use (baseline)
    /// </summary>
    [Fact]
    public async Task VerifyCodeAsync_ValidCode_FirstUse_ShouldSucceed()
    {
        // Ensure Docker is available
        EnsureTestInfrastructureAvailable();

        // Arrange
        var user = await _dbContext!.Users
            .Where(u => u.Email == TestUserEmail)
            .FirstAsync(TestCancellationToken);
        var secret = user.TotpSecretEncrypted!.Replace("encrypted_", "");
        var validCode = GenerateValidTotpCode(secret);

        // Act
        var result = await _totpService!.VerifyCodeAsync(user.Id, validCode);

        // Assert
        result.Should().BeTrue("Valid TOTP code should succeed on first use");

        // Verify code was stored in used_totp_codes table
        var usedCode = await _dbContext.UsedTotpCodes
            .FirstOrDefaultAsync(u => u.UserId == user.Id, TestCancellationToken);
        usedCode.Should().NotBeNull();
        usedCode.UserId.Should().Be(user.Id);
        usedCode.CodeHash.Should().NotBeNull();
        (usedCode.ExpiresAt > DateTime.UtcNow).Should().BeTrue();

        _output("✅ TEST 1 PASSED: Valid TOTP code works on first use");
    }

    /// <summary>
    /// TEST 2: 🔴 SECURITY - TOTP replay attack MUST be blocked
    /// OWASP ASVS 2.8.3: Verifiable that OTP values are used only once
    /// </summary>
    [Fact]
    public async Task VerifyCodeAsync_ReuseValidCode_ShouldFail_ReplayAttackPrevention()
    {
        // Ensure Docker is available
        EnsureTestInfrastructureAvailable();

        // Arrange
        var user = await _dbContext!.Users
            .Where(u => u.Email == TestUserEmail)
            .FirstAsync(TestCancellationToken);
        var secret = user.TotpSecretEncrypted!.Replace("encrypted_", "");
        var validCode = GenerateValidTotpCode(secret);

        // Act - First use: should succeed
        var firstAttempt = await _totpService!.VerifyCodeAsync(user.Id, validCode);

        // Act - Second use: MUST fail (replay attack)
        var secondAttempt = await _totpService.VerifyCodeAsync(user.Id, validCode);

        // Assert
        firstAttempt.Should().BeTrue("First use of valid TOTP code should succeed");
        secondAttempt.Should().BeFalse("🔴 CRITICAL: Replay attack MUST be blocked - code already used");

        // Verify only ONE entry in used_totp_codes (first use)
        var usedCodesCount = await _dbContext.UsedTotpCodes
            .CountAsync(u => u.UserId == user.Id, TestCancellationToken);
        usedCodesCount.Should().Be(1);

        _output("✅ TEST 2 PASSED: Replay attack successfully blocked");
    }

    /// <summary>
    /// TEST 3: ✅ SECURITY - Different TOTP codes work independently
    /// </summary>
    [Fact]
    public async Task VerifyCodeAsync_DifferentCodes_ShouldBothSucceed()
    {
        // Ensure Docker is available
        EnsureTestInfrastructureAvailable();

        // Arrange
        var user = await _dbContext!.Users.FirstAsync(u => u.Email == TestUserEmail);
        var secret = user.TotpSecretEncrypted!.Replace("encrypted_", "");

        // Generate first code
        var firstCode = GenerateValidTotpCode(secret);
        var firstResult = await _totpService!.VerifyCodeAsync(user.Id, firstCode);

        // Wait for time step to change (31 seconds to ensure new code)
        await Task.Delay(AuthenticationTestConstants.TwoFactor.PastTotpWindow, TestCancellationToken);

        // Generate second code (different time step)
        var secondCode = GenerateValidTotpCode(secret);

        // Act
        var secondResult = await _totpService.VerifyCodeAsync(user.Id, secondCode);

        // Assert
        firstResult.Should().BeTrue("First TOTP code should succeed");
        secondResult.Should().BeTrue("Second TOTP code (different time step) should also succeed");
        secondCode.Should().NotBe(firstCode);

        // Verify TWO entries in used_totp_codes
        var usedCodesCount = await _dbContext.UsedTotpCodes
            .CountAsync(u => u.UserId == user.Id, TestCancellationToken);
        usedCodesCount.Should().Be(2);

        _output("✅ TEST 3 PASSED: Different TOTP codes work independently");
    }

    /// <summary>
    /// TEST 4: ✅ SECURITY - Expired codes are NOT checked (performance optimization)
    /// </summary>
    [Fact]
    public async Task VerifyCodeAsync_ExpiredUsedCode_ShouldNotInterfereWithNewCode()
    {
        // Ensure Docker is available
        EnsureTestInfrastructureAvailable();

        // Arrange
        var user = await _dbContext!.Users
            .Where(u => u.Email == TestUserEmail)
            .FirstAsync(TestCancellationToken);
        var passwordHashingService = _serviceProvider!.GetRequiredService<IPasswordHashingService>();

        // Manually insert an expired used code (simulating old attack attempt)
        var expiredCode = new UsedTotpCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = passwordHashingService.HashSecret("123456"),
            TimeStep = 12345,
            UsedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-3) // Expired 3 minutes ago
        };
        _dbContext.UsedTotpCodes.Add(expiredCode);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Generate new valid code
        var secret = user.TotpSecretEncrypted!.Replace("encrypted_", "");
        var validCode = GenerateValidTotpCode(secret);

        // Act
        var result = await _totpService!.VerifyCodeAsync(user.Id, validCode);

        // Assert
        result.Should().BeTrue("New valid code should work even with expired codes in DB");

        _output("✅ TEST 4 PASSED: Expired codes do not interfere");
    }

    /// <summary>
    /// TEST 5: ✅ SECURITY - Invalid TOTP code still fails
    /// </summary>
    [Fact]
    public async Task VerifyCodeAsync_InvalidCode_ShouldFail()
    {
        // Ensure Docker is available
        EnsureTestInfrastructureAvailable();

        // Arrange
        var user = await _dbContext!.Users
            .Where(u => u.Email == TestUserEmail)
            .FirstAsync(TestCancellationToken);
        var invalidCode = "000000"; // Wrong code

        // Act
        var result = await _totpService!.VerifyCodeAsync(user.Id, invalidCode);

        // Assert
        result.Should().BeFalse("Invalid TOTP code should fail");

        // Verify NO entry in used_totp_codes (invalid codes are not stored)
        var usedCodesCount = await _dbContext.UsedTotpCodes
            .CountAsync(u => u.UserId == user.Id, TestCancellationToken);
        usedCodesCount.Should().Be(0);

        _output("✅ TEST 5 PASSED: Invalid TOTP code fails correctly");
    }
}
