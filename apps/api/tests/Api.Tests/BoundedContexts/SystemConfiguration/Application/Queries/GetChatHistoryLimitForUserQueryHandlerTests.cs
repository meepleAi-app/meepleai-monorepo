using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Unit tests for GetChatHistoryLimitForUserQueryHandler.
/// Issue #4918: Tier-based configurable limit lookup.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetChatHistoryLimitForUserQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly GetChatHistoryLimitForUserQueryHandler _handler;

    public GetChatHistoryLimitForUserQueryHandlerTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _handler = new GetChatHistoryLimitForUserQueryHandler(_mockConfigService.Object);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Editor")]
    [InlineData("editor")]
    public async Task Handle_WhenAdminOrEditor_ReturnsZeroUnlimited(string role)
    {
        // Arrange
        var query = new GetChatHistoryLimitForUserQuery(UserTier: "free", UserRole: role);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: 0 = unlimited convention
        result.Should().Be(0);

        // Verify config service is NOT called for admin/editor
        _mockConfigService.Verify(
            s => s.GetConfigurationByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("free", GetChatHistoryLimitsQueryHandler.FreeTierKey)]
    [InlineData("anonymous", GetChatHistoryLimitsQueryHandler.FreeTierKey)]
    [InlineData("normal", GetChatHistoryLimitsQueryHandler.NormalTierKey)]
    [InlineData("user", GetChatHistoryLimitsQueryHandler.NormalTierKey)]
    [InlineData("premium", GetChatHistoryLimitsQueryHandler.PremiumTierKey)]
    [InlineData("pro", GetChatHistoryLimitsQueryHandler.PremiumTierKey)]
    [InlineData("enterprise", GetChatHistoryLimitsQueryHandler.PremiumTierKey)]
    public async Task Handle_WhenTierConfigured_ReadsCorrectConfigKey(string tier, string expectedKey)
    {
        // Arrange
        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                expectedKey,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(expectedKey, "42"));

        var query = new GetChatHistoryLimitForUserQuery(UserTier: tier, UserRole: "User");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(42);
        _mockConfigService.Verify(
            s => s.GetConfigurationByKeyAsync(expectedKey, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenConfigNotFound_ReturnsDefaultForTier()
    {
        // Arrange: config not in DB
        _mockConfigService.Setup(s => s.GetConfigurationByKeyAsync(
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        var query = new GetChatHistoryLimitForUserQuery(UserTier: "free", UserRole: "User");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit);
    }

    [Fact]
    public async Task Handle_WhenUnknownTier_ReturnsFreeTierDefault()
    {
        // Arrange
        var query = new GetChatHistoryLimitForUserQuery(UserTier: "unknown-tier", UserRole: "User");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: unknown tier defaults to Free limit
        result.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit);
    }

    [Fact]
    public async Task Handle_WhenNullTier_ReturnsFreeTierDefault()
    {
        // Arrange
        var query = new GetChatHistoryLimitForUserQuery(UserTier: null, UserRole: "User");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: null tier defaults to Free limit
        result.Should().Be(GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit);
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

    private static SystemConfigurationDto CreateConfigDto(string key, string value)
    {
        var now = DateTime.UtcNow;
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
            CreatedAt: now,
            UpdatedAt: now,
            CreatedByUserId: Guid.NewGuid().ToString(),
            UpdatedByUserId: null,
            LastToggledAt: null
        );
    }
}
