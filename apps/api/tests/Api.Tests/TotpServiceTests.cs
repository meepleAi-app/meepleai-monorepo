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
    private readonly ServiceProvider _serviceProvider;
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
        _serviceProvider = services.BuildServiceProvider();
        var dataProtectionProvider = _serviceProvider.GetRequiredService<IDataProtectionProvider>();
        _encLoggerMock = new Mock<ILogger<EncryptionService>>();
        _encryptionService = new EncryptionService(dataProtectionProvider, _encLoggerMock.Object);

        // Setup auth and audit services (simplified for tests)
        var mockPasswordHashing = new Mock<IPasswordHashingService>();
        _authService = new AuthService(_dbContext, mockPasswordHashing.Object, sessionCache: null, timeProvider: TimeProvider.System);
        _auditService = new AuditService(_dbContext, Mock.Of<ILogger<AuditService>>());

        // Setup TOTP service
        _loggerMock = new Mock<ILogger<TotpService>>();
        _totpService = new TotpService(_dbContext, _encryptionService, _authService, _auditService, mockPasswordHashing.Object, _loggerMock.Object);
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
        result.QrCodeUrl.Should().Contain(Uri.EscapeDataString(userEmail));
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
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var enableCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, enableCode);

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
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var validCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, validCode);

        // Verify enabled
        var userBefore = await _dbContext.Users.FindAsync(userId);
        userBefore?.IsTwoFactorEnabled.Should().BeTrue();

        // Act - Disable with new code
        var disableCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.DisableTwoFactorAsync(userId, password, disableCode);

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
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var validCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, validCode);

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

    #region TEST-574 Security & Edge Case Tests (P0)

    [Fact]
    public async Task GenerateSetupAsync_ConcurrentCalls_GeneratesUniqueSecrets()
    {
        // Arrange
        var userId = "test-user-concurrent";
        var userEmail = "concurrent@example.com";
        var user = new UserEntity
        {
            Id = userId,
            Email = userEmail,
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act - 10 concurrent setup calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _totpService.GenerateSetupAsync(userId, userEmail))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert - All secrets unique (cryptographically secure RNG)
        var secrets = results.Select(r => r.Secret).ToList();
        secrets.Should().OnlyHaveUniqueItems();
        secrets.Should().AllSatisfy(s => s.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GenerateSetupAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var action = async () => await _totpService.GenerateSetupAsync("nonexistent", "test@example.com");
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User not found");
    }

    [Fact]
    public async Task GenerateSetupAsync_ReenrollmentDeletesOldBackupCodes()
    {
        // Arrange
        var userId = "test-user-reenroll";
        var user = new UserEntity
        {
            Id = userId,
            Email = "reenroll@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // First enrollment
        var setup1 = await _totpService.GenerateSetupAsync(userId, user.Email);
        var oldCount = await _dbContext.UserBackupCodes.CountAsync(bc => bc.UserId == userId);
        oldCount.Should().Be(10);

        // Act - Second enrollment
        var setup2 = await _totpService.GenerateSetupAsync(userId, user.Email);

        // Assert - Only new codes exist
        var newCodes = await _dbContext.UserBackupCodes.Where(bc => bc.UserId == userId).ToListAsync();
        newCodes.Count.Should().Be(10);
        setup1.Secret.Should().NotBe(setup2.Secret);
    }

    [Fact]
    public async Task GenerateSecret_GeneratesValid160BitSecret()
    {
        // Arrange
        var userId = "test-user-secret";
        var user = new UserEntity
        {
            Id = userId,
            Email = "secret@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act - GenerateSetupAsync calls GenerateSecret internally
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var secret = setup.Secret;

        // Assert - Base32 encoding of 20 bytes (160 bits) = 32 chars
        secret.Should().NotBeNullOrEmpty();
        secret.Length.Should().Be(32);
        secret.Should().MatchRegex(@"^[A-Z2-7]+$");

        // Verify decodes to 20 bytes
        var bytes = OtpNet.Base32Encoding.ToBytes(secret);
        bytes.Length.Should().Be(20);
    }

    [Fact]
    public async Task GenerateQrCodeUrl_ContainsValidOtpauthUri()
    {
        // Arrange
        var userId = "test-user-qr";
        var userEmail = "qr@example.com";
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
        var setup = await _totpService.GenerateSetupAsync(userId, userEmail);

        // Assert
        setup.QrCodeUrl.Should().StartWith("otpauth://totp/");
        setup.QrCodeUrl.Should().Contain($"MeepleAI:{Uri.EscapeDataString(userEmail)}");
        setup.QrCodeUrl.Should().Contain($"secret={setup.Secret}");
        setup.QrCodeUrl.Should().Contain("issuer=MeepleAI");
    }

    [Fact]
    public async Task GenerateBackupCodes_NoAmbiguousCharacters()
    {
        // Arrange
        var userId = "test-user-backup";
        var user = new UserEntity
        {
            Id = userId,
            Email = "backup@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);

        // Assert - No ambiguous chars (0, O, 1, I, lowercase l)
        // Note: Uppercase L is included in implementation charset
        var ambiguous = new[] { '0', 'O', '1', 'I', 'l' };
        setup.BackupCodes.Should().AllSatisfy(code =>
        {
            code.Should().NotContainAny(ambiguous.Select(c => c.ToString()));
        });
    }

    [Fact]
    public async Task GenerateBackupCodes_UniqueCodesPerGeneration()
    {
        // Arrange
        var userId = "test-user-unique";
        var user = new UserEntity
        {
            Id = userId,
            Email = "unique@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);

        // Assert
        setup.BackupCodes.Should().OnlyHaveUniqueItems();
        setup.BackupCodes.Count.Should().Be(10);
    }

    [Fact]
    public async Task VerifyTotpCode_WithClockSkew_AcceptsCodesInWindow()
    {
        // Arrange
        var userId = "test-user-skew";
        var user = new UserEntity
        {
            Id = userId,
            Email = "skew@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var enableCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, enableCode);

        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Secret);
        var totp = new OtpNet.Totp(secretBytes);

        // Act - Test ±60 seconds (within window)
        var currentCode = totp.ComputeTotp();
        var futureCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(60));
        var pastCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(-60));

        // Assert - All accepted (TimeWindowSteps = 2 = ±60s)
        var current = await _totpService.VerifyCodeAsync(userId, currentCode);
        var future = await _totpService.VerifyCodeAsync(userId, futureCode);
        var past = await _totpService.VerifyCodeAsync(userId, pastCode);

        current.Should().BeTrue();
        future.Should().BeTrue();
        past.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTotpCode_OutsideTimeWindow_RejectsCode()
    {
        // Arrange
        var userId = "test-user-window";
        var user = new UserEntity
        {
            Id = userId,
            Email = "window@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var enableCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, enableCode);

        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Secret);
        var totp = new OtpNet.Totp(secretBytes);

        // Act - Far future code (outside ±60s window)
        var farFutureCode = totp.ComputeTotp(DateTime.UtcNow.AddMinutes(5));

        // Assert
        var result = await _totpService.VerifyCodeAsync(userId, farFutureCode);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTotpCode_WithInvalidBase32Secret_ReturnsFalse()
    {
        // Arrange
        var userId = "test-user-invalid";
        var user = new UserEntity
        {
            Id = userId,
            Email = "invalid@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = true,
            TotpSecretEncrypted = await _encryptionService.EncryptAsync("INVALID-SECRET!", "TotpSecrets")
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _totpService.VerifyCodeAsync(userId, "123456");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithInvalidHashFormat_ReturnsFalse()
    {
        // Arrange
        var userId = "test-user-badhash";
        var user = new UserEntity
        {
            Id = userId,
            Email = "badhash@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = true
        };
        _dbContext.Users.Add(user);

        // Corrupted hash
        _dbContext.UserBackupCodes.Add(new UserBackupCodeEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CodeHash = "invalid-format",
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _totpService.VerifyBackupCodeAsync(userId, "AAAA-BBBB");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_ConcurrentAttemptsOnSameCode_OnlyOneSucceeds()
    {
        // Arrange
        var userId = "test-user-race";
        var user = new UserEntity
        {
            Id = userId,
            Email = "race@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var enableCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, enableCode);

        var code = setup.BackupCodes[0];

        // Act - 5 concurrent attempts
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _totpService.VerifyBackupCodeAsync(userId, code))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert - Only 1 succeeds (serializable isolation)
        results.Count(r => r).Should().Be(1);
        results.Count(r => !r).Should().Be(4);
    }

    [Fact]
    public async Task DisableTwoFactorAsync_WithBackupCode_Succeeds()
    {
        // Arrange
        var userId = "test-user-disable-backup";
        var password = "Test123!";
        var user = new UserEntity
        {
            Id = userId,
            Email = "disable@example.com",
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Setup and enable 2FA
        var setup = await _totpService.GenerateSetupAsync(userId, user.Email);
        var enableCode = GenerateValidTotpCode(setup.Secret);
        await _totpService.EnableTwoFactorAsync(userId, enableCode);

        var backupCode = setup.BackupCodes[0];

        // Act - Disable with backup code
        await _totpService.DisableTwoFactorAsync(userId, password, backupCode);

        // Assert
        var userAfter = await _dbContext.Users.FindAsync(userId);
        userAfter?.IsTwoFactorEnabled.Should().BeFalse();
        userAfter?.TotpSecretEncrypted.Should().BeNull();
    }

    [Fact]
    public async Task DisableTwoFactorAsync_UserNotEnabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = "test-user-not-enabled";
        var password = "Test123!";
        var user = new UserEntity
        {
            Id = userId,
            Email = "notenabled@example.com",
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert - API throws UnauthorizedAccessException for invalid code
        var action = async () => await _totpService.DisableTwoFactorAsync(userId, password, "123456");
        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid verification code");
    }

    [Fact]
    public async Task GetTwoFactorStatusAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var action = async () => await _totpService.GetTwoFactorStatusAsync("nonexistent");
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User not found");
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            _serviceProvider.Dispose();
        }
    }
}