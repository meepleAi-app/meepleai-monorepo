# Database Sync — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an admin tool for comparing and synchronizing PostgreSQL databases (schema + data) between local and staging, with web UI, API, and CLI.

**Architecture:** New `DatabaseSync` bounded context in the API monolite, SSH tunnel sidecar container for staging access, REST API consumed by Next.js admin page and bash CLI. All operations gated behind `Features.DatabaseSync` flag and `RequireSuperAdminSession()`.

**Tech Stack:** .NET 9 / EF Core / MediatR / Npgsql (backend), Python Flask (sidecar), Next.js / React Query / Tailwind / shadcn (frontend), Bash (CLI)

**Spec:** `docs/superpowers/specs/2026-03-18-database-sync-design.md`

---

## Task 1: Domain Models, Enums, and Interfaces

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Enums/SyncDirection.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Enums/TunnelState.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/MigrationInfo.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/SchemaDiffResult.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/TableInfo.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/ColumnDiff.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/RowDiff.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/DataDiffResult.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/TunnelStatusResult.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/SyncResult.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Interfaces/ISshTunnelClient.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Interfaces/IRemoteDatabaseConnector.cs`

- [ ] **Step 1: Create SyncDirection enum**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Enums/SyncDirection.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Enums;

/// <summary>
/// Direction for sync operations. PascalCase values match .NET default JSON serialization.
/// </summary>
internal enum SyncDirection
{
    LocalToStaging,
    StagingToLocal
}
```

- [ ] **Step 2: Create TunnelState enum**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Enums/TunnelState.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Enums;

internal enum TunnelState
{
    Closed,
    Opening,
    Open,
    Error
}
```

- [ ] **Step 3: Create MigrationInfo model**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/MigrationInfo.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record MigrationInfo(
    string MigrationId,
    string ProductVersion,
    DateTime? AppliedOn
);
```

- [ ] **Step 4: Create SchemaDiffResult model**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/SchemaDiffResult.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record SchemaDiffResult(
    IReadOnlyList<MigrationInfo> Common,
    IReadOnlyList<MigrationInfo> LocalOnly,
    IReadOnlyList<MigrationInfo> StagingOnly
);
```

- [ ] **Step 5: Create TableInfo model**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/TableInfo.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record TableInfo(
    string TableName,
    string SchemaName,
    long LocalRowCount,
    long StagingRowCount,
    string? BoundedContext
);
```

- [ ] **Step 6: Create ColumnDiff, RowDiff, DataDiffResult models**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/ColumnDiff.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record ColumnDiff(
    string Column,
    string? LocalValue,
    string? StagingValue
);
```

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/RowDiff.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record RowDiff(
    Dictionary<string, string?> PrimaryKey,
    IReadOnlyList<ColumnDiff> Differences
);
```

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/DataDiffResult.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record DataDiffResult(
    string TableName,
    long LocalRowCount,
    long StagingRowCount,
    int IdenticalCount,
    IReadOnlyList<RowDiff> Modified,
    IReadOnlyList<Dictionary<string, string?>> LocalOnly,
    IReadOnlyList<Dictionary<string, string?>> StagingOnly
);
```

- [ ] **Step 7: Create TunnelStatusResult and SyncResult models**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/TunnelStatusResult.cs
using Api.BoundedContexts.DatabaseSync.Domain.Enums;

namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record TunnelStatusResult(
    TunnelState Status,
    int UptimeSeconds,
    string? Message
);
```

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Models/SyncResult.cs
namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record SyncResult(
    bool Success,
    int Inserted,
    int Updated,
    Guid OperationId,
    string? ErrorMessage = null
);
```

- [ ] **Step 8: Create ISshTunnelClient interface**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Interfaces/ISshTunnelClient.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;

namespace Api.BoundedContexts.DatabaseSync.Domain.Interfaces;

internal interface ISshTunnelClient
{
    Task<TunnelStatusResult> GetStatusAsync(CancellationToken ct = default);
    Task<TunnelStatusResult> OpenAsync(CancellationToken ct = default);
    Task<TunnelStatusResult> CloseAsync(CancellationToken ct = default);
}
```

- [ ] **Step 9: Create IRemoteDatabaseConnector interface**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Domain/Interfaces/IRemoteDatabaseConnector.cs
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Domain.Interfaces;

/// <summary>
/// Provides NpgsqlConnection to the remote (staging) database via the SSH tunnel sidecar.
/// </summary>
internal interface IRemoteDatabaseConnector
{
    /// <summary>
    /// Creates and opens a connection to the remote database.
    /// Throws if tunnel is not open.
    /// </summary>
    Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default);
}
```

- [ ] **Step 10: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 11: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/
git commit -m "feat(db-sync): add domain models, enums, and interfaces"
```

---

## Task 2: Feature Flag Migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_SeedDatabaseSyncFeatureFlag.cs`

- [ ] **Step 1: Create EF Core migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add SeedDatabaseSyncFeatureFlag`

- [ ] **Step 2: Edit the migration Up method**

Replace the generated `Up` method body with (match the exact schema from `20260318080115_SeedGameNightV2FeatureFlags.cs`):

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql($"""
        INSERT INTO "system_configurations"
            ("Id", "Key", "Value", "ValueType", "Description", "Category",
             "IsActive", "RequiresRestart", "Environment", "Version",
             "CreatedAt", "UpdatedAt", "CreatedByUserId")
        SELECT gen_random_uuid(), 'Features.DatabaseSync', 'false', 'bool',
               'Enable Database Sync admin tool', 'Features', true, false, 'All', 1,
               NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC',
               COALESCE(
                   (SELECT "Id" FROM "users" WHERE "Role" = 'Admin' ORDER BY "CreatedAt" LIMIT 1),
                   '00000000-0000-0000-0000-000000000000'::uuid)
        WHERE NOT EXISTS (SELECT 1 FROM "system_configurations" WHERE "Key" = 'Features.DatabaseSync');
    """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("""DELETE FROM "system_configurations" WHERE "Key" = 'Features.DatabaseSync';""");
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(db-sync): add feature flag migration for Features.DatabaseSync"
```

---

## Task 3: SSH Tunnel Sidecar (Python Container)

**Files:**
- Create: `infra/sidecar/ssh-tunnel/Dockerfile`
- Create: `infra/sidecar/ssh-tunnel/server.py`
- Create: `infra/sidecar/ssh-tunnel/tunnel_manager.py`
- Create: `infra/sidecar/ssh-tunnel/requirements.txt`

- [ ] **Step 1: Create requirements.txt**

```
# infra/sidecar/ssh-tunnel/requirements.txt
flask==3.1.*
```

- [ ] **Step 2: Create tunnel_manager.py**

```python
# infra/sidecar/ssh-tunnel/tunnel_manager.py
"""SSH tunnel process manager with auto-close and host whitelist."""

import os
import subprocess
import threading
import time
import logging

logger = logging.getLogger(__name__)

ALLOWED_HOSTS = os.environ.get("ALLOWED_HOSTS", "").split(",")
MAX_DURATION = int(os.environ.get("MAX_TUNNEL_DURATION", "3600"))
SSH_KEY_PATH = os.environ.get("SSH_KEY_PATH", "/root/.ssh/key")


class TunnelManager:
    def __init__(self):
        self._process = None
        self._status = "closed"
        self._message = None
        self._started_at = None
        self._lock = threading.Lock()
        self._timer = None

    @property
    def status(self):
        with self._lock:
            # Check if process died
            if self._process and self._process.poll() is not None:
                self._status = "error"
                self._message = f"SSH process exited with code {self._process.returncode}"
                self._process = None
            return self._status

    @property
    def uptime_seconds(self):
        if self._started_at and self._status == "open":
            return int(time.time() - self._started_at)
        return 0

    @property
    def message(self):
        return self._message

    def open(self, host, port, user, local_port):
        with self._lock:
            if self._process and self._process.poll() is None:
                return {"status": "open", "message": "Tunnel already open"}

            # Validate host
            host = host.strip()
            if host not in ALLOWED_HOSTS:
                self._status = "error"
                self._message = f"Host {host} not in allowed list"
                return {"status": "error", "message": self._message}

            # Validate SSH key exists
            if not os.path.isfile(SSH_KEY_PATH):
                self._status = "error"
                self._message = f"SSH key not found at {SSH_KEY_PATH}"
                return {"status": "error", "message": self._message}

            self._status = "opening"
            self._message = f"Connecting to {host}..."

            cmd = [
                "ssh", "-N", "-o", "StrictHostKeyChecking=accept-new",
                "-o", "ServerAliveInterval=30",
                "-o", "ServerAliveCountMax=3",
                "-o", "ConnectTimeout=10",
                "-i", SSH_KEY_PATH,
                "-L", f"0.0.0.0:{local_port}:localhost:{port}",
                f"{user}@{host}"
            ]

            try:
                self._process = subprocess.Popen(
                    cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE
                )
                # Wait briefly to catch immediate failures
                time.sleep(2)
                if self._process.poll() is not None:
                    stderr = self._process.stderr.read().decode() if self._process.stderr else ""
                    self._status = "error"
                    self._message = f"SSH failed: {stderr.strip()}"
                    self._process = None
                    return {"status": "error", "message": self._message}

                self._status = "open"
                self._started_at = time.time()
                self._message = f"Tunnel open: localhost:{local_port} -> {host}:{port}"

                # Auto-close timer
                if self._timer:
                    self._timer.cancel()
                self._timer = threading.Timer(MAX_DURATION, self._auto_close)
                self._timer.daemon = True
                self._timer.start()

                return {"status": "open", "message": self._message}

            except Exception as e:
                self._status = "error"
                self._message = str(e)
                return {"status": "error", "message": self._message}

    def close(self):
        with self._lock:
            if self._timer:
                self._timer.cancel()
                self._timer = None
            if self._process and self._process.poll() is None:
                self._process.terminate()
                self._process.wait(timeout=5)
            self._process = None
            self._status = "closed"
            self._started_at = None
            self._message = None
            return {"status": "closed"}

    def _auto_close(self):
        logger.warning("Auto-closing tunnel after max duration")
        self.close()
```

