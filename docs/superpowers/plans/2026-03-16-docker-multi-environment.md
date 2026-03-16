# Docker Multi-Environment Configuration — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure Docker Compose from 14+ fragmented files into a clean 4-environment system (dev, integration, staging, prod) with Makefile convenience commands, per-environment secrets, and SSH tunnel support for integration.

**Architecture:** Single base `docker-compose.yml` (no ports, no env_file, no environment) + 4 override files. Makefile wraps complex `-f` combos. Secrets organized in per-environment subfolders. HyperDX removed entirely.

**Tech Stack:** Docker Compose v2.20+, Traefik v3.6, PowerShell 7+, GNU Make, SSH

**Spec:** `docs/superpowers/specs/2026-03-16-docker-multi-environment-design.md`

---

## Chunk 1: Cleanup — Remove Legacy Files and HyperDX

### Task 1: Delete legacy/archived compose files

**Files:**
- Delete: `infra/compose.logging.yml`
- Delete: `infra/compose.meepleai.yml`
- Delete: `infra/compose.mvp.yml`
- Delete: `infra/compose.release.yml`
- Delete: `infra/compose.test.yml`
- Delete: `infra/compose.staging-traefik.yml`
- Delete: `infra/compose.hyperdx.yml`
- Delete: `infra/archived/compose.test-legacy.yml`
- Delete: `infra/archived/compose.traefik-examples.yml`
- Delete: `infra/archived/README.md`
- Delete: `infra/archived/` (directory)

- [ ] **Step 1: Delete legacy compose files**

```bash
cd infra
git rm compose.logging.yml compose.meepleai.yml compose.mvp.yml compose.release.yml compose.test.yml compose.staging-traefik.yml compose.hyperdx.yml
git rm -r archived/
```

- [ ] **Step 2: Delete HyperDX scripts**

```bash
git rm infra/scripts/verify-hyperdx.sh
git rm tools/verify-hyperdx-ingestion.sh
git rm tools/verify-hyperdx-migration.sh
```

- [ ] **Step 3: Commit**

```bash
git add -A infra/compose.*.yml infra/archived/ infra/scripts/verify-hyperdx.sh tools/verify-hyperdx-*.sh
git commit -m "chore(infra): remove legacy compose files and HyperDX scripts"
```

---

### Task 2: Remove HyperDX from frontend

**Files:**
- Delete: `apps/web/src/lib/hyperdx.ts`
- Delete: `apps/web/src/lib/hyperdx-stub.ts`
- Delete: `apps/web/src/components/HyperDXProvider.tsx`
- Modify: `apps/web/src/app/layout.tsx` — remove HyperDXProvider import and usage
- Modify: `apps/web/package.json` — remove `@hyperdx/browser` dependency
- Modify: `apps/web/next.config.js` — remove HyperDX-related config
- Modify: `apps/web/proxy.ts` — remove HyperDX proxy routes
- Modify: `apps/web/src/lib/api/core/logger.ts` — remove HyperDX references
- Modify: `apps/web/src/components/auth/AuthProvider.tsx` — remove HyperDX tracking calls
- Modify: `apps/web/src/lib/analytics/track-event.ts` — remove HyperDX tracking
- Modify: `apps/web/src/lib/logger.ts` — remove HyperDX references
- Modify: `apps/web/src/lib/i18n/infrastructure.ts` — remove HyperDX references

- [ ] **Step 1: Delete HyperDX-specific frontend files**

```bash
git rm apps/web/src/lib/hyperdx.ts apps/web/src/lib/hyperdx-stub.ts apps/web/src/components/HyperDXProvider.tsx
```

- [ ] **Step 2: Remove HyperDXProvider from layout.tsx**

Open `apps/web/src/app/layout.tsx`, remove the `HyperDXProvider` import and its wrapping `<HyperDXProvider>` component from the JSX tree. Keep the rest of the layout intact.

- [ ] **Step 3: Remove @hyperdx/browser from package.json**

```bash
cd apps/web && pnpm remove @hyperdx/browser
```

- [ ] **Step 4: Clean HyperDX references from remaining frontend files**

For each file listed above (next.config.js, proxy.ts, logger.ts, AuthProvider.tsx, track-event.ts, infrastructure.ts):
- Search for `hyperdx` or `HyperDX` imports and calls
- Remove import lines and function calls
- Replace with no-op or remove entirely if the function was HyperDX-only
- Keep any non-HyperDX logging/analytics intact

- [ ] **Step 5: Verify frontend builds**

```bash
cd apps/web && pnpm typecheck && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/
git commit -m "chore(web): remove HyperDX provider, dependencies, and tracking"
```

---

### Task 3: Remove HyperDX from backend (.NET)

**Files:**
- Delete: `apps/api/src/Api/Infrastructure/Health/Checks/HyperDxHealthCheck.cs`
- Delete: `apps/api/src/Api/Routing/TelemetryTestEndpoints.cs`
- Delete: `apps/api/src/Api/Routing/TestTelemetryEndpoints.cs`
- Modify: `apps/api/src/Api/Api.csproj` — remove OpenTelemetry NuGet packages
- Modify: `apps/api/src/Api/Program.cs` — remove OTEL/HyperDX setup
- Modify: `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs` — remove HyperDX-specific OTLP exporter config
- Modify: `apps/api/src/Api/Infrastructure/Health/Extensions/HealthCheckServiceExtensions.cs` — remove HyperDX health check registration
- Modify: `apps/api/src/Api/Extensions/WebApplicationExtensions.cs` — remove OTEL references
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/InfrastructureHealthService.cs` — remove HyperDX from service list

Note: Keep `MeepleAiMetrics.cs`, `MeepleAiActivitySources.cs`, `QualityMetrics.cs`, `PromptManagementMetrics.cs` — these are Prometheus metrics (not HyperDX-specific). Only remove the OTLP exporter that sends to HyperDX. Keep `SensitiveDataProcessor.cs` if it's used by non-OTEL logging.

- [ ] **Step 1: Delete HyperDX-specific backend files**

```bash
git rm apps/api/src/Api/Infrastructure/Health/Checks/HyperDxHealthCheck.cs
git rm apps/api/src/Api/Routing/TelemetryTestEndpoints.cs
git rm apps/api/src/Api/Routing/TestTelemetryEndpoints.cs
```

- [ ] **Step 2: Remove OTLP exporter packages from Api.csproj**

Remove these NuGet packages (keep Prometheus exporter if present):
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- Any `HyperDX` packages

Keep:
- `OpenTelemetry.Exporter.Prometheus` (used by Prometheus/Grafana)
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.*` (keep if used by Prometheus metrics)

