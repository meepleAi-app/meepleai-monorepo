using Api.Infrastructure.Filters;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Filters;

/// <summary>
/// Unit tests for the C8 <see cref="AntiforgeryEndpointFilter"/>.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C8).
///
/// The filter is the per-endpoint enforcement of the antiforgery / CSRF
/// double-submit cookie pattern: it pulls <see cref="IAntiforgery"/> from
/// RequestServices and calls <c>ValidateRequestAsync</c>; on
/// <see cref="AntiforgeryValidationException"/> it returns 400 instead of
/// letting the request hit the underlying endpoint.
///
/// Two contracts:
///   1. In the <c>Testing</c> ASP.NET Core environment the filter MUST be a
///      no-op so the existing integration test suite (which doesn't carry
///      CSRF tokens) keeps working without per-test scaffolding.
///   2. Outside Testing the filter MUST validate the token and short-circuit
///      with a 400 BadRequest on validation failure — never silently let the
///      request through.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class AntiforgeryEndpointFilterTests
{
    private readonly Mock<IAntiforgery> _antiforgery = new();
    private readonly Mock<IHostEnvironment> _env = new();
    private readonly Mock<EndpointFilterDelegate> _next = new();
    private readonly AntiforgeryEndpointFilter _filter = new();

    [Fact]
    public async Task InvokeAsync_TestingEnvironment_BypassesValidation_AndCallsNext()
    {
        var context = CreateContext("Testing");
        var sentinel = Results.Ok(new { passed = true });
        _next.Setup(n => n(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(sentinel);

        var result = await _filter.InvokeAsync(context, _next.Object);

        result.Should().Be(sentinel);
        _next.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
        _antiforgery.Verify(
            a => a.ValidateRequestAsync(It.IsAny<HttpContext>()),
            Times.Never,
            "Testing environment must NOT consult IAntiforgery — the existing " +
            "integration tests don't carry CSRF tokens and would otherwise fail.");
    }

    [Fact]
    public async Task InvokeAsync_Production_ValidToken_CallsNext()
    {
        var context = CreateContext("Production");
        var sentinel = Results.Ok();
        _antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Returns(Task.CompletedTask);
        _next.Setup(n => n(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(sentinel);

        var result = await _filter.InvokeAsync(context, _next.Object);

        result.Should().Be(sentinel);
        _next.Verify(n => n(It.IsAny<EndpointFilterInvocationContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Production_InvalidToken_ReturnsBadRequest_AndDoesNotCallNext()
    {
        var context = CreateContext("Production");
        _antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("invalid"));

        var result = await _filter.InvokeAsync(context, _next.Object);

        result.Should().NotBeNull();
        // Results.BadRequest(object) returns Microsoft.AspNetCore.Http.HttpResults.BadRequest<T>;
        // type-name match is enough for this assertion (avoid coupling to the
        // exact generic argument shape).
        result!.GetType().FullName.Should().StartWith(
            "Microsoft.AspNetCore.Http.HttpResults.BadRequest",
            "an invalid CSRF token must short-circuit with HTTP 400 before " +
            "the underlying endpoint runs — letting the request through " +
            "would defeat the entire C8 fix.");
        _next.Verify(
            n => n(It.IsAny<EndpointFilterInvocationContext>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentEnvironment_ValidatesToken()
    {
        // Sanity: only the literal "Testing" string bypasses; "Development"
        // and any other environment must still go through validation.
        var context = CreateContext("Development");
        _antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Returns(Task.CompletedTask);
        _next.Setup(n => n(It.IsAny<EndpointFilterInvocationContext>()))
            .ReturnsAsync(Results.Ok());

        await _filter.InvokeAsync(context, _next.Object);

        _antiforgery.Verify(
            a => a.ValidateRequestAsync(It.IsAny<HttpContext>()),
            Times.Once);
    }

    private EndpointFilterInvocationContext CreateContext(string environmentName)
    {
        _env.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var services = new ServiceCollection();
        services.AddSingleton(_antiforgery.Object);
        services.AddSingleton(_env.Object);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

}
