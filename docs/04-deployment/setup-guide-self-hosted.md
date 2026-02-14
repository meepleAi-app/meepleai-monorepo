# MeepleAI Self-Hosted Setup Guide

**Target**: Staging + Production Beta (5-20 users)
**Budget**: €20-25/mo | **Region**: EU (Hetzner)

---

## Architecture Overview

```
                    Cloudflare (DNS + CDN + SSL) €0
                              ↓
         ┌────────────────────┼────────────────────┐
    meepleai.com      api.meepleai.com      staging.*
         └────────────────────┼────────────────────┘
                              ↓
                    Hetzner VPS CPX31 €15.59
                    8 vCPU | 16GB | 160GB NVMe
                              ↓
                    Traefik (Reverse Proxy + SSL)
                              ↓
      ┌──────┬──────┬─────────┼─────────┬──────┬──────┐
   .NET   Python  PostgreSQL  Redis  Qdrant  Next.js
    API  Services      16              Vector   (opt)
         Embed/
         Rerank
```

---

## Quick Start (Day 1-3)

### Day 1: Accounts & Provisioning

| Step | Service | Action | Cost |
|------|---------|--------|------|
| 1 | Porkbun | Register meepleai.com | €10/yr |
| 2 | Cloudflare | Add site, get nameservers | €0 |
| 3 | Porkbun | Update nameservers | - |
| 4 | Hetzner | Create CPX31 VPS (Ubuntu 24.04) | €15.59/mo |
| 5 | SSH | `ssh root@VPS_IP` | - |

### Day 2: Server Setup

```bash
# Update & install essentials
apt update && apt upgrade -y
apt install -y git curl wget nano htop ufw fail2ban ca-certificates gnupg

# Timezone & firewall
timedatectl set-timezone Europe/Rome
ufw default deny incoming && ufw default allow outgoing
ufw allow 22/tcp && ufw allow 80/tcp && ufw allow 443/tcp
ufw --force enable

# Create non-root user
adduser meepleai && usermod -aG sudo meepleai
mkdir -p /home/meepleai/.ssh
cp ~/.ssh/authorized_keys /home/meepleai/.ssh/
chown -R meepleai:meepleai /home/meepleai/.ssh
chmod 700 /home/meepleai/.ssh && chmod 600 /home/meepleai/.ssh/authorized_keys

# Disable root login
sed -i 's/PermitRootLogin yes/PermitRootLogin no/' /etc/ssh/sshd_config
sed -i 's/#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
systemctl restart sshd

# Install Docker
curl -fsSL https://get.docker.com | sh
usermod -aG docker meepleai
apt install -y docker-compose-plugin
systemctl enable docker && systemctl start docker

# Reconnect as meepleai user
exit && ssh meepleai@VPS_IP

# Test Docker
docker run hello-world

# Create directory structure
mkdir -p /home/meepleai/{app,backups/{postgres,redis,qdrant},scripts,logs}
chmod 750 /home/meepleai/backups
```

---

## Resource Allocation (16GB RAM)

| Service | RAM Min | RAM Max | Notes |
|---------|---------|---------|-------|
| PostgreSQL | 512MB | 2GB | Shared buffers 512MB |
| Redis | 128MB | 512MB | Maxmemory 256MB |
| Qdrant | 512MB | 2GB | ~50K embeddings |
| .NET API | 512MB | 2GB | GC optimized |
| Embedding | 1GB | 3GB | sentence-transformers |
| Reranker | 512MB | 2GB | cross-encoder |
| Traefik | 64MB | 256MB | Reverse proxy |
| Next.js | 256MB | 1GB | If self-hosted |
| Ubuntu | 512MB | 1GB | OS + buffer |
| **Buffer** | 2GB | 4GB | Peak handling |
| **TOTAL** | ~6GB | ~16GB | ✅ Fits CPX31 |

### Storage Allocation (160GB NVMe)

| Usage | Estimate | Notes |
|-------|----------|-------|
| Ubuntu | 5GB | Base OS |
| Docker images | 15GB | All containers |
| PostgreSQL | 5-20GB | User data |
| Qdrant vectors | 1-5GB | ~200 manuals |
| Redis | 100MB | Cache |
| PDFs | 10-50GB | Uploads |
| Backups | 20GB | 7-day retention |
| Logs | 5GB | With rotation |
| **Free** | 50-100GB | Growth margin |

---

## DNS Configuration (Cloudflare)

| Type | Name | Content | Proxy | Purpose |
|------|------|---------|-------|---------|
| A | `@` | VPS_IP | ☁️ Proxied | Frontend |
| A | `www` | VPS_IP | ☁️ Proxied | WWW redirect |
| A | `api` | VPS_IP | 🔘 DNS only | API (WebSocket) |
| A | `staging` | VPS_IP | ☁️ Proxied | Staging frontend |
| A | `api-staging` | VPS_IP | 🔘 DNS only | Staging API |

**SSL/TLS Settings**: Full (strict), Always HTTPS, TLS 1.2+, TLS 1.3 enabled

**Verify**: `dig @8.8.8.8 meepleai.com +short` → Should show VPS_IP

