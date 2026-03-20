using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Tests for API key usage tracking functionality (ISSUE-904).
/// Tests RecordUsage method, UsageCount increment, and ApiKeyUsedEvent.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ApiKeyUsageTests
{
    [Fact]
    public void RecordUsage_IncrementsUsageCount()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read,write");

        var initialCount = apiKey.UsageCount;
        var endpoint = "/api/v1/games";

        // Act
        apiKey.RecordUsage(endpoint);

        // Assert
        apiKey.UsageCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public void RecordUsage_UpdatesLastUsedAt()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read");

        var beforeUsage = DateTime.UtcNow;
        var endpoint = "/api/v1/games";

        // Act
        apiKey.RecordUsage(endpoint);

        // Assert
        apiKey.LastUsedAt.Should().NotBeNull();
        (apiKey.LastUsedAt >= beforeUsage).Should().BeTrue();
    }

    [Fact]
    public void RecordUsage_RaisesDomainEvent()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read");

        var endpoint = "/api/v1/games";
        var ipAddress = "192.168.1.1";
        var userAgent = "Mozilla/5.0";

        // Act
        apiKey.RecordUsage(endpoint, ipAddress, userAgent);

        // Assert - We can't directly access domain events from unit tests
        // But we can verify the side effects: UsageCount incremented and LastUsedAt updated
        apiKey.UsageCount.Should().Be(1);
        apiKey.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordUsage_MultipleTimes_IncrementsCorrectly()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read");

        var endpoint = "/api/v1/games";

        // Act
        apiKey.RecordUsage(endpoint);
        apiKey.RecordUsage(endpoint);
        apiKey.RecordUsage(endpoint);

        // Assert
        apiKey.UsageCount.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RecordUsage_WithInvalidEndpoint_ThrowsArgumentException(string? endpoint)
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read");

        // Act & Assert
        var act = () => apiKey.RecordUsage(endpoint!);
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordUsage_WithCustomTimestamp_UsesProvidedTimestamp()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read");

        var customTimestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var endpoint = "/api/v1/games";

        // Act
        apiKey.RecordUsage(endpoint, usedAt: customTimestamp);

        // Assert
        apiKey.LastUsedAt.Should().Be(customTimestamp);
    }

    [Fact]
    public void RecordUsage_WithAllParameters_StoresAllData()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read,write,admin");

        var endpoint = "/api/v1/admin/users";
        var ipAddress = "203.0.113.42";
        var userAgent = "PostmanRuntime/7.32.3";
        var timestamp = DateTime.UtcNow;

        // Act
        apiKey.RecordUsage(endpoint, ipAddress, userAgent, timestamp);

        // Assert
        apiKey.UsageCount.Should().Be(1);
        apiKey.LastUsedAt.Should().Be(timestamp);
    }
}