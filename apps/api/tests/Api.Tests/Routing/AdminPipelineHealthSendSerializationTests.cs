using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Issue #4059: <c>GET /api/v1/admin/kb/pipeline/health</c> answered 500 on every call. Measured
/// 2026-10-04 against the local stack, three consecutive requests, all 500, same exception each time:
/// <code>
/// System.InvalidOperationException: A second operation was started on this context instance
/// before a previous operation completed.
///   at GetPdfStorageHealthQueryHandler.Handle(GetPdfStorageHealthQuery, CancellationToken)
///   at AdminPipelineEndpoints.GetPipelineHealth(...)
/// </code>
/// The endpoint started four <c>IMediator.Send</c> calls and awaited them with
/// <c>Task.WhenAll</c>. Three of the four handlers
/// (<see cref="GetStepDurationStatsQuery"/> → ProcessingMetricsService,
/// <see cref="GetPdfStorageHealthQuery"/>, <see cref="GetProcessingQueueQuery"/>) read through the
/// one request-scoped <c>MeepleAiDbContext</c>, which EF Core forbids using concurrently.
///
/// <para>
/// ⚠️ The invariant under test is <b>no two sends in flight at once</b>, not "no two EF sends at
/// once". "This send does not touch the DbContext" is a property of the whole MediatR pipeline, not
/// of the handler: every send also runs <c>AuditLoggingBehavior</c>, which holds the request-scoped
/// context and queries it on the <c>[AuditableAction]</c> path. Asserting the narrower invariant
/// would leave the defect one attribute away from returning.
/// </para>
///
/// <para>
/// The detection is not a race. <see cref="RecordingMediator"/> marks itself busy and only then
/// awaits; under <c>Task.WhenAll</c> the endpoint issues the next <c>Send</c> <b>synchronously</b>,
/// before any continuation can run, so the overlap is observed on the calling thread. The 25 ms
/// hold only bounds how long the serialized version takes — it is not what makes the parallel
/// version fail. Verified by perturbation: restoring the <c>Task.WhenAll</c> version turns this
/// test red with "A second operation was started on this context instance".
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4059")]
public sealed class AdminPipelineHealthSendSerializationTests
{
    /// <summary>
    /// Stands in for the request scope's single <c>MeepleAiDbContext</c>: it answers every send,
    /// and throws EF Core's own message — same type, same text — the moment a second send starts
    /// while one is still in flight.
    /// </summary>
    private sealed class RecordingMediator : IMediator
    {
        private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(25);

        private readonly object _gate = new();
        private readonly List<string> _sends = new();
        private string? _inFlight;

        public IReadOnlyList<string> Sends
        {
            get { lock (_gate) { return _sends.ToList(); } }
        }

        public async Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var name = request.GetType().Name;

            lock (_gate)
            {
                _sends.Add(name);
                if (_inFlight is not null)
                {
                    // Verbatim EF Core text (EF Core 9, CoreStrings.ConcurrentMethodInvocation) so a
                    // failure message reads like the production log it stands for.
                    throw new InvalidOperationException(
                        "A second operation was started on this context instance before a previous " +
                        "operation completed. This is usually caused by different threads concurrently " +
                        $"using the same instance of DbContext. (in flight: {_inFlight}, starting: {name})");
                }

                _inFlight = name;
            }

            try
            {
                await Task.Delay(Hold, cancellationToken).ConfigureAwait(false);
                return (TResponse)Respond(request);
            }
            finally
            {
                lock (_gate) { _inFlight = null; }
            }
        }

        private static object Respond(object request) => request switch
        {
            GetServiceHealthStatusesQuery => (IReadOnlyCollection<ServiceHealthStatus>)new[]
            {
                new ServiceHealthStatus("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.Zero),
                new ServiceHealthStatus("embedding", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.Zero),
            },
            GetStepDurationStatsQuery => new Dictionary<string, StepDurationStats>(StringComparer.Ordinal)
            {
                ["Extracting"] = new StepDurationStats("Extracting", 1.0, 1.0, 2.0, 3.0, 10),
            },
            GetPdfStorageHealthQuery => new PdfStorageHealthDto(
                new PostgresInfoDto(3, 42, 1.5),
                new VectorStoreInfoDto(42, true),
                new FileStorageInfoDto(3, 1024, "1 KB", new Dictionary<string, int>(StringComparer.Ordinal) { ["Ready"] = 3 }),
                "healthy",
                DateTime.UtcNow),
            GetProcessingQueueQuery => new PaginatedQueueResponse(
                Array.Empty<ProcessingJobDto>(), 0, 1, 10, 0),
            _ => throw new NotSupportedException($"Unexpected query {request.GetType().Name}"),
        };

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => throw new NotSupportedException();
    }

    /// <summary>
    /// The embedding /metrics scrape is allowed to stay concurrent, so it has to be satisfiable
    /// without a server: a handler that always fails exercises the endpoint's catch path.
    /// </summary>
    private sealed class UnreachableHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new FailingHandler()) { BaseAddress = new Uri("http://embedding.invalid") };

        private sealed class FailingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromException<HttpResponseMessage>(new HttpRequestException("no embedding service in a unit test"));
        }
    }

    [Fact]
    public async Task SendsOneQueryAtATime()
    {
        var mediator = new RecordingMediator();

        var result = await AdminPipelineEndpoints.GetPipelineHealth(
            new UnreachableHttpClientFactory(),
            mediator,
            NullLogger<Program>.Instance,
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull(
            "the endpoint must answer 200 with all four queries served one at a time");
        mediator.Sends.Should().HaveCount(4,
            "each of the four queries must be sent exactly once — no retry, none dropped");
    }

    /// <summary>
    /// Guards the guard: if <see cref="RecordingMediator"/> stopped detecting overlap, the test above
    /// would pass against the <c>Task.WhenAll</c> version too and report nothing. Here two sends are
    /// deliberately started together, the way the endpoint used to start them.
    /// </summary>
    [Fact]
    public async Task RecordingMediator_DetectsOverlap_SoTheTestAboveCannotPassVacuously()
    {
        var mediator = new RecordingMediator();

        var first = mediator.Send(new GetPdfStorageHealthQuery(), TestContext.Current.CancellationToken);
        var second = async () => await mediator.Send(new GetStepDurationStatsQuery(), TestContext.Current.CancellationToken);

        await second.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A second operation was started on this context instance*");
        await first;
    }
}
