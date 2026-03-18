# Integration Hardening + e5-base Switch

**Date**: 2026-03-18
**Status**: Approved
**Branch**: `frontend-dev`

## Scope

Three operational tasks to unblock the integration profile and optimize the embedding pipeline:

1. **Traefik basic auth on staging** — enable `integration-auth` middleware
2. **Schema alignment** — apply 5 pending EF Core migrations to staging DB
3. **e5-base switch** — change embedding model from e5-large (1024d) to e5-base (768d)

## Context

- Staging DB is empty (0 PDFs, 0 embeddings, `pgvector_embeddings` table doesn't exist yet)
- Only `BetaV0InitialCreate` migration applied (5 pending)
- `INTEGRATION_BASIC_AUTH` not set on staging Traefik
- pgvector 0.8.2 installed, extension ready
- Benchmark data: e5-base is 3.7x faster on ARM64 (0.32s vs 1.20s per chunk)
- `deploy-staging.sh` sources all `secrets/*.secret` files before `docker compose up` (`set -a && for f in secrets/*.secret; do source "$f"; done && set +a`)

## Design

### 3A: Traefik Basic Auth

**What**: Generate htpasswd hash, add `INTEGRATION_BASIC_AUTH` to `traefik.secret` on staging server, redeploy Traefik.

**How it works**: `deploy-staging.sh` (lines 363-379) sources all `.secret` files into shell env before running `docker compose up`. The label `${INTEGRATION_BASIC_AUTH}` in `compose.staging.yml` is interpolated at compose-time from this shell environment.

**Hash format**: Use `htpasswd -nbB` (bcrypt `$2y$`) if available, or `openssl passwd -apr1` as fallback. Both are supported by Traefik v3. Escape `$` as `$$` for Docker Compose interpolation.

**Steps**:
1. SSH to staging server
2. Generate hash: `htpasswd -nbB admin <password>` (or openssl fallback)
3. Escape `$` → `$$` in the hash
4. Append `INTEGRATION_BASIC_AUTH=admin:$$2y$$...` to `/opt/meepleai/secrets/traefik.secret`
5. Redeploy: `cd /opt/meepleai/infra && set -a && for f in secrets/*.secret; do source "$f"; done && set +a && docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml up -d traefik`

**Verification**:
- `curl -u admin:<pass> https://meepleai.app/services/embedding/health` → 200
- `curl https://meepleai.app/services/embedding/health` (no auth) → 401

### 3B: Schema Alignment

**What**: Apply 5 pending migrations to staging PostgreSQL.

**Migrations** (in order):
1. `20260316105334_AddServiceHealthStates`
2. `20260316115449_WidenAlertTypeColumn`
3. `20260317072335_AddSharedGamesFtsIndex`
4. `20260317160741_AddInvitationFlowTables`
5. `20260317185832_AddSelectedKnowledgeBaseIdsToChatThread`

**Method**: SSH tunnel (PG on 15432) + `dotnet ef database update` from local machine via PowerShell.

**Command**:
```powershell
# Terminal 1: SSH tunnel
ssh -i ~/.ssh/meepleai-staging -L 15432:localhost:5432 deploy@204.168.135.69

# Terminal 2: Apply migrations (PowerShell)
cd apps/api/src/Api
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=15432;Database=meepleai;Username=meepleai;Password=MeepleAI-Integration-2026"
dotnet ef database update
```

**Known gotcha**: Staging PG password is `MeepleAI-Integration-2026` (no special chars — `=` and `!` cause SCRAM auth issues). See `project_integration_profile.md`.

**Risk**: Zero — DB is empty, no data to lose. Only additive schema changes.

**Verification**: Query `"__EFMigrationsHistory"` shows 6 rows.

### 4: e5-base Switch

**What**: Change default embedding model and dimensions across all environments.

#### Code changes

**`PgVectorStoreAdapter.cs`** — update default parameter for consistency:
```csharp
// EnsureCollectionExistsAsync signature
int vectorDimension = 768  // was 1024
```
Note: Both callers (VectorReembeddingService, PdfProcessingPipelineService) pass dimensions explicitly, so this default is never used at runtime. Updated for consistency only.

**`EmbeddingProviderType.cs`** — update `External` dimensions:
```csharp
EmbeddingProviderType.External => 768,  // was 1024
```

**`EmbeddingProviderType.cs`** (same file, `GetModelName` method) — update model name for External:
```csharp
EmbeddingProviderType.External => "intfloat/multilingual-e5-base",  // was e5-large
```

**`VectorDocumentEntity.cs`** — already defaults to `EmbeddingDimensions = 768`, no change needed.

#### Compose file changes

**`compose.dev.yml`** (change existing values):
```yaml
EMBEDDING_MODEL: intfloat/multilingual-e5-base      # was e5-large
EMBEDDING_DIMENSIONS: 768                             # was 1024
Embedding__Dimensions: 768                            # was 1024
```

**`compose.integration.yml`** (change existing overrides):
```yaml
EMBEDDING_MODEL: intfloat/multilingual-e5-base       # was e5-large
EMBEDDING_DIMENSIONS: 768                             # was 1024
Embedding__Dimensions: 768                            # was 1024
```

**`compose.staging.yml`** (add — currently absent):
```yaml
EMBEDDING_MODEL: intfloat/multilingual-e5-base
Embedding__Dimensions: 768
```

**`compose.prod.yml`** (add — currently absent):
```yaml
EMBEDDING_MODEL: intfloat/multilingual-e5-base
Embedding__Dimensions: 768
```

**`docker-compose.yml`** (base default):
```yaml
EMBEDDING_MODEL: ${EMBEDDING_MODEL:-intfloat/multilingual-e5-base}
```

#### What does NOT change
- Embedding service Python code — already supports e5-base in `ALLOWED_MODELS`
- pgvector_embeddings table — doesn't exist yet, will be created at runtime with `vector(768)` by `PgVectorStoreAdapter.EnsureCollectionExistsAsync()` (dimension passed explicitly by callers)
- No EF migration needed — the table is created dynamically

**Staging deployment**: Redeploy embedding-service + API after compose file update.

**Verification**:
- `curl -u admin:<pass> https://meepleai.app/services/embedding/health` shows model = e5-base
- Upload a test PDF, check embeddings have 768 dimensions

## Execution Order

1. **3A** Traefik basic auth — create secret on staging server, redeploy traefik (~5 min)
2. **3B** Schema migrations — SSH tunnel + `dotnet ef database update` (~5 min)
3. **4** Code changes — update C# files + all compose files (local commit)
4. Deploy updated compose to staging (push + `deploy-staging.sh`)
5. Verify all three (success criteria below)

## Success Criteria

- [ ] `curl -u admin:<pass> https://meepleai.app/services/embedding/health` → 200
- [ ] `curl https://meepleai.app/services/embedding/health` (no auth) → 401
- [ ] `"__EFMigrationsHistory"` has 6 rows
- [ ] Staging tables include service_health_states, invitation links
- [ ] Embedding service loads `multilingual-e5-base` model
- [ ] `EmbeddingProviderType.External.GetDimensions()` returns 768
