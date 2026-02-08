using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for Token Limit Enforcement with real database and cache.
/// Tests 80% warning threshold, 100% blocking behavior, and token reset logic.
/// Issue #3697: Epic 1 - Testing & Integration (Phase 2b)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3697")]
public sealed class TokenLimitEnforcementIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ITokenTierRepository? _tierRepository;
    private IUserTokenUsageRepository? _usageRepository;
    private ITokenTrackingService? _tokenTrackingService;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data
    private static readonly Guid TestUserId1 = new("B0000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("B0000000-0000-0000-0000-000000000002");
    private static readonly Guid TestUserId3 = new("B0000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId4 = new("B0000000-0000-0000-0000-000000000004");

    public TokenLimitEnforcementIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database
        _databaseName = $"test_tokenlimit_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<ITokenTierRepository, TokenTierRepository>();
        services.AddScoped<IUserTokenUsageRepository, UserTokenUsageRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // HybridCache (in-memory for tests)
        services.AddHybridCache();
        services.AddScoped<IHybridCacheService, HybridCacheService>();
        services.AddScoped<ITokenTrackingService, TokenTrackingService>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _tierRepository = _serviceProvider.GetRequiredService<ITokenTierRepository>();
        _usageRepository = _serviceProvider.GetRequiredService<IUserTokenUsageRepository>();
        _tokenTrackingService = _serviceProvider.GetRequiredService<ITokenTrackingService>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await ((ServiceProvider)_serviceProvider).DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task TokenEnforcement_Below80Percent_ShouldAllowWithoutWarning()
    {
        // Arrange - Create Free tier (10,000 tokens/month)
        var tier = TokenTier.Create(TierName.Free, TierLimits.FreeTier(), TierPricing.FreeTier());
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Create user usage
        var usage = UserTokenUsage.Create(TestUserId1, tier.Id);
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Use 7900 tokens (79%, below 80% threshold)
        var (exceeded, remaining) = await _tokenTrackingService!.TrackUsageAsync(
            TestUserId1,
            tokensConsumed: 7900,
            cost: 1.58m,
            TestCancellationToken
        );

        // Assert - Should allow usage without warning
        exceeded.Should().BeFalse();
        remaining.Should().Be(2100); // 10,000 - 7,900

        var updatedUsage = await _usageRepository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        updatedUsage.Should().NotBeNull();
        updatedUsage!.TokensUsed.Should().Be(7900);
        updatedUsage.IsNearLimit.Should().BeFalse(); // Below 80%
        updatedUsage.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task TokenEnforcement_At80Percent_ShouldWarnButAllowUsage()
    {
        // Arrange - Create Basic tier (50,000 tokens/month)
        var tier = TokenTier.Create(TierName.Basic, TierLimits.BasicTier(), TierPricing.BasicTier());
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var usage = UserTokenUsage.Create(TestUserId2, tier.Id);
        usage.RecordUsage(39999, 7.99m); // Start at 79.998%
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Add 1 token to cross 80% threshold (40,000 / 50,000 = 80%)
        var (exceeded, remaining) = await _tokenTrackingService!.TrackUsageAsync(
            TestUserId2,
            tokensConsumed: 1,
            cost: 0.0002m,
            TestCancellationToken
        );

        // Assert - Should allow usage but set warning flag
        exceeded.Should().BeFalse();
        remaining.Should().Be(10000); // 50,000 - 40,000

        var updatedUsage = await _usageRepository.GetByUserIdAsync(TestUserId2, TestCancellationToken);
        updatedUsage.Should().NotBeNull();
        updatedUsage!.TokensUsed.Should().Be(40000);
        updatedUsage.IsNearLimit.Should().BeTrue(); // >= 80%
        updatedUsage.IsBlocked.Should().BeFalse(); // < 100%
        updatedUsage.Warnings.Should().HaveCountGreaterThanOrEqualTo(1); // Warning recorded
    }

    [Fact]
    public async Task TokenEnforcement_At100Percent_ShouldBlockUsage()
    {
        // Arrange - Create Free tier (10,000 tokens/month)
        var tier = TokenTier.Create(TierName.Free, TierLimits.FreeTier(), TierPricing.FreeTier());
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var usage = UserTokenUsage.Create(TestUserId3, tier.Id);
        usage.RecordUsage(9999, 1.99m); // Start at 99.99%
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Try to add 2 tokens to exceed limit (10,001 / 10,000 = 100.01%)
        var (exceeded, remaining) = await _tokenTrackingService!.TrackUsageAsync(
            TestUserId3,
            tokensConsumed: 2,
            cost: 0.0004m,
            TestCancellationToken
        );

        // Assert - Should block usage
        exceeded.Should().BeTrue();
        remaining.Should().Be(0);

        var updatedUsage = await _usageRepository.GetByUserIdAsync(TestUserId3, TestCancellationToken);
        updatedUsage.Should().NotBeNull();
        updatedUsage!.TokensUsed.Should().Be(10001); // Usage recorded but blocked
        updatedUsage.IsNearLimit.Should().BeTrue();
        updatedUsage.IsBlocked.Should().BeTrue(); // >= 100%
    }

    [Fact]
    public async Task TokenEnforcement_MultipleRequests_ShouldAccumulateCorrectly()
    {
        // Arrange - Create Pro tier (200,000 tokens/month)
        var tier = TokenTier.Create(TierName.Pro, TierLimits.ProTier(), TierPricing.ProTier());
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var usage = UserTokenUsage.Create(TestUserId4, tier.Id);
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple incremental usage requests
        await _tokenTrackingService!.TrackUsageAsync(TestUserId4, 50000, 4.00m, TestCancellationToken); // 25%
        await _tokenTrackingService.TrackUsageAsync(TestUserId4, 60000, 4.80m, TestCancellationToken); // 55%
        await _tokenTrackingService.TrackUsageAsync(TestUserId4, 70000, 5.60m, TestCancellationToken); // 90%

        // Assert - Should accumulate to 180,000 tokens (90%)
        var finalUsage = await _usageRepository.GetByUserIdAsync(TestUserId4, TestCancellationToken);
        finalUsage.Should().NotBeNull();
        finalUsage!.TokensUsed.Should().Be(180000);
        finalUsage.Cost.Should().Be(14.40m);
        finalUsage.MessagesCount.Should().Be(3);
        finalUsage.IsNearLimit.Should().BeTrue(); // 90% > 80%
        finalUsage.IsBlocked.Should().BeFalse(); // 90% < 100%

        // Verify can still use remaining 10%
        var (exceeded, remaining) = await _tokenTrackingService.TrackUsageAsync(
            TestUserId4,
            tokensConsumed: 10000,
            cost: 0.80m,
            TestCancellationToken
        );

        exceeded.Should().BeFalse();
        remaining.Should().Be(10000); // 200,000 - 190,000
    }
}