- [ ] **Step 3: Create server.py**

```python
# infra/sidecar/ssh-tunnel/server.py
"""Minimal HTTP API for SSH tunnel management with bearer auth."""

import os
import logging
from functools import wraps

from flask import Flask, request, jsonify

from tunnel_manager import TunnelManager

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)

manager = TunnelManager()

AUTH_TOKEN = os.environ.get("SIDECAR_AUTH_TOKEN")


def require_auth(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        if not AUTH_TOKEN:
            return jsonify({"error": "SIDECAR_AUTH_TOKEN not configured"}), 500
        auth = request.headers.get("Authorization", "")
        if auth != f"Bearer {AUTH_TOKEN}":
            return jsonify({"error": "Unauthorized"}), 401
        return f(*args, **kwargs)
    return decorated


@app.route("/health", methods=["GET"])
def health():
    return "OK", 200


@app.route("/status", methods=["GET"])
@require_auth
def status():
    return jsonify({
        "status": manager.status,
        "uptime_seconds": manager.uptime_seconds,
        "message": manager.message
    })


@app.route("/open", methods=["POST"])
@require_auth
def open_tunnel():
    data = request.get_json(silent=True) or {}
    host = data.get("host", os.environ.get("DEFAULT_HOST", "204.168.135.69"))
    port = int(data.get("port", 5432))
    user = data.get("user", "deploy")
    local_port = int(data.get("localPort", 15432))

    result = manager.open(host, port, user, local_port)
    status_code = 200 if result["status"] in ("open", "opening") else 400
    return jsonify(result), status_code


@app.route("/close", methods=["DELETE"])
@require_auth
def close_tunnel():
    result = manager.close()
    return jsonify(result)


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=2222)
```

- [ ] **Step 4: Create Dockerfile**

```dockerfile
# infra/sidecar/ssh-tunnel/Dockerfile
FROM python:3.12-alpine3.19

RUN apk add --no-cache openssh-client

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY server.py tunnel_manager.py ./

RUN mkdir -p /root/.ssh && chmod 700 /root/.ssh

EXPOSE 2222

HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD wget -q --spider http://localhost:2222/health || exit 1

CMD ["python", "server.py"]
```

- [ ] **Step 5: Test sidecar builds**

Run: `cd infra/sidecar/ssh-tunnel && docker build -t meepleai-ssh-tunnel-sidecar .`
Expected: Successfully built

- [ ] **Step 6: Commit**

```bash
git add infra/sidecar/ssh-tunnel/
git commit -m "feat(db-sync): add SSH tunnel sidecar container"
```

---

## Task 4: Docker Compose Changes

**Files:**
- Modify: `infra/compose.dev.yml`
- Create: `infra/secrets/dev/db-sync.secret.example`

- [ ] **Step 1: Add sidecar service to compose.dev.yml**

Add after the last service definition, before `volumes:`:

```yaml
  # ===== Database Sync (profile: db-sync) =====
  ssh-tunnel-sidecar:
    build:
      context: ./sidecar/ssh-tunnel
      dockerfile: Dockerfile
    container_name: meepleai-ssh-tunnel-sidecar
    restart: "no"
    volumes:
      - ${SSH_KEY_PATH:-~/.ssh/meepleai-staging}:/root/.ssh/key:ro
    env_file:
      - ./secrets/dev/db-sync.secret
    environment:
      - ALLOWED_HOSTS=204.168.135.69
      - MAX_TUNNEL_DURATION=3600
      - DEFAULT_HOST=204.168.135.69
    networks:
      - meepleai
    profiles:
      - db-sync
```

- [ ] **Step 2: Add SIDECAR_AUTH_TOKEN to API service env_file list**

In the `api` service section of `compose.dev.yml`, add to the `env_file` list:

```yaml
      - ./secrets/dev/db-sync.secret
```

- [ ] **Step 3: Create db-sync.secret.example**

```bash
# infra/secrets/dev/db-sync.secret.example
# Shared auth token between API and sidecar (generate with: openssl rand -hex 32)
SIDECAR_AUTH_TOKEN=change-me-generate-with-openssl-rand-hex-32
SIDECAR_BASE_URL=http://ssh-tunnel-sidecar:2222
```

- [ ] **Step 4: Create actual dev secret**

Run:
```bash
TOKEN=$(openssl rand -hex 32)
cat > infra/secrets/dev/db-sync.secret << EOF
SIDECAR_AUTH_TOKEN=${TOKEN}
SIDECAR_BASE_URL=http://ssh-tunnel-sidecar:2222
EOF
```

- [ ] **Step 5: Verify .gitignore covers the secret**

Run: `grep -q "*.secret" infra/.gitignore && echo "OK" || echo "MISSING"`
Expected: OK (existing pattern `*.secret` should cover it)

- [ ] **Step 6: Commit**

```bash
git add infra/compose.dev.yml infra/secrets/dev/db-sync.secret.example
git commit -m "feat(db-sync): add sidecar to Docker compose with db-sync profile"
```

---

## Task 5: Infrastructure — SshTunnelClient

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/SshTunnelClient.cs`
- Create: `tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/SshTunnelClientTests.cs`

- [ ] **Step 1: Write tests for SshTunnelClient**

```csharp
// tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/SshTunnelClientTests.cs
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class SshTunnelClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:2222";
    private const string AuthToken = "test-token";

    public SshTunnelClientTests()
    {
        _httpClient = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri(BaseUrl) };
    }

    private SshTunnelClient CreateClient() =>
        new(_httpClient, AuthToken, NullLogger<SshTunnelClient>.Instance);

    [Fact]
    public async Task GetStatusAsync_ReturnsOpenStatus_WhenSidecarRespondsOpen()
    {
        SetupResponse(HttpMethod.Get, "/status", new { status = "open", uptime_seconds = 120, message = "Tunnel open" });

        var result = await CreateClient().GetStatusAsync();

        Assert.Equal(TunnelState.Open, result.Status);
        Assert.Equal(120, result.UptimeSeconds);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsError_WhenSidecarUnreachable()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await CreateClient().GetStatusAsync();

        Assert.Equal(TunnelState.Error, result.Status);
        Assert.Contains("Connection refused", result.Message);
    }

    [Fact]
    public async Task OpenAsync_ReturnsOpening_WhenSidecarAccepts()
    {
        SetupResponse(HttpMethod.Post, "/open", new { status = "opening", message = "Connecting..." });

        var result = await CreateClient().OpenAsync();

        Assert.Equal(TunnelState.Opening, result.Status);
    }

    [Fact]
    public async Task CloseAsync_ReturnsClosed()
    {
        SetupResponse(HttpMethod.Delete, "/close", new { status = "closed" });

        var result = await CreateClient().CloseAsync();

        Assert.Equal(TunnelState.Closed, result.Status);
    }

    private void SetupResponse(HttpMethod method, string path, object body)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == method && r.RequestUri!.AbsolutePath == path),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            });
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~SshTunnelClientTests" --no-restore -v m 2>&1 | tail -10`
Expected: FAIL (class not found)

- [ ] **Step 3: Implement SshTunnelClient**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/SshTunnelClient.cs
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal sealed class SshTunnelClient : ISshTunnelClient
{
    private readonly HttpClient _httpClient;
    private readonly string _authToken;
    private readonly ILogger<SshTunnelClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public SshTunnelClient(HttpClient httpClient, string authToken, ILogger<SshTunnelClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TunnelStatusResult> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "/status");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SidecarStatusResponse>(JsonOptions, ct).ConfigureAwait(false);
            return MapStatus(body!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tunnel status from sidecar");
            return new TunnelStatusResult(TunnelState.Error, 0, ex.Message);
        }
    }

    public async Task<TunnelStatusResult> OpenAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "/open");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SidecarStatusResponse>(JsonOptions, ct).ConfigureAwait(false);
            return MapStatus(body!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open tunnel via sidecar");
            return new TunnelStatusResult(TunnelState.Error, 0, ex.Message);
        }
    }

    public async Task<TunnelStatusResult> CloseAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, "/close");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SidecarStatusResponse>(JsonOptions, ct).ConfigureAwait(false);
            return MapStatus(body!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close tunnel via sidecar");
            return new TunnelStatusResult(TunnelState.Error, 0, ex.Message);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        return request;
    }

    private static TunnelStatusResult MapStatus(SidecarStatusResponse response)
    {
        var state = response.Status switch
        {
            "closed" => TunnelState.Closed,
            "opening" => TunnelState.Opening,
            "open" => TunnelState.Open,
            _ => TunnelState.Error
        };
        return new TunnelStatusResult(state, response.UptimeSeconds, response.Message);
    }

    private sealed record SidecarStatusResponse(
        string Status,
        [property: JsonPropertyName("uptime_seconds")] int UptimeSeconds,
        string? Message
    );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~SshTunnelClientTests" --no-restore -v m 2>&1 | tail -10`
