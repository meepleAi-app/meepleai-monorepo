using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for PDF upload quota enforcement.
/// Tests the complete quota system: tracking, limits, tier-based quotas, admin bypass.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
/// <remarks>
/// <para><b>Redis:</b> è quello della fixture. In CI è il service container dello shard
/// (<c>TEST_REDIS_CONNSTRING</c>), condiviso con le collection che girano in parallelo; in locale
/// può essere il Redis dello stack di sviluppo (<c>make ci-be-integration-fast</c>). Per questo i
/// test non fanno mai <c>FLUSHDB</c> (#4528): cancellava le chiavi degli altri test.</para>
/// <para>Il flush non serviva: le chiavi di <see cref="PdfUploadQuotaService"/> sono per utente
/// (<c>pdf:upload:daily:{userId}:…</c>, <c>pdf:upload:weekly:{userId}:…</c>,
/// <c>pdf:quota:reservation:{userId}:…</c>, <c>pdf:upload:game:{userId}:…</c>) e ogni test crea i suoi
/// utenti con <c>Guid.NewGuid()</c>, quindi nessun test legge i contatori di un altro.
/// <c>DisposeAsync</c> cancella solo le chiavi degli utenti creati dal test: alcuni test le scrivono
/// senza TTL, e su un Redis che non muore col job resterebbero per sempre.</para>
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2031")]
public sealed class PdfUploadQuotaEnforcementIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private IPdfUploadQuotaService? _quotaService;
    private readonly List<Guid> _createdUserIds = [];

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfUploadQuotaEnforcementIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Use SharedTestcontainersFixture for both PostgreSQL AND Redis to prevent Docker hijack
        // FIX: Use unique database name to prevent conflicts in parallel execution
        _databaseName = $"test_quota_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Se da qui in poi qualcosa lancia, xUnit NON chiama DisposeAsync: il database isolato, la
        // connessione Redis e il service provider resterebbero. Succede davvero: il Timeout del test
        // copre anche InitializeAsync; allo scadere xUnit passa al test successivo e qui la lettura
        // del token del test lancia (ObjectDisposedException, osservato in #4528).
        try
        {
            // Use SharedTestcontainersFixture Redis (no separate container needed!)
            var redisConnectionString = _fixture.RedisConnectionString;

            var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

            // Register Redis
            _redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(_redis);

            // Register repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();

            // Register configuration service mock
            var configServiceMock = new Mock<IConfigurationService>();
            configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()!, It.IsAny<int?>(), It.IsAny<string?>()))
                .ReturnsAsync((int?)null); // Use default limits
            services.AddSingleton<IConfigurationService>(configServiceMock.Object);

            // Register quota service
            services.AddScoped<IPdfUploadQuotaService, PdfUploadQuotaService>();

            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
            _quotaService = _serviceProvider.GetRequiredService<IPdfUploadQuotaService>();

            await _dbContext.Database.MigrateAsync(TestCancellationToken);
        }
        catch
        {
            // DisposeAsync regge lo stato parziale: ogni campo è controllato, la lista utenti è
            // vuota, e il drop del database non usa il token del test (cancellato o già smaltito).
            try
            {
                await DisposeAsync();
            }
            catch
            {
                // L'eccezione da riportare è quella dell'inizializzazione.
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // #4528: solo le chiavi degli utenti creati da questo test, mai l'intero database Redis.
        foreach (var userId in _createdUserIds)
        {
            try
            {
                await _fixture.FlushRedisByPrefixAsync($"pdf:*:{userId}:*");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        if (_dbContext != null)
            await _dbContext.DisposeAsync();
        if (_redis != null)
            await _redis.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for PostgreSQL cleanup
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

        // Issue #2031: No separate Redis container to dispose (using SharedTestcontainersFixture)
    }
    private async Task<User> CreateUserAsync(UserTier tier, AuthRole? role = null)
    {
        var userRepo = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var user = new User(
            id: Guid.NewGuid(),
            email: Email.Parse($"user-{Guid.NewGuid()}@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("UniqueT3stPwd!"),
            role: role ?? AuthRole.User,
            tier: tier);

        await userRepo.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _createdUserIds.Add(user.Id);

        return user;
    }

    private async Task<PdfUploadQuotaInfo> GetQuotaInfoAsync(Guid userId, UserTier tier, AuthRole role)
    {
        return await _quotaService!.GetQuotaInfoAsync(userId, tier, role, TestCancellationToken);
    }

    private async Task IncrementUploadAsync(Guid userId)
    {
        await _quotaService!.IncrementUploadCountAsync(userId, TestCancellationToken);
    }

    private static string GetWeekKey(DateTime date)
    {
        // ISO 8601 week: yyyy-Www (e.g., 2025-W47)
        // Same logic as PdfUploadQuotaService.GetWeekKey
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var weekRule = System.Globalization.CalendarWeekRule.FirstFourDayWeek;
        var firstDayOfWeek = DayOfWeek.Monday;

        var year = date.Year;
        var week = calendar.GetWeekOfYear(date, weekRule, firstDayOfWeek);

        // Handle ISO 8601 year transitions
        // Jan 1-3 might be in week 52/53 of previous year
        if (week >= 52 && date.Month == 1)
        {
            year--;
        }
        // Dec 29-31 might be in week 1 of next year
        else if (week == 1 && date.Month == 12)
        {
            year++;
        }

        return $"{year}-W{week:D2}";
    }
    [Fact(Timeout = 90_000)]
    public async Task FreeTier_FiveUploadsInDay_SixthUploadDenied()
    {
        // Arrange - Create free tier user
        var user = await CreateUserAsync(UserTier.Free);

        // Act - Upload 5 PDFs (daily limit)
        for (int i = 0; i < 5; i++)
        {
            var quotaCheck = await _quotaService!.CheckQuotaAsync(
                user.Id, user.Tier, user.Role, TestCancellationToken);

            quotaCheck.Allowed.Should().BeTrue($"Upload {i + 1} should be allowed");

            await _quotaService.IncrementUploadCountAsync(user.Id, TestCancellationToken);
        }

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.DailyUploadsUsed.Should().Be(5);
        info.DailyLimit.Should().Be(5);
        (info.DailyLimit - info.DailyUploadsUsed).Should().Be(0); // DailyRemaining computed

        // Act - Attempt 6th upload (should be denied)
        var deniedCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert
        deniedCheck.Allowed.Should().BeFalse();
        deniedCheck.ErrorMessage.ShouldIndicateDailyLimitReached();
        deniedCheck.ErrorMessage.ShouldIndicateFreeTierDailyLimit();
        deniedCheck.ErrorMessage.ShouldIndicateFreeTier();
    }

    [Fact(Timeout = 90_000)]
    public async Task FreeTier_TwentyUploadsInWeek_TwentyFirstUploadDenied()
    {
        // Arrange - Create free tier user
        var user = await CreateUserAsync(UserTier.Free);

        // Act - Upload 20 PDFs (weekly limit)
        for (int i = 0; i < 20; i++)
        {
            await IncrementUploadAsync(user.Id);
        }

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.WeeklyUploadsUsed.Should().Be(20);
        info.WeeklyLimit.Should().Be(20);
        (info.WeeklyLimit - info.WeeklyUploadsUsed).Should().Be(0); // WeeklyRemaining computed

        // Act - Attempt 21st upload (should be denied)
        var deniedCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert
        deniedCheck.Allowed.Should().BeFalse();
        deniedCheck.ErrorMessage.ShouldIndicateQuotaLimitReached();
        deniedCheck.ErrorMessage.ShouldIndicateFreeTierLimit();
        deniedCheck.ErrorMessage.ShouldIndicateFreeTier();
    }
    [Fact(Timeout = 90_000)]
    public async Task NormalTier_TwentyUploadsInDay_AllAllowed()
    {
        // Arrange - Create normal tier user
        var user = await CreateUserAsync(UserTier.Normal);

        // Act - Upload 20 PDFs (daily limit)
        for (int i = 0; i < 20; i++)
        {
            var quotaCheck = await _quotaService!.CheckQuotaAsync(
                user.Id, user.Tier, user.Role, TestCancellationToken);

            quotaCheck.Allowed.Should().BeTrue($"Upload {i + 1} should be allowed");

            await IncrementUploadAsync(user.Id);
        }

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.DailyUploadsUsed.Should().Be(20);
        info.DailyLimit.Should().Be(20);
        (info.DailyLimit - info.DailyUploadsUsed).Should().Be(0); // DailyRemaining computed

        // Act - Attempt 21st upload (should be denied)
        var deniedCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert
        deniedCheck.Allowed.Should().BeFalse();
        deniedCheck.ErrorMessage.ShouldIndicateDailyLimitReached();
        deniedCheck.ErrorMessage.ShouldIndicateNormalTierDailyLimit();
        deniedCheck.ErrorMessage.ShouldIndicateNormalTier();
    }

    [Fact(Timeout = 90_000)]
    public async Task NormalTier_HundredUploadsInWeek_AllAllowed()
    {
        // Arrange - Create normal tier user
        var user = await CreateUserAsync(UserTier.Normal);

        // Act - Simulate 100 uploads (weekly limit) by setting Redis directly
        // This is much faster than actually incrementing 100 times
        var db = _redis!.GetDatabase();
        var now = TimeProvider.System.GetUtcNow().DateTime;
        var today = now.ToString("yyyy-MM-dd");
        var weekKey = $"pdf:upload:weekly:{user.Id}:{GetWeekKey(now)}";
        await db.StringSetAsync($"pdf:upload:daily:{user.Id}:{today}", 100);
        await db.StringSetAsync(weekKey, 100);

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.WeeklyUploadsUsed.Should().Be(100);
        info.WeeklyLimit.Should().Be(100);
        (info.WeeklyLimit - info.WeeklyUploadsUsed).Should().Be(0); // WeeklyRemaining computed

        // Act - Attempt 101st upload (should be denied)
        var deniedCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert
        deniedCheck.Allowed.Should().BeFalse();
        deniedCheck.ErrorMessage.ShouldIndicateQuotaLimitReached();
        deniedCheck.ErrorMessage.ShouldIndicateNormalTierLimit();
        deniedCheck.ErrorMessage.ShouldIndicateNormalTier();
    }
    [Fact(Timeout = 90_000)]
    public async Task PremiumTier_HundredUploadsInDay_AllAllowed()
    {
        // Arrange - Create premium tier user
        var user = await CreateUserAsync(UserTier.Premium);

        // Act - Simulate 100 uploads (daily limit) by setting Redis directly
        // This is much faster than actually incrementing 100 times
        var db = _redis!.GetDatabase();
        var now = TimeProvider.System.GetUtcNow().DateTime;
        var today = now.ToString("yyyy-MM-dd");
        var weekKey = $"pdf:upload:weekly:{user.Id}:{GetWeekKey(now)}";
        await db.StringSetAsync($"pdf:upload:daily:{user.Id}:{today}", 100);
        await db.StringSetAsync(weekKey, 100);

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.DailyUploadsUsed.Should().Be(100);
        info.DailyLimit.Should().Be(100);
        (info.DailyLimit - info.DailyUploadsUsed).Should().Be(0); // DailyRemaining computed

        // Act - Attempt 101st upload (should be denied)
        var deniedCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert
        deniedCheck.Allowed.Should().BeFalse();
        deniedCheck.ErrorMessage.ShouldIndicateDailyLimitReached();
        deniedCheck.ErrorMessage.ShouldIndicatePremiumTierDailyLimit();
    }
    [Fact(Timeout = 90_000)]
    public async Task AdminUser_UnlimitedUploads_NoQuotaCheck()
    {
        // Arrange - Create admin user (tier doesn't matter for admin)
        var user = await CreateUserAsync(UserTier.Free, AuthRole.Admin);

        // Act - Simulate high usage by setting Redis directly (1000 uploads)
        // This is much faster than actually incrementing 1000 times
        var db = _redis!.GetDatabase();
        var now = TimeProvider.System.GetUtcNow().DateTime;
        var today = now.ToString("yyyy-MM-dd");
        var weekKey = $"pdf:upload:weekly:{user.Id}:{GetWeekKey(now)}";
        await db.StringSetAsync($"pdf:upload:daily:{user.Id}:{today}", 1000);
        await db.StringSetAsync(weekKey, 1000);

        // Verify quota check - admin always has unlimited quota (even with 1000 uploads)
        var quotaCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert - Admin bypasses quota completely
        quotaCheck.Allowed.Should().BeTrue();
        quotaCheck.DailyLimit.Should().Be(int.MaxValue);
        quotaCheck.WeeklyLimit.Should().Be(int.MaxValue);

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.IsUnlimited.Should().BeTrue();
        (info.DailyLimit - info.DailyUploadsUsed).Should().Be(int.MaxValue); // DailyRemaining computed
        (info.WeeklyLimit - info.WeeklyUploadsUsed).Should().Be(int.MaxValue); // WeeklyRemaining computed
    }

    [Fact(Timeout = 90_000)]
    public async Task EditorUser_UnlimitedUploads_NoQuotaCheck()
    {
        // Arrange - Create editor user
        var user = await CreateUserAsync(UserTier.Normal, AuthRole.Editor);

        // Act - Simulate high usage by setting Redis directly (500 uploads, beyond normal tier limits)
        // This is much faster than actually incrementing 500 times
        var db = _redis!.GetDatabase();
        var now = TimeProvider.System.GetUtcNow().DateTime;
        var today = now.ToString("yyyy-MM-dd");
        var weekKey = $"pdf:upload:weekly:{user.Id}:{GetWeekKey(now)}";
        await db.StringSetAsync($"pdf:upload:daily:{user.Id}:{today}", 500);
        await db.StringSetAsync(weekKey, 500);

        // Verify quota check - editor always has unlimited quota (even with 500 uploads)
        var quotaCheck = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        // Assert - Editor bypasses quota completely
        quotaCheck.Allowed.Should().BeTrue();
        quotaCheck.DailyLimit.Should().Be(int.MaxValue);
        quotaCheck.WeeklyLimit.Should().Be(int.MaxValue);

        // Verify quota info
        var info = await GetQuotaInfoAsync(user.Id, user.Tier, user.Role);
        info.IsUnlimited.Should().BeTrue();
    }
    [Fact(Timeout = 90_000)]
    public async Task UserUpgrade_FreeToPremium_QuotaLimitIncreases()
    {
        // Arrange - Create free tier user and upload 5 PDFs (at limit)
        var user = await CreateUserAsync(UserTier.Free);

        for (int i = 0; i < 5; i++)
        {
            await IncrementUploadAsync(user.Id);
        }

        // Verify at limit
        var quotaCheckBefore = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);
        quotaCheckBefore.Allowed.Should().BeFalse();

        // Act - Upgrade to Premium
        var userRepo = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        user.UpdateTier(UserTier.Premium, AuthRole.Admin);
        await userRepo.UpdateAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert - Now has higher limits (usage is still 5, but limit is 100)
        var quotaCheckAfter = await _quotaService!.CheckQuotaAsync(
            user.Id, UserTier.Premium, user.Role, TestCancellationToken);

        quotaCheckAfter.Allowed.Should().BeTrue();
        quotaCheckAfter.DailyUploadsUsed.Should().Be(5);
        quotaCheckAfter.DailyLimit.Should().Be(100);
        (quotaCheckAfter.DailyLimit - quotaCheckAfter.DailyUploadsUsed).Should().Be(95); // DailyRemaining computed
    }

    [Fact(Timeout = 90_000)]
    public async Task UserDowngrade_PremiumToFree_QuotaLimitDecreases()
    {
        // Arrange - Create premium tier user and upload 10 PDFs
        var user = await CreateUserAsync(UserTier.Premium);

        for (int i = 0; i < 10; i++)
        {
            await IncrementUploadAsync(user.Id);
        }

        // Verify still allowed (10 < 100)
        var quotaCheckBefore = await _quotaService!.CheckQuotaAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);
        quotaCheckBefore.Allowed.Should().BeTrue();

        // Act - Downgrade to Free
        var userRepo = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        user.UpdateTier(UserTier.Free, AuthRole.Admin);
        await userRepo.UpdateAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert - Now exceeds free tier limit (usage 10 > limit 5)
        var quotaCheckAfter = await _quotaService!.CheckQuotaAsync(
            user.Id, UserTier.Free, user.Role, TestCancellationToken);

        quotaCheckAfter.Allowed.Should().BeFalse();
        quotaCheckAfter.DailyUploadsUsed.Should().Be(10);
        quotaCheckAfter.DailyLimit.Should().Be(5);
        quotaCheckAfter.ErrorMessage.ShouldIndicateDailyLimitReached();
    }
    [Fact(Timeout = 90_000)]
    public async Task MultipleUsers_QuotaTrackedIndependently()
    {
        // Arrange - Create 3 users with different tiers
        var user1 = await CreateUserAsync(UserTier.Free);
        var user2 = await CreateUserAsync(UserTier.Normal);
        var user3 = await CreateUserAsync(UserTier.Premium);

        // Act - Upload different amounts for each user
        // User1: 3 uploads
        for (int i = 0; i < 3; i++)
            await IncrementUploadAsync(user1.Id);

        // User2: 10 uploads
        for (int i = 0; i < 10; i++)
            await IncrementUploadAsync(user2.Id);

        // User3: 50 uploads
        for (int i = 0; i < 50; i++)
            await IncrementUploadAsync(user3.Id);

        // Assert - Each user has independent quota
        var info1 = await GetQuotaInfoAsync(user1.Id, user1.Tier, user1.Role);
        info1.DailyUploadsUsed.Should().Be(3);
        (info1.DailyLimit - info1.DailyUploadsUsed).Should().Be(2); // DailyRemaining computed

        var info2 = await GetQuotaInfoAsync(user2.Id, user2.Tier, user2.Role);
        info2.DailyUploadsUsed.Should().Be(10);
        (info2.DailyLimit - info2.DailyUploadsUsed).Should().Be(10); // DailyRemaining computed

        var info3 = await GetQuotaInfoAsync(user3.Id, user3.Tier, user3.Role);
        info3.DailyUploadsUsed.Should().Be(50);
        (info3.DailyLimit - info3.DailyUploadsUsed).Should().Be(50); // DailyRemaining computed
    }
    [Fact(Timeout = 90_000)]
    public async Task QuotaTracking_PersistsInRedis_AcrossServiceInstances()
    {
        // Arrange - Create user and upload 3 PDFs with first service instance
        var user = await CreateUserAsync(UserTier.Free);

        for (int i = 0; i < 3; i++)
            await IncrementUploadAsync(user.Id);

        // Act - Create new quota service instance (simulates service restart)
        var configServiceMock = new Mock<IConfigurationService>();
        configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()!, It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        var newQuotaService = new PdfUploadQuotaService(
            _redis!,
            configServiceMock.Object,
            _serviceProvider!.GetRequiredService<ILogger<PdfUploadQuotaService>>(),
            TimeProvider.System);

        // Assert - New service instance can read quota from Redis
        var info = await newQuotaService.GetQuotaInfoAsync(
            user.Id, user.Tier, user.Role, TestCancellationToken);

        info.DailyUploadsUsed.Should().Be(3);
        (info.DailyLimit - info.DailyUploadsUsed).Should().Be(2); // DailyRemaining computed
        info.WeeklyUploadsUsed.Should().Be(3);
        (info.WeeklyLimit - info.WeeklyUploadsUsed).Should().Be(17); // WeeklyRemaining computed
    }
}
