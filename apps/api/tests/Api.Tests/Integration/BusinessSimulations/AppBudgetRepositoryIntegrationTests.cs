using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using AppBudgetAggregate = Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets.AppBudget;

namespace Api.Tests.Integration.BusinessSimulations;

/// <summary>
/// Integration tests for <see cref="AppBudgetRepository"/> using a real
/// Testcontainers PostgreSQL instance (Issue #1838 SP5 F4-C5).
///
/// Covers: insert-on-upsert (singleton creation), update-on-upsert (in-place
/// edit), GetCurrentAsync semantics, optimistic concurrency via stale
/// RowVersion, and singleton enforcement (second Upsert reuses the same row
/// rather than creating a duplicate).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class AppBudgetRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAppBudgetRepository? _repository;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AppBudgetRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_appbudgetrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IAppBudgetRepository, AppBudgetRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IAppBudgetRepository>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch (NpgsqlException)
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsNull_WhenBudgetNeverConfigured()
    {
        var result = await _repository!.GetCurrentAsync(TestCancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewBudget_WhenAbsent()
    {
        var budget = AppBudgetAggregate.Create(
            Money.Create(1500m, "USD"),
            alertThresholdPct: 80,
            criticalThresholdPct: 95,
            createdBy: "admin@meepleai.dev");

        await _repository!.UpsertAsync(budget, TestCancellationToken);

        var fetched = await _repository.GetCurrentAsync(TestCancellationToken);

        fetched.Should().NotBeNull();
        fetched!.MonthlyLimit.Amount.Should().Be(1500m);
        fetched.MonthlyLimit.Currency.Should().Be("USD");
        fetched.AlertThresholdPct.Should().Be(80);
        fetched.CriticalThresholdPct.Should().Be(95);
        fetched.IsEnabled.Should().BeTrue();
        fetched.CreatedBy.Should().Be("admin@meepleai.dev");
        fetched.UpdatedBy.Should().Be("admin@meepleai.dev");
        fetched.RowVersion.Should().NotBeEmpty(
            "Postgres xmin must populate the concurrency token on insert");
    }

    [Fact]
    public async Task UpsertAsync_SecondCall_UpdatesInPlaceAndKeepsSingleton()
    {
        var first = AppBudgetAggregate.Create(Money.Create(1000m, "USD"), 80, 95, "admin");
        await _repository!.UpsertAsync(first, TestCancellationToken);

        var loaded = await _repository.GetCurrentAsync(TestCancellationToken);
        loaded!.UpdateLimit(Money.Create(2500m, "USD"), 75, 92, "admin2");

        await _repository.UpsertAsync(loaded, TestCancellationToken);

        var refetched = await _repository.GetCurrentAsync(TestCancellationToken);
        refetched.Should().NotBeNull();
        refetched!.MonthlyLimit.Amount.Should().Be(2500m);
        refetched.AlertThresholdPct.Should().Be(75);
        refetched.CriticalThresholdPct.Should().Be(92);
        refetched.UpdatedBy.Should().Be("admin2");

        // Singleton enforcement: only one row should exist.
        var rowCount = await _dbContext!.AppBudgets.CountAsync(TestCancellationToken);
        rowCount.Should().Be(1, "AppBudget is a singleton — second upsert must update, not insert");
    }

    [Fact]
    public async Task UpsertAsync_OnStaleRowVersion_ThrowsConcurrencyException()
    {
        var initial = AppBudgetAggregate.Create(Money.Create(1000m, "USD"), 80, 95, "admin");
        await _repository!.UpsertAsync(initial, TestCancellationToken);

        // Admin A and Admin B both load the same row.
        var loadedByA = await _repository.GetCurrentAsync(TestCancellationToken);
        var loadedByB = await _repository.GetCurrentAsync(TestCancellationToken);

        // Admin A commits first → bumps RowVersion.
        loadedByA!.UpdateLimit(Money.Create(2000m, "USD"), 80, 95, "adminA");
        await _repository.UpsertAsync(loadedByA, TestCancellationToken);

        // Admin B attempts to commit with the stale RowVersion captured before
        // Admin A's update.
        loadedByB!.UpdateLimit(Money.Create(3000m, "USD"), 80, 95, "adminB");

        var act = async () => await _repository.UpsertAsync(loadedByB, TestCancellationToken);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task UpsertAsync_PreservesCreatedAtAndCreatedBy_AcrossUpdates()
    {
        var first = AppBudgetAggregate.Create(Money.Create(1000m, "USD"), 80, 95, "founder");
        await _repository!.UpsertAsync(first, TestCancellationToken);
        var loaded = await _repository.GetCurrentAsync(TestCancellationToken);
        var origCreatedAt = loaded!.CreatedAt;

        loaded.UpdateLimit(Money.Create(2000m, "USD"), 80, 95, "ops");
        await _repository.UpsertAsync(loaded, TestCancellationToken);

        var refetched = await _repository.GetCurrentAsync(TestCancellationToken);
        refetched!.CreatedBy.Should().Be("founder",
            "CreatedBy is set on insert and must survive subsequent updates");
        refetched.CreatedAt.Should().BeCloseTo(origCreatedAt, TimeSpan.FromSeconds(1));
    }
}
