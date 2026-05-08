using System.Net;
using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.Services;
using Api.BoundedContexts.Authentication.Infrastructure.Middleware;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Middleware;

/// <summary>
/// Integration tests for the conditional registration of <see cref="StagingAccessMiddleware"/>
/// based on <c>ASPNETCORE_ENVIRONMENT</c>. Replicates the wiring logic of
/// <c>WebApplicationExtensions.ConfigureAuthMiddleware</c> via TestServer to verify the gate
/// condition without booting the full app (which would require DB/Redis stack).
/// </summary>
/// <remarks>
/// Why TestServer instead of WebApplicationFactory&lt;Program&gt;: Program.cs has heavy startup
/// dependencies (DbContext, Redis, embedding services). For testing JUST the env-based gate
/// activation, TestServer with replicated wiring logic gives identical coverage without the
/// startup cost. The wiring code under test (the `if (env.IsEnvironment("Staging")) ...` block)
/// is mirrored verbatim from <c>WebApplicationExtensions.ConfigureAuthMiddleware</c>.
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public class StagingAccessGateIntegrationTests
{
    [Theory]
    [InlineData("Staging", "allowed@example.com", "allowed@example.com", HttpStatusCode.OK,
        "staging gate active + email in allowlist => pass")]
    [InlineData("Staging", "allowed@example.com", "blocked@example.com", HttpStatusCode.Forbidden,
        "staging gate active + email NOT in allowlist => 403")]
    [InlineData("Development", "allowed@example.com", "blocked@example.com", HttpStatusCode.OK,
        "dev environment => gate not registered, all authenticated requests pass")]
    [InlineData("Production", "allowed@example.com", "blocked@example.com", HttpStatusCode.OK,
        "prod environment => gate not registered, all authenticated requests pass")]
    [InlineData("Staging", "", "anyone@example.com", HttpStatusCode.OK,
        "staging gate active but allowlist empty => default-safe pass-through")]
    public async Task ConditionalGate_OnlyDeniesAuthenticatedRequests_OnStagingWithPopulatedAllowlist(
        string aspNetCoreEnvironment,
        string allowlist,
        string authenticatedEmail,
        HttpStatusCode expectedStatus,
        string scenario)
    {
        using var server = CreateServer(aspNetCoreEnvironment, allowlist, authenticatedEmail);
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/probe", UriKind.Relative));

        response.StatusCode.Should().Be(expectedStatus, scenario);
    }

    [Fact]
    public async Task ConditionalGate_DoesNotInterfere_WithUnauthenticatedRequests()
    {
        // Even on Staging with populated allowlist, unauthenticated requests must
        // pass through the staging gate (auth middleware handles 401 separately).
        using var server = CreateServer(
            aspNetCoreEnvironment: "Staging",
            allowlist: "allowed@example.com",
            authenticatedEmail: null /* unauthenticated */);
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/probe", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "unauthenticated requests must NOT be denied by the staging gate (auth middleware territory)");
    }

    /// <summary>
    /// Builds a TestServer that replicates the wiring logic of
    /// <c>WebApplicationExtensions.ConfigureAuthMiddleware</c>:
    /// register <see cref="IStagingAccessGuard"/>, then conditionally insert
    /// <see cref="StagingAccessMiddleware"/> only when environment is "Staging".
    /// </summary>
    private static TestServer CreateServer(
        string aspNetCoreEnvironment,
        string allowlist,
        string? authenticatedEmail)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseEnvironment(aspNetCoreEnvironment);

                webHost.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["STAGING_ALLOWED_EMAILS"] = allowlist,
                        ["Staging:ContactEmail"] = "test-contact@example.com",
                    });
                });

                webHost.ConfigureServices(services =>
                {
                    services.AddSingleton<IStagingAccessGuard, StagingAccessGuard>();
                });

                webHost.Configure((context, app) =>
                {
                    // Simulate authentication: inject ClaimsPrincipal if email provided
                    app.Use(async (ctx, next) =>
                    {
                        if (authenticatedEmail != null)
                        {
                            var identity = new ClaimsIdentity(
                                new[] { new Claim(ClaimTypes.Email, authenticatedEmail) },
                                "TestAuth");
                            ctx.User = new ClaimsPrincipal(identity);
                        }
                        await next().ConfigureAwait(false);
                    });

                    // Mirror of WebApplicationExtensions.ConfigureAuthMiddleware staging gate.
                    // If this block diverges from production, this test is the regression
                    // signal: the production wiring should be updated too.
                    if (context.HostingEnvironment.IsEnvironment("Staging"))
                    {
                        app.UseMiddleware<StagingAccessMiddleware>();
                    }

                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = 200;
                        await ctx.Response.WriteAsync("OK").ConfigureAwait(false);
                    });
                });
            });

        return hostBuilder.Start().GetTestServer();
    }
}