- [ ] **Step 3: Clean ObservabilityServiceExtensions.cs**

Remove the OTLP exporter configuration (`.AddOtlpExporter(...)` that points to HyperDX). Keep Prometheus exporter and metrics setup.

- [ ] **Step 4: Remove HyperDX health check registration**

In `HealthCheckServiceExtensions.cs`, remove the `.AddCheck<HyperDxHealthCheck>(...)` line.

- [ ] **Step 5: Clean InfrastructureHealthService.cs**

Remove HyperDX from the list of monitored services.

- [ ] **Step 6: Remove HyperDX-related test references**

Check these test files and remove HyperDX-specific assertions/mocks:
- `apps/api/tests/Api.Tests/Integration/Administration/HealthEndpointsIntegrationTests.cs`
- `apps/api/tests/Api.Tests/Integration/Administration/InfrastructureDetailsEndpointTests.cs`
- `apps/api/tests/Api.Tests/Observability/TwoFactorMetricsTests.cs`

- [ ] **Step 7: Verify backend builds and tests**

```bash
cd apps/api/src/Api && dotnet build
cd ../../tests/Api.Tests && dotnet test --filter "Category=Unit" --no-build
```

- [ ] **Step 8: Commit**

```bash
git add apps/api/
git commit -m "chore(api): remove HyperDX health check, OTLP exporter, and telemetry test endpoints"
```

---

### Task 4: Remove HyperDX from Python services

**Files:**
- Modify: `apps/embedding-service/main.py` — remove OTEL setup
- Modify: `apps/embedding-service/requirements.txt` — remove `opentelemetry-*` packages
- Modify: `apps/reranker-service/main.py` — remove OTEL setup
- Modify: `apps/reranker-service/requirements.txt` — remove `opentelemetry-*` packages
- Modify: `apps/smoldocling-service/src/main.py` — remove OTEL setup
- Modify: `apps/smoldocling-service/requirements.txt` — remove `opentelemetry-*` packages
- Modify: `apps/unstructured-service/src/main.py` — remove OTEL setup
- Modify: `apps/unstructured-service/requirements.txt` — remove `opentelemetry-*` packages

- [ ] **Step 1: Remove OTEL from each Python service**

For each service (`embedding-service`, `reranker-service`, `smoldocling-service`, `unstructured-service`):

1. In `main.py` (or `src/main.py`): Remove `opentelemetry` imports, `TracerProvider` setup, `instrument_*` calls. Keep standard Python logging.
2. In `requirements.txt`: Remove all `opentelemetry-*` lines.

- [ ] **Step 2: Commit**

```bash
git add apps/embedding-service/ apps/reranker-service/ apps/smoldocling-service/ apps/unstructured-service/
git commit -m "chore(ai-services): remove OpenTelemetry/HyperDX instrumentation from Python services"
```

---

### Task 5: Remove HyperDX from infra config and env

**Files:**
- Modify: `infra/docker-compose.yml` — remove `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME` env vars from all services
- Modify: `infra/prometheus.yml` — remove HyperDX scrape target if present
- Modify: `infra/grafana-datasources.yml` — remove HyperDX datasource if present
- Modify: `infra/traefik/traefik.yml` — remove HyperDX references
- Modify: `infra/env/api.env.release` — remove HyperDX env vars
- Modify: `infra/start-full.ps1` — remove HyperDX compose file reference
- Modify: `infra/start-full.sh` — remove HyperDX compose file reference
- Modify: `infra/start-observability.sh` — remove HyperDX compose file reference
- Modify: `infra/monitoring/README.md` — remove HyperDX section
- Delete: `infra/secrets/hyperdx.secret.example` (if exists)
- Modify: `tools/check-config.ps1` — remove HyperDX check
- Modify: `.gitignore` — remove HyperDX-related entries

- [ ] **Step 1: Remove OTEL env vars from docker-compose.yml**

In `infra/docker-compose.yml`, remove these environment variables from `embedding-service`, `unstructured-service`, `smoldocling-service`, `reranker-service`, `orchestration-service`:
```yaml
# Remove these lines:
- OTEL_EXPORTER_OTLP_ENDPOINT=http://hyperdx:14318
- OTEL_SERVICE_NAME=<service-name>
```

- [ ] **Step 2: Clean infra scripts and config**

Remove HyperDX references from `start-full.ps1`, `start-full.sh`, `start-observability.sh`, `prometheus.yml`, `grafana-datasources.yml`, `traefik/traefik.yml`, `env/api.env.release`, `tools/check-config.ps1`.

- [ ] **Step 3: Commit**

```bash
git add infra/ tools/ .gitignore
git commit -m "chore(infra): remove all HyperDX references from config, scripts, and env files"
```

---

### Task 6: Clean HyperDX from documentation

**Files:**
- Many `docs/` files reference HyperDX (ports, setup, architecture diagrams)
- Focus on files that give incorrect operational instructions

- [ ] **Step 1: Update key docs**

Priority docs to update (remove HyperDX sections/references):
- `docs/deployment/docker-quickstart.md`
- `docs/deployment/docker-services.md`
- `docs/deployment/health-checks.md`
- `docs/deployment/log-aggregation-guide.md`
- `docs/development/docker/service-endpoints.md`
- `docs/development/docker/docker-profiles.md`
- `docs/development/docker/advanced-features.md`
- `docs/development/docker/README.md`
- `docs/development/README.md`
- `docs/architecture/diagrams/infrastructure-overview.md`
- `docs/api/health-check-api.md`

For each: search "hyperdx" / "HyperDX" / "14317" / "14318" / "8180" and remove those sections/lines.

- [ ] **Step 2: Commit**

```bash
git add docs/
git commit -m "docs: remove HyperDX references from deployment and architecture docs"
```

---

### Task 7: Remove HyperDX from admin frontend components

**Files:**
- Modify: `apps/web/src/components/admin/overview/SystemHealthCard.tsx` — remove HyperDX service entry
- Modify: `apps/web/src/components/admin/ServiceStatusCard.tsx` — remove HyperDX
- Modify: `apps/web/src/components/admin/ServiceHealthMatrix.stories.tsx` — remove HyperDX from stories
- Modify: `apps/web/src/components/admin/ServiceCard.stories.tsx` — remove HyperDX from stories
- Modify: `apps/web/src/components/rag-dashboard/StrategyFlowVisualizer.tsx` — remove HyperDX node if present

- [ ] **Step 1: Remove HyperDX from admin components**

