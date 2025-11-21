using Api.Filters;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for RequireAdminSessionFilter (Issue #1446 - Future Enhancement).
/// Validates automatic session validation with Admin role requirement.
/// </summary>
public class RequireAdminSessionFilterTests
{
    private readonly Mock<EndpointFilterDelegate> _nextMock;

    public RequireAdminSessionFilterTests()
    {
        _nextMock = new Mock<EndpointFilterDelegate>();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenSessionNotPresent()
    {
        // Arrange
        var filter = new RequireAdminSessionFilter();
        var context = CreateFilterContext(includeSession: false);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        var filter = new RequireAdminSessionFilter();
        var session = CreateTestSession(role: "User"); // Regular user, not admin
        var context = CreateFilterContext(includeSession: true, session: session);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate_WhenUserIsAdmin()
    {
        // Arrange
        var filter = new RequireAdminSessionFilter();
        var session = CreateTestSession(role: "Admin");
        var context = CreateFilterContext(includeSession: true, session: session);
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
    public async Task InvokeAsync_PassesSessionFromHttpContextItems()
    {
        // Arrange
        var filter = new RequireAdminSessionFilter();
        var adminSession = CreateTestSession(role: "Admin");
        var context = CreateFilterContext(includeSession: true, session: adminSession);

        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify that the session in HttpContext.Items is the admin session
        Assert.True(context.HttpContext.Items.ContainsKey(nameof(ActiveSession)));
        var sessionInContext = context.HttpContext.Items[nameof(ActiveSession)] as ActiveSession;
        Assert.NotNull(sessionInContext);
        Assert.Equal("Admin", sessionInContext.User.Role);
        Assert.Equal(adminSession.User.Id, sessionInContext.User.Id);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsForbidden_WhenUserIsEditor()
    {
        // Arrange
        var filter = new RequireAdminSessionFilter();
        var session = CreateTestSession(role: "Editor"); // Editor is not admin
        var context = CreateFilterContext(includeSession: true, session: session);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    private EndpointFilterInvocationContext CreateFilterContext(bool includeSession, ActiveSession? session = null)
    {
        var httpContext = new DefaultHttpContext();

        if (includeSession)
        {
            var testSession = session ?? CreateTestSession();
            httpContext.Items[nameof(ActiveSession)] = testSession;
        }

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    private ActiveSession CreateTestSession(string role = "User")
    {
        return new ActiveSession
        {
            Session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                UserId = "test-user-id",
                Token = "test-token",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                LastActivityAt = DateTime.UtcNow
            },
            User = new User
            {
                Id = "test-user-id",
                Email = "test@example.com",
                DisplayName = "Test User",
                Role = role,
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}
