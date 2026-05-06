# Database Sync — Admin Tool for Schema & Data Comparison

**Date**: 2026-03-18
**Status**: Draft
**Author**: AI-assisted design
**Bounded Context**: DatabaseSync (new)

## 1. Overview

Admin tool that enables SuperAdmin users to compare and synchronize PostgreSQL databases between local and staging environments. Supports both schema (migration) comparison and data comparison, with bidirectional sync capabilities.

### Goals

- Detect schema drift between local and staging (missing migrations)
- Compare data in selected tables across environments
- Synchronize in either direction (local → staging, staging → local) per table
- Provide both a web UI (admin panel) and CLI interface
- Maintain full audit trail of all sync operations

### Non-Goals (v1)

- Rollback of applied migrations
- DELETE operations during data sync (only INSERT + UPDATE)
- Binary/blob column diff (shown as "[binary data differs]")
- Tables with >10,000 rows of difference (suggest pg_dump)
- Sync of tables with custom triggers
- Applying staging-only migrations to local (requires local migration classes; developer must pull branch and run `dotnet ef database update`)
- Tables with >50,000 total rows (suggest pg_dump)
- Production environment support

## 2. Architecture

### High-Level Diagram

```
┌─────────────────────────────────────┐
│         Admin UI (Next.js)          │
│  /admin/database-sync               │
└──────────┬──────────────────────────┘
           │ REST API
┌──────────▼──────────────────────────┐
│     API Monolite (.NET 9)           │
│  BoundedContexts/DatabaseSync/      │
│  ├── Domain/                        │
│  │   ├── SchemaDiff                 │
│  │   ├── DataDiff                   │
│  │   ├── SyncOperation              │
│  │   ├── SyncResult                 │
│  ├── Application/                   │
│  │   ├── Commands/                  │
│  │   │   ├── OpenTunnelCommand      │
│  │   │   ├── CloseTunnelCommand     │
│  │   │   ├── ApplyMigrationsCommand │
│  │   │   ├── SyncTableDataCommand   │
│  │   │   ├── PreviewMigrationSqlCmd │
│  │   ├── Queries/                   │
│  │   │   ├── GetTunnelStatusQuery   │
│  │   │   ├── CompareSchemaQuery     │
│  │   │   ├── CompareTableDataQuery  │
│  │   │   ├── ListTablesQuery        │
│  │   │   ├── GetSyncHistoryQuery    │
│  │   ├── Handlers/                  │
│  ├── Infrastructure/                │
│  │   ├── RemoteDatabaseConnector    │
│  │   ├── SshTunnelClient            │
│  │   ├── SchemaDiffEngine           │
│  │   ├── DataDiffEngine             │
│  └── (Routing in Api/Routing/)      │
│       └── DatabaseSyncEndpoints     │
└──────────┬──────────────────────────┘
           │ HTTP (internal Docker network)
┌──────────▼──────────────────────────┐
│     SSH Tunnel Sidecar              │
│  (Alpine + openssh-client)          │
│  API: POST /open, DELETE /close,    │
│       GET /status, GET /health      │
│  Forwards: 0.0.0.0:15432 →         │
│    staging:5432                     │
└─────────────────────────────────────┘
```

### Design Decisions

- **Plugin-like bounded context**: Lives in the monolite but gated behind `Features.DatabaseSync` feature flag. Disabled by default.
- **Sidecar for SSH**: The API container never holds SSH keys. A separate lightweight container manages the tunnel, mountable read-only.
- **Docker profile**: Sidecar only starts with `--profile db-sync`, not part of normal dev workflow.
- **No second DbContext registered in DI**: The `RemoteDatabaseConnector` creates ad-hoc `NpgsqlConnection` instances to the forwarded port. No EF Core DbContext for remote — raw SQL only for reads, EF Core `IMigrator` for migration apply.

## 3. SSH Tunnel Sidecar

### Container Specification