---

## Secrets Generation

```bash
cd /home/meepleai/app/infra/secrets

# Password generator
generate_password() { openssl rand -base64 32 | tr -d '/+=' | head -c 32; }

# database.secret
cat > database.secret << EOF
POSTGRES_HOST=postgres
POSTGRES_PORT=5432
POSTGRES_DB=meepleai
POSTGRES_USER=meepleai
POSTGRES_PASSWORD=$(generate_password)
EOF

# redis.secret
cat > redis.secret << EOF
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=$(generate_password)
EOF

# qdrant.secret
cat > qdrant.secret << EOF
QDRANT_HOST=qdrant
QDRANT_PORT=6333
QDRANT_API_KEY=$(generate_password)
EOF

# jwt.secret
cat > jwt.secret << EOF
JWT_SECRET_KEY=$(openssl rand -base64 64 | tr -d '\n')
JWT_ISSUER=meepleai.com
JWT_AUDIENCE=meepleai-users
JWT_EXPIRY_HOURS=24
JWT_REFRESH_EXPIRY_DAYS=7
EOF

# admin.secret
cat > admin.secret << EOF
ADMIN_EMAIL=admin@meepleai.com
ADMIN_PASSWORD=$(generate_password)
EOF

# embedding-service.secret
cat > embedding-service.secret << EOF
EMBEDDING_MODEL=sentence-transformers/all-MiniLM-L6-v2
EMBEDDING_DEVICE=cpu
EOF

# Protect & backup
chmod 600 *.secret
cat *.secret > /home/meepleai/backups/secrets-backup-$(date +%Y%m%d).txt
chmod 600 /home/meepleai/backups/secrets-backup-*.txt
```

**Save backup in password manager!**

---

## Docker Compose (Production)

**File**: `/home/meepleai/app/infra/docker-compose.prod.yml`

### Key Services Configuration

**Traefik** (Reverse Proxy):
- Ports: 80 (redirect) → 443 (HTTPS)
- Let's Encrypt: TLS challenge
- Dashboard: Disabled (security)

**PostgreSQL**:
- Image: postgres:16-alpine
- Resources: 512MB-2GB
- Health: `pg_isready` (10s interval, 5 retries)
- Volumes: postgres_data

**Redis**:
- Image: redis:7-alpine
- Maxmemory: 256MB (LRU eviction)
- Health: `redis-cli ping`
- AOF persistence: enabled

**Qdrant**:
- Image: qdrant/qdrant:v1.12.0
- Resources: 512MB-2GB
- Health: `curl http://localhost:6333/health`

**.NET API**:
- Traefik labels: `api.meepleai.com` → :8080
- Depends on: postgres, redis, qdrant (healthy)
- Health: `/health` endpoint
- Resources: 512MB-2GB

**Python Services** (Embedding/Reranker):
- Embedding: :8000 (1-3GB RAM)
- Reranker: :8001 (512MB-2GB)
- Models: sentence-transformers, cross-encoder

### Deployment Flow

```bash
cd /home/meepleai/app
git clone https://github.com/YOUR_ORG/meepleai-monorepo-frontend.git .

cd infra
# Create .env from secrets
cat secrets/*.secret | grep '=' > .env
chmod 600 .env

# Build & start
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d postgres redis qdrant
sleep 30  # Wait for DB

docker compose -f docker-compose.prod.yml up -d embedding-service reranker-service
sleep 60  # Wait for ML models

docker compose -f docker-compose.prod.yml up -d api traefik

# Verify
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs --tail=50
```

---

## Health Checks & Verification

### Service Tests

```bash
# PostgreSQL
docker exec meepleai-postgres psql -U meepleai -d meepleai -c "SELECT version();"

# Redis
docker exec meepleai-redis redis-cli -a PASSWORD ping  # → PONG

# Qdrant
curl http://localhost:6333/health  # → {"status":"ok"}

# API
curl http://localhost:8080/health  # → {"status":"healthy"}
curl https://api.meepleai.com/health  # Public

# SSL
curl -I https://api.meepleai.com/health  # → HTTP/2 200

# Embedding/Reranker
curl http://localhost:8000/health && curl http://localhost:8001/health
```

### Resource Monitoring

```bash
# RAM usage
free -h

# CPU
htop

# Disk
df -h

# Container stats
docker stats --no-stream
```

---

## Automated Backups

### Backup Script

**File**: `/home/meepleai/scripts/backup-all.sh`

**Features**:
- PostgreSQL: `pg_dumpall` → gzip
- Redis: `BGSAVE` → dump.rdb
- Qdrant: Snapshot API
- Retention: 7 days auto-cleanup
- Logging: `/home/meepleai/logs/backup.log`
- Disk alerts: >80% usage warning

