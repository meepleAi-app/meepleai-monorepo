using System.Net;
using Api.Extensions;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// #4912 — the routes that take a session's join code are rate limited per user. The code is the
/// credential since #4883, so trying codes in bulk is trying passwords in bulk: the request past the
/// limit gets a 429, the ones within it do not, the read and the join have separate buckets, and the
/// bucket of one user is not the bucket of another.
/// <para>
/// Own class with its own host: the shared factory disables rate limiting (#3887), and the flag is
/// per host. Every request below uses a code that exists nowhere (404 when it goes through), which
/// is also what an attacker's guesses look like.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "4912")]
public sealed class SessionCodeRateLimitTests : IAsyncLifetime
{
    /// <summary>Six characters of the code alphabet (no I, O, 0, 1) that no session has.</summary>
    private const string UnknownCode = "ZZZZZ9";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public SessionCodeRateLimitTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"code_ratelimit_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
            _factory = IntegrationWebApplicationFactory.Create(connectionString, enableRateLimiting: true);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Database.MigrateAsync();

            _client = _factory.CreateClient();
        }
        catch
        {
            await SafeCleanupAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await SafeCleanupAsync();

    private async Task SafeCleanupAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private async Task<string> NewUserTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);
        return token;
    }

    private Task<HttpResponseMessage> GetByCodeAsync(string token, string route = "game-sessions") =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/{route}/code/{UnknownCode}", token));

    private Task<HttpResponseMessage> JoinByCodeAsync(string token) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/game-sessions/code/{UnknownCode}/join", token, new { displayName = "Giulia" }));

    [Fact(Timeout = 120_000)]
    public async Task GetByCode_TheRequestPastTheLimitIs429_AndTheOnesWithinItAreNot()
    {
        var token = await NewUserTokenAsync();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < RateLimitingServiceExtensions.SessionCodeReadPermitLimit + 1; i++)
        {
            statuses.Add((await GetByCodeAsync(token)).StatusCode);
        }

        statuses.Take(RateLimitingServiceExtensions.SessionCodeReadPermitLimit)
            .Should().AllBeEquivalentTo(HttpStatusCode.NotFound, "within the limit a wrong guess is just a 404");
        statuses[RateLimitingServiceExtensions.SessionCodeReadPermitLimit]
            .Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact(Timeout = 120_000)]
    public async Task JoinByCode_TheRequestPastTheLimitIs429_AndItsBucketIsNotTheReadOne()
    {
        var token = await NewUserTokenAsync();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < RateLimitingServiceExtensions.SessionCodeJoinPermitLimit + 1; i++)
        {
            statuses.Add((await JoinByCodeAsync(token)).StatusCode);
        }

        statuses.Take(RateLimitingServiceExtensions.SessionCodeJoinPermitLimit)
            .Should().NotContain(HttpStatusCode.TooManyRequests, "within the limit the guess reaches the handler");
        statuses[RateLimitingServiceExtensions.SessionCodeJoinPermitLimit]
            .Should().Be(HttpStatusCode.TooManyRequests);

        (await GetByCodeAsync(token)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the read has a bucket of its own");
    }

    [Fact(Timeout = 120_000)]
    public async Task GetByCode_TheBucketIsPerUser_AndSharedByTheTwoLookupRoutes()
    {
        var exhausted = await NewUserTokenAsync();
        var other = await NewUserTokenAsync();

        for (var i = 0; i < RateLimitingServiceExtensions.SessionCodeReadPermitLimit; i++)
        {
            await GetByCodeAsync(exhausted);
        }

        (await GetByCodeAsync(exhausted, route: "live-sessions")).StatusCode
            .Should().Be(HttpStatusCode.TooManyRequests, "a guessed code is a guessed code on either lookup route");
        (await GetByCodeAsync(other)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "another user's bucket is untouched");
    }
}
