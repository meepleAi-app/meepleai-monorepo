# Docker Compose Profiles Guide

**Last Updated**: 2026-02-02

Comprehensive guide to MeepleAI Docker Compose profiles for selective service startup.

---

## What Are Profiles?

Docker Compose profiles allow you to **selectively start subsets of services** based on your needs, saving system resources and startup time.

**Example**:
```bash
# Start only core services (5 services, ~4 GB RAM)
docker compose --profile minimal up -d

# Start everything (17+ services, ~18 GB RAM)
docker compose --profile full up -d
```

---

## Available Profiles

### Profile Matrix

| Profile | Services | RAM | CPU | Startup | Use Case |
|---------|----------|-----|-----|---------|----------|
| **minimal** | 5 | ~4 GB | 2-4 cores | ~30s | Core development |
| **dev** | 8 | ~6 GB | 4 cores | ~1min | Daily development |
| **ai** | 10 | ~12 GB | 6 cores | ~3min | AI/ML development |
| **observability** | 11 | ~8 GB | 4 cores | ~1.5min | Monitoring/debugging |
| **automation** | 6 | ~5 GB | 2 cores | ~45s | Workflow development |
| **full** | 17+ | ~18 GB | 8 cores | ~3min | Complete stack |

---

## Profile Details

### 1. Minimal Profile (Core Services)

**Purpose**: Bare minimum for API and frontend development

**Services Included**:
- `postgres` (database)
- `qdrant` (vector database)
- `redis` (cache)
- `api` (.NET backend)
- `web` (Next.js frontend)

**Resource Usage**:
- **RAM**: ~4 GB
- **CPU**: 2-4 cores
- **Disk**: ~5 GB
- **Startup Time**: ~30 seconds

**Start Command**:
```bash
cd infra
docker compose --profile minimal up -d
```

**When to Use**:
- Daily frontend/backend development
- Testing core API functionality
- Low-resource environments
- Laptop development with limited RAM

**What's Missing**:
- ❌ AI services (embedding, reranking)
- ❌ Monitoring (Grafana, Prometheus)
- ❌ Email testing (Mailpit)
- ❌ Automation (n8n)
- ❌ LLM (Ollama)

**Service Endpoints**:
- Web: http://localhost:3000
- API: http://localhost:8080
- Database: localhost:5432
- Qdrant: http://localhost:6333
- Redis: localhost:6379

---

### 2. Dev Profile (Development Stack)

**Purpose**: Extended development with monitoring and debugging tools

**Services Included**:
- All from `minimal` profile
- `prometheus` (metrics collection)
- `grafana` (dashboards)
- `mailpit` (email testing)

**Resource Usage**:
- **RAM**: ~6 GB
- **CPU**: 4 cores
- **Disk**: ~8 GB
- **Startup Time**: ~1 minute

**Start Command**:
```bash
cd infra
docker compose --profile dev up -d
```

**When to Use**:
- Development with monitoring needs
- Email feature development/testing
- Performance debugging
- API metrics analysis

**Additional Endpoints**:
- Grafana: http://localhost:3001 (admin/admin)
- Prometheus: http://localhost:9090
- Mailpit: http://localhost:8025

**Dashboards Available**:
- System Metrics (CPU, RAM, disk)
- API Performance (requests/sec, latency)
- Database Performance (queries, connections)

---

### 3. AI Profile (Machine Learning Services)

**Purpose**: Full AI/ML stack for embeddings, document processing, LLM

**Services Included**:
- All from `minimal` profile
- `ollama` (local LLM)
- `embedding-service` (multilingual embeddings)
- `reranker-service` (cross-encoder reranking)
- `unstructured-service` (PDF extraction stage 1)
- `smoldocling-service` (PDF extraction stage 2)

**Resource Usage**:
- **RAM**: ~12 GB (18 GB with GPU)
- **CPU**: 6 cores (4 cores if GPU)
- **Disk**: ~25 GB (includes model downloads)
- **Startup Time**: ~3 minutes (first time: ~5 minutes for model downloads)

**Start Command**:
```bash
cd infra
docker compose --profile ai up -d
```

**When to Use**:
- AI agent development
- PDF document processing
- RAG (Retrieval Augmented Generation) testing
- Embedding pipeline development
- Testing reranking strategies

**Additional Endpoints**:
- Ollama: http://localhost:11434
- Embedding Service: http://localhost:8000
- Reranker Service: http://localhost:8003
- Unstructured: http://localhost:8001
- SmolDocling: http://localhost:8002

**Notes**:
- ⚠️ First startup downloads models (~2 GB total)
- GPU acceleration recommended but not required
- SmolDocling warmup takes 2-5 minutes on first run
- Ollama requires manual model pull: `docker compose exec ollama ollama pull nomic-embed-text`

---

### 4. Observability Profile (Full Monitoring)

**Purpose**: Complete observability stack for production-like monitoring

