using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Services;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Connects to Docker Socket Proxy (tecnativa/docker-socket-proxy) for read-only container info.
/// Issue #138: IDockerProxyService implementation.
/// </summary>
internal sealed class DockerProxyService : IDockerProxyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DockerProxyService> _logger;

    public DockerProxyService(HttpClient httpClient, ILogger<DockerProxyService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ContainerInfoDto>> GetContainersAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1.43/containers/json?all=true", ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var containers = await response.Content
                .ReadFromJsonAsync<List<DockerContainerResponse>>(ct)
                .ConfigureAwait(false);

            if (containers is null || containers.Count == 0)
                return Array.Empty<ContainerInfoDto>().AsReadOnly();

            // Issue #3042: enrich each RUNNING container with live CPU%/memory from
            // /stats. Stopped containers are skipped (no stats to read). The fan-out
            // is bounded to keep the list latency in check; per-call failures degrade
            // gracefully to null metrics (the list still returns).
            var stats = await FetchRunningContainerStatsAsync(containers, ct).ConfigureAwait(false);

            return containers.Select(c =>
            {
                stats.TryGetValue(c.Id, out var s);
                return new ContainerInfoDto(
                    Id: c.Id[..12],
                    Name: c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                    Image: c.Image,
                    State: c.State,
                    Status: c.Status,
                    Created: DateTimeOffset.FromUnixTimeSeconds(c.Created).UtcDateTime,
                    Labels: c.Labels ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    CpuPercent: s?.CpuPercent,
                    MemoryUsageBytes: s?.MemoryUsageBytes,
                    MemoryLimitBytes: s?.MemoryLimitBytes);
            }).ToList().AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Docker Socket Proxy");
            return Array.Empty<ContainerInfoDto>().AsReadOnly();
        }
    }

    /// <summary>
    /// Fan-out the Docker /stats call for every running container, bounded at
    /// <c>MaxDegreeOfParallelism = 4</c>. The dictionary is keyed on the FULL
    /// container id (the list DTO later truncates to 12 chars). Non-running
    /// containers never hit the endpoint.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, ContainerStats>> FetchRunningContainerStatsAsync(
        IReadOnlyList<DockerContainerResponse> containers, CancellationToken ct)
    {
        var running = containers
            .Where(c => string.Equals(c.State, "running", StringComparison.Ordinal))
            .ToList();

        var result = new ConcurrentDictionary<string, ContainerStats>(StringComparer.Ordinal);
        if (running.Count == 0)
            return result;

        await Parallel.ForEachAsync(
            running,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (c, token) =>
            {
                var s = await GetContainerStatsAsync(c.Id, token).ConfigureAwait(false);
                if (s is not null)
                    result[c.Id] = s;
            }).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Reads a one-shot (<c>stream=false</c>) stats sample for a single container and
    /// computes CPU%/memory. Returns null on any failure (unreachable proxy, 5xx,
    /// timeout, malformed body) so the caller can degrade to null metrics.
    /// A 3s per-call deadline caps a hung /stats read.
    /// </summary>
    private async Task<ContainerStats?> GetContainerStatsAsync(string containerId, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var response = await _httpClient
                .GetAsync($"/v1.43/containers/{containerId}/stats?stream=false", cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var sample = await response.Content
                .ReadFromJsonAsync<DockerStatsResponse>(cts.Token)
                .ConfigureAwait(false);

            return sample is null ? null : ComputeStats(sample);
        }
        catch (Exception ex)
        {
            // Enriching with stats is best-effort: ANY failure reading one container's
            // stats degrades to null metrics so the list still returns. We deliberately
            // catch broadly — HttpRequestException, JsonException, the InvalidOperationException
            // that ReadFromJsonAsync raises on a bad charset, the 3s per-call deadline, etc.
            // The ONLY exception that must propagate is a genuine OUTER-token cancellation
            // (the caller is being torn down); the 3s deadline fires on cts.Token, not ct,
            // so ThrowIfCancellationRequested lets it fall through to the null return.
            ct.ThrowIfCancellationRequested();
            _logger.LogDebug(ex, "Failed to read stats for container {ContainerId}", containerId);
            return null;
        }
    }

    /// <summary>
    /// Docker CPU% formula (per the CLI): <c>(cpuDelta / systemDelta) * onlineCpus * 100</c>,
    /// where the deltas are between the current and previous sample embedded in the
    /// <c>stream=false</c> response. Memory subtracts the inactive-file/cache offset
    /// (cgroup v2/v1 aware) from usage, mirroring `docker stats`.
    /// </summary>
    private static ContainerStats ComputeStats(DockerStatsResponse s)
    {
        double cpuPercent = 0.0;
        var cpu = s.CpuStats;
        var precpu = s.PreCpuStats;
        if (cpu?.CpuUsage is not null && precpu?.CpuUsage is not null
            && cpu.SystemCpuUsage is not null && precpu.SystemCpuUsage is not null)
        {
            double cpuDelta = (double)(cpu.CpuUsage.TotalUsage ?? 0) - (precpu.CpuUsage.TotalUsage ?? 0);
            double systemDelta = (double)(cpu.SystemCpuUsage.Value) - precpu.SystemCpuUsage.Value;
            double cpus = cpu.OnlineCpus ?? cpu.CpuUsage.PercpuUsage?.Count ?? 1;
            if (systemDelta > 0 && cpuDelta > 0)
                cpuPercent = (cpuDelta / systemDelta) * cpus * 100.0;
        }

        long memUsage = 0;
        long memLimit = 0;
        var mem = s.MemoryStats;
        if (mem is not null)
        {
            ulong usage = mem.Usage ?? 0;
            ulong offset = MemoryOffset(usage, mem.Stats);
            memUsage = (long)(usage - offset);
            memLimit = (long)(mem.Limit ?? 0);
        }

        return new ContainerStats(cpuPercent, memUsage, memLimit);
    }

    /// <summary>
    /// Returns the memory offset to subtract from <c>usage</c> so the reported figure
    /// excludes reclaimable page cache — matching `docker stats`. Tries the cgroup v2
    /// key first, then v1 fallbacks; only subtracts when the value is strictly less
    /// than usage (defensive against malformed samples).
    /// </summary>
    private static ulong MemoryOffset(ulong usage, IReadOnlyDictionary<string, ulong>? stats)
    {
        if (stats is null) return 0;
        foreach (var key in new[] { "total_inactive_file", "inactive_file", "cache" })
        {
            if (stats.TryGetValue(key, out var v) && v < usage)
                return v;
        }
        return 0;
    }

    public async Task<ContainerDetailDto?> GetContainerAsync(string containerId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/v1.43/containers/{containerId}/json", ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var detail = await response.Content
                .ReadFromJsonAsync<DockerContainerDetailResponse>(ct)
                .ConfigureAwait(false);

            if (detail is null) return null;

            return new ContainerDetailDto(
                Id: detail.Id[..12],
                Name: detail.Name.TrimStart('/'),
                Image: detail.Config?.Image ?? "unknown",
                State: detail.State?.Status ?? "unknown",
                Status: detail.State?.Status ?? "unknown",
                Created: DateTime.Parse(detail.Created, CultureInfo.InvariantCulture),
                StartedAt: DateTime.TryParse(detail.State?.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var started) ? started : DateTime.MinValue,
                MemoryUsageBytes: 0, // Stats endpoint needed for live metrics
                CpuPercent: 0,
                Labels: detail.Config?.Labels ?? new Dictionary<string, string>(StringComparer.Ordinal),
                Ports: detail.NetworkSettings?.Ports?.SelectMany(p =>
                    p.Value?.Select(b => new PortMapping(
                        int.TryParse(p.Key.Split('/')[0], CultureInfo.InvariantCulture, out var pp) ? pp : 0,
                        int.TryParse(b.HostPort, CultureInfo.InvariantCulture, out var hp) ? hp : 0,
                        p.Key.Contains('/', StringComparison.Ordinal) ? p.Key.Split('/')[1] : "tcp"
                    )) ?? Enumerable.Empty<PortMapping>()
                ).ToList().AsReadOnly() ?? Array.Empty<PortMapping>().AsReadOnly()
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get container {ContainerId}", containerId);
            return null;
        }
    }

    public async Task<ContainerLogsDto> GetContainerLogsAsync(
        string containerId, int tailLines = 100, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/v1.43/containers/{containerId}/logs?stdout=true&stderr=true&tail={tailLines}&timestamps=true",
                ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Docker log lines have 8-byte header prefix for multiplexed streams
            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Length > 8 ? line[8..] : line)
                .ToList();

            var containerInfo = await GetContainerAsync(containerId, ct).ConfigureAwait(false);

            return new ContainerLogsDto(
                ContainerId: containerId,
                ContainerName: containerInfo?.Name ?? containerId,
                Lines: lines.AsReadOnly(),
                FetchedAt: DateTime.UtcNow);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get logs for container {ContainerId}", containerId);
            return new ContainerLogsDto(containerId, containerId, Array.Empty<string>(), DateTime.UtcNow);
        }
    }
}

