using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Api.Tests;

public class TenantContextTests
{
    [Fact]
    public void TenantId_ReturnsDefaultTenant_WhenHttpContextIsNull()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var context = new TenantContext(
            httpContextAccessor.Object,
            Options.Create(new TenantContextOptions { DefaultTenantId = "single-tenant" }));

        Assert.Equal("single-tenant", context.TenantId);
    }

    [Fact]
    public void TenantId_ReturnsDefaultTenant_WhenClaimIsMissing()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "user@example.com")
        }, "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new Mock<HttpContext>();
        httpContext.Setup(x => x.User).Returns(principal);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext.Object);

        var context = new TenantContext(
            httpContextAccessor.Object,
            Options.Create(new TenantContextOptions { DefaultTenantId = "single-tenant" }));

        Assert.Equal("single-tenant", context.TenantId);
    }

    [Fact]
    public void TenantId_PrefersClaim_WhenPresent()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("tenant", "tenant-from-claim"),
            new Claim(ClaimTypes.Email, "user@example.com")
        }, "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new Mock<HttpContext>();
        httpContext.Setup(x => x.User).Returns(principal);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext.Object);

        var context = new TenantContext(
            httpContextAccessor.Object,
            Options.Create(new TenantContextOptions { DefaultTenantId = "single-tenant" }));

        Assert.Equal("tenant-from-claim", context.TenantId);
    }

    [Fact]
    public void GetRequiredTenantId_ReturnsDefault_WhenClaimMissing()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var context = new TenantContext(
            httpContextAccessor.Object,
            Options.Create(new TenantContextOptions { DefaultTenantId = "single-tenant" }));

        Assert.Equal("single-tenant", context.GetRequiredTenantId());
    }

    [Fact]
    public void GetRequiredTenantId_Throws_WhenDefaultNotConfigured()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var context = new TenantContext(
            httpContextAccessor.Object,
            Options.Create(new TenantContextOptions { DefaultTenantId = "" }));

        var ex = Assert.Throws<UnauthorizedAccessException>(() => context.GetRequiredTenantId());
        Assert.Contains("No tenant context available", ex.Message);
    }
}
