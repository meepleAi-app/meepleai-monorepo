# Deployment Quick Reference

> **Scope**: Fast reference guide for common deployment tasks and emergency procedures
> **Last Updated**: 2026-01-30

---

## 🚀 Deploy New Version

### Staging

```bash
# Merge to staging branch
git checkout main-staging
git merge main-dev
git push origin main-staging

# Auto-deploys to: https://staging.meepleai.com
# Monitor: https://github.com/owner/repo/actions
```

### Production

```bash
# 1. Create release tag
git checkout main
git tag -a v1.2.3 -m "Release v1.2.3: Feature description"
git push origin v1.2.3

# 2. Approve deployment
# GitHub → Actions → Deploy to Production → Review → Approve

# 3. Monitor
# https://grafana.meepleai.io
```

---

## 🐳 Docker Commands

### Images

```bash
# Build local
docker build -t api:dev -f apps/api/src/Api/Dockerfile apps/api

# Pull from registry
docker pull ghcr.io/owner/repo/api:v1.2.3

# List images
docker images | grep meepleai

# Remove unused
docker image prune -a -f
```

### Containers

```bash
# Start services
cd infra && docker compose up -d

# Stop services
docker compose stop

# Restart service
docker compose restart api

# View logs
docker compose logs -f api

# Container shell
docker compose exec api /bin/bash
```

### Volumes

```bash
# List volumes
docker volume ls | grep meepleai

# Inspect volume
docker volume inspect meepleai_postgres_data

# Backup volume
docker run --rm \
  -v meepleai_postgres_data:/source:ro \
  -v /backups:/backup \
  alpine tar czf /backup/data.tar.gz -C /source .

# Remove volume (DESTRUCTIVE!)
docker volume rm meepleai_postgres_data
```

---

## 💾 Backup & Restore

### Database Backup

```bash
# PostgreSQL
docker compose exec postgres pg_dump -U postgres meepleai | gzip > backup-$(date +%Y%m%d).sql.gz

# Restore
gunzip -c backup.sql.gz | docker compose exec -T postgres psql -U postgres meepleai
```

### Volume Backup

```bash
# PDF uploads
docker run --rm \
  -v meepleai_pdf_uploads:/source:ro \
  -v /backups:/backup \
  alpine tar czf /backup/pdf-$(date +%Y%m%d).tar.gz -C /source .

# Qdrant
docker run --rm \
  -v meepleai_qdrant_data:/source:ro \
  -v /backups:/backup \
  alpine tar czf /backup/qdrant-$(date +%Y%m%d).tar.gz -C /source .
```

### Complete Backup

```bash
# Run automated script
/opt/meepleai/scripts/backup-all.sh

# Verify backup
ls -lh /backups/$(date +%Y%m%d-*)/
```

---

## 🔄 Rollback

### Quick Rollback

```bash
# Re-deploy previous version
git push origin v1.2.2  # Previous working tag

# OR manual
ssh user@server
docker pull ghcr.io/owner/repo/api:v1.2.2
export API_IMAGE="ghcr.io/owner/repo/api:v1.2.2"
docker compose up -d --no-deps api
```

### Database Rollback

```bash
# Stop API
docker compose stop api

# Restore backup
gunzip -c /backups/pre-deploy-20260130.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai

# Start API
docker compose start api
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

# Database
docker compose exec postgres pg_isready -U postgres

# Redis
docker compose exec redis redis-cli ping
```

### Detailed Check

```bash
# Container status
docker compose ps --format json | jq

# Resource usage
docker stats --no-stream

# Disk space
df -h /var/lib/docker/volumes
```

---

## 🛠️ Troubleshooting

### Service Not Starting

```bash
# Check logs
docker compose logs --tail 50 api

# Inspect container
docker inspect meepleai-api | jq '.[0].State'

# Restart
docker compose restart api
```

### Connection Issues

```bash
# Test database
docker compose exec postgres psql -U postgres meepleai -c "SELECT 1"

# Test Redis
docker compose exec redis redis-cli -a $REDIS_PASSWORD ping

# Test Qdrant
curl http://localhost:6333/health
```

### Performance Issues

```bash
# Check resource usage
docker stats

# Check disk I/O
iostat -x 1

# Check network
netstat -an | grep ESTABLISHED | wc -l
```

