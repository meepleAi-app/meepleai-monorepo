using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Xunit;

namespace Api.Tests.Integration.Services;

/// <summary>
/// Issue #1628 contract: TotpService writers must use the AsTracking() canonical pattern
/// (Issue #888), not the Update() workaround introduced by PR #1627. Reverting to either
/// (a) FindAsync without any tracking opt-in, or
/// (b) Update() that marks all columns Modified
/// fails these tests.
///
/// Fixture: a NoTracking-aligned WebApplicationFactory (matches prod PERF-06).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Authentication")]
public sealed class TotpServiceTrackingContractTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;

    public TotpServiceTrackingContractTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #1628 follow-up: use SharedTestcontainersFixture instead of a dedicated container.
        var dbName = $"test_totp_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName).ConfigureAwait(false);

        _factory = IntegrationWebApplicationFactory.Create(connStr);

        // Apply migrations on the freshly-created empty database (pre-flight Q2).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async ValueTask<UserEntity> SeedUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid():N}@test.local",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            Language = "en",
            Theme = "light",
            DataRetentionDays = 365,
            Status = "active",
            EmailVerified = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GenerateSetupAsync_PersistsSecret_UnderNoTrackingDefault()
    {
        // Arrange: seed user.
        var user = await SeedUserAsync();

        // Act: invoke the writer via the same DI scope as production.
        using (var actScope = _factory.Services.CreateScope())
        {
            var totp = actScope.ServiceProvider.GetRequiredService<ITotpService>();
            await totp.GenerateSetupAsync(user.Id, user.Email);
        }

        // Assert: re-read from a fresh scope. With the Update() workaround OR the canonical
        // AsTracking() pattern this passes. With FindAsync alone (no tracking opt-in), this
        // FAILS — TotpSecretEncrypted stays null.
        using (var assertScope = _factory.Services.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reloaded = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            reloaded.TotpSecretEncrypted.Should().NotBeNull(
                "GenerateSetupAsync must persist the encrypted secret to the users table");
        }
    }

    [Fact]
    public async Task EnableTwoFactorAsync_PersistsEnabledFlag_UnderNoTrackingDefault()
    {
        // Arrange: seed user, run setup to produce a valid secret + persisted encrypted secret.
        var user = await SeedUserAsync();

        string secret;
        using (var setupScope = _factory.Services.CreateScope())
        {
            var totp = setupScope.ServiceProvider.GetRequiredService<ITotpService>();
            var setup = await totp.GenerateSetupAsync(user.Id, user.Email);
            secret = setup.Secret;
        }

        // Compute a valid TOTP code for the secret using OtpNet (matches the service impl).
        var totpComputer = new Totp(Base32Encoding.ToBytes(secret), step: 30);
        var code = totpComputer.ComputeTotp();

        // Act: invoke EnableTwoFactorAsync.
        bool result;
        using (var actScope = _factory.Services.CreateScope())
        {
            var totp = actScope.ServiceProvider.GetRequiredService<ITotpService>();
            result = await totp.EnableTwoFactorAsync(user.Id, code);
        }

        // Assert
        result.Should().BeTrue("the freshly computed TOTP code must verify");
        using (var assertScope = _factory.Services.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reloaded = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            reloaded.IsTwoFactorEnabled.Should().BeTrue();
            reloaded.TwoFactorEnabledAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DisableTwoFactorAsync_ClearsSecretAndFlag_UnderNoTrackingDefault()
    {
        // Arrange: seed user, fully enable 2FA.
        var user = await SeedUserAsync();

        string secret;
        using (var setupScope = _factory.Services.CreateScope())
        {
            var totp = setupScope.ServiceProvider.GetRequiredService<ITotpService>();
            var setup = await totp.GenerateSetupAsync(user.Id, user.Email);
            secret = setup.Secret;
        }

        var totpComputer = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret), step: 30);
        var enableCode = totpComputer.ComputeTotp();

        using (var enableScope = _factory.Services.CreateScope())
        {
            var totp = enableScope.ServiceProvider.GetRequiredService<ITotpService>();
            (await totp.EnableTwoFactorAsync(user.Id, enableCode)).Should().BeTrue();
        }

        // Disable requires password + a fresh TOTP code. The enable code is consumed by the
        // replay-prevention nonce check, so we need a code from a different TOTP step.
        // VerifyTotpCodeAsync accepts a ±2-step (±60s) window, so a 31s wait sits too close to
        // the enable code's acceptance window and can flake under CI load. Wait 91s (>3 full
        // 30s steps) to clear both the verification window and the nonce reuse risk.
        await Task.Delay(TimeSpan.FromSeconds(91));
        var disableCode = totpComputer.ComputeTotp();

        // We need a known password on the seeded user — UPDATE it inline.
        const string password = "TestPassword123!";
        var hasher = _factory.Services.GetRequiredService<IPasswordHashingService>();
        using (var pwScope = _factory.Services.CreateScope())
        {
            var db = pwScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var u = await db.Users.AsTracking().FirstAsync(x => x.Id == user.Id);
            u.PasswordHash = hasher.HashSecret(password);
            await db.SaveChangesAsync();
        }

        // Act
        using (var actScope = _factory.Services.CreateScope())
        {
            var totp = actScope.ServiceProvider.GetRequiredService<ITotpService>();
            await totp.DisableTwoFactorAsync(user.Id, password, disableCode);
        }

        // Assert: secret + flag cleared, backup codes removed.
        using (var assertScope = _factory.Services.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reloaded = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            reloaded.IsTwoFactorEnabled.Should().BeFalse();
            reloaded.TotpSecretEncrypted.Should().BeNull();
            reloaded.TwoFactorEnabledAt.Should().BeNull();

            var remainingCodes = await db.UserBackupCodes.CountAsync(c => c.UserId == user.Id);
            remainingCodes.Should().Be(0);
        }
    }
}
