using Api.Filters;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for RequireSessionFilter (Issue #1446).
/// Validates automatic session validation via endpoint filter.
/// </summary>
public class RequireSessionFilterTests
{
    private readonly Mock<EndpointFilterDelegate> _nextMock;

    public RequireSessionFilterTests()
    {
        _nextMock = new Mock<EndpointFilterDelegate>();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenSessionNotPresent()
    {
        // Arrange
        var filter = new RequireSessionFilter();
        var context = CreateFilterContext(includeSession: false);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);
        var httpResult = Assert.IsAssignableFrom<IResult>(result);

        // Verify that next delegate was NOT called
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate_WhenSessionPresent()
    {
        // Arrange
        var filter = new RequireSessionFilter();
        var context = CreateFilterContext(includeSession: true);
        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify that next delegate WAS called once
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_PassesSessionFromHttpContextItems()
    {
        // Arrange
        var filter = new RequireSessionFilter();
        var testSession = CreateTestSession();
        var context = CreateFilterContext(includeSession: true, session: testSession);

        var expectedResult = Results.Ok(new { success = true });
        _nextMock.Setup(next => next(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify that the session in HttpContext.Items is the same one we set
        Assert.True(context.HttpContext.Items.ContainsKey(nameof(ActiveSession)));
        var sessionInContext = context.HttpContext.Items[nameof(ActiveSession)] as ActiveSession;
        Assert.NotNull(sessionInContext);
        Assert.Equal(testSession.Session.Id, sessionInContext.Session.Id);
        Assert.Equal(testSession.User.Id, sessionInContext.User.Id);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenSessionIsNull()
    {
        // Arrange
        var filter = new RequireSessionFilter();
        var httpContext = new DefaultHttpContext();
        // Set session key with null value
        httpContext.Items[nameof(ActiveSession)] = null;

        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);

        // Verify that next delegate was NOT called
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenSessionIsWrongType()
    {
        // Arrange
        var filter = new RequireSessionFilter();
        var httpContext = new DefaultHttpContext();
        // Set session key with wrong type (string instead of ActiveSession)
        httpContext.Items[nameof(ActiveSession)] = "not a session";

        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);

        // Verify that next delegate was NOT called
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

    private ActiveSession CreateTestSession()
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
                Role = "User",
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}