**Services Included**:
- All from `dev` profile
- `alertmanager` (alert management)
- `cadvisor` (container metrics)
- `node-exporter` (host metrics)
- `hyperdx` (unified logs/traces) - optional with compose.hyperdx.yml

**Resource Usage**:
- **RAM**: ~8 GB
- **CPU**: 4 cores
- **Disk**: ~10 GB
- **Startup Time**: ~1.5 minutes

**Start Command**:
```bash
cd infra

# Without HyperDX
docker compose --profile observability up -d

# With HyperDX (unified observability)
docker compose -f docker-compose.yml -f compose.hyperdx.yml --profile observability up -d
```

**When to Use**:
- Performance testing
- Debugging production issues locally
- Alert rule development
- Infrastructure monitoring
- Capacity planning

**Additional Endpoints**:
- Alertmanager: http://localhost:9093
- cAdvisor: http://localhost:8082
- Node Exporter: http://localhost:9100/metrics
- HyperDX (if enabled): http://localhost:8180

**Monitoring Capabilities**:
- Container resource usage (CPU, RAM, network, disk)
- Host system metrics (CPU, RAM, disk I/O)
- API request metrics (rate, latency, errors)
- Database metrics (connections, queries, cache hits)
- Alert rules for thresholds (high memory, disk space, errors)

---

### 5. Automation Profile (Workflow Stack)

**Purpose**: n8n workflow automation for data pipelines and integrations

**Services Included**:
- All from `minimal` profile
- `n8n` (workflow automation)

**Resource Usage**:
- **RAM**: ~5 GB
- **CPU**: 2 cores
- **Disk**: ~6 GB
- **Startup Time**: ~45 seconds

**Start Command**:
```bash
cd infra
docker compose --profile automation up -d
```

**When to Use**:
- Developing n8n workflows
- BGG data synchronization automation
- Email notification workflows
- Scheduled report generation
- Webhook integrations

**Additional Endpoints**:
- n8n: http://localhost:5678 (admin/n8nadmin)

**Common Workflows**:
- BGG game data sync (hourly/daily)
- Email notifications on events
- Webhook triggers for external integrations
- Scheduled database backups
- Data transformation pipelines

---

### 6. Full Profile (Complete Stack)

**Purpose**: Everything enabled for comprehensive development and testing

**Services Included**:
- All services from all profiles
- All 17+ services running simultaneously

**Resource Usage**:
- **RAM**: ~18 GB (24 GB recommended)
- **CPU**: 8 cores (12 cores recommended)
- **Disk**: ~35 GB
- **Startup Time**: ~3 minutes

**Start Command**:
```bash
cd infra
docker compose --profile full up -d
```

**When to Use**:
- Integration testing across all components
- Production simulation
- Demonstrating full platform capabilities
- Load testing
- Training/onboarding new developers

**All Endpoints**: See [service-endpoints.md](./service-endpoints.md)

**⚠️ Requirements**:
- Minimum 24 GB system RAM
- 8+ CPU cores
- 40 GB free disk space
- Good internet connection (for model downloads)

---

## Profile Combinations

### Multiple Profiles

You can activate multiple profiles simultaneously:

```bash
# AI + Observability
docker compose --profile ai --profile observability up -d

# Dev + Automation
docker compose --profile dev --profile automation up -d

# All profiles (equivalent to 'full')
docker compose --profile minimal --profile dev --profile ai --profile observability --profile automation up -d
```

### Custom Profile Selection

**Example**: Core + AI services without Ollama

```yaml
# Create custom compose.override.yml
services:
  ollama:
    profiles:
      - disabled  # Disable Ollama
```

```bash
# Start AI profile (Ollama won't start)
docker compose --profile ai up -d
```

---

## Profile Switching

### Switch Between Profiles

```bash
# Stop current services
docker compose down

# Start with different profile
docker compose --profile <new-profile> up -d

# Example: minimal → ai
docker compose down
docker compose --profile ai up -d
```

### Add Services to Running Stack

```bash
# Start minimal
docker compose --profile minimal up -d

# Add monitoring (without restarting existing services)
docker compose --profile observability up -d

# Now running: minimal + observability services
```

### Remove Services from Running Stack

```bash
# Stop specific services
docker compose stop grafana prometheus mailpit

# Or stop and remove
docker compose rm -sf grafana prometheus mailpit
```

---

## Profile Performance Comparison

### Startup Time Breakdown

| Profile | Pull Time (1st run) | Start Time | Total (1st run) |
|---------|---------------------|------------|-----------------|
| **minimal** | ~2 min | ~30s | ~2.5 min |
| **dev** | ~3 min | ~1 min | ~4 min |
| **ai** | ~8 min | ~3 min | ~11 min |
| **observability** | ~4 min | ~1.5 min | ~5.5 min |
| **automation** | ~2.5 min | ~45s | ~3.5 min |
| **full** | ~10 min | ~3 min | ~13 min |

