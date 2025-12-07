using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Filters;
using Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for RequireAuthenticatedUserFilter (Issue #1446 - Future Enhancement).
/// Validates authentication via session OR API key.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RequireAuthenticatedUserFilterTests
{
    private readonly Mock<EndpointFilterDelegate> _nextMock;

    public RequireAuthenticatedUserFilterTests()
    {
        _nextMock = new Mock<EndpointFilterDelegate>();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenNoAuthentication()
    {
        // Arrange
        var filter = new RequireAuthenticatedUserFilter();
        var context = CreateFilterContext(includeSession: false, includeApiKey: false);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate_WhenSessionPresent()
    {
        // Arrange
        var filter = new RequireAuthenticatedUserFilter();
        var context = CreateFilterContext(includeSession: true, includeApiKey: false);
        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate_WhenApiKeyPresent()
    {
        // Arrange
        var filter = new RequireAuthenticatedUserFilter();
        var context = CreateFilterContext(includeSession: false, includeApiKey: true);
        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate_WhenBothSessionAndApiKeyPresent()
    {
        // Arrange (session takes priority)
        var filter = new RequireAuthenticatedUserFilter();
        var context = CreateFilterContext(includeSession: true, includeApiKey: true);
        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_SessionAvailableInHttpContextItems_WhenSessionAuth()
    {
        // Arrange
        var filter = new RequireAuthenticatedUserFilter();
        var testSession = CreateTestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[nameof(SessionStatusDto)] = testSession;

        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify session is in HttpContext.Items
        Assert.True(context.HttpContext.Items.ContainsKey(nameof(SessionStatusDto)));
        var sessionInContext = context.HttpContext.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        Assert.NotNull(sessionInContext);
        Assert.Equal(testSession.User.Id, sessionInContext.User.Id);
    }

    [Fact]
    public async Task InvokeAsync_UserClaimsAvailableInHttpContext_WhenApiKeyAuth()
    {
        // Arrange
        var filter = new RequireAuthenticatedUserFilter();
        var httpContext = new DefaultHttpContext();

        // Simulate API key authentication via ClaimsPrincipal
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "api-user"),
            new Claim(ClaimTypes.NameIdentifier, "api-user-id")
        };
        var identity = new ClaimsIdentity(claims, "ApiKey");
        httpContext.User = new ClaimsPrincipal(identity);

        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify user is authenticated
        Assert.True(context.HttpContext.User.Identity?.IsAuthenticated);
        Assert.Equal("api-user", context.HttpContext.User.Identity?.Name);
    }

    private EndpointFilterInvocationContext CreateFilterContext(bool includeSession, bool includeApiKey)
    {
        var httpContext = new DefaultHttpContext();

        if (includeSession)
        {
            var testSession = CreateTestSession();
            httpContext.Items[nameof(SessionStatusDto)] = testSession;
        }

        if (includeApiKey)
        {
            // Simulate API key authentication
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "api-user"),
                new Claim(ClaimTypes.NameIdentifier, "api-user-id")
            };
            var identity = new ClaimsIdentity(claims, "ApiKey");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    private SessionStatusDto CreateTestSession()
    {
        var userDto = new UserDto(
            Id: Guid.NewGuid(),
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null
        );
        return new SessionStatusDto(
            IsValid: true,
            User: userDto,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            LastSeenAt: DateTime.UtcNow
        );
    }
}

