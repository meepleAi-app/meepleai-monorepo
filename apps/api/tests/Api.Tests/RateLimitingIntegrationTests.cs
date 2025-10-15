using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for rate limiting middleware.
///
/// Feature: Rate limiting for API endpoints
/// As a system
/// I want to enforce rate limits on API requests
/// So that I can prevent abuse and ensure fair resource allocation
/// </summary>
public class RateLimitingIntegrationTests : IntegrationTestBase
{
    public RateLimitingIntegrationTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// Scenario: Requests beyond rate limit return 429 with proper headers
    ///   Given admin user is authenticated with rate limit service
    ///   When user makes request within limit
    ///   Then request succeeds
    ///   When user exceeds rate limit
    ///   Then request returns 429 with retry-after headers
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task RequestsBeyondLimit_Return429WithHeadersAndBody()
    {
        // Given: Admin user is authenticated with rate limit service
        await using var context = await CreateClientContextAsync();

        // When: User makes request within limit
        context.RateLimitService.EnqueueResponse(allowed: true, tokensRemaining: 59, retryAfterSeconds: 0);
        var okResponse = await context.Client.GetAsync("/logs");

        // Then: Request succeeds
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        // When: User exceeds rate limit
        context.RateLimitService.EnqueueResponse(allowed: false, tokensRemaining: 0, retryAfterSeconds: 15);
        var limitedResponse = await context.Client.GetAsync("/logs");

        // Then: Request returns 429 with retry-after headers
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);

        var payload = await limitedResponse.Content.ReadFromJsonAsync<RateLimitErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Rate limit exceeded", payload!.error);
        Assert.Equal(15, payload.retryAfter);
        Assert.Contains("Too many requests", payload.message, StringComparison.OrdinalIgnoreCase);

        // Admin role has 1000 tokens by default configuration
        var expectedLimit = "1000";