- **Base image**: `alpine:3.19` (~7MB) + `openssh-client`
- **HTTP server**: Minimal Go binary or Python Flask (~100 lines)
- **Exposed port**: `2222` (internal Docker network only)
- **SSH key**: Mounted read-only from host at `/root/.ssh/key`

### API

| Endpoint | Method | Request | Response |
|----------|--------|---------|----------|
| `/status` | GET | — | `{status: "closed\|opening\|open\|error", uptime_seconds: int, message: string}` |
| `/open` | POST | `{host, port, user, localPort}` | `{status: "opening"}` |
| `/close` | DELETE | — | `{status: "closed"}` |
| `/health` | GET | — | `200 OK` or `503` |

### Configuration (environment variables)

| Variable | Default | Description |
|----------|---------|-------------|
| `ALLOWED_HOSTS` | `204.168.135.69` | Whitelist of allowed SSH target hosts |
| `MAX_TUNNEL_DURATION` | `3600` | Auto-close tunnel after N seconds |
| `SSH_KEY_PATH` | `/root/.ssh/key` | Path to mounted SSH private key |
| `SIDECAR_AUTH_TOKEN` | (required) | Bearer token for API authentication |

### Docker Compose Addition

```yaml
# compose.dev.yml (addition)
ssh-tunnel-sidecar:
  build: ./sidecar/ssh-tunnel
  volumes:
    - ${SSH_KEY_PATH:-~/.ssh/meepleai-staging}:/root/.ssh/key:ro
  environment:
    - ALLOWED_HOSTS=204.168.135.69
    - MAX_TUNNEL_DURATION=3600
  networks:
    - meepleai
  profiles:
    - db-sync
```

### Security

- SSH key mounted read-only
- Host whitelist enforced server-side
- Auto-close after configurable duration (default 1h)
- **Shared secret authentication**: `SIDECAR_AUTH_TOKEN` env var set on both API container and sidecar. All sidecar API calls must include `Authorization: Bearer <token>` header. Prevents lateral movement from compromised containers on the shared Docker network.
- Only reachable from Docker internal network
- Docker profile: does not start by default

## 4. Schema Comparison & Migration Apply

### Schema Diff

1. Query `__EFMigrationsHistory` on both databases
2. Produce three lists:
   - **Common**: migrations present on both
   - **Local only**: migrations applied locally but missing on staging
   - **Staging only**: migrations applied on staging but missing locally
3. Each entry shows: `MigrationId`, `ProductVersion`, application timestamp

### SQL Preview

- For **local-only migrations** (to apply on staging): use EF Core `IMigrator.GenerateScript(fromMigration, toMigration)` to produce exact SQL. This works because the migration classes exist in the local assembly.
- For **staging-only migrations** (to apply on local): **out of scope for v1**. EF Core `IMigrator` requires migration classes in the assembly; staging-only migrations don't exist locally. The UI shows these as "staging-only" for awareness but the "Apply" button is disabled. To resolve, the developer must pull the branch containing those migrations and run `dotnet ef database update` locally.
- SQL displayed in UI with syntax highlighting before apply
- SQL hash stored in audit log for traceability

### Migration Apply

- Admin previews SQL → types confirmation text → backend executes on target DB
- Direction chosen per operation: "Apply to staging" or "Apply to local"
- **Forward-only**: only additive migrations supported. Rollback not supported in v1.
- Executed via `NpgsqlCommand` on the target connection (not via EF Core migration runner, since remote DB has no DbContext)
- Full transaction: rollback on any error
- Audit logged with: direction, migration names, SQL hash, user, timestamp

### Confirmation UX

Confirmation text format: `APPLY {N} MIGRATIONS TO {TARGET}` (e.g., "APPLY 3 MIGRATIONS TO STAGING")

## 5. Data Comparison & Sync

### Table Listing

- `ListTablesQuery` reads `information_schema.tables` on both databases
- Returns: table name, schema, row count (local), row count (staging), estimated size
- Tables grouped by bounded context (derived from naming convention or hardcoded mapping)

### Data Diff Algorithm

