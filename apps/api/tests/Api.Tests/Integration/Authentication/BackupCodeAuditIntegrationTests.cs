using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// #5164 — <c>TotpService</c> wrote the audit of a backup-code attempt inside the Serializable transaction it opens,
/// through <c>AuditService</c>, which saves on the same context. A failed attempt never commits that transaction, so
/// its audit row was rolled back with it: no failed attempt with a backup code left a trace in <c>audit_logs</c>. The
/// audit of a failed attempt is now written after the transaction, once.
/// </summary>
/// <remarks>On Postgres: under InMemory there is no transaction, and the row would survive for nothing.</remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "5164")]
public sealed class BackupCodeAuditIntegrationTests : IAsyncLifetime
{
    private const string Code = "AUDIT-5164";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public BackupCodeAuditIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_backupaudit_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await TestMigrationHelper.MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
        catch { /* ignore cleanup errors */ }
    }

    [Fact]
    public async Task AWrongBackupCode_LeavesAFailedAuditRow()
    {
        var userId = await SeedTwoFactorUserAsync(codeUsed: false);

        (await VerifyAsync(userId, "WRONG-0000")).Should().BeFalse();

        var rows = await AuditRowsAsync(userId);
        rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Action = "BackupCodeVerify", Result = "Failed", Details = "Failed backup code attempt" });
    }

    [Fact]
    public async Task NoUnusedBackupCodeLeft_LeavesAFailedAuditRow()
    {
        var userId = await SeedTwoFactorUserAsync(codeUsed: true);

        (await VerifyAsync(userId, Code)).Should().BeFalse();

        var rows = await AuditRowsAsync(userId);
        rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Action = "BackupCodeVerify", Result = "Failed", Details = "No unused backup codes available" });
    }

    /// <summary>The success was audited after the commit already; it stays a single row.</summary>
    [Fact]
    public async Task TheRightBackupCode_LeavesOneSuccessAuditRow()
    {
        var userId = await SeedTwoFactorUserAsync(codeUsed: false);

        (await VerifyAsync(userId, Code)).Should().BeTrue();

        var rows = await AuditRowsAsync(userId);
        rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Action = "BackupCodeUsed", Result = "Success" });
    }

    private async Task<bool> VerifyAsync(Guid userId, string code)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var rateLimit = new Mock<IRateLimitService>();
        rateLimit
            .Setup(r => r.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 5, RetryAfterSeconds: 0));
        var redis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(new Mock<StackExchange.Redis.IDatabase>().Object);

        var totp = new TotpService(
            db,
            Mock.Of<IEncryptionService>(),
            new AuditService(db, NullLogger<AuditService>.Instance),
            new PasswordHashingService(),
            rateLimit.Object,
            Mock.Of<IAlertingService>(),
            redis.Object,
            NullLogger<TotpService>.Instance);

        return await totp.VerifyBackupCodeAsync(userId, code, Ct);
    }

    private async Task<Guid> SeedTwoFactorUserAsync(bool codeUsed)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"audit-{Guid.NewGuid():N}@backup-5164.test",
            PasswordHash = "x",
            DisplayName = "Audit probe",
            IsTwoFactorEnabled = true,
        };
        db.Users.Add(user);
        db.UserBackupCodes.Add(new UserBackupCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = new PasswordHashingService().HashSecret(Code),
            IsUsed = codeUsed,
            UsedAt = codeUsed ? DateTime.UtcNow : null,
        });
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<List<AuditLogEntity>> AuditRowsAsync(Guid userId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId && a.Resource == "TwoFactor")
            .ToListAsync(Ct);
    }
}