---

## 🔒 Secret Management

### List Secrets

```bash
cd infra/secrets
ls -la *.secret
```

### Generate New Secrets

```bash
# Auto-generate all
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# Manual specific secret
openssl rand -base64 32 > jwt.secret
```

### Rotate Secrets

```bash
# 1. Generate new secret
openssl rand -base64 32 > new-jwt.secret

# 2. Update secret file
cp new-jwt.secret infra/secrets/jwt.secret

# 3. Restart service
docker compose restart api

# 4. Verify
curl https://api.meepleai.io/health
```

---

## 📊 Monitoring

### Grafana

```bash
# URL: https://grafana.meepleai.io
# User: admin
# Pass: cat infra/secrets/monitoring.secret | grep GRAFANA_ADMIN_PASSWORD
```

### Prometheus

```bash
# Metrics endpoint
curl http://localhost:9090/metrics

# Query API
curl 'http://localhost:9090/api/v1/query?query=up'
```

### Logs

```bash
# Real-time
docker compose logs -f api web

# Search errors
docker compose logs --since 1h api | grep -i error

# Export logs
docker compose logs --no-color > logs-$(date +%Y%m%d).txt
```

---

## 🚨 Emergency Procedures

### Service Down

```bash
# 1. Check status
docker compose ps

# 2. Check logs
docker compose logs --tail 100 api

# 3. Restart
docker compose restart api

# 4. If still down, rollback
docker pull ghcr.io/owner/repo/api:v1.2.2
docker compose up -d --no-deps api
```

### Database Emergency

```bash
# Connection pool exhausted
docker compose restart api

# Corruption detected
docker compose stop api
docker compose exec postgres pg_resetwal /var/lib/postgresql/data

# Complete failure
# Restore from backup (see Backup & Restore)
```

### Disk Full

```bash
# Check usage
df -h

# Clean Docker
docker system prune -a -f --volumes

# Clean old backups
find /backups -mtime +30 -delete

# Emergency: Expand disk or migrate data
```

---

## 📋 Checklists

### Pre-Deploy Checklist

- [ ] Tests passing in CI
- [ ] Staging deployed and tested (24+ hours)
- [ ] CHANGELOG.md updated
- [ ] Database migrations tested
- [ ] Rollback plan documented
- [ ] Team notified
- [ ] Monitoring dashboards ready
- [ ] Backup completed

### Post-Deploy Checklist

- [ ] Health checks pass
- [ ] Smoke tests pass
- [ ] Grafana shows normal metrics
- [ ] No error spike in logs
- [ ] Response times acceptable
- [ ] User reports checked
- [ ] GitHub release created
- [ ] Team notified of completion

### Emergency Rollback Checklist

- [ ] Identify root cause
- [ ] Notify team immediately
- [ ] Pull previous image version
- [ ] Stop current containers
- [ ] Restore database if needed
- [ ] Deploy previous version
- [ ] Verify health checks
- [ ] Monitor for stability
- [ ] Document incident
- [ ] Post-mortem scheduled

---

## 🔗 Related Documentation

### Deployment Guides
- [Docker Versioning Guide](./docker-versioning-guide.md)
- [Deployment Workflows Guide](./deployment-workflows-guide.md)
- [Docker Volume Management](./docker-volume-management.md)
- [Production Deployment Guide](./production-deployment-meepleai.md)

### Infrastructure
- [Infrastructure Deployment Checklist](./infrastructure-deployment-checklist.md)
- [Monitoring Setup Guide](./monitoring-setup-guide.md)
- [Secrets Management](./secrets-management.md)

### Operations
- [Disaster Recovery Runbook](./runbooks/disaster-recovery.md)
- [Backup & Restore Runbook](./runbooks/backup-restore.md)
- [Troubleshooting Runbook](./runbooks/troubleshooting.md)

---

## 📞 Support Contacts

| Issue Type | Contact | Response Time |
|------------|---------|---------------|
| P0 - Outage | DevOps on-call | 15 min |
| P1 - Critical | DevOps team | 1 hour |
| P2 - High | Support team | 4 hours |
| P3 - Normal | Email support | 24 hours |

---

**Last Updated**: 2026-01-30
