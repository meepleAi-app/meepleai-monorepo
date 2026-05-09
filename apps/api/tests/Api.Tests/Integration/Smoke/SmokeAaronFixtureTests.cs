using Api.Infrastructure;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Smoke;

/// <summary>
/// Verifies that the smoke-test-users.sql fixture inserts the smoke-aaron@meepleai.test
/// persona correctly after EF Core migrations are applied.
///
/// This persona is used by the Bruno-based API smoke test collection
/// (tests/api-smoke/) to validate tier-quota gating in sub-issues SG1-SG4 (#902-905).
///
/// Issue #910 (SG0) — Foundation: Bruno setup + smoke persona.
/// </summary>
[Collection("Integration-GroupA")]
public sealed class SmokeAaronFixtureTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    public SmokeAaronFixtureTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_smokeaaron_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations so the schema is ready
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync();
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SmokeAaronFixture_AppliedAfterMigrations_HasFreeTier()
    {
        // Arrange — resolve path to tests/fixtures/smoke-test-users.sql
        // BaseDirectory is the test output dir; we walk up to repo root.
        // Output path: apps/api/tests/Api.Tests/bin/Debug/net9.0/
        // Repo root:   ../../../../../../../  (7 levels up)
        var fixturePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "..",
                "tests", "fixtures", "smoke-test-users.sql"));

        var fixtureSql = await File.ReadAllTextAsync(fixturePath);
        fixtureSql.Should().NotBeNullOrWhiteSpace("fixture file must not be empty");

        // Act — apply the SQL fixture
        await _dbContext!.Database.ExecuteSqlRawAsync(fixtureSql);

        // Assert
        var aaron = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "smoke-aaron@meepleai.test");

        aaron.Should().NotBeNull("smoke-aaron fixture should insert the user");
        aaron!.Tier.Should().Be("free", "smoke-aaron is the free-tier persona for SG smoke tests");
        aaron.Role.Should().Be("user", "smoke-aaron must NOT be admin/editor/superadmin (those bypass tier limits)");
        aaron.IsContributor.Should().BeFalse("contributors are resolved as premium tier");
        aaron.IsDemoAccount.Should().BeTrue("smoke-aaron is a demo/test account");
        aaron.EmailVerified.Should().BeTrue("avoid grace-period gating in smoke tests");
        aaron.Status.Should().Be("Active");
        aaron.PasswordHash.Should().NotBeNullOrEmpty("must be able to login");
    }
}
