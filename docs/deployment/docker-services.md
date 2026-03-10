# Docker Services - Reference Guide

**Last Updated**: 2026-02-12

## Service Configuration Matrix

| Service | Image | Port | RAM Limit | CPU Limit | Health Check | Volumes |
|---------|-------|------|-----------|-----------|--------------|---------|
| **postgres** | postgres:16.4-alpine3.20 | 127.0.0.1:5432:5432 | 2GB | 2 | pg_isready | postgres_data |
| **qdrant** | qdrant/qdrant:v1.12.4 | 127.0.0.1:6333:6333<br/>127.0.0.1:6334:6334 | 4GB | 2 | HTTP /readyz | qdrant_data |
| **redis** | redis:7.4.1-alpine3.20 | 127.0.0.1:6379:6379 | 1GB | 1 | redis-cli ping | (ephemeral) |
| **api** | build:../apps/api | 127.0.0.1:8080:8080 | 4GB | 2 | HTTP / | pdf_uploads |
| **web** | build:../apps/web | 127.0.0.1:3000:3000 | 1GB | 1 | HTTP / | (none) |
| **traefik** | traefik:v3.2 | 80:80<br/>8080:8080 | 512MB | 1 | (none) | traefik/dynamic<br/>traefik/logs |
| **embedding-service** | build:../apps/embedding | 8000:8000 | 4GB | 2 | HTTP /health | (none) |
| **reranker-service** | build:../apps/reranker | 127.0.0.1:8003:8003 | 2GB | 2 | HTTP /health | reranker_models |
| **unstructured-service** | build:../apps/unstructured | 8001:8001 | 2GB | 2 | HTTP /health | unstructured_temp |
| **smoldocling-service** | build:../apps/smoldocling | 8002:8002 | 4GB | 2 | HTTP /health | smoldocling_temp<br/>smoldocling_models |
| **ollama** | ollama/ollama:0.3.14 | 11434:11434 | 8GB | 4 | ollama list | ollama_data |
| **ollama-pull** | curlimages/curl:8.12.1 | (none) | 512MB | 1 | (none) | (none) |
| **prometheus** | prom/prometheus:v3.7.0 | 127.0.0.1:9090:9090 | 2GB | 1 | HTTP /-/healthy | prometheus_data |
| **grafana** | grafana/grafana:11.4.0 | 127.0.0.1:3001:3000 | 1GB | 1 | HTTP /api/health | grafana_data |
| **alertmanager** | prom/alertmanager:v0.27.0 | 127.0.0.1:9093:9093 | 512MB | 0.5 | HTTP /-/healthy | alertmanager_data |
| **cadvisor** | gcr.io/cadvisor/cadvisor:v0.49.1 | 127.0.0.1:8082:8080 | 512MB | 0.5 | (none) | (host mounts) |
| **node-exporter** | prom/node-exporter:v1.8.2 | 127.0.0.1:9100:9100 | 256MB | 0.5 | (none) | (host mounts) |
| **hyperdx** | hyperdx/hyperdx-local:latest | 8180:8080<br/>14317:4317<br/>14318:4318 | 4GB | 2 | HTTP /health | hyperdx_data<br/>hyperdx_logs |
| **mailpit** | axllent/mailpit:v1.22 | 127.0.0.1:1025:1025<br/>127.0.0.1:8025:8025 | 128MB | 0.5 | HTTP /api/v1/messages | mailpit_data |
| **n8n** | n8nio/n8n:1.114.4 | 127.0.0.1:5678:5678 | 1GB | 1 | (none) | (none) |
| **socket-proxy** | ghcr.io/tecnativa/docker-socket-proxy:0.3.0 | (internal) | 256MB | 0.5 | HTTP /version | (socket) |

## Service Purpose Summary

