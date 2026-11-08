using System.Net;
using Api.Extensions;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// #4050: one host for the class, a fresh database per test. The one thing this host does differently
/// from the shared one is rate limiting ON (#3887: the flag is per host). The limiter's buckets live in
/// the host and so outlive a test, which is fine here: every test creates its own users, and the
/// buckets are per user.
/// </summary>
public sealed class SessionCodeRateLimitTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "code_ratelimit")
{
    protected override bool EnableRateLimiting => true;
}

/// <summary>
/// #4912 — the routes that take a session's join code are rate limited per user. The code is the
/// credential since #4883, so trying codes in bulk is trying passwords in bulk: the request past the
/// limit gets a 429, the ones within it do not, the read and the join have separate buckets, and the
/// bucket of one user is not the bucket of another.
/// <para>
/// Every request below uses a code that exists nowhere (404 when it goes through), which is also what
/// an attacker's guesses look like.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "4912")]
public sealed class SessionCodeRateLimitTests
    : IClassFixture<SessionCodeRateLimitTestsHostFixture>, IAsyncLifetime
{
    /// <summary>Six characters of the code alphabet (no I, O, 0, 1) that no session has.</summary>
    private const string UnknownCode = "ZZZZZ9";

    private readonly SessionCodeRateLimitTestsHostFixture _hostFixture;
    private HttpClient _client = null!;

    public SessionCodeRateLimitTests(SessionCodeRateLimitTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // #4050: DEVE precedere ogni uso dell'host, altrimenti gli utenti finiscono nel database di avvio.
        await _hostFixture.BeginTestAsync();
        _client = _hostFixture.Client;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<string> NewUserTokenAsync()
    {
        using var scope = _hostFixture.CreateScopeOnCurrentDatabase();
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
