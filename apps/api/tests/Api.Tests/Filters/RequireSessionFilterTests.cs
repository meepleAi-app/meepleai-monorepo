using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Filters;
using Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for RequireSessionFilter (Issue #1446).
/// Validates automatic session validation via endpoint filter.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RequireSessionFilterTests
{
    private readonly Mock<EndpointFilterDelegate> _nextMock;

    public RequireSessionFilterTests()
    {
        _nextMock = new Mock<EndpointFilterDelegate>();
    }

    /// <summary>
    /// Test implementation of EndpointFilterInvocationContext for unit testing.
    /// DefaultEndpointFilterInvocationContext is internal to ASP.NET Core.
    /// </summary>
    private sealed class TestEndpointFilterInvocationContext : EndpointFilterInvocationContext
    {
        private readonly HttpContext _httpContext;

        public TestEndpointFilterInvocationContext(HttpContext httpContext)
        {
            _httpContext = httpContext;
        }

        public override HttpContext HttpContext => _httpContext;

        public override IList<object?> Arguments => new List<object?>();

        public override T GetArgument<T>(int index)
        {
#pragma warning disable MA0025 // Implement the functionality - This is a test stub, not used in tests
            throw new NotImplementedException();
#pragma warning restore MA0025
        }
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
        Assert.True(context.HttpContext.Items.ContainsKey(nameof(SessionStatusDto)));
        var sessionInContext = context.HttpContext.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        Assert.NotNull(sessionInContext);
        Assert.NotNull(sessionInContext.User);
        Assert.Equal(testSession.User!.Id, sessionInContext.User.Id);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenSessionIsNull()
    {
        // Arrange
        var filter = new RequireSessionFilter();
        var httpContext = new DefaultHttpContext();
        // Set session key with null value
        httpContext.Items[nameof(SessionStatusDto)] = null;

        var context = new TestEndpointFilterInvocationContext(httpContext);

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
        // Set session key with wrong type (string instead of SessionStatusDto)
        httpContext.Items[nameof(SessionStatusDto)] = "not a session";

        var context = new TestEndpointFilterInvocationContext(httpContext);

        // Act
        var result = await filter.InvokeAsync(context, _nextMock.Object);

        // Assert
        Assert.NotNull(result);

        // Verify that next delegate was NOT called
        _nextMock.Verify(next => next(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    private EndpointFilterInvocationContext CreateFilterContext(bool includeSession, SessionStatusDto? session = null)
    {
        var httpContext = new DefaultHttpContext();

        if (includeSession)
        {
            var testSession = session ?? CreateTestSession();
            httpContext.Items[nameof(SessionStatusDto)] = testSession;
        }

        return new TestEndpointFilterInvocationContext(httpContext);
    }

    private SessionStatusDto CreateTestSession()
    {
        var userDto = new UserDto(
            Id: Guid.NewGuid(),
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
        return new SessionStatusDto(
            IsValid: true,
            User: userDto,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            LastSeenAt: DateTime.UtcNow
        );
    }
}

