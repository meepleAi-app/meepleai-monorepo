using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Issue #3042: per-container CPU%/memory enrichment via the Docker /stats endpoint.
/// The HttpClient is stubbed with a URL-dispatching handler so the container list
/// and each /stats sample can be served independently and the requested URIs asserted.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3042")]
public sealed class DockerProxyServiceTests
{
    // Numbers chosen so the CPU%/memory math lands on round expected values:
    //   cpuDelta = 200M - 100M = 100M ; systemDelta = 2000M - 1000M = 1000M ; cpus = 4
    //   cpuPercent = (100M / 1000M) * 4 * 100 = 40.0
    //   memUsage   = 536_870_912 - 36_870_912(cache) = 500_000_000
    //   memLimit   = 1_073_741_824 (1 GiB)
    private const string RunningStatsJson = """
    {
      "cpu_stats": {
        "cpu_usage": { "total_usage": 200000000, "percpu_usage": [1, 2, 3, 4] },
        "system_cpu_usage": 2000000000,
        "online_cpus": 4
      },
      "precpu_stats": {
        "cpu_usage": { "total_usage": 100000000 },
        "system_cpu_usage": 1000000000
      },
      "memory_stats": {
        "usage": 536870912,
        "limit": 1073741824,
        "stats": { "cache": 36870912 }
      }
    }
    """;

    private static string ListJson(params (string Id, string State)[] containers)
    {
        var items = containers.Select(c => $$"""
        {
          "Id": "{{c.Id}}",
          "Names": ["/name-{{c.Id[..6]}}"],
          "Image": "img:latest",
          "State": "{{c.State}}",
          "Status": "some status",
          "Created": 1700000000,
          "Labels": {}
        }
        """);
        return "[" + string.Join(",", items) + "]";
    }

    /// <summary>Dispatches by URL path; records every requested absolute path for assertions.</summary>
    private sealed class DispatchHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _dispatch;
        // ConcurrentBag: the /stats fan-out issues up to 4 concurrent requests, so the
        // recorder is mutated from multiple threads (a plain List<T>.Add would flake).
        public ConcurrentBag<string> RequestedPaths { get; } = new();

        public DispatchHandler(Func<HttpRequestMessage, HttpResponseMessage> dispatch) => _dispatch = dispatch;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestedPaths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(_dispatch(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (DockerProxyService Svc, DispatchHandler Handler) BuildSubject(
        Func<HttpRequestMessage, HttpResponseMessage> dispatch)
    {
        var handler = new DispatchHandler(dispatch);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://docker-proxy:2375") };
        var svc = new DockerProxyService(httpClient, NullLogger<DockerProxyService>.Instance);
        return (svc, handler);
    }

    [Fact]
    public async Task GetContainersAsync_RunningContainer_ComputesCpuAndMemory()
    {
        var (svc, _) = BuildSubject(req =>
            req.RequestUri!.AbsolutePath.Contains("/stats", StringComparison.Ordinal)
                ? Json(RunningStatsJson)
                : Json(ListJson(("abc123456789def", "running"))));

        var result = await svc.GetContainersAsync(CancellationToken.None);

        result.Should().ContainSingle();
        var c = result[0];
        c.CpuPercent.Should().NotBeNull();
        c.CpuPercent!.Value.Should().BeApproximately(40.0, 0.01);
        c.MemoryUsageBytes.Should().Be(500_000_000);
        c.MemoryLimitBytes.Should().Be(1_073_741_824);
    }

    [Fact]
    public async Task GetContainersAsync_StoppedContainer_MetricsNull_NoStatsCall()
    {
        var (svc, handler) = BuildSubject(req =>
            req.RequestUri!.AbsolutePath.Contains("/stats", StringComparison.Ordinal)
                ? Json(RunningStatsJson)
                : Json(ListJson(("stopped00000000", "exited"))));

        var result = await svc.GetContainersAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].CpuPercent.Should().BeNull();
        result[0].MemoryUsageBytes.Should().BeNull();
        result[0].MemoryLimitBytes.Should().BeNull();
        handler.RequestedPaths.Should().NotContain(p => p.Contains("/stats", StringComparison.Ordinal),
            because: "stopped containers must never hit the /stats endpoint");
    }

    [Fact]
    public async Task GetContainersAsync_StatsCallFails_ListStillReturnsWithNullMetricsForThatContainer()
    {
        // Container A's /stats returns 500; container B's succeeds. A degrades to null,
        // B is still populated, and the list itself is unaffected.
        var (svc, _) = BuildSubject(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (!path.Contains("/stats", StringComparison.Ordinal))
                return Json(ListJson(("aaaaaaaaaaaa0000", "running"), ("bbbbbbbbbbbb0000", "running")));
            return path.Contains("aaaaaaaaaaaa0000", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : Json(RunningStatsJson);
        });

        var result = await svc.GetContainersAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        var a = result.Single(c => c.Id == "aaaaaaaaaaaa");
        var b = result.Single(c => c.Id == "bbbbbbbbbbbb");
        a.CpuPercent.Should().BeNull();
        a.MemoryUsageBytes.Should().BeNull();
        b.CpuPercent!.Value.Should().BeApproximately(40.0, 0.01);
        b.MemoryUsageBytes.Should().Be(500_000_000);
    }

    [Fact]
    public async Task GetContainersAsync_StatsUnreachable_AllNull_ListStillReturns()
    {
        var (svc, _) = BuildSubject(req =>
            req.RequestUri!.AbsolutePath.Contains("/stats", StringComparison.Ordinal)
                ? throw new HttpRequestException("connection refused")
                : Json(ListJson(("cccccccccccc0000", "running"))));

        var result = await svc.GetContainersAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].CpuPercent.Should().BeNull();
        result[0].MemoryUsageBytes.Should().BeNull();
    }

    [Fact]
    public async Task GetContainersAsync_StatsThrowsNonHttpException_DegradesToNull_NoThrow()
    {
        // A non-allow-listed exception (e.g. InvalidOperationException from ReadFromJsonAsync
        // on a bad charset) must NOT abort the fan-out and 500 the whole list — it degrades
        // that one container to null metrics like any other stats failure.
        var (svc, _) = BuildSubject(req =>
            req.RequestUri!.AbsolutePath.Contains("/stats", StringComparison.Ordinal)
                ? throw new InvalidOperationException("bad charset")
                : Json(ListJson(("dddddddddddd0000", "running"))));

        var result = await svc.GetContainersAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].CpuPercent.Should().BeNull();
        result[0].MemoryUsageBytes.Should().BeNull();
    }
}
