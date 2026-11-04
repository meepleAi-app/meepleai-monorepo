using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Services;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Shared.Services;

/// <summary>
/// Tests for TierEnforcementService.
/// D3: Game Night Flow - tier enforcement with Redis atomic counters.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TierEnforcementServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDb;
    private readonly Mock<ILogger<TierEnforcementService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly MeepleAiDbContext _dbContext;
    private readonly TierEnforcementService _sut;

    public TierEnforcementServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);
        _mockLogger = new Mock<ILogger<TierEnforcementService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // In-memory EF context with required dependencies
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TierEnforcement_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new TierEnforcementService(
            _mockRedis.Object,
            _dbContext,
            _memoryCache,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _dbContext.Dispose();
    }

    #region GetLimitsAsync Tests

    [Fact]
    public async Task GetLimitsAsync_AdminUser_ReturnsUnlimited()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.Unlimited);
    }

    [Fact]
    public async Task GetLimitsAsync_SuperAdminUser_ReturnsUnlimited()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "superadmin", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.Unlimited);
    }

    [Fact]
    public async Task GetLimitsAsync_EditorUser_ReturnsUnlimited()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "editor", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.Unlimited);
    }

    [Fact]
    public async Task GetLimitsAsync_FreeUser_ReturnsFreeTierLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.FreeTier);
    }

    [Fact]
    public async Task GetLimitsAsync_ContributorUser_ReturnsPremiumLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free", isContributor: true);
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.PremiumTier);
    }

    [Fact]
    public async Task GetLimitsAsync_PremiumUser_ReturnsPremiumLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.PremiumTier);
    }

    [Fact]
    public async Task GetLimitsAsync_UnknownTier_ReturnsFreeTierFallback()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "unknown");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert — falls back to FreeTier when definition not found
        limits.Should().Be(TierLimits.FreeTier);
    }

    #endregion

    #region CanPerformAsync Tests

    [Fact]
    public async Task CanPerformAsync_AgentQuery_UnderLimit_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Redis returns 5 (under FreeTier limit of 20)
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5");

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.AgentQuery);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanPerformAsync_AgentQuery_AtLimit_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Redis returns 20 (at FreeTier limit of 20)
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"20");

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.AgentQuery);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanPerformAsync_SaveSession_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier); // SessionSaveEnabled = false

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanPerformAsync_SaveSession_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier); // SessionSaveEnabled = true

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanPerformAsync_AdminUser_AlwaysReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.AgentQuery);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CheckUsageAsync Tests (Issue #4168, 5g-b)

    [Fact]
    public async Task CheckUsageAsync_UnderTheLimit_ReportsTheCountAndIsNotExceeded()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5");

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.Should().Be(new TierUsageCheck(Current: 5, Max: 20, CounterReadable: true));
        usage.Exceeded.Should().BeFalse();
    }

    [Fact]
    public async Task CheckUsageAsync_AtTheLimit_IsExceeded()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"20");

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.Should().Be(new TierUsageCheck(Current: 20, Max: 20, CounterReadable: true));
        usage.Exceeded.Should().BeTrue();
    }

    /// <summary>
    /// A counter that was never written is a count of zero, and it was read: Redis answered.
    /// </summary>
    [Fact]
    public async Task CheckUsageAsync_WhenNothingWasCountedYet_IsZeroAndReadable()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.Should().Be(new TierUsageCheck(Current: 0, Max: 20, CounterReadable: true));
    }

    /// <summary>
    /// Redis down is not "zero used": the caller must be able to tell, to warn about it.
    /// CanPerformAsync cannot say so — it reads the failure as a zero.
    /// </summary>
    [Fact]
    public async Task CheckUsageAsync_WhenRedisDoesNotAnswer_SaysTheCounterWasNotRead_AndIsNotExceeded()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.Should().Be(new TierUsageCheck(Current: 0, Max: 20, CounterReadable: false));
        usage.Exceeded.Should().BeFalse();
    }

    /// <summary>
    /// «pro» è un alias di «premium» (UserTier) e nessun piano porta quel nome. Prima di
    /// #4168 la quota di domande non era applicata e non si vedeva; da lì, un utente pro avrebbe
    /// avuto le domande del piano gratuito.
    /// </summary>
    [Fact]
    public async Task CheckUsageAsync_AProUser_GetsThePremiumPlan()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "pro");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        await SeedTierDefinition("premium", TierLimits.PremiumTier);
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"25");

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.Should().Be(new TierUsageCheck(Current: 25, Max: TierLimits.PremiumTier.MaxAgentQueriesPerDay, CounterReadable: true));
        usage.Exceeded.Should().BeFalse();
    }

    /// <summary>
    /// Un piano che non esiste fra le definizioni dà i limiti del gratuito, come prima: ma non più
    /// in silenzio, perché con la quota di domande quel ripiego ferma l'utente a 20 al giorno.
    /// </summary>
    [Fact]
    public async Task CheckUsageAsync_AUserOnAPlanThatDoesNotExist_GetsTheFreeLimits_AndItIsLogged()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "enterprise");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.Max.Should().Be(TierLimits.FreeTier.MaxAgentQueriesPerDay);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("enterprise", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckUsageAsync_AnAdmin_IsUnlimited_AndTheCounterIsNotRead()
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        var usage = await _sut.CheckUsageAsync(userId, TierAction.AgentQuery);

        usage.IsUnlimited.Should().BeTrue();
        usage.Exceeded.Should().BeFalse();
        _mockDb.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    /// <summary>
    /// The window is the UTC day. The key the check reads is the key the record writes, and both
    /// change at UTC midnight — not at the midnight of the server or of the user.
    /// </summary>
    // Il fuso «locale» dell'orologio finto è lontano da UTC nei due sensi: un uso dell'ora locale
    // del provider (GetLocalNow) cambierebbe giorno almeno in un caso, su qualunque macchina.
    // Non copre DateTimeOffset.LocalDateTime, che segue il fuso del processo: su un runner in UTC
    // quella regressione non si vede.
    [Theory]
    [InlineData("2026-01-15T23:59:59Z", "2026-01-15", 14)]
    [InlineData("2026-01-16T00:00:00Z", "2026-01-16", -10)]
    public async Task CheckUsageAsync_ReadsTheCounterOfTheUtcDay_TheOneRecordUsageWrites(string now, string day, int localOffsetHours)
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
            DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture));
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone(
            "far-from-utc", TimeSpan.FromHours(localOffsetHours), "far-from-utc", "far-from-utc"));
        var sut = new TierEnforcementService(_mockRedis.Object, _dbContext, _memoryCache, _mockLogger.Object, time);
        var read = new List<string>();
        var written = new List<string>();
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => read.Add(key.ToString()))
            .ReturnsAsync((RedisValue)"1");
        _mockDb.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, long, CommandFlags>((key, _, _) => written.Add(key.ToString()))
            .ReturnsAsync(2);

        await sut.CheckUsageAsync(userId, TierAction.AgentQuery);
        await sut.RecordUsageAsync(userId, TierAction.AgentQuery);

        read.Should().Equal($"tier:usage:{userId}:AgentQuery:{day}");
        written.Should().Equal(read);
    }

    #endregion

    #region RecordUsageAsync Tests

    [Fact]
    public async Task RecordUsageAsync_AgentQuery_IncrementsRedisCounter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedKeyPattern = $"tier:usage:{userId}:AgentQuery:";

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith(expectedKeyPattern)),
                1,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.AgentQuery);

        // Assert
        _mockDb.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().StartsWith(expectedKeyPattern)),
            1,
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordUsageAsync_UploadPdf_UsesMonthlyBucket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var monthBucket = DateTime.UtcNow.ToString("yyyy-MM");
        var expectedKey = $"tier:usage:{userId}:UploadPdf:{monthBucket}";

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.UploadPdf);

        // Assert
        _mockDb.Verify(d => d.StringIncrementAsync(
            (RedisKey)expectedKey, 1, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordUsageAsync_ProposeToSharedCatalog_UsesWeeklyBucket()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.ProposeToSharedCatalog);

        // Assert — verify key contains weekly bucket format (yyyy-Www)
        _mockDb.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().Contains(":ProposeToSharedCatalog:")),
            1,
            It.IsAny<CommandFlags>()), Times.Once);
    }

    // #4541: il bucket settimanale e' la settimana ISO 8601 (lunedi'-domenica, l'anno e' quello del
    // giovedi'). Il vecchio calcolo univa l'anno di calendario al numero di settimana: il venerdi'
    // 1/1/2027 dava 2027-W53 invece di 2026-W53, e il limite settimanale ripartiva da zero a meta'
    // settimana. Valori scritti a mano e verificati con System.Globalization.ISOWeek.
    [Theory]
    [InlineData("2026-12-28T00:00:00Z", "2026-W53")] // lunedi'
    [InlineData("2026-12-31T12:00:00Z", "2026-W53")] // giovedi'
    [InlineData("2027-01-01T12:00:00Z", "2026-W53")] // venerdi': il vecchio calcolo dava 2027-W53
    [InlineData("2027-01-03T23:59:59Z", "2026-W53")] // domenica, ultimo secondo
    [InlineData("2027-01-04T00:00:00Z", "2027-W01")] // lunedi'
    [InlineData("2024-12-30T12:00:00Z", "2025-W01")] // il vecchio calcolo dava 2024-W53
    [InlineData("2024-12-31T12:00:00Z", "2025-W01")]
    [InlineData("2025-12-29T12:00:00Z", "2026-W01")]
    [InlineData("2025-12-31T12:00:00Z", "2026-W01")]
    [InlineData("2029-12-31T12:00:00Z", "2030-W01")]
    [InlineData("2021-12-29T12:00:00Z", "2021-W52")]
    [InlineData("2022-01-01T12:00:00Z", "2021-W52")] // sabato: il vecchio calcolo dava 2022-W52
    [InlineData("2025-11-22T10:00:00Z", "2025-W47")] // settimana qualsiasi
    public async Task ProposeToSharedCatalog_WritesAndReadsTheIsoWeekBucket(string utcNow, string isoWeek)
    {
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
            DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture));
        var sut = new TierEnforcementService(_mockRedis.Object, _dbContext, _memoryCache, _mockLogger.Object, time);
        var read = new List<string>();
        var written = new List<string>();
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => read.Add(key.ToString()))
            .ReturnsAsync((RedisValue)"0");
        _mockDb.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, long, CommandFlags>((key, _, _) => written.Add(key.ToString()))
            .ReturnsAsync(2);

        await sut.CheckUsageAsync(userId, TierAction.ProposeToSharedCatalog);
        await sut.RecordUsageAsync(userId, TierAction.ProposeToSharedCatalog);

        read.Should().Equal($"tier:usage:{userId}:ProposeToSharedCatalog:{isoWeek}");
        written.Should().Equal(read);
    }

    [Fact]
    public async Task RecordUsageAsync_SetsExpiry_OnIncrement()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.AgentQuery);

        // Assert — TTL set (2 days for daily bucket)
        _mockDb.Verify(d => d.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalDays >= 1),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    #endregion

    #region GetUsageAsync Tests

    [Fact]
    public async Task GetUsageAsync_ReturnsSnapshot_WithRedisAndDbData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Redis returns usage values
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"3");

        // Act
        var snapshot = await _sut.GetUsageAsync(userId);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.PrivateGamesMax.Should().Be(TierLimits.FreeTier.MaxPrivateGames);
        snapshot.AgentQueriesTodayMax.Should().Be(TierLimits.FreeTier.MaxAgentQueriesPerDay);
        snapshot.SessionSaveEnabled.Should().Be(TierLimits.FreeTier.SessionSaveEnabled);
    }

    #endregion

    #region Helpers

    private async Task SeedUser(Guid userId, string role, string tier, bool isContributor = false)
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@test.com",
            Role = role,
            Tier = tier,
            IsContributor = isContributor,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedTierDefinition(string name, TierLimits limits)
    {
        var tier = TierDefinition.Create(name, $"{name} Tier", limits, name == "premium" ? "standard" : "free");
        _dbContext.TierDefinitions.Add(tier);
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
