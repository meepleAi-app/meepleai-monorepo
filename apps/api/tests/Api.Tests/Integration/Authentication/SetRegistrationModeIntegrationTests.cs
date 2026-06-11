using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for SetRegistrationModeCommandHandler against real PostgreSQL.
/// Issue #2116 follow-up: a real DB exercise (Testcontainers) is the only way to catch
/// the actual 23505 duplicate-key violation that the unit-test mocks cannot see.
///
/// Scenario covered:
///   - Pre-existing seed row at Environment="Production" + current env = "Development"
///     → handler must NOT raise 23505, must write a new row at Environment="Development".
///   - Idempotency: second call in the same env must hit the Update path, no duplicate row.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2116")]
public sealed class SetRegistrationModeIntegrationTests : IAsyncLifetime
{
    private const string RegistrationKey = "Registration:PublicEnabled";
    private const string DevelopmentEnv = "Development";
    private const string ProductionEnv = "Production";

    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private FakeTimeProvider _timeProvider = null!;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid AdminUserId = new("a0000000-0000-0000-0000-000000002116");

    public SetRegistrationModeIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_setregmode_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var services = IntegrationServiceCollectionBuilder.CreateBase(enforcedBuilder.ConnectionString);

        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);

        // Auth repos needed to seed the admin user (FK target for system_configurations.CreatedByUserId).
        services.AddScoped<IUserRepository, UserRepository>();

        // SystemConfiguration repos + service required by SetRegistrationModeCommandHandler
        // and the CreateConfigurationCommandHandler / UpdateConfigValueCommandHandler it dispatches.
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IConfigurationService, ConfigurationService>();

        // IWebHostEnvironment stub — pretend ASPNETCORE_ENVIRONMENT="Development" to reproduce the bug.
        var environmentMock = new Mock<IWebHostEnvironment>();
        environmentMock.Setup(e => e.EnvironmentName).Returns(DevelopmentEnv);
        services.AddSingleton(environmentMock.Object);

        // Override the default Mock.Of<IHybridCacheService>() from CreateBase: it returns
        // null without invoking the factory, so ConfigurationService.GetConfigurationByKeyAsync
        // can never observe the row written by a previous call → the second toggle re-enters
        // the Create branch and triggers a real 23505 inside the test. Use a passthrough impl.
        services.RemoveAll<IHybridCacheService>();
        services.AddScoped<IHybridCacheService, PassthroughHybridCache>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await MigrateWithRetry(_dbContext);

        // Seed admin user (FK target).
        var userRepo = _serviceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var adminUser = new User(
            AdminUserId,
            new Email("admin-2116@test.meepleai.dev"),
            "Test Admin 2116",
            PasswordHash.Create("AdminUnusualPwd123!"),
            Role.Admin);
        await userRepo.AddAsync(adminUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch (Exception ex)
            {
                _output($"Warning: Failed to drop database {_databaseName}: {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task SetRegistrationMode_WithSeedRowAtProduction_AndCurrentEnvIsDevelopment_DoesNotRaiseDuplicateKey()
    {
        // Arrange: seed the "Production" row that triggered the original 500.
        // Mirrors the row produced by Migration 20260... InitialCreate seed for Registration:PublicEnabled.
        await SeedSystemConfigurationRowAsync(ProductionEnv, "false", version: 1);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Act: superadmin toggles registration mode → handler enters Create branch
        // (no row exists for Environment="Development" yet). Pre-fix this raised 23505
        // because the Create branch hardcoded Environment="Production".
        var act = async () => await mediator.Send(
            new SetRegistrationModeCommand(Enabled: true, AdminId: AdminUserId),
            TestCancellationToken);

        await act.Should().NotThrowAsync();

        // Assert: there are now TWO independent rows — the original Production seed (unchanged)
        // and the new Development row carrying the toggled value.
        var rows = await _dbContext!.Set<SystemConfigurationEntity>()
            .Where(c => c.Key == RegistrationKey)
            .OrderBy(c => c.Environment)
            .ToListAsync(TestCancellationToken);

        rows.Should().HaveCount(2, "the handler must keep the Production seed and add a Development row, never overwrite");

        var devRow = rows.Should().ContainSingle(r => r.Environment == DevelopmentEnv).Subject;
        devRow.Value.Should().Be("true");
        devRow.Version.Should().Be(1, "the Create path must produce a brand-new row at Version=1 — symmetric guard with Scenario 2's Version=2 assertion on the Update path");
        devRow.CreatedByUserId.Should().Be(AdminUserId);

        var prodRow = rows.Should().ContainSingle(r => r.Environment == ProductionEnv).Subject;
        prodRow.Value.Should().Be("false", "the original Production seed must remain untouched");
    }

    [Fact]
    public async Task SetRegistrationMode_TwoConsecutiveTogglesInDevelopment_HitUpdatePath_NoDuplicateRow()
    {
        // Arrange: seed Production row (the original bug condition). Each MediatR send happens
        // in a fresh DI scope to mirror the production behavior: every HTTP request gets its own
        // scoped DbContext + scoped ConfigurationService. Sharing a single scope across both
        // toggles would let the EF Core change tracker see a tracked Get-side entity and a
        // detached Update-side entity with the same Id — a tracker conflict unrelated to #2116
        // and not reproducible from a real HTTP path.
        await SeedSystemConfigurationRowAsync(ProductionEnv, "false", version: 1);

        var scopeFactory = _serviceProvider!.GetRequiredService<IServiceScopeFactory>();

        await using (var scope1 = scopeFactory.CreateAsyncScope())
        {
            var mediator1 = scope1.ServiceProvider.GetRequiredService<IMediator>();
            await mediator1.Send(
                new SetRegistrationModeCommand(Enabled: true, AdminId: AdminUserId),
                TestCancellationToken);
        }

        // Act: second toggle flipping the value, in a fresh scope.
        await using var scope2 = scopeFactory.CreateAsyncScope();
        var mediator2 = scope2.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator2.Send(
            new SetRegistrationModeCommand(Enabled: false, AdminId: AdminUserId),
            TestCancellationToken);

        await act.Should().NotThrowAsync(
            "the second call must take the Update path; entering Create again would violate IX_system_configurations_Key_Environment");

        // Assert: still exactly ONE Development row (updated, not duplicated). Production row untouched.
        // Use a fresh DbContext scope to bypass any change-tracker state held by the assertion fixture's _dbContext.
        await using var assertScope = scopeFactory.CreateAsyncScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var devRows = await assertContext.Set<SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Key == RegistrationKey && c.Environment == DevelopmentEnv)
            .ToListAsync(TestCancellationToken);
        devRows.Should().HaveCount(1);
        devRows[0].Value.Should().Be("false", "the second toggle flipped the value back to false");
        devRows[0].Version.Should().Be(2, "the Update path must bump Version");
    }

    private async Task SeedSystemConfigurationRowAsync(string environment, string value, int version)
    {
        var entity = new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = RegistrationKey,
            Value = value,
            ValueType = "Boolean",
            Description = "Controls whether public registration is enabled",
            Category = "Authentication",
            IsActive = true,
            RequiresRestart = false,
            Environment = environment,
            Version = version,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = AdminUserId,
            UpdatedByUserId = null,
        };

        _dbContext!.Set<SystemConfigurationEntity>().Add(entity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task MigrateWithRetry(MeepleAiDbContext context, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                _output($"Migration attempt {i + 1} failed: {ex.Message}. Retrying...");
                await Task.Delay(TimeSpan.FromSeconds(2), TestCancellationToken);
            }
        }
    }

    /// <summary>
    /// Always invoke the factory — needed for end-to-end tests where the same DbContext
    /// must see writes performed by earlier MediatR sends. The default
    /// Mock.Of&lt;IHybridCacheService&gt;() returns null without calling the factory and
    /// the lookup is permanently stuck on a cache miss.
    /// </summary>
    private sealed class PassthroughHybridCache : IHybridCacheService
    {
        public Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            string[]? tags = null,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class
            => factory(ct);

        public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

        public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);

        public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(new HybridCacheStats());
    }
}