        Assert.Equal(expectedLimit, GetSingleHeaderValue(limitedResponse, "X-RateLimit-Limit"));
        Assert.Equal("0", GetSingleHeaderValue(limitedResponse, "X-RateLimit-Remaining"));
        Assert.Equal("15", GetSingleHeaderValue(limitedResponse, "Retry-After"));
        // Cleanup happens automatically via DisposeAsync
    }

    /// <summary>
    /// Scenario: Rate limiter fails open when Redis is unavailable
    ///   Given admin user is authenticated
    ///   And rate limit service throws exception (Redis down)
    ///   When user makes request
    ///   Then request succeeds (fail-open behavior)
    ///   And headers show full limit available
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task RateLimiter_FailsOpen_WhenServiceThrows()
    {
        // Given: Admin user is authenticated
        await using var context = await CreateClientContextAsync();

        // And: Rate limit service throws exception (Redis down)
        context.RateLimitService.FailWith(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));

        // When: User makes request
        var response = await context.Client.GetAsync("/logs");

        // Then: Request succeeds (fail-open behavior)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Headers show full limit available
        // Admin role has 1000 tokens by default configuration
        var expectedLimit = "1000";

        Assert.Equal(expectedLimit, GetSingleHeaderValue(response, "X-RateLimit-Limit"));
        Assert.Equal(expectedLimit, GetSingleHeaderValue(response, "X-RateLimit-Remaining"));
        Assert.False(response.Headers.Contains("Retry-After"));
        // Cleanup happens automatically via DisposeAsync
    }

    private async Task<TestClientContext> CreateClientContextAsync()
    {
        var rateLimitService = new TestRateLimitService();

        var factory = Factory.WithTestServices(services =>
        {
            var descriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(RateLimitService))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(rateLimitService);
            services.AddSingleton<RateLimitService>(sp => sp.GetRequiredService<TestRateLimitService>());
        });

        var client = factory.CreateClient();

        var email = $"rate-limit-admin-{TestRunId}-{Guid.NewGuid():N}@example.com";
        var registerPayload = new RegisterPayload(Email: email, Password: "Password123!", DisplayName: "Rate Limit Admin", Role: null);
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerPayload);
        registerResponse.EnsureSuccessStatusCode();

        // Track user for cleanup
        var userId = await GetUserIdByEmailAsync(factory.Services, email);
        TrackUserId(userId);

        await PromoteUserAsync(factory.Services, email, UserRole.Admin);

        var cookies = ExtractCookies(registerResponse);
        AttachCookies(client, cookies);

        return new TestClientContext(factory, client, rateLimitService);
    }

    private static async Task<string> GetUserIdByEmailAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return user.Id;
    }

    private static IReadOnlyList<string> ExtractCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return Array.Empty<string>();
        }

        return values
            .Select(value => value.Split(';')[0])
            .ToList();
    }

    private static void AttachCookies(HttpClient client, IReadOnlyList<string> cookies)
    {
        client.DefaultRequestHeaders.Remove("Cookie");

        if (cookies.Count > 0)
        {
            var headerValue = string.Join("; ", cookies);
            client.DefaultRequestHeaders.Add("Cookie", headerValue);
        }
    }

    private static async Task PromoteUserAsync(IServiceProvider services, string email, UserRole role)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;

        await db.SaveChangesAsync();
    }

    private static string GetSingleHeaderValue(HttpResponseMessage response, string headerName)
    {
        return response.Headers.TryGetValues(headerName, out var values)
            ? values.Single()
            : response.Content.Headers.GetValues(headerName).Single();
    }

    private sealed record RateLimitErrorResponse(string error, int retryAfter, string message);

    private sealed class TestClientContext : IDisposable, IAsyncDisposable
    {
        public TestClientContext(WebApplicationFactory<Program> factory, HttpClient client, TestRateLimitService rateLimitService)
        {
            Factory = factory;
            Client = client;
            RateLimitService = rateLimitService;
        }

        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }
        public TestRateLimitService RateLimitService { get; }

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestRateLimitService : RateLimitService
    {
        private readonly Queue<RedisResult[]> _responses = new();
        private readonly Mock<IDatabase> _mockDatabase;
        private Exception? _nextException;

        public TestRateLimitService()
            : base(CreateMultiplexer(out var databaseMock), NullLogger<RateLimitService>.Instance, CreateDefaultConfig())
        {
            _mockDatabase = databaseMock;
            _mockDatabase
                .Setup(db => db.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .Returns((string script, RedisKey[] keys, RedisValue[] values, CommandFlags _) =>
                {
                    if (_nextException is { } ex)
                    {
                        _nextException = null;
                        return Task.FromException<RedisResult>(ex);
                    }

                    if (_responses.TryDequeue(out var next))
                    {
                        return Task.FromResult(RedisResult.Create(next));
                    }

                    var maxTokens = (int)values[0];
                    return Task.FromResult(RedisResult.Create(new RedisResult[]
                    {
                        RedisResult.Create(1),
                        RedisResult.Create(maxTokens),
                        RedisResult.Create(0)
                    }));
                });
        }

        public void EnqueueResponse(bool allowed, int tokensRemaining, int retryAfterSeconds)
        {
            _responses.Enqueue(new RedisResult[]
            {
                RedisResult.Create(allowed ? 1 : 0),
                RedisResult.Create(tokensRemaining),
                RedisResult.Create(retryAfterSeconds)
            });
        }

        public void FailWith(Exception exception)
        {
            _nextException = exception;
        }

        private static IConnectionMultiplexer CreateMultiplexer(out Mock<IDatabase> databaseMock)
        {
            databaseMock = new Mock<IDatabase>();
            var multiplexerMock = new Mock<IConnectionMultiplexer>();
            var localDatabaseMock = databaseMock;

            multiplexerMock
                .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(() => localDatabaseMock.Object);

            return multiplexerMock.Object;
        }

        private static IOptions<RateLimitConfiguration> CreateDefaultConfig()
        {
            var config = new RateLimitConfiguration
            {
                Admin = new RoleLimitConfiguration { MaxTokens = 1000, RefillRate = 10.0 },
                Editor = new RoleLimitConfiguration { MaxTokens = 500, RefillRate = 5.0 },
                User = new RoleLimitConfiguration { MaxTokens = 100, RefillRate = 1.0 },
                Anonymous = new RoleLimitConfiguration { MaxTokens = 60, RefillRate = 1.0 }
            };

            return Options.Create(config);
        }
    }
}
