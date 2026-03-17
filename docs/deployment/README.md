# Deployment Guide

**MeepleAI Deployment** - Docker, Traefik, monitoring, production setup

---

## Quick Reference

| Environment | URL | Command |
|-------------|-----|---------|
| **Local** | http://localhost:3000 | `cd infra && docker compose --profile minimal up -d` |
| **Staging** | https://staging.meepleai.com | `docker compose -f docker-compose.yml -f compose.traefik.yml --profile full up -d` |
| **Production** | https://app.meepleai.com | Same as staging + `.env.production` |

**Related**: [GitHub Alternatives & Cost Optimization](github-alternatives-cost-optimization.md)

---

## Architecture

**Stack**: Traefik → Web (Next.js:3000) + API (.NET:8080) → Infrastructure (PostgreSQL:5432, Qdrant:6333, Redis:6379, n8n:5678, Grafana:3001, Prometheus:9090)

**Docker Profiles**: `minimal` (postgres+qdrant+redis), `dev` (+ grafana+prometheus), `observability` (+ alertmanager), `ai` (OpenRouter), `automation` (n8n), `full` (all)

---

## Local Development

### Quick Start

```bash
# 1. Setup
git clone <repo-url> && cd meepleai-monorepo
cp .env.example .env.local  # Edit: OPENROUTER_API_KEY, ADMIN credentials

# 2. Start Infrastructure
cd infra && docker compose --profile minimal up -d

# 3. Backend (Terminal 1)
cd apps/api/src/Api && dotnet run  # :8080

# 4. Frontend (Terminal 2)
cd apps/web && pnpm dev  # :3000
```

**Full Docker** (all services):
```bash
cd infra && docker compose --profile full up -d
# Access: Web :3000, API :8080, Grafana :3001, n8n :5678, Scalar :8080/scalar/v1
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

### .env.production Template

```bash
# API
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Postgres=Host=postgres;Database=meepleai_prod;Username=meepleai;Password=<pwd>
OPENROUTER_API_KEY=<key>
INITIAL_ADMIN_EMAIL=admin@meepleai.com
INITIAL_ADMIN_PASSWORD=<secure-pwd>

# Frontend
NEXT_PUBLIC_API_BASE=https://api.meepleai.com
NEXT_PUBLIC_ENV=production

# Security
CORS__AllowedOrigins__0=https://app.meepleai.com
JWT_SECRET=<64-char-random>
COOKIE_SECRET=<64-char-random>

# Traefik
TRAEFIK_ACME_EMAIL=admin@meepleai.com
DOMAIN_WEB=app.meepleai.com
DOMAIN_API=api.meepleai.com

# Monitoring
GRAFANA_ADMIN_PASSWORD=<secure-pwd>
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

**Health Check**: `curl http://localhost:8080/health/config` → `"database_configured": true, "database_source": "postgres_vars|connection_string"`

**HA Setup**: Primary-Replica (streaming replication) + PgBouncer (connection multiplexing) + HAProxy (load balancing) + Patroni (failover)

---

## Traefik & DNS

**DNS Records** (A → server-ip):
```
app.meepleai.com, api.meepleai.com, n8n.meepleai.com, grafana.meepleai.com
```

**SSL**: Automatic Let's Encrypt (Traefik auto-renews)
**Dashboard**: https://traefik.meepleai.com (Basic Auth: `htpasswd -nb admin pwd`)

---

## Backups

**Daily Automated** (cron 2 AM):
```bash
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
docker exec meepleai-postgres pg_dump -U meepleai meepleai_prod | gzip > /opt/meepleai/backups/postgres_$DATE.sql.gz
docker exec meepleai-qdrant tar -czf - /qdrant/storage > /opt/meepleai/backups/qdrant_$DATE.tar.gz
find /opt/meepleai/backups -name "*.gz" -mtime +7 -delete
```