1. Admin selects a table to compare
2. Backend identifies primary key column(s) from `information_schema.key_column_usage`
3. Fetches all PKs + row hashes from both databases. The hash query dynamically builds a safe column list excluding `vector`, `bytea`, `json`, `jsonb` columns (queried from `information_schema.columns`):
   ```sql
   -- Column list built dynamically, excluding unsafe types
   SELECT id, md5(ROW(col1, col2, col3)::text) as row_hash FROM {table} t
   -- NOT: ROW(t.*) — would fail on vector/bytea columns
   ```
   **Total row cap**: tables with >50,000 total rows on either side are rejected with a warning suggesting `pg_dump`. This is separate from the 10k diff-row limit.
4. Compares in memory to produce four categories:
   - **Identical**: same PK, same hash → count only, not displayed
   - **Modified**: same PK, different hash → fetch full rows, highlight differing columns
   - **Local only**: PK exists only in local
   - **Staging only**: PK exists only in staging

### Data Sync

- Direction chosen per table: `Local → Staging` or `Staging → Local`
- Operations generated:
  - `INSERT` for rows missing on target (using source row data)
  - `UPDATE` for rows with matching PK but different data
  - **No DELETE**: rows present only on target are left untouched (v1 safety)
- Executed in a **single transaction**: atomic rollback on any failure
- FK constraint pre-check: warn if table has foreign keys to tables not yet synced

### Confirmation UX

Confirmation text format: `SYNC {TABLE} TO {TARGET}` (e.g., "SYNC system_configurations TO STAGING")

### Limitations (v1)

| Limitation | Reason | Mitigation |
|------------|--------|------------|
| No `bytea`/blob columns in diff | Too heavy for in-memory comparison | Show "[binary data differs]" |
| Max 10,000 rows of difference | Memory and performance | Warning + suggest `pg_dump` |
| No DELETE on target | Safety — avoid accidental data loss | Document in UI |
| No tables with custom triggers | Unpredictable side effects | Pre-check and warning |

## 6. API Endpoints

All endpoints under `/api/v1/admin/db-sync/`, requiring `RequireSuperAdminSession()` and `Features.DatabaseSync` flag.

| Endpoint | Method | CQRS Handler | Description |
|----------|--------|-------------- |-------------|
| `/tunnel/status` | GET | `GetTunnelStatusQuery` | Current tunnel state |
| `/tunnel/open` | POST | `OpenTunnelCommand` | Open SSH tunnel via sidecar |
| `/tunnel/close` | DELETE | `CloseTunnelCommand` | Close SSH tunnel |
| `/schema/compare` | GET | `CompareSchemaQuery` | Compare migrations local vs staging |
| `/schema/preview-sql` | POST | `PreviewMigrationSqlCommand` | Generate SQL for missing migrations |
| `/schema/apply` | POST | `ApplyMigrationsCommand` | Apply migrations to target |
| `/tables` | GET | `ListTablesQuery` | List tables with row counts |
| `/tables/{name}/compare` | GET | `CompareTableDataQuery` | Diff data for selected table |
| `/tables/{name}/sync` | POST | `SyncTableDataCommand` | Sync table data to target |
| `/operations/history` | GET | `GetSyncOperationsHistoryQuery` | Audit trail of past operations |

### Request/Response Examples

**POST /tunnel/open**
```json
// Request: empty (config from environment)
// Response:
{ "status": "opening", "message": "Establishing SSH tunnel to staging" }
```

**GET /schema/compare**
```json
{
  "common": [
    { "migrationId": "20260316055120_Beta0", "productVersion": "9.0.0", "appliedOn": "2026-03-16T06:00:00Z" }
  ],
  "localOnly": [
    { "migrationId": "20260318130032_AddAgentMemoryUpdatedAt", "productVersion": "9.0.0" }
  ],
  "stagingOnly": []
}
```

**POST /schema/apply**
```json
// Request:
{ "direction": "LocalToStaging", "confirmation": "APPLY 3 MIGRATIONS TO STAGING" }
// Response:
{ "success": true, "migrationsApplied": 3, "operationId": "guid" }
```

