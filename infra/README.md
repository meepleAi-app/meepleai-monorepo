# MeepleAI Infrastructure

**Infrastructure as Code** - Docker Compose, monitoring, secrets management, and observability stack configuration.

---

## 📁 Directory Structure

```
infra/
├── docker-compose.yml              # Base service definitions
├── docker-compose.dev.yml          # Development override
├── docker-compose.infisical.yml    # Infisical secrets management
├── compose.test.yml                # Test/CI environment (NEW)
├── compose.staging.yml             # Staging environment (NEW)
├── compose.prod.yml                # Production environment (NEW)
├── prometheus.yml                  # Prometheus configuration
├── alertmanager.yml                # Alert routing configuration
├── grafana-datasources.yml         # Grafana data sources
├── grafana-dashboards.yml          # Grafana dashboard config
├── OPS-05-SETUP.md                # Infrastructure setup guide
├── README-dev.md                   # Development environment guide
├── dashboards/                     # Grafana dashboard JSON
├── env/                            # Environment configuration files
├── init/                           # Initialization scripts
│   └── n8n/                        # n8n workflow initialization
├── n8n/                            # n8n workflow definitions
│   ├── templates/                  # Workflow templates
│   └── workflows/                  # Production workflows
├── prometheus/                     # Prometheus configurations (NEW)
│   └── alerts/                     # Modular alert rule files (NEW)
│       ├── api-performance.yml     # API metrics & alerts
│       ├── database-health.yml     # PostgreSQL alerts
│       ├── cache-performance.yml   # Redis alerts
│       ├── vector-search.yml       # Qdrant alerts
│       ├── infrastructure.yml      # Memory, CPU, resources
│       ├── quality-metrics.yml     # AI quality monitoring
│       └── pdf-processing.yml      # PDF pipeline alerts
├── scripts/                        # Infrastructure utility scripts
└── secrets/                        # Secret templates (gitignored)
```

---

## 🚀 Quick Start

### Multi-Environment Support (NEW)

MeepleAI infrastructure supports multiple environments with hierarchical compose files:

#### Development

```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

Development mode includes:
- Hot reload enabled
- Debug ports exposed
- Verbose logging
- Simpler authentication

#### Test/CI

```bash
cd infra
docker compose -f docker-compose.yml -f compose.test.yml up
```

Test mode optimizations:
- In-memory storage (PostgreSQL, Redis)
- Minimal observability (use `--profile observability` if needed)
- Fast startup for CI pipelines
- No auto-restart

#### Staging

```bash
cd infra
docker compose -f docker-compose.yml -f compose.staging.yml up -d
```

Staging includes:
- Production-like configuration
- Full observability stack
- 60-day metrics retention
- Logging with rotation

#### Production

```bash
cd infra
docker compose -f docker-compose.yml -f compose.prod.yml up -d
```

Production features:
- Resource limits and reservations
- 90-day metrics retention
- Enhanced security
- High availability settings

### Start with Infisical (Secrets Management)

```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.infisical.yml up -d
```

---

## 📦 Services Overview

### Core Services

| Service | Port | Description | Health Check |
|---------|------|-------------|--------------|
| **postgres** | 5432 | PostgreSQL 16 database | `curl localhost:5432` |
| **qdrant** | 6333 | Vector database for embeddings | `curl http://localhost:6333/healthz` |
| **redis** | 6379 | Cache and session store | `redis-cli ping` |

### AI/ML Services

| Service | Port | Description | Docker Image |
|---------|------|-------------|--------------|
| **ollama** | 11434 | Local LLM runtime | `ollama/ollama:latest` |
| **embedding** | 8000 | BGE-M3 multilingual embedding | Custom (apps/embedding-service) |
| **unstructured** | 8001 | PDF text extraction (Stage 1) | Custom (apps/unstructured-service) |
| **smoldocling** | 8002 | VLM PDF extraction (Stage 2) | Custom (apps/smoldocling-service) |

### Observability Services

| Service | Port | Description | Access URL |
|---------|------|-------------|------------|
| **seq** | 8081 | Centralized logging | http://localhost:8081 |
| **jaeger** | 16686 | Distributed tracing | http://localhost:16686 |
| **prometheus** | 9090 | Metrics collection | http://localhost:9090 |
| **alertmanager** | 9093 | Alert routing | http://localhost:9093 |
| **grafana** | 3001 | Dashboards & visualization | http://localhost:3001 (admin/admin) |

### Workflow & App Services

| Service | Port | Description | Access URL |
|---------|------|-------------|------------|
| **n8n** | 5678 | Workflow automation | http://localhost:5678 |
| **api** | 8080 | ASP.NET Core API | http://localhost:8080 |
| **web** | 3000 | Next.js frontend | http://localhost:3000 |

---

## 🔧 Configuration Files

### Docker Compose

- **docker-compose.yml**: Base service definitions
  - All 15 services defined
  - Environment-agnostic configuration
  - Health checks configured