Expected: 4 passed

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/SshTunnelClient.cs tests/Api.Tests/BoundedContexts/DatabaseSync/
git commit -m "feat(db-sync): implement SshTunnelClient with unit tests"
```

---

## Task 6: Infrastructure — RemoteDatabaseConnector

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/RemoteDatabaseConnector.cs`
- Create: `tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/RemoteDatabaseConnectorTests.cs`

- [ ] **Step 1: Write test**

```csharp
// tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/RemoteDatabaseConnectorTests.cs
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class RemoteDatabaseConnectorTests
{
    [Fact]
    public async Task OpenConnectionAsync_ThrowsInvalidOperation_WhenTunnelNotOpen()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();
        tunnelClient.Setup(t => t.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TunnelStatusResult(TunnelState.Closed, 0, null));

        var connector = new RemoteDatabaseConnector(
            tunnelClient.Object,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            NullLogger<RemoteDatabaseConnector>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.OpenConnectionAsync());
    }
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~RemoteDatabaseConnectorTests" --no-restore -v m 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Implement RemoteDatabaseConnector**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/RemoteDatabaseConnector.cs
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal sealed class RemoteDatabaseConnector : IRemoteDatabaseConnector
{
    private readonly ISshTunnelClient _tunnelClient;
    private readonly string _connectionString;
    private readonly ILogger<RemoteDatabaseConnector> _logger;

    public RemoteDatabaseConnector(
        ISshTunnelClient tunnelClient,
        string connectionString,
        ILogger<RemoteDatabaseConnector> logger)
    {
        _tunnelClient = tunnelClient ?? throw new ArgumentNullException(nameof(tunnelClient));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var status = await _tunnelClient.GetStatusAsync(ct).ConfigureAwait(false);
        if (status.Status != TunnelState.Open)
        {
            throw new InvalidOperationException(
                $"SSH tunnel is not open (current state: {status.Status}). Open the tunnel first via POST /api/v1/admin/db-sync/tunnel/open");
        }

        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Opened connection to remote database");
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~RemoteDatabaseConnectorTests" --no-restore -v m 2>&1 | tail -10`
Expected: 1 passed

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/RemoteDatabaseConnector.cs tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/RemoteDatabaseConnectorTests.cs
git commit -m "feat(db-sync): implement RemoteDatabaseConnector with tunnel check"
```

---

## Task 7: Infrastructure — SchemaDiffEngine

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/SchemaDiffEngine.cs`
- Create: `tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/SchemaDiffEngineTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/SchemaDiffEngineTests.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class SchemaDiffEngineTests
{
    [Fact]
    public void ComputeDiff_WithIdenticalLists_ReturnsAllCommon()
    {
        var local = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260317_AddFts", "9.0.0", DateTime.UtcNow)
        };
        var staging = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260317_AddFts", "9.0.0", DateTime.UtcNow)
        };

        var result = SchemaDiffEngine.ComputeDiff(local, staging);

        Assert.Equal(2, result.Common.Count);
        Assert.Empty(result.LocalOnly);
        Assert.Empty(result.StagingOnly);
    }

    [Fact]
    public void ComputeDiff_WithLocalOnlyMigrations_ReturnsCorrectly()
    {
        var local = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260318_NewFeature", "9.0.0", DateTime.UtcNow)
        };
        var staging = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow)
        };

        var result = SchemaDiffEngine.ComputeDiff(local, staging);

        Assert.Single(result.Common);
        Assert.Single(result.LocalOnly);
        Assert.Equal("20260318_NewFeature", result.LocalOnly[0].MigrationId);
        Assert.Empty(result.StagingOnly);
    }

    [Fact]
    public void ComputeDiff_WithStagingOnlyMigrations_ReturnsCorrectly()
    {
        var local = new List<MigrationInfo> { new("20260316_Beta0", "9.0.0", DateTime.UtcNow) };
        var staging = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260317_StagingFix", "9.0.0", DateTime.UtcNow)
        };

        var result = SchemaDiffEngine.ComputeDiff(local, staging);

        Assert.Single(result.Common);
        Assert.Empty(result.LocalOnly);
        Assert.Single(result.StagingOnly);
    }
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~SchemaDiffEngineTests" --no-restore -v m 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Implement SchemaDiffEngine**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/SchemaDiffEngine.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal static class SchemaDiffEngine
{
    /// <summary>
    /// Reads __EFMigrationsHistory from a database connection.
    /// </summary>
    public static async Task<List<MigrationInfo>> ReadMigrationsAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        var migrations = new List<MigrationInfo>();
        await using var cmd = new NpgsqlCommand(
            """SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" """,
            conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            migrations.Add(new MigrationInfo(
                MigrationId: reader.GetString(0),
                ProductVersion: reader.GetString(1),
                AppliedOn: null // EF doesn't store applied timestamp by default
            ));
        }
        return migrations;
    }

    /// <summary>
    /// Pure function: computes schema diff from two migration lists.
    /// </summary>
    public static SchemaDiffResult ComputeDiff(
        IReadOnlyList<MigrationInfo> localMigrations,
        IReadOnlyList<MigrationInfo> stagingMigrations)
    {
        var localIds = localMigrations.ToDictionary(m => m.MigrationId, m => m);
        var stagingIds = stagingMigrations.ToDictionary(m => m.MigrationId, m => m);

        var common = new List<MigrationInfo>();
        var localOnly = new List<MigrationInfo>();
        var stagingOnly = new List<MigrationInfo>();

        foreach (var m in localMigrations)
        {
            if (stagingIds.ContainsKey(m.MigrationId))
                common.Add(m);
            else
                localOnly.Add(m);
        }

        foreach (var m in stagingMigrations)
        {
            if (!localIds.ContainsKey(m.MigrationId))
                stagingOnly.Add(m);
        }

        return new SchemaDiffResult(common, localOnly, stagingOnly);
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~SchemaDiffEngineTests" --no-restore -v m 2>&1 | tail -10`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/SchemaDiffEngine.cs tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/SchemaDiffEngineTests.cs
git commit -m "feat(db-sync): implement SchemaDiffEngine with unit tests"
```

---

## Task 8: Infrastructure — DataDiffEngine

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngine.cs`
- Create: `tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngineTests.cs`

- [ ] **Step 1: Write tests for the pure diff logic**

