using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;

/// <summary>
/// Unit tests for <see cref="WikimediaCircuitBreakerHandler"/>. Issue #1823
/// Wave 3 M10 (ADR DEC-3f).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikimediaCircuitBreakerHandlerTests
{
    private static HttpClient CreateClientWithInnerHandler(HttpMessageHandler inner, string clientName = "test")
    {
        var breaker = new WikimediaCircuitBreakerHandler(
            NullLogger<WikimediaCircuitBreakerHandler>.Instance,
            clientName)
        {
            InnerHandler = inner,
        };

        return new HttpClient(breaker)
        {
            BaseAddress = new Uri("https://test.example.com/")
        };
    }

    [Fact]
    public async Task Sends_3xxAnd2xxResponses_Pass_Through_WithoutOpeningCircuit()
    {
        // 4 successful responses → circuit must NOT open.
        var inner = new SequencedResponseHandler(new[]
        {
            HttpStatusCode.OK,
            HttpStatusCode.OK,
            HttpStatusCode.OK,
            HttpStatusCode.OK,
        });
        var client = CreateClientWithInnerHandler(inner);

        for (var i = 0; i < 4; i++)
        {
            var resp = await client.GetAsync("/probe");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Sends_NonTransient4xx_DoesNotCountTowardsBreaker()
    {
        // Three consecutive 404s should NOT open the circuit (4xx is client-side
        // and the breaker only handles transient upstream failures per DEC-3f).
        var inner = new SequencedResponseHandler(new[]
        {
            HttpStatusCode.NotFound,
            HttpStatusCode.NotFound,
            HttpStatusCode.NotFound,
            HttpStatusCode.NotFound,
        });
        var client = CreateClientWithInnerHandler(inner);

        // The 4th call MUST hit the inner handler (no BrokenCircuitException).
        for (var i = 0; i < 4; i++)
        {
            var resp = await client.GetAsync("/missing");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        inner.CallCount.Should().Be(4,
            "404 is client-side — the breaker MUST forward every request to the inner handler");
    }

    [Fact]
    public async Task After3Consecutive5xx_4thRequest_ThrowsBrokenCircuitException()
    {
        var inner = new SequencedResponseHandler(new[]
        {
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            // 4th call SHOULD be rejected by the breaker BEFORE reaching inner.
            HttpStatusCode.OK,
        });
        var client = CreateClientWithInnerHandler(inner);

        // First 3 calls return 5xx and tick the failure counter.
        for (var i = 0; i < WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold; i++)
        {
            var resp = await client.GetAsync("/sparql");
            ((int)resp.StatusCode).Should().BeGreaterThanOrEqualTo(500);
        }

        // 4th call is short-circuited by the open breaker.
        var act = async () => await client.GetAsync("/sparql");
        await act.Should().ThrowAsync<BrokenCircuitException>(
            "after 3 consecutive 5xx within the sampling window the circuit OPENs and rejects subsequent calls without reaching the inner handler");

        inner.CallCount.Should().Be(WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold,
            "the 4th call MUST be rejected without dispatching to the inner handler");
    }

    [Fact]
    public async Task HttpRequestException_CountsAsFailure_AndCanOpenCircuit()
    {
        // 3 consecutive network failures → 4th call short-circuited.
        var inner = new ThrowingHandler(new HttpRequestException("simulated"));
        var client = CreateClientWithInnerHandler(inner);

        // Drive the breaker with 3 failures.
        for (var i = 0; i < WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold; i++)
        {
            var act = async () => await client.GetAsync("/sparql");
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        // 4th call: breaker is OPEN.
        var openCall = async () => await client.GetAsync("/sparql");
        await openCall.Should().ThrowAsync<BrokenCircuitException>();

        inner.CallCount.Should().Be(WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold,
            "HttpRequestException MUST count towards the failure threshold");
    }

    [Fact]
    public void Constructor_RequiresClientName()
    {
        var act = () => new WikimediaCircuitBreakerHandler(
            NullLogger<WikimediaCircuitBreakerHandler>.Instance,
            clientName: "  ");

        act.Should().Throw<ArgumentException>().WithMessage("*Client name required*");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test helpers
    // ──────────────────────────────────────────────────────────────────────

    private sealed class SequencedResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _statuses;
        public int CallCount { get; private set; }

        public SequencedResponseHandler(HttpStatusCode[] statuses)
        {
            _statuses = statuses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var status = _statuses[Math.Min(CallCount, _statuses.Length - 1)];
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;
        public int CallCount { get; private set; }

        public ThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromException<HttpResponseMessage>(_exception);
        }
    }
}