Search each file for "hyperdx" / "HyperDX" entries in service lists, health check arrays, story definitions. Remove those entries.

- [ ] **Step 2: Verify frontend builds**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/
git commit -m "chore(admin): remove HyperDX from service health and monitoring components"
```

---

## Chunk 2: Rewrite Base Compose File

### Task 8: Create new `docker-compose.yml` (base)

**Files:**
- Rewrite: `infra/docker-compose.yml`

The base file defines all services with:
- Image/build context
- Healthchecks
- `depends_on` with conditions
- Volumes (neutral names)
- Network (`meepleai`)
- Profiles (new set: `ai`, `monitoring`, `automation`, `proxy`, `storage`)
- Resource limits (deploy.resources)
- **NO** `ports`, `env_file`, `environment`, `labels`

- [ ] **Step 1: Back up current base file**

```bash
cp infra/docker-compose.yml infra/docker-compose.yml.bak
```

- [ ] **Step 2: Rewrite docker-compose.yml**

Reference the current file for service definitions. Create new file with these services:

**Core (no profile — always active):**
- `postgres` — `pgvector/pgvector:pg16`, healthcheck `pg_isready`, volume `postgres_data`
- `redis` — `redis:7.4.1-alpine3.20`, healthcheck `redis-cli ping`, volume via `redis.conf` mount
- `qdrant` — `qdrant/qdrant:v1.12.4`, healthcheck TCP check, volume `qdrant_data`

**Profile `ai`:**
- `ollama` — `ollama/ollama:0.5.4`, volume `ollama_data`
- `ollama-pull` — `curlimages/curl:8.12.1`, depends_on ollama healthy
- `embedding-service` — build `../apps/embedding-service`
- `unstructured-service` — build `../apps/unstructured-service`
- `smoldocling-service` — build `../apps/smoldocling-service`, volumes `smoldocling_temp`, `smoldocling_models`
- `reranker-service` — build `../apps/reranker-service`, volume `reranker_models`
- `orchestration-service` — build `../apps/orchestration-service`, depends_on postgres healthy + redis healthy

**Profile `monitoring`:**
- `prometheus` — `prom/prometheus:v3.7.0`, volume `prometheus_data`
- `grafana` — `grafana/grafana:11.4.0`, depends_on prometheus, volume `grafana_data`
- `alertmanager` — `prom/alertmanager:v0.27.0`, volume `alertmanager_data`
- `node-exporter` — `prom/node-exporter:v1.8.2`
- `cadvisor` — `gcr.io/cadvisor/cadvisor:v0.49.1`
- `docker-socket-proxy` — `tecnativa/docker-socket-proxy:0.6` (upgraded from 0.2)

**Profile `automation`:**
- `n8n` — `n8nio/n8n:1.114.4`, depends_on postgres healthy
- `mailpit` — `axllent/mailpit:v1.22`, volume `mailpit_data`

**Profile `storage`:**
- `minio` — `minio/minio:RELEASE.2024-11-07T00-52-20Z`, volume `minio_data`
- `minio-init` — `minio/mc:RELEASE.2024-11-17T19-35-25Z`, depends_on minio healthy

**Profile `proxy`:**
- `traefik` — `traefik:v3.6`

**App (no profile — always active):**
- `api` — build `../apps/api`, depends_on postgres+qdrant+redis, volume `pdf_uploads`
- `web` — build `../apps/web`, depends_on api

Key principles:
- `redis` command uses only `redis-server /etc/redis/redis.conf` — no `--requirepass` (password comes from override env)
- All `container_name` values preserved (e.g., `meepleai-postgres`)
- `restart: unless-stopped` on all services
- `shm_size: 1g` on postgres
- Keep `init/postgres-init.sql` volume mount on postgres
- Keep `redis/redis.conf` volume mount on redis
- Keep `scripts/load-secrets-env.sh` volume mount + entrypoint on services that need it (api, grafana, n8n, alertmanager)

Wait — the `load-secrets-env.sh` entrypoint and the `command` need to stay in base since they're not environment-specific. The only things that change per env are `env_file`, `ports`, `environment`, and `labels`.

- [ ] **Step 3: Verify YAML syntax**

```bash
cd infra && docker compose -f docker-compose.yml config --quiet
```

This will fail without an override (no env_file), but should parse YAML correctly.

- [ ] **Step 4: Commit**

```bash
git add infra/docker-compose.yml
git commit -m "refactor(infra): rewrite docker-compose.yml as env-neutral base (no ports, no env_file)"
```

---

## Chunk 3: Environment Override Files

### Task 9: Create `compose.dev.yml`

**Files:**
- Rewrite: `infra/compose.dev.yml`

- [ ] **Step 1: Rewrite compose.dev.yml**

Override adds to each service:
- `env_file: [./secrets/dev/<name>.secret]`
- `ports: ["127.0.0.1:<port>:<port>"]`
- `environment:` with dev-specific values
- `volumes:` with `_dev_data` suffixed volumes where needed

Services to override:

```yaml
services:
  postgres:
    env_file: [./secrets/dev/database.secret]
    ports: ["127.0.0.1:5432:5432"]
    environment:
      POSTGRES_SHARED_BUFFERS: 512MB
      POSTGRES_EFFECTIVE_CACHE_SIZE: 1536MB
    volumes:
      - postgres_dev_data:/var/lib/postgresql/data
      - ./init/postgres-init.sql:/docker-entrypoint-initdb.d/init.sql:ro

  redis:
    env_file: [./secrets/dev/redis.secret]
    ports: ["127.0.0.1:6379:6379"]
    command: ["sh", "-c", "redis-server /etc/redis/redis.conf --requirepass $$REDIS_PASSWORD"]

  qdrant:
    env_file: [./secrets/dev/qdrant.secret]
    ports: ["127.0.0.1:6333:6333", "127.0.0.1:6334:6334"]
    environment:
      QDRANT__SERVICE__API_KEY: ${QDRANT_API_KEY}

  api:
    env_file:
      - ./secrets/dev/database.secret
      - ./secrets/dev/redis.secret
      - ./secrets/dev/qdrant.secret
      - ./secrets/dev/jwt.secret
      - ./secrets/dev/openrouter.secret
      - ./secrets/dev/admin.secret
      - ./secrets/dev/embedding-service.secret
      - ./secrets/dev/storage.secret
      - ./env/api.env.dev
    ports: ["127.0.0.1:8080:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
      POSTGRES_HOST: postgres
      REDIS_HOST: redis
      QDRANT_URL: http://qdrant:6333
      OLLAMA_URL: http://ollama:11434
      EMBEDDING_PROVIDER: external
      Embedding__Provider: External
      EMBEDDING_MODEL: intfloat/multilingual-e5-large
      EMBEDDING_DIMENSIONS: 1024
      Embedding__Dimensions: 1024
      LOCAL_EMBEDDING_URL: http://embedding-service:8000
      Embedding__LocalServiceUrl: http://embedding-service:8000
      EMBEDDING_FALLBACK_ENABLED: "false"
      Embedding__FallbackEnabled: "false"
      RERANKER_URL: http://reranker-service:8003
    volumes:
      - ./secrets/dev:/app/infra/secrets:ro

  web:
    env_file: [./env/web.env.dev]
    ports: ["127.0.0.1:3000:3000"]

  # AI services — ports only
  ollama:
    ports: ["127.0.0.1:11434:11434"]
  embedding-service:
    ports: ["127.0.0.1:8000:8000"]
  unstructured-service:
    ports: ["127.0.0.1:8001:8001"]
  smoldocling-service:
    ports: ["127.0.0.1:8002:8002"]
  reranker-service:
    ports: ["127.0.0.1:8003:8003"]
  orchestration-service:
    env_file: [./secrets/dev/database.secret, ./secrets/dev/redis.secret]
    ports: ["127.0.0.1:8004:8004"]
    environment:
      EMBEDDING_SERVICE_URL: http://embedding-service:8000
      RERANKER_SERVICE_URL: http://reranker-service:8003
      UNSTRUCTURED_SERVICE_URL: http://unstructured-service:8001
      SMOLDOCLING_SERVICE_URL: http://smoldocling-service:8002
      QDRANT_URL: http://qdrant:6333

  # Monitoring
  prometheus:
    ports: ["127.0.0.1:9090:9090"]
    command: ['--config.file=/etc/prometheus/prometheus.yml', '--storage.tsdb.path=/prometheus', '--storage.tsdb.retention.time=30d', '--web.enable-lifecycle']
  grafana:
    env_file: [./secrets/dev/monitoring.secret]
    ports: ["127.0.0.1:3001:3000"]
    environment:
      GF_SECURITY_ADMIN_USER: admin
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_USERS_ALLOW_SIGN_UP: "false"
      GF_SERVER_ROOT_URL: "http://localhost:3001"
      GF_SECURITY_ALLOW_EMBEDDING: "true"
      GF_AUTH_ANONYMOUS_ENABLED: "true"
      GF_AUTH_ANONYMOUS_ORG_ROLE: "Viewer"
  alertmanager:
    env_file: [./secrets/dev/monitoring.secret]
    ports: ["127.0.0.1:9093:9093"]
  cadvisor:
    ports: ["127.0.0.1:8082:8080"]
  node-exporter:
    ports: ["127.0.0.1:9100:9100"]

  # Automation
  n8n:
    env_file: [./secrets/dev/database.secret, ./secrets/dev/n8n.secret, ./env/n8n.env.dev]
    ports: ["127.0.0.1:5678:5678"]
  mailpit:
    ports: ["127.0.0.1:1025:1025", "127.0.0.1:8025:8025"]

  # Storage
  minio:
    env_file: [./secrets/dev/storage.secret]
    ports: ["127.0.0.1:9000:9000", "127.0.0.1:9001:9001"]
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER:-minioadmin}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD:-minioadmin123}