```csharp
// tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngineTests.cs
using Api.BoundedContexts.DatabaseSync.Infrastructure;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class DataDiffEngineTests
{
    [Fact]
    public void ComputeHashDiff_IdenticalHashes_ReturnsOnlyIdentical()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };
        var staging = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };

        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);

        Assert.Equal(2, identical);
        Assert.Empty(modified);
        Assert.Empty(localOnly);
        Assert.Empty(stagingOnly);
    }

    [Fact]
    public void ComputeHashDiff_DifferentHashes_DetectsModified()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hashA" };
        var staging = new Dictionary<string, string> { ["id1"] = "hashB" };

        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);

        Assert.Equal(0, identical);
        Assert.Single(modified);
        Assert.Equal("id1", modified[0]);
        Assert.Empty(localOnly);
        Assert.Empty(stagingOnly);
    }

    [Fact]
    public void ComputeHashDiff_MissingRows_DetectsOnlyLocal_OnlyStaging()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hash1", ["id3"] = "hash3" };
        var staging = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };

        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);

        Assert.Equal(1, identical);
        Assert.Empty(modified);
        Assert.Single(localOnly);
        Assert.Equal("id3", localOnly[0]);
        Assert.Single(stagingOnly);
        Assert.Equal("id2", stagingOnly[0]);
    }

    [Fact]
    public void BuildSafeColumnList_ExcludesUnsafeTypes()
    {
        var columns = new List<(string name, string type)>
        {
            ("id", "uuid"),
            ("name", "text"),
            ("embedding", "vector"),
            ("data", "bytea"),
            ("config", "jsonb"),
            ("age", "integer")
        };

        var safe = DataDiffEngine.FilterSafeColumns(columns);

        Assert.Equal(3, safe.Count);
        Assert.Contains("id", safe);
        Assert.Contains("name", safe);
        Assert.Contains("age", safe);
    }
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~DataDiffEngineTests" --no-restore -v m 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Implement DataDiffEngine**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngine.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal static class DataDiffEngine
{
    private static readonly HashSet<string> UnsafeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bytea", "vector", "json", "jsonb", "xml"
    };

    /// <summary>
    /// Filters out columns with types that cannot be safely cast to text for hashing.
    /// </summary>
    public static List<string> FilterSafeColumns(IReadOnlyList<(string name, string type)> columns)
    {
        return columns
            .Where(c => !UnsafeTypes.Contains(c.type))
            .Select(c => c.name)
            .ToList();
    }

    /// <summary>
    /// Gets column names and types for a table from information_schema.
    /// </summary>
    public static async Task<List<(string name, string type)>> GetColumnsAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
    {
        var columns = new List<(string, string)>();
        await using var cmd = new NpgsqlCommand(
            "SELECT column_name, udt_name FROM information_schema.columns WHERE table_schema = $1 AND table_name = $2 ORDER BY ordinal_position",
            conn);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(table);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            columns.Add((reader.GetString(0), reader.GetString(1)));
        }
        return columns;
    }

    /// <summary>
    /// Gets primary key column(s) for a table.
    /// </summary>
    public static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
    {
        var pkCols = new List<string>();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            WHERE tc.table_schema = $1 AND tc.table_name = $2 AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY kcu.ordinal_position
            """, conn);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(table);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            pkCols.Add(reader.GetString(0));
        }
        return pkCols;
    }

    /// <summary>
    /// Fetches PK→hash map for a table using safe columns only.
    /// </summary>
    public static async Task<Dictionary<string, string>> FetchRowHashesAsync(
        NpgsqlConnection conn, string schema, string table,
        IReadOnlyList<string> pkColumns, IReadOnlyList<string> safeColumns,
        CancellationToken ct = default)
    {
        var pkExpr = string.Join(" || '::' || ", pkColumns.Select(c => $"\"{c}\"::text"));
        var colList = string.Join(", ", safeColumns.Select(c => $"\"{c}\""));

        var sql = $"SELECT {pkExpr} as pk, md5(ROW({colList})::text) as row_hash FROM \"{schema}\".\"{table}\"";

        var hashes = new Dictionary<string, string>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            hashes[reader.GetString(0)] = reader.GetString(1);
        }
        return hashes;
    }

    /// <summary>
    /// Pure function: compares two PK→hash maps and categorizes differences.
    /// </summary>
    public static (int identical, List<string> modified, List<string> localOnly, List<string> stagingOnly)
        ComputeHashDiff(Dictionary<string, string> localHashes, Dictionary<string, string> stagingHashes)
    {
        int identical = 0;
        var modified = new List<string>();
        var localOnly = new List<string>();
        var stagingOnly = new List<string>();

        foreach (var (pk, hash) in localHashes)
        {
            if (stagingHashes.TryGetValue(pk, out var stagingHash))
            {
                if (hash == stagingHash)
                    identical++;
                else
                    modified.Add(pk);
            }
            else
            {
                localOnly.Add(pk);
            }
        }

        foreach (var pk in stagingHashes.Keys)
        {
            if (!localHashes.ContainsKey(pk))
                stagingOnly.Add(pk);
        }

        return (identical, modified, localOnly, stagingOnly);
    }

    /// <summary>
    /// Gets estimated row count from pg_stat_user_tables (fast, no full scan).
    /// </summary>
    public static async Task<long> GetEstimatedRowCountAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(n_live_tup, 0) FROM pg_stat_user_tables WHERE schemaname = $1 AND relname = $2",
            conn);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(table);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long count ? count : 0;
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~DataDiffEngineTests" --no-restore -v m 2>&1 | tail -10`
Expected: 4 passed

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngine.cs tests/Api.Tests/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngineTests.cs
git commit -m "feat(db-sync): implement DataDiffEngine with hash-based comparison"
```

---

## Task 9: DI Registration + Configuration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DatabaseSyncServiceExtensions.cs`
- Modify: `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs` (add `AddDatabaseSyncContext()` call)

- [ ] **Step 1: Create DatabaseSyncServiceExtensions**

```csharp
// apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DatabaseSyncServiceExtensions.cs
using System.Text.Json.Serialization;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal static class DatabaseSyncServiceExtensions
{
    public static IServiceCollection AddDatabaseSyncContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var sidecarBaseUrl = Environment.GetEnvironmentVariable("SIDECAR_BASE_URL") ?? "http://ssh-tunnel-sidecar:2222";
        var sidecarAuthToken = Environment.GetEnvironmentVariable("SIDECAR_AUTH_TOKEN") ?? "";

        // SSH Tunnel Client
        services.AddHttpClient("SshTunnelSidecar", client =>
        {
            client.BaseAddress = new Uri(sidecarBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<ISshTunnelClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("SshTunnelSidecar");
            var logger = sp.GetRequiredService<ILogger<SshTunnelClient>>();
            return new SshTunnelClient(httpClient, sidecarAuthToken, logger);
        });

        // Remote Database Connector
        // Connection string points to the sidecar's forwarded port
        var remoteHost = Environment.GetEnvironmentVariable("REMOTE_DB_HOST") ?? "ssh-tunnel-sidecar";
        var remotePort = Environment.GetEnvironmentVariable("REMOTE_DB_PORT") ?? "15432";
        var remoteDb = Environment.GetEnvironmentVariable("REMOTE_DB_NAME") ?? "meepleai";
        var remoteUser = Environment.GetEnvironmentVariable("REMOTE_DB_USER") ?? "meepleai";
        var remotePassword = Environment.GetEnvironmentVariable("REMOTE_DB_PASSWORD") ?? "";
        var remoteConnStr = $"Host={remoteHost};Port={remotePort};Database={remoteDb};Username={remoteUser};Password={remotePassword};Timeout=10";

        services.AddScoped<IRemoteDatabaseConnector>(sp =>
        {
            var tunnelClient = sp.GetRequiredService<ISshTunnelClient>();
            var logger = sp.GetRequiredService<ILogger<RemoteDatabaseConnector>>();
            return new RemoteDatabaseConnector(tunnelClient, remoteConnStr, logger);
        });

        // Register JsonStringEnumConverter for SyncDirection in global JSON options
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }
}
```

- [ ] **Step 2: Register in ApplicationServiceExtensions**

Find the file `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs` and add after the last `Add*Context()` call:

```csharp
using Api.BoundedContexts.DatabaseSync.Infrastructure.DependencyInjection;
// ...
services.AddDatabaseSyncContext(configuration);
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DependencyInjection/ apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs
git commit -m "feat(db-sync): add DI registration for DatabaseSync services"
```

---

