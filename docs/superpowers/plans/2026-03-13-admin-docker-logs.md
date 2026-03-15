# Admin Infrastructure Panel Phase 3 — Docker/Logs Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Log Viewer and Container Management infrastructure for the admin panel (#137-#142). Enables admins to view application logs (via HyperDX), inspect Docker containers, and manage container lifecycle — all from the admin dashboard.

**Architecture:** Backend uses a Docker Socket Proxy (read-only) to safely expose container info without giving the API full Docker socket access. Frontend adds two new admin pages: Log Viewer (HyperDX embed + audit filtering) and Container Logs UI (per-container log streaming). CQRS pattern throughout — all endpoints use `IMediator.Send()`.

**Tech Stack:** .NET 9 (MediatR, Docker.DotNet), Next.js 16, React 19, Tailwind 4, shadcn/ui, Vitest

**Branch:** `feature/issue-124-ph3-docker-logs` from `frontend-dev`

**Security Note:** Docker socket access is security-sensitive. The proxy MUST be read-only for Phase 3 (container list, inspect, logs). Write operations (restart, stop) are Phase 4 only.

---

## Chunk 1: Backend — Docker Socket Proxy & Service (#137, #138)

### Task 1: Create branch and Docker Socket Proxy config

**Files:**
- Modify: `infra/docker-compose.yml` — add docker-socket-proxy service
- Create: `infra/secrets/docker-proxy.secret.example`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git checkout frontend-dev && git pull
git checkout -b feature/issue-124-ph3-docker-logs
git config branch.feature/issue-124-ph3-docker-logs.parent frontend-dev
```

- [ ] **Step 2: Add Docker Socket Proxy to docker-compose.yml**

Add after the existing services section:

```yaml
  # Issue #137: Docker Socket Proxy (read-only)
  # Security layer between API and Docker daemon
  docker-socket-proxy:
    image: tecnativa/docker-socket-proxy:0.2
    container_name: meepleai-docker-proxy
    restart: unless-stopped
    profiles: [dev, observability, full]
    environment:
      # Read-only access for Phase 3
      CONTAINERS: 1    # List/inspect containers
      SERVICES: 0      # No swarm services
      TASKS: 0         # No swarm tasks
      NETWORKS: 0      # No network management
      VOLUMES: 0       # No volume management
      IMAGES: 0        # No image management
      INFO: 1          # System info
      VERSION: 1       # Docker version
      EVENTS: 0        # No event stream (Phase 4)
      PING: 1          # Health check
      # Write operations disabled (Phase 4)
      POST: 0          # No container create/start/stop/restart
      DELETE: 0        # No container delete
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    networks:
      - meepleai
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 128M
```

- [ ] **Step 3: Create secret example file**

```bash
# infra/secrets/docker-proxy.secret.example
# Docker Socket Proxy Configuration
# Used by: API service to connect to Docker proxy
DOCKER_PROXY_HOST=docker-socket-proxy
DOCKER_PROXY_PORT=2375
```

- [ ] **Step 4: Commit**

```bash
git add infra/docker-compose.yml infra/secrets/docker-proxy.secret.example
git commit -m "infra: add Docker Socket Proxy for admin container management (#137)"
```

### Task 2: IDockerProxyService backend interface and implementation (#138)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IDockerProxyService.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/DockerProxyService.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Docker/GetContainersQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Docker/GetContainerLogsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/Docker/GetContainersQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/Docker/GetContainerLogsQueryHandler.cs`

