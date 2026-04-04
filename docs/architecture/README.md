# Infrastructure Documentation

**Docker Compose + Traefik + Monitoring**

---

## Quick Start

**Prerequisites**:
- Docker 24+
- Docker Compose 2.20+

**Setup**:
```bash
# 1. Generate secrets
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# 2. Start infrastructure
cd ../
docker compose up -d postgres qdrant redis

# 3. Verify services
docker compose ps
```

---

## Architecture

### Service Stack

**Core Services**:
- PostgreSQL 16 (database)
- Redis 7 (cache + sessions)
- Qdrant (vector search)

**Application**:
- API (.NET 9)
- Web (Next.js 14)

**AI/ML Services**:
- embedding-service (Python)
- reranker-service (Python)
- unstructured-service (PDF processing)
- smoldocling-service (OCR)

**Infrastructure**:
- Traefik (reverse proxy)
- Prometheus (metrics)
- Grafana (dashboards)

---

## Configuration

### Docker Compose Profiles

**Minimal** (core only):
```bash
docker compose --profile minimal up -d
# Services: postgres, redis, qdrant, api, web
```

**Dev** (with monitoring):
```bash
docker compose --profile dev up -d
# Services: minimal + prometheus, grafana
```

**Full** (all services):
```bash
docker compose --profile full up -d
# Services: everything including AI/ML, n8n, mailpit
```

### Environment Variables

**Location**: `infra/secrets/*.secret`

**Critical Secrets** (required):
- `database.secret` - PostgreSQL credentials
- `redis.secret` - Redis password
- `qdrant.secret` - Qdrant API key
- `jwt.secret` - JWT signing key
- `admin.secret` - Admin credentials
- `embedding-service.secret` - Embedding model

**Important Secrets** (warnings):
- `openrouter.secret` - LLM API key
- `unstructured-service.secret` - PDF processing
- `bgg.secret` - BoardGameGeek API

**Optional Secrets**:
- `oauth.secret` - OAuth providers
- `email.secret` - Email service
- `monitoring.secret` - Grafana admin

---

## Service Management

### Common Commands

```bash
# Start services
docker compose up -d

# View logs
docker compose logs -f
docker compose logs -f api

# Restart service
docker compose restart api

# Stop all services
docker compose down

# Stop and remove volumes (⚠️ DATA LOSS)
docker compose down -v

# Check service health
docker compose ps
```

### Health Checks

**Endpoints**:
- API: http://localhost:8080/health
- Grafana: http://localhost:3001
- Traefik: http://localhost:8090

**Check Script**:
```bash
pwsh -c "Invoke-WebRequest -Uri http://localhost:8080/health | Select-Object StatusCode,Content"
```

---

## Networking

### Traefik Reverse Proxy

**Configuration**: `infra/traefik/traefik.yml`

**Routes**:
```yaml
# API
api.localhost → localhost:8080

# Frontend
localhost → localhost:3000

# Grafana
grafana.localhost → localhost:3001
```

**Dashboard**: http://localhost:8090

### Service Discovery

Traefik auto-discovers services via Docker labels:

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.api.rule=Host(`api.localhost`)"
  - "traefik.http.services.api.loadbalancer.server.port=8080"
```

---

## Monitoring

### Prometheus

**URL**: http://localhost:9090
**Config**: `infra/monitoring/prometheus/prometheus.yml`

**Metrics**:
- Application metrics (custom)
- Container metrics (cAdvisor)
- PostgreSQL metrics (postgres_exporter)

### Grafana

**URL**: http://localhost:3001
**Credentials**: `infra/secrets/monitoring.secret`

**Dashboards**:
- Application Overview
- Database Performance
- Infrastructure Health

**Import Dashboards**:
```bash
# From infra/monitoring/grafana/dashboards/
docker cp dashboard.json meepleai-grafana:/etc/grafana/provisioning/dashboards/
```

---

## Database Management

### PostgreSQL

**Connection**:
```bash
# Via Docker
docker exec -it meepleai-postgres psql -U admin -d meepleai

# Via connection string
psql "postgresql://admin:password@localhost:5432/meepleai"
```

**Backup**:
```bash
# Backup database
docker exec meepleai-postgres pg_dump -U admin meepleai > backup.sql

# Restore database
cat backup.sql | docker exec -i meepleai-postgres psql -U admin meepleai
```

### Redis

**Connection**:
```bash
# Via Docker
docker exec -it meepleai-redis redis-cli -a password

# Commands
PING          # Check connection
KEYS *        # List keys (⚠️ use in dev only)
DBSIZE        # Count keys
FLUSHDB       # Clear database (⚠️ DATA LOSS)
```

### Qdrant

**Connection**:
- Dashboard: http://localhost:6333/dashboard
- API: http://localhost:6333

**Operations**:
```bash
# Via curl
curl http://localhost:6333/collections

# Check health
curl http://localhost:6333/healthz
```

---

## Volume Management

### Persistent Volumes

**Data Volumes**:
```yaml
volumes:
  postgres_data:       # PostgreSQL data
  redis_data:          # Redis persistence
  qdrant_data:         # Qdrant vectors
  grafana_data:        # Grafana dashboards
```

**Backup Volumes**:
```bash
# Backup volume
docker run --rm \
  -v meepleai_postgres_data:/data \
  -v $(pwd):/backup \
  busybox tar czf /backup/postgres-backup.tar.gz /data

# Restore volume
docker run --rm \
  -v meepleai_postgres_data:/data \
  -v $(pwd):/backup \
  busybox tar xzf /backup/postgres-backup.tar.gz -C /
```

---

## Troubleshooting

### Common Issues

**Service won't start**:
```bash
# Check logs
docker compose logs service-name

# Check secrets
ls infra/secrets/*.secret

# Regenerate secrets if needed
cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated
```

**Port conflicts**:
```bash
# Find process using port
netstat -ano | findstr :8080

# Kill process
taskkill /PID <PID> /F
```

**Volume issues**:
```bash
# Remove volumes and restart (⚠️ DATA LOSS)
docker compose down -v
docker compose up -d
```

---

## Production Deployment

### Security Hardening

**Steps**:
1. Rotate all secrets
2. Enable HTTPS (Let's Encrypt)
3. Configure firewall rules
4. Enable fail2ban
5. Set up log aggregation
6. Configure backup automation

**Guide**: [Infrastructure Deployment Checklist](../deployment/infrastructure-deployment-checklist.md)

### Scaling

**Horizontal Scaling**:
```yaml
# docker-compose.yml
services:
  api:
    deploy:
      replicas: 3
```

**Vertical Scaling**:
```yaml
# Resource limits
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
```

**Guide**: [Scaling Guide](../deployment/scaling-guide.md)

---

## Related Documentation

- [Deployment Guide](../deployment/README.md)
- [Secrets Management](../deployment/secrets-management.md)
- [Monitoring Setup](../deployment/monitoring-setup-guide.md)
- [Runbooks](../deployment/runbooks/README.md)

---

**Last Updated**: 2026-01-31
**Maintainer**: DevOps Team
