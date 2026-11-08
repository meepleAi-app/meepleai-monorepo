using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4512: the embedding service's Prometheus metrics used to be read by two endpoints, each with
/// its own <c>IHttpClientFactory</c>, its own parser and its own timeout (5 s and 3 s). One query now
/// reads them, through the one reader the pipeline health also uses. The body below is the format
/// <c>apps/embedding-service/main.py</c> writes: counters as integers, sums as floats.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4512")]
public sealed class GetEmbeddingServiceMetricsQueryHandlerTests
{
    private const string MetricsBody =
        "# HELP embed_requests_total Total embedding requests\n" +
        "# TYPE embed_requests_total counter\n" +
        "embed_requests_total 8\n" +
        "# HELP embed_failures_total Total embedding failures\n" +
        "# TYPE embed_failures_total counter\n" +
        "embed_failures_total 1\n" +
        "# HELP embed_duration_ms_sum Cumulative embedding latency in ms\n" +
        "# TYPE embed_duration_ms_sum counter\n" +
        "embed_duration_ms_sum 1001.0\n" +
        "# HELP embed_total_chars_sum Cumulative characters processed\n" +
        "# TYPE embed_total_chars_sum counter\n" +
        "embed_total_chars_sum 52344.6\n";

    [Fact]
    public async Task PrometheusBody_IsParsed()
    {
        var metrics = await HandleAsync(new StubHandler().Answer(HttpStatusCode.OK, MetricsBody));

        metrics.RequestsTotal.Should().Be(8);
        metrics.FailuresTotal.Should().Be(1);
        metrics.DurationMsSum.Should().Be(1001.0);
        metrics.TotalCharsSum.Should().Be(52345, "the character sum is shown rounded to a whole number");
        metrics.AvgDurationMs.Should().Be(125.12, "1001 / 8 = 125.125, rounded to two decimals");
        metrics.FailureRate.Should().Be(12.5, "1 failure in 8 requests is 12.5 %");
    }

    [Fact]
    public async Task NoRequestsYet_DoesNotDivideByZero()
    {
        var metrics = await HandleAsync(new StubHandler().Answer(HttpStatusCode.OK,
            "embed_requests_total 0\nembed_failures_total 0\nembed_duration_ms_sum 0.0\nembed_total_chars_sum 0.0\n"));

        metrics.Should().Be(EmbeddingServiceMetricsDto.Unavailable);
    }

    [Fact]
    public async Task NonSuccessStatus_IsAllZeros()
    {
        var metrics = await HandleAsync(new StubHandler().Answer(HttpStatusCode.ServiceUnavailable, MetricsBody));

        metrics.Should().Be(EmbeddingServiceMetricsDto.Unavailable,
            "a body that came with an error status is not the service's metrics");
    }

    [Fact]
    public async Task HttpFailure_IsAllZeros()
    {
        var metrics = await HandleAsync(new StubHandler().Throw(new HttpRequestException("connection refused")));

        metrics.Should().Be(EmbeddingServiceMetricsDto.Unavailable);
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => HandleAsync(new StubHandler().Answer(HttpStatusCode.OK, MetricsBody), ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "a caller that gave up must not be answered with zeros as if the service were down");
    }

    /// <summary>
    /// The reader's own timeout is a failure of the service, not a cancellation by the caller: it
    /// must end in zeros, not in an exception. The clock is virtual, so the test does not wait.
    /// </summary>
    [Fact]
    public async Task ServiceThatNeverAnswers_TimesOutIntoAllZeros()
    {
        var time = new FakeTimeProvider();
        var stub = new StubHandler().Hang();

        var pending = HandleAsync(stub, time);
        await stub.Started.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        pending.IsCompleted.Should().BeFalse("the service has not answered and the timeout has not elapsed");

        time.Advance(EmbeddingServiceMetricsReader.Timeout);

        var metrics = await pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        metrics.Should().Be(EmbeddingServiceMetricsDto.Unavailable);
    }

    /// <summary>
    /// The web validates <c>/admin/embedding/metrics</c> with <c>EmbeddingServiceMetricsSchema</c>
    /// (<c>apps/web/src/lib/api/schemas/admin-knowledge-base.schemas.ts</c>): these six keys, in the
    /// API's camelCase.
    /// </summary>
    [Fact]
    public void Dto_SerializesToTheKeysTheWebValidates()
    {
        var json = JsonSerializer.Serialize(
            EmbeddingServiceMetricsDto.Unavailable,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal(
            "requestsTotal", "failuresTotal", "durationMsSum", "totalCharsSum", "avgDurationMs", "failureRate");
    }

    private static Task<EmbeddingServiceMetricsDto> HandleAsync(
        StubHandler stub, TimeProvider? time = null, CancellationToken? ct = null)
    {
        var factory = new StubFactory(stub);
        var reader = new EmbeddingServiceMetricsReader(
            factory, time ?? TimeProvider.System, NullLogger<EmbeddingServiceMetricsReader>.Instance);
        var handler = new GetEmbeddingServiceMetricsQueryHandler(reader);

        return handler.Handle(new GetEmbeddingServiceMetricsQuery(), ct ?? TestContext.Current.CancellationToken);
    }

    private sealed class StubFactory(StubHandler stub) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            name.Should().Be(EmbeddingServiceMetricsReader.HttpClientName);
            return new HttpClient(stub, disposeHandler: false) { BaseAddress = new Uri("http://embedding-service:8000") };
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private (HttpStatusCode Status, string Body)? _answer;
        private Exception? _failure;
        private bool _hang;

        public Task Started => _started.Task;

        public StubHandler Answer(HttpStatusCode status, string body)
        {
            _answer = (status, body);
            return this;
        }

        public StubHandler Throw(Exception failure)
        {
            _failure = failure;
            return this;
        }

        public StubHandler Hang()
        {
            _hang = true;
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri!.AbsolutePath.Should().Be("/metrics");
            cancellationToken.ThrowIfCancellationRequested();
            _started.TrySetResult();

            if (_hang)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            if (_failure is not null)
            {
                throw _failure;
            }

            var (status, body) = _answer ?? (HttpStatusCode.NotFound, "");
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
