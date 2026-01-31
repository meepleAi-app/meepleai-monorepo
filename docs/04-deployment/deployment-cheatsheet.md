# MeepleAI Deployment Cheat Sheet

> **One-page quick reference for deployment, Docker, and emergency procedures**

---

## 🚀 Deployment Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│ FEATURE → STAGING → PRODUCTION                                      │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  1️⃣ DEVELOP                                                         │
│     git checkout -b feature/issue-123-game-sessions                 │
│     # ... code ...                                                  │
│     git commit -m "feat(game): add session management"             │
│     git push → PR → merge to main-dev                              │
│                                                                      │
│  2️⃣ STAGING                                                         │
│     git checkout main-staging                                       │
│     git merge main-dev                                              │
│     git push origin main-staging                                    │
│     → Auto-deploy: staging-20260130-a1b2c3d                        │
│     → Test 24+ hours                                               │
│                                                                      │
│  3️⃣ PRODUCTION                                                      │
│     git checkout main                                               │
│     git tag -a v1.2.3 -m "Release v1.2.3"                          │
│     git push origin v1.2.3                                         │
│     → Approve in GitHub Actions                                    │
│     → Auto-deploy: v1.2.3 (blue-green)                            │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 🐳 Docker Essentials

### Images

```bash
# Build
docker build -t api:dev -f apps/api/src/Api/Dockerfile apps/api

# Pull from registry
docker pull ghcr.io/owner/meepleai-monorepo/api:v1.2.3

# Tag
docker tag api:dev ghcr.io/owner/repo/api:v1.2.3

# Push
docker push ghcr.io/owner/repo/api:v1.2.3

# List
docker images | grep meepleai

# Remove
docker image prune -a -f
```

### Containers

```bash
# Start
docker compose up -d

# Stop
docker compose stop

# Restart
docker compose restart api

# Logs
docker compose logs -f api web

# Shell
docker compose exec api /bin/bash

# Stats
docker stats --no-stream
```

### Volumes

```bash
# List
docker volume ls | grep meepleai

# Inspect
docker volume inspect meepleai_postgres_data

# Backup
docker run --rm -v meepleai_postgres_data:/src:ro -v /backups:/dst alpine tar czf /dst/backup.tar.gz -C /src .

# Restore
docker run --rm -v meepleai_postgres_data:/dst -v /backups:/src alpine tar xzf /src/backup.tar.gz -C /dst

# Remove
docker volume rm meepleai_postgres_data

# Prune
docker volume prune -f
```

---

## 💾 Backup Commands

### PostgreSQL

```bash
# Backup
docker compose exec postgres pg_dump -U postgres meepleai | gzip > backup-$(date +%Y%m%d).sql.gz

# Restore
gunzip -c backup.sql.gz | docker compose exec -T postgres psql -U postgres meepleai

# All databases
docker compose exec postgres pg_dumpall -U postgres > backup-all.sql
```

### Qdrant

```bash
# Volume backup
docker run --rm \
  -v meepleai_qdrant_data:/src:ro \
  -v /backups:/dst \
  alpine tar czf /dst/qdrant-$(date +%Y%m%d).tar.gz -C /src .
```

### PDF Uploads

```bash
# Volume backup
docker run --rm \
  -v meepleai_pdf_uploads:/src:ro \
  -v /backups:/dst \
  alpine tar czf /dst/pdf-$(date +%Y%m%d).tar.gz -C /src .
```

### Complete Backup

```bash
# Automated script
/opt/meepleai/scripts/backup-all.sh

# Manual all-in-one
docker compose exec postgres pg_dump -U postgres meepleai | gzip > /backups/postgres.sql.gz && \
docker run --rm -v meepleai_qdrant_data:/src:ro -v /backups:/dst alpine tar czf /dst/qdrant.tar.gz -C /src . && \
docker run --rm -v meepleai_pdf_uploads:/src:ro -v /backups:/dst alpine tar czf /dst/pdf.tar.gz -C /src .
```

---

## 🔄 Rollback Procedures

### Quick Rollback (Latest Version)

