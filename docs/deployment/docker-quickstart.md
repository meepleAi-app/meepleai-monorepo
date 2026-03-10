# Docker Services - Quick Start Guide

**Last Updated**: 2026-02-12

## Quick Reference

| Environment | Profile | Services | RAM | Command |
|------------|---------|----------|-----|---------|
| **Backend Dev** | `minimal` | 6 | 7GB | `COMPOSE_PROFILES=minimal docker compose up -d` |
| **Full Dev** | `dev` | 12 | 15GB | `COMPOSE_PROFILES=dev docker compose up -d` |
| **AI/ML** | `ai` | 11 | 25GB | `COMPOSE_PROFILES=ai docker compose up -d` |
| **Automation** | `automation` | 8 | 10GB | `COMPOSE_PROFILES=automation docker compose up -d` |
| **Monitoring** | `observability` | 13 | 18GB | `COMPOSE_PROFILES=observability docker compose up -d` |
| **Production** | `full` | 21 | 41GB | `COMPOSE_PROFILES=full docker compose up -d` |

**Total Production**: 21 services, ~41GB RAM, ~27 CPUs

## Setup Checklist

```bash
# 1. Generate secrets (15-30min saved with auto-gen)
cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated

# 2. Validate configuration
docker compose config --quiet && echo "✅ Valid"

# 3. Start services (choose profile from table above)
COMPOSE_PROFILES=minimal docker compose up -d

# 4. Check health
docker compose ps
docker compose logs -f api web
```

## Service Endpoints

| Service | URL | Credentials |
|---------|-----|-------------|
| **API** | http://localhost:8080 | See `infra/secrets/admin.secret` |
| **Web** | http://localhost:3000 | N/A |
| **Grafana** | http://localhost:3001 | `admin` / `monitoring.secret` |
| **Prometheus** | http://localhost:9090 | None |
| **HyperDX** | http://localhost:8180 | API key: `demo` |
| **Qdrant** | http://localhost:6333 | None (localhost-only) |
| **Mailpit** | http://localhost:8025 | None |
| **n8n** | http://localhost:5678 | Setup on first visit |
| **Traefik** | http://localhost:8080 | None (dev mode) |

## Common Commands

```bash
# Status and logs
docker compose ps                    # Service status
docker compose logs -f api web       # Follow logs (API + Web)
docker compose logs --tail=50 api    # Last 50 lines
docker stats                         # Resource usage

# Service control
docker compose restart api           # Restart service
docker compose stop                  # Stop all
docker compose down                  # Stop + remove containers
docker compose down -v               # ⚠️ Stop + delete volumes (data loss!)

# Health checks
docker compose exec api curl http://localhost:8080/health
docker compose exec postgres pg_isready -U meepleai_user
docker compose exec redis redis-cli -a $REDIS_PASSWORD ping

# Cleanup
docker system prune -a              # Remove unused containers/images
docker volume ls | grep meepleai    # List volumes
docker network inspect meepleai     # Inspect network
```

## Troubleshooting Quick Fixes

| Symptom | Quick Fix |
|---------|-----------|
| **Secret validation error** | `cd infra/secrets && pwsh setup-secrets.ps1` |
| **Port conflict (8080)** | `netstat -ano \| findstr :8080` → `taskkill /PID <PID> /F` |
| **DB auth failed** | Check `infra/secrets/database.secret`, restart postgres |
| **OOM (exit 137)** | Increase memory limits in `docker-compose.yml` |
| **Service unreachable** | `docker network inspect meepleai`, verify network |
| **Lost data on restart** | Check `docker volume ls`, restore from backup |
| **High memory** | `docker stats`, prune: `docker system prune -a` |

## Profile Decision Tree

```
Working on:
├─ Backend API only? → minimal (6 services, 7GB)
├─ Frontend + API? → dev (12 services, 15GB)
├─ AI/RAG features? → ai (11 services, 25GB)
├─ Workflow automation? → automation (8 services, 10GB)
├─ Infrastructure monitoring? → observability (13 services, 18GB)
└─ Complete system? → full (21 services, 41GB)
```

## Critical Secrets (blocks startup)

