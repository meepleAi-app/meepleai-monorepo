# MeepleAI Infrastructure

**Infrastructure as Code** - Docker Compose, monitoring, secrets management, and observability stack configuration.

## 🎯 Quick Start Guide

### Development (Recommended)

```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

### Test/CI (Fast, In-Memory)

```bash
cd infra
docker compose -f docker-compose.yml -f compose.test.yml up -d
```

### Staging (Production-Like)

```bash
cd infra
docker compose -f docker-compose.yml -f compose.staging.yml up -d
```

### Production (Full Security)

```bash
cd infra
# Ensure secrets are configured first!
./tools/secrets/init-secrets.sh

docker compose -f docker-compose.yml -f compose.prod.yml up -d
```

### MCP Servers (Optional)

```bash
cd docker/mcp
docker compose up -d
```

---

## 📁 File Structure

```
infra/
├── docker-compose.yml              # Base configuration (production-ready with secrets)
├── docker-compose.dev.yml          # Development overrides (85 lines, extends base) ⭐ REFACTORED
├── compose.test.yml                # Test/CI overrides (in-memory, minimal) ⭐ FIXED
├── compose.staging.yml             # Staging overrides (production-like) ⭐ FIXED
├── compose.prod.yml                # Production overrides (resource limits) ⭐ FIXED
├── experimental/                   # Experimental features ⭐ NEW
│   └── docker-compose.infisical.yml  # POC: Infisical secrets management
├── prometheus.yml                  # Prometheus configuration
├── prometheus-rules.yml            # Alert rules
├── alertmanager.yml                # Alert routing configuration
├── grafana-datasources.yml         # Grafana data sources
├── grafana-dashboards.yml          # Grafana dashboard config
├── dashboards/                     # Grafana dashboard JSON
├── env/                            # Environment configuration files
├── init/                           # Initialization scripts
│   ├── postgres-init.sql
│   └── n8n/                        # n8n workflow initialization
├── n8n/                            # n8n workflow definitions
│   ├── templates/                  # Workflow templates
│   └── workflows/                  # Production workflows
├── prometheus/                     # Prometheus configurations
│   └── alerts/                     # Modular alert rule files
├── scripts/                        # Infrastructure utility scripts
└── secrets/                        # Docker secrets (gitignored)
```

---

## 🏗️ Architecture Changes (2025-11-22 Refactor)

### What Changed

#### ✅ Fixed: docker-compose.dev.yml

**Before** (647 lines):
- Duplicated ALL services from base file
- Difficult to maintain (changes needed in 2 places)
- Included redundant MCP servers

**After** (85 lines, **87% reduction**):
- Extends `docker-compose.yml` via Docker Compose merge
- Only overrides necessary for development
- No more duplication

**Migration**:
```bash
# Old way (deprecated)
docker compose -f docker-compose.dev.yml up -d

# New way (required)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

#### ✅ Fixed: compose.{test,staging,prod}.yml

**Issue**: Used incorrect service names (`meepleai-postgres` instead of `postgres`)

**Fix**: All override files now use correct base service names

**Impact**: Override files now actually work for their intended environments

#### ✅ Moved: docker-compose.infisical.yml

**Before**: `infra/docker-compose.infisical.yml`

**After**: `infra/experimental/docker-compose.infisical.yml`