**GET /tables/{name}/compare**
```json
{
  "tableName": "system_configurations",
  "localRowCount": 42,
  "stagingRowCount": 38,
  "identical": 35,
  "modified": [
    {
      "primaryKey": { "id": "guid" },
      "differences": [
        { "column": "Value", "local": "true", "staging": "false" },
        { "column": "UpdatedAt", "local": "2026-03-18", "staging": "2026-03-16" }
      ]
    }
  ],
  "localOnly": [ { "primaryKey": { "id": "guid" }, "preview": { "Key": "Features.GameNightV2" } } ],
  "stagingOnly": []
}
```

**POST /tables/{name}/sync**
```json
// Request:
{ "direction": "LocalToStaging", "confirmation": "SYNC system_configurations TO STAGING" }
// Response:
{ "success": true, "inserted": 4, "updated": 3, "operationId": "guid" }
```

## 7. CLI Tool

### Structure

```
infra/tools/db-sync/
├── db-sync.sh              # Entry point
├── lib/
│   ├── common.sh           # Auth, API base URL, error handling
│   ├── tunnel.sh           # connect, disconnect, status
│   ├── schema.sh           # compare, preview, apply
│   ├── data.sh             # tables, diff, sync
│   └── history.sh          # operation history
├── .env.example            # API_URL, ADMIN_TOKEN config
└── README.md               # Usage documentation
```

### Commands

```bash
# Configuration
cd infra/tools/db-sync && cp .env.example .env  # set API_URL, ADMIN_TOKEN

# Tunnel management
./db-sync.sh tunnel status
./db-sync.sh tunnel connect
./db-sync.sh tunnel disconnect

# Schema operations
./db-sync.sh schema compare
./db-sync.sh schema preview
./db-sync.sh schema apply --confirm

# Data operations
./db-sync.sh data tables
./db-sync.sh data diff <table_name>
./db-sync.sh data sync <table_name> --direction local-to-staging --confirm

# History
./db-sync.sh history [--limit 20]
```

### Pattern

`db-sync.sh <domain> <action> [args]` — consistent, predictable, each domain in its own file.

### Authentication

Requires a valid SuperAdmin JWT token, passed via:
- `--token <jwt>` flag, or
- `MEEPLEAI_ADMIN_TOKEN` environment variable sourced from `infra/secrets/dev/db-sync-cli.secret`

The `.env` file references the secret path but does not contain the token directly. The `infra/tools/db-sync/.env` file must be added to `.gitignore`. This aligns with the project's existing secret management pattern (`infra/secrets/{env}/*.secret`).

## 8. Frontend Admin UI

### Page Location

`/admin/database-sync` — new page in existing admin panel.

### Layout

Three-tab interface with persistent tunnel status banner:

```
┌──────────────────────────────────────────────────────┐
│  🔄 Database Sync                    [🟢 Connected]  │
├──────────┬──────────┬──────────┬─────────────────────┤
│  Schema  │  Data    │  History │                     │
└──────────┴──────────┴──────────┴─────────────────────┘
```

### Tab: Schema

- Table with columns: Migration name, Local (✅/❌), Staging (✅/❌)
- "Preview SQL" button → modal with syntax-highlighted SQL
- "Apply" button → confirmation dialog with text input
- Real-time operation status (spinner + progress)

### Tab: Data

- Left sidebar: table list grouped by bounded context, badge with row counts (L/S)
- Right panel: select table → diff summary (identical, modified, local only, staging only)
- Expand modified row → side-by-side field diff with highlighted differences
- Direction dropdown + sync button with confirmation dialog

### Tab: History

- Chronological list of past sync operations
- Each entry: type, table, direction, rows affected, user, timestamp, outcome

### Components

