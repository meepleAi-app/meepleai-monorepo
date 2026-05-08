using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.Services;
using Api.BoundedContexts.Authentication.Infrastructure.Middleware;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Middleware;

/// <summary>
/// Unit tests for StagingAccessMiddleware (DevOps wave 1 email allowlist gate).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class StagingAccessMiddlewareTests
{
    private static DefaultHttpContext CreateAuthenticatedContext(string? email)
    {
        var claims = new List<Claim>();
        if (email != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            Response = { Body = new MemoryStream() }
        };
        return context;
    }

    private static DefaultHttpContext CreateUnauthenticatedContext()
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
            Response = { Body = new MemoryStream() }
        };
    }

    private sealed class StubGuard : IStagingAccessGuard
    {
        private readonly HashSet<string> _allowed;
        public StubGuard(params string[] emails)
        {
            _allowed = emails.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        public bool IsEmailAllowed(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            if (_allowed.Count == 0) return true;
            return _allowed.Contains(email);
        }
        public bool HasNonEmptyAllowlist => _allowed.Count > 0;
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthenticated_PassesThrough()
    {
        var context = CreateUnauthenticatedContext();
        var nextCalled = false;
        var middleware = new StagingAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new StubGuard("badsworm@gmail.com"));

        nextCalled.Should().BeTrue("unauthenticated requests are handled by auth middleware, not this one");
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthenticatedAndAllowed_PassesThrough()
    {
        var context = CreateAuthenticatedContext("badsworm@gmail.com");
        var nextCalled = false;
        var middleware = new StagingAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new StubGuard("badsworm@gmail.com"));

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthenticatedAndNotAllowed_Returns403WithStructuredJson()
    {
        var context = CreateAuthenticatedContext("hacker@evil.com");
        var nextCalled = false;
        var middleware = new StagingAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new StubGuard("badsworm@gmail.com"));

        nextCalled.Should().BeFalse("denied requests must short-circuit the pipeline");
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        context.Response.ContentType.Should().StartWith("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<DeniedResponse>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        payload.Should().NotBeNull();
        payload!.Code.Should().Be("STAGING_ACCESS_DENIED");
        payload.Message.Should().Contain("invite only", "user-facing message must explain access policy");
        payload.Message.Should().Contain("badsworm@gmail.com",
            "wave 1 simplification: contact email embedded in message for direct frontend display");
        payload.ContactEmail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthenticatedWithoutEmailClaim_Returns403()
    {
        var context = CreateAuthenticatedContext(email: null);
        var nextCalled = false;
        var middleware = new StagingAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new StubGuard("badsworm@gmail.com"));

        nextCalled.Should().BeFalse("missing email claim on staging is fail-safe deny");
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_WhenAllowlistEmpty_PassesThroughEvenForUnknownEmails()
    {
        // Default-safe behavior: empty allowlist = open access.
        // Combined with Staging-only wiring + startup warning, this prevents
        // accidental lockout if STAGING_ALLOWED_EMAILS is misconfigured.
        var context = CreateAuthenticatedContext("anyone@example.com");
        var nextCalled = false;
        var middleware = new StagingAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new StubGuard(/* empty allowlist */));

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private sealed record DeniedResponse(string Code, string Message, string ContactEmail);
}