```bash
# Re-deploy previous tag
git push origin v1.2.2

# OR manual
docker pull ghcr.io/owner/repo/api:v1.2.2
export API_IMAGE="ghcr.io/owner/repo/api:v1.2.2"
docker compose up -d --no-deps api
```

### With Database Restore

```bash
# 1. Stop API
docker compose stop api

# 2. Restore database
gunzip -c /backups/pre-deploy-20260130.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai

# 3. Deploy previous version
docker pull ghcr.io/owner/repo/api:v1.2.2
docker compose up -d api

# 4. Verify
curl https://api.meepleai.io/health
```

---

## 🔍 Health Checks

### Quick Status

```bash
# All services
docker compose ps

# Health endpoints
curl https://www.meepleai.io/health
curl https://api.meepleai.io/health

# One-liner
curl -sf https://api.meepleai.io/health && echo "✅" || echo "❌"
```

### Detailed Health

```bash
# PostgreSQL
docker compose exec postgres pg_isready -U postgres

# Redis
docker compose exec redis redis-cli -a $REDIS_PASSWORD ping

# Qdrant
curl http://localhost:6333/health

# All at once
services=(api web postgres redis qdrant)
for svc in "${services[@]}"; do
  echo "$svc: $(docker compose exec $svc echo "UP" 2>/dev/null || echo "DOWN")"
done
```

---

## 🛠️ Troubleshooting

### Service Down

```bash
# Check logs
docker compose logs --tail 100 api

# Restart
docker compose restart api

# Full restart
docker compose down && docker compose up -d
```

### Database Issues

```bash
# Connection test
docker compose exec postgres psql -U postgres meepleai -c "SELECT 1"

# Active connections
docker compose exec postgres psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"

# Kill idle connections
docker compose exec postgres psql -U postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE state = 'idle' AND state_change < NOW() - INTERVAL '5 minutes';"
```

### Memory Issues

```bash
# Check usage
docker stats --no-stream

# Restart high-memory service
docker compose restart api

# Emergency: Increase limits in docker-compose.yml
```

### Disk Full

```bash
# Check space
df -h
docker system df

# Clean up
docker system prune -a -f --volumes

# Emergency: Delete old backups
find /backups -mtime +7 -delete
```

---

## 🔑 Secret Commands

### Generate Secrets

```bash
# JWT secret (64 chars)
openssl rand -base64 48

# Password (32 chars)
openssl rand -base64 32

# API key format
echo "sk-$(openssl rand -hex 32)"
```

### Rotate Secrets

```bash
# 1. Generate new
openssl rand -base64 32 > infra/secrets/jwt.secret

# 2. Restart API
docker compose restart api

# 3. Verify
curl https://api.meepleai.io/health
```

---

## 📊 Monitoring

### Grafana

```bash
# URL
open https://grafana.meepleai.io

# Credentials
cat infra/secrets/monitoring.secret | grep GRAFANA_ADMIN_PASSWORD
```

### Metrics

```bash
# Prometheus metrics
curl http://localhost:9090/metrics

# API metrics
curl http://localhost:8080/metrics

# Query
curl 'http://localhost:9090/api/v1/query?query=http_requests_total'
```

### Logs

```bash
# Real-time
docker compose logs -f api

# Errors only
docker compose logs --since 1h api | grep -i error

# Export
docker compose logs --no-color api > api-logs-$(date +%Y%m%d).txt
```

---

## 🔒 Security

### Secrets Check

```bash
# Verify all required secrets exist
required=(database.secret redis.secret jwt.secret admin.secret)
for secret in "${required[@]}"; do
  [ -f "infra/secrets/$secret" ] && echo "✅ $secret" || echo "❌ $secret MISSING"
done
```

### Firewall

```bash
# Check UFW status
sudo ufw status

# Allow required ports
sudo ufw allow 22/tcp   # SSH
sudo ufw allow 80/tcp   # HTTP
sudo ufw allow 443/tcp  # HTTPS
```

### SSL Certificate

```bash
# Check expiry
echo | openssl s_client -servername www.meepleai.io -connect www.meepleai.io:443 2>/dev/null | openssl x509 -noout -dates

# Force renewal (Traefik)
docker restart meepleai-traefik
```

---

## 🎯 Performance

