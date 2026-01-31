using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Integration;

/// <summary>
/// Integration tests for Config Import/Export with real database.
/// Tests end-to-end configuration import/export operations with PostgreSQL.
/// Uses SharedTestcontainersFixture for Docker hijack prevention (Issue #2031).
/// Issue: #2188
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "Testcontainers")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "2188")]
[Trait("Issue", "2031")]
[Trait("Issue", "2620")]
public class ConfigImportExportIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    // Test data constants
    private static readonly Guid TestUserId = new("40000000-0000-0000-0000-000000000001");

    public ConfigImportExportIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Use SharedTestcontainersFixture for safe container management
        _databaseName = "test_configimportexport_" + Guid.NewGuid().ToString("N");
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Setup dependency injection
        var services = new ServiceCollection();

        // Configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _isolatedDbConnectionString
        });
        var configuration = configBuilder.Build();
        services.AddSingleton<IConfiguration>(configuration);

        // MediatR (required by DbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Domain event infrastructure (required by DbContext)
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // HybridCache (required by event handlers) - Issue #2620
        services.AddHybridCache();

        // Mock IHybridCacheService for testing (required by event handlers) - Issue #2620
        services.AddScoped<Api.Services.IHybridCacheService>(_ =>
            Moq.Mock.Of<Api.Services.IHybridCacheService>());

        // DbContext with enforced connection settings (Issue #2031 best practices)
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(enforcedBuilder.ConnectionString)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // Register repositories and handlers
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ImportConfigsCommandHandler>();
        services.AddScoped<ExportConfigsQueryHandler>();

        // Logging infrastructure (required by MediatR)
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database schema
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        // Seed required User for FK constraints (Issue #2620)
        await SeedTestUserAsync();
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

        // Issue #2031: Clean up isolated database
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    #region Helper Methods

    /// <summary>
    /// Seeds a test user to satisfy CreatedByUserId FK constraint.
    /// Issue: #2620
    /// </summary>
    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-configimportexport@meepleai.dev",
            DisplayName = "Test User",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
    }

    #endregion

    [Fact(Timeout = 30000)] // 30s timeout for integration tests
    public async Task ImportAndExport_EndToEnd_WithRealDatabase()
    {
        // Arrange
        var userId = TestUserId;
        var importHandler = _serviceProvider!.GetRequiredService<ImportConfigsCommandHandler>();
        var exportHandler = _serviceProvider!.GetRequiredService<ExportConfigsQueryHandler>();

        var configsToImport = new List<ConfigurationImportItem>
        {
            new("App:Name", "MeepleAI", "string", "Application name", "App", true, false, "All"),
            new("App:Version", "1.0.0", "string", "App version", "App", true, false, "All"),
            new("Feature:Beta", "true", "bool", "Beta feature flag", "Features", false, false, "All")
        };

        var importCommand = new ImportConfigsCommand(configsToImport, OverwriteExisting: false, userId);

        // Act - Import
        var importCount = await importHandler.Handle(importCommand, CancellationToken.None);

        // Assert - Import
        importCount.Should().Be(3);

        // Act - Export
        var exportQuery = new ExportConfigsQuery(Environment: "All", ActiveOnly: false);
        var exportResult = await exportHandler.Handle(exportQuery, CancellationToken.None);

        // Assert - Export
        exportResult.Should().NotBeNull();
        exportResult.Configurations.Should().HaveCount(3);
        exportResult.Environment.Should().Be("All");
        exportResult.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify exported data matches imported
        exportResult.Configurations.Should().Contain(c => c.Key == "App:Name" && c.Value == "MeepleAI");
        exportResult.Configurations.Should().Contain(c => c.Key == "App:Version" && c.Value == "1.0.0");
        exportResult.Configurations.Should().Contain(c => c.Key == "Feature:Beta" && c.Value == "true" && !c.IsActive);
    }

    [Fact(Timeout = 30000)] // 30s timeout for integration tests
    public async Task ImportWithOverwrite_UpdatesExistingConfig_InRealDatabase()
    {
        // Arrange
        var userId = TestUserId;
        var repository = _serviceProvider!.GetRequiredService<IConfigurationRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
        var importHandler = _serviceProvider!.GetRequiredService<ImportConfigsCommandHandler>();

        // Create initial configuration in database
        var initialConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("App:MaxUsers"),
            value: "100",
            valueType: "int",
            createdByUserId: userId,
            description: "Max users",
            category: "App",
            environment: "All"
        );

        await repository.AddAsync(initialConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Clear change tracker to allow fresh load (Issue #2620)
        _dbContext!.ChangeTracker.Clear();

        // Import with overwrite
        var configsToImport = new List<ConfigurationImportItem>
        {
            new("App:MaxUsers", "200", "int", "Max users updated", "App", true, false, "All")
        };

        var importCommand = new ImportConfigsCommand(configsToImport, OverwriteExisting: true, userId);

        // Act
        var importCount = await importHandler.Handle(importCommand, CancellationToken.None);

        // Assert
        importCount.Should().Be(1);

        // Verify update in database
        var updatedConfig = await repository.GetByKeyAsync("App:MaxUsers", "All", false, CancellationToken.None);
        updatedConfig.Should().NotBeNull();
        updatedConfig!.Value.Should().Be("200");
        updatedConfig.Version.Should().Be(2); // Version incremented
    }

    [Fact(Timeout = 30000)] // 30s timeout for integration tests
    public async Task Export_WithActiveOnlyFilter_ReturnsOnlyActiveConfigs()
    {
        // Arrange
        var userId = TestUserId;
        var repository = _serviceProvider!.GetRequiredService<IConfigurationRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
        var exportHandler = _serviceProvider!.GetRequiredService<ExportConfigsQueryHandler>();

        // Create active and inactive configurations
        var activeConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("Active:Config"),
            value: "active_value",
            valueType: "string",
            createdByUserId: userId,
            environment: "All"
        );

        var inactiveConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("Inactive:Config"),
            value: "inactive_value",
            valueType: "string",
            createdByUserId: userId,
            environment: "All"
        );
        inactiveConfig.Deactivate();

        await repository.AddAsync(activeConfig, CancellationToken.None);
        await repository.AddAsync(inactiveConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act - Export with ActiveOnly=true
        var exportQuery = new ExportConfigsQuery(Environment: "All", ActiveOnly: true);
        var exportResult = await exportHandler.Handle(exportQuery, CancellationToken.None);

        // Assert
        exportResult.Should().NotBeNull();
        exportResult.Configurations.Should().HaveCount(1);
        exportResult.Configurations.Should().Contain(c => c.Key == "Active:Config");
        exportResult.Configurations.Should().NotContain(c => c.Key == "Inactive:Config");
    }

    [Fact(Timeout = 30000)] // 30s timeout for integration tests
    public async Task Export_WithEnvironmentFilter_ReturnsMatchingConfigs()
    {
        // Arrange
        var userId = TestUserId;
        var repository = _serviceProvider!.GetRequiredService<IConfigurationRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
        var exportHandler = _serviceProvider!.GetRequiredService<ExportConfigsQueryHandler>();

        // Create configurations with different environments
        var prodConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("Prod:Setting"),
            value: "prod_value",
            valueType: "string",
            createdByUserId: userId,
            environment: "Production"
        );

        var devConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("Dev:Setting"),
            value: "dev_value",
            valueType: "string",
            createdByUserId: userId,
            environment: "Development"
        );

        var allConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("Global:Setting"),
            value: "global_value",
            valueType: "string",
            createdByUserId: userId,
            environment: "All"
        );

        await repository.AddAsync(prodConfig, CancellationToken.None);
        await repository.AddAsync(devConfig, CancellationToken.None);
        await repository.AddAsync(allConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act - Export Production environment
        var exportQuery = new ExportConfigsQuery(Environment: "Production", ActiveOnly: false);
        var exportResult = await exportHandler.Handle(exportQuery, CancellationToken.None);

        // Assert
        exportResult.Should().NotBeNull();
        exportResult.Configurations.Should().HaveCount(2); // Production + All
        exportResult.Configurations.Should().Contain(c => c.Key == "Prod:Setting");
        exportResult.Configurations.Should().Contain(c => c.Key == "Global:Setting");
        exportResult.Configurations.Should().NotContain(c => c.Key == "Dev:Setting");
    }
}
