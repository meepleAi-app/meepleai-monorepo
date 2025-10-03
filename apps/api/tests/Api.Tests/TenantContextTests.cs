using Api.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Api.Tests;

public class TenantContextTests
{
    [Fact]
    public void TenantId_ReturnsNull_WhenHttpContextIsNull()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        Assert.Null(tenantId);
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenUserIsNull()
    {
        // Arrange
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns((ClaimsPrincipal?)null);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        Assert.Null(tenantId);
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenTenantClaimDoesNotExist()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("user_id", "123"),
            new Claim("email", "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        Assert.Null(tenantId);
    }

    [Fact]
    public void TenantId_ReturnsValue_WhenTenantClaimExists()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("tenant", "tenant-123"),
            new Claim("user_id", "user-456")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        Assert.Equal("tenant-123", tenantId);
    }

    [Fact]
    public void GetRequiredTenantId_ThrowsException_WhenTenantIdIsNull()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(() => tenantContext.GetRequiredTenantId());
        Assert.Contains("No tenant context available", exception.Message);
    }

    [Fact]
    public void GetRequiredTenantId_ThrowsException_WhenTenantIdIsEmpty()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("tenant", "")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(() => tenantContext.GetRequiredTenantId());
        Assert.Contains("No tenant context available", exception.Message);
    }

    [Fact]
    public void GetRequiredTenantId_ThrowsException_WhenTenantIdIsWhitespace()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("tenant", "   ")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(() => tenantContext.GetRequiredTenantId());
        Assert.Contains("No tenant context available", exception.Message);
    }

    [Fact]
    public void GetRequiredTenantId_ReturnsValue_WhenTenantIdExists()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("tenant", "tenant-456")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var tenantId = tenantContext.GetRequiredTenantId();

        // Assert
        Assert.Equal("tenant-456", tenantId);
    }

    [Fact]
    public void TenantId_IsAccessedMultipleTimes_ReturnsConsistentValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("tenant", "tenant-789")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var firstAccess = tenantContext.TenantId;
        var secondAccess = tenantContext.TenantId;
        var thirdAccess = tenantContext.TenantId;

        // Assert
        Assert.Equal("tenant-789", firstAccess);
        Assert.Equal("tenant-789", secondAccess);
        Assert.Equal("tenant-789", thirdAccess);
    }

    [Fact]
    public void TenantId_WithMultipleTenantClaims_ReturnsFirst()
    {
        // Arrange - in case there are multiple tenant claims, should return the first one
        var claims = new List<Claim>
        {
            new Claim("tenant", "tenant-first"),
            new Claim("tenant", "tenant-second")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(user);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        var tenantContext = new TenantContext(mockHttpContextAccessor.Object);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        Assert.Equal("tenant-first", tenantId);
    }
}