### Database

```bash
# Vacuum
docker compose exec postgres vacuumdb -U postgres meepleai --analyze

# Slow queries
docker compose exec postgres psql -U postgres -c "SELECT * FROM pg_stat_statements ORDER BY total_exec_time DESC LIMIT 10;"
```

### Cache

```bash
# Redis info
docker compose exec redis redis-cli -a $REDIS_PASSWORD INFO stats

# Clear cache
docker compose exec redis redis-cli -a $REDIS_PASSWORD FLUSHALL
```

### Qdrant

```bash
# Collection info
curl http://localhost:6333/collections/documents

# Optimize
curl -X POST http://localhost:6333/collections/documents/optimize
```

---

## 📈 Scaling

### Horizontal (More Instances)

```bash
# Scale API to 3 instances
docker compose up -d --scale api=3

# Verify
docker compose ps api
```

### Vertical (More Resources)

```yaml
# Edit docker-compose.yml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 4G
```

```bash
# Apply
docker compose up -d --no-deps api
```

---

## 🚨 Emergency Commands

### Complete System Restart

```bash
docker compose down && docker compose up -d
```

### Force Rebuild

```bash
docker compose down
docker compose build --no-cache
docker compose up -d
```

### Nuclear Option (DESTRUCTIVE!)

```bash
# Stop everything
docker compose down -v

# Remove all MeepleAI resources
docker system prune -a -f --volumes

# Restore from backup
# (see Docker Volume Management guide)
```

---

## 📊 Version Matrix

| Environment | Branch | Tag Format | URL | Deploy Trigger |
|-------------|--------|------------|-----|----------------|
| **Dev** | `feature/*` | `build` | `localhost:3000` | Manual |
| **Staging** | `main-staging` | `staging-YYYYMMDD-SHA` | `staging.meepleai.com` | Push to branch |
| **Prod** | `main` | `v*.*.*` | `www.meepleai.io` | Git tag + approval |

---

## 🔗 Quick Links

| Service | URL | Credentials |
|---------|-----|-------------|
| **Production** | https://www.meepleai.io | User accounts |
| **API** | https://api.meepleai.io | API keys |
| **Grafana** | https://grafana.meepleai.io | `admin` / secret file |
| **Traefik** | https://traefik.meepleai.io | `admin` / bcrypt hash |
| **API Docs** | https://api.meepleai.io/scalar/v1 | Public |

---

## 📖 Documentation Map

| Topic | Document | Read Time |
|-------|----------|-----------|
| **Image Versioning** | [Docker Versioning Guide](./docker-versioning-guide.md) | 15 min |
| **Deployment Pipeline** | [Deployment Workflows Guide](./deployment-workflows-guide.md) | 20 min |
| **Volume Management** | [Docker Volume Management](./docker-volume-management.md) | 25 min |
| **Quick Reference** | [Deployment Quick Reference](./deployment-quick-reference.md) | 5 min |
| **Production Setup** | [Production Deployment Guide](./production-deployment-meepleai.md) | 30 min |
| **Cost Planning** | [Infrastructure Cost Summary](./infrastructure-cost-summary.md) | 20 min |

---

## 💡 Pro Tips

### Faster Deploys
```bash
# Use GitHub Actions cache
cache-from: type=gha
cache-to: type=gha,mode=max
```

### Zero Downtime
```bash
# Blue-green pattern
docker compose up -d --scale api=2  # Start new
sleep 30 && curl http://new:8080/health  # Verify
docker compose up -d --scale api=1  # Remove old
```

### Resource Monitoring
```bash
# Watch resources in real-time
watch -n 2 'docker stats --no-stream'

# Alert on high usage
docker stats --format "{{.Name}}: {{.CPUPerc}} {{.MemPerc}}" | awk '$2 > 80 {print "⚠️ " $0}'
```

### Log Debugging
```bash
# Tail with timestamp
docker compose logs -f --timestamps api

# Filter errors
docker compose logs --since 1h api 2>&1 | grep -E "ERROR|FATAL|Exception"

# Count errors
docker compose logs --since 1h api 2>&1 | grep -c ERROR
```

---

**Last Updated**: 2026-01-30