**Restore**:
```bash
# PostgreSQL
docker compose stop api
gunzip -c backups/postgres_20260101.sql.gz | docker exec -i meepleai-postgres psql -U meepleai meepleai_prod
docker compose start api

# Qdrant
docker compose stop qdrant
tar -xzf backups/qdrant_20260101.tar.gz -C /var/lib/docker/volumes/qdrant_data/_data/
docker compose start qdrant
```

**Migrations**: Auto-applied on startup via `DbInitializer.cs`. Manual: `docker exec -it meepleai-api dotnet ef database update`

---

## Monitoring

| Service | Access | Credentials |
|---------|--------|-------------|
| **Grafana** | https://grafana.meepleai.com | `admin` / `<GRAFANA_ADMIN_PASSWORD>` |
| **Prometheus** | :9090 (internal) | Metrics at `/metrics` |

**Dashboards**: System (CPU/mem/disk), API (latency/throughput), RAG (confidence), DB (pool/queries)

**Prometheus Metrics**:
```
http_requests_total{endpoint, method, status}
http_request_duration_seconds{endpoint}
rag_query_confidence{game}
database_connections_active
redis_cache_hit_rate
```

**Alerts** (Alertmanager → Slack):
- API P95 >1s, Error rate >5%, DB pool exhausted, Disk >80%, RAG confidence <0.70

**Logs**:
```bash
docker compose logs -f api  # Real-time
docker compose logs --tail 100 api  # Last 100
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

## CI/CD & Maintenance

**Auto-Deploy** (push to `main`):
```yaml
# .github/workflows/deploy.yml
ssh-action: cd /opt/meepleai && git pull && docker compose --profile full down && up -d --build
```

**Manual**: `ssh user@meepleai.com` → `cd /opt/meepleai && git pull && docker compose --profile full up -d --build`

**Maintenance**:
```bash
# Monthly: Vacuum PostgreSQL
docker exec meepleai-postgres vacuumdb -U meepleai -d meepleai_prod --analyze

# Weekly: Optimize Qdrant
curl -X POST http://localhost:6333/collections/documents/optimize

# Cleanup: docker container/image/volume prune -f
```

---

## Troubleshooting

| Issue | Diagnostic | Fix |
|-------|------------|-----|
| API not responding | `docker compose logs api` | Check DB, restart: `docker compose restart api` |
| CORS errors | `CORS__AllowedOrigins` | Update `.env`, restart |
| SSL invalid | Traefik logs | Verify DNS, email in config |
| DB pool exhausted | Grafana metrics | Increase `MaxPoolSize` |
| High memory | `docker stats` | Adjust resource limits |

**Health Checks**:
```bash
curl https://api.meepleai.com/health  # API
docker exec meepleai-postgres pg_isready -U meepleai  # DB
curl http://localhost:6333/health  # Qdrant
docker exec meepleai-redis redis-cli ping  # Redis
```

---

## Disaster Recovery

**RTO**: <1h • **RPO**: <24h

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
- Server (8GB/4CPU): $40-80 • Storage (100GB): $10 • OpenRouter (10K): $20-50 • Backups (S3): $5

**Strategies**: CDN (Cloudflare free), aggressive caching, optimize embeddings, auto-scale down, spot instances

---

## Resources

**Operations**: [Operations Manual](../operations/operations-manual.md) — Consolidated service management, backup/restore, monitoring, incident response, disaster recovery, maintenance schedules.

**Guides**:
- [Docker Versioning](./docker-versioning-guide.md), [Deployment Workflows](./deployment-workflows-guide.md)
- [Cost Summary](./infrastructure-cost-summary.md), [Domain Setup](./domain-setup-guide.md), [Email/TOTP](./email-totp-services.md), [BGG API](./boardgamegeek-api-setup.md)

**Related**: [Monitoring](../02-development/README.md#monitoring), [Testing](../05-testing/README.md), [Security](../06-security/README.md)

---

**Version**: 1.1 • **Updated**: 2026-01-30 • **Maintainers**: DevOps Team
