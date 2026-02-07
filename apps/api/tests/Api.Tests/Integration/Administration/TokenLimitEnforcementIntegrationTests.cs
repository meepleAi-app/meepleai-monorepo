using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
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
/// Issue #3697: Epic 1 - Testing & Integration (Phase 2)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Dependency", "Redis")]
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
    private TokenTrackingService? _tokenTrackingService;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data
    private static readonly Guid TestTierId = new("A0000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId1 = new("B0000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("B0000000-0000-0000-0000-000000000002");
    private static readonly Guid TestUserId3 = new("B0000000-0000-0000-0000-000000000003");

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
        services.AddScoped<TokenTrackingService>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _tierRepository = _serviceProvider.GetRequiredService<ITokenTierRepository>();
        _usageRepository = _serviceProvider.GetRequiredService<IUserTokenUsageRepository>();
        _tokenTrackingService = _serviceProvider.GetRequiredService<TokenTrackingService>();
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
    public async Task TokenEnforcement_At80Percent_ShouldWarnButAllowUsage()
    {
        // Arrange - Create tier with 1000 monthly tokens
        var tier = TokenTier.Create(
            TestTierId,
            "Test Tier",
            TierType.Premium,
            monthlyTokenLimit: 1000,
            pricePerMonth: 10m
        );
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Create user usage at 79% (790 tokens)
        var usage = UserTokenUsage.Create(TestUserId1, TestTierId, currentPeriodStart: DateTime.UtcNow.AddDays(-15));
        usage.RecordTokenUsage(790, "prompt", null);
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Track usage that crosses 80% threshold (790 + 20 = 810 tokens = 81%)
        var result = await _tokenTrackingService!.TrackTokenUsageAsync(
            TestUserId1,
            tokensUsed: 20,
            usageType: "prompt",
            requestIdentifier: null,
            TestCancellationToken
        );

        // Assert - Should allow usage but return warning
        result.Should().NotBeNull();
        result.IsWithinLimit.Should().BeTrue(); // Usage allowed
        result.WarningThresholdExceeded.Should().BeTrue(); // Warning issued
        result.CurrentUsage.Should().Be(810);
        result.MonthlyLimit.Should().Be(1000);
        result.UsagePercentage.Should().BeApproximately(81m, 0.1m);
    }

    [Fact]
    public async Task TokenEnforcement_At100Percent_ShouldBlockUsage()
    {
        // Arrange - Create tier with 500 monthly tokens
        var tier = TokenTier.Create(
            TestTierId,
            "Limited Tier",
            TierType.Free,
            monthlyTokenLimit: 500,
            pricePerMonth: 0m
        );
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Create user usage at 100% (500 tokens)
        var usage = UserTokenUsage.Create(TestUserId2, TestTierId, currentPeriodStart: DateTime.UtcNow.AddDays(-10));
        usage.RecordTokenUsage(500, "prompt", null);
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Attempt to use more tokens
        var result = await _tokenTrackingService!.TrackTokenUsageAsync(
            TestUserId2,
            tokensUsed: 10,
            usageType: "prompt",
            requestIdentifier: null,
            TestCancellationToken
        );

        // Assert - Should block usage
        result.Should().NotBeNull();
        result.IsWithinLimit.Should().BeFalse(); // Usage blocked
        result.WarningThresholdExceeded.Should().BeTrue();
        result.CurrentUsage.Should().Be(500); // No additional tokens recorded
        result.MonthlyLimit.Should().Be(500);
        result.UsagePercentage.Should().Be(100m);
    }

    [Fact]
    public async Task TokenReset_WhenPeriodExpires_ShouldResetUsageToZero()
    {
        // Arrange - Create tier
        var tier = TokenTier.Create(
            TestTierId,
            "Monthly Reset Tier",
            TierType.Premium,
            monthlyTokenLimit: 10000,
            pricePerMonth: 50m
        );
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Create user usage with OLD period (35 days ago, expired after 30 days)
        var oldPeriodStart = DateTime.UtcNow.AddDays(-35);
        var usage = UserTokenUsage.Create(TestUserId3, TestTierId, currentPeriodStart: oldPeriodStart);
        usage.RecordTokenUsage(9000, "prompt", null); // Was at 90% in old period
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Track new usage (should trigger period reset)
        var result = await _tokenTrackingService!.TrackTokenUsageAsync(
            TestUserId3,
            tokensUsed: 100,
            usageType: "prompt",
            requestIdentifier: null,
            TestCancellationToken
        );

        // Assert - Should have reset to new period
        result.Should().NotBeNull();
        result.IsWithinLimit.Should().BeTrue();
        result.WarningThresholdExceeded.Should().BeFalse(); // No warning (only 100 tokens in new period)
        result.CurrentUsage.Should().Be(100); // Reset + new usage
        result.MonthlyLimit.Should().Be(10000);
        result.UsagePercentage.Should().BeApproximately(1m, 0.1m);

        // Verify database updated with new period
        var updatedUsage = await _usageRepository.GetByUserIdAsync(TestUserId3, TestCancellationToken);
        updatedUsage.Should().NotBeNull();
        updatedUsage!.CurrentPeriodStart.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedUsage.CurrentPeriodStart.Should().BeAfter(oldPeriodStart);
    }

    [Fact]
    public async Task TokenEnforcement_MultipleRequests_ShouldAccumulateCorrectly()
    {
        // Arrange - Create tier
        var tier = TokenTier.Create(
            TestTierId,
            "Accumulation Tier",
            TierType.Professional,
            monthlyTokenLimit: 1000,
            pricePerMonth: 25m
        );
        await _tierRepository!.AddAsync(tier, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var usage = UserTokenUsage.Create(Guid.NewGuid(), TestTierId, currentPeriodStart: DateTime.UtcNow);
        await _usageRepository!.AddAsync(usage, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple token usage requests
        var userId = usage.UserId;
        await _tokenTrackingService!.TrackTokenUsageAsync(userId, 200, "prompt", null, TestCancellationToken); // 20%
        await _tokenTrackingService.TrackTokenUsageAsync(userId, 300, "completion", null, TestCancellationToken); // 50%
        await _tokenTrackingService.TrackTokenUsageAsync(userId, 400, "prompt", null, TestCancellationToken); // 90%

        // Assert - Should accumulate to 90%
        var finalUsage = await _usageRepository.GetByUserIdAsync(userId, TestCancellationToken);
        finalUsage.Should().NotBeNull();
        finalUsage!.TokensUsedThisPeriod.Should().Be(900);

        // Verify cached result matches database
        var cachedResult = await _tokenTrackingService.GetCurrentUsageAsync(userId, TestCancellationToken);
        cachedResult.Should().NotBeNull();
        cachedResult.CurrentUsage.Should().Be(900);
        cachedResult.MonthlyLimit.Should().Be(1000);
        cachedResult.UsagePercentage.Should().BeApproximately(90m, 0.1m);
        cachedResult.WarningThresholdExceeded.Should().BeTrue(); // 90% > 80%
        cachedResult.IsWithinLimit.Should().BeTrue(); // 90% < 100%
    }
}