| Service | Role | Critical | Profile |
|---------|------|----------|---------|
| **postgres** | Relational database (DDD 9 contexts) | ✅ | minimal, dev, ai, automation, observability, full |
| **qdrant** | Vector database (RAG pipeline) | ✅ | minimal, dev, ai, automation, observability, full |
| **redis** | Cache + session store | ✅ | minimal, dev, ai, automation, observability, full |
| **api** | .NET 9 backend (CQRS) | ✅ | minimal, dev, ai, automation, observability, full |
| **web** | Next.js 14 frontend | ✅ | minimal, dev, ai, automation, observability, full |
| **traefik** | Reverse proxy + routing | ✅ | minimal, dev, ai, automation, observability, full |
| **embedding-service** | Multilingual embeddings (1024d) | ⚠️ | ai, full |
| **reranker-service** | Cross-encoder reranking | ⚠️ | ai, full |
| **unstructured-service** | PDF Stage 1 (fast) | ⚠️ | ai, full |
| **smoldocling-service** | PDF Stage 2 (VLM) | ⚠️ | ai, full |
| **ollama** | Local LLM hosting | ⚠️ | ai, full |
| **ollama-pull** | Model downloader | ⚠️ | ai, full |
| **prometheus** | Metrics collection | 📊 | dev, observability, full |
| **grafana** | Visualization + dashboards | 📊 | dev, observability, full |
| **alertmanager** | Alert routing | 📊 | observability, full |
| **cadvisor** | Container metrics | 📊 | observability, full |
| **node-exporter** | Host metrics | 📊 | observability, full |
| **hyperdx** | Unified observability | 📊 | observability, full |
| **mailpit** | Email testing (dev only) | 🔧 | dev, observability, full |
| **n8n** | Workflow automation | 🔧 | automation, full |
| **socket-proxy** | Docker API security | 🔒 | prod |

## Configuration Files

| File | Purpose | Environment |
|------|---------|-------------|
| `docker-compose.yml` | Main service definitions | All |
| `compose.traefik.yml` | Traefik override | Production |
| `compose.hyperdx.yml` | HyperDX override | Observability |
| `infra/secrets/*.secret` | Secret values (gitignored) | All |
| `infra/env/*.env` | Environment configs | Dev/Prod |
| `infra/prometheus.yml` | Scrape targets | Observability |
| `infra/prometheus-rules.yml` | Alert rules | Observability |
| `infra/grafana-datasources.yml` | Grafana datasources | Observability |
| `infra/traefik/dynamic/*.yml` | Middleware configs | Production |

## AI/ML Service Details

### Embedding Service
- **Model**: intfloat/multilingual-e5-large
- **Dimensions**: 1024
- **Languages**: 100+ (including Italian)
- **Device**: CPU (GPU optional, 10-20x faster)
- **Batch Size**: Configurable
- **Warmup**: Enabled

### Reranker Service
- **Model**: BAAI/bge-reranker-v2-m3
- **Batch Size**: 32
- **Warmup**: Enabled
- **Use Case**: RAG precision improvement

### Unstructured Service (Stage 1)
- **Strategy**: Fast (OCR-free)
- **Language**: Italian
- **Max File**: 50MB
- **Timeout**: 30s
- **Chunking**: 2000 chars, 200 overlap
- **Quality**: >0.80 threshold

### SmolDocling Service (Stage 2)
- **Model**: docling-project/SmolDocling-256M-preview
- **Device**: CPU (cuda for GPU)
- **Max Pages**: 20
- **Timeout**: 60s
- **Quality**: >0.70 threshold
- **Fallback**: When Unstructured <0.80

### Ollama
- **Models**: nomic-embed-text (auto-pulled)
- **Max Loaded**: 3 concurrent
- **Keep Alive**: 5 minutes
- **GPU Memory**: 80% fraction

## Observability Stack Details

### Prometheus
- **Retention**: 30 days, 5GB max
- **Scrape Interval**: 15s (default)
- **Lifecycle API**: Enabled
- **Targets**: api, postgres, redis, qdrant, cadvisor, node-exporter

### Grafana
- **Datasources**: Prometheus (auto-provisioned)
- **Dashboards**: Auto-loaded from `./dashboards/`
- **Anonymous Access**: Viewer role
- **Iframe**: Enabled

### HyperDX
- **Logs**: 30 days retention
- **Traces**: 30 days retention
- **Sessions**: 7 days retention
- **Storage**: 50GB max
- **ClickHouse**: 4GB RAM limit

### Alertmanager
- **Channels**: Email, Slack
- **Routes**: Critical vs warning
- **Inhibition**: Duplicate suppression

## Secret Files Reference

**Critical** (blocks startup):
- `database.secret`: POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
- `redis.secret`: REDIS_PASSWORD
- `jwt.secret`: JWT_SECRET_KEY, JWT_ISSUER, JWT_AUDIENCE
- `admin.secret`: DEFAULT_ADMIN_EMAIL, DEFAULT_ADMIN_PASSWORD
- `embedding-service.secret`: EMBEDDING_API_KEY

**Important** (warns):
- `openrouter.secret`: OPENROUTER_API_KEY
- `unstructured-service.secret`: UNSTRUCTURED_API_KEY
- `bgg.secret`: BGG_API_USERNAME, BGG_API_PASSWORD