- [ ] **Step 1: Write the domain interface**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IDockerProxyService.cs
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
    IReadOnlyDictionary<string, string> Labels);

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
```

- [ ] **Step 2: Write the infrastructure implementation**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/DockerProxyService.cs
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<IReadOnlyList<ContainerInfoDto>> GetContainersAsync(CancellationToken ct)
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
                Labels: c.Labels ?? new Dictionary<string, string>()
            )).ToList().AsReadOnly() ?? Array.Empty<ContainerInfoDto>().AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Docker Socket Proxy");
            return Array.Empty<ContainerInfoDto>().AsReadOnly();
        }
    }

    public async Task<ContainerDetailDto?> GetContainerAsync(string containerId, CancellationToken ct)
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
                Created: DateTime.Parse(detail.Created),
                StartedAt: DateTime.TryParse(detail.State?.StartedAt, out var started) ? started : DateTime.MinValue,
                MemoryUsageBytes: 0, // Stats endpoint needed for live metrics
                CpuPercent: 0,
                Labels: detail.Config?.Labels ?? new Dictionary<string, string>(),
                Ports: detail.NetworkSettings?.Ports?.SelectMany(p =>
                    p.Value?.Select(b => new PortMapping(
                        int.TryParse(p.Key.Split('/')[0], out var pp) ? pp : 0,
                        int.TryParse(b.HostPort, out var hp) ? hp : 0,
                        p.Key.Contains('/') ? p.Key.Split('/')[1] : "tcp"
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
        string containerId, int tailLines, CancellationToken ct)
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
```

- [ ] **Step 3: Write CQRS queries and handlers**

```csharp
// GetContainersQuery.cs
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Docker;

public record GetContainersQuery : IRequest<IReadOnlyList<ContainerInfoDto>>;
```

```csharp
// GetContainerLogsQuery.cs
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Docker;

public record GetContainerLogsQuery(
    string ContainerId,
    int TailLines = 100) : IRequest<ContainerLogsDto>;
```

```csharp
// GetContainersQueryHandler.cs
using Api.BoundedContexts.Administration.Application.Queries.Docker;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.Docker;

internal sealed class GetContainersQueryHandler
    : IRequestHandler<GetContainersQuery, IReadOnlyList<ContainerInfoDto>>
{
    private readonly IDockerProxyService _dockerProxy;

    public GetContainersQueryHandler(IDockerProxyService dockerProxy)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
    }

    public async Task<IReadOnlyList<ContainerInfoDto>> Handle(
        GetContainersQuery request, CancellationToken ct)
    {
        return await _dockerProxy.GetContainersAsync(ct).ConfigureAwait(false);
    }
}
```

