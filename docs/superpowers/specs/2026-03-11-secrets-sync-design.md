# Secrets Sync & Service Restart Tool — Design Spec

**Date**: 2026-03-11
**Issue**: Operational tooling — no GitHub issue
**Branch**: feature/secrets-sync-tool (from main-dev)

## Problem

Secret files on the staging server (meepleai.app) were generated with `setup-secrets.ps1 -SaveGenerated` and some values were never saved locally. There is no mechanism to:
1. Pull current server secrets to local for backup/editing
2. Push updated secrets to the server
3. Know which services need restarting after a secret change
4. Distinguish between environment-shared and environment-specific secrets

## Solution

A PowerShell script (`sync-secrets.ps1`) with a YAML manifest that manages bidirectional secret sync between local and staging, with service restart orchestration.

## Architecture

### File Structure (flat + staging mirror)

```
infra/secrets/
├── *.secret              # Local dev secrets (unchanged, current structure)
├── *.secret.example      # Templates (unchanged)
├── staging/              # Mirror of server secrets (populated by -Pull)
│   ├── database.secret
│   ├── redis.secret
│   ├── qdrant.secret
│   ├── jwt.secret
│   ├── admin.secret
│   ├── oauth.secret
│   ├── n8n.secret
│   ├── monitoring.secret
│   ├── openrouter.secret
│   ├── bgg.secret
│   ├── embedding-service.secret
│   ├── reranker-service.secret
│   ├── smoldocling-service.secret
│   ├── unstructured-service.secret
│   └── ...
├── secrets-sync.yml      # Manifest (NEW)
├── setup-secrets.ps1     # Existing (no changes needed)
├── generate-env-from-secrets.ps1  # Existing (no changes needed)
└── README.md             # Existing (update with sync docs)
```

No changes to `docker-compose.yml` or existing scripts. The `staging/` folder is a local mirror only — the server continues reading from `/opt/meepleai/repo/infra/secrets/*.secret` (flat).

### Manifest: `secrets-sync.yml`

```yaml
# Secret sync policy for staging server
# Policy types:
#   sync            - Same value local and staging, source: infra/secrets/<name>
#   staging-only    - Staging-specific value, source: infra/secrets/staging/<name>
#   skip            - Not synced (manual management only)

secrets:
  # Common — same value everywhere
  openrouter.secret: sync
  bgg.secret: sync
  embedding-service.secret: sync
  reranker-service.secret: sync
  smoldocling-service.secret: sync
  unstructured-service.secret: sync

  # Staging-specific — different values than local dev
  database.secret: staging-only
  redis.secret: staging-only
  qdrant.secret: staging-only
  jwt.secret: staging-only
  admin.secret: staging-only
  oauth.secret: staging-only
  n8n.secret: staging-only
  monitoring.secret: staging-only
  email.secret: staging-only

  # Not synced
  storage.secret: skip
  traefik.secret: skip
  gmail-app-password.secret: skip

# Secret-to-service dependency map
# Used for selective restart after sync
service_map:
  database.secret: [postgres, api, n8n]
  redis.secret: [redis, api]
  qdrant.secret: [qdrant, api]
  jwt.secret: [api]
  admin.secret: [api]
  openrouter.secret: [api]
  oauth.secret: [api]
  bgg.secret: [api]
  email.secret: [api, alertmanager]
  embedding-service.secret: [embedding-service]
  reranker-service.secret: [reranker-service]
  smoldocling-service.secret: [smoldocling-service]
  unstructured-service.secret: [unstructured-service]
  monitoring.secret: [grafana, alertmanager]
  n8n.secret: [n8n]
  storage.secret: [minio, minio-setup, api]
  traefik.secret: [traefik]

# Stateful services — password change warning
stateful_services: [postgres, redis, qdrant]

# Server connection
server:
  host: 204.168.135.69
  user: deploy
  key_path: "D:\\Repositories\\SSH Keys\\meepleai-staging"
  remote_secrets_path: /opt/meepleai/repo/infra/secrets
  compose_dir: /opt/meepleai/repo/infra
  compose_cmd: >-
    docker compose
    -f docker-compose.yml
    -f compose.staging.yml
    -f compose.staging-traefik.yml
```

### Script: `sync-secrets.ps1`

**Location**: `infra/scripts/sync-secrets.ps1`

**Modes**:

```
# Pull server secrets to local staging/ mirror
sync-secrets.ps1 -Pull [-Only <name>] [-DryRun]

# Push local secrets to server (uses manifest policy)
sync-secrets.ps1 [-Push] [-Only <name>] [-DryRun] [-Force] [-SkipRestart]

# Show status (diff without action)
sync-secrets.ps1 -Status
```

### Pull Flow

