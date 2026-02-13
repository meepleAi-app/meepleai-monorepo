using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Middleware;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.Middleware;

/// <summary>
/// Unit tests for BggRateLimitMiddleware (Issue #4275).
/// Validates tier-based BGG API rate limiting with Redis sliding window.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class BggRateLimitMiddlewareTests
{
    private readonly Mock<ILogger<BggRateLimitMiddleware>> _loggerMock;
    private readonly Mock<IRateLimitService> _rateLimitServiceMock;
    private readonly BggRateLimitOptions _options;
    private bool _nextCalled;

    public BggRateLimitMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<BggRateLimitMiddleware>>();
        _rateLimitServiceMock = new Mock<IRateLimitService>();
        _nextCalled = false;

        _options = new BggRateLimitOptions
        {
            FreeTier = 5,
            NormalTier = 10,
            PremiumTier = 20,
            EditorTier = 15,
            WindowSeconds = 60,
            AdminBypass = true,
            EnableMetrics = true
        };
    }

    [Fact]
    public async Task InvokeAsync_NonBggRequest_SkipsRateLimiting()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/games/search");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.True(_nextCalled, "Next middleware should be called");
        _rateLimitServiceMock.Verify(
            x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Rate limit check should be skipped for non-BGG requests");
    }

    [Fact]
    public async Task InvokeAsync_BggRequest_UnauthenticatedUser_Returns401()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search?query=wingspan");
        // No authentication - no ActiveSession

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
        Assert.False(_nextCalled, "Next middleware should NOT be called for unauthenticated requests");
    }

    [Fact]
    public async Task InvokeAsync_FreeTierUser_EnforcesFreeTierLimit()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search?query=wingspan");
        SetupAuthenticatedUser(context, userId: "user-1", tier: "Free", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(
                "bgg:user-1",
                5, // FreeTier limit
                5.0 / 60.0, // Refill rate
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(
                Allowed: true,
                TokensRemaining: 3,
                RetryAfterSeconds: 0));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(_nextCalled);
        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Limit"));
        Assert.Equal("5", context.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("3", context.Response.Headers["X-RateLimit-Remaining"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_NormalTierUser_EnforcesNormalTierLimit()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/games/266192");
        SetupAuthenticatedUser(context, userId: "user-2", tier: "Normal", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(
                "bgg:user-2",
                10, // NormalTier limit
                10.0 / 60.0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(
                Allowed: true,
                TokensRemaining: 8,
                RetryAfterSeconds: 0));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(_nextCalled);
        Assert.Equal("10", context.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("8", context.Response.Headers["X-RateLimit-Remaining"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_PremiumTierUser_EnforcesPremiumTierLimit()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "user-3", tier: "Premium", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(
                "bgg:user-3",
                20, // PremiumTier limit
                20.0 / 60.0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 15, 0));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal("20", context.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("15", context.Response.Headers["X-RateLimit-Remaining"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_EditorTierUser_EnforcesEditorTierLimit()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "editor-1", tier: "Normal", role: "Editor");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(
                "bgg:editor-1",
                15, // EditorTier limit
                15.0 / 60.0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 12, 0));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal("15", context.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("12", context.Response.Headers["X-RateLimit-Remaining"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_AdminUser_BypassesRateLimit()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "admin-1", tier: "Premium", role: "Admin");

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(_nextCalled);
        Assert.Equal("unlimited", context.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("unlimited", context.Response.Headers["X-RateLimit-Remaining"].ToString());

        // Rate limit service should NOT be called for admin
        _rateLimitServiceMock.Verify(
            x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RateLimitExceeded_Returns429WithRetryAfter()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "user-4", tier: "Free", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(
                "bgg:user-4",
                5,
                5.0 / 60.0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(
                Allowed: false,
                TokensRemaining: 0,
                RetryAfterSeconds: 45));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal(429, context.Response.StatusCode);
        Assert.False(_nextCalled, "Next middleware should NOT be called when rate limited");
        Assert.Equal("45", context.Response.Headers["Retry-After"].ToString());
        Assert.Equal("5", context.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("0", context.Response.Headers["X-RateLimit-Remaining"].ToString());

        // Verify JSON response body
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.Equal("Rate limit exceeded", errorResponse.GetProperty("error").GetString());
        Assert.Contains("5 requests per minute", errorResponse.GetProperty("message").GetString());
        Assert.Equal(45, errorResponse.GetProperty("retryAfter").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_ResponseHeadersIncludeResetTimestamp()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "user-5", tier: "Normal", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 7, 53));

        var middleware = CreateMiddleware();
        var beforeTimestamp = DateTimeOffset.UtcNow.AddSeconds(50).ToUnixTimeSeconds();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        var afterTimestamp = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();
        var resetHeader = context.Response.Headers["X-RateLimit-Reset"].ToString();
        var resetTimestamp = long.Parse(resetHeader);

        Assert.InRange(resetTimestamp, beforeTimestamp, afterTimestamp);
    }

    [Fact]
    public async Task InvokeAsync_RedisError_FailsOpenAndAllowsRequest()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "user-6", tier: "Normal", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(_nextCalled, "Request should be allowed on Redis failure (fail-open)");
        Assert.Equal("Error", context.Response.Headers["X-RateLimit-Status"].ToString());

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("BGG rate limit check failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTier_DefaultsToFreeTier()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "user-7", tier: "UnknownTier", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(
                "bgg:user-7",
                5, // Should default to FreeTier
                5.0 / 60.0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 4, 0));

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        Assert.Equal("5", context.Response.Headers["X-RateLimit-Limit"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_MultipleUsers_SeparateRateLimitKeys()
    {
        // Arrange
        var user1Context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(user1Context, userId: "user-8", tier: "Normal", role: "User");

        var user2Context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(user2Context, userId: "user-9", tier: "Normal", role: "User");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync("bgg:user-8", It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 9, 0));

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync("bgg:user-9", It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 10, 0));

        var middleware = CreateMiddleware();

        // Act
        _nextCalled = false;
        await middleware.InvokeAsync(user1Context, _rateLimitServiceMock.Object);
        var user1Called = _nextCalled;

        _nextCalled = false;
        await middleware.InvokeAsync(user2Context, _rateLimitServiceMock.Object);
        var user2Called = _nextCalled;

        // Assert
        Assert.True(user1Called && user2Called);
        _rateLimitServiceMock.Verify(x => x.CheckRateLimitAsync("bgg:user-8", It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        _rateLimitServiceMock.Verify(x => x.CheckRateLimitAsync("bgg:user-9", It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_AdminBypassDisabled_AdminStillGetsRateLimited()
    {
        // Arrange
        var options = new BggRateLimitOptions
        {
            AdminBypass = false, // Disable bypass
            NormalTier = 10
        };

        var context = CreateHttpContext("/api/v1/bgg/search");
        SetupAuthenticatedUser(context, userId: "admin-2", tier: "Normal", role: "Admin");

        _rateLimitServiceMock
            .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 8, 0));

        var middleware = CreateMiddleware(options);

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        _rateLimitServiceMock.Verify(
            x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "Admin should be rate limited when AdminBypass is false");
    }

    [Theory]
    [InlineData("/api/v1/bgg/search", true)]
    [InlineData("/api/v1/bgg/games/123", true)]
    [InlineData("/api/v1/bgg/thumbnails", true)]
    [InlineData("/API/V1/BGG/SEARCH", true)] // Case-insensitive
    [InlineData("/api/v1/games/search", false)]
    [InlineData("/api/v1/admin/bgg-queue", false)]
    [InlineData("/health", false)]
    public async Task InvokeAsync_PathMatching_CorrectlyIdentifiesBggRequests(string path, bool shouldApplyRateLimit)
    {
        // Arrange
        var context = CreateHttpContext(path);
        SetupAuthenticatedUser(context, userId: "user-10", tier: "Normal", role: "User");

        if (shouldApplyRateLimit)
        {
            _rateLimitServiceMock
                .Setup(x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RateLimitResult(true, 9, 0));
        }

        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context, _rateLimitServiceMock.Object);

        // Assert
        if (shouldApplyRateLimit)
        {
            _rateLimitServiceMock.Verify(
                x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
                Times.Once,
                $"Rate limit should be applied to BGG path: {path}");
        }
        else
        {
            _rateLimitServiceMock.Verify(
                x => x.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
                Times.Never,
                $"Rate limit should NOT be applied to non-BGG path: {path}");
        }
    }

    private HttpContext CreateHttpContext(string path = "/api/v1/bgg/search")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private void SetupAuthenticatedUser(HttpContext context, string userId, string tier, string role)
    {
        // Simulate ActiveSession set by SessionAuthenticationMiddleware
        context.Items["ActiveSession"] = new
        {
            Authenticated = true,
            Session = new
            {
                User = new
                {
                    Id = userId,
                    Tier = tier,
                    Role = role
                }
            },
            Metadata = (object?)null
        };
    }

    private BggRateLimitMiddleware CreateMiddleware(BggRateLimitOptions? options = null)
    {
        var optionsWrapper = Options.Create(options ?? _options);

        RequestDelegate next = _ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        };

        return new BggRateLimitMiddleware(next, _loggerMock.Object, optionsWrapper);
    }
}
