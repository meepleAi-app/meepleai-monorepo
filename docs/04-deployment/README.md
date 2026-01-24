# Deployment Guide

**MeepleAI Deployment Documentation** - Production deployment guide with Docker, Traefik, monitoring

---

## Quick Reference

| Environment | URL | Purpose |
|-------------|-----|---------|
| **Local** | http://localhost:3000 | Development |
| **Staging** | https://staging.meepleai.com | Pre-production testing |
| **Production** | https://app.meepleai.com | Live production |

**📚 Related Guides**:
- **[GitHub Alternatives & Cost Optimization](github-alternatives-cost-optimization.md)** - Zero-cost CI/CD strategies, self-hosted runners, Oracle Always Free setup

---

## Architecture Overview

### Deployment Stack

```
Internet → Traefik (reverse proxy)
  ↓
  ├─ Web App (Next.js:3000)
  ├─ API (ASP.NET:8080)
  └─ Infrastructure:
      ├─ PostgreSQL:5432
      ├─ Qdrant:6333
      ├─ Redis:6379
      ├─ n8n:5678
      ├─ Grafana:3001
      └─ Prometheus:9090
```

### Docker Compose Profiles

**Available Profiles**:
```yaml
minimal:       postgres, qdrant, redis (core services only)
dev:           minimal + grafana, prometheus (development)
observability: dev + alertmanager (monitoring alerts)
ai:            OpenRouter integration (enabled by env)
automation:    n8n workflows
full:          All services
```

---

## Local Development

### Prerequisites

- Docker Desktop 24+
- Docker Compose 2.20+
- 4GB+ RAM available for containers
- Ports available: 3000, 8080, 5432, 6333, 6379

### Quick Start

**1. Clone Repository**
```bash
git clone <repo-url>
cd meepleai-monorepo
```

**2. Environment Configuration**
```bash
cp .env.example .env.local

# Edit .env.local with required values
OPENROUTER_API_KEY=<your-key>
INITIAL_ADMIN_EMAIL=admin@local.dev
INITIAL_ADMIN_PASSWORD=<secure-password>
```

**3. Start Core Services**
```bash
cd infra
docker compose --profile minimal up -d

# Verify services
docker compose ps
docker compose logs -f postgres
```

**4. Run Backend (Terminal 1)**
```bash
cd apps/api/src/Api
dotnet run
# API: http://localhost:8080
```

**5. Run Frontend (Terminal 2)**
```bash
cd apps/web
pnpm dev
# Web: http://localhost:3000
```

### Alternative: Full Docker Setup

**Run Everything in Docker**:
```bash
cd infra
docker compose --profile full up -d
```

**Access Services**:
- Web: http://localhost:3000
- API: http://localhost:8080
- Grafana: http://localhost:3001
- n8n: http://localhost:5678
- Scalar API Docs: http://localhost:8080/scalar/v1

---

## Production Deployment

### Prerequisites