- **docker-compose.dev.yml**: Development overrides
  - Hot reload volumes
  - Debug ports exposed
  - Verbose logging enabled

- **compose.test.yml**: Test/CI environment (NEW)
  - In-memory storage for speed
  - Minimal observability stack
  - Optimized for CI pipelines

- **compose.staging.yml**: Staging environment (NEW)
  - Production-like settings
  - Full observability enabled
  - Longer metrics retention (60d)

- **compose.prod.yml**: Production environment (NEW)
  - Resource limits and reservations
  - Enhanced security settings
  - 90-day metrics retention
  - High availability configuration

- **docker-compose.infisical.yml**: Secrets management
  - Infisical integration
  - Secret injection
  - Environment variable management

### Monitoring & Observability

- **prometheus.yml**: Prometheus scrape configuration
  - Scrape intervals (15s default)
  - Service discovery
  - Modular alert rules loading

- **prometheus/alerts/** (NEW - Modular Organization):
  - **api-performance.yml**: API errors, response times, request rates
  - **database-health.yml**: PostgreSQL availability and performance
  - **cache-performance.yml**: Redis health monitoring
  - **vector-search.yml**: Qdrant vector database alerts
  - **infrastructure.yml**: Memory, CPU, and resource alerts
  - **quality-metrics.yml**: AI quality and confidence monitoring
  - **pdf-processing.yml**: PDF extraction pipeline alerts

  Benefits:
  - 40+ alert rules organized by category
  - Easier navigation and maintenance
  - Cleaner PR diffs
  - Modular enable/disable by category

- **alertmanager.yml**: Alert routing
  - Email notifications
  - Slack webhooks
  - PagerDuty integration
  - Alert grouping/throttling

- **grafana-datasources.yml**: Grafana data sources
  - Prometheus
  - Jaeger
  - PostgreSQL (future)

- **grafana-dashboards.yml**: Dashboard provisioning
  - System overview
  - RAG performance
  - PDF processing metrics
  - API latency

---

## 📂 Subdirectories

### dashboards/

**Grafana dashboard JSON definitions**

Contains dashboard templates for:
- System health overview
- RAG pipeline performance
- PDF processing metrics
- API endpoint latency
- Database query performance
- Redis cache hit rates

**Usage**:
```bash
# Dashboards auto-loaded on Grafana startup
# Access at http://localhost:3001/dashboards
```

### env/

**Environment configuration files**

- `.env.example` - Template for environment variables
- `.env.dev` - Development configuration (gitignored)
- `.env.prod` - Production configuration (gitignored)
- `.env.test` - Test environment (gitignored)

**Required variables**:
- `OPENROUTER_API_KEY` - LLM provider API key
- `ConnectionStrings__Postgres` - PostgreSQL connection string
- `QDRANT_URL` - Qdrant vector DB URL
- `REDIS_URL` - Redis connection string
- `INITIAL_ADMIN_EMAIL` - Bootstrap admin user
- `INITIAL_ADMIN_PASSWORD` - Bootstrap admin password

### init/

**Initialization scripts** (run on container startup)

- **n8n/**: n8n workflow initialization
  - Workflow templates
  - Credential setup
  - Webhook configuration

### n8n/

**n8n workflow definitions**

- **templates/**: Reusable workflow templates
  - PDF processing workflow
  - RAG quality monitoring
  - Error notification workflow

- **workflows/**: Production workflows (JSON)
  - Automated quality checks
  - Alert routing
  - Batch processing jobs

**Usage**:
```bash
# Import workflows via n8n UI
# Or mount as volume in docker-compose.yml
```

### prometheus/

**Prometheus configurations**

- **alerts/**: Alert rule files (organized by category)
  - `api-alerts.yml` - API endpoint alerts
  - `db-alerts.yml` - Database alerts
  - `rag-alerts.yml` - RAG quality alerts
  - `infrastructure-alerts.yml` - Infrastructure health

### scripts/

**Infrastructure utility scripts**

- Database backup scripts
- Secret rotation scripts
- Health check scripts
- Cleanup scripts

**Example**:
```bash
# Backup PostgreSQL
./scripts/backup-postgres.sh

# Rotate secrets
./scripts/rotate-secrets.sh
```

### secrets/

**Secret templates** (gitignored in production)

Contains templates for:
- API keys
- Database passwords
- OAuth client secrets
- JWT signing keys

**IMPORTANT**: Never commit actual secrets to git!

---

## 🛠️ Common Operations

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f meepleai-api
docker compose logs -f meepleai-web

# Last 100 lines
docker compose logs --tail=100 meepleai-api
```

### Restart Services

```bash
# All services
docker compose restart

# Specific service
docker compose restart meepleai-api
docker compose restart meepleai-postgres
```

### Stop Stack

```bash
# Stop all
docker compose down

# Stop and remove volumes (CAUTION: data loss)
docker compose down -v
```

### Scale Services

```bash
# Scale API to 3 replicas
docker compose up -d --scale meepleai-api=3

# Scale web to 2 replicas
docker compose up -d --scale meepleai-web=2
```

### Update Images

```bash
# Pull latest images
docker compose pull

# Rebuild custom images
docker compose build

# Pull and rebuild
docker compose up -d --build
```

### Health Checks

```bash
# API health
curl http://localhost:8080/health

# Qdrant health
curl http://localhost:6333/healthz

# Redis health
redis-cli ping

# Prometheus targets
curl http://localhost:9090/api/v1/targets
```

---

## 🔍 Troubleshooting

### Service Won't Start

```bash
# Check logs
docker compose logs service-name

# Check resource usage
docker stats

# Check networks
docker network ls
docker network inspect infra_default
```

### Database Connection Issues

```bash
# Check PostgreSQL logs
docker compose logs meepleai-postgres

# Connect to database
docker compose exec meepleai-postgres psql -U meeple -d meepleai

# Check connections
docker compose exec meepleai-postgres psql -U meeple -c "SELECT * FROM pg_stat_activity;"
```

### Qdrant Issues

```bash
# Check Qdrant logs
docker compose logs meepleai-qdrant

# Check collections
curl http://localhost:6333/collections

# Check cluster info
curl http://localhost:6333/cluster
```

### Redis Issues

```bash
# Connect to Redis CLI
docker compose exec meepleai-redis meepleai-redis-cli

# Check memory usage
docker compose exec meepleai-redis meepleai-redis-cli INFO memory

# Monitor commands
docker compose exec meepleai-redis meepleai-redis-cli MONITOR
```

### Prometheus/Grafana Issues

```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets | jq

# Check Prometheus config
docker compose exec meepleai-prometheus promtool check config /etc/meepleai-prometheus/meepleai-prometheus.yml

# Reload Prometheus config
curl -X POST http://localhost:9090/-/reload

# Check Grafana logs
docker compose logs meepleai-grafana
```

---

## 📚 Documentation

- **[OPS-05-SETUP.md](./OPS-05-SETUP.md)** - Detailed infrastructure setup guide
- **[README-dev.md](./README-dev.md)** - Development environment guide
- **[Deployment Guide](../docs/05-operations/deployment/board-game-ai-deployment-guide.md)** - Production deployment
- **[Disaster Recovery](../docs/05-operations/deployment/disaster-recovery.md)** - DR procedures
- **[Runbooks](../docs/05-operations/runbooks/)** - Incident response guides

---

## 🔒 Security

### Secret Management

- **Development**: Use `.env.dev` (gitignored)
- **Production**: Use Infisical or HashiCorp Vault
- **Never**: Commit secrets to git

### Network Security

- All services on isolated Docker network
- Only necessary ports exposed
- TLS/SSL for production (via reverse proxy)

### Access Control

- Grafana: Default admin/admin (CHANGE IN PRODUCTION!)
- n8n: Configure OAuth in production
- Seq: Configure API keys for production

---

## 📊 Monitoring

### Key Metrics

- **API Latency**: P50, P95, P99 (target: <500ms P95)
- **Error Rate**: 5xx errors (target: <1%)
- **RAG Quality**: Confidence scores (target: ≥0.70)
- **Database**: Query time, connection pool usage
- **Cache**: Redis hit rate (target: >80%)

### Dashboards

Access Grafana at http://localhost:3001:

1. **System Overview**: Overall health, resource usage
2. **API Performance**: Endpoint latency, throughput, errors
3. **RAG Pipeline**: Retrieval quality, generation latency
4. **PDF Processing**: Success rate, processing time
5. **Infrastructure**: CPU, memory, disk, network

### Alerts

Configured alerts (via Prometheus + Alertmanager):

- High error rate (>5% for 5 minutes)
- High latency (P95 >2s for 5 minutes)
- RAG quality degradation (confidence <0.60)
- Database connection pool exhausted
- Redis cache unavailable
- Disk usage >85%

---

## 🚀 Production Deployment

For production deployment, see:
- [Deployment Guide](../docs/05-operations/deployment/board-game-ai-deployment-guide.md)
- [Kubernetes Manifests](./k8s/) (if applicable)

**Production Checklist**:
- [ ] Use Infisical/Vault for secrets
- [ ] Configure TLS/SSL certificates
- [ ] Set resource limits (CPU, memory)
- [ ] Enable authentication on all services
- [ ] Configure backup schedules
- [ ] Set up monitoring alerts
- [ ] Configure log retention
- [ ] Enable audit logging
- [ ] Review security settings
- [ ] Test disaster recovery

---

## 🤝 Contributing

When modifying infrastructure:

1. **Test locally first** with `docker-compose.dev.yml`
2. **Update documentation** (this README, OPS-05-SETUP.md)
3. **Test health checks** after changes
4. **Update Prometheus alerts** if adding new services
5. **Add Grafana dashboards** for new metrics
6. **Create PR** with `[INFRA]` prefix

---

**Last Updated**: 2025-11-15
**Maintainer**: Infrastructure Team
**Total Services**: 15