volumes:
  postgres_dev_data:
```

- [ ] **Step 2: Validate**

```bash
cd infra && docker compose -f docker-compose.yml -f compose.dev.yml config --quiet
```

- [ ] **Step 3: Commit**

```bash
git add infra/compose.dev.yml
git commit -m "refactor(infra): rewrite compose.dev.yml for per-environment secrets structure"
```

---

### Task 10: Create `compose.integration.yml`

**Files:**
- Create: `infra/compose.integration.yml`

- [ ] **Step 1: Write compose.integration.yml**

Only `api` and `web` services. All infra services are simply not declared — they run on the remote server.

```yaml
# Integration: Local code + remote services via SSH tunnel + Traefik basic auth
# Prerequisites: make tunnel (SSH tunnels to staging server)
#
# Usage: docker compose -f docker-compose.yml -f compose.integration.yml up api web -d

services:
  api:
    ports: ["127.0.0.1:8080:8080"]
    extra_hosts: ["host.docker.internal:host-gateway"]
    env_file:
      - ./secrets/integration/database.secret
      - ./secrets/integration/redis.secret
      - ./secrets/integration/qdrant.secret
      - ./secrets/integration/jwt.secret
      - ./secrets/integration/openrouter.secret
      - ./secrets/integration/admin.secret
      - ./secrets/integration/services-auth.secret
    environment:
      ASPNETCORE_ENVIRONMENT: Integration
      ASPNETCORE_URLS: http://+:8080
      POSTGRES_HOST: host.docker.internal
      POSTGRES_PORT: "15432"
      REDIS_HOST: host.docker.internal
      REDIS_PORT: "16379"
      QDRANT_URL: http://host.docker.internal:16333
      EMBEDDING_PROVIDER: external
      Embedding__Provider: External
      EMBEDDING_MODEL: intfloat/multilingual-e5-large
      EMBEDDING_DIMENSIONS: 1024
      Embedding__Dimensions: 1024
      LOCAL_EMBEDDING_URL: https://meepleai.app/services/embedding
      Embedding__LocalServiceUrl: https://meepleai.app/services/embedding
      RERANKER_URL: https://meepleai.app/services/reranker
      EMBEDDING_FALLBACK_ENABLED: "false"
      Embedding__FallbackEnabled: "false"
    volumes:
      - ./secrets/integration:/app/infra/secrets:ro

  web:
    ports: ["127.0.0.1:3000:3000"]
    extra_hosts: ["host.docker.internal:host-gateway"]
    environment:
      NODE_ENV: development
      NEXT_PUBLIC_API_BASE: http://localhost:8080