```
1. SSH to server, tar all *.secret files
2. Download tar to temp, extract
3. For each secret:
   a. If staging/<name> exists locally → show diff
   b. If new → mark as NEW
4. Show summary table:
   ┌─────────────────────────────┬──────────┐
   │ Secret                      │ Status   │
   ├─────────────────────────────┼──────────┤
   │ database.secret             │ NEW      │
   │ redis.secret                │ UPDATED  │
   │ openrouter.secret           │ OK       │
   └─────────────────────────────┴──────────┘
5. "Save to staging/? [y/N]"
6. Copy to infra/secrets/staging/
7. Clean temp files
```

### Push Flow

```
1. Read manifest, determine source for each secret:
   - sync → infra/secrets/<name>
   - staging-only → infra/secrets/staging/<name>
   - skip → ignore
2. SCP download current server files to temp (for diff + backup)
3. For each secret with a local source:
   a. Diff local source vs server current
   b. If different → mark CHANGED
   c. If same → mark OK
4. Show summary table:
   ┌─────────────────────────────┬──────────┬───────────────────┐
   │ Secret                      │ Status   │ Services          │
   ├─────────────────────────────┼──────────┼───────────────────┤
   │ openrouter.secret           │ CHANGED  │ api               │
   │ jwt.secret                  │ CHANGED  │ api               │
   │ database.secret             │ OK       │ -                 │
   └─────────────────────────────┴──────────┴───────────────────┘
5. If any CHANGED secret impacts stateful services:
   ⚠️  WARNING: database.secret changed → postgres is stateful!
   Changing passwords on running stateful services may cause data
   inaccessibility. The volume retains the old password.
   Proceed only if you know what you're doing.
6. SSH: backup changed files on server to secrets/backup/<name>.<YYYYMMDD>
7. SCP upload changed files to server
8. Deduplicate impacted services list
9. "Restart services: api, n8n? [y/N]"
10. SSH: cd compose_dir && source env vars && docker compose up -d --force-recreate <services>
11. SSH: docker compose ps <services> (verify healthy)
12. Clean temp files
```

### Status Flow

```
1. Same as Push steps 1-4, but no action taken
2. Shows what would happen on a push
```

### Safeguards

| Safeguard | Description |
|---|---|
| **Stateful warning** | Red warning when `database`, `redis`, or `qdrant` secrets change |
| **Server backup** | Auto-backup on server before overwrite: `backup/<name>.<YYYYMMDD>` |
| **DryRun mode** | `--DryRun` shows all actions without executing |
| **Diff confirmation** | Always shows diff and asks before upload/download |
| **--Only filter** | Process a single secret: `--Only openrouter` |
| **--SkipRestart** | Upload secrets but don't restart (for batch updates) |
| **Missing source check** | Errors if manifest says `sync` but local file doesn't exist |
| **SSH connectivity check** | Verifies SSH connection before starting operations |

### Implementation Notes

- Uses `ssh` and `scp` commands (available in Windows 10+ and Git Bash)
- Reads YAML manifest with simple regex parsing (no external YAML module dependency)
- Diff comparison uses file content hash (SHA256), not timestamps
- Temp files go to `$env:TEMP/meepleai-secrets-sync/` and are cleaned on exit
- The `--force-recreate` flag on `docker compose up` is required because `restart` does NOT reload `env_file` values
- Server env vars must be re-sourced before compose command:
  ```bash
  cd /opt/meepleai/repo/infra && set -a; for f in secrets/*.secret; do source "$f" 2>/dev/null; done; set +a
  ```

### .gitignore Updates

```gitignore
# Already gitignored: *.secret (except .example)
# staging/ secrets are also *.secret, so already covered
# Backup dir on server only, not in repo
```

No `.gitignore` changes needed — existing `*.secret` pattern covers `staging/*.secret`.

## Files to Create

| File | Action |
|---|---|
| `infra/secrets/secrets-sync.yml` | CREATE — manifest |
| `infra/scripts/sync-secrets.ps1` | CREATE — sync script |
| `infra/secrets/staging/.gitkeep` | EXISTS — no change |

## Files NOT Modified

- `docker-compose.yml` — no path changes
- `compose.staging.yml` — no path changes
- `setup-secrets.ps1` — independent tool
- Existing `*.secret` files — untouched

## Usage Workflow

```
# First time: pull secrets from server to establish baseline
pwsh infra/scripts/sync-secrets.ps1 -Pull

# Edit a staging secret locally
notepad infra/secrets/staging/jwt.secret

# Edit a common secret locally
notepad infra/secrets/openrouter.secret

# Check what changed
pwsh infra/scripts/sync-secrets.ps1 -Status

# Push changes and restart impacted services
pwsh infra/scripts/sync-secrets.ps1

# Push only one secret
pwsh infra/scripts/sync-secrets.ps1 -Only openrouter

# Dry run (see what would happen)
pwsh infra/scripts/sync-secrets.ps1 -DryRun
```
