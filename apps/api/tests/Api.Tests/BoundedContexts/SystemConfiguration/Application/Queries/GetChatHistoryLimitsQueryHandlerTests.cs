using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Unit tests for GetChatHistoryLimitsQueryHandler.
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetChatHistoryLimitsQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly GetChatHistoryLimitsQueryHandler _handler;

    public GetChatHistoryLimitsQueryHandlerTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _handler = new GetChatHistoryLimitsQueryHandler(_mockConfigService.Object);
    }

    [Fact]
    public async Task Handle_WhenAllLimitsConfigured_ReturnsConfiguredValues()
    {
        // Arrange
        var freeConfig = CreateConfigDto(GetChatHistoryLimitsQueryHandler.FreeTierKey, "5");
        var normalConfig = CreateConfigDto(GetChatHistoryLimitsQueryHandler.NormalTierKey, "50");
        var premiumConfig = CreateConfigDto(GetChatHistoryLimitsQueryHandler.PremiumTierKey, "500");

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(freeConfig);

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.NormalTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalConfig);

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.PremiumTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(premiumConfig);

        var query = new GetChatHistoryLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FreeTierLimit.Should().Be(5);
        result.NormalTierLimit.Should().Be(50);
        result.PremiumTierLimit.Should().Be(500);
    }

    [Fact]
    public async Task Handle_WhenNoLimitsConfigured_ReturnsDefaults()
    {
        // Arrange: no configs in DB
        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        var query = new GetChatHistoryLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FreeTierLimit.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit);
        result.NormalTierLimit.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultNormalTierLimit);
        result.PremiumTierLimit.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultPremiumTierLimit);
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("1", 1)]
    [InlineData("999", 999)]
    public async Task Handle_WhenFreeTierConfigured_ParsesCorrectly(string dbValue, int expectedLimit)
    {
        // Arrange
        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(GetChatHistoryLimitsQueryHandler.FreeTierKey, dbValue));

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                It.IsIn(
                    GetChatHistoryLimitsQueryHandler.NormalTierKey,
                    GetChatHistoryLimitsQueryHandler.PremiumTierKey),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        var query = new GetChatHistoryLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FreeTierLimit.Should().Be(expectedLimit);
    }

    [Fact]
    public async Task Handle_WhenInvalidValue_ReturnsDefaultForThatTier()
    {
        // Arrange: invalid string value for Free tier
        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(GetChatHistoryLimitsQueryHandler.FreeTierKey, "not-a-number"));

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                It.IsIn(
                    GetChatHistoryLimitsQueryHandler.NormalTierKey,
                    GetChatHistoryLimitsQueryHandler.PremiumTierKey),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        var query = new GetChatHistoryLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: falls back to default for invalid parse
        result.FreeTierLimit.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit);
    }

    [Fact]
    public async Task Handle_WhenSomeConfigured_MixesDbAndDefaults()
    {
        // Arrange: only Free tier configured in DB
        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(GetChatHistoryLimitsQueryHandler.FreeTierKey, "25"));

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                It.IsIn(
                    GetChatHistoryLimitsQueryHandler.NormalTierKey,
                    GetChatHistoryLimitsQueryHandler.PremiumTierKey),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        var query = new GetChatHistoryLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: Free from DB, Normal/Premium use defaults
        result.FreeTierLimit.Should().Be(25);
        result.NormalTierLimit.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultNormalTierLimit);
        result.PremiumTierLimit.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultPremiumTierLimit);
    }

    [Fact]
    public async Task Handle_WhenAllConfigured_SetsLastUpdatedAtToMostRecent()
    {
        // Arrange: three configs with different updatedAt
        var oldest = DateTime.UtcNow.AddHours(-3);
        var middle = DateTime.UtcNow.AddHours(-2);
        var newest = DateTime.UtcNow.AddHours(-1);

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(GetChatHistoryLimitsQueryHandler.FreeTierKey, "10", newest));

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.NormalTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(GetChatHistoryLimitsQueryHandler.NormalTierKey, "100", middle));

        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                GetChatHistoryLimitsQueryHandler.PremiumTierKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(GetChatHistoryLimitsQueryHandler.PremiumTierKey, "1000", oldest));

        var query = new GetChatHistoryLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: LastUpdatedAt should be the most recent
        result.LastUpdatedAt.Should().BeCloseTo(newest, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static SystemConfigurationDto CreateConfigDto(
        string key,
        string value,
        DateTime? updatedAt = null)
    {
        var timestamp = updatedAt ?? DateTime.UtcNow;
        return new SystemConfigurationDto(
            Id: Guid.NewGuid().ToString(),
            Key: key,
            Value: value,
            ValueType: "int",
            Description: null,
            Category: "ChatHistory",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: timestamp,
            UpdatedAt: timestamp,
            CreatedByUserId: Guid.NewGuid().ToString(),
            UpdatedByUserId: Guid.NewGuid().ToString(),
            LastToggledAt: null
        );
    }
}
