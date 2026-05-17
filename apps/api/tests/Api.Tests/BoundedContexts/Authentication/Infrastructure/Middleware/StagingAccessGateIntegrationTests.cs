using System.Net;
using System.Security.Claims;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Services;
using Api.BoundedContexts.Authentication.Infrastructure.Middleware;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Middleware;

/// <summary>
/// Integration tests for the conditional registration of <see cref="StagingAccessMiddleware"/>
/// based on <c>ASPNETCORE_ENVIRONMENT</c>. Rebuilt in #845 to drive the new DB-backed guard
/// via a stub <see cref="IStagingAllowlistRepository"/>, exercising:
/// (1) env-conditional registration, (2) auth claim extraction, (3) FAIL-CLOSED semantics
/// (empty allowlist denies access in Staging).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public class StagingAccessGateIntegrationTests
{
    [Theory]
    [InlineData("Staging", "allowed@example.com", "allowed@example.com", HttpStatusCode.OK,
        "staging gate active + email in allowlist => pass")]
    [InlineData("Staging", "allowed@example.com", "blocked@example.com", HttpStatusCode.Forbidden,
        "staging gate active + email NOT in allowlist => 403")]
    [InlineData("Development", "", "anyone@example.com", HttpStatusCode.OK,
        "dev environment => gate not registered, all authenticated requests pass")]
    [InlineData("Production", "", "anyone@example.com", HttpStatusCode.OK,
        "prod environment => gate not registered, all authenticated requests pass")]
    [InlineData("Staging", "", "anyone@example.com", HttpStatusCode.Forbidden,
        "staging gate active + EMPTY allowlist => FAIL-CLOSED 403 (#845)")]
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
        // Unauthenticated requests must pass through the staging gate even with
        // populated allowlist — the auth middleware handles 401 separately.
        using var server = CreateServer(
            aspNetCoreEnvironment: "Staging",
            allowlist: "allowed@example.com",
            authenticatedEmail: null);
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/probe", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "unauthenticated requests must NOT be denied by the staging gate");
    }

    /// <summary>
    /// Builds a TestServer that registers the new DB-backed <see cref="StagingAccessGuard"/>
    /// with a stub <see cref="IStagingAllowlistRepository"/>, mirroring the production wiring.
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

                webHost.ConfigureServices(services =>
                {
                    services.AddMemoryCache();
                    services.AddSingleton<IStagingAllowlistRepository>(
                        new StubAllowlistRepository(ParseAllowlist(allowlist)));
                    services.AddSingleton<IStagingAccessGuard, StagingAccessGuard>();
                });

                webHost.Configure((context, app) =>
                {
                    // Simulate authentication
                    app.Use(async (ctx, next) =>
                    {
                        if (authenticatedEmail is not null)
                        {
                            var identity = new ClaimsIdentity(
                                new[] { new Claim(ClaimTypes.Email, authenticatedEmail) },
                                "TestAuth");
                            ctx.User = new ClaimsPrincipal(identity);
                        }
                        await next().ConfigureAwait(false);
                    });

                    // Mirror of WebApplicationExtensions staging gate.
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

    private static IReadOnlySet<string> ParseAllowlist(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Stub repository returning a fixed set of emails. Only <see cref="GetAllowedEmailsAsync"/>
    /// is exercised by the guard hot-path; other members throw to surface unintended use.
    /// </summary>
    private sealed class StubAllowlistRepository : IStagingAllowlistRepository
    {
        private readonly IReadOnlySet<string> _emails;

        public StubAllowlistRepository(IReadOnlySet<string> emails) => _emails = emails;

        public Task<IReadOnlySet<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_emails);

        public Task<StagingAllowlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StagingAllowlistEntry>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StagingAllowlistEntry?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(StagingAllowlistEntry entity, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(StagingAllowlistEntry entity, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(StagingAllowlistEntry entity, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