**Cron Setup**:
```bash
# Create wrapper with env vars
cat > /home/meepleai/scripts/backup-env.sh << 'EOF'
#!/bin/bash
source /home/meepleai/app/infra/.env
export REDIS_PASSWORD QDRANT_API_KEY
/home/meepleai/scripts/backup-all.sh
EOF
chmod +x /home/meepleai/scripts/backup-env.sh

# Add to crontab
crontab -e
# Daily 3:00 AM
0 3 * * * /home/meepleai/scripts/backup-env.sh >> /home/meepleai/logs/backup-cron.log 2>&1
# Log cleanup (weekly)
0 4 * * 0 find /home/meepleai/logs -name "*.log" -mtime +30 -delete
```

### Restore Process

```bash
# PostgreSQL
gunzip -c /home/meepleai/backups/postgres/dump_YYYYMMDD.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai

# Redis
docker cp /home/meepleai/backups/redis/dump_YYYYMMDD.rdb meepleai-redis:/data/dump.rdb
docker restart meepleai-redis
```

---

## Staging Environment (Optional)

**File**: `docker-compose.staging.yml`

**Changes vs Production**:
- Separate database: `meepleai_staging`
- Reduced resources: 512MB-1GB
- Port :8081 (vs :8080)
- Subdomain: `api-staging.meepleai.com`

**Start/Stop**:
```bash
# Start staging
docker compose -f docker-compose.staging.yml up -d

# Verify
curl https://api-staging.meepleai.com/health

# Stop when not needed
docker compose -f docker-compose.staging.yml down
```

**Cost**: €5.40/mo (on-demand 50h) | €40/mo (always-on reduced) | €78.85/mo (full replica)

---

## Security Hardening

### Fail2Ban

```bash
sudo apt install -y fail2ban

sudo cat > /etc/fail2ban/jail.local << 'EOF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true
maxretry = 3
bantime = 86400
EOF

sudo systemctl enable fail2ban && sudo systemctl restart fail2ban
```

### Automatic Security Updates

```bash
sudo apt install -y unattended-upgrades
sudo dpkg-reconfigure -plow unattended-upgrades
# Select "Yes" for auto-updates
```

### Service Access Control

- Databases exposed: `127.0.0.1` only (not public)
- Firewall: UFW blocks all except 22/80/443
- SSH: Key-only, no password, no root

---

## Monitoring

### Health Check Script

**File**: `/home/meepleai/scripts/health-check.sh`

**Checks**:
- PostgreSQL: `pg_isready`
- Redis: `ping`
- Qdrant: `/health` endpoint
- API: `/health` endpoint
- Embedding/Reranker: `/health` endpoints

**Resources Reported**:
- RAM: Used/Total
- Disk: Used/Total (% used)
- Load average

**Cron**: Every 5 minutes → `/home/meepleai/logs/health.log`

```bash
*/5 * * * * /home/meepleai/scripts/health-check.sh >> /home/meepleai/logs/health.log 2>&1
```

---

## Runbook Quick Reference

### Common Commands

| Task | Command |
|------|---------|
| **SSH** | `ssh meepleai@VPS_IP` |
| **Status** | `docker compose -f docker-compose.prod.yml ps` |
| **Logs** | `docker compose -f docker-compose.prod.yml logs -f` |
| **Restart** | `docker compose -f docker-compose.prod.yml restart [service]` |
| **Update** | `git pull && docker compose -f docker-compose.prod.yml up -d --build` |
| **Backup** | `/home/meepleai/scripts/backup-all.sh` |
| **Health** | `/home/meepleai/scripts/health-check.sh` |
| **Stats** | `docker stats --no-stream` |

### Troubleshooting

| Issue | Solution |
|-------|----------|
| **Container won't start** | `docker compose logs SERVICE` + `docker inspect` |
| **Disk full** | `docker system prune -a` + delete old backups |
| **RAM exhausted** | `docker stats` → restart heavy service |
| **SSL not working** | Check Cloudflare SSL mode = Full (strict) |
| **API unreachable** | Check Traefik labels, verify DNS propagation |

---

## Cost Summary

| Item | Service | Monthly |
|------|---------|---------|
| VPS CPX31 | Hetzner | €15.59 |
| Backup snapshots | Hetzner | €3.08 |
| Domain | Porkbun | ~€0.83 |
| DNS/CDN/SSL | Cloudflare Free | €0 |
| Email | Resend Free | €0 |
| R2 Storage | Cloudflare (10GB free) | ~€0-5 |
| **TOTAL** | - | **€19.50-24.50** |

**One-time**: Domain €10

---

## Launch Checklist

### Pre-Launch
- [ ] Domain acquired & DNS configured
- [ ] VPS provisioned, Docker installed
- [ ] All containers running & healthy
- [ ] SSL valid (HTTPS works)
- [ ] Backup script tested
- [ ] Health check automated
- [ ] Security hardened (fail2ban, UFW, no root)
- [ ] Runbook documented

### Post-Launch (24h)
- [ ] Monitor logs: `docker compose logs -f`
- [ ] Verify nightly backup ran
- [ ] Check resources: `htop`, `df -h`
- [ ] Full functional test

### Weekly
- [ ] Verify backups work
- [ ] Review error logs
- [ ] Update Docker images
- [ ] Check disk space

---

**Created**: 2026-01-27 | **Budget**: €20-25/mo | **Approach**: Full self-hosted