**Optional**:
- `oauth.secret`: Google, GitHub, Microsoft OAuth
- `email.secret`: SMTP config
- `monitoring.secret`: Grafana admin, webhooks
- `n8n.secret`: Encryption key, webhooks
- `storage.secret`: S3/Azure credentials
- `traefik.secret`: Let's Encrypt, dashboard
- `qdrant.secret`: QDRANT_API_KEY (prod only)

## Performance Tuning

### PostgreSQL
```yaml
Shared Buffers: 512MB
Effective Cache: 1536MB
Work Memory: 16MB
Maintenance Work: 256MB
Shared Memory: 1GB
```

### Redis
```yaml
Max Memory: 768MB
Policy: allkeys-lru
Persistence: AOF enabled
```

### Qdrant
```yaml
Collections: Auto-optimized
Index: HNSW algorithm
Vectors: In-memory + disk
```

### API
```yaml
HybridCache: Enabled
Connection Pool: 100 max
Timeout: 30s
Rate Limit: Traefik middleware
```

## Scaling Strategies

**Vertical Scaling** (single host):
- PostgreSQL: 4 CPU, 4GB → Enable PgBouncer
- Qdrant: 8GB RAM → Add SSD storage
- Redis: 2GB maxmemory → Tune eviction
- API: 4 CPU, 8GB → HybridCache
- Embedding: Add GPU (10-20x speedup)
- SmolDocling: Add GPU, 8GB RAM

**Horizontal Scaling** (multi-host):
- API: Docker Swarm replicas or K8s Pods
- Web: CDN + SSR replicas
- Embedding: K8s HPA by queue length
- PostgreSQL: Read replicas via PgBouncer
- Qdrant: Cluster mode (3+ nodes)
- Redis: Sentinel or Cluster

## Security Hardening

**Container**:
- Run as non-root
- Read-only filesystems + tmpfs
- `no-new-privileges` enabled
- Minimal base images (Alpine)
- Vulnerability scanning (Trivy)

**Network**:
- Localhost binding (127.0.0.1)
- Internal Docker network
- Traefik rate limiting
- IP whitelisting for admin
- Firewall rules (ufw/iptables)

**Secrets**:
- File-based `.secret` files
- Restricted permissions (600)
- Rotation every 90 days
- Secret scanning in CI/CD

**Production**:
- Enable Socket Proxy
- HTTPS/TLS via Traefik
- CORS strict origins
- CSRF protection
- JWT signature validation
- Input sanitization

## Backup Strategy

**PostgreSQL**:
```bash
# Daily pg_dump
docker compose exec -T postgres pg_dump -U user -d db > backup.sql
gzip backup.sql
```

**Qdrant**:
```bash
# Snapshot collections
curl -X POST "http://localhost:6333/collections/games/snapshots"
curl "http://localhost:6333/collections/games/snapshots/name" -o snapshot.bin
```

**Redis**:
- AOF persistence enabled
- Daily RDB snapshots
- Replication to standby

**Volumes**:
```bash
docker run --rm -v vol:/source:ro -v /backup:/dest alpine tar czf /dest/vol.tar.gz /source
```

## Monitoring Alerts

**Critical** (2m for):
- API down
- Database unreachable
- Redis memory >90%

**Warning** (5m for):
- DB connections >90
- Qdrant latency >2s p95
- Embedding errors >10%

**Info**:
- Disk space <20%
- Container restarts
- High CPU >80%

## Production Checklist

**Pre-deployment**:
- [ ] Generate secrets: `pwsh setup-secrets.ps1`
- [ ] Validate config: `docker compose config --quiet`
- [ ] Review resource limits
- [ ] Enable Socket Proxy: `COMPOSE_PROFILES=prod`
- [ ] Configure TLS in Traefik
- [ ] Set ASPNETCORE_ENVIRONMENT=Production

**Monitoring**:
- [ ] Configure Alertmanager channels
- [ ] Set up Grafana dashboards
- [ ] Enable HyperDX tracing
- [ ] Test alert routing

**Backup**:
- [ ] Automate PostgreSQL dumps
- [ ] Configure volume backups
- [ ] Test restore procedures
- [ ] Enable PITR (WAL archiving)

**Scaling**:
- [ ] Add GPU for AI services
- [ ] Enable PgBouncer
- [ ] Configure Redis Sentinel
- [ ] Set up API replicas

## Reference

- **Quick Start**: `docs/04-deployment/docker-quickstart.md`
- **Troubleshooting**: `docs/04-deployment/docker-troubleshooting.md`
- **Architecture**: `docs/01-architecture/system-overview.md`
- **Traefik Docs**: https://doc.traefik.io/traefik/
- **Prometheus Best Practices**: https://prometheus.io/docs/practices/