**Note**: Pull time only applies to first run. Subsequent runs use cached images.

### Memory Usage Over Time

| Profile | Idle | Light Load | Heavy Load | Peak |
|---------|------|------------|------------|------|
| **minimal** | 3 GB | 4 GB | 6 GB | 8 GB |
| **dev** | 5 GB | 6 GB | 8 GB | 10 GB |
| **ai** | 10 GB | 12 GB | 16 GB | 20 GB |
| **observability** | 6 GB | 8 GB | 10 GB | 12 GB |
| **automation** | 4 GB | 5 GB | 6 GB | 8 GB |
| **full** | 15 GB | 18 GB | 24 GB | 30 GB |

---

## Optimizing Profile Usage

### For Limited Resources (8 GB RAM)

```bash
# Best: Minimal + selective services
docker compose --profile minimal up -d

# Add only what you need
docker compose up -d mailpit  # If testing emails
docker compose up -d grafana prometheus  # If debugging
```

### For Medium Resources (16 GB RAM)

```bash
# Recommended: dev or ai profile
docker compose --profile dev up -d
# Or
docker compose --profile ai up -d

# Can combine:
docker compose --profile dev --profile automation up -d
```

### For High Resources (32 GB+ RAM)

```bash
# Use full profile freely
docker compose --profile full up -d

# Or all profiles explicitly
docker compose --profile minimal --profile dev --profile ai --profile observability --profile automation up -d
```

---

## Profile Use Cases

### Scenario 1: Frontend Developer

**Needs**: Web UI, API, Database

```bash
# Minimal profile is perfect
docker compose --profile minimal up -d

# Access:
# - Web: http://localhost:3000
# - API: http://localhost:8080
```

### Scenario 2: Backend Developer

**Needs**: API, Database, Cache, Monitoring

```bash
# Dev profile includes monitoring
docker compose --profile dev up -d

# Access:
# - API: http://localhost:8080
# - Grafana: http://localhost:3001
# - Prometheus: http://localhost:9090
```

### Scenario 3: AI/ML Developer

**Needs**: Full AI stack

```bash
# AI profile with all ML services
docker compose --profile ai up -d

# Access:
# - Embeddings: http://localhost:8000
# - Reranker: http://localhost:8003
# - PDF Processing: http://localhost:8001, :8002
# - Ollama: http://localhost:11434
```

### Scenario 4: DevOps/SRE

**Needs**: Full monitoring, metrics, logs

```bash
# Observability with HyperDX
docker compose -f docker-compose.yml -f compose.hyperdx.yml --profile observability up -d

# Access:
# - Grafana: http://localhost:3001
# - Prometheus: http://localhost:9090
# - HyperDX: http://localhost:8180
# - Alertmanager: http://localhost:9093
```

### Scenario 5: Integration Tester

**Needs**: Everything for end-to-end tests

```bash
# Full stack
docker compose --profile full up -d

# Run E2E tests
cd ../apps/web
pnpm test:e2e
```

---

## Profile-Specific Configuration

### Environment Variables Per Profile

**Create `.env.<profile>` files**:

```bash
# .env.dev
LOG_LEVEL=Debug
ENABLE_PROFILING=true
CACHE_ENABLED=true

# .env.ai
EMBEDDING_BATCH_SIZE=32
OLLAMA_MAX_MODELS=3
ENABLE_GPU=false

# .env.observability
PROMETHEUS_RETENTION=30d
GRAFANA_ALLOW_EMBEDDING=true
```

**Load profile-specific env**:
```bash
docker compose --env-file .env.dev --profile dev up -d
```

---

## Troubleshooting Profiles

### Profile Not Starting All Expected Services

```bash
# Check which services belong to profile
docker compose config --profiles

# See which services will start
docker compose --profile <profile> config --services

# Verify service profile assignment in docker-compose.yml
grep -A 2 "profiles:" infra/docker-compose.yml
```

### Wrong Services Starting

```bash
# Some services might be in multiple profiles
# Check docker-compose.yml for profile assignments

services:
  api:
    profiles: [minimal, dev, ai, observability, automation, full]
  grafana:
    profiles: [dev, observability, full]
```

### Profile Performance Issues

```bash
# Check resource usage
docker stats

# If near limits, switch to lighter profile
docker compose down
docker compose --profile minimal up -d
```

---

## Additional Resources

- **Quick Start**: [quick-start.md](./quick-start.md)
- **Service Endpoints**: [service-endpoints.md](./service-endpoints.md)
- **Clean Builds**: [clean-builds.md](./clean-builds.md)
- **Common Commands**: [common-commands.md](./common-commands.md)
- **Troubleshooting**: [troubleshooting.md](./troubleshooting.md)
- **Advanced Features**: [advanced-features.md](./advanced-features.md)

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team
