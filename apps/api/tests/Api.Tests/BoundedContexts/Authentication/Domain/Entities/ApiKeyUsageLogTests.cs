using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Tests for ApiKeyUsageLog entity (ISSUE-904).
/// Tests usage log creation and validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ApiKeyUsageLogTests
{
    [Fact]
    public void Create_WithValidData_CreatesUsageLog()
    {
        // Arrange
        var id = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var endpoint = "/api/v1/games";
        var ipAddress = "192.168.1.1";
        var userAgent = "Mozilla/5.0";
        var httpMethod = "GET";
        var statusCode = 200;
        var responseTimeMs = 150L;
        var usedAt = DateTime.UtcNow;

        // Act
        var log = ApiKeyUsageLog.Create(
            id,
            keyId,
            endpoint,
            ipAddress,
            userAgent,
            httpMethod,
            statusCode,
            responseTimeMs,
            usedAt);

        // Assert
        Assert.Equal(id, log.Id);
        Assert.Equal(keyId, log.KeyId);
        Assert.Equal(endpoint, log.Endpoint);
        Assert.Equal(ipAddress, log.IpAddress);
        Assert.Equal(userAgent, log.UserAgent);
        Assert.Equal(httpMethod, log.HttpMethod);
        Assert.Equal(statusCode, log.StatusCode);
        Assert.Equal(responseTimeMs, log.ResponseTimeMs);
        Assert.Equal(usedAt, log.UsedAt);
    }

    [Fact]
    public void Create_WithMinimalData_CreatesUsageLog()
    {
        // Arrange
        var id = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var endpoint = "/api/v1/health";

        // Act
        var log = ApiKeyUsageLog.Create(id, keyId, endpoint);

        // Assert
        Assert.Equal(id, log.Id);
        Assert.Equal(keyId, log.KeyId);
        Assert.Equal(endpoint, log.Endpoint);
        Assert.Null(log.IpAddress);
        Assert.Null(log.UserAgent);
        Assert.Null(log.HttpMethod);
        Assert.Null(log.StatusCode);
        Assert.Null(log.ResponseTimeMs);
        Assert.True(log.UsedAt <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidEndpoint_ThrowsArgumentException(string? endpoint)
    {
        // Arrange
        var id = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            ApiKeyUsageLog.Create(id, keyId, endpoint!));
    }

    [Fact]
    public void Create_WithLongEndpoint_CreatesUsageLog()
    {
        // Arrange
        var id = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var endpoint = "/api/v1/games/search?query=very-long-game-name-with-many-parameters&page=1&pageSize=50&sortBy=popularity&includeExpansions=true";

        // Act
        var log = ApiKeyUsageLog.Create(id, keyId, endpoint);

        // Assert
        Assert.Equal(endpoint, log.Endpoint);
    }

    [Fact]
    public void Create_WithIPv6Address_CreatesUsageLog()
    {
        // Arrange
        var id = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var endpoint = "/api/v1/games";
        var ipAddress = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

        // Act
        var log = ApiKeyUsageLog.Create(id, keyId, endpoint, ipAddress);

        // Assert
        Assert.Equal(ipAddress, log.IpAddress);
    }

    [Fact]
    public void Create_WithVariousHttpMethods_CreatesUsageLog()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        var endpoint = "/api/v1/games";
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };

        foreach (var method in methods)
        {
            // Act
            var log = ApiKeyUsageLog.Create(
                Guid.NewGuid(),
                keyId,
                endpoint,
                httpMethod: method);

            // Assert
            Assert.Equal(method, log.HttpMethod);
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(500)]
    public void Create_WithVariousStatusCodes_CreatesUsageLog(int statusCode)
    {
        // Arrange
        var id = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var endpoint = "/api/v1/games";

        // Act
        var log = ApiKeyUsageLog.Create(
            id,
            keyId,
            endpoint,
            statusCode: statusCode);

        // Assert
        Assert.Equal(statusCode, log.StatusCode);
    }
}
