# MeepleAI — Deployment Guide

**Docker, Traefik, CI/CD, Integration Mode**

---

## Environments

| Environment | URL | Branch | Deploy |
|-------------|-----|--------|--------|
| **Local Dev** | http://localhost:3000 | any | Manual (`dotnet run` + `pnpm dev`) |
| **Integration** | localhost → staging services | any | SSH tunnel + local API/Web |
| **Staging** | https://meepleai.app | `main-staging` | Auto (push) or manual dispatch |
| **Production** | — | `main` | Not yet active |

---

## Quick Start — Local Development

### Option A: Full Docker (tutto in container)

```bash
cd infra
docker compose --profile dev up -d     # Core + monitoring
# oppure
docker compose --profile ai up -d      # Core + AI services
# oppure
docker compose --profile full up -d    # Everything
```

Access: Web `:3000`, API `:8080`, Grafana `:3001`, Scalar `:8080/scalar/v1`

### Option B: Hybrid (infra Docker, API/Web locali)

```bash
# 1. Infrastructure
cd infra && docker compose up -d postgres redis qdrant

# 2. API (Terminal 1)
cd apps/api/src/Api && dotnet run    # :8080

# 3. Frontend (Terminal 2)
cd apps/web && pnpm dev              # :3000
```

### Option C: Integration (locale → staging services)

Nessun Docker locale. API e frontend locali connessi ai servizi staging via SSH tunnel.

```bash
# 1. Apri tunnel (una volta)
bash infra/scripts/integration-tunnel.sh

# 2. API (Terminal 1)
cd apps/api/src/Api && dotnet run --launch-profile Integration

# 3. Frontend (Terminal 2)
cd apps/web && pnpm dev
```

**Prerequisiti**: chiave SSH `~/.ssh/meepleai-staging`, secrets in `infra/secrets/integration/`.

Tunnel ports: PG `:15432`, Redis `:16379`, Qdrant `:16333`, Embedding `:18000`, Reranker `:18003`, Unstructured `:18001`, SmolDocling `:18002`, Ollama `:21434`.

---

## Docker Profiles

| Profile | Services | Use Case |
|---------|----------|----------|
| *(none)* | postgres, redis, qdrant, api, web | Infra base |
| `ai` | + ollama, embedding, unstructured, smoldocling, reranker, orchestrator | PDF processing, RAG |
| `monitoring` | + prometheus, grafana, alertmanager, node-exporter, cadvisor | Observability |
| `automation` | + n8n, mailpit | Workflow, email test |
| `storage` | + minio | S3-compatible local storage |

Compose files:
- `docker-compose.yml` — base (no ports, no secrets)
- `compose.dev.yml` — dev override (ports, `secrets/dev/`)
- `compose.staging.yml` — staging (Traefik labels, `secrets/staging/`)
- `compose.integration.yml` — integration (SSH tunnel, `secrets/integration/`)
- `compose.traefik.yml` — reverse proxy

Dev override auto-loaded via `infra/.env`:
```
COMPOSE_FILE=docker-compose.yml;compose.dev.yml
```

---

## CI/CD Pipeline

### Branch Strategy

```
feature/*  --PR-->  main-dev  --merge-->  main-staging  (auto-deploy meepleai.app)
                        |
                   frontend-dev  (frontend features)
```

| Branch | Scopo | Deploy | CI |
|--------|-------|--------|-----|
| `feature/*` | Sviluppo feature | Nessuno | PR check |
| `main-dev` | Integrazione sviluppo | Nessuno | CI completa |
| `frontend-dev` | Feature frontend | Nessuno | CI completa |
| `main-staging` | Ambiente attivo | meepleai.app (auto) | CI + Deploy |
| `main` | Futuro production | Non attivo | Predisposta |

### Deploy su meepleai.app

```bash
# Opzione 1: Merge su main-staging (auto-deploy)
git checkout main-staging && git pull
git merge main-dev
git push origin main-staging

# Opzione 2: Manual dispatch
# GitHub Actions > "Deploy to Staging" > Run workflow
```

### Pipeline Steps (deploy-staging.yml)

| Step | Descrizione |
|------|-------------|
| 1. CI Tests | Frontend + Backend + Python tests |
| 2. Build Images | Docker build + push su GHCR (tag `staging-latest`) |
| 3. SSH Deploy | `docker compose up -d` sul server Hetzner |
| 4. Health Check | `curl localhost:8080/health` |
| 5. Validation | `curl https://meepleai.app/health` |
| 6. Notify | Slack + GitHub Summary |

---

## Staging Server

**Server**: Hetzner CAX21 ARM64 — `deploy@204.168.135.69`
**SSH Key**: `~/.ssh/meepleai-staging`

### Accesso

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
```

### Struttura

```
/opt/meepleai/
  docker-compose.yml
  compose.staging.yml
  compose.traefik.yml
  secrets/           # *.secret files
  traefik/           # Traefik config
  scripts/           # Utility scripts
