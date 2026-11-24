using System.Security.Claims;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Filters;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Filters;

/// <summary>
/// Unit tests for <see cref="RequireLiveSessionCreatorFilter"/> — the endpoint filter that lets only the
/// creator of a live session pause, resume, complete, save and remove players (#4763).
/// 401 if unauthenticated, 404 if the session is missing, 403 for an authorized participant who is not the
/// creator and for a stranger, else next().
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RequireLiveSessionCreatorFilterTests
{
    private readonly Mock<EndpointFilterDelegate> _nextMock = new();

    private static EndpointFilterInvocationContext CreateContext(
        Guid? userId, string? sessionIdRoute, IMediator? mediator)
    {
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test"));
        }

        if (sessionIdRoute is not null)
        {
            httpContext.Request.RouteValues["sessionId"] = sessionIdRoute;
        }

        var services = new ServiceCollection();
        if (mediator is not null)
        {
            services.AddSingleton(mediator);
        }
        httpContext.RequestServices = services.BuildServiceProvider();

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    private static Mock<IMediator> MediatorReturning(bool found, bool authorized, bool isCreator)
    {
        var mock = new Mock<IMediator>();
        mock.Setup(m => m.Send(
                It.IsAny<GetLiveSessionParticipantContextQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiveSessionParticipantContextResult(found, authorized, isCreator));
        return mock;
    }

    [Fact]
    public async Task InvokeAsync_NoAuthenticatedUser_Returns401_AndDoesNotCallNext()
    {
        var filter = new RequireLiveSessionCreatorFilter();
        var context = CreateContext(userId: null, sessionIdRoute: Guid.NewGuid().ToString(), mediator: null);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(401);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_SessionNotFound_Returns404_AndDoesNotCallNext()
    {
        var filter = new RequireLiveSessionCreatorFilter();
        var mediator = MediatorReturning(found: false, authorized: false, isCreator: false);
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid().ToString(), mediator.Object);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(404);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]  // a linked player: a participant, not the creator
    [InlineData(false)] // a stranger
    public async Task InvokeAsync_NotTheCreator_Returns403_AndDoesNotCallNext(bool authorized)
    {
        var filter = new RequireLiveSessionCreatorFilter();
        var mediator = MediatorReturning(found: true, authorized: authorized, isCreator: false);
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid().ToString(), mediator.Object);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(403);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Creator_CallsNext()
    {
        var filter = new RequireLiveSessionCreatorFilter();
        var mediator = MediatorReturning(found: true, authorized: true, isCreator: true);
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid().ToString(), mediator.Object);

        var expected = Results.Ok("passed");
        _nextMock.Setup(n => n(It.IsAny<EndpointFilterInvocationContext>())).ReturnsAsync(expected);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().Be(expected);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }
}
