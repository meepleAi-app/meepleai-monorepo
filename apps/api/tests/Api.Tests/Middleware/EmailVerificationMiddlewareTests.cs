using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Middleware;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AuthUserDto = Api.BoundedContexts.Authentication.Application.DTOs.UserDto;

namespace Api.Tests.Middleware;

/// <summary>
/// #4985: a user blocked by email verification could not end their own session, because
/// <c>POST /api/v1/auth/logout</c> was not exempt and answered 403 like any other endpoint.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4985")]
public sealed class EmailVerificationMiddlewareTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static HttpContext BlockedUserRequest(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        // Unverified, never had a grace period: what the seed user had on 2026-11-11.
        var user = new AuthUserDto(
            Guid.NewGuid(), "test@meepleai.com", "Test User", "user", "free",
            Now.UtcDateTime.AddDays(-30), false, null, 1, 0,
            EmailVerified: false, EmailVerifiedAt: null, VerificationGracePeriodEndsAt: null);
        context.Items[nameof(SessionStatusDto)] = new SessionStatusDto(
            true, new Principal(user, Actor: null), Now.UtcDateTime.AddHours(1), Now.UtcDateTime);
        return context;
    }

    private static async Task<(int Status, bool NextCalled)> RunAsync(HttpContext context)
    {
        var nextCalled = false;
        var middleware = new EmailVerificationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<EmailVerificationMiddleware>.Instance);

        await middleware.InvokeAsync(context, new FixedTime());
        return (context.Response.StatusCode, nextCalled);
    }

    [Fact]
    public async Task Logout_IsNotBlocked_ForAnUnverifiedUserPastTheGracePeriod()
    {
        var (status, nextCalled) = await RunAsync(BlockedUserRequest("POST", "/api/v1/auth/logout"));

        nextCalled.Should().BeTrue("a blocked user must be able to end their session");
        status.Should().NotBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OtherEndpoints_StayBlocked_ForTheSameUser()
    {
        var (status, nextCalled) = await RunAsync(BlockedUserRequest("GET", "/api/v1/library"));

        nextCalled.Should().BeFalse();
        status.Should().Be(StatusCodes.Status403Forbidden);
    }
}
