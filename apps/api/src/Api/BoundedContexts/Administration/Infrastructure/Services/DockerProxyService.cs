using System.Globalization;
using System.Net.Http.Json;
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

            return containers?.Select(c => new ContainerInfoDto(
                Id: c.Id[..12],
                Name: c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                Image: c.Image,
                State: c.State,
                Status: c.Status,
                Created: DateTimeOffset.FromUnixTimeSeconds(c.Created).UtcDateTime,
                Labels: c.Labels ?? new Dictionary<string, string>(StringComparer.Ordinal)
            )).ToList().AsReadOnly() ?? Array.Empty<ContainerInfoDto>().AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Docker Socket Proxy");
            return Array.Empty<ContainerInfoDto>().AsReadOnly();
        }
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
