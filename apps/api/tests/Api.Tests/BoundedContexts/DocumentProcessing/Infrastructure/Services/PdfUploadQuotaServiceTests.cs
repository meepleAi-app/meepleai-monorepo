using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;
using UserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;
using Api.Tests.Constants;
using System.Globalization;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public class PdfUploadQuotaServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly TestTimeProvider _timeProvider;
    private readonly PdfUploadQuotaService _service;

    public PdfUploadQuotaServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _configServiceMock = new Mock<IConfigurationService>();
        _timeProvider = new TestTimeProvider();

        // Setup GetDatabase() - matches both parameterless and with-parameters calls
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
        _redisMock.Setup(r => r.GetDatabase(-1, null))
            .Returns(_databaseMock.Object);

        _service = new PdfUploadQuotaService(
            _redisMock.Object,
            _configServiceMock.Object,
            NullLogger<PdfUploadQuotaService>.Instance,
            _timeProvider);
    }
    [Fact]
    public async Task CheckQuotaAsync_AdminUser_BypassesQuotaCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Admin;

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeTrue();
        result.DailyUploadsUsed.Should().Be(0);
        result.DailyLimit.Should().Be(int.MaxValue);
        result.WeeklyUploadsUsed.Should().Be(0);
        result.WeeklyLimit.Should().Be(int.MaxValue);
        result.DailyResetAt.Should().Be(DateTime.MaxValue);
        result.WeeklyResetAt.Should().Be(DateTime.MaxValue);

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
        var userRole = AuthRole.Editor;

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeTrue();
        result.DailyLimit.Should().Be(int.MaxValue);
        result.WeeklyLimit.Should().Be(int.MaxValue);

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
        var userRole = AuthRole.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config: Free tier = 5 daily, 20 weekly
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:free:DailyLimit", null, null))
            .ReturnsAsync((int?)null); // Use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:free:WeeklyLimit", null, null))
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
        result.Allowed.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("Daily upload limit reached");
        result.ErrorMessage.Should().Contain("5 PDF/day");
        result.ErrorMessage.Should().Contain("free tier");
        result.DailyUploadsUsed.Should().Be(5);
        result.DailyLimit.Should().Be(5);
        result.WeeklyUploadsUsed.Should().Be(10);
        result.WeeklyLimit.Should().Be(20);
    }

    [Fact]
    public async Task CheckQuotaAsync_WeeklyLimitReached_DeniesAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
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
        result.Allowed.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("Weekly upload limit reached");
        result.ErrorMessage.Should().Contain("20 PDF/week");
        result.ErrorMessage.Should().Contain("free tier");
        result.DailyUploadsUsed.Should().Be(3);
        result.WeeklyUploadsUsed.Should().Be(20);
    }

    [Fact]
    public async Task CheckQuotaAsync_WithinLimits_AllowsAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults (Normal: 20 daily, 100 weekly)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:normal:DailyLimit", null, null))
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
        result.Allowed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.DailyUploadsUsed.Should().Be(10);
        result.DailyLimit.Should().Be(20);
        result.WeeklyUploadsUsed.Should().Be(50);
        result.WeeklyLimit.Should().Be(100);
        result.DailyResetAt.Should().Be(new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Utc));
        // Weekly reset should be next Monday (Nov 24, 2025 - Saturday + 2 days)
        result.WeeklyResetAt.Should().Be(new DateTime(2025, 11, 24, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CheckQuotaAsync_RedisFailure_FailsOpen()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        // Setup Redis to throw exception
        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert - Fail-open: allow access when Redis is down
        result.Allowed.Should().BeTrue();
        result.DailyUploadsUsed.Should().Be(0);
        result.DailyLimit.Should().Be(int.MaxValue);
        result.WeeklyUploadsUsed.Should().Be(0);
        result.WeeklyLimit.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task CheckQuotaAsync_CustomConfiguredLimits_UsesConfigValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup custom config: Premium = 200 daily, 1000 weekly (overriding defaults)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:premium:DailyLimit", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(200);
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:premium:WeeklyLimit", It.IsAny<int?>(), It.IsAny<string?>()))
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
        result.Allowed.Should().BeTrue();
        result.DailyUploadsUsed.Should().Be(150);
        result.DailyLimit.Should().Be(200); // Custom limit
        result.WeeklyUploadsUsed.Should().Be(800);
        result.WeeklyLimit.Should().Be(1000); // Custom limit
    }
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
            .ReturnsAsync(RedisResult.Create(new RedisValue("1")));

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
        capturedDailyKeys.Should().NotBeNull();
        capturedDailyKeys.Should().ContainSingle();
        capturedDailyKeys[0].ToString().Should().Contain("pdf:upload:daily");
        capturedDailyKeys[0].ToString().Should().Contain(userId.ToString());
        capturedDailyKeys[0].ToString().Should().Contain("2025-11-22");

        // Verify weekly key format: pdf:upload:weekly:{userId}:2025-W47
        capturedWeeklyKeys.Should().NotBeNull();
        capturedWeeklyKeys.Should().ContainSingle();
        capturedWeeklyKeys[0].ToString().Should().Contain("pdf:upload:weekly");
        capturedWeeklyKeys[0].ToString().Should().Contain(userId.ToString());
        capturedWeeklyKeys[0].ToString().Should().Contain("2025-W47");

        // Verify TTL values (25 hours = 90000 seconds for daily)
        capturedDailyValues.Should().NotBeNull();
        capturedDailyValues.Should().ContainSingle();
        Assert.Equal(90000, (int)capturedDailyValues[0]); // 25 * 3600

        // Verify TTL values (8 days = 691200 seconds for weekly)
        capturedWeeklyValues.Should().NotBeNull();
        capturedWeeklyValues.Should().ContainSingle();
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
        // Note: When Redis fails on first call (daily), the catch block exits early,
        // so weekly script is never attempted
        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Once()); // Attempted only daily, then exception caught and method exited
    }
    [Fact]
    public async Task GetQuotaInfoAsync_AdminUser_ReturnsUnlimitedQuota()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Admin;

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue();
        info.DailyUploadsUsed.Should().Be(0);
        info.DailyLimit.Should().Be(int.MaxValue);
        info.DailyRemaining.Should().Be(int.MaxValue);
        info.WeeklyUploadsUsed.Should().Be(0);
        info.WeeklyLimit.Should().Be(int.MaxValue);
        info.WeeklyRemaining.Should().Be(int.MaxValue);
        info.DailyResetAt.Should().Be(DateTime.MaxValue);
        info.WeeklyResetAt.Should().Be(DateTime.MaxValue);

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
        var userRole = AuthRole.Editor;

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue();
        info.DailyLimit.Should().Be(int.MaxValue);
        info.WeeklyLimit.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task GetQuotaInfoAsync_RegularUser_ReturnsQuotaInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults (Normal: 20 daily, 100 weekly)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:normal:DailyLimit", null, null))
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
        info.IsUnlimited.Should().BeFalse();
        info.DailyUploadsUsed.Should().Be(12);
        info.DailyLimit.Should().Be(20);
        info.DailyRemaining.Should().Be(8); // 20 - 12
        info.WeeklyUploadsUsed.Should().Be(45);
        info.WeeklyLimit.Should().Be(100);
        info.WeeklyRemaining.Should().Be(55); // 100 - 45
        info.DailyResetAt.Should().Be(new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Utc));
        info.WeeklyResetAt.Should().Be(new DateTime(2025, 11, 24, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetQuotaInfoAsync_UsageExceedsLimit_RemainingIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 10, 0, 0, DateTimeKind.Utc));

        // Setup config to use defaults (Free: 5 daily, 20 weekly)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:free:DailyLimit", 10, null))
            .ReturnsAsync(10);
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:free:WeeklyLimit", 50, null))
            .ReturnsAsync(50);

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
        info.DailyUploadsUsed.Should().Be(7);
        info.DailyLimit.Should().Be(5);
        info.DailyRemaining.Should().Be(0); // Math.Max(0, 5 - 7) = 0
        info.WeeklyUploadsUsed.Should().Be(22);
        info.WeeklyLimit.Should().Be(20);
        info.WeeklyRemaining.Should().Be(0); // Math.Max(0, 20 - 22) = 0
    }

    [Fact]
    public async Task GetQuotaInfoAsync_RedisFailure_Throws()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act & Assert - Should throw (unlike CheckQuotaAsync which fails open)
        await Assert.ThrowsAsync<RedisConnectionException>(() =>
            _service.GetQuotaInfoAsync(userId, userTier, userRole));
    }
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
        var testDate = DateTime.Parse(dateString + " 10:00:00", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
        var utcTestDate = DateTime.SpecifyKind(testDate, DateTimeKind.Utc);
        _timeProvider.SetUtcNow(utcTestDate);

        RedisKey[]? capturedDailyKeys = null;
        RedisKey[]? capturedWeeklyKeys = null;

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
                }
                else
                {
                    capturedWeeklyKeys = keys;
                }
                callCount++;
            })
            .ReturnsAsync(RedisResult.Create(new RedisValue("1")));

        // Act
        await _service.IncrementUploadCountAsync(userId);

        // Assert
        // Debug: Verify GetDatabase was called
        _redisMock.Verify(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.AtLeastOnce(), "GetDatabase should be called");

        // Debug: Check if ScriptEvaluateAsync was called at all
        callCount.Should().Be(2); // Should be 2 (daily + weekly)

        capturedWeeklyKeys.Should().NotBeNull();
        capturedWeeklyKeys.Should().ContainSingle();
        var weeklyKeyString = capturedWeeklyKeys[0].ToString();

        // Debug: Show actual vs expected
        if (!weeklyKeyString.Contains(expectedWeekKey))
        {
            var actualWeek = weeklyKeyString.Split(':').LastOrDefault() ?? "UNKNOWN";
            throw new Xunit.Sdk.XunitException($"Week key mismatch for {dateString}: expected {expectedWeekKey}, got {actualWeek}");
        }

        weeklyKeyString.Should().Contain(expectedWeekKey);
    }
    [Fact]
    public async Task CheckQuotaAsync_DailyReset_CalculatesNextMidnight()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        // Test at 10:30 PM on Nov 22
        _timeProvider.SetUtcNow(new DateTime(2025, 11, 22, 22, 30, 0, DateTimeKind.Utc));

        // Setup config mocks - return null for any GetValueAsync call (will use defaults)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("0"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert - Daily reset should be next midnight (Nov 23 00:00)
        result.DailyResetAt.Should().Be(new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Utc));
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
        var userRole = AuthRole.User;

        var currentDate = DateTime.Parse(currentDateString + " 10:00:00", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        var expectedReset = DateTime.Parse(expectedResetDateString + " 00:00:00", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

        _timeProvider.SetUtcNow(DateTime.SpecifyKind(currentDate, DateTimeKind.Utc));

        // Setup config mocks - return null for any GetValueAsync call (will use defaults)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("0"));

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.WeeklyResetAt.Should().Be(expectedReset);
    }
    // FakeTimeProvider removed - now using TestTimeProvider from Api.Tests.Infrastructure

    // ============================================================================
    // Issue #3653: Per-Game Quota Tests for Private PDF Uploads
    // ============================================================================

    [Fact]
    public async Task CheckPerGameQuotaAsync_AdminUser_BypassesQuotaCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Admin;

        // Act
        var result = await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeTrue();
        result.PerGameUsed.Should().Be(0);
        result.PerGameLimit.Should().Be(int.MaxValue);
        result.PerGameRemaining.Should().Be(int.MaxValue);

        // Verify Redis was never called
        _databaseMock.Verify(
            db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckPerGameQuotaAsync_EditorUser_BypassesQuotaCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.Editor;

        // Act
        var result = await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeTrue();
        result.PerGameLimit.Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData("free", 1)]
    [InlineData("normal", 3)]
    [InlineData("premium", 10)]
    public async Task CheckPerGameQuotaAsync_PerGameLimitReached_DeniesAccess(string tierValue, int expectedLimit)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;

        // Setup config to use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: per-game = limit reached
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("pdf:upload:game")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(expectedLimit.ToString(CultureInfo.InvariantCulture)));

        // Act
        var result = await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("Per-game private PDF limit reached");
        result.ErrorMessage.Should().Contain($"{expectedLimit} PDF/game");
        result.PerGameUsed.Should().Be(expectedLimit);
        result.PerGameLimit.Should().Be(expectedLimit);
        result.PerGameRemaining.Should().Be(0);
    }

    [Theory]
    [InlineData("free", 1)]
    [InlineData("normal", 3)]
    [InlineData("premium", 10)]
    public async Task CheckPerGameQuotaAsync_WithinLimits_AllowsAccess(string tierValue, int expectedLimit)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;

        // Setup config to use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: per-game = 0 (no uploads yet)
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("pdf:upload:game")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("0"));

        // Act
        var result = await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.PerGameUsed.Should().Be(0);
        result.PerGameLimit.Should().Be(expectedLimit);
        result.PerGameRemaining.Should().Be(expectedLimit);
    }

    [Fact]
    public async Task CheckPerGameQuotaAsync_RedisFailure_FailsOpen()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        // Setup Redis to throw exception
        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act
        var result = await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert - Fail-open: allow access when Redis is down
        result.Allowed.Should().BeTrue();
        result.PerGameLimit.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task CheckPerGameQuotaAsync_CustomConfiguredLimit_UsesConfigValue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        // Setup custom config: Premium = 50 per game
        _configServiceMock.Setup(c => c.GetValueAsync<int?>("UploadLimits:premium:PerGameLimit", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(50);

        // Setup Redis: per-game = 25
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("pdf:upload:game")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("25"));

        // Act
        var result = await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert
        result.Allowed.Should().BeTrue();
        result.PerGameUsed.Should().Be(25);
        result.PerGameLimit.Should().Be(50);
        result.PerGameRemaining.Should().Be(25);
    }

    [Fact]
    public async Task IncrementPerGameCountAsync_ExecutesStringIncrement()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _databaseMock.Setup(db => db.StringIncrementAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        // Act
        await _service.IncrementPerGameCountAsync(userId, gameId);

        // Assert
        _databaseMock.Verify(
            db => db.StringIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains($"pdf:upload:game:{userId}:{gameId}")),
                1,
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task IncrementPerGameCountAsync_RedisFailure_HandlesGracefully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _databaseMock.Setup(db => db.StringIncrementAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act - Should not throw
        await _service.IncrementPerGameCountAsync(userId, gameId);

        // Assert - Method completes without exception (logs warning internally)
        _databaseMock.Verify(
            db => db.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once());
    }

    [Fact]
    public async Task DecrementPerGameCountAsync_ExecutesLuaScript()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(new RedisValue("1")));

        // Act
        await _service.DecrementPerGameCountAsync(userId, gameId);

        // Assert
        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains("DECR")),
                It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0].ToString().Contains($"pdf:upload:game:{userId}:{gameId}")),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task DecrementPerGameCountAsync_RedisFailure_HandlesGracefully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act - Should not throw
        await _service.DecrementPerGameCountAsync(userId, gameId);

        // Assert - Method completes without exception
        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Once());
    }

    [Fact]
    public async Task GetPerGameQuotaInfoAsync_AdminUser_ReturnsUnlimitedQuota()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Admin;

        // Act
        var info = await _service.GetPerGameQuotaInfoAsync(userId, gameId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue();
        info.GameId.Should().Be(gameId);
        info.PerGameUsed.Should().Be(0);
        info.PerGameLimit.Should().Be(int.MaxValue);
        info.PerGameRemaining.Should().Be(int.MaxValue);

        // Verify Redis was never called
        _databaseMock.Verify(
            db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPerGameQuotaInfoAsync_EditorUser_ReturnsUnlimitedQuota()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.Editor;

        // Act
        var info = await _service.GetPerGameQuotaInfoAsync(userId, gameId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue();
        info.PerGameLimit.Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData("free", 1)]
    [InlineData("normal", 3)]
    [InlineData("premium", 10)]
    public async Task GetPerGameQuotaInfoAsync_RegularUser_ReturnsQuotaInfo(string tierValue, int expectedLimit)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;

        // Setup config to use defaults
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: per-game usage
        var currentUsage = expectedLimit > 1 ? expectedLimit - 1 : 0;
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("pdf:upload:game")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(currentUsage.ToString(CultureInfo.InvariantCulture)));

        // Act
        var info = await _service.GetPerGameQuotaInfoAsync(userId, gameId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeFalse();
        info.GameId.Should().Be(gameId);
        info.PerGameUsed.Should().Be(currentUsage);
        info.PerGameLimit.Should().Be(expectedLimit);
        info.PerGameRemaining.Should().Be(expectedLimit - currentUsage);
    }

    [Fact]
    public async Task GetPerGameQuotaInfoAsync_UsageExceedsLimit_RemainingIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        // Setup config to use defaults (Free = 1 per game)
        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        // Setup Redis: per-game = 3 (exceeds Free tier limit of 1)
        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("pdf:upload:game")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("3"));

        // Act
        var info = await _service.GetPerGameQuotaInfoAsync(userId, gameId, userTier, userRole);

        // Assert
        info.PerGameUsed.Should().Be(3);
        info.PerGameLimit.Should().Be(1);
        info.PerGameRemaining.Should().Be(0); // Math.Max(0, 1 - 3) = 0
    }

    [Fact]
    public async Task GetPerGameQuotaInfoAsync_RedisFailure_Throws()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act & Assert - Should throw (unlike CheckPerGameQuotaAsync which fails open)
        await Assert.ThrowsAsync<RedisConnectionException>(() =>
            _service.GetPerGameQuotaInfoAsync(userId, gameId, userTier, userRole));
    }

    [Fact]
    public async Task CheckPerGameQuotaAsync_VerifiesCorrectKeyFormat()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        RedisKey? capturedKey = null;
        _databaseMock.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, flags) => capturedKey = key)
            .ReturnsAsync(new RedisValue("0"));

        // Act
        await _service.CheckPerGameQuotaAsync(userId, gameId, userTier, userRole);

        // Assert - Verify key format: pdf:upload:game:{userId}:{gameId}
        capturedKey.Should().NotBeNull();
        var keyString = capturedKey!.Value.ToString();
        keyString.Should().Be($"pdf:upload:game:{userId}:{gameId}");
    }
}
