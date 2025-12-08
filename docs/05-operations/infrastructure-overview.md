# MeepleAI Infrastructure Overview

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-08
**Status**: Production Ready
**Location**: Consolidated from `infra/README.md` + `infra/INFRASTRUCTURE.md`

---

## Table of Contents

1. [Overview](#overview)
2. [Docker Compose Profiles](#docker-compose-profiles)
3. [Services Overview](#services-overview)
4. [Quick Start Guide](#quick-start-guide)
5. [Directory Structure](#directory-structure)
6. [Common Operations](#common-operations)
7. [Monitoring & Observability](#monitoring--observability)
8. [Security & Secrets](#security--secrets)
9. [Troubleshooting](#troubleshooting)
10. [Related Documentation](#related-documentation)

---

## Overview

**Infrastructure as Code** - Docker Compose-based infrastructure with monitoring, secrets management, and full observability stack for MeepleAI.

### Key Components

- **17 Docker Services**: Core (postgres, redis, qdrant), AI/ML (ollama, embedding, unstructured, smoldocling, reranker), Observability (prometheus, grafana, alertmanager, cadvisor, node-exporter), Workflow (n8n), App (api, web)
- **5 Docker Profiles**: minimal, dev, observability, ai, automation, full
- **Reverse Proxy**: Traefik v3.2 (optional, dev-first) - See [Traefik Guide](deployment/traefik-guide.md)
- **13 Grafana Dashboards**: API performance, errors, cache, AI quality, RAG operations, infrastructure, 2FA security, ingestion, LLM cost, RAG evaluation, quality gauges, HTTP retry, infrastructure monitoring
- **31 Prometheus Alert Rules**: API, database, cache, PDF processing, quality metrics (9 categories)
- **14 n8n Workflow Templates**: Automation, backup, monitoring

---

## Docker Compose Profiles

**Issue #702** - Flexible service composition for different use cases

### Profile Overview

| Profile | Services | Use Case | Start Time |
|---------|----------|----------|------------|
| **minimal** | postgres, redis, qdrant, api, web | Core only | ~30s |
| **dev** | minimal + prometheus, grafana | Development | ~45s |
| **observability** | dev + alertmanager, hyperdx | Full monitoring | ~60s |
| **ai** | ollama, embedding, unstructured, smoldocling | AI/ML only | ~90s |
| **automation** | n8n | Workflows only | ~15s |
| **full** | All 17 services | Complete stack | ~120s |

### Quick Start Commands

```bash
# Core services only (fastest)
docker compose up -d

# Development + monitoring
docker compose --profile dev up -d

# Full observability stack
docker compose --profile observability up -d

# AI/ML services
docker compose --profile ai up -d

# Workflow automation
docker compose --profile automation up -d

# Everything (default for backward compatibility)
docker compose --profile full up -d
```

### Startup Scripts (Issue #702)

Convenient scripts for common profiles:

```bash
# Minimal (core only)
./start-minimal.sh

# Development + basic monitoring
./start-dev.sh

# Full observability
./start-observability.sh

# AI/ML services
./start-ai.sh

# Automation (n8n)
./start-automation.sh
```

### Profile Combinations

```bash
# Core + AI + Monitoring
docker compose --profile dev --profile ai up -d

# Core + Workflows + Observability
docker compose --profile observability --profile automation up -d
```

---

## Services Overview

### Core Services (Profile: minimal)

| Service | Port | Description | Health Check |
|---------|------|-------------|--------------|
| **postgres** | 5432 | PostgreSQL 16 database | `pg_isready` |
| **qdrant** | 6333 | Vector database | `curl /healthz` |
| **redis** | 6379 | Cache & sessions | `redis-cli ping` |
| **api** | 8080 | ASP.NET Core API | `curl /health` |
| **web** | 3000 | Next.js frontend | HTTP 200 |

### AI/ML Services (Profile: ai)

| Service | Port | Description | Model/Version |
|---------|------|-------------|---------------|
| **ollama** | 11434 | Local LLM runtime | gemma2:2b |
| **embedding** | 8000 | Multilingual embeddings | BGE-M3 |
| **unstructured** | 8001 | PDF extraction (Stage 1) | Apache 2.0 |
| **smoldocling** | 8002 | VLM PDF extraction (Stage 2) | 256M params |

### Observability Services (Profile: observability)

| Service | Port | Description | URL |
|---------|------|-------------|-----|
| **hyperdx** | 8180 | Unified observability | http://localhost:8180 |
| **prometheus** | 9090 | Metrics collection | http://localhost:9090 |
| **alertmanager** | 9093 | Alert routing | http://localhost:9093 |
| **grafana** | 3001 | Dashboards | http://localhost:3001 |
| **cadvisor** | 8082 | Container metrics | http://localhost:8082 |
| **node-exporter** | 9100 | Host metrics | http://localhost:9100 |

### Workflow Automation (Profile: automation)

| Service | Port | Description | URL |
|---------|------|-------------|-----|
| **n8n** | 5678 | Workflow automation | http://localhost:5678 |

### Reverse Proxy (Optional, Issue #703)

| Service | Port | Description | Status |
|---------|------|-------------|--------|
| **traefik** | 80, 8080 | Reverse proxy + dashboard | Dev-first, optional |

See [Traefik Guide](deployment/traefik-guide.md) for setup and usage.

---

## Quick Start Guide

### Development (Recommended)

```bash
cd infra

# Option 1: Using startup script
./start-dev.sh

# Option 2: Using docker compose directly
docker compose --profile dev up -d

# Verify services
docker compose ps
curl http://localhost:8080/health
curl http://localhost:3000
```

### Production (Full Security)

```bash
cd infra

# 1. Initialize secrets first
./tools/secrets/init-secrets.sh

# 2. Start with production overrides
docker compose -f docker-compose.yml -f compose.prod.yml --profile full up -d

# 3. Verify deployment
docker compose ps
docker compose logs --tail=100
```

### Test/CI (Fast, In-Memory)

```bash
cd infra

# Fast CI/CD with tmpfs
docker compose -f docker-compose.yml -f compose.test.yml up -d

# With observability (if needed)
docker compose -f docker-compose.yml -f compose.test.yml --profile observability up -d
```

### Staging (Production-Like)

```bash
cd infra

# Ensure secrets configured
./tools/secrets/init-secrets.sh staging

# Start staging stack
docker compose -f docker-compose.yml -f compose.staging.yml --profile full up -d
```

---

## Directory Structure

```
infra/
├── docker-compose.yml              # Base configuration (production-ready)
├── docker-compose.dev.yml          # Development overrides (85 lines)
├── docker-compose.traefik.yml      # Traefik reverse proxy (Issue #703)
├── docker-compose.override.yml.example  # Local customization (Issue #707)
├── compose.test.yml                # Test/CI overrides (in-memory)
├── compose.staging.yml             # Staging overrides
├── compose.prod.yml                # Production overrides
│
├── start-minimal.sh                # Startup scripts (Issue #702)
├── start-dev.sh
├── start-observability.sh
├── start-ai.sh
├── start-automation.sh
│
├── env/                            # Environment variables per service
│   ├── README.md                   # Env files guide
│   ├── *.env.dev.example          # Development examples
│   ├── *.env.staging.example      # Staging examples
│   └── *.env.prod.example         # Production examples
│
├── secrets/                        # Docker secrets (gitignored)
│   ├── README.md                   # Secrets management guide
│   ├── dev/                        # Development secrets
│   ├── staging/                    # Staging secrets
│   └── prod/                       # Production secrets
│
├── dashboards/                     # Grafana dashboards (8 JSON files)
│   ├── README.md                   # Dashboard documentation
│   ├── api-performance.json
│   ├── error-monitoring.json
│   ├── cache-optimization.json
│   ├── ai-quality-monitoring.json
│   ├── ai-rag-operations.json
│   ├── infrastructure.json
│   ├── quality-metrics-gauges.json
│   └── http-retry-metrics.json
│
├── prometheus/                     # Prometheus configuration
│   ├── README.md                   # Prometheus setup guide
│   ├── prometheus.yml              # Main config
│   ├── prometheus-rules.yml        # Recording rules
│   └── alerts/                     # Alert rules (9 files)
│       ├── README.md
│       ├── api-performance.yml
│       ├── database-health.yml
│       ├── cache-performance.yml
│       ├── infrastructure.yml
│       ├── pdf-processing.yml
│       ├── quality-metrics.yml
│       ├── vector-search.yml
│       ├── prompt-management.yml
│       └── http-retry-alerts.yaml
│
├── n8n/                            # n8n workflow automation
│   ├── README.md                   # n8n setup guide
│   ├── templates/                  # Workflow templates (14 files)
│   └── workflows/                  # Production workflows
│
├── traefik/                        # Traefik reverse proxy (Issue #703)
│   ├── README.md                   # Traefik setup guide
│   ├── TESTING.md                  # Testing procedures
│   ├── PRODUCTION-CHECKLIST.md     # Production readiness
│   ├── traefik.yml                 # Static configuration
│   └── config/                     # Dynamic configuration
│
├── init/                           # Initialization scripts
│   ├── README.md
│   ├── postgres-init.sql
│   └── n8n/                        # n8n workflow init
│
├── scripts/                        # Infrastructure utility scripts
│   ├── README.md
│   ├── backup/
│   ├── monitoring/
│   └── maintenance/
│
└── experimental/                   # Experimental features
    └── docker-compose.infisical.yml  # POC: Infisical secrets
```

---

## Common Operations

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api
docker compose logs -f postgres

# Last 100 lines
docker compose logs --tail=100 api

# Since timestamp
docker compose logs --since 2025-12-08T10:00:00 api
```

### Restart Services

```bash
# All
docker compose restart

# Specific
docker compose restart api
docker compose restart postgres

# Rebuild and restart
docker compose up -d --build api
```

### Stop Stack

```bash
# Stop (keep volumes)
docker compose down

# Stop specific profile
docker compose --profile dev down

# Stop and remove volumes (⚠️ data loss)
docker compose down -v

# Stop and remove images
docker compose down --rmi all
```

### Rebuild Services

```bash
# Rebuild all
docker compose build --no-cache

# Rebuild specific
docker compose build --no-cache api

# Rebuild and restart
docker compose up -d --build api

# Pull latest images
docker compose pull
```

### Health Checks

```bash
# API health
curl http://localhost:8080/health

# Qdrant health
curl http://localhost:6333/healthz

# Redis health
docker compose exec redis redis-cli ping

# Postgres health
docker compose exec postgres pg_isready

# Prometheus targets
curl http://localhost:9090/api/v1/targets

# All services status
docker compose ps
```

### Resource Usage

```bash
# Real-time resource usage
docker stats

# Container inspection
docker compose top

# Disk usage
docker system df -v

# Clean unused resources
docker system prune -a --volumes
```

---

## Monitoring & Observability

### Unified Observability (HyperDX - Issue #1561)

**HyperDX** provides unified logs, traces, metrics, and session replay in single platform.

**Access**: http://localhost:8180

**Features**:
- Log aggregation (Serilog → OTLP)
- Distributed tracing (OpenTelemetry, W3C)
- Metrics (Prometheus → HyperDX)
- Session replay (browser)
- Correlation IDs across services

See: [ADR-015: HyperDX Observability](../../01-architecture/adr/adr-015-hyperdx-observability.md)

### Key Metrics

| Category | Metrics | Target |
|----------|---------|--------|
| **API Latency** | P50, P95, P99 | P95 < 500ms |
| **Error Rate** | 5xx errors | < 1% |
| **RAG Quality** | Confidence scores | ≥ 0.70 |
| **Database** | Query time, connections | P95 < 100ms |
| **Cache** | Redis hit rate | > 80% |
| **PDF Processing** | Success rate | > 95% |

### Grafana Dashboards

**Access**: http://localhost:3001 (admin/admin)

**Available Dashboards**:
1. **API Performance** - Request rates, latency, error rates
2. **Error Monitoring** - 5xx errors, stack traces, trends
3. **Cache Optimization** - Redis hit/miss rates, memory usage
4. **AI Quality Monitoring** - LLM confidence, hallucination detection
5. **RAG Operations** - Vector search, retrieval accuracy
6. **Infrastructure** - CPU, memory, disk, network
7. **Quality Metrics** - P@10, MRR, recall scores
8. **HTTP Retry Metrics** - Retry rates, backoff analysis

### Prometheus Alerts (40+ Rules)

**Access**: http://localhost:9090

**Alert Categories**:
- **API Performance** (`api-performance.yml`) - High latency, errors
- **Database Health** (`database-health.yml`) - PostgreSQL alerts
- **Cache Performance** (`cache-performance.yml`) - Redis monitoring
- **Vector Search** (`vector-search.yml`) - Qdrant alerts
- **Infrastructure** (`infrastructure.yml`) - Resource alerts (Issue #705)
- **Quality Metrics** (`quality-metrics.yml`) - AI quality
- **PDF Processing** (`pdf-processing.yml`) - PDF pipeline
- **Prompt Management** (`prompt-management.yml`) - LLM cost tracking

### Infrastructure Monitoring (Issue #705)

**New Components**:
- **cAdvisor**: Container-level metrics (CPU, memory, network, disk)
- **node-exporter**: Host-level metrics (system, hardware)

**Metrics Collected**:
- Container resource usage
- Host CPU, memory, disk I/O
- Network statistics
- Filesystem metrics

**Dashboards**: `infrastructure.json` in Grafana

**Runbook**: [Infrastructure Monitoring](../runbooks/infrastructure-monitoring.md)

---

## Security & Secrets

### Secret Management

#### Development
```bash
# Plain env vars (.env.dev)
POSTGRES_PASSWORD=meeplepass
GRAFANA_ADMIN_PASSWORD=admin
OPENROUTER_API_KEY=your_key_here
```

#### Staging/Production
```bash
# Docker secrets (./secrets/)
./tools/secrets/init-secrets.sh

# Verify secrets
ls -la ./secrets/prod/

# Required secrets:
- postgres-password.txt
- openrouter-api-key.txt
- n8n-encryption-key.txt
- n8n-basic-auth-password.txt
- gmail-app-password.txt
- grafana-admin-password.txt
- initial-admin-password.txt
```

See: [Secrets Management Guide](../../infra/secrets/README.md)

### Network Security

- **Isolated Docker Network**: All services in private network
- **Exposed Ports**: Only necessary ports published
- **TLS/SSL**: Via reverse proxy (production)
- **Traefik Integration**: Optional reverse proxy with automatic HTTPS (Issue #703)

### Access Control

**Development**:
- Grafana: `admin/admin`
- n8n: No auth (local only)

**Production** (MUST CHANGE):
- Grafana: Use secrets (`grafana-admin-password.txt`)
- n8n: Configure OAuth
- Seq: Configure API keys
- Traefik: Basic auth or OAuth

**See Also**: [OAuth Security](../../06-security/oauth-security.md)

---

## Troubleshooting

### Issue: Secrets Not Found

**Error**: `postgres-password secret not found`

**Fix**:
```bash
cd infra
./tools/secrets/init-secrets.sh
```

### Issue: Port Already in Use

**Error**: `port 5432 already allocated`

**Fix**:
```bash
# Find process
lsof -i :5432  # macOS/Linux
netstat -ano | findstr :5432  # Windows

# Stop system PostgreSQL (Linux)
sudo systemctl stop postgresql

# Stop Docker service
docker compose stop postgres
```

### Issue: Out of Disk Space

**Fix**:
```bash
# Check disk usage
docker system df -v

# Clean volumes
docker volume prune

# Clean images
docker image prune -a

# Clean everything (⚠️ destructive)
docker system prune -a --volumes
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

# Check resource limits
docker stats

# Check health status
docker compose ps
```

### Issue: Slow Performance

**Check**:
```bash
# Resource usage
docker stats

# Container logs for errors
docker compose logs --tail=100

# Database connections
docker compose exec postgres psql -U meepleai -c "SELECT count(*) FROM pg_stat_activity;"

# Redis memory
docker compose exec redis redis-cli INFO memory

# Qdrant status
curl http://localhost:6333/collections
```

### Issue: Traefik Not Routing

**Check**:
```bash
# Traefik dashboard
open http://traefik.localhost:8080

# Service labels
docker compose config | grep traefik

# Traefik logs
docker compose logs traefik
```

See: [Traefik Testing Guide](deployment/traefik-testing.md)

---

## Related Documentation

### Infrastructure & Deployment
- **[Traefik Reverse Proxy](deployment/traefik-guide.md)** - Setup, configuration, routing (Issue #703)
- **[Traefik Testing](deployment/traefik-testing.md)** - Testing procedures (Issue #703)
- **[Traefik Production](deployment/traefik-production.md)** - Production checklist (Issue #703)
- **[Deployment Guide](deployment/board-game-ai-deployment-guide.md)** - Production deployment
- **[Multi-Environment Strategy](deployment/multi-environment-strategy.md)** - Dev/Staging/Prod
- **[Disaster Recovery](deployment/disaster-recovery.md)** - DR procedures
- **[Docker Override](../../infra/docker-compose.override.yml.example)** - Local customization (Issue #707)

### Monitoring & Observability
- **[Prometheus Setup](monitoring/prometheus-setup.md)** - Configuration, alerts, queries
- **[Prometheus Queries](monitoring/prometheus-llm-queries.md)** - LLM cost tracking
- **[Grafana Dashboards](monitoring/grafana-llm-cost-dashboard.md)** - LLM cost dashboard
- **[HyperDX Integration](../../01-architecture/adr/adr-015-hyperdx-observability.md)** - ADR (Issue #1561)
- **[Slack Notifications](monitoring/slack-notifications.md)** - Alert routing

### Workflow Automation
- **[n8n Workflow Automation](workflow-automation.md)** - Setup, templates, workflows

### Runbooks
- **[Runbooks Index](../runbooks/README.md)** - All operational runbooks
- **[Infrastructure Monitoring](../runbooks/infrastructure-monitoring.md)** - Resource alerts (Issue #705)
- **[High Error Rate](../runbooks/high-error-rate.md)** - Error spike response
- **[Slow Performance](../runbooks/slow-performance.md)** - Performance degradation
- **[RAG Evaluation Pipeline](../runbooks/rag-evaluation-pipeline.md)** - RAG testing

### Backup & Recovery
- **[Backup Strategy](../backup/backup-strategy.md)** - Automated backups (Issue #704)
- **[Restore Procedures](../backup/restore-procedures.md)** - Recovery steps (Issue #704)

### Component-Specific
- **[Grafana Dashboards](../../infra/dashboards/README.md)** - Dashboard catalog
- **[Environment Configuration](../../infra/env/README.md)** - Env vars guide
- **[Initialization Scripts](../../infra/init/README.md)** - Init procedures
- **[Utility Scripts](../../infra/scripts/README.md)** - Maintenance scripts
- **[Secrets Management](../../infra/secrets/README.md)** - Secrets guide

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated `infra/README.md` + `infra/INFRASTRUCTURE.md` into single overview
- ✅ Added Docker Compose Profiles documentation (Issue #702)
- ✅ Added Traefik integration references (Issue #703)
- ✅ Added Docker Override customization (Issue #707)
- ✅ Added infrastructure monitoring (cAdvisor, node-exporter) - Issue #705
- ✅ Updated all cross-references to new docs structure
- ✅ Moved to `docs/05-operations/infrastructure-overview.md`

### 2025-11-22: Infrastructure Refactor

**Changes**:
- ✅ Reduced `docker-compose.dev.yml` from 647 to 85 lines (87% reduction)
- ✅ Fixed service naming in `compose.{test,staging,prod}.yml`
- ✅ Consolidated Grafana dashboards into single `/dashboards` directory
- ✅ Consolidated Prometheus alerts into single `/prometheus/alerts` directory
- ✅ Moved `docker-compose.infisical.yml` to `experimental/`
- ✅ Archived outdated documentation

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-08
**Maintainer**: Infrastructure Team
**Total Services**: 17 (with infrastructure monitoring)
**Profiles**: 6 (minimal, dev, observability, ai, automation, full)