- Linux server (Ubuntu 22.04+ recommended)
- Docker + Docker Compose installed
- Domain configured with DNS records
- SSL certificate (Let's Encrypt via Traefik)
- 8GB+ RAM, 4+ CPU cores
- 100GB+ SSD storage

### Server Setup

**1. Install Docker**
```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
sudo systemctl enable docker
sudo systemctl start docker
```

**2. Install Docker Compose**
```bash
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" \
  -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
```

**3. Clone Repository**
```bash
git clone <repo-url> /opt/meepleai
cd /opt/meepleai
```

**4. Production Environment**
```bash
# Create production .env
cat > .env.production <<EOF
# API Configuration
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=meepleai_prod;Username=meepleai;Password=<strong-password>
QDRANT_URL=http://qdrant:6333
REDIS_URL=redis:6379

# OpenRouter
OPENROUTER_API_KEY=<your-production-key>

# Admin Account
INITIAL_ADMIN_EMAIL=admin@meepleai.com
INITIAL_ADMIN_PASSWORD=<strong-password>

# Frontend
NEXT_PUBLIC_API_BASE=https://api.meepleai.com
NEXT_PUBLIC_ENV=production

# Security
CORS__AllowedOrigins__0=https://app.meepleai.com
JWT_SECRET=<64-char-random-string>
COOKIE_SECRET=<64-char-random-string>

# Traefik
TRAEFIK_ACME_EMAIL=admin@meepleai.com
DOMAIN_WEB=app.meepleai.com
DOMAIN_API=api.meepleai.com

# Monitoring
GRAFANA_ADMIN_PASSWORD=<strong-password>
PROMETHEUS_RETENTION=30d

# n8n
N8N_BASIC_AUTH_USER=admin
N8N_BASIC_AUTH_PASSWORD=<strong-password>
N8N_HOST=n8n.meepleai.com
EOF

# Secure .env file
chmod 600 .env.production
```

**5. Start with Traefik**
```bash
cd infra
docker compose \
  -f docker-compose.yml \
  -f docker-compose.traefik.yml \
  --profile full \
  --env-file ../.env.production \
  up -d
```

**6. Verify Deployment**
```bash
# Check all containers running
docker compose ps

# Check logs
docker compose logs -f api web

# Health check
curl https://api.meepleai.com/health
```

### Database Configuration (PostgreSQL)

**Issue #2460: Production-Grade Connection String**

MeepleAI supports two database connection patterns for production deployments:

#### Option 1: POSTGRES_* Environment Variables (Default)

The simplest approach using environment variables. SecretsHelper automatically constructs the connection string:

```bash
# .env.production or compose.prod.yml
POSTGRES_HOST=your-production-host.com
POSTGRES_PORT=5432
POSTGRES_DB=meepleai_prod
POSTGRES_USER=your-production-user
POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password

# Or direct password (not recommended for production)
POSTGRES_PASSWORD=your-secure-password
```

**Advantages**:
- Simple configuration
- Works with Docker Secrets
- Automatic connection string construction
- Health check recognizes configuration

**Health Check Output**:
```json
{
  "database_configured": true,
  "database_source": "postgres_vars"
}
```

#### Option 2: Full Connection String (Advanced)

For managed PostgreSQL services (AWS RDS, Azure Database, Google Cloud SQL) requiring SSL/TLS and connection pooling optimization:

```bash
# .env.production
ConnectionStrings__Postgres="Host=your-db.rds.amazonaws.com;Database=meepleai_prod;Username=admin;Password=SecurePass123!;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50;SSL Mode=Require;Trust Server Certificate=false;Command Timeout=30;Timeout=15"
```

**Connection String Parameters**:

| Parameter | Value | Description |
|-----------|-------|-------------|
| `Pooling` | `true` | Enable connection pooling |
| `Minimum Pool Size` | `5` | Pre-allocate 5 connections |
| `Maximum Pool Size` | `50` | Limit to 50 concurrent connections |
| `SSL Mode` | `Require` | Force SSL/TLS encryption |
| `Trust Server Certificate` | `false` | Validate SSL certificate (recommended) |
| `Command Timeout` | `30` | SQL command execution timeout (seconds) |
| `Timeout` | `15` | Connection establishment timeout (seconds) |

**Template File**: See `infra/secrets/postgres-connection-string-production.example.txt` for full template

**Health Check Output**:
```json
{
  "database_configured": true,
  "database_source": "connection_string"
}
```

#### SSL/TLS Configuration

**Managed PostgreSQL Services**:
- AWS RDS: SSL Mode=Require + Download RDS CA certificate
- Azure Database: SSL Mode=Require + Trust Server Certificate=false
- Google Cloud SQL: SSL Mode=Require + Cloud SQL Proxy recommended

**Self-Hosted PostgreSQL**:
```bash
# Enable SSL in postgresql.conf
ssl = on
ssl_cert_file = '/path/to/server.crt'
ssl_key_file = '/path/to/server.key'

# Require SSL for all connections in pg_hba.conf
hostssl all all 0.0.0.0/0 scram-sha-256
```

#### Connection Pooling Tuning

**Production Recommendations**:
- **Minimum Pool Size**: 5-10 (pre-allocate connections, reduce latency spikes)
- **Maximum Pool Size**: 50-100 (based on PostgreSQL `max_connections` setting)
- **Command Timeout**: 30 seconds (prevents long-running queries from blocking)
- **Timeout**: 15 seconds (connection establishment timeout)

**Monitoring**:
- Check Grafana dashboard: "PostgreSQL Connection Pool Usage"
- Monitor active connections: `SELECT count(*) FROM pg_stat_activity;`
- Adjust pool sizes based on actual traffic patterns

#### High Availability Setup

**Recommended Production Architecture**:
1. **Primary-Replica Setup**: PostgreSQL streaming replication
2. **Connection Pooling**: PgBouncer for connection multiplexing
3. **Load Balancing**: HAProxy or cloud load balancer
4. **Automatic Failover**: Patroni or cloud-native HA solutions

**Example with PgBouncer**:
```bash
# ConnectionString points to PgBouncer instead of PostgreSQL directly
ConnectionStrings__Postgres="Host=pgbouncer;Port=6432;Database=meepleai_prod;..."

# PgBouncer handles connection pooling and failover
```

#### Troubleshooting

**Health Check Shows `database_configured: false`**:
```bash
# Verify POSTGRES_PASSWORD is set
echo $POSTGRES_PASSWORD

# Or verify connection string
docker compose exec api printenv | grep -i postgres

# Check health check endpoint
curl http://localhost:8080/health/config
```

**SSL Connection Failures**:
```bash
# Test SSL connection manually
psql "sslmode=require host=your-db.com dbname=meepleai user=admin"

# Check server SSL certificate validity
openssl s_client -connect your-db.com:5432 -starttls postgres
```

**Connection Pool Exhausted**:
```bash
# Monitor active connections in Grafana or manually
docker compose exec postgres psql -U admin -d meepleai_prod -c \
  "SELECT count(*) as active_connections FROM pg_stat_activity WHERE state = 'active';"

# Increase Maximum Pool Size if consistently hitting limit
# Verify PostgreSQL max_connections supports higher pool size
```

---

## Traefik Configuration

### DNS Setup

**Required DNS Records** (pointing to server IP):
```
A     app.meepleai.com      → <server-ip>
A     api.meepleai.com      → <server-ip>
A     n8n.meepleai.com      → <server-ip>
A     grafana.meepleai.com  → <server-ip>
```

### SSL Certificates

**Automatic Let's Encrypt** via Traefik:
```yaml
# infra/traefik/traefik.yml
certificatesResolvers:
  letsencrypt:
    acme:
      email: admin@meepleai.com
      storage: /letsencrypt/acme.json
      httpChallenge:
        entryPoint: web
```

**Certificate Renewal**: Automatic (Traefik handles renewal)

### Traefik Dashboard

**Access**: https://traefik.meepleai.com

**Basic Auth** (configure in `docker-compose.traefik.yml`):
```bash
# Generate password hash
echo $(htpasswd -nb admin your-password)

# Add to docker-compose.traefik.yml labels
--entrypoints.websecure.http.middlewares=auth@docker
--middlewares.auth.basicauth.users=admin:$$apr1$$...
```

---

## Database Management

### Backup Strategy

**Automated Daily Backups**:
```bash
# Backup script (add to cron)
cat > /opt/meepleai/backup.sh <<'EOF'
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR=/opt/meepleai/backups
mkdir -p $BACKUP_DIR

# PostgreSQL backup
docker exec meepleai-postgres pg_dump -U meepleai meepleai_prod | \
  gzip > $BACKUP_DIR/postgres_$DATE.sql.gz

# Qdrant backup (vector data)
docker exec meepleai-qdrant tar -czf - /qdrant/storage > \
  $BACKUP_DIR/qdrant_$DATE.tar.gz

# Keep last 7 days
find $BACKUP_DIR -name "*.gz" -mtime +7 -delete

echo "Backup completed: $DATE"
EOF

chmod +x /opt/meepleai/backup.sh

# Add to crontab (daily at 2 AM)
crontab -e
0 2 * * * /opt/meepleai/backup.sh >> /var/log/meepleai-backup.log 2>&1
```

### Restore from Backup

**PostgreSQL Restore**:
```bash
# Stop API temporarily
docker compose stop api

# Restore database
gunzip -c backups/postgres_20260101_020000.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai meepleai_prod

# Restart API
docker compose start api
```

**Qdrant Restore**:
```bash
docker compose stop qdrant
docker exec meepleai-qdrant rm -rf /qdrant/storage
tar -xzf backups/qdrant_20260101_020000.tar.gz -C /var/lib/docker/volumes/qdrant_data/_data/
docker compose start qdrant
```

### Migrations

**Auto-Applied on Startup** via `DbInitializer.cs`

**Manual Migration** (if needed):
```bash
docker exec -it meepleai-api dotnet ef database update
```

---

## Monitoring & Observability

### Grafana Dashboards

**Access**: https://grafana.meepleai.com

**Default Dashboards**:
- System Overview (CPU, memory, disk)
- API Performance (request latency, throughput)
- RAG Quality (confidence scores, hallucination rate)
- Database Metrics (connection pool, query duration)

**Credentials**: `admin` / `<GRAFANA_ADMIN_PASSWORD>`

### Prometheus Metrics

**Endpoint**: https://api.meepleai.com/metrics

**Key Metrics**:
```
http_requests_total{endpoint, method, status}
http_request_duration_seconds{endpoint}
rag_query_confidence{game}
pdf_processing_duration_seconds{stage}
database_connections_active
redis_cache_hit_rate
```

### Logs (Serilog → HyperDX)

**View Logs**:
```bash
# Real-time logs
docker compose logs -f api

# Filter by service
docker compose logs -f postgres qdrant redis

# Last 100 lines
docker compose logs --tail 100 api
```

**HyperDX Integration** (optional):
```bash
# Add to .env.production
HYPERDX_API_KEY=<your-key>
HYPERDX_SERVICE_NAME=meepleai-api
```

### Alerts (Alertmanager)

**Configure Slack Notifications**:
```yaml
# infra/alertmanager/config.yml
route:
  receiver: 'slack'

receivers:
  - name: 'slack'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/...'
        channel: '#alerts'
        text: 'Alert: {{ .CommonAnnotations.summary }}'
```

**Alert Rules**:
- API response time P95 > 1s
- Error rate > 5%
- Database connection pool exhausted
- Disk usage > 80%
- RAG confidence < 0.70

---

## Scaling & Performance

### Horizontal Scaling

**API Instances** (behind Traefik load balancer):
```yaml
# docker-compose.prod.yml
services:
  api:
    deploy:
      replicas: 3  # 3 API instances
      resources:
        limits:
          cpus: '2'
          memory: 2G
```

**Database Read Replicas**:
```yaml
services:
  postgres-replica:
    image: postgres:16
    environment:
      POSTGRESQL_REPLICATION_MODE: slave
      POSTGRESQL_MASTER_HOST: postgres
```

### Vertical Scaling

**Resource Limits**:
```yaml
# Recommended production limits
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 4G
        reservations:
          cpus: '2'
          memory: 2G

  postgres:
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 8G

  qdrant:
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 8G
```

### Caching Strategy

**HybridCache (L1 + L2)**:
- L1: In-memory (per API instance)
- L2: Redis (shared across instances)

**Cache Keys**:
- Games list: 5 min TTL
- RAG results: 30 min TTL (keyed by hash(question+gameId))
- User profiles: 1 min TTL

---

## Security Hardening

### SSL/TLS Configuration

**Traefik TLS Settings**:
```yaml
# infra/traefik/traefik.yml
tls:
  options:
    default:
      minVersion: VersionTLS12
      cipherSuites:
        - TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256
        - TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384
```

### Firewall Rules

**UFW Configuration**:
```bash
# Allow only necessary ports
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp   # SSH
sudo ufw allow 80/tcp   # HTTP (Traefik)
sudo ufw allow 443/tcp  # HTTPS (Traefik)
sudo ufw enable
```

### Secret Management

**Never commit secrets to Git**:
```bash
# .gitignore
.env*
!.env.example
*.key
*.pem
appsettings.Production.json
```

**Use Docker Secrets** (production):
```yaml
# docker-compose.secrets.yml
services:
  api:
    secrets:
      - postgres_password
      - jwt_secret

secrets:
  postgres_password:
    file: /run/secrets/postgres_password
  jwt_secret:
    file: /run/secrets/jwt_secret
```

---

## CI/CD Pipeline

### GitHub Actions Workflow

**Auto-deploy on Push to `main`**:
```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Deploy to Server
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.SERVER_HOST }}
          username: ${{ secrets.SERVER_USER }}
          key: ${{ secrets.SSH_PRIVATE_KEY }}
          script: |
            cd /opt/meepleai
            git pull origin main
            docker compose --profile full down
            docker compose --profile full up -d --build
```

### Manual Deployment

**SSH to Server**:
```bash
ssh user@meepleai.com

# Pull latest code
cd /opt/meepleai
git pull origin main

# Rebuild and restart
docker compose --profile full down
docker compose --profile full up -d --build

# Verify
docker compose ps
curl https://api.meepleai.com/health
```

---

## Troubleshooting

### Common Issues

| Issue | Diagnostic | Fix |
|-------|------------|-----|
| **API not responding** | `docker compose logs api` | Check DB connection, restart: `docker compose restart api` |
| **CORS errors** | Check `CORS__AllowedOrigins` | Update .env, restart API |
| **SSL certificate invalid** | Traefik logs | Check DNS, verify email in Traefik config |
| **Database connection pool exhausted** | Grafana metrics | Increase `MaxPoolSize` in connection string |
| **High memory usage** | `docker stats` | Adjust resource limits in docker-compose.yml |
| **Slow RAG queries** | Check Qdrant logs | Optimize vector search params, add indexes |

### Health Checks

**All Services**:
```bash
# API health
curl https://api.meepleai.com/health

# Database connection
docker exec meepleai-postgres pg_isready -U meepleai

# Qdrant health
curl http://localhost:6333/health

# Redis health
docker exec meepleai-redis redis-cli ping
```

### Log Analysis

**Find Errors**:
```bash
# API errors (last 1 hour)
docker compose logs --since 1h api | grep -i error

# Database connection errors
docker compose logs postgres | grep -i "connection"

# Performance issues
docker compose logs api | grep -i "slow query"
```

---

## Maintenance

### Update Strategy

**Zero-Downtime Rolling Update**:
```bash
# Pull latest code
git pull origin main

# Build new images
docker compose build

# Rolling update (one container at a time)
docker compose up -d --no-deps --scale api=2 --build api
sleep 30
docker compose up -d --no-deps --scale api=1 api
```

### Database Maintenance

**Vacuum PostgreSQL** (monthly):
```bash
docker exec meepleai-postgres vacuumdb -U meepleai -d meepleai_prod --analyze --verbose
```

**Optimize Qdrant** (weekly):
```bash
curl -X POST http://localhost:6333/collections/documents/optimize
```

### Disk Space Cleanup

**Remove Unused Docker Resources**:
```bash
# Remove stopped containers
docker container prune -f

# Remove unused images
docker image prune -a -f

# Remove unused volumes (CAREFUL!)
docker volume prune -f
```

---

## Cost Optimization

### Resource Planning

**Estimated Monthly Costs** (AWS/DigitalOcean):
- **Server** (8GB RAM, 4 CPU): $40-80/month
- **Storage** (100GB SSD): $10/month
- **Bandwidth** (1TB): Included
- **OpenRouter API** (10K queries): $20-50/month
- **Backups** (S3/Spaces): $5/month
- **Total**: ~$75-145/month

### Cost Reduction Strategies

1. **Use CDN** for static assets (Cloudflare free tier)
2. **Cache aggressively** to reduce API calls
3. **Optimize embeddings** (smaller models for non-critical queries)
4. **Auto-scale down** during low-traffic hours
5. **Use spot instances** for non-production environments

---

## Disaster Recovery

### Backup Checklist

- [ ] Database backups (daily, retained 7 days)
- [ ] Vector database backups (daily)
- [ ] Configuration files (.env, docker-compose.yml)
- [ ] SSL certificates (Let's Encrypt auto-renews)
- [ ] Application code (Git repository)

### Recovery Procedure

**Full System Recovery** (from total failure):
```bash
# 1. Provision new server
# 2. Install Docker + Docker Compose
# 3. Restore code
git clone <repo-url> /opt/meepleai
cd /opt/meepleai

# 4. Restore configuration
scp backup-server:.env.production .env.production

# 5. Start infrastructure
docker compose --profile full up -d

# 6. Restore database
gunzip -c backups/postgres_latest.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai meepleai_prod

# 7. Restore Qdrant
tar -xzf backups/qdrant_latest.tar.gz -C /var/lib/docker/volumes/qdrant_data/_data/

# 8. Restart all services
docker compose restart

# 9. Verify
curl https://api.meepleai.com/health
```

**RTO (Recovery Time Objective)**: < 1 hour
**RPO (Recovery Point Objective)**: < 24 hours

---

## Resources

### Official Documentation
- [Traefik Documentation](https://doc.traefik.io/traefik/)
- [Docker Compose Reference](https://docs.docker.com/compose/)
- [PostgreSQL Backup Guide](https://www.postgresql.org/docs/current/backup.html)
- [Grafana Dashboards](https://grafana.com/grafana/dashboards/)

### MeepleAI Deployment Guides (New 2026-01-18)
- [NEW-GUIDES-INDEX.md](./NEW-GUIDES-INDEX.md) - Complete deployment guide index
- [Infrastructure Cost Summary](./infrastructure-cost-summary.md) - Budget planning (Alpha → Release 10K)
- [Domain Setup Guide](./domain-setup-guide.md) - Domain acquisition and DNS configuration
- [Email & TOTP Services](./email-totp-services.md) - Communication services setup
- [Monitoring Setup Guide](./monitoring-setup-guide.md) - Grafana + Prometheus configuration
- [Infrastructure Deployment Checklist](./infrastructure-deployment-checklist.md) - Complete deployment workflow
- [BoardGameGeek API Setup](./boardgamegeek-api-setup.md) - BGG integration configuration
- [Secrets Management](./secrets-management.md) - Secret system (.secret files)
- [Auto-Configuration Guide](./auto-configuration-guide.md) - Auto-config system (ADR-021)

### Related Documentation
- [Monitoring Guide](../02-development/README.md#monitoring)
- [Testing Guide](../05-testing/README.md)
- [Security Guide](../06-security/README.md)

---

**Version**: 1.0
**Last Updated**: 2026-01-01
**Maintainers**: DevOps Team