// Docker API response DTOs (internal)
internal record DockerContainerResponse(
    string Id,
    List<string> Names,
    string Image,
    string State,
    string Status,
    long Created,
    Dictionary<string, string>? Labels);

internal record DockerContainerDetailResponse(
    string Id,
    string Name,
    string Created,
    DockerStateResponse? State,
    DockerConfigResponse? Config,
    DockerNetworkSettingsResponse? NetworkSettings);

internal record DockerStateResponse(string Status, string StartedAt);
internal record DockerConfigResponse(string Image, Dictionary<string, string>? Labels);
internal record DockerNetworkSettingsResponse(
    Dictionary<string, List<DockerPortBinding>?>? Ports);
internal record DockerPortBinding(string HostIp, string HostPort);

// Issue #3042: computed per-container metrics (internal projection, not a DTO).
internal sealed record ContainerStats(double CpuPercent, long MemoryUsageBytes, long MemoryLimitBytes);

// Docker /stats?stream=false response DTOs. The endpoint uses snake_case keys,
// so [JsonPropertyName] is required — STJ's case-insensitive matching does NOT
// bridge the underscores (e.g. "cpu_stats" ≠ CpuStats).
internal sealed record DockerStatsResponse(
    [property: JsonPropertyName("cpu_stats")] DockerCpuStats? CpuStats,
    [property: JsonPropertyName("precpu_stats")] DockerCpuStats? PreCpuStats,
    [property: JsonPropertyName("memory_stats")] DockerMemoryStats? MemoryStats);

internal sealed record DockerCpuStats(
    [property: JsonPropertyName("cpu_usage")] DockerCpuUsage? CpuUsage,
    [property: JsonPropertyName("system_cpu_usage")] ulong? SystemCpuUsage,
    [property: JsonPropertyName("online_cpus")] int? OnlineCpus);

internal sealed record DockerCpuUsage(
    [property: JsonPropertyName("total_usage")] ulong? TotalUsage,
    [property: JsonPropertyName("percpu_usage")] List<ulong>? PercpuUsage);

internal sealed record DockerMemoryStats(
    [property: JsonPropertyName("usage")] ulong? Usage,
    [property: JsonPropertyName("limit")] ulong? Limit,
    [property: JsonPropertyName("stats")] Dictionary<string, ulong>? Stats);
