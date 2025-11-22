using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

public class PdfUploadQuotaServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly PdfUploadQuotaService _service;

    public PdfUploadQuotaServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _configServiceMock = new Mock<IConfigurationService>();
        _timeProvider = new FakeTimeProvider();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _service = new PdfUploadQuotaService(
            _redisMock.Object,
            _configServiceMock.Object,
            NullLogger<PdfUploadQuotaService>.Instance,
            _timeProvider);
    }

    #region CheckQuotaAsync Tests

    [Fact]
    public async Task CheckQuotaAsync_AdminUser_BypassesQuotaCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.Admin;

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal(0, result.DailyUploadsUsed);
        Assert.Equal(int.MaxValue, result.DailyLimit);
        Assert.Equal(0, result.WeeklyUploadsUsed);
        Assert.Equal(int.MaxValue, result.WeeklyLimit);
        Assert.Equal(DateTime.MaxValue, result.DailyResetAt);
        Assert.Equal(DateTime.MaxValue, result.WeeklyResetAt);

        // Verify Redis was never called
        _databaseMock.Verify(
            db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQuotaAsync_EditorUser_BypassesQuotaCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = Role.Editor;

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal(int.MaxValue, result.DailyLimit);
        Assert.Equal(int.MaxValue, result.WeeklyLimit);

        // Verify Redis was never called
        _databaseMock.Verify(
            db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQuotaAsync_DailyLimitReached_DeniesAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config: Free tier = 5 daily, 20 weekly
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:free:DailyLimit", It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null); // Use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:free:WeeklyLimit", It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: daily = 5 (limit reached), weekly = 10
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("5"));

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("weekly")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("10"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.False(result.Allowed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Daily upload limit reached", result.ErrorMessage);
        Assert.Contains("5 PDF/day", result.ErrorMessage);
        Assert.Contains("free tier", result.ErrorMessage);
        Assert.Equal(5, result.DailyUploadsUsed);
        Assert.Equal(5, result.DailyLimit);
        Assert.Equal(10, result.WeeklyUploadsUsed);
        Assert.Equal(20, result.WeeklyLimit);
    }

    [Fact]
    public async Task CheckQuotaAsync_WeeklyLimitReached_DeniesAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: daily = 3, weekly = 20 (limit reached)
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("3"));

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("weekly")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("20"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.False(result.Allowed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Weekly upload limit reached", result.ErrorMessage);
        Assert.Contains("20 PDF/week", result.ErrorMessage);
        Assert.Contains("free tier", result.ErrorMessage);
        Assert.Equal(3, result.DailyUploadsUsed);
        Assert.Equal(20, result.WeeklyUploadsUsed);
    }

    [Fact]
    public async Task CheckQuotaAsync_WithinLimits_AllowsAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = Role.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults (Normal: 20 daily, 100 weekly)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: daily = 10, weekly = 50 (both within limits)
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("10"));

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("weekly")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("50"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.Allowed);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(10, result.DailyUploadsUsed);
        Assert.Equal(20, result.DailyLimit);
        Assert.Equal(50, result.WeeklyUploadsUsed);
        Assert.Equal(100, result.WeeklyLimit);
        Assert.Equal(new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Utc), result.DailyResetAt);
        // Weekly reset should be next Monday (Nov 24, 2025 - Saturday + 2 days)
        Assert.Equal(new DateTime(2025, 11, 24, 0, 0, 0, DateTimeKind.Utc), result.WeeklyResetAt);
    }

    [Fact]
    public async Task CheckQuotaAsync_RedisFailure_FailsOpen()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = Role.User;

        // Setup Redis to throw exception
        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert - Fail-open: allow access when Redis is down
        Assert.True(result.Allowed);
        Assert.Equal(0, result.DailyUploadsUsed);
        Assert.Equal(int.MaxValue, result.DailyLimit);
        Assert.Equal(0, result.WeeklyUploadsUsed);
        Assert.Equal(int.MaxValue, result.WeeklyLimit);
    }

    [Fact]
    public async Task CheckQuotaAsync_CustomConfiguredLimits_UsesConfigValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = Role.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup custom config: Premium = 200 daily, 1000 weekly (overriding defaults)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:premium:DailyLimit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(200);
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:premium:WeeklyLimit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000);

        // Setup Redis: daily = 150, weekly = 800
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("150"));

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("weekly")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("800"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert - Should use custom limits, not defaults
        Assert.True(result.Allowed);
        Assert.Equal(150, result.DailyUploadsUsed);
        Assert.Equal(200, result.DailyLimit); // Custom limit
        Assert.Equal(800, result.WeeklyUploadsUsed);
        Assert.Equal(1000, result.WeeklyLimit); // Custom limit
    }

    #endregion

    #region IncrementUploadCountAsync Tests

    [Fact]
    public async Task IncrementUploadCountAsync_ExecutesLuaScriptAtomically()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        RedisKey[]? capturedDailyKeys = null;
        RedisKey[]? capturedWeeklyKeys = null;
        RedisValue[]? capturedDailyValues = null;
        RedisValue[]? capturedWeeklyValues = null;

        var callCount = 0;
        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[]?, RedisValue[]?, CommandFlags>((script, keys, values, flags) =>
            {
                if (callCount == 0)
                {
                    capturedDailyKeys = keys;
                    capturedDailyValues = values;
                }
                else
                {
                    capturedWeeklyKeys = keys;
                    capturedWeeklyValues = values;
                }
                callCount++;
            })
            .ReturnsAsync(RedisResult.Create(new RedisValue(1)));

        // Act
        await _service.IncrementUploadCountAsync(userId);

        // Assert - Lua script called twice (daily + weekly)
        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains("INCR") && s.Contains("EXPIRE")),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Exactly(2));

        // Verify daily key format: pdf:upload:daily:{userId}:2025-11-22
        Assert.NotNull(capturedDailyKeys);
        Assert.Single(capturedDailyKeys);
        Assert.Contains("pdf:upload:daily", capturedDailyKeys[0].ToString());
        Assert.Contains(userId.ToString(), capturedDailyKeys[0].ToString());
        Assert.Contains("2025-11-22", capturedDailyKeys[0].ToString());

        // Verify weekly key format: pdf:upload:weekly:{userId}:2025-W47
        Assert.NotNull(capturedWeeklyKeys);
        Assert.Single(capturedWeeklyKeys);
        Assert.Contains("pdf:upload:weekly", capturedWeeklyKeys[0].ToString());
        Assert.Contains(userId.ToString(), capturedWeeklyKeys[0].ToString());
        Assert.Contains("2025-W47", capturedWeeklyKeys[0].ToString());

        // Verify TTL values (25 hours = 90000 seconds for daily)
        Assert.NotNull(capturedDailyValues);
        Assert.Single(capturedDailyValues);
        Assert.Equal(90000, (int)capturedDailyValues[0]); // 25 * 3600

        // Verify TTL values (8 days = 691200 seconds for weekly)
        Assert.NotNull(capturedWeeklyValues);
        Assert.Single(capturedWeeklyValues);
        Assert.Equal(691200, (int)capturedWeeklyValues[0]); // 8 * 24 * 3600
    }

    [Fact]
    public async Task IncrementUploadCountAsync_RedisFailure_HandlesGracefully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act - Should not throw
        await _service.IncrementUploadCountAsync(userId);

        // Assert - Method completes without exception (logs warning internally)
        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Exactly(2)); // Attempted both daily and weekly
    }

    #endregion

    #region GetQuotaInfoAsync Tests

    [Fact]
    public async Task GetQuotaInfoAsync_AdminUser_ReturnsUnlimitedQuota()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.Admin;

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        Assert.True(info.IsUnlimited);
        Assert.Equal(0, info.DailyUploadsUsed);
        Assert.Equal(int.MaxValue, info.DailyLimit);
        Assert.Equal(int.MaxValue, info.DailyRemaining);
        Assert.Equal(0, info.WeeklyUploadsUsed);
        Assert.Equal(int.MaxValue, info.WeeklyLimit);
        Assert.Equal(int.MaxValue, info.WeeklyRemaining);
        Assert.Equal(DateTime.MaxValue, info.DailyResetAt);
        Assert.Equal(DateTime.MaxValue, info.WeeklyResetAt);

        // Verify Redis was never called
        _databaseMock.Verify(
            db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task GetQuotaInfoAsync_EditorUser_ReturnsUnlimitedQuota()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = Role.Editor;

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        Assert.True(info.IsUnlimited);
        Assert.Equal(int.MaxValue, info.DailyLimit);
        Assert.Equal(int.MaxValue, info.WeeklyLimit);
    }

    [Fact]
    public async Task GetQuotaInfoAsync_RegularUser_ReturnsQuotaInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = Role.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults (Normal: 20 daily, 100 weekly)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: daily = 12, weekly = 45
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("12"));

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("weekly")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("45"));

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        Assert.False(info.IsUnlimited);
        Assert.Equal(12, info.DailyUploadsUsed);
        Assert.Equal(20, info.DailyLimit);
        Assert.Equal(8, info.DailyRemaining); // 20 - 12
        Assert.Equal(45, info.WeeklyUploadsUsed);
        Assert.Equal(100, info.WeeklyLimit);
        Assert.Equal(55, info.WeeklyRemaining); // 100 - 45
        Assert.Equal(new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Utc), info.DailyResetAt);
        Assert.Equal(new DateTime(2025, 11, 24, 0, 0, 0, DateTimeKind.Utc), info.WeeklyResetAt);
    }

    [Fact]
    public async Task GetQuotaInfoAsync_UsageExceedsLimit_RemainingIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults (Free: 5 daily, 20 weekly)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: daily = 7 (exceeds limit), weekly = 22 (exceeds limit)
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("7"));

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("weekly")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("22"));

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        Assert.Equal(7, info.DailyUploadsUsed);
        Assert.Equal(5, info.DailyLimit);
        Assert.Equal(0, info.DailyRemaining); // Math.Max(0, 5 - 7) = 0
        Assert.Equal(22, info.WeeklyUploadsUsed);
        Assert.Equal(20, info.WeeklyLimit);
        Assert.Equal(0, info.WeeklyRemaining); // Math.Max(0, 20 - 22) = 0
    }

    [Fact]
    public async Task GetQuotaInfoAsync_RedisFailure_Throws()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = Role.User;

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act & Assert - Should throw (unlike CheckQuotaAsync which fails open)
        await Assert.ThrowsAsync<RedisConnectionException>(() =>
            _service.GetQuotaInfoAsync(userId, userTier, userRole));
    }

    #endregion

    #region Week Key Edge Cases Tests

    [Theory]
    [InlineData("2024-12-29", "2024-W52")] // Sunday Dec 29, 2024 - Week 52 of 2024
    [InlineData("2024-12-30", "2025-W01")] // Monday Dec 30, 2024 - Week 1 of 2025 (ISO 8601)
    [InlineData("2024-12-31", "2025-W01")] // Tuesday Dec 31, 2024 - Week 1 of 2025
    [InlineData("2025-01-01", "2025-W01")] // Wednesday Jan 1, 2025 - Week 1 of 2025
    [InlineData("2025-01-02", "2025-W01")] // Thursday Jan 2, 2025 - Week 1 of 2025
    [InlineData("2025-01-03", "2025-W01")] // Friday Jan 3, 2025 - Week 1 of 2025
    [InlineData("2025-12-29", "2026-W01")] // Monday Dec 29, 2025 - Week 1 of 2026 (ISO 8601)
    [InlineData("2025-12-30", "2026-W01")] // Tuesday Dec 30, 2025 - Week 1 of 2026
    [InlineData("2025-12-31", "2026-W01")] // Wednesday Dec 31, 2025 - Week 1 of 2026
    [InlineData("2026-01-01", "2026-W01")] // Thursday Jan 1, 2026 - Week 1 of 2026
    [InlineData("2026-01-02", "2026-W01")] // Friday Jan 2, 2026 - Week 1 of 2026
    [InlineData("2026-01-03", "2026-W01")] // Saturday Jan 3, 2026 - Week 1 of 2026
    [InlineData("2026-01-04", "2026-W01")] // Sunday Jan 4, 2026 - Week 1 of 2026
    [InlineData("2027-01-01", "2026-W53")] // Friday Jan 1, 2027 - Week 53 of 2026 (edge case)
    [InlineData("2027-01-02", "2026-W53")] // Saturday Jan 2, 2027 - Week 53 of 2026
    [InlineData("2027-01-03", "2026-W53")] // Sunday Jan 3, 2027 - Week 53 of 2026
    [InlineData("2027-01-04", "2027-W01")] // Monday Jan 4, 2027 - Week 1 of 2027
    public async Task IncrementUploadCountAsync_ISO8601WeekEdgeCases_GeneratesCorrectWeekKey(
        string dateString,
        string expectedWeekKey)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testDate = DateTime.Parse(dateString + " 10:00:00", null, System.Globalization.DateTimeStyles.AssumeUniversal);
        _timeProvider.SetUtcNow(testDate);

        RedisKey? capturedWeeklyKey = null;

        var callCount = 0;
        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[]?, RedisValue[]?, CommandFlags>((script, keys, values, flags) =>
            {
                if (callCount == 1 && keys != null) // Second call is weekly
                {
                    capturedWeeklyKey = keys[0];
                }
                callCount++;
            })
            .ReturnsAsync(RedisResult.Create(new RedisValue(1)));

        // Act
        await _service.IncrementUploadCountAsync(userId);

        // Assert
        Assert.NotNull(capturedWeeklyKey);
        var weeklyKeyString = capturedWeeklyKey.ToString();
        Assert.Contains(expectedWeekKey, weeklyKeyString);
    }

    #endregion

    #region Daily/Weekly Reset Tests

    [Fact]
    public async Task CheckQuotaAsync_DailyReset_CalculatesNextMidnight()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        // Test at 10:30 PM on Nov 22
        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 22, 30, 0, DateTimeKind.Utc));

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("0"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert - Daily reset should be next midnight (Nov 23 00:00)
        Assert.Equal(new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Utc), result.DailyResetAt);
    }

    [Theory]
    [InlineData("2025-11-22", "2025-11-24")] // Saturday → next Monday
    [InlineData("2025-11-23", "2025-11-24")] // Sunday → next Monday
    [InlineData("2025-11-24", "2025-12-01")] // Monday → next Monday (7 days)
    [InlineData("2025-11-25", "2025-12-01")] // Tuesday → next Monday
    [InlineData("2025-11-26", "2025-12-01")] // Wednesday → next Monday
    public async Task CheckQuotaAsync_WeeklyReset_CalculatesNextMonday(
        string currentDateString,
        string expectedResetDateString)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        var currentDate = DateTime.Parse(currentDateString + " 10:00:00", null, System.Globalization.DateTimeStyles.AssumeUniversal);
        var expectedReset = DateTime.Parse(expectedResetDateString + " 00:00:00", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        _timeProvider.SetUtcNow(currentDate);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("0"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.Equal(expectedReset, result.WeeklyResetAt);
    }

    #endregion

    #region Helper Classes

    private class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public void SetUtcNow(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    #endregion
}
