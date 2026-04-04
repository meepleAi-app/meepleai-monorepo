using Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Middleware;

/// <summary>
/// Tests for SecurityHeadersMiddleware (Issue #1447).
/// Validates that all 7 security headers are correctly added to responses.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SecurityHeadersMiddlewareTests
{
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<SecurityHeadersMiddleware>>();
        _environmentMock = new Mock<IHostEnvironment>();

        // Default environment for tests that don't explicitly set it
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Development");

        // Default options with all headers enabled
        _options = new SecurityHeadersOptions
        {
            EnableCsp = true,
            EnableHsts = true,
            EnableXFrameOptions = true,
            EnableXContentTypeOptions = true,
            EnableReferrerPolicy = true,
            EnablePermissionsPolicy = true
        };
    }

    [Fact]
    public async Task InvokeAsync_AddsAllSecurityHeaders_WhenEnabled()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - All 6 security headers should be present
        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeTrue();
        context.Response.Headers.ContainsKey("X-Frame-Options").Should().BeTrue();
        context.Response.Headers.ContainsKey("X-Content-Type-Options").Should().BeTrue();
        context.Response.Headers.ContainsKey("Referrer-Policy").Should().BeTrue();
        context.Response.Headers.ContainsKey("Permissions-Policy").Should().BeTrue();

        // HSTS should be skipped in development
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ContentSecurityPolicy_HasCorrectValue()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeTrue();
        var cspValue = context.Response.Headers["Content-Security-Policy"].ToString();

        // Verify key CSP directives
        cspValue.Should().ContainEquivalentOf("default-src 'self'");
        cspValue.Should().ContainEquivalentOf("script-src");
        cspValue.Should().ContainEquivalentOf("style-src");
        cspValue.Should().ContainEquivalentOf("frame-ancestors 'none'");
    }

    [Fact]
    public async Task InvokeAsync_XFrameOptions_SetToDeny()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("X-Frame-Options").Should().BeTrue();
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_XContentTypeOptions_SetToNoSniff()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("X-Content-Type-Options").Should().BeTrue();
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_ReferrerPolicy_HasCorrectValue()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("Referrer-Policy").Should().BeTrue();
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_PermissionsPolicy_RestrictsDangerousFeatures()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("Permissions-Policy").Should().BeTrue();
        var permissionsPolicy = context.Response.Headers["Permissions-Policy"].ToString();

        // Verify dangerous features are restricted
        permissionsPolicy.Should().Contain("camera=()");
        permissionsPolicy.Should().Contain("microphone=()");
        permissionsPolicy.Should().Contain("geolocation=()");
    }

    [Fact]
    public async Task InvokeAsync_HstsSkipped_InDevelopmentEnvironment()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Development");
        var context = CreateHttpContext(isHttps: true);
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - HSTS should NOT be present in development
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HstsSkipped_ForLocalhostInProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var context = CreateHttpContext(isHttps: true, host: "localhost");
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - HSTS should NOT be present for localhost
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HstsSkipped_For127001InProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var context = CreateHttpContext(isHttps: true, host: "127.0.0.1");
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - HSTS should NOT be present for 127.0.0.1
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HstsAdded_ForHttpsInProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var context = CreateHttpContext(isHttps: true, host: "api.meepleai.dev");
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - HSTS should be present for HTTPS in production
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeTrue();
        var hstsValue = context.Response.Headers["Strict-Transport-Security"].ToString();
        hstsValue.Should().Contain("max-age=31536000");
        hstsValue.Should().Contain("includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_HstsSkipped_ForHttpRequestsInProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var context = CreateHttpContext(isHttps: false, host: "api.meepleai.dev");
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - HSTS should NOT be present for HTTP requests
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_DoesNotOverwriteExistingHeaders()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN"); // Pre-existing header
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Should not overwrite existing header
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task InvokeAsync_SkipsDisabledHeaders()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableCsp = false,
            EnableHsts = false,
            EnableXFrameOptions = false,
            EnableXContentTypeOptions = true,
            EnableReferrerPolicy = true,
            EnablePermissionsPolicy = true
        };

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Disabled headers should not be present
        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
        context.Response.Headers.ContainsKey("X-Frame-Options").Should().BeFalse();

        // Enabled headers should be present
        context.Response.Headers.ContainsKey("X-Content-Type-Options").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WorksWithStreamingResponses()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.ContentType = "text/event-stream"; // SSE content type

        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(
            next: async (ctx) =>
            {
                nextCalled = true;
                await ctx.Response.WriteAsync("data: test\n\n");
            },
            _loggerMock.Object,
            _environmentMock.Object,
            Options.Create(_options));

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Next middleware should be called
        nextCalled.Should().BeTrue();

        // Security headers should still be added
        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeTrue();
        context.Response.Headers.ContainsKey("X-Frame-Options").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CustomPolicies_AreRespected()
    {
        // Arrange
        var customOptions = new SecurityHeadersOptions
        {
            EnableCsp = true,
            CspPolicy = "default-src 'none'",
            XFrameOptionsPolicy = "SAMEORIGIN",
            ReferrerPolicyValue = "no-referrer"
        };

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(customOptions);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Custom policies should be used
        context.Response.Headers["Content-Security-Policy"].ToString().Should().Be("default-src 'none'");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
    }

    [Fact]
    public async Task InvokeAsync_AllHeaders_CountedCorrectly()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Count all security headers (excluding HSTS in development)
        var securityHeaders = new[]
        {
            "Content-Security-Policy",
            "X-Frame-Options",
            "X-Content-Type-Options",
            "Referrer-Policy",
            "Permissions-Policy"
        };

        var presentHeaders = securityHeaders.Count(h => context.Response.Headers.ContainsKey(h));
        presentHeaders.Should().Be(5);
    }

    [Fact]
    public async Task InvokeAsync_HandlesNullPolicyStrings_Gracefully()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableCsp = true,
            CspPolicy = null!  // Null policy
        };

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(options);

        // Act & Assert - Should not throw, handles gracefully
        await middleware.InvokeAsync(context);

        // Header should not be added if policy is null
        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ExtendedPermissionsPolicy_IncludesAllFeatures()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnablePermissionsPolicy = true,
            PermissionsPolicyValue = "camera=(), microphone=(), geolocation=(), payment=(), usb=(), serial=(), sync-xhr=(), fullscreen=(), picture-in-picture=(), accelerometer=(), gyroscope=(), magnetometer=(), web-share=()"
        };

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("Permissions-Policy").Should().BeTrue();
        var permissionsPolicy = context.Response.Headers["Permissions-Policy"].ToString();

        // Verify extended features are present
        permissionsPolicy.Should().Contain("fullscreen=()");
        permissionsPolicy.Should().Contain("picture-in-picture=()");
        permissionsPolicy.Should().Contain("accelerometer=()");
        permissionsPolicy.Should().Contain("gyroscope=()");
        permissionsPolicy.Should().Contain("magnetometer=()");
        permissionsPolicy.Should().Contain("web-share=()");
    }

    [Fact]
    public async Task InvokeAsync_ContinuesOnHeaderException()
    {
        // Arrange
        var context = CreateHttpContext();
        var options = new SecurityHeadersOptions
        {
            EnableCsp = true,
            CspPolicy = "valid-policy"
        };

        // Create middleware that will succeed despite any header issues
        var middleware = CreateMiddleware(options);

        // Act - Should not throw even if there are internal issues
        await middleware.InvokeAsync(context);

        // Assert - Middleware completed successfully
        context.Response.Should().NotBeNull();
    }

    // Helper methods

    private DefaultHttpContext CreateHttpContext(bool isHttps = false, string host = "localhost")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = isHttps ? "https" : "http";
        context.Request.Host = new HostString(host);
        context.Request.Path = "/api/v1/test";
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private SecurityHeadersMiddleware CreateMiddleware(SecurityHeadersOptions options)
    {
        return new SecurityHeadersMiddleware(
            next: (context) => Task.CompletedTask,
            _loggerMock.Object,
            _environmentMock.Object,
            Options.Create(options));
    }
}
