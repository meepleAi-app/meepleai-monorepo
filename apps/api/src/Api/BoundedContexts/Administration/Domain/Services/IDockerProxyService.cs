namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Read-only Docker container information via socket proxy.
/// Issue #138: IDockerProxyService backend.
/// </summary>
public interface IDockerProxyService
{
    /// <summary>Gets all containers (running and stopped).</summary>
    Task<IReadOnlyList<ContainerInfoDto>> GetContainersAsync(CancellationToken ct = default);

    /// <summary>Gets detailed info for a specific container.</summary>
    Task<ContainerDetailDto?> GetContainerAsync(string containerId, CancellationToken ct = default);

    /// <summary>Gets recent logs for a container.</summary>
    Task<ContainerLogsDto> GetContainerLogsAsync(
        string containerId, int tailLines = 100, CancellationToken ct = default);
}

public record ContainerInfoDto(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    DateTime Created,
    IReadOnlyDictionary<string, string> Labels,
    // Issue #3042: live per-container metrics from the Docker /stats endpoint.
    // Null when the container is not running or the stats call fails/unreachable.
    double? CpuPercent = null,
    long? MemoryUsageBytes = null,
    long? MemoryLimitBytes = null);

public record ContainerDetailDto(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    DateTime Created,
    DateTime StartedAt,
    long MemoryUsageBytes,
    double CpuPercent,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<PortMapping> Ports);

public record PortMapping(int PrivatePort, int PublicPort, string Type);

public record ContainerLogsDto(
    string ContainerId,
    string ContainerName,
    IReadOnlyList<string> Lines,
    DateTime FetchedAt);
