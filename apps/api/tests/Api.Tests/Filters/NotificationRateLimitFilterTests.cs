using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Filters;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for NotificationRateLimitFilter.
/// Issue #2155: Rate limiting for mark-all endpoint.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class NotificationRateLimitFilterTests
{
    private readonly Mock<EndpointFilterDelegate> _nextMock;
    private readonly Mock<IRateLimitService> _rateLimitServiceMock;
    private readonly Mock<ILogger<NotificationRateLimitFilter>> _loggerMock;

    public NotificationRateLimitFilterTests()
    {
        _nextMock = new Mock<EndpointFilterDelegate>();
        _rateLimitServiceMock = new Mock<IRateLimitService>();
        _loggerMock = new Mock<ILogger<NotificationRateLimitFilter>>();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenNoUserIdentity()
    {
        // Arrange
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: false);
        ConfigureServices(context);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate_WhenRateLimitAllowed()
    {
        // Arrange
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: true);
        ConfigureServices(context);

        _rateLimitServiceMock.Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 9, RetryAfterSeconds: 0));

        var expectedResult = Results.Ok(new { updatedCount = 5 });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Returns429_WhenRateLimitExceeded()
    {
        // Arrange
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: true);
        ConfigureServices(context);

        _rateLimitServiceMock.Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: false, TokensRemaining: 0, RetryAfterSeconds: 45));

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);

        // Verify headers were set
        Assert.True(context.HttpContext.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal("45", context.HttpContext.Response.Headers["Retry-After"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_UsesCorrectRateLimitKey_ForSessionAuth()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: true, userId: userId, authType: "session");
        ConfigureServices(context);

        string? capturedKey = null;
        _rateLimitServiceMock.Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int, double, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 9, RetryAfterSeconds: 0));

        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(Results.Ok());

        // Act
        await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal($"notifications:{userId}", capturedKey);
    }

    [Fact]
    public async Task InvokeAsync_UsesCorrectRateLimitKey_ForApiKeyAuth()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: true, userId: userId, authType: "apikey");
        ConfigureServices(context);

        string? capturedKey = null;
        _rateLimitServiceMock.Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int, double, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 9, RetryAfterSeconds: 0));

        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(Results.Ok());

        // Act
        await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal($"notifications:{userId}", capturedKey);
    }

    [Fact]
    public async Task InvokeAsync_UsesDefaultLimits()
    {
        // Arrange
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: true);
        ConfigureServices(context);

        int? capturedMaxTokens = null;
        double? capturedRefillRate = null;
        _rateLimitServiceMock.Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int, double, CancellationToken>((_, max, refill, _) =>
            {
                capturedMaxTokens = max;
                capturedRefillRate = refill;
            })
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 9, RetryAfterSeconds: 0));

        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(Results.Ok());

        // Act
        await filter.InvokeAsync(context, _nextMock.Object);

        // Assert - 10 requests per minute
        Assert.Equal(NotificationRateLimitFilter.DefaultMaxRequests, capturedMaxTokens);
        Assert.Equal(NotificationRateLimitFilter.DefaultRefillRate, capturedRefillRate);
    }

    [Fact]
    public async Task InvokeAsync_SetsRateLimitHeaders_WhenAllowed()
    {
        // Arrange
        var filter = new NotificationRateLimitFilter(_loggerMock.Object);
        var context = CreateFilterContext(authenticated: true);
        ConfigureServices(context);

        _rateLimitServiceMock.Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 7, RetryAfterSeconds: 0));

        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(Results.Ok());

        // Act
        await filter.InvokeAsync(context, _nextMock.Object);

        // Assert - Headers should be set for observability
        Assert.Equal("10", context.HttpContext.Response.Headers["X-RateLimit-Limit"].ToString());
        Assert.Equal("7", context.HttpContext.Response.Headers["X-RateLimit-Remaining"].ToString());
    }

    [Fact]
    public void DefaultMaxRequests_Is10()
    {
        // Assert - Issue #2155 requirement: 10 requests per minute
        Assert.Equal(10, NotificationRateLimitFilter.DefaultMaxRequests);
    }

    [Fact]
    public void DefaultRefillRate_IsCorrectForOneMinuteWindow()
    {
        // Assert - 10 tokens per minute = 10/60 tokens per second
        const double expectedRate = 10.0 / 60.0;
        Assert.Equal(expectedRate, NotificationRateLimitFilter.DefaultRefillRate, precision: 5);
    }

    private EndpointFilterInvocationContext CreateFilterContext(
        bool authenticated,
        Guid? userId = null,
        string authType = "session")
    {
        var httpContext = new DefaultHttpContext();
        userId ??= Guid.NewGuid();

        if (authenticated)
        {
            if (authType == "session")
            {
                var userDto = new UserDto(
                    Id: userId.Value,
                    Email: "test@example.com",
                    DisplayName: "Test User",
                    Role: "User",
                    Tier: "normal",
                    CreatedAt: DateTime.UtcNow,
                    IsTwoFactorEnabled: false,
                    TwoFactorEnabledAt: null,
                    Level: 1,
                    ExperiencePoints: 0
                );
                var session = new SessionStatusDto(
                    IsValid: true,
                    User: userDto,
                    ExpiresAt: DateTime.UtcNow.AddHours(1),
                    LastSeenAt: DateTime.UtcNow
                );
                httpContext.Items[nameof(SessionStatusDto)] = session;
            }
            else if (authType == "apikey")
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "api-user"),
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                };
                var identity = new ClaimsIdentity(claims, "ApiKey");
                httpContext.User = new ClaimsPrincipal(identity);
            }
        }

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    private void ConfigureServices(EndpointFilterInvocationContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_rateLimitServiceMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        context.HttpContext.RequestServices = serviceProvider;
    }
}
