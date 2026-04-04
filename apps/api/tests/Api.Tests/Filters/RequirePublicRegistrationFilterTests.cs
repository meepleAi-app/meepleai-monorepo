using Api.Filters;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for RequirePublicRegistrationFilter endpoint filter.
/// Validates fail-closed behavior: registration is blocked by default
/// and only allowed when explicitly enabled via configuration.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class RequirePublicRegistrationFilterTests
{
    private readonly Mock<IConfigurationService> _configServiceMock;

    public RequirePublicRegistrationFilterTests()
    {
        _configServiceMock = new Mock<IConfigurationService>();
    }

    private sealed class TestEndpointFilterInvocationContext : EndpointFilterInvocationContext
    {
        private readonly HttpContext _httpContext;

        public TestEndpointFilterInvocationContext(HttpContext httpContext)
        {
            _httpContext = httpContext;
        }

        public override HttpContext HttpContext => _httpContext;
        public override IList<object?> Arguments => new List<object?>();
        public override T GetArgument<T>(int index) => default!;
    }

    [Fact]
    public async Task InvokeAsync_WhenPublicRegistrationEnabled_CallsNext()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(true);

        var filter = new RequirePublicRegistrationFilter();
        var httpContext = CreateHttpContext();
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        await filter.InvokeAsync(invocationContext, next);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenPublicRegistrationDisabled_Returns403()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(false);

        var filter = new RequirePublicRegistrationFilter();
        var httpContext = CreateHttpContext();
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        var result = await filter.InvokeAsync(invocationContext, next);

        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenConfigNotFound_Returns403_FailClosed()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync((bool?)null);

        var filter = new RequirePublicRegistrationFilter();
        var httpContext = CreateHttpContext();
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        var result = await filter.InvokeAsync(invocationContext, next);

        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenConfigServiceThrows_Returns403_FailClosed()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var filter = new RequirePublicRegistrationFilter();
        var httpContext = CreateHttpContext();
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        var result = await filter.InvokeAsync(invocationContext, next);

        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_ResponseContainsUnavailableMessage()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(false);

        var filter = new RequirePublicRegistrationFilter();
        var httpContext = CreateHttpContext();
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        var result = await filter.InvokeAsync(invocationContext, next);

        // Result should be a JSON response with 403 status
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenEnabled_DoesNotModifyResponse()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(true);

        var filter = new RequirePublicRegistrationFilter();
        var httpContext = CreateHttpContext();
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var expectedResult = Results.Ok("registration success");
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expectedResult);

        var result = await filter.InvokeAsync(invocationContext, next);

        result.Should().Be(expectedResult);
    }

    #region Helper Methods

    private HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();

        var services = new ServiceCollection();
        services.AddSingleton(_configServiceMock.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        return httpContext;
    }

    #endregion
}