```csharp
// GetContainerLogsQueryHandler.cs
using Api.BoundedContexts.Administration.Application.Queries.Docker;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.Docker;

internal sealed class GetContainerLogsQueryHandler
    : IRequestHandler<GetContainerLogsQuery, ContainerLogsDto>
{
    private readonly IDockerProxyService _dockerProxy;

    public GetContainerLogsQueryHandler(IDockerProxyService dockerProxy)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
    }

    public async Task<ContainerLogsDto> Handle(
        GetContainerLogsQuery request, CancellationToken ct)
    {
        return await _dockerProxy.GetContainerLogsAsync(
            request.ContainerId, request.TailLines, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Register DI**

In the Administration DI registration file (find via `grep -r "AddScoped.*IDockerProxyService\|AddHttpClient" apps/api/src/Api/BoundedContexts/Administration/`), add:

```csharp
// Register Docker Proxy Service
services.AddHttpClient<IDockerProxyService, DockerProxyService>(client =>
{
    var host = Environment.GetEnvironmentVariable("DOCKER_PROXY_HOST") ?? "docker-socket-proxy";
    var port = Environment.GetEnvironmentVariable("DOCKER_PROXY_PORT") ?? "2375";
    client.BaseAddress = new Uri($"http://{host}:{port}");
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/
git commit -m "feat(admin): add IDockerProxyService with read-only container access (#138)"
```

### Task 3: Container Management API Endpoints (#139)

**Files:**
- Create: `apps/api/src/Api/Routing/AdminDockerEndpoints.cs`

- [ ] **Step 1: Create endpoints file**

```csharp
// apps/api/src/Api/Routing/AdminDockerEndpoints.cs
using Api.BoundedContexts.Administration.Application.Queries.Docker;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin Docker container management endpoints (read-only).
/// Issue #139: Container management API.
/// </summary>
internal static class AdminDockerEndpoints
{
    public static RouteGroupBuilder MapAdminDockerEndpoints(this RouteGroupBuilder group)
    {
        var dockerGroup = group.MapGroup("/admin/docker")
            .WithTags("Admin", "Docker");

        // List all containers
        dockerGroup.MapGet("/containers", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetContainersQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("List Docker containers")
        .WithDescription("Returns all containers (running and stopped) via Docker Socket Proxy");

        // Get container logs
        dockerGroup.MapGet("/containers/{containerId}/logs", async (
            HttpContext context,
            IMediator mediator,
            string containerId,
            int? tail,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetContainerLogsQuery(containerId, tail ?? 100);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get container logs")
        .WithDescription("Returns recent log lines for a specific container");

        return group;
    }
}
```

- [ ] **Step 2: Register endpoints in routing**

Find the main routing registration file and add:
```csharp
group.MapAdminDockerEndpoints();
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/AdminDockerEndpoints.cs
git commit -m "feat(admin): add Docker container management API endpoints (#139)"
```

---

## Chunk 2: Frontend — Log Viewer & Container UI (#140, #141)

### Task 4: Admin API Client — Docker methods

**Files:**
- Modify: `apps/web/src/lib/api/clients/adminClient.ts`
- Modify: `apps/web/src/lib/api/schemas/index.ts` (or appropriate schema file)

- [ ] **Step 1: Add Zod schemas for Docker responses**

```typescript
// In the appropriate schema file
import { z } from 'zod';

export const containerInfoSchema = z.object({
  id: z.string(),
  name: z.string(),
  image: z.string(),
  state: z.string(),
  status: z.string(),
  created: z.string(),
  labels: z.record(z.string()),
});

export const containerLogsSchema = z.object({
  containerId: z.string(),
  containerName: z.string(),
  lines: z.array(z.string()),
  fetchedAt: z.string(),
});

export type ContainerInfo = z.infer<typeof containerInfoSchema>;
export type ContainerLogs = z.infer<typeof containerLogsSchema>;
```

- [ ] **Step 2: Add Docker methods to adminClient**

```typescript
// In adminClient.ts, add to the admin object:

async getDockerContainers(): Promise<ContainerInfo[]> {
  const result = await httpClient.get(
    '/api/v1/admin/docker/containers',
    z.array(containerInfoSchema)
  );
  return result ?? [];
},

async getContainerLogs(
  containerId: string,
  tail: number = 100
): Promise<ContainerLogs | null> {
  return httpClient.get(
    `/api/v1/admin/docker/containers/${containerId}/logs?tail=${tail}`,
    containerLogsSchema
  );
},
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(admin): add Docker API client methods and schemas (#139)"
```

### Task 5: Log Viewer Page (#140)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/LogViewer.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/NavConfig.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/LogViewer.test.tsx`

- [ ] **Step 1: Write failing test for LogViewer**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/LogViewer.test.tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockGetDockerContainers = vi.hoisted(() => vi.fn());
const mockGetContainerLogs = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
      getContainerLogs: mockGetContainerLogs,
    },
  },
}));

import { LogViewer } from '../LogViewer';

const mockContainers = [
  { id: 'abc123', name: 'meepleai-api', image: 'meepleai-api:latest', state: 'running', status: 'Up 2 hours', created: new Date().toISOString(), labels: {} },
  { id: 'def456', name: 'meepleai-postgres', image: 'pgvector/pgvector:pg16', state: 'running', status: 'Up 2 hours', created: new Date().toISOString(), labels: {} },
];

const mockLogs = {
  containerId: 'abc123',
  containerName: 'meepleai-api',
  lines: ['2026-03-13T10:00:00Z info: Application started', '2026-03-13T10:00:01Z info: Listening on :8080'],
  fetchedAt: new Date().toISOString(),
};

describe('LogViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders container list', async () => {
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('container-list')).toBeInTheDocument();
    });

    expect(screen.getByTestId('container-item-abc123')).toBeInTheDocument();
    expect(screen.getByTestId('container-item-def456')).toBeInTheDocument();
  });

  it('clicking container loads its logs', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    mockGetContainerLogs.mockResolvedValue(mockLogs);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('container-item-abc123')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('container-item-abc123'));

    await waitFor(() => {
      expect(screen.getByTestId('log-output')).toBeInTheDocument();
    });

    expect(screen.getByText(/Application started/)).toBeInTheDocument();
  });

  it('shows loading state', () => {
    mockGetDockerContainers.mockReturnValue(new Promise(() => {}));
    render(<LogViewer />);

    expect(screen.getByTestId('log-viewer-loading')).toBeInTheDocument();
  });

  it('shows empty state when no containers', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('log-viewer-empty')).toBeInTheDocument();
    });
  });

  it('refresh button reloads logs', async () => {
    const user = userEvent.setup();
    mockGetDockerContainers.mockResolvedValue(mockContainers);
    mockGetContainerLogs.mockResolvedValue(mockLogs);
    render(<LogViewer />);

    await waitFor(() => {
      expect(screen.getByTestId('container-item-abc123')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('container-item-abc123'));

    await waitFor(() => {
      expect(screen.getByTestId('log-refresh-btn')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('log-refresh-btn'));

    await waitFor(() => {
      expect(mockGetContainerLogs).toHaveBeenCalledTimes(2);
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/monitor/logs/__tests__/LogViewer.test.tsx
```

Expected: FAIL — module not found

- [ ] **Step 3: Implement LogViewer component**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/LogViewer.tsx
'use client';

import { useCallback, useEffect, useState } from 'react';
import { FileText, Loader2, Monitor, RefreshCw, Terminal } from 'lucide-react';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import type { ContainerInfo, ContainerLogs } from '@/lib/api/schemas';

function ContainerStatusBadge({ state }: { state: string }) {
  const variant = state === 'running' ? 'default' : state === 'exited' ? 'destructive' : 'secondary';
  return <Badge variant={variant} className="text-[10px]">{state}</Badge>;
}

export function LogViewer() {
  const [containers, setContainers] = useState<ContainerInfo[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [logs, setLogs] = useState<ContainerLogs | null>(null);
  const [loading, setLoading] = useState(true);
  const [logsLoading, setLogsLoading] = useState(false);

  const fetchContainers = useCallback(async () => {
    try {
      const data = await api.admin.getDockerContainers();
      setContainers(data);
    } catch {
      // Toast handled by API client
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchLogs = useCallback(async (containerId: string) => {
    setLogsLoading(true);
    try {
      const data = await api.admin.getContainerLogs(containerId);
      setLogs(data);
    } catch {
      // Toast handled by API client
    } finally {
      setLogsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchContainers();
  }, [fetchContainers]);

  const handleContainerClick = useCallback((id: string) => {
    setSelectedId(id);
    fetchLogs(id);
  }, [fetchLogs]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-10" data-testid="log-viewer-loading">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (containers.length === 0) {
    return (
      <div className="text-center py-10 text-sm text-muted-foreground" data-testid="log-viewer-empty">
        <Monitor className="mx-auto h-8 w-8 mb-2 text-muted-foreground/50" />
        No containers found. Is Docker Socket Proxy running?
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-[280px_1fr] gap-4" data-testid="log-viewer">
      {/* Container sidebar */}
      <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-hidden" data-testid="container-list">
        <div className="px-4 py-3 border-b">
          <h3 className="font-quicksand font-semibold text-sm flex items-center gap-2">
            <Terminal className="h-4 w-4" />
            Containers ({containers.length})
          </h3>
        </div>
        <div className="max-h-[500px] overflow-y-auto">
          {containers.map(c => (
            <button
              key={c.id}
              data-testid={`container-item-${c.id}`}
              onClick={() => handleContainerClick(c.id)}
              className={cn(
                'w-full text-left px-4 py-2.5 border-b last:border-0 hover:bg-slate-50 dark:hover:bg-zinc-800/50 transition-colors',
                selectedId === c.id && 'bg-blue-50 dark:bg-blue-900/20 border-l-2 border-l-blue-500'
              )}
            >
              <div className="flex items-center justify-between">
                <span className="font-mono text-xs font-medium truncate">{c.name}</span>
                <ContainerStatusBadge state={c.state} />
              </div>
              <p className="text-[10px] text-muted-foreground mt-0.5 truncate">{c.image}</p>
            </button>
          ))}
        </div>
      </div>

      {/* Log output */}
      <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-hidden">
        {selectedId && logs ? (
          <>
            <div className="flex items-center justify-between px-4 py-3 border-b">
              <div className="flex items-center gap-2">
                <FileText className="h-4 w-4 text-blue-600" />
                <span className="font-mono text-sm font-medium">{logs.containerName}</span>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => fetchLogs(selectedId)}
                disabled={logsLoading}
                data-testid="log-refresh-btn"
                className="gap-1.5"
              >
                <RefreshCw className={cn('h-3.5 w-3.5', logsLoading && 'animate-spin')} />
                Refresh
              </Button>
            </div>
            <div
              className="p-4 font-mono text-xs leading-relaxed max-h-[500px] overflow-y-auto bg-zinc-950 text-green-400"
              data-testid="log-output"
            >
              {logs.lines.map((line, i) => (
                <div key={i} className="hover:bg-zinc-900 px-1 whitespace-pre-wrap break-all">
                  {line}
                </div>
              ))}
              {logs.lines.length === 0 && (
                <div className="text-zinc-500 text-center py-4">No log output</div>
              )}
            </div>
          </>
        ) : (
          <div className="flex items-center justify-center py-20 text-sm text-muted-foreground">
            Select a container to view logs
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Create page and NavConfig**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx
import { LogViewer } from './LogViewer';
import { LogsNavConfig } from './NavConfig';

export default function LogViewerPage() {
  return (
    <div className="space-y-5" data-testid="logs-page">
      <LogsNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Log Viewer
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Container logs and application monitoring via Docker Socket Proxy.
        </p>
      </div>
      <LogViewer />
    </div>
  );
}
```

NavConfig follows the same pattern as `services/NavConfig.tsx`.

- [ ] **Step 5: Run tests**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/monitor/logs/__tests__/LogViewer.test.tsx
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/logs/
git commit -m "feat(admin): add Log Viewer page with container log display (#140, #141)"
```

---

## Chunk 3: Tests & PR (#142)

### Task 6: Integration tests and final validation

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/page.test.tsx`

- [ ] **Step 1: Write page-level test**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

vi.mock('@/lib/api', () => ({
  api: { admin: { getDockerContainers: vi.fn().mockResolvedValue([]) } },
}));

import LogViewerPage from '../page';

describe('LogViewerPage', () => {
  it('renders page with title', async () => {
    render(<LogViewerPage />);
    expect(screen.getByText('Log Viewer')).toBeInTheDocument();
    expect(screen.getByTestId('logs-page')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run all admin monitor tests**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/monitor/
```

- [ ] **Step 3: Run full frontend checks**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/logs/__tests__/
git commit -m "test(admin): add Log Viewer and Container UI tests (#142)"
```

### Task 7: Create PR

- [ ] **Step 1: Push and create PR**

```bash
git push -u origin feature/issue-124-ph3-docker-logs
gh pr create --base frontend-dev --title "feat(admin): Phase 3 — Docker/Logs infrastructure (#137-#142)" --body "$(cat <<'EOF'
## Summary
- Docker Socket Proxy setup (read-only) for secure container access (#137)
- IDockerProxyService backend with container list, inspect, and logs (#138)
- Admin API endpoints for Docker container management (#139)
- Log Viewer page with container sidebar and log output (#140, #141)
- Component and page tests (#142)

## Security
- Docker Socket Proxy is READ-ONLY (POST=0, DELETE=0)
- All endpoints require AdminOrAbove authorization
- No write operations on containers (Phase 4)

## Test plan
- [ ] Docker Socket Proxy starts correctly with `docker compose --profile dev up -d docker-socket-proxy`
- [ ] `/admin/monitor/logs` page loads and shows containers
- [ ] Clicking a container loads its logs
- [ ] All frontend tests pass
- [ ] Backend compiles and all tests pass

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 2: Update issues**

```bash
for issue in 137 138 139 140 141 142; do
  gh issue edit $issue --add-label "status:in-review"
done
```