| Component | Purpose |
|-----------|---------|
| `TunnelStatusBanner` | Top bar with connection state, connect/disconnect button |
| `ConfirmationDialog` | Text input confirmation for dangerous operations (reuses existing admin pattern) |
| `SqlPreviewModal` | Code block with SQL syntax highlighting |
| `DiffTable` | Table with highlighted differing cells |
| `TableSelector` | Sidebar with grouped tables and row count badges |

### State Management

React Query for all API calls (caching, refetch, loading states). No Zustand needed — state is server-side.

## 9. Security & Error Handling

### Authorization

- All endpoints require `RequireSuperAdminSession()` (not `RequireAdminSession()` — the latter also admits Admin role)
- Route group uses `.RequireAuthorization("RequireSuperAdmin")` matching existing `AdminOperationsEndpoints.cs` pattern
- Feature flag `Features.DatabaseSync` must be enabled (disabled by default)
- Every write operation logged in `AuditLogEntity`

### Operational Protections

| Risk | Mitigation |
|------|------------|
| Accidental sync | Explicit text confirmation for every write operation |
| Forgotten open tunnel | Auto-close after 1h, persistent UI banner |
| FK violations during sync | Pre-check dependencies, warning with related table list |
| Large table sync | 10k row difference limit, warning with pg_dump suggestion |
| Data corruption | Single transaction per sync: full rollback on error |
| SQL injection via table name | Whitelist tables from `information_schema`, no raw user input in SQL |
| Staging DB unreachable | 10s connection timeout, retry with backoff, clear error message |
| Concurrent sync operations | PostgreSQL advisory lock: one sync operation at a time |

### Sidecar Error Handling

| Error | Sidecar Response |
|-------|-----------------|
| SSH connection refused | `{status: "error", message: "SSH connection refused"}` |
| Key authentication failed | `{status: "error", message: "SSH key rejected"}` |
| Tunnel process crash | Healthcheck detects, API gets `closed` on next status check |

### Backend Error Handling

| Error | HTTP Response |
|-------|-------------- |
| Sidecar not running | `503` — "Sidecar not running. Start with --profile db-sync" |
| DB unreachable (tunnel open) | `502` — "Remote database unreachable" |
| Migration apply failed | `500` — rollback + SQL error message |
| Data sync failed | `500` — rollback + error detail |

### Audit Trail

Uses the existing `AuditLogEntity` fields (`Action`, `Resource`, `ResourceId`, `Result`, `Details`). Structured metadata is JSON-serialized into the `Details` string field:

```csharp
// Example audit log creation
new AuditLogEntity
{
    Action = "DatabaseSync.ApplyMigrations",
    Resource = "Schema",
    ResourceId = operationId.ToString(),
    Result = "Success",
    Details = JsonSerializer.Serialize(new
    {
        direction = "LocalToStaging",
        migrations = new[] { "20260318130032_AddAgentMemoryUpdatedAt", "..." },
        sqlHash = "abc123"
    }),
    UserId = adminId,
    IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
    UserAgent = httpContext.Request.Headers.UserAgent.ToString()
}
```

## 10. File Structure (New Files)

### Backend

