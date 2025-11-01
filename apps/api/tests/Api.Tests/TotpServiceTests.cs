using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// AUTH-07: Unit tests for TotpService
/// Tests TOTP generation, verification, backup codes, and security features
/// </summary>
public class TotpServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly MeepleAiDbContext _dbContext;
    private readonly TotpService _totpService;
    private readonly Mock<ILogger<TotpService>> _loggerMock;
    private readonly Mock<ILogger<EncryptionService>> _encLoggerMock;
    private readonly IEncryptionService _encryptionService;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;

    public TotpServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup SQLite in-memory database (consistent with other tests)
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite($"DataSource=:memory:")
            .Options;
        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        // Setup encryption service with DataProtection
        var services = new ServiceCollection();
        services.AddDataProtection();
        var serviceProvider = services.BuildServiceProvider();
        var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
        _encLoggerMock = new Mock<ILogger<EncryptionService>>();
        _encryptionService = new EncryptionService(dataProtectionProvider, _encLoggerMock.Object);

        // Setup auth and audit services (simplified for tests)
        _authService = new AuthService(_dbContext, sessionCache: null, timeProvider: TimeProvider.System);
        _auditService = new AuditService(_dbContext, Mock.Of<ILogger<AuditService>>());

        // Setup TOTP service
        _loggerMock = new Mock<ILogger<TotpService>>();
        _totpService = new TotpService(_dbContext, _encryptionService, _authService, _auditService, _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateSetupAsync_ShouldGenerateSecretAndBackupCodes()
    {
        // Arrange
        var userId = "test-user-1";
        var userEmail = "test@example.com";
        var user = new UserEntity
        {
            Id = userId,
            Email = userEmail,
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _totpService.GenerateSetupAsync(userId, userEmail);

        // Assert
        result.Should().NotBeNull();
        result.Secret.Should().NotBeEmpty();
        result.QrCodeUrl.Should().NotBeEmpty();
        result.BackupCodes.Count.Should().Be(10);

        // Verify QR code URL format
        result.QrCodeUrl.Should().StartWith("otpauth://totp/MeepleAI:");
        result.QrCodeUrl.Should().Contain(userEmail);
        result.QrCodeUrl.Should().Contain("secret=");

        // Verify backup codes format (XXXX-XXXX)
        foreach (var code in result.BackupCodes)
        {
            code.Should().MatchRegex(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$");
        }

        // Verify secret is stored encrypted
        var userAfter = await _dbContext.Users.FindAsync(userId);
        userAfter?.TotpSecretEncrypted.Should().NotBeNull();
        userAfter!.TotpSecretEncrypted.Should().NotBe(result.Secret); // Should be encrypted

        // Verify backup codes are stored hashed
        var storedCodes = await _dbContext.UserBackupCodes.Where(bc => bc.UserId == userId).ToListAsync();
        storedCodes.Count.Should().Be(10);
        storedCodes.Should().OnlyContain(bc => !string.IsNullOrEmpty(bc.CodeHash));
    }

    [Fact]
    public async Task EnableTwoFactorAsync_WithValidCode_ShouldEnable()
    {
        // Arrange
        var userId = "test-user-2";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test2@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);

        // Generate valid TOTP code using the secret
        var validCode = GenerateValidTotpCode(setup.Secret);

        // Act
        var result = await _totpService.EnableTwoFactorAsync(userId, validCode);

        // Assert
        result.Should().BeTrue();
        var userAfter = await _dbContext.Users.FindAsync(userId);
        userAfter?.IsTwoFactorEnabled.Should().BeTrue();
        userAfter?.TwoFactorEnabledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EnableTwoFactorAsync_WithInvalidCode_ShouldFail()
    {
        // Arrange
        var userId = "test-user-3";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test3@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _totpService.GenerateSetupAsync(userId, user.Email);

        // Act
        var result = await _totpService.EnableTwoFactorAsync(userId, "000000"); // Invalid code

        // Assert
        result.Should().BeFalse();
        var userAfter = await _dbContext.Users.FindAsync(userId);
        userAfter?.IsTwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_ShouldMarkAsUsed()
    {
        // Arrange
        var userId = "test-user-4";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test4@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var backupCode = setup.BackupCodes[0];

        // Act - First use should succeed
        var result1 = await _totpService.VerifyBackupCodeAsync(userId, backupCode);
        result1.Should().BeTrue();

        // Act - Second use should fail (single-use enforcement)
        var result2 = await _totpService.VerifyBackupCodeAsync(userId, backupCode);
        result2.Should().BeFalse();

        // Verify code is marked as used
        var storedCodes = await _dbContext.UserBackupCodes
            .Where(bc => bc.UserId == userId)
            .ToListAsync();
        var usedCode = storedCodes.FirstOrDefault(bc => bc.IsUsed);
        usedCode.Should().NotBeNull();
        usedCode.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DisableTwoFactorAsync_ShouldClearAllData()
    {
        // Arrange
        var userId = "test-user-5";
        var password = "Test123!";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test5@example.com",
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var validCode = GenerateValidTotpCode(setup.Secret);

        // Act
        await _totpService.DisableTwoFactorAsync(userId, password, validCode);

        // Assert
        var userAfter = await _dbContext.Users.FindAsync(userId);
        userAfter?.IsTwoFactorEnabled.Should().BeFalse();
        userAfter?.TotpSecretEncrypted.Should().BeNull();
        userAfter?.TwoFactorEnabledAt.Should().BeNull();

        // Verify all backup codes deleted
        var backupCodes = await _dbContext.UserBackupCodes.Where(bc => bc.UserId == userId).ToListAsync();
        backupCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTwoFactorStatusAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var userId = "test-user-6";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test6@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = true,
            TwoFactorEnabledAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);

        // Use 2 backup codes
        await _totpService.VerifyBackupCodeAsync(userId, setup.BackupCodes[0]);
        await _totpService.VerifyBackupCodeAsync(userId, setup.BackupCodes[1]);

        // Act
        var status = await _totpService.GetTwoFactorStatusAsync(userId);

        // Assert
        status.IsEnabled.Should().BeTrue();
        status.EnabledAt.Should().NotBeNull();
        status.UnusedBackupCodesCount.Should().Be(8); // 10 - 2 used
    }

    // Helper: Generate valid TOTP code
    private string GenerateValidTotpCode(string secret)
    {
        var secretBytes = OtpNet.Base32Encoding.ToBytes(secret);
        var totp = new OtpNet.Totp(secretBytes);
        return totp.ComputeTotp();
    }

    // Helper: Hash password (duplicate from AuthService)
    private string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
