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
/// Unit tests for <see cref="RequireLiveSessionParticipantFilter"/> — the endpoint filter enforcing
/// per-session participant authorization on live-session endpoints (#2573).
/// 401 if unauthenticated, 404 if session missing/unresolvable, 403 if not a participant, else next().
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RequireLiveSessionParticipantFilterTests
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

    private static Mock<IMediator> MediatorReturning(bool found, bool authorized)
    {
        var mock = new Mock<IMediator>();
        mock.Setup(m => m.Send(
                It.IsAny<GetLiveSessionParticipantContextQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiveSessionParticipantContextResult(found, authorized));
        return mock;
    }

    [Fact]
    public async Task InvokeAsync_NoAuthenticatedUser_Returns401_AndDoesNotCallNext()
    {
        var filter = new RequireLiveSessionParticipantFilter();
        var context = CreateContext(userId: null, sessionIdRoute: Guid.NewGuid().ToString(), mediator: null);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(401);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_MissingSessionIdRoute_Returns404_AndDoesNotCallNext()
    {
        var filter = new RequireLiveSessionParticipantFilter();
        var context = CreateContext(userId: Guid.NewGuid(), sessionIdRoute: null, mediator: null);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(404);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_SessionNotFound_Returns404_AndDoesNotCallNext()
    {
        var filter = new RequireLiveSessionParticipantFilter();
        var mediator = MediatorReturning(found: false, authorized: false);
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid().ToString(), mediator.Object);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(404);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_NotParticipant_Returns403_AndDoesNotCallNext()
    {
        var filter = new RequireLiveSessionParticipantFilter();
        var mediator = MediatorReturning(found: true, authorized: false);
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid().ToString(), mediator.Object);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(403);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Participant_CallsNext()
    {
        var filter = new RequireLiveSessionParticipantFilter();
        var mediator = MediatorReturning(found: true, authorized: true);
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid().ToString(), mediator.Object);

        var expected = Results.Ok("passed");
        _nextMock.Setup(n => n(It.IsAny<EndpointFilterInvocationContext>())).ReturnsAsync(expected);

        var result = await filter.InvokeAsync(context, _nextMock.Object);

        result.Should().Be(expected);
        _nextMock.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }
}
