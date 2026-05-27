using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for <see cref="GetAdminsWithoutTwoFactorQueryHandler"/> (SP5 Admin Security S3 — T6).
/// Verifies the compliance-sweep predicate: only users with Role in (admin, superadmin) AND
/// IsTwoFactorEnabled=false are returned; admins who already enrolled and non-admin users are excluded.
/// Exercises the real repository (<see cref="UserRepository.GetAdminUsersAsync"/>) against Postgres,
/// not a hand-built DTO (lesson S2 — acceptance must exercise the real pipeline).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
public sealed class GetAdminsWithoutTwoFactorQueryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetAdminsWithoutTwoFactorQueryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_admins_no_2fa_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null) await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    [Fact]
    public async Task Handle_ReturnsOnlyAdminsAndSuperadminsWithoutTwoFactor()
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var adminNo2Fa = SeedUser(db, "admin-no2fa@meepleai.test", role: "admin", twoFactor: false);
        var superadminNo2Fa = SeedUser(db, "root-no2fa@meepleai.test", role: "superadmin", twoFactor: false);
        SeedUser(db, "admin-with2fa@meepleai.test", role: "admin", twoFactor: true);     // excluded: enrolled
        SeedUser(db, "regular@meepleai.test", role: "user", twoFactor: false);            // excluded: not admin
        await db.SaveChangesAsync(TestCancellationToken);

        var handler = new GetAdminsWithoutTwoFactorQueryHandler(
            new UserRepository(db, Mock.Of<IDomainEventCollector>()));

        // Act
        var result = await handler.Handle(new GetAdminsWithoutTwoFactorQuery(), TestCancellationToken);

        // Assert — exactly the two non-enrolled privileged accounts.
        result.Should().HaveCount(2);
        result.Select(u => u.Id).Should().BeEquivalentTo(new[] { adminNo2Fa, superadminNo2Fa });
        result.Should().OnlyContain(u => !u.IsTwoFactorEnabled);
        result.Select(u => u.Email).Should().Contain("admin-no2fa@meepleai.test")
            .And.NotContain("admin-with2fa@meepleai.test")
            .And.NotContain("regular@meepleai.test");
    }

    [Fact]
    public async Task Handle_AllAdminsEnrolled_ReturnsEmpty()
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        SeedUser(db, "admin-a@meepleai.test", role: "admin", twoFactor: true);
        SeedUser(db, "root-b@meepleai.test", role: "superadmin", twoFactor: true);
        SeedUser(db, "regular-c@meepleai.test", role: "user", twoFactor: false);
        await db.SaveChangesAsync(TestCancellationToken);

        var handler = new GetAdminsWithoutTwoFactorQueryHandler(
            new UserRepository(db, Mock.Of<IDomainEventCollector>()));

        var result = await handler.Handle(new GetAdminsWithoutTwoFactorQuery(), TestCancellationToken);

        result.Should().BeEmpty();
    }

    private static Guid SeedUser(MeepleAiDbContext db, string email, string role, bool twoFactor)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = id,
            Email = email,
            DisplayName = email.Split('@')[0],
            Role = role,
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = twoFactor,
            TwoFactorEnabledAt = twoFactor ? DateTime.UtcNow : null,
            // The entity→domain mapping only restores 2FA state when both the flag AND an encrypted
            // secret are present (UserRepository.MapToDomain) — mirroring Enable2FA, which always sets
            // both. A secret is therefore required to model a genuinely 2FA-enrolled account.
            TotpSecretEncrypted = twoFactor ? "encrypted-totp-secret-test" : null,
        });
        return id;
    }

    private static async Task ApplyMigrationsAsync(MeepleAiDbContext db)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
        }
    }
}
