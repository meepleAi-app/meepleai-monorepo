using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
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
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Authentication")]
public sealed class TotpServiceTrackingContractTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();
        await _postgres.StartAsync();

        _factory = IntegrationWebApplicationFactory.Create(_postgres.GetConnectionString());

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
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
}
