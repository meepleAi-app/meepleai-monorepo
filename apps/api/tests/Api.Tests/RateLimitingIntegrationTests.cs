using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests;

public class RateLimitingIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _fixture;

    public RateLimitingIntegrationTests(WebApplicationFactoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RequestsBeyondLimit_Return429WithHeadersAndBody()
    {
        using var context = CreateClientContext();

        context.RateLimitService.EnqueueResponse(allowed: true, tokensRemaining: 59, retryAfterSeconds: 0);
        var okResponse = await context.Client.GetAsync("/logs");
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        context.RateLimitService.EnqueueResponse(allowed: false, tokensRemaining: 0, retryAfterSeconds: 15);
        var limitedResponse = await context.Client.GetAsync("/logs");

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);

        var payload = await limitedResponse.Content.ReadFromJsonAsync<RateLimitErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Rate limit exceeded", payload!.error);
        Assert.Equal(15, payload.retryAfter);
        Assert.Contains("Too many requests", payload.message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("60", GetSingleHeaderValue(limitedResponse, "X-RateLimit-Limit"));
        Assert.Equal("0", GetSingleHeaderValue(limitedResponse, "X-RateLimit-Remaining"));
        Assert.Equal("15", GetSingleHeaderValue(limitedResponse, "Retry-After"));
    }

    [Fact]
    public async Task RateLimiter_FailsOpen_WhenServiceThrows()
    {
        using var context = CreateClientContext();

        context.RateLimitService.FailWith(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));

        var response = await context.Client.GetAsync("/logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("60", GetSingleHeaderValue(response, "X-RateLimit-Limit"));
        Assert.Equal("60", GetSingleHeaderValue(response, "X-RateLimit-Remaining"));
        Assert.False(response.Headers.Contains("Retry-After"));
    }

    private TestClientContext CreateClientContext()
    {
        var rateLimitService = new TestRateLimitService();

        var factory = _fixture.WithTestServices(services =>
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
        return new TestClientContext(factory, client, rateLimitService);
    }

    private static string GetSingleHeaderValue(HttpResponseMessage response, string headerName)
    {
        return response.Headers.TryGetValues(headerName, out var values)
            ? values.Single()
            : response.Content.Headers.GetValues(headerName).Single();
    }

    private sealed record RateLimitErrorResponse(string error, int retryAfter, string message);

    private sealed class TestClientContext : IDisposable
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
    }

    private sealed class TestRateLimitService : RateLimitService
    {
        private readonly Queue<RedisResult[]> _responses = new();
        private readonly Mock<IDatabase> _mockDatabase;
        private Exception? _nextException;

        public TestRateLimitService()
            : base(CreateMultiplexer(out var databaseMock), NullLogger<RateLimitService>.Instance)
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

            multiplexerMock
                .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(() => databaseMock.Object);

            return multiplexerMock.Object;
        }
    }
}