```
apps/api/src/Api/BoundedContexts/DatabaseSync/
├── Domain/
│   ├── Enums/
│   │   └── SyncDirection.cs              # LocalToStaging, StagingToLocal (PascalCase, matches .NET JSON serialization)
│   ├── Models/
│   │   ├── SchemaDiffResult.cs           # Common, LocalOnly, StagingOnly lists
│   │   ├── DataDiffResult.cs             # Identical, Modified, SourceOnly, TargetOnly
│   │   ├── RowDiff.cs                    # PK + list of column differences
│   │   ├── ColumnDiff.cs                 # Column, LocalValue, StagingValue
│   │   ├── TableInfo.cs                  # Name, Schema, LocalCount, StagingCount
│   │   ├── MigrationInfo.cs              # MigrationId, ProductVersion, AppliedOn
│   │   └── TunnelStatus.cs               # Status enum, Uptime, Message
│   └── Interfaces/
│       ├── IRemoteDatabaseConnector.cs   # Abstraction for remote DB access
│       └── ISshTunnelClient.cs           # Abstraction for sidecar communication
├── Application/
│   ├── Commands/
│   │   ├── OpenTunnelCommand.cs
│   │   ├── CloseTunnelCommand.cs
│   │   ├── PreviewMigrationSqlCommand.cs
│   │   ├── ApplyMigrationsCommand.cs
│   │   └── SyncTableDataCommand.cs
│   ├── Queries/
│   │   ├── GetTunnelStatusQuery.cs
│   │   ├── CompareSchemaQuery.cs
│   │   ├── CompareTableDataQuery.cs
│   │   ├── ListTablesQuery.cs
│   │   └── GetSyncOperationsHistoryQuery.cs
│   └── Handlers/
│       ├── OpenTunnelHandler.cs
│       ├── CloseTunnelHandler.cs
│       ├── PreviewMigrationSqlHandler.cs
│       ├── ApplyMigrationsHandler.cs
│       ├── SyncTableDataHandler.cs
│       ├── GetTunnelStatusHandler.cs
│       ├── CompareSchemaHandler.cs
│       ├── CompareTableDataHandler.cs
│       ├── ListTablesHandler.cs
│       └── GetSyncOperationsHistoryHandler.cs
├── Infrastructure/
│   ├── RemoteDatabaseConnector.cs        # NpgsqlConnection to forwarded port
│   ├── SshTunnelClient.cs               # HTTP client to sidecar API
│   ├── SchemaDiffEngine.cs              # __EFMigrationsHistory comparison
│   ├── DataDiffEngine.cs                # Row hash comparison + full row fetch
│   └── DatabaseSyncServiceExtensions.cs  # DI registration + JsonStringEnumConverter config

# Routing (in centralized Api/Routing/ directory, matching project convention)
apps/api/src/Api/Routing/
└── DatabaseSyncEndpoints.cs              # All /api/v1/admin/db-sync/* endpoints
```

### Sidecar

```
infra/sidecar/ssh-tunnel/
├── Dockerfile
├── server.py                # Flask/aiohttp minimal HTTP server
├── tunnel_manager.py        # SSH process management
└── requirements.txt         # flask (or no deps if Go)
```

### CLI

```
infra/tools/db-sync/
├── db-sync.sh
├── lib/
│   ├── common.sh
│   ├── tunnel.sh
│   ├── schema.sh
│   ├── data.sh
│   └── history.sh
├── .env.example
└── README.md
```

### Frontend

```
apps/web/src/app/admin/database-sync/
├── page.tsx                          # Main page with tabs
├── components/
│   ├── TunnelStatusBanner.tsx
│   ├── SchemaComparisonTab.tsx
│   ├── DataComparisonTab.tsx
│   ├── HistoryTab.tsx
│   ├── ConfirmationDialog.tsx
│   ├── SqlPreviewModal.tsx
│   ├── DiffTable.tsx
│   └── TableSelector.tsx
├── hooks/
│   ├── useTunnelStatus.ts           # React Query hook
│   ├── useSchemaCompare.ts
│   ├── useTableCompare.ts
│   └── useSyncOperations.ts
└── types/
    └── db-sync.ts                    # TypeScript types matching API responses
```

## 11. Feature Flag

Seed migration adds to `system_configurations`:

```sql
INSERT INTO system_configurations (id, key, value, description, is_active, created_at, updated_at)
VALUES (gen_random_uuid(), 'Features.DatabaseSync', 'false', 'Enable Database Sync admin tool', true, now(), now());
```

Enabled only by SuperAdmin via existing SystemConfiguration endpoints.

## 12. Future Enhancements (out of scope v1)

- Production environment support (with additional safeguards)
- DELETE sync mode (opt-in, with extra confirmation)
- Migration rollback support
- Scheduled/automated sync
- Conflict resolution for bidirectional modified rows
- Support for multiple remote environments (not just staging)
- Blob/binary column diff with download comparison
- Real-time progress via SSE for large table syncs