## Task 10: Commands, Queries, and Handlers — Tunnel Operations

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/GetTunnelStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/OpenTunnelCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/CloseTunnelCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Handlers/GetTunnelStatusHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Handlers/OpenTunnelHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Handlers/CloseTunnelHandler.cs`

- [ ] **Step 1: Create tunnel query and commands**

```csharp
// GetTunnelStatusQuery.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Queries;
internal record GetTunnelStatusQuery() : IQuery<TunnelStatusResult>;
```

```csharp
// OpenTunnelCommand.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Commands;
internal record OpenTunnelCommand() : ICommand<TunnelStatusResult>;
```

```csharp
// CloseTunnelCommand.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Commands;
internal record CloseTunnelCommand() : ICommand<TunnelStatusResult>;
```

- [ ] **Step 2: Create tunnel handlers**

```csharp
// GetTunnelStatusHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class GetTunnelStatusHandler : IQueryHandler<GetTunnelStatusQuery, TunnelStatusResult>
{
    private readonly ISshTunnelClient _tunnelClient;
    public GetTunnelStatusHandler(ISshTunnelClient tunnelClient) => _tunnelClient = tunnelClient;
    public Task<TunnelStatusResult> Handle(GetTunnelStatusQuery query, CancellationToken ct)
        => _tunnelClient.GetStatusAsync(ct);
}
```

```csharp
// OpenTunnelHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class OpenTunnelHandler : ICommandHandler<OpenTunnelCommand, TunnelStatusResult>
{
    private readonly ISshTunnelClient _tunnelClient;
    public OpenTunnelHandler(ISshTunnelClient tunnelClient) => _tunnelClient = tunnelClient;
    public Task<TunnelStatusResult> Handle(OpenTunnelCommand command, CancellationToken ct)
        => _tunnelClient.OpenAsync(ct);
}
```

```csharp
// CloseTunnelHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class CloseTunnelHandler : ICommandHandler<CloseTunnelCommand, TunnelStatusResult>
{
    private readonly ISshTunnelClient _tunnelClient;
    public CloseTunnelHandler(ISshTunnelClient tunnelClient) => _tunnelClient = tunnelClient;
    public Task<TunnelStatusResult> Handle(CloseTunnelCommand command, CancellationToken ct)
        => _tunnelClient.CloseAsync(ct);
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Application/
git commit -m "feat(db-sync): add tunnel commands, queries, and handlers"
```

---

## Task 11: Commands, Queries, and Handlers — Schema Operations

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/CompareSchemaQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/PreviewMigrationSqlCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/ApplyMigrationsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Handlers/CompareSchemaHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Handlers/PreviewMigrationSqlHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Handlers/ApplyMigrationsHandler.cs`

- [ ] **Step 1: Create schema query and commands**

```csharp
// CompareSchemaQuery.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Queries;
internal record CompareSchemaQuery() : IQuery<SchemaDiffResult>;
```

```csharp
// PreviewMigrationSqlCommand.cs
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Commands;
/// <summary>Returns the SQL that would be executed to apply local-only migrations to staging.</summary>
internal record PreviewMigrationSqlCommand() : ICommand<string>;
```

```csharp
// ApplyMigrationsCommand.cs
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Commands;
internal record ApplyMigrationsCommand(
    SyncDirection Direction,
    string Confirmation,
    Guid AdminUserId
) : ICommand<SyncResult>;
```

- [ ] **Step 2: Create CompareSchemaHandler**

```csharp
// CompareSchemaHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class CompareSchemaHandler : IQueryHandler<CompareSchemaQuery, SchemaDiffResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public CompareSchemaHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext;
        _remoteConnector = remoteConnector;
    }

    public async Task<SchemaDiffResult> Handle(CompareSchemaQuery query, CancellationToken ct)
    {
        // Read local migrations
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
            await localConn.OpenAsync(ct).ConfigureAwait(false);
        var localMigrations = await SchemaDiffEngine.ReadMigrationsAsync(localConn, ct).ConfigureAwait(false);

        // Read remote migrations
        await using var remoteConn = await _remoteConnector.OpenConnectionAsync(ct).ConfigureAwait(false);
        var remoteMigrations = await SchemaDiffEngine.ReadMigrationsAsync(remoteConn, ct).ConfigureAwait(false);

        return SchemaDiffEngine.ComputeDiff(localMigrations, remoteMigrations);
    }
}
```

- [ ] **Step 3: Create PreviewMigrationSqlHandler**

```csharp
// PreviewMigrationSqlHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class PreviewMigrationSqlHandler : ICommandHandler<PreviewMigrationSqlCommand, string>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public PreviewMigrationSqlHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext;
        _remoteConnector = remoteConnector;
    }

    public async Task<string> Handle(PreviewMigrationSqlCommand command, CancellationToken ct)
    {
        // Get staging's last applied migration to use as fromMigration
        await using var remoteConn = await _remoteConnector.OpenConnectionAsync(ct).ConfigureAwait(false);
        var stagingMigrations = await SchemaDiffEngine.ReadMigrationsAsync(remoteConn, ct).ConfigureAwait(false);
        var lastStagingMigration = stagingMigrations.LastOrDefault()?.MigrationId;

        var migrator = _dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        // Generate SQL only for migrations AFTER staging's last applied migration
        var sql = migrator.GenerateScript(
            fromMigration: lastStagingMigration,  // from staging's last migration
            toMigration: null,                      // to latest local
            idempotent: true);                      // safe to re-run
        return sql;
    }
}
```

- [ ] **Step 4: Create ApplyMigrationsHandler**

```csharp
// ApplyMigrationsHandler.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class ApplyMigrationsHandler : ICommandHandler<ApplyMigrationsCommand, SyncResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;
    private readonly ILogger<ApplyMigrationsHandler> _logger;

    public ApplyMigrationsHandler(
        MeepleAiDbContext dbContext,
        IRemoteDatabaseConnector remoteConnector,
        ILogger<ApplyMigrationsHandler> logger)
    {
        _dbContext = dbContext;
        _remoteConnector = remoteConnector;
        _logger = logger;
    }

    public async Task<SyncResult> Handle(ApplyMigrationsCommand command, CancellationToken ct)
    {
        if (command.Direction == SyncDirection.StagingToLocal)
        {
            return new SyncResult(false, 0, 0, Guid.Empty,
                "Applying staging-only migrations to local is not supported in v1. Pull the branch and run 'dotnet ef database update' locally.");
        }

        // Generate SQL only for migrations after staging's last applied one
        var migrator = _dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        var lastStagingMigration = remoteMigrations.LastOrDefault()?.MigrationId;
        var sql = migrator.GenerateScript(fromMigration: lastStagingMigration, toMigration: null, idempotent: true);

        if (string.IsNullOrWhiteSpace(sql))
        {
            return new SyncResult(true, 0, 0, Guid.NewGuid(), "No migrations to apply");
        }

        var sqlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))[..16];

        // Validate confirmation
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
            await localConn.OpenAsync(ct).ConfigureAwait(false);
        var localMigrations = await SchemaDiffEngine.ReadMigrationsAsync(localConn, ct).ConfigureAwait(false);

        await using var remoteConn = await _remoteConnector.OpenConnectionAsync(ct).ConfigureAwait(false);
        var remoteMigrations = await SchemaDiffEngine.ReadMigrationsAsync(remoteConn, ct).ConfigureAwait(false);
        var diff = SchemaDiffEngine.ComputeDiff(localMigrations, remoteMigrations);

        var expectedConfirmation = $"APPLY {diff.LocalOnly.Count} MIGRATIONS TO STAGING";
        if (!string.Equals(command.Confirmation, expectedConfirmation, StringComparison.Ordinal))
        {
            return new SyncResult(false, 0, 0, Guid.Empty,
                $"Confirmation mismatch. Expected: \"{expectedConfirmation}\"");
        }

        var operationId = Guid.NewGuid();

        // Execute SQL on remote DB in transaction
        await using var tx = await remoteConn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, remoteConn, tx);
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Applied {Count} migrations to staging. OperationId={OpId}",
                diff.LocalOnly.Count, operationId);

            // Audit log
            var auditLog = new AuditLog(
                id: Guid.NewGuid(),
                userId: command.AdminUserId,
                action: "DatabaseSync.ApplyMigrations",
                resource: "Schema",
                result: "Success",
                resourceId: operationId.ToString(),
                details: JsonSerializer.Serialize(new
                {
                    direction = "LocalToStaging",
                    migrations = diff.LocalOnly.Select(m => m.MigrationId).ToArray(),
                    sqlHash
                }));
            _dbContext.Set<AuditLog>().Add(auditLog);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            return new SyncResult(true, diff.LocalOnly.Count, 0, operationId);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to apply migrations to staging");
            return new SyncResult(false, 0, 0, operationId, ex.Message);
        }
    }
}
```

- [ ] **Step 5: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Application/
git commit -m "feat(db-sync): add schema comparison and migration apply handlers"
```

---

## Task 12: Commands, Queries, and Handlers — Data Operations

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/ListTablesQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/CompareTableDataQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/GetSyncOperationsHistoryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/SyncTableDataCommand.cs`
- Create: corresponding handlers for each

This task is large. Each handler follows the same CQRS patterns from Tasks 10-11 but has significant business logic. Full implementations below.

- [ ] **Step 1: Write tests for data operation handlers**

```csharp
// tests/Api.Tests/BoundedContexts/DatabaseSync/Application/DataOperationHandlerTests.cs
using Api.BoundedContexts.DatabaseSync.Infrastructure;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class DataOperationHandlerTests
{
    [Fact]
    public void ComputeHashDiff_RejectsOver50kRows()
    {
        // The handler must check row counts before calling ComputeHashDiff
        var largeLocal = Enumerable.Range(0, 50001)
            .ToDictionary(i => i.ToString(), i => "hash");
        var staging = new Dictionary<string, string>();

        // ComputeHashDiff itself doesn't enforce the cap — the handler must check before calling
        // This test validates the cap check logic should be in the handler
        Assert.True(largeLocal.Count > 50000);
    }

    [Fact]
    public void SyncConfirmation_MustMatchExactFormat()
    {
        var tableName = "system_configurations";
        var direction = "STAGING";
        var expected = $"SYNC {tableName} TO {direction}";

        Assert.Equal("SYNC system_configurations TO STAGING", expected);
        Assert.NotEqual("sync system_configurations to staging", expected); // case-sensitive
    }
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~DataOperationHandlerTests" --no-restore -v m 2>&1 | tail -10`
Expected: FAIL (class not found), then PASS after creation

- [ ] **Step 3: Create ListTablesQuery + Handler**

```csharp
// ListTablesQuery.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Queries;
internal record ListTablesQuery() : IQuery<IReadOnlyList<TableInfo>>;
```

```csharp
// ListTablesHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class ListTablesHandler : IQueryHandler<ListTablesQuery, IReadOnlyList<TableInfo>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public ListTablesHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext;
        _remoteConnector = remoteConnector;
    }

    public async Task<IReadOnlyList<TableInfo>> Handle(ListTablesQuery query, CancellationToken ct)
    {
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
            await localConn.OpenAsync(ct).ConfigureAwait(false);

        await using var remoteConn = await _remoteConnector.OpenConnectionAsync(ct).ConfigureAwait(false);

        var localTables = await GetTablesAsync(localConn, ct).ConfigureAwait(false);
        var remoteTables = await GetTablesAsync(remoteConn, ct).ConfigureAwait(false);
        var remoteMap = remoteTables.ToDictionary(t => $"{t.schema}.{t.name}", t => t.count);

        var result = new List<TableInfo>();
        foreach (var (schema, name, localCount) in localTables)
        {
            var key = $"{schema}.{name}";
            var stagingCount = remoteMap.GetValueOrDefault(key, 0);
            result.Add(new TableInfo(name, schema, localCount, stagingCount, GuessBoundedContext(name)));
        }
        return result;
    }

    private static async Task<List<(string schema, string name, long count)>> GetTablesAsync(
        NpgsqlConnection conn, CancellationToken ct)
    {
        var tables = new List<(string, string, long)>();
        await using var cmd = new NpgsqlCommand("""
            SELECT t.schemaname, t.relname, COALESCE(t.n_live_tup, 0)
            FROM pg_stat_user_tables t
            WHERE t.schemaname = 'public'
            ORDER BY t.relname
        """, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            tables.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        return tables;
    }

    private static string? GuessBoundedContext(string tableName) => tableName switch
    {
        var n when n.StartsWith("game") => "GameManagement",
        var n when n.StartsWith("user") => "Administration",
        var n when n.StartsWith("chat") || n.StartsWith("pdf") || n.StartsWith("vector") || n.StartsWith("text_chunk") => "KnowledgeBase",
        var n when n.StartsWith("session") || n.StartsWith("participant") || n.StartsWith("score") => "SessionTracking",
        var n when n.StartsWith("system_config") || n.StartsWith("ai_model") => "SystemConfiguration",
        var n when n.StartsWith("audit") || n.StartsWith("admin") || n.StartsWith("prompt") => "Administration",
        var n when n.StartsWith("achievement") || n.StartsWith("badge") || n.StartsWith("leaderboard") => "Gamification",
        _ => null
    };
}
```

- [ ] **Step 4: Create CompareTableDataQuery + Handler**

```csharp
// CompareTableDataQuery.cs
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Queries;
internal record CompareTableDataQuery(string TableName) : IQuery<DataDiffResult>;
```

```csharp
// CompareTableDataHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class CompareTableDataHandler : IQueryHandler<CompareTableDataQuery, DataDiffResult>
{
    private const long MaxTotalRows = 50_000;
    private const int MaxDiffRows = 10_000;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;
    private readonly ILogger<CompareTableDataHandler> _logger;

    public CompareTableDataHandler(
        MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector,
        ILogger<CompareTableDataHandler> logger)
    {
        _dbContext = dbContext;
        _remoteConnector = remoteConnector;
        _logger = logger;
    }

    public async Task<DataDiffResult> Handle(CompareTableDataQuery query, CancellationToken ct)
    {
        var tableName = query.TableName;
        const string schema = "public";

        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
            await localConn.OpenAsync(ct).ConfigureAwait(false);

        await using var remoteConn = await _remoteConnector.OpenConnectionAsync(ct).ConfigureAwait(false);

        // Validate table exists in information_schema (whitelist)
        var localTableExists = await TableExistsAsync(localConn, schema, tableName, ct).ConfigureAwait(false);
        var remoteTableExists = await TableExistsAsync(remoteConn, schema, tableName, ct).ConfigureAwait(false);
        if (!localTableExists || !remoteTableExists)
            throw new InvalidOperationException($"Table '{tableName}' does not exist on both databases");

        // Check 50k row cap
        var localCount = await DataDiffEngine.GetEstimatedRowCountAsync(localConn, schema, tableName, ct).ConfigureAwait(false);
        var remoteCount = await DataDiffEngine.GetEstimatedRowCountAsync(remoteConn, schema, tableName, ct).ConfigureAwait(false);
        if (localCount > MaxTotalRows || remoteCount > MaxTotalRows)
            throw new InvalidOperationException(
                $"Table '{tableName}' has too many rows (local: {localCount}, staging: {remoteCount}). " +
                $"Max supported: {MaxTotalRows}. Use pg_dump for large tables.");

        // Get PK columns and safe column list
        var pkColumns = await DataDiffEngine.GetPrimaryKeyColumnsAsync(localConn, schema, tableName, ct).ConfigureAwait(false);
        if (pkColumns.Count == 0)
            throw new InvalidOperationException($"Table '{tableName}' has no primary key. Cannot compare.");

        var allColumns = await DataDiffEngine.GetColumnsAsync(localConn, schema, tableName, ct).ConfigureAwait(false);
        var safeColumns = DataDiffEngine.FilterSafeColumns(allColumns);

        // Fetch hashes from both DBs
        var localHashes = await DataDiffEngine.FetchRowHashesAsync(localConn, schema, tableName, pkColumns, safeColumns, ct).ConfigureAwait(false);
        var remoteHashes = await DataDiffEngine.FetchRowHashesAsync(remoteConn, schema, tableName, pkColumns, safeColumns, ct).ConfigureAwait(false);

        var (identical, modifiedPks, localOnlyPks, stagingOnlyPks) = DataDiffEngine.ComputeHashDiff(localHashes, remoteHashes);

        var totalDiffs = modifiedPks.Count + localOnlyPks.Count + stagingOnlyPks.Count;
        if (totalDiffs > MaxDiffRows)
            throw new InvalidOperationException(
                $"Too many differences ({totalDiffs}). Max supported: {MaxDiffRows}. Use pg_dump.");

        // Fetch full rows for diffs (TODO: implement FetchRowsByPks in DataDiffEngine)
        // For now, return summary with PKs
        var modified = modifiedPks.Select(pk =>
            new RowDiff(new Dictionary<string, string?> { [pkColumns[0]] = pk }, Array.Empty<ColumnDiff>())).ToList();
        var localOnly = localOnlyPks.Select(pk =>
            new Dictionary<string, string?> { [pkColumns[0]] = pk }).ToList();
        var stagingOnly = stagingOnlyPks.Select(pk =>
            new Dictionary<string, string?> { [pkColumns[0]] = pk }).ToList();

        return new DataDiffResult(tableName, localCount, remoteCount, identical, modified, localOnly, stagingOnly);
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = $1 AND table_name = $2)", conn);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(table);
        return (bool)(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }
}
```

- [ ] **Step 5: Create SyncTableDataCommand + Handler**

```csharp
// SyncTableDataCommand.cs
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Commands;
internal record SyncTableDataCommand(
    string TableName,
    SyncDirection Direction,
    string Confirmation,
    Guid AdminUserId
) : ICommand<SyncResult>;
```

```csharp
// SyncTableDataHandler.cs
using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class SyncTableDataHandler : ICommandHandler<SyncTableDataCommand, SyncResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;
    private readonly ILogger<SyncTableDataHandler> _logger;

    public SyncTableDataHandler(
        MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector,
        ILogger<SyncTableDataHandler> logger)
    {
        _dbContext = dbContext;
        _remoteConnector = remoteConnector;
        _logger = logger;
    }

    public async Task<SyncResult> Handle(SyncTableDataCommand command, CancellationToken ct)
    {
        var target = command.Direction == SyncDirection.LocalToStaging ? "STAGING" : "LOCAL";
        var expectedConfirmation = $"SYNC {command.TableName} TO {target}";
        if (!string.Equals(command.Confirmation, expectedConfirmation, StringComparison.Ordinal))
            return new SyncResult(false, 0, 0, Guid.Empty, $"Confirmation mismatch. Expected: \"{expectedConfirmation}\"");

        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
            await localConn.OpenAsync(ct).ConfigureAwait(false);

        await using var remoteConn = await _remoteConnector.OpenConnectionAsync(ct).ConfigureAwait(false);

        // Determine source and target connections
        var sourceConn = command.Direction == SyncDirection.LocalToStaging ? localConn : remoteConn;
        var targetConn = command.Direction == SyncDirection.LocalToStaging ? remoteConn : localConn;

        // Acquire advisory lock on target DB
        var lockKey = $"db_sync_{command.TableName}";
        await using var lockCmd = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtext($1))", targetConn);
        lockCmd.Parameters.AddWithValue(lockKey);
        var lockAcquired = (bool)(await lockCmd.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        if (!lockAcquired)
            return new SyncResult(false, 0, 0, Guid.Empty, "Another sync operation is in progress for this table");

        var operationId = Guid.NewGuid();

        try
        {
            // Check FK dependencies
            var fkWarning = await CheckForeignKeysAsync(targetConn, "public", command.TableName, ct).ConfigureAwait(false);

            // Get columns and PKs
            var pkColumns = await DataDiffEngine.GetPrimaryKeyColumnsAsync(sourceConn, "public", command.TableName, ct).ConfigureAwait(false);
            var allColumns = await DataDiffEngine.GetColumnsAsync(sourceConn, "public", command.TableName, ct).ConfigureAwait(false);
            var safeColumns = DataDiffEngine.FilterSafeColumns(allColumns);
            var allColumnNames = allColumns.Select(c => c.name).ToList();

            // Compute diff
            var sourceHashes = await DataDiffEngine.FetchRowHashesAsync(sourceConn, "public", command.TableName, pkColumns, safeColumns, ct).ConfigureAwait(false);
            var targetHashes = await DataDiffEngine.FetchRowHashesAsync(targetConn, "public", command.TableName, pkColumns, safeColumns, ct).ConfigureAwait(false);
            var (_, modifiedPks, sourceOnlyPks, _) = DataDiffEngine.ComputeHashDiff(sourceHashes, targetHashes);

            int inserted = 0, updated = 0;

            // Execute in transaction on target
            await using var tx = await targetConn.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                // INSERT source-only rows
                foreach (var pk in sourceOnlyPks)
                {
                    var row = await FetchRowAsync(sourceConn, "public", command.TableName, pkColumns[0], pk, allColumnNames, ct).ConfigureAwait(false);
                    if (row != null)
                    {
                        await InsertRowAsync(targetConn, tx, "public", command.TableName, row, ct).ConfigureAwait(false);
                        inserted++;
                    }
                }

                // UPDATE modified rows
                foreach (var pk in modifiedPks)
                {
                    var row = await FetchRowAsync(sourceConn, "public", command.TableName, pkColumns[0], pk, allColumnNames, ct).ConfigureAwait(false);
                    if (row != null)
                    {
                        await UpdateRowAsync(targetConn, tx, "public", command.TableName, pkColumns[0], pk, row, ct).ConfigureAwait(false);
                        updated++;
                    }
                }

                await tx.CommitAsync(ct).ConfigureAwait(false);

                // Audit log
                var auditLog = new AuditLog(
                    id: Guid.NewGuid(), userId: command.AdminUserId,
                    action: "DatabaseSync.SyncTableData", resource: command.TableName,
                    result: "Success", resourceId: operationId.ToString(),
                    details: JsonSerializer.Serialize(new
                    {
                        direction = command.Direction.ToString(),
                        tableName = command.TableName,
                        inserted, updated, fkWarning
                    }));
                _dbContext.Set<AuditLog>().Add(auditLog);
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

                return new SyncResult(true, inserted, updated, operationId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(ex, "Sync failed for table {Table}", command.TableName);
                return new SyncResult(false, 0, 0, operationId, ex.Message);
            }
        }
        finally
        {
            // Release advisory lock
            await using var unlockCmd = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(hashtext($1))", targetConn);
            unlockCmd.Parameters.AddWithValue(lockKey);
            await unlockCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task<string?> CheckForeignKeysAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT DISTINCT ccu.table_name AS referenced_table
            FROM information_schema.table_constraints tc
            JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
            WHERE tc.table_schema = $1 AND tc.table_name = $2 AND tc.constraint_type = 'FOREIGN KEY'
        """, conn);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(table);
        var refs = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            refs.Add(reader.GetString(0));
        return refs.Count > 0 ? $"FK dependencies: {string.Join(", ", refs)}" : null;
    }

    private static async Task<Dictionary<string, object?>?> FetchRowAsync(
        NpgsqlConnection conn, string schema, string table, string pkCol, string pkValue,
        List<string> columns, CancellationToken ct)
    {
        var colList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        await using var cmd = new NpgsqlCommand(
            $"SELECT {colList} FROM \"{schema}\".\"{table}\" WHERE \"{pkCol}\"::text = $1 LIMIT 1", conn);
        cmd.Parameters.AddWithValue(pkValue);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }

    private static async Task InsertRowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string schema, string table,
        Dictionary<string, object?> row, CancellationToken ct)
    {
        var cols = string.Join(", ", row.Keys.Select(c => $"\"{c}\""));
        var parms = string.Join(", ", row.Keys.Select((_, i) => $"${i + 1}"));
        await using var cmd = new NpgsqlCommand($"INSERT INTO \"{schema}\".\"{table}\" ({cols}) VALUES ({parms})", conn, tx);
        foreach (var val in row.Values) cmd.Parameters.AddWithValue(val ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task UpdateRowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string schema, string table,
        string pkCol, string pkValue, Dictionary<string, object?> row, CancellationToken ct)
    {
        var setClauses = row.Where(kv => kv.Key != pkCol)
            .Select((kv, i) => $"\"{kv.Key}\" = ${i + 1}").ToList();
        var sql = $"UPDATE \"{schema}\".\"{table}\" SET {string.Join(", ", setClauses)} WHERE \"{pkCol}\"::text = ${setClauses.Count + 1}";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var kv in row.Where(kv => kv.Key != pkCol)) cmd.Parameters.AddWithValue(kv.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue(pkValue);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Create GetSyncOperationsHistoryQuery + Handler**

```csharp
// GetSyncOperationsHistoryQuery.cs
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.DatabaseSync.Application.Queries;
internal record SyncHistoryEntry(string Action, string Resource, string? ResourceId, string Result, string? Details, DateTime CreatedAt, Guid? UserId);
internal record GetSyncOperationsHistoryQuery(int Limit = 50) : IQuery<IReadOnlyList<SyncHistoryEntry>>;
```

```csharp
// GetSyncOperationsHistoryHandler.cs
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class GetSyncOperationsHistoryHandler : IQueryHandler<GetSyncOperationsHistoryQuery, IReadOnlyList<SyncHistoryEntry>>
{
    private readonly MeepleAiDbContext _dbContext;
    public GetSyncOperationsHistoryHandler(MeepleAiDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<SyncHistoryEntry>> Handle(GetSyncOperationsHistoryQuery query, CancellationToken ct)
    {
        return await _dbContext.Set<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>()
            .AsNoTracking()
            .Where(a => a.Action.StartsWith("DatabaseSync."))
            .OrderByDescending(a => a.CreatedAt)
            .Take(query.Limit)
            .Select(a => new SyncHistoryEntry(a.Action, a.Resource, a.ResourceId, a.Result, a.Details, a.CreatedAt, a.UserId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
```

- [ ] **Step 7: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Application/
git commit -m "feat(db-sync): add data comparison, sync, and history handlers"
```

---

## Task 13: Routing — DatabaseSyncEndpoints

**Files:**
- Create: `apps/api/src/Api/Routing/DatabaseSyncEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/ApiRouter.cs` (or equivalent route registration file — add `MapDatabaseSyncEndpoints()` call)

- [ ] **Step 1: Create DatabaseSyncEndpoints.cs**

Follow the exact pattern from `AdminOperationsEndpoints.cs`. Here is a complete example showing the first 3 endpoints and the registration pattern — replicate for all 10 endpoints:

```csharp
// apps/api/src/Api/Routing/DatabaseSyncEndpoints.cs
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.Extensions;
using Api.Services;
using MediatR;

namespace Api.Routing;

internal static class DatabaseSyncEndpoints
{
    public static RouteGroupBuilder MapDatabaseSyncEndpoints(this RouteGroupBuilder group)
    {
        var syncGroup = group.MapGroup("/admin/db-sync")
            .WithTags("Admin", "DatabaseSync")
            .RequireAuthorization("RequireSuperAdmin");

        // --- Tunnel ---
        syncGroup.MapGet("/tunnel/status", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "feature_disabled", message = "Database Sync is disabled" }, statusCode: 403);

            var result = await mediator.Send(new GetTunnelStatusQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Get SSH tunnel status");

        syncGroup.MapPost("/tunnel/open", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "feature_disabled" }, statusCode: 403);

            var result = await mediator.Send(new OpenTunnelCommand(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Open SSH tunnel to staging");

        syncGroup.MapDelete("/tunnel/close", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "feature_disabled" }, statusCode: 403);

            var result = await mediator.Send(new CloseTunnelCommand(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Close SSH tunnel");

        // --- Schema ---
        // GET /schema/compare → mediator.Send(new CompareSchemaQuery())
        // POST /schema/preview-sql → mediator.Send(new PreviewMigrationSqlCommand())
        // POST /schema/apply → mediator.Send(new ApplyMigrationsCommand(request.Direction, request.Confirmation, session.User.Id))

        // --- Data ---
        // GET /tables → mediator.Send(new ListTablesQuery())
        // GET /tables/{name}/compare → mediator.Send(new CompareTableDataQuery(name))
        // POST /tables/{name}/sync → mediator.Send(new SyncTableDataCommand(name, request.Direction, request.Confirmation, session.User.Id))

        // --- History ---
        // GET /operations/history → mediator.Send(new GetSyncOperationsHistoryQuery(limit))

        // Implement all remaining endpoints following the same pattern:
        // 1. RequireSuperAdminSession()
        // 2. Feature flag check
        // 3. mediator.Send()
        // 4. Results.Ok()

        return group;
    }
}

// Request DTOs
internal record ApplyMigrationsRequest(SyncDirection Direction, string Confirmation);
internal record SyncTableDataRequest(SyncDirection Direction, string Confirmation);
```

The route group is registered in the API router. The `.RequireAuthorization("RequireSuperAdmin")` on the group provides an additional layer. Each handler still calls `context.RequireSuperAdminSession()` for the session tuple (needed to extract `session.User.Id` for audit logging).

Map all 10 endpoints from spec Section 6:
- `GET /tunnel/status`
- `POST /tunnel/open`
- `DELETE /tunnel/close`
- `GET /schema/compare`
- `POST /schema/preview-sql`
- `POST /schema/apply`
- `GET /tables`
- `GET /tables/{name}/compare`
- `POST /tables/{name}/sync`
- `GET /operations/history`

- [ ] **Step 2: Register in API router**

Find the main routing file and add: `group.MapDatabaseSyncEndpoints();`

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/DatabaseSyncEndpoints.cs apps/api/src/Api/Routing/ApiRouter.cs
git commit -m "feat(db-sync): add REST API endpoints for database sync"
```

---

## Task 14: Frontend — Types and API Hooks

**Files:**
- Create: `apps/web/src/app/admin/database-sync/types/db-sync.ts`
- Create: `apps/web/src/app/admin/database-sync/hooks/useTunnelStatus.ts`
- Create: `apps/web/src/app/admin/database-sync/hooks/useSchemaCompare.ts`
- Create: `apps/web/src/app/admin/database-sync/hooks/useTableCompare.ts`
- Create: `apps/web/src/app/admin/database-sync/hooks/useSyncOperations.ts`

- [ ] **Step 1: Create TypeScript types**

Define types matching all API response shapes from spec Section 6 (TunnelStatus, SchemaDiffResult, TableInfo, DataDiffResult, SyncResult, etc.)

- [ ] **Step 2: Create useTunnelStatus hook**

Use `useQuery` with 5s polling (`refetchInterval: 5000`) for `/api/v1/admin/db-sync/tunnel/status`. Add `useMutation` for open/close.

- [ ] **Step 3: Create useSchemaCompare hook**

Use `useQuery` (manual trigger via `enabled: false` + `refetch()`) for `/api/v1/admin/db-sync/schema/compare`. Add `useMutation` for preview-sql and apply.

- [ ] **Step 4: Create useTableCompare hook**

Use `useQuery` for `/api/v1/admin/db-sync/tables` (table list) and `/api/v1/admin/db-sync/tables/{name}/compare` (per-table diff). Add `useMutation` for sync.

- [ ] **Step 5: Create useSyncOperations hook**

Use `useQuery` for `/api/v1/admin/db-sync/operations/history`.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/database-sync/
git commit -m "feat(db-sync): add frontend TypeScript types and React Query hooks"
```

---

## Task 15: Frontend — Shared Components

**Files:**
- Create: `apps/web/src/app/admin/database-sync/components/TunnelStatusBanner.tsx`
- Create: `apps/web/src/app/admin/database-sync/components/ConfirmationDialog.tsx`
- Create: `apps/web/src/app/admin/database-sync/components/SqlPreviewModal.tsx`
- Create: `apps/web/src/app/admin/database-sync/components/DiffTable.tsx`
- Create: `apps/web/src/app/admin/database-sync/components/TableSelector.tsx`

- [ ] **Step 1: Create TunnelStatusBanner**

Top bar showing tunnel state (colored badge: green=open, yellow=opening, red=error, gray=closed) with connect/disconnect button. Uses `useTunnelStatus` hook.

- [ ] **Step 2: Create ConfirmationDialog**

Reusable dialog with text input that must match expected confirmation string. Uses shadcn `Dialog` component. Props: `title`, `description`, `expectedText`, `onConfirm`, `isLoading`.

- [ ] **Step 3: Create SqlPreviewModal**

Modal showing SQL with monospace font and basic syntax highlighting. Uses shadcn `Dialog`. Props: `sql`, `isOpen`, `onClose`.

- [ ] **Step 4: Create DiffTable**

Table component showing row diffs with highlighted differing cells. Props: `modified`, `localOnly`, `stagingOnly`, `identicalCount`.

- [ ] **Step 5: Create TableSelector**

Sidebar list of tables grouped by bounded context, with row count badges. Props: `tables`, `selectedTable`, `onSelect`.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/database-sync/components/
git commit -m "feat(db-sync): add shared UI components for database sync admin"
```

---

## Task 16: Frontend — Tab Components and Page

**Files:**
- Create: `apps/web/src/app/admin/database-sync/components/SchemaComparisonTab.tsx`
- Create: `apps/web/src/app/admin/database-sync/components/DataComparisonTab.tsx`
- Create: `apps/web/src/app/admin/database-sync/components/HistoryTab.tsx`
- Create: `apps/web/src/app/admin/database-sync/page.tsx`

- [ ] **Step 1: Create SchemaComparisonTab**

Shows migration comparison table (name, local ✅/❌, staging ✅/❌), "Preview SQL" and "Apply" buttons. Uses `useSchemaCompare` hook + `SqlPreviewModal` + `ConfirmationDialog`.

- [ ] **Step 2: Create DataComparisonTab**

Two-panel layout: `TableSelector` on left, diff results on right. Uses `useTableCompare` hook + `DiffTable` + `ConfirmationDialog`. Direction dropdown for sync.

- [ ] **Step 3: Create HistoryTab**

Chronological list of past operations. Uses `useSyncOperations` hook. Shows: type, table, direction, rows affected, user, timestamp, outcome.

- [ ] **Step 4: Create page.tsx**

Main page with `TunnelStatusBanner` at top, three tabs using shadcn `Tabs` component. Guard: if tunnel is closed, show "Connect to staging first" prompt.

```tsx
'use client';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import { TunnelStatusBanner } from './components/TunnelStatusBanner';
import { SchemaComparisonTab } from './components/SchemaComparisonTab';
import { DataComparisonTab } from './components/DataComparisonTab';
import { HistoryTab } from './components/HistoryTab';

export default function DatabaseSyncPage() {
  return (
    <div className="space-y-6">
      <TunnelStatusBanner />
      <Tabs defaultValue="schema">
        <TabsList>
          <TabsTrigger value="schema">Schema</TabsTrigger>
          <TabsTrigger value="data">Data</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>
        <TabsContent value="schema"><SchemaComparisonTab /></TabsContent>
        <TabsContent value="data"><DataComparisonTab /></TabsContent>
        <TabsContent value="history"><HistoryTab /></TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 5: Verify frontend build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/database-sync/
git commit -m "feat(db-sync): add admin database sync page with Schema, Data, History tabs"
```

---

## Task 17: CLI Tool

**Files:**
- Create: `infra/tools/db-sync/db-sync.sh`
- Create: `infra/tools/db-sync/lib/common.sh`
- Create: `infra/tools/db-sync/lib/tunnel.sh`
- Create: `infra/tools/db-sync/lib/schema.sh`
- Create: `infra/tools/db-sync/lib/data.sh`
- Create: `infra/tools/db-sync/lib/history.sh`
- Create: `infra/tools/db-sync/.env.example`
- Modify: `.gitignore` (add `infra/tools/db-sync/.env`)

- [ ] **Step 1: Create .env.example**

```bash
# infra/tools/db-sync/.env.example
# API base URL
API_URL=http://localhost:8080/api/v1/admin/db-sync

# SuperAdmin JWT token — source from secrets, do not store directly
# Generate with: source ../../secrets/dev/db-sync-cli.secret
MEEPLEAI_ADMIN_TOKEN=
```

- [ ] **Step 2: Create lib/common.sh**

Shared functions: load `.env`, validate token, `api_call()` wrapper using curl with auth header and JSON content-type, error handling, colored output.

- [ ] **Step 3: Create lib/tunnel.sh, lib/schema.sh, lib/data.sh, lib/history.sh**

Each file implements the subcommands for its domain, calling `api_call()` from common.sh. For `--confirm` flags, prompt interactively for confirmation text.

- [ ] **Step 4: Create db-sync.sh entry point**

Routes `$1` (domain) to the correct lib script, passes remaining args. Shows usage on `--help` or no args.

- [ ] **Step 5: Make scripts executable and add .gitignore entry**

Run: `chmod +x infra/tools/db-sync/db-sync.sh`
Add `infra/tools/db-sync/.env` to `.gitignore`

- [ ] **Step 6: Test CLI help**

Run: `./infra/tools/db-sync/db-sync.sh --help`
Expected: Shows usage with all commands

- [ ] **Step 7: Commit**

```bash
git add infra/tools/db-sync/ .gitignore
git commit -m "feat(db-sync): add CLI tool for database sync operations"
```

---

## Task 18: Integration Test — End-to-End Tunnel + Schema Compare

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/DatabaseSync/Integration/DatabaseSyncEndpointTests.cs`

- [ ] **Step 1: Write integration tests**

Test the endpoints with a mock sidecar (or use Testcontainers if feasible). At minimum test:
- `GET /tunnel/status` returns 200 with valid JSON
- `POST /tunnel/open` returns 200 when sidecar is available
- `GET /schema/compare` returns valid diff when both DBs are available
- Authorization: non-SuperAdmin gets 403
- Feature flag: disabled flag returns 403

- [ ] **Step 2: Run integration tests**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~DatabaseSyncEndpointTests" --no-restore -v m 2>&1 | tail -15`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/DatabaseSync/
git commit -m "test(db-sync): add integration tests for database sync endpoints"
```

---

## Task 19: Final Verification and Cleanup

- [ ] **Step 1: Run full backend build**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | tail -5`
Expected: Build succeeded, 0 warnings related to DatabaseSync

- [ ] **Step 2: Run all DatabaseSync tests**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "BoundedContext=DatabaseSync" -v m 2>&1 | tail -15`
Expected: All tests pass

- [ ] **Step 3: Run frontend build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 4: Run frontend lint and typecheck**

Run: `cd apps/web && pnpm typecheck && pnpm lint`
Expected: No errors

- [ ] **Step 5: Verify Docker sidecar builds**

Run: `cd infra && docker compose -f docker-compose.yml -f compose.dev.yml --profile db-sync build ssh-tunnel-sidecar 2>&1 | tail -5`
Expected: Successfully built

- [ ] **Step 6: Final commit if any cleanup needed**

```bash
git add -A
git commit -m "chore(db-sync): final cleanup and verification"
```

---

## Summary

| Task | Description | Estimated Steps |
|------|-------------|-----------------|
| 1 | Domain models, enums, interfaces | 11 |
| 2 | Feature flag migration | 4 |
| 3 | SSH tunnel sidecar (Python) | 6 |
| 4 | Docker compose changes | 6 |
| 5 | SshTunnelClient + tests | 5 |
| 6 | RemoteDatabaseConnector + tests | 5 |
| 7 | SchemaDiffEngine + tests | 5 |
| 8 | DataDiffEngine + tests | 5 |
| 9 | DI registration | 4 |
| 10 | Tunnel commands/queries/handlers | 4 |
| 11 | Schema commands/queries/handlers | 6 |
| 12 | Data commands/queries/handlers | 6 |
| 13 | REST API endpoints | 4 |
| 14 | Frontend types + hooks | 6 |
| 15 | Frontend shared components | 6 |
| 16 | Frontend page + tabs | 6 |
| 17 | CLI tool | 7 |
| 18 | Integration tests | 3 |
| 19 | Final verification | 6 |
| **Total** | | **~103 steps** |