**Reason**: Clarifies that this is a POC (Issue #936), not production-ready

### Migration Guide

If you were using the old files:

1. **docker-compose.dev.yml users**:
   ```bash
   # Replace this:
   docker compose -f docker-compose.dev.yml up -d

   # With this:
   docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
   ```

2. **Infisical users**:
   ```bash
   # Replace this:
   docker compose -f docker-compose.infisical.yml up -d

   # With this:
   cd experimental
   docker compose -f docker-compose.infisical.yml up -d
   ```

3. **No changes needed** for test/staging/prod (just fixed internally)

---

## 🗂️ Compose Files Reference

### Base: `docker-compose.yml`

**Purpose**: Production-ready base with all 15 services

**Features**:
- ✅ Docker secrets for sensitive data
- ✅ Full observability stack
- ✅ AI/ML services (Ollama, Embedding, Unstructured, SmolDocling)
- ✅ Core infrastructure (PostgreSQL, Qdrant, Redis, n8n)

**Services**: postgres, qdrant, redis, ollama, ollama-pull, embedding-service, unstructured-service, smoldocling-service, seq, jaeger, prometheus, alertmanager, grafana, n8n, api, web

**Secrets Required**:
- `./secrets/postgres-password.txt`
- `./secrets/openrouter-api-key.txt`
- `./secrets/n8n-encryption-key.txt`
- `./secrets/n8n-basic-auth-password.txt`
- `./secrets/gmail-app-password.txt`
- `./secrets/grafana-admin-password.txt`
- `./secrets/initial-admin-password.txt`

**Usage**:
```bash
# Initialize secrets first
./tools/secrets/init-secrets.sh

# Start stack
docker compose up -d
```

---

### Development: `docker-compose.dev.yml`

**Purpose**: Local development with simplified auth

**Overrides**:
- ❌ No Docker secrets (uses plain env vars)
- ✅ Simple passwords (postgres: `meeplepass`, grafana: `admin`)
- ✅ Only 85 lines (vs 647 before refactor)

**Usage**:
```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

**Env Vars** (optional, has defaults):
```bash
POSTGRES_PASSWORD=meeplepass
GRAFANA_ADMIN_PASSWORD=admin
OPENROUTER_API_KEY=your_key_here
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=Demo123!
```

---

### Test/CI: `compose.test.yml`

**Purpose**: Fast CI/CD pipelines

**Optimizations**:
- 🚀 PostgreSQL on tmpfs (in-memory)
- 🚀 Redis on tmpfs (in-memory)
- 🚫 Observability disabled (use `--profile observability` if needed)
- ⏱️ Faster healthchecks (3s vs 10s)

**Usage**:
```bash
# Minimal test stack
docker compose -f docker-compose.yml -f compose.test.yml up -d

# With observability (if needed)
docker compose -f docker-compose.yml -f compose.test.yml --profile observability up -d
```

---

### Staging: `compose.staging.yml`

**Purpose**: Production-like pre-release testing

**Features**:
- ✅ Docker secrets enabled
- ✅ Persistent volumes (staging-specific)
- ✅ Log rotation (10MB, 3 files)
- ⏰ Prometheus retention: 60 days

**Usage**:
```bash
docker compose -f docker-compose.yml -f compose.staging.yml up -d
```

**Volumes**:
- `meepleai-pgdata-staging`
- `meepleai-qdrantdata-staging`
- `meepleai-redisdata-staging`
- `meepleai-seqdata-staging`
- `meepleai-prometheus-staging`
- `meepleai-grafana-staging`

---

### Production: `compose.prod.yml`

**Purpose**: Production deployment with full security

**Features**:
- 🔐 Mandatory secrets (errors if missing)
- 🏋️ CPU/Memory limits enforced
- 📊 Prometheus retention: 90 days, 50GB max
- 💾 Persistent Jaeger storage
- 📝 Log rotation (50MB, 10 files)

**Resource Limits**:
| Service | CPU | Memory | Reserved CPU | Reserved Mem |
|---------|-----|--------|--------------|--------------|
| postgres | 2 | 4GB | 1 | 2GB |
| qdrant | 2 | 8GB | 1 | 4GB |
| api | 4 | 8GB | 2 | 4GB |
| web | 2 | 4GB | 1 | 2GB |

**Required Env Vars**:
```bash
REDIS_PASSWORD=<required>
PRODUCTION_API_URL=<required>
GRAFANA_ROOT_URL=<required>
SEQ_PASSWORD_HASH=<required>
```

**Usage**:
```bash
# Validate secrets
ls -la ./secrets/prod/

# Deploy
docker compose -f docker-compose.yml -f compose.prod.yml up -d
```

---

### Experimental: Infisical POC

**Location**: `experimental/docker-compose.infisical.yml`

**Purpose**: POC for automatic secrets rotation (Issue #936)

**Status**: ⚠️ Experimental, not production-ready

**Usage**:
```bash
cd experimental
docker compose -f docker-compose.infisical.yml up -d

# Access UI
open http://localhost:8080
```

See file for detailed setup instructions.

---

## 📦 Services Overview

### Core Services

| Service | Port | Description | Health Check |
|---------|------|-------------|--------------|
| **postgres** | 5432 | PostgreSQL 16 database | `pg_isready` |
| **qdrant** | 6333 | Vector database | `curl localhost:6333/healthz` |
| **redis** | 6379 | Cache & sessions | `redis-cli ping` |

### AI/ML Services

| Service | Port | Description |
|---------|------|-------------|
| **ollama** | 11434 | Local LLM runtime |
| **embedding-service** | 8000 | BGE-M3 multilingual embeddings |
| **unstructured-service** | 8001 | PDF extraction (Stage 1) |
| **smoldocling-service** | 8002 | VLM PDF extraction (Stage 2) |

### Observability Services

| Service | Port | Description | URL |
|---------|------|-------------|-----|
| **seq** | 8081 | Centralized logging | http://localhost:8081 |
| **jaeger** | 16686 | Distributed tracing | http://localhost:16686 |
| **prometheus** | 9090 | Metrics collection | http://localhost:9090 |
| **alertmanager** | 9093 | Alert routing | http://localhost:9093 |
| **grafana** | 3001 | Dashboards | http://localhost:3001 (admin/admin) |

### Workflow & App

| Service | Port | Description | URL |
|---------|------|-------------|-----|
| **n8n** | 5678 | Workflow automation | http://localhost:5678 |
| **api** | 8080 | ASP.NET Core API | http://localhost:8080 |
| **web** | 3000 | Next.js frontend | http://localhost:3000 |

---

## 🛠️ Common Operations

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api
docker compose logs -f postgres

# Last 100 lines
docker compose logs --tail=100 api
```

### Restart Services

```bash
# All
docker compose restart

# Specific
docker compose restart api
docker compose restart postgres
```

### Stop Stack

```bash
# Stop (keep volumes)
docker compose down

# Stop and remove volumes (⚠️ data loss)
docker compose down -v
```

### Rebuild Services

```bash
# Rebuild all
docker compose build --no-cache

# Rebuild specific
docker compose build --no-cache api

# Rebuild and restart
docker compose up -d --build api
```

### Health Checks

```bash
# API health
curl http://localhost:8080/health

# Qdrant health
curl http://localhost:6333/healthz

# Redis health
docker compose exec redis redis-cli ping

# Prometheus targets
curl http://localhost:9090/api/v1/targets
```

---

## 🔍 Troubleshooting

### Issue: Secrets Not Found

**Error**: `postgres-password secret not found`

**Fix**:
```bash
./tools/secrets/init-secrets.sh
```

### Issue: Port Already in Use

**Error**: `port 5432 already allocated`

**Fix**:
```bash
# Find process
lsof -i :5432

# Stop system PostgreSQL
sudo systemctl stop postgresql
```

### Issue: Out of Disk Space

**Fix**:
```bash
# Clean volumes
docker volume prune

# Clean images
docker image prune -a

# Check usage
docker system df -v
```

### Issue: Service Won't Start

**Diagnosis**:
```bash
# Check logs
docker compose logs <service_name>

# Validate compose file
docker compose config

# Check container details
docker inspect <container_id>
```

---

## 📚 Documentation

- **[INFRASTRUCTURE.md](./INFRASTRUCTURE.md)** - Comprehensive infrastructure documentation (50+ pages)
- **[env/README.md](./env/README.md)** - Environment variables guide
- **[docs/archive/](./docs/archive/)** - Archived documentation (historical reference)
- **[Multi-Environment Strategy](../docs/05-operations/deployment/multi-environment-strategy.md)** - 3-tier deployment strategy
- **[Deployment Guide](../docs/05-operations/deployment/board-game-ai-deployment-guide.md)** - Production deployment
- **[Disaster Recovery](../docs/05-operations/deployment/disaster-recovery.md)** - DR procedures
- **[Runbooks](../docs/05-operations/runbooks/)** - Incident response guides

---

## 🔒 Security

### Secret Management

- **Development**: Plain env vars (`.env.dev`)
- **Staging/Production**: Docker secrets (`./secrets/`)
- **Never**: Commit secrets to git!

### Network Security

- Isolated Docker network
- Only necessary ports exposed
- TLS/SSL via reverse proxy (production)

### Access Control

- Grafana: Default admin/admin (**CHANGE IN PRODUCTION**)
- n8n: Configure OAuth (production)
- Seq: Configure API keys (production)

---

## 📊 Monitoring

### Key Metrics

- **API Latency**: P50, P95, P99 (target: <500ms P95)
- **Error Rate**: 5xx errors (target: <1%)
- **RAG Quality**: Confidence scores (target: ≥0.70)
- **Database**: Query time, connection pool
- **Cache**: Redis hit rate (target: >80%)

### Dashboards

Access Grafana at http://localhost:3001:

1. System Overview
2. API Performance
3. RAG Pipeline
4. PDF Processing
5. Infrastructure

### Alerts (40+ Rules)

Organized in `prometheus/alerts/`:
- `api-performance.yml` - API errors, latency
- `database-health.yml` - PostgreSQL alerts
- `cache-performance.yml` - Redis monitoring
- `vector-search.yml` - Qdrant alerts
- `infrastructure.yml` - Resource alerts
- `quality-metrics.yml` - AI quality
- `pdf-processing.yml` - PDF pipeline

---

## 🤝 Contributing

When modifying infrastructure:

1. Test locally with `docker-compose.dev.yml`
2. Update this README
3. Test health checks
4. Update Prometheus alerts (if adding services)
5. Add Grafana dashboards (if adding metrics)
6. Create PR with `[INFRA]` prefix

---

## 📝 Changelog

### 2025-11-22: Infrastructure Consolidation

**Changes**:
- ✅ Consolidated Grafana dashboards into single `/dashboards` directory
- ✅ Consolidated Prometheus alerts into single `/prometheus/alerts` directory
- ✅ Archived outdated documentation (README-dev.md, OPS-05-SETUP.md)
- ✅ Removed empty `/observability` directory structure
- ✅ Reduced `docker-compose.dev.yml` from 647 to 85 lines (87% reduction)
- ✅ Fixed service naming in `compose.{test,staging,prod}.yml`
- ✅ Moved `docker-compose.infisical.yml` to `experimental/`

**Impact**:
- Simpler directory structure (removed nested observability folder)
- All dashboards and alerts in predictable locations
- Easier maintenance (no duplication)
- Clear separation of current vs archived documentation

---

**Last Updated**: 2025-11-22
**Maintainer**: Infrastructure Team
**Total Services**: 15
**Version**: 2.0 (Post-Refactor)