```

### Services esposti via Traefik (basic auth)

| Servizio | URL | Porta interna |
|----------|-----|---------------|
| Web | https://meepleai.app | 3000 |
| API | https://meepleai.app/api | 8080 |
| Embedding | https://meepleai.app/services/embedding | 8000 |
| Reranker | https://meepleai.app/services/reranker | 8003 |
| Unstructured | https://meepleai.app/services/unstructured | 8001 |
| SmolDocling | https://meepleai.app/services/smoldocling | 8002 |
| Ollama | https://meepleai.app/services/ollama | 11434 |
| Qdrant | https://meepleai.app/services/qdrant | 6333 |
| Grafana | https://meepleai.app/grafana | 3000 |
| Prometheus | https://meepleai.app/prometheus | 9090 |

I servizi `/services/*` e monitoring sono protetti da basic auth (`INTEGRATION_BASIC_AUTH`).

### Manual Deploy

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
cd /opt/meepleai
export QDRANT_API_KEY=$(grep QDRANT_API_KEY secrets/qdrant.secret | cut -d= -f2)
export INTEGRATION_BASIC_AUTH='...'  # From traefik.secret
docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
  --profile ai --profile monitoring up -d
```

---

## Secrets

### Struttura

```
infra/secrets/
  *.secret              # Dev locale (default)
  dev/*.secret          # Dev Docker override (compose.dev.yml)
  integration/*.secret  # Integration mode (staging credentials)
  staging/*.secret      # Staging (server-side only)
```

### Setup iniziale

```bash
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated   # Genera tutti i secret dev
```

### File richiesti

| Priorità | File | Blocca startup |
|----------|------|----------------|
| CRITICAL | database, redis, qdrant, jwt, admin, embedding-service | Sì |
| IMPORTANT | openrouter, unstructured-service, bgg | Warning |
| OPTIONAL | oauth, email, monitoring, n8n, storage, traefik | Silenzioso |

### Feature Flags

Alcune feature richiedono un flag in `system_configurations` DB:

```sql
INSERT INTO system_configurations ("Id", "Key", "Value", "ValueType", "Category", "IsActive", ...)
VALUES (gen_random_uuid(), 'Features.PdfUpload', 'true', 'boolean', 'Features', true, ...);
```

---

## Backups

### Automatici (cron 2 AM)

```bash
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
docker exec meepleai-postgres pg_dump -U meepleai meepleai_staging | gzip > backups/postgres_$DATE.sql.gz
docker exec meepleai-qdrant tar -czf - /qdrant/storage > backups/qdrant_$DATE.tar.gz
find backups/ -name "*.gz" -mtime +7 -delete
```

### Restore

```bash
docker compose stop api
gunzip -c backups/postgres_YYYYMMDD.sql.gz | docker exec -i meepleai-postgres psql -U meepleai meepleai_staging
docker compose start api
```

---

## Production Deployment

### Prerequisites Checklist

| Requirement | Details |
|-------------|---------|
| Server | Ubuntu 22.04+, 8GB RAM, 4 CPU, 100GB SSD |
| Software | Docker 24+, Docker Compose 2.20+ |
| Network | Domain + DNS (A records), SSL via Traefik |
| Ports | 80, 443 (Traefik), 22 (SSH) |

### Setup Commands

```bash
# 1. Install Docker
curl -fsSL https://get.docker.com | sh && sudo usermod -aG docker $USER

# 2. Clone repo
git clone <repo-url> /opt/meepleai && cd /opt/meepleai

# 3. Create .env.production (see below)

# 4. Start with Traefik
cd infra && docker compose -f docker-compose.yml -f compose.traefik.yml \
  --profile full --env-file ../.env.production up -d

# 5. Verify
docker compose ps && curl https://api.meepleai.com/health
```

### Database Configuration (Issue #2460)

**Option 1: Environment Variables** (Simple):
```bash
POSTGRES_HOST=your-host.com
POSTGRES_PORT=5432
POSTGRES_DB=meepleai_prod
POSTGRES_USER=admin
POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password  # Or POSTGRES_PASSWORD
```

**Option 2: Full Connection String** (Managed DBs - AWS RDS, Azure, GCP):
```bash
ConnectionStrings__Postgres="Host=db.rds.amazonaws.com;Database=meepleai_prod;Username=admin;Password=<pwd>;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50;SSL Mode=Require;Trust Server Certificate=false;Command Timeout=30;Timeout=15"
```

**Connection Pooling**:

| Parameter | Value | Purpose |
|-----------|-------|---------|
| Pooling | true | Enable pooling |
| Min/Max Pool | 5-10 / 50-100 | Pre-allocate + limit connections |
| SSL Mode | Require | Force TLS encryption |
| Command/Timeout | 30s / 15s | Prevent hangs |

---

## Monitoring

| Service | Access | Credentials |
|---------|--------|-------------|
| **Grafana** | https://meepleai.app/grafana | `admin` / from monitoring.secret |
| **Prometheus** | https://meepleai.app/prometheus | basic auth |

**Dashboards**: System (CPU/mem/disk), API (latency/throughput), RAG (confidence), DB (pool/queries)

**Prometheus Metrics**:
```
http_requests_total{endpoint, method, status}
http_request_duration_seconds{endpoint}
rag_query_confidence{game}
database_connections_active
redis_cache_hit_rate
```

**Alerts** (Alertmanager → Slack): API P95 >1s, Error rate >5%, DB pool exhausted, Disk >80%, RAG confidence <0.70

```bash
docker compose logs -f api          # Real-time
docker compose logs --tail 100 api  # Ultimi 100
```

---

## Scaling & Security

### Horizontal Scaling

```yaml
# docker-compose.prod.yml
api:
  replicas: 3  # Behind Traefik LB
  resources: { limits: { cpus: '2', memory: 2G } }
postgres-replica:  # Read replicas
  environment: { POSTGRESQL_REPLICATION_MODE: slave }
```

**Caching**: L1 (in-memory) + L2 (Redis) → Games (5min), RAG (30min), Profiles (1min)

### Security

**Traefik TLS**: `minVersion: VersionTLS12` + `cipherSuites: [TLS_ECDHE_RSA_WITH_AES_*_GCM_SHA*]`

**Firewall**:
```bash
sudo ufw allow 22/tcp 80/tcp 443/tcp && sudo ufw enable
```

**Secrets**: `.gitignore` (.env*, *.key), Docker Secrets: `/run/secrets/postgres_password`

---

## Disaster Recovery

**RTO**: <1h | **RPO**: <24h

**Procedure**:
```bash
# 1. Provision server + Install Docker
# 2. git clone <repo> /opt/meepleai && cd /opt/meepleai
# 3. scp backup-server:.env.production .
# 4. docker compose --profile full up -d
# 5. gunzip -c backups/postgres_latest.sql.gz | docker exec -i meepleai-postgres psql -U meepleai meepleai_prod
# 6. tar -xzf backups/qdrant_latest.tar.gz -C /var/lib/docker/volumes/qdrant_data/_data/
# 7. docker compose restart && curl https://api.meepleai.com/health
```

---

## Cost Optimization

**Monthly Estimate** (AWS/DigitalOcean): ~$75-145
- Server (8GB/4CPU): $40-80 | Storage (100GB): $10 | OpenRouter (10K): $20-50 | Backups (S3): $5

**Strategies**: CDN (Cloudflare free), aggressive caching, optimize embeddings, auto-scale down, spot instances

---

## Troubleshooting

| Issue | Diagnostic | Fix |
|-------|------------|-----|
| API non risponde | `docker compose logs api` | Check DB, `docker compose restart api` |
| PDF upload 403 | Feature flag | Insert `Features.PdfUpload = true` in DB |
| SuperAdmin 403 | Auth policy | Verificare che le policy includano `"SuperAdmin"` |
| Endpoint duplicato | API crash on startup | Check `WithName()` unico per ogni endpoint |
| Redis crash | `requirepass` config error | Verificare `secrets/dev/redis.secret` esiste |
| CORS errors | `CORS__AllowedOrigins` | Aggiornare env, restart |

### Health Checks

```bash
curl http://localhost:8080/health                     # API
docker exec meepleai-postgres pg_isready -U postgres  # DB
curl http://localhost:6333/healthz                     # Qdrant
docker exec meepleai-redis redis-cli ping             # Redis
```

---

## Resources

**Operations**: [Operations Manual](../operations/operations-manual.md) — Consolidated service management, backup/restore, monitoring, incident response, disaster recovery, maintenance schedules.

**Guides**:

| Guida | Contenuto |
|-------|-----------|
| [Staging Setup](./staging-setup-guide.md) | Setup server staging completo |
| [SSH Manual Deploy](./ssh-manual-deploy.md) | Deploy manuale via SSH |
| [Docker Versioning](./docker-versioning-guide.md) | Image tagging, GHCR |
| [Deployment Workflows](./deployment-workflows-guide.md) | Pipeline staging → production |
| [R2 Storage](./r2-storage-configuration-guide.md) | Cloudflare R2 setup |
| [Infrastructure Cost](./infrastructure-cost-summary.md) | Budget per fase |
| [Domain Setup](./domain-setup-guide.md) | DNS, SSL, Cloudflare |
| [Environments](./environments.md) | Differenze dev/staging/prod |
| [Email/TOTP](./email-totp-services.md) | Email and TOTP services |
| [BGG API](./boardgamegeek-api-setup.md) | BoardGameGeek API setup |
| [Cost Optimization](./github-alternatives-cost-optimization.md) | GitHub Alternatives & Cost |

**Related**: [Monitoring](../development/README.md#monitoring), [Testing](../testing/README.md), [Security](../security/README.md)

---

**Version**: 2.1 | **Updated**: 2026-03-17 | **Maintainers**: DevOps Team