```

- [ ] **Step 2: Validate YAML**

```bash
cd infra && docker compose -f docker-compose.yml -f compose.integration.yml config --quiet 2>&1 | head -5
```

- [ ] **Step 3: Commit**

```bash
git add infra/compose.integration.yml
git commit -m "feat(infra): add compose.integration.yml for local code + remote services"
```

---

### Task 11: Rewrite `compose.staging.yml`

**Files:**
- Rewrite: `infra/compose.staging.yml`

- [ ] **Step 1: Rewrite compose.staging.yml**

Key changes from current:
- `env_file` paths now use `./secrets/staging/` prefix
- Volume names preserve existing names (`meepleai-pgdata-staging` etc.) to avoid data loss
- Traefik labels for public routing + basic auth on `/services/*`
- `restart: always` for production-like reliability

```yaml
# Staging: meepleai.app deployment
# Server: deploy@204.168.135.69 (Hetzner CAX21 ARM64)
#
# Usage: docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
#          --profile ai --profile monitoring --profile proxy up -d

services:
  postgres:
    restart: always
    env_file: [./secrets/staging/database.secret]
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-meepleai_staging}
      POSTGRES_USER: ${POSTGRES_USER:-meepleai}
    volumes:
      - pgdata-staging:/var/lib/postgresql/data
      - ./init/postgres-init.sql:/docker-entrypoint-initdb.d/init.sql:ro

  redis:
    restart: always
    env_file: [./secrets/staging/redis.secret]
    command: ["sh", "-c", "redis-server /etc/redis/redis.conf --requirepass $$REDIS_PASSWORD"]
    volumes:
      - redisdata-staging:/data

  qdrant:
    restart: always
    env_file: [./secrets/staging/qdrant.secret]
    environment:
      QDRANT__SERVICE__API_KEY: ${QDRANT_API_KEY}
    volumes:
      - qdrantdata-staging:/qdrant/storage

  api:
    restart: always
    env_file:
      - ./secrets/staging/database.secret
      - ./secrets/staging/jwt.secret
      - ./secrets/staging/admin.secret
      - ./secrets/staging/openrouter.secret
      - ./secrets/staging/oauth.secret
      - ./secrets/staging/redis.secret
      - ./secrets/staging/qdrant.secret
      - ./secrets/staging/embedding-service.secret
      - ./secrets/staging/storage.secret
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
      ASPNETCORE_URLS: http://+:8080
      POSTGRES_HOST: postgres
      POSTGRES_USER: ${POSTGRES_USER:-meepleai}
      POSTGRES_DB: ${POSTGRES_DB:-meepleai_staging}
      REDIS_HOST: redis
      QDRANT_URL: http://qdrant:6333
      EMBEDDING_PROVIDER: external
      Embedding__Provider: External
      LOCAL_EMBEDDING_URL: http://embedding-service:8000
      Embedding__LocalServiceUrl: http://embedding-service:8000
      RERANKER_URL: http://reranker-service:8003
      Authentication__OAuth__CallbackBaseUrl: "https://meepleai.app"
      FrontendUrl: "https://meepleai.app"
      Authentication__SessionCookie__Secure: "true"
      Authentication__SessionCookie__SameSite: "Lax"
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=Host(`meepleai.app`) && PathPrefix(`/api`)"
      - "traefik.http.routers.api.entrypoints=websecure"
      - "traefik.http.routers.api.tls.certresolver=letsencrypt"
      - "traefik.http.services.api.loadbalancer.server.port=8080"
    logging:
      driver: "json-file"
      options: { max-size: "10m", max-file: "3" }

  web:
    restart: always
    build:
      args:
        NEXT_PUBLIC_API_BASE: "https://meepleai.app"
        NEXT_PUBLIC_APP_URL: "https://meepleai.app"
    environment:
      NODE_ENV: production
      NEXT_PUBLIC_API_BASE: "https://meepleai.app"
      NEXT_PUBLIC_APP_URL: "https://meepleai.app"
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=Host(`meepleai.app`)"
      - "traefik.http.routers.web.entrypoints=websecure"
      - "traefik.http.routers.web.tls.certresolver=letsencrypt"
      - "traefik.http.services.web.loadbalancer.server.port=3000"
    logging:
      driver: "json-file"
      options: { max-size: "10m", max-file: "3" }

  # AI services with Traefik basic auth for integration access
  embedding-service:
    restart: always
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.embedding.rule=Host(`meepleai.app`) && PathPrefix(`/services/embedding`)"
      - "traefik.http.routers.embedding.entrypoints=websecure"
      - "traefik.http.routers.embedding.tls.certresolver=letsencrypt"
      - "traefik.http.routers.embedding.middlewares=integration-auth,strip-services-embedding"
      - "traefik.http.middlewares.strip-services-embedding.stripprefix.prefixes=/services/embedding"
      - "traefik.http.services.embedding.loadbalancer.server.port=8000"

  reranker-service:
    restart: always
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.reranker.rule=Host(`meepleai.app`) && PathPrefix(`/services/reranker`)"
      - "traefik.http.routers.reranker.entrypoints=websecure"
      - "traefik.http.routers.reranker.tls.certresolver=letsencrypt"
      - "traefik.http.routers.reranker.middlewares=integration-auth,strip-services-reranker"
      - "traefik.http.middlewares.strip-services-reranker.stripprefix.prefixes=/services/reranker"
      - "traefik.http.services.reranker.loadbalancer.server.port=8003"

  unstructured-service:
    restart: always
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.unstructured.rule=Host(`meepleai.app`) && PathPrefix(`/services/unstructured`)"
      - "traefik.http.routers.unstructured.entrypoints=websecure"
      - "traefik.http.routers.unstructured.tls.certresolver=letsencrypt"
      - "traefik.http.routers.unstructured.middlewares=integration-auth,strip-services-unstructured"
      - "traefik.http.middlewares.strip-services-unstructured.stripprefix.prefixes=/services/unstructured"
      - "traefik.http.services.unstructured.loadbalancer.server.port=8001"

  smoldocling-service:
    restart: always
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.smoldocling.rule=Host(`meepleai.app`) && PathPrefix(`/services/smoldocling`)"
      - "traefik.http.routers.smoldocling.entrypoints=websecure"
      - "traefik.http.routers.smoldocling.tls.certresolver=letsencrypt"
      - "traefik.http.routers.smoldocling.middlewares=integration-auth,strip-services-smoldocling"
      - "traefik.http.middlewares.strip-services-smoldocling.stripprefix.prefixes=/services/smoldocling"
      - "traefik.http.services.smoldocling.loadbalancer.server.port=8002"

  ollama:
    restart: always
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.ollama.rule=Host(`meepleai.app`) && PathPrefix(`/services/ollama`)"
      - "traefik.http.routers.ollama.entrypoints=websecure"
      - "traefik.http.routers.ollama.tls.certresolver=letsencrypt"
      - "traefik.http.routers.ollama.middlewares=integration-auth,strip-services-ollama"
      - "traefik.http.middlewares.strip-services-ollama.stripprefix.prefixes=/services/ollama"
      - "traefik.http.services.ollama.loadbalancer.server.port=11434"

  # Monitoring with Traefik basic auth
  grafana:
    restart: always
    env_file: [./secrets/staging/monitoring.secret]
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.grafana.rule=Host(`meepleai.app`) && PathPrefix(`/grafana`)"
      - "traefik.http.routers.grafana.entrypoints=websecure"
      - "traefik.http.routers.grafana.tls.certresolver=letsencrypt"
      - "traefik.http.routers.grafana.middlewares=integration-auth"
      - "traefik.http.services.grafana.loadbalancer.server.port=3000"
    environment:
      GF_SERVER_ROOT_URL: "https://meepleai.app/grafana"
      GF_SERVER_SERVE_FROM_SUB_PATH: "true"
    volumes:
      - grafana-staging:/var/lib/grafana
      - ./grafana-datasources.yml:/etc/grafana/provisioning/datasources/datasources.yml:ro
      - ./grafana-dashboards.yml:/etc/grafana/provisioning/dashboards/dashboards.yml:ro
      - ./dashboards:/var/lib/grafana/dashboards:ro

  prometheus:
    restart: always
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.prometheus.rule=Host(`meepleai.app`) && PathPrefix(`/prometheus`)"
      - "traefik.http.routers.prometheus.entrypoints=websecure"
      - "traefik.http.routers.prometheus.tls.certresolver=letsencrypt"
      - "traefik.http.routers.prometheus.middlewares=integration-auth"
      - "traefik.http.services.prometheus.loadbalancer.server.port=9090"
    command: ['--config.file=/etc/prometheus/prometheus.yml', '--storage.tsdb.path=/prometheus', '--storage.tsdb.retention.time=60d', '--web.enable-lifecycle']
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ./prometheus-rules.yml:/etc/prometheus/prometheus-rules.yml:ro
      - prometheus-staging:/prometheus

  alertmanager:
    restart: always

  n8n:
    restart: always

  # Traefik basic auth middleware for integration access
  traefik:
    labels:
      - "traefik.http.middlewares.integration-auth.basicauth.users=${INTEGRATION_BASIC_AUTH}"
      - "traefik.http.middlewares.rate-limit.ratelimit.average=100"
      - "traefik.http.middlewares.rate-limit.ratelimit.burst=50"

# Preserve existing staging volume names to avoid data loss
volumes:
  pgdata-staging:
    name: meepleai-pgdata-staging
  qdrantdata-staging:
    name: meepleai-qdrantdata-staging
  redisdata-staging:
    name: meepleai-redisdata-staging
  prometheus-staging:
    name: meepleai-prometheus-staging
  grafana-staging:
    name: meepleai-grafana-staging
```

- [ ] **Step 2: Commit**

```bash
git add infra/compose.staging.yml
git commit -m "refactor(infra): rewrite compose.staging.yml with Traefik routing and per-env secrets"
```

---

### Task 12: Rewrite `compose.traefik.yml` (Linux-only, shared staging/prod)

**Files:**
- Rewrite: `infra/compose.traefik.yml`

- [ ] **Step 1: Rewrite compose.traefik.yml**

Linux-only file. Uses Unix socket via docker-socket-proxy v0.6.

```yaml
# Traefik reverse proxy — Linux only (staging/prod servers)
# Dev on Windows uses direct port binding, not Traefik.

services:
  docker-socket-proxy:
    image: ghcr.io/tecnativa/docker-socket-proxy:0.6
    restart: always
    environment:
      CONTAINERS: 1
      SERVICES: 1
      TASKS: 0
      NETWORKS: 0
      POST: 0
      BUILD: 0
      COMMIT: 0
      EXEC: 0
      IMAGES: 0
      VOLUMES: 0
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    networks:
      - meepleai
    security_opt:
      - no-new-privileges=true
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:2375/version"]
      interval: 10s
      timeout: 5s
      retries: 3

  traefik:
    image: traefik:v3.6
    restart: always
    depends_on:
      docker-socket-proxy:
        condition: service_healthy
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - traefik_certs:/letsencrypt
      - ./traefik/dynamic:/etc/traefik/dynamic:ro
    networks:
      - meepleai
    security_opt:
      - no-new-privileges=true
    healthcheck:
      test: ["CMD", "traefik", "healthcheck"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  traefik_certs:
```

Note: The `traefik` command/config is loaded via `traefik.staging.yml` or `traefik.prod.yml` static config file — mounted as a volume by the env-specific override. The staging override adds `- ./traefik/traefik.staging.yml:/etc/traefik/traefik.yml:ro` to volumes.

Actually, better to add the staging config mount in `compose.staging.yml` under the traefik service override.

- [ ] **Step 2: Add traefik config mount to compose.staging.yml**

Add to the `traefik:` service in `compose.staging.yml`:
```yaml
  traefik:
    volumes:
      - ./traefik/traefik.staging.yml:/etc/traefik/traefik.yml:ro
    labels:
      # ... (already defined above)
```

- [ ] **Step 3: Commit**

```bash
git add infra/compose.traefik.yml
git commit -m "refactor(infra): rewrite compose.traefik.yml as Linux-only shared config with socket-proxy v0.6"
```

---

### Task 13: Update `compose.prod.yml`

**Files:**
- Rewrite: `infra/compose.prod.yml`

- [ ] **Step 1: Rewrite compose.prod.yml**

Same structure as staging but with:
- `env_file` paths use `./secrets/prod/`
- `${PROD_DOMAIN}` instead of hardcoded `meepleai.app`
- Stricter resource limits
- Volume names with `-prod` suffix
- Traefik labels with `${PROD_DOMAIN}`

Use the current `compose.prod.yml` as reference for resource limits. Update `env_file` paths and labels.

- [ ] **Step 2: Commit**

```bash
git add infra/compose.prod.yml
git commit -m "refactor(infra): rewrite compose.prod.yml with per-env secrets and PROD_DOMAIN variable"
```

---

## Chunk 4: Secrets, Scripts, and Makefile

### Task 14: Create per-environment secret directories

**Files:**
- Create: `infra/secrets/dev/.gitkeep`
- Create: `infra/secrets/integration/.gitkeep`
- Create: `infra/secrets/staging/.gitkeep`  (already exists as `infra/secrets/prod/`)
- Modify: `infra/.gitignore` or root `.gitignore` — add secret folder exclusions

- [ ] **Step 1: Create secret directories**

```bash
mkdir -p infra/secrets/{dev,integration,staging}
touch infra/secrets/dev/.gitkeep infra/secrets/integration/.gitkeep infra/secrets/staging/.gitkeep
```

- [ ] **Step 2: Move existing staging secrets**

If `infra/secrets/*.secret` files exist on the staging server, they need to be moved to `infra/secrets/staging/`. This is a server-side operation:
```bash
# ON THE STAGING SERVER:
cd /opt/meepleai/repo/infra/secrets
mkdir -p staging
cp *.secret staging/
```

- [ ] **Step 3: Update .gitignore**

Add:
```gitignore
# Per-environment secrets
infra/secrets/dev/*.secret
infra/secrets/integration/*.secret
infra/secrets/staging/*.secret
infra/secrets/prod/*.secret
```

- [ ] **Step 4: Commit**

```bash
git add infra/secrets/dev/.gitkeep infra/secrets/integration/.gitkeep infra/secrets/staging/.gitkeep .gitignore
git commit -m "chore(infra): create per-environment secret directories"
```

---

### Task 15: Refactor `setup-secrets.ps1`

**Files:**
- Rewrite: `infra/secrets/setup-secrets.ps1`

- [ ] **Step 1: Refactor setup-secrets.ps1**

Add `-Environment` parameter:
```powershell
param(
    [ValidateSet("dev", "integration", "staging", "prod")]
    [string]$Environment = "dev",
    [switch]$SaveGenerated
)
```

Key changes:
- Output dir: `$PSScriptRoot/$Environment/`
- For `dev`: auto-generate all secrets (existing behavior, new output path)
- For `integration`: prompt to copy from staging, set `POSTGRES_HOST=host.docker.internal`, `POSTGRES_PORT=15432`, etc. Generate `services-auth.secret` with basic auth creds.
- For `staging`: interactive prompt, generate `INTEGRATION_BASIC_AUTH` htpasswd in `traefik.secret`
- For `prod`: interactive + min 16 char password validation

- [ ] **Step 2: Test dev generation**

```bash
cd infra/secrets && pwsh setup-secrets.ps1 -Environment dev -SaveGenerated
ls dev/*.secret
```

- [ ] **Step 3: Commit**

```bash
git add infra/secrets/setup-secrets.ps1
git commit -m "refactor(infra): add -Environment parameter to setup-secrets.ps1"
```

---

### Task 16: Create `ssh-tunnel.ps1`

**Files:**
- Create: `infra/scripts/ssh-tunnel.ps1`

- [ ] **Step 1: Write ssh-tunnel.ps1**

```powershell
# SSH tunnel for integration environment
# Forwards Postgres, Redis, Qdrant from staging server to local ports

param(
    [ValidateSet("start", "stop", "status")]
    [string]$Action = "start",
    [string]$Server = "deploy@204.168.135.69",
    [string]$KeyPath = "$HOME/.ssh/meepleai-staging",
    [int]$PostgresLocal = 15432,
    [int]$RedisLocal = 16379,
    [int]$QdrantLocal = 16333
)

$controlSocket = "$HOME/.ssh/meepleai-tunnel.sock"

switch ($Action) {
    "start" {
        # Check if tunnel already exists
        $check = ssh -O check -S $controlSocket $Server 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tunnel already active." -ForegroundColor Yellow
            return
        }

        Write-Host "Opening SSH tunnels to $Server..." -ForegroundColor Cyan
        ssh -fN `
            -o ControlMaster=auto `
            -o ControlPath=$controlSocket `
            -o ControlPersist=yes `
            -o ExitOnForwardFailure=yes `
            -o ServerAliveInterval=30 `
            -o ServerAliveCountMax=3 `
            -L "${PostgresLocal}:localhost:5432" `
            -L "${RedisLocal}:localhost:6379" `
            -L "${QdrantLocal}:localhost:6333" `
            -i $KeyPath $Server

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tunnels open:" -ForegroundColor Green
            Write-Host "  PostgreSQL: localhost:$PostgresLocal -> server:5432"
            Write-Host "  Redis:      localhost:$RedisLocal -> server:6379"
            Write-Host "  Qdrant:     localhost:$QdrantLocal -> server:6333"
        } else {
            Write-Host "Failed to open tunnels. Check SSH key and server." -ForegroundColor Red
        }
    }
    "stop" {
        Write-Host "Closing SSH tunnels..." -ForegroundColor Cyan
        ssh -O exit -S $controlSocket $Server 2>$null
        if (Test-Path $controlSocket) { Remove-Item $controlSocket -Force }
        Write-Host "Tunnels closed." -ForegroundColor Green
    }
    "status" {
        $check = ssh -O check -S $controlSocket $Server 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tunnels ACTIVE" -ForegroundColor Green
            Write-Host "  PostgreSQL: localhost:$PostgresLocal"
            Write-Host "  Redis:      localhost:$RedisLocal"
            Write-Host "  Qdrant:     localhost:$QdrantLocal"
        } else {
            Write-Host "No active tunnels." -ForegroundColor Yellow
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/ssh-tunnel.ps1
git commit -m "feat(infra): add SSH tunnel script for integration environment"
```

---

### Task 17: Create Makefile

**Files:**
- Create: `infra/Makefile`

- [ ] **Step 1: Write Makefile**

```makefile
.DEFAULT_GOAL := help
COMPOSE := docker compose -f docker-compose.yml

# === Environments ===

dev: ## Start full local development (all profiles)
	$(COMPOSE) -f compose.dev.yml \
		--profile ai --profile monitoring --profile automation \
		--profile proxy --profile storage up -d

dev-core: ## Start core only (postgres, redis, qdrant, api, web)
	$(COMPOSE) -f compose.dev.yml up -d

dev-down: ## Stop dev environment
	$(COMPOSE) -f compose.dev.yml --profile ai --profile monitoring \
		--profile automation --profile proxy --profile storage down

tunnel: ## Open SSH tunnels to staging server
	pwsh -File scripts/ssh-tunnel.ps1 -Action start

tunnel-stop: ## Close SSH tunnels
	pwsh -File scripts/ssh-tunnel.ps1 -Action stop

tunnel-status: ## Check SSH tunnel status
	pwsh -File scripts/ssh-tunnel.ps1 -Action status

integration: ## Start local api+web with remote services (run 'make tunnel' first)
	@echo "Ensure tunnels are open: make tunnel"
	$(COMPOSE) -f compose.integration.yml up api web -d

integration-down: ## Stop integration
	$(COMPOSE) -f compose.integration.yml down

staging: ## Deploy staging on server (meepleai.app)
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy up -d

staging-down: ## Stop staging
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy down

prod: ## Deploy production (requires PROD_DOMAIN env var)
	@test -n "$(PROD_DOMAIN)" || (echo "Error: PROD_DOMAIN is required" && exit 1)
	$(COMPOSE) -f compose.prod.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy up -d

prod-down: ## Stop production
	$(COMPOSE) -f compose.prod.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy down

# === Secrets ===

secrets-dev: ## Generate dev secrets (auto-generated)
	pwsh -File secrets/setup-secrets.ps1 -Environment dev -SaveGenerated

secrets-integration: ## Setup integration secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment integration

secrets-staging: ## Setup staging secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment staging

secrets-prod: ## Setup production secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment prod

# === Utilities ===

logs: ## Tail logs for a service (usage: make logs s=api)
	$(COMPOSE) logs -f $(s)

ps: ## Show running containers
	$(COMPOSE) ps

build: ## Build all images
	$(COMPOSE) build

clean: ## Remove all volumes and containers (DATA LOSS!)
	@echo "WARNING: This will delete ALL data. Press Ctrl+C to cancel."
	@sleep 3
	$(COMPOSE) down -v --remove-orphans

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

.PHONY: dev dev-core dev-down tunnel tunnel-stop tunnel-status integration \
        integration-down staging staging-down prod prod-down \
        secrets-dev secrets-integration secrets-staging secrets-prod \
        logs ps build clean help
```

- [ ] **Step 2: Test help command**

```bash
cd infra && make help
```

- [ ] **Step 3: Commit**

```bash
git add infra/Makefile
git commit -m "feat(infra): add Makefile with convenience commands for all environments"
```

---

## Chunk 5: Traefik Config and Validation

### Task 18: Update Traefik static config for staging

**Files:**
- Rewrite: `infra/traefik/traefik.staging.yml`

- [ ] **Step 1: Update traefik.staging.yml**

Ensure it has:
- `entryPoints.web` (80) with HTTP→HTTPS redirect
- `entryPoints.websecure` (443)
- Docker provider via socket proxy (`tcp://docker-socket-proxy:2375`)
- Let's Encrypt ACME HTTP challenge for `meepleai.app`
- `providers.docker.exposedByDefault: false`
- `providers.docker.network: meepleai`
- Log level INFO
- Prometheus metrics enabled

- [ ] **Step 2: Commit**

```bash
git add infra/traefik/traefik.staging.yml
git commit -m "refactor(infra): update traefik.staging.yml for new compose structure"
```

---

### Task 19: Remove backup and final cleanup

**Files:**
- Delete: `infra/docker-compose.yml.bak` (created in Task 8)
- Modify: `CLAUDE.md` — update Docker commands section
- Modify: `infra/start-full.ps1` — update or delete (replaced by Makefile)
- Modify: `infra/start-full.sh` — update or delete
- Modify: `infra/start-observability.sh` — update or delete

- [ ] **Step 1: Delete backup file**

```bash
rm infra/docker-compose.yml.bak
```

- [ ] **Step 2: Delete legacy start scripts (replaced by Makefile)**

```bash
git rm infra/start-full.ps1 infra/start-full.sh infra/start-observability.sh
```

- [ ] **Step 3: Update CLAUDE.md Quick Reference**

Update the Quick Reference table to use `make` commands:

| Task | Command | Location |
|------|---------|----------|
| Start Dev | `make dev` | `infra/` |
| Start Core Only | `make dev-core` | `infra/` |
| Start Integration | `make tunnel && make integration` | `infra/` |
| Deploy Staging | `make staging` | `infra/` (on server) |

- [ ] **Step 4: Commit**

```bash
git add infra/ CLAUDE.md
git commit -m "chore(infra): remove legacy start scripts, update CLAUDE.md with make commands"
```

---

## Chunk 6: Smoke Test

### Task 20: Validate dev environment end-to-end

- [ ] **Step 1: Generate dev secrets**

```bash
cd infra && make secrets-dev
```

- [ ] **Step 2: Start dev-core**

```bash
make dev-core
```

- [ ] **Step 3: Verify services are healthy**

```bash
make ps
# Expected: postgres, redis, qdrant, api, web — all healthy
```

- [ ] **Step 4: Test API**

```bash
curl http://localhost:8080/
# Expected: 200 OK or health response
```

- [ ] **Step 5: Test Web**

```bash
curl http://localhost:3000/
# Expected: 200 OK (Next.js page)
```

- [ ] **Step 6: Stop and clean up**

```bash
make dev-down
```

- [ ] **Step 7: Validate integration config**

```bash
# Just validate YAML, don't start (no tunnel available)
docker compose -f docker-compose.yml -f compose.integration.yml config --quiet
```

- [ ] **Step 8: Validate staging config**

```bash
docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml config --quiet
```

- [ ] **Step 9: Final commit if any fixes needed**

```bash
git add -A && git commit -m "fix(infra): smoke test fixes for multi-environment setup"
```

---

## Chunk 7: Documentation PDF

### Task 21: Regenerate Docker environments PDF guide

**Files:**
- Regenerate: `docs/deployment/docker-environments-guide.pdf`

The PDF was initially generated during the design phase but needs to be regenerated to reflect:
- HyperDX removal (no more observability profile references to HyperDX)
- Final file structure after implementation
- Actual Makefile commands
- Any changes made during implementation

- [ ] **Step 1: Generate updated PDF**

Use Python `reportlab` to regenerate `docs/deployment/docker-environments-guide.pdf` with:
- Updated file structure (no legacy files)
- Correct profile list (no HyperDX)
- Final Makefile commands
- Final Traefik routing table
- Updated troubleshooting section

Language: Italian.

- [ ] **Step 2: Commit**

```bash
git add docs/deployment/docker-environments-guide.pdf
git commit -m "docs(deployment): regenerate Docker multi-environment PDF guide"
```

---

## Summary

| Chunk | Tasks | Description |
|-------|-------|-------------|
| 1 | 1-7 | Cleanup: delete legacy files, remove HyperDX everywhere |
| 2 | 8 | Rewrite base `docker-compose.yml` |
| 3 | 9-13 | Create/rewrite 4 environment overrides + traefik |
| 4 | 14-17 | Secrets dirs, setup script, SSH tunnel, Makefile |
| 5 | 18-19 | Traefik config, final cleanup |
| 6 | 20 | Smoke test validation |
| 7 | 21 | Regenerate PDF documentation |

**Total: 21 tasks, ~52 steps**

**Branch**: `feature/docker-multi-environment`
**Parent**: `main-dev`
**PR target**: `main-dev`