| File | Contents | Purpose |
|------|----------|---------|
| `database.secret` | POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB | PostgreSQL auth |
| `redis.secret` | REDIS_PASSWORD | Redis auth |
| `jwt.secret` | JWT_SECRET_KEY, JWT_ISSUER, JWT_AUDIENCE | API auth |
| `admin.secret` | DEFAULT_ADMIN_EMAIL, DEFAULT_ADMIN_PASSWORD | Initial admin |
| `embedding-service.secret` | EMBEDDING_API_KEY | AI service auth |

**Setup**: `cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated`

## Resource Requirements

| Profile | Min RAM | Recommended RAM | Min CPU | Notes |
|---------|---------|----------------|---------|-------|
| minimal | 8GB | 16GB | 4 | Core dev only |
| dev | 16GB | 24GB | 8 | + Monitoring |
| ai | 32GB | 64GB | 12 | GPU recommended |
| full | 48GB | 128GB | 16 | + GPU for production |

## Production Checklist

**Security**:
- [ ] Enable Docker Socket Proxy: `COMPOSE_PROFILES=prod docker compose up -d`
- [ ] Rotate secrets every 90 days
- [ ] Configure TLS/HTTPS in Traefik
- [ ] Bind services to internal network (not `0.0.0.0`)
- [ ] Enable authentication for Grafana, n8n, HyperDX
- [ ] Restrict file permissions: `chmod 600 infra/secrets/*.secret`

**Monitoring**:
- [ ] Configure Alertmanager email/Slack
- [ ] Set up Prometheus retention (30 days)
- [ ] Enable HyperDX distributed tracing
- [ ] Create Grafana dashboards (auto-provisioned)

**Backup**:
- [ ] Daily PostgreSQL backups (`pg_dump`)
- [ ] Qdrant snapshot exports
- [ ] Redis AOF persistence
- [ ] Volume backups: `docker volume backup`
- [ ] Test restore monthly

**Scaling**:
- [ ] Enable API HybridCache
- [ ] Configure PgBouncer for connection pooling
- [ ] Add GPU for embedding/SmolDocling services
- [ ] Implement Redis Sentinel for HA
- [ ] Set up read replicas for PostgreSQL

## Quick Diagnostics

```bash
# Service health
docker compose ps | grep -v "Up (healthy)"

# Resource pressure
docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}"

# Network connectivity
docker compose exec api ping qdrant
docker compose exec api curl http://embedding-service:8000/health

# Log errors
docker compose logs --since 10m | grep -i "error\|fatal\|exception"

# Volume usage
docker system df -v

# Container restarts
docker ps -a --format "table {{.Names}}\t{{.Status}}\t{{.RestartCount}}"
```

## Service Dependencies

```
Core Stack (always required):
  postgres → (nothing)
  qdrant → (nothing)
  redis → (nothing)
  api → postgres, qdrant, redis
  web → api
  traefik → (nothing)

AI Stack (optional):
  embedding-service → (nothing)
  reranker-service → (nothing)
  unstructured-service → (nothing)
  smoldocling-service → (nothing)
  ollama → (nothing)
  ollama-pull → ollama

Monitoring Stack (optional):
  prometheus → (scrapes all)
  grafana → prometheus
  alertmanager → prometheus
  hyperdx → (nothing)
  cadvisor → (nothing)
  node-exporter → (nothing)
  mailpit → (nothing)

Automation Stack (optional):
  n8n → postgres
```

## Advanced Usage

```bash
# Multi-profile (custom combination)
COMPOSE_PROFILES=minimal,observability docker compose up -d

# With Traefik reverse proxy
docker compose -f docker-compose.yml -f compose.traefik.yml up -d

# With HyperDX unified observability
docker compose -f docker-compose.yml -f compose.hyperdx.yml up -d

# Force rebuild (after code changes)
docker compose build --no-cache api web
docker compose up -d api web

# View specific service config
docker compose config --services
docker compose config | grep -A 20 "api:"

# Export logs for analysis
docker compose logs --no-color > docker-logs-$(date +%Y%m%d).txt

# Remove specific volume
docker compose down
docker volume rm meepleai_postgres_data
docker compose up -d postgres
```

## Reference

- **Full Service Details**: `docs/04-deployment/docker-services.md`
- **Troubleshooting Guide**: `docs/04-deployment/docker-troubleshooting.md`
- **Docker Compose Docs**: https://docs.docker.com/compose/
- **MeepleAI Architecture**: `docs/01-architecture/system-overview.md`
