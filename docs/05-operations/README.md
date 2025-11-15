# Operations Documentation

**Deployment, Monitoring, Runbooks, and DevOps** - Everything needed to deploy, operate, and maintain MeepleAI in production.

---

## 📁 Directory Structure

```
05-operations/
├── deployment/                    # Deployment guides
│   ├── board-game-ai-deployment-guide.md  # Complete deployment guide (35 pages) ⭐
│   ├── frontend-deployment.md    # Next.js deployment (Vercel, Docker)
│   └── disaster-recovery.md      # DR plan and procedures
├── runbooks/                      # Incident response runbooks
│   ├── high-error-rate.md        # Error spike troubleshooting
│   ├── dependency-down.md        # PG/Qdrant/Redis outage
│   ├── ai-quality-low.md         # RAG quality degradation
│   ├── error-spike.md            # Sudden error increase
│   ├── prompt-management-deployment.md  # Prompt deployment
│   └── general-troubleshooting.md # Common issues
├── monitoring/                    # Observability and monitoring
│   └── logging-and-audit.md      # Serilog, Seq, correlation IDs, audit trails
└── README.md                      # This file
```

---

## 🚀 Quick Start for Operations

**New DevOps/SRE onboarding** - Read in this order:

1. **[Deployment Guide](./deployment/board-game-ai-deployment-guide.md)** (35 pages) - Complete production deployment
2. **[Infrastructure Diagram](../01-architecture/diagrams/infrastructure-overview.md)** - All 15 services
3. **[General Troubleshooting](./runbooks/general-troubleshooting.md)** - Common issues and fixes
4. **[Logging & Audit](./monitoring/logging-and-audit.md)** - Observability setup
5. **[Disaster Recovery](./deployment/disaster-recovery.md)** - DR procedures

---

## 📚 Documentation by Category

### Deployment Guides

**Production deployment procedures and best practices.**

| Guide | Description | Priority | Pages |
|-------|-------------|----------|-------|
| [Deployment Guide](./deployment/board-game-ai-deployment-guide.md) | Complete production deployment (Docker Compose → Kubernetes) | ⭐ Essential | 35 |
| [Frontend Deployment](./deployment/frontend-deployment.md) | Next.js deployment (Vercel, Docker, static export) | Recommended | 10 |
| [Disaster Recovery](./deployment/disaster-recovery.md) | DR plan, backup/restore procedures, RTO/RPO | ⭐ Essential | 15 |

**Deployment Guide Contents**:
- Environment setup (dev, staging, production)
- Docker Compose deployment (15 services)
- Kubernetes deployment (manifests, Helm charts)
- Database migrations
- Secrets management (Vault, Infisical)
- SSL/TLS configuration
- Load balancing (Nginx, Kong)
- Monitoring setup (Prometheus, Grafana, Seq, Jaeger)
- Health checks configuration
- Auto-scaling setup
- Rollback procedures
- Zero-downtime deployments

**Frontend Deployment Options**:
1. **Vercel** (recommended for SSR/ISR)
   ```bash
   vercel --prod
   ```

2. **Docker**
   ```bash
   docker build -t meepleai-web:latest .
   docker run -p 3000:3000 meepleai-web:latest
   ```

3. **Static Export** (for CDN)
   ```bash
   pnpm build && pnpm export
   # Deploy .next/out/ to S3/CloudFront
   ```

**Disaster Recovery Highlights**:
- **RTO (Recovery Time Objective)**: 4 hours
- **RPO (Recovery Point Objective)**: 1 hour
- **Backup Schedule**: PostgreSQL (hourly), Qdrant (daily), Redis (none - cache only)
- **Backup Retention**: 30 days (daily), 90 days (weekly), 1 year (monthly)
- **DR Testing**: Quarterly

---

### Runbooks

**Incident response procedures for common operational issues.**

| Runbook | Scenario | MTTR Target | Priority |
|---------|----------|-------------|----------|
| [General Troubleshooting](./runbooks/general-troubleshooting.md) | Common issues (CORS, auth, migrations, etc.) | N/A | ⭐ Start here |
| [High Error Rate](./runbooks/high-error-rate.md) | Error rate >5% for 5+ minutes | 30 min | Essential |
| [Dependency Down](./runbooks/dependency-down.md) | PostgreSQL/Qdrant/Redis outage | 15 min | Essential |
| [AI Quality Low](./runbooks/ai-quality-low.md) | RAG confidence <0.60, hallucinations | 1 hour | Important |
| [Error Spike](./runbooks/error-spike.md) | Sudden error increase | 30 min | Important |
| [Prompt Management Deployment](./runbooks/prompt-management-deployment.md) | Deploying prompt changes | N/A | Optional |

**Runbook Structure** (consistent across all runbooks):

```markdown
# [Runbook Title]

## Symptoms
- What users see
- Alert conditions
- Metrics thresholds

## Investigation
- Step-by-step diagnostic commands
- Log queries (Seq, Grafana, Jaeger)
- Metric checks (Prometheus)

## Resolution
- Step-by-step fix procedures
- Rollback instructions
- Escalation path

## Prevention
- Long-term fixes
- Monitoring improvements
- Alert tuning

## Related Runbooks
- Links to related procedures
```

**Example: High Error Rate Runbook**

**Symptoms**:
- Prometheus alert: `error_rate > 0.05 for 5m`
- Grafana dashboard shows spike in 5xx errors
- User reports of "Internal Server Error"

**Investigation**:
```bash
# Check error logs (Seq)
curl "http://seq:8081/api/events?filter=@Level='Error'&count=100"

# Check Prometheus metrics
curl "http://prometheus:9090/api/v1/query?query=rate(http_requests_total{status=~'5..'}[5m])"

# Check service health
curl http://api:8080/health
```

**Resolution**:
1. Identify failing service (API, Qdrant, PostgreSQL)
2. Check resource usage (CPU, memory, disk)
3. Restart service if needed
4. Scale horizontally if resource-constrained
5. Rollback recent deployment if regression

---

### Monitoring & Observability

**Observability stack configuration and usage.**

| Document | Description | Priority |
|----------|-------------|----------|
| [Logging & Audit](./monitoring/logging-and-audit.md) | Serilog → Seq, correlation IDs, audit trails | ⭐ Essential |

**Observability Stack** (15 services total):

```
┌─────────────────────────────────────────────────┐
│              Application Logs                    │
│          (Serilog → Seq:8081)                   │
│  - Correlation IDs (W3C Trace Context)          │
│  - Structured logging (JSON)                    │
│  - Log levels: Debug, Info, Warning, Error      │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│            Distributed Tracing                   │
│       (OpenTelemetry → Jaeger:16686)            │
│  - W3C Trace Context propagation                │
│  - Spans: HTTP, DB queries, external calls      │
│  - Trace visualization                          │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│                 Metrics                          │
│         (Prometheus:9090)                        │
│  - Request rate, latency (P50, P95, P99)        │
│  - Error rate (5xx)                             │
│  - Custom metrics (RAG quality, PDF success)    │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│              Visualization                       │
│           (Grafana:3001)                        │
│  - System overview dashboard                    │
│  - RAG performance dashboard                    │
│  - PDF processing dashboard                     │
│  - API latency dashboard                        │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│                Alerting                          │
│        (Alertmanager:9093)                      │
│  - Email, Slack, PagerDuty                      │
│  - Alert grouping & throttling                  │
│  - Escalation policies                          │
└─────────────────────────────────────────────────┘
```

**Key Metrics**:

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| API Latency (P95) | <500ms | >2s for 5min |
| Error Rate (5xx) | <1% | >5% for 5min |
| RAG Confidence | ≥0.70 | <0.60 for 10min |
| PDF Success Rate | ≥95% | <90% for 10min |
| Database Query Time | <100ms | >500ms for 5min |
| Redis Hit Rate | >80% | <60% for 10min |
| CPU Usage | <70% | >90% for 5min |
| Memory Usage | <80% | >95% for 5min |
| Disk Usage | <85% | >95% |

**Logging Best Practices**:
```csharp
// Good: Structured logging
_logger.LogInformation("User {UserId} logged in from {IpAddress}", userId, ipAddress);

// Bad: String concatenation
_logger.LogInformation($"User {userId} logged in from {ipAddress}");

// Good: Correlation ID propagation
_logger.BeginScope(new Dictionary<string, object> {
    ["CorrelationId"] = correlationId,
    ["UserId"] = userId
});

// Good: Log levels
_logger.LogDebug("Cache miss for key {Key}", key);  // Dev only
_logger.LogInformation("PDF processing started for {DocumentId}", docId);  // Prod
_logger.LogWarning("RAG confidence low: {Confidence}", 0.55);  // Needs attention
_logger.LogError(ex, "Failed to process PDF {DocumentId}", docId);  // Immediate attention
_logger.LogCritical(ex, "Database connection failed");  // Critical system issue
```

**Correlation IDs**:
- W3C Trace Context standard (`traceparent` header)
- Propagated across all services
- Used to correlate logs, traces, and metrics
- Format: `00-{trace-id}-{span-id}-{flags}`

**Audit Logging**:
- All write operations logged (create, update, delete)
- User actions tracked (login, logout, settings changes)
- Admin actions audited (user management, config changes)
- Retention: 1 year minimum

---

## 🎯 Common Operations Tasks

### Deploying to Production

**Step-by-step production deployment**:

```bash
# 1. Build Docker images
cd infra
docker compose build

# 2. Tag images
docker tag meepleai-api:latest registry.example.com/meepleai-api:v1.2.3
docker tag meepleai-web:latest registry.example.com/meepleai-web:v1.2.3

# 3. Push to registry
docker push registry.example.com/meepleai-api:v1.2.3
docker push registry.example.com/meepleai-web:v1.2.3

# 4. Update Kubernetes manifests
kubectl set image deployment/api api=registry.example.com/meepleai-api:v1.2.3
kubectl set image deployment/web web=registry.example.com/meepleai-web:v1.2.3

# 5. Rollout
kubectl rollout status deployment/api
kubectl rollout status deployment/web

# 6. Verify health
curl https://api.meepleai.dev/health
curl https://meepleai.dev/api/health

# 7. Monitor (watch for 15 minutes)
# - Check error rate in Grafana
# - Check logs in Seq
# - Check latency in Prometheus

# 8. Rollback if issues
kubectl rollout undo deployment/api
kubectl rollout undo deployment/web
```

See [Deployment Guide](./deployment/board-game-ai-deployment-guide.md) for complete procedures.

---

### Investigating Performance Issues

**Performance investigation workflow**:

```bash
# 1. Check Prometheus metrics
curl "http://prometheus:9090/api/v1/query?query=rate(http_request_duration_seconds_sum[5m])/rate(http_request_duration_seconds_count[5m])"

# 2. Check Grafana dashboards
# - API Performance dashboard (http://grafana:3001/d/api-perf)
# - Database dashboard (http://grafana:3001/d/database)

# 3. Check Jaeger traces (slowest requests)
# - Open Jaeger UI (http://jaeger:16686)
# - Filter by service: meepleai-api
# - Sort by duration (descending)
# - Identify bottleneck spans (DB queries, external API calls)

# 4. Check Seq logs (errors, warnings)
curl "http://seq:8081/api/events?filter=@Level='Warning' OR @Level='Error'&count=100"

# 5. Profile slow endpoints (if needed)
# - dotnet-trace for CPU profiling
# - dotnet-dump for memory dumps
```

**Common Performance Bottlenecks**:
- **Slow DB Queries**: Add indexes, optimize queries, use AsNoTracking
- **N+1 Queries**: Use eager loading (Include/ThenInclude)
- **Cache Misses**: Increase cache TTL, warm cache on startup
- **External API Latency**: Add timeout, retry logic, circuit breaker
- **Large Payloads**: Enable Brotli/Gzip compression, paginate results

---

### Backup & Restore

**PostgreSQL Backup**:
```bash
# Backup
docker exec postgres pg_dump -U meeple meepleai > backup-$(date +%Y%m%d-%H%M%S).sql

# Restore
docker exec -i postgres psql -U meeple meepleai < backup-20251115-120000.sql

# Automated backup (cron)
0 * * * * /opt/meepleai/backup-postgres.sh  # Hourly
```

**Qdrant Backup**:
```bash
# Backup (snapshot API)
curl -X POST "http://qdrant:6333/collections/board_game_rules/snapshots"

# Download snapshot
curl "http://qdrant:6333/collections/board_game_rules/snapshots/{snapshot-name}" -o qdrant-backup.snapshot

# Restore (upload snapshot)
curl -X PUT "http://qdrant:6333/collections/board_game_rules/snapshots/upload" \
  --data-binary @qdrant-backup.snapshot
```

**Redis** (no backup needed - cache only, can be rebuilt)

See [Disaster Recovery](./deployment/disaster-recovery.md) for complete procedures.

---

### Scaling Services

**Horizontal Scaling (Kubernetes)**:
```bash
# Scale API replicas
kubectl scale deployment/api --replicas=5

# Scale web replicas
kubectl scale deployment/web --replicas=3

# Auto-scaling (HPA)
kubectl autoscale deployment/api --cpu-percent=70 --min=2 --max=10
```

**Vertical Scaling (Docker Compose)**:
```yaml
# docker-compose.yml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G
```

**Database Scaling**:
- **Read Replicas**: For read-heavy workloads (CQRS read models)
- **Connection Pooling**: PG pool size 10-100 (configured in ConnectionStrings)
- **Sharding**: Future (if >1TB data)

---

### Managing Secrets

**Vault/Infisical** (recommended for production):
```bash
# Store secret
vault kv put secret/meepleai/openrouter-api-key value=sk-or-***

# Read secret
vault kv get secret/meepleai/openrouter-api-key

# Rotate secret
vault kv put secret/meepleai/openrouter-api-key value=sk-or-new-***

# Inject into container
docker run -e OPENROUTER_API_KEY="$(vault kv get -field=value secret/meepleai/openrouter-api-key)" ...
```

**Kubernetes Secrets**:
```bash
# Create secret
kubectl create secret generic openrouter-api-key --from-literal=value=sk-or-***

# Use in deployment
kubectl set env deployment/api OPENROUTER_API_KEY=secret://openrouter-api-key/value
```

---

## 🚨 Incident Response

### Severity Levels

| Severity | Description | Response Time | Example |
|----------|-------------|---------------|---------|
| **SEV-1** | Critical system down, all users affected | 15 min | API completely down |
| **SEV-2** | Major functionality broken, many users affected | 1 hour | RAG not working |
| **SEV-3** | Minor functionality broken, few users affected | 4 hours | PDF upload slow |
| **SEV-4** | Cosmetic issue, no user impact | 1 week | UI typo |

### Incident Response Workflow

1. **Detect** (monitoring alerts, user reports)
2. **Acknowledge** (update status page, notify team)
3. **Investigate** (use runbooks, check logs/metrics/traces)
4. **Mitigate** (quick fix, rollback, scale up)
5. **Resolve** (permanent fix, deploy)
6. **Postmortem** (what happened, why, how to prevent)

### On-Call Rotation

- **Primary**: Responds within 15 min
- **Secondary**: Escalation after 30 min
- **Escalation**: Engineering lead after 1 hour

### Status Page

- **URL**: https://status.meepleai.dev
- **Updates**: Every 30 min during incidents
- **Components**: API, Web, Chat, PDF Upload, Search

---

## 🔍 Finding What You Need

### By Role

**I'm DevOps/SRE**:
1. [Deployment Guide](./deployment/board-game-ai-deployment-guide.md) - Production deployment
2. [Infrastructure Diagram](../01-architecture/diagrams/infrastructure-overview.md) - All services
3. [General Troubleshooting](./runbooks/general-troubleshooting.md) - Common fixes
4. [Disaster Recovery](./deployment/disaster-recovery.md) - DR procedures

**I'm On-Call**:
1. [General Troubleshooting](./runbooks/general-troubleshooting.md) - Start here
2. [High Error Rate](./runbooks/high-error-rate.md) - Error spike
3. [Dependency Down](./runbooks/dependency-down.md) - Service outage
4. [AI Quality Low](./runbooks/ai-quality-low.md) - RAG degradation

**I'm a Developer**:
1. [Logging & Audit](./monitoring/logging-and-audit.md) - How to log properly
2. [Frontend Deployment](./deployment/frontend-deployment.md) - Deploying Next.js

### By Scenario

**Deploying to Production**:
- [Deployment Guide](./deployment/board-game-ai-deployment-guide.md)
- Common Operations Tasks → Deploying to Production

**Investigating Outage**:
- [Dependency Down Runbook](./runbooks/dependency-down.md)
- [High Error Rate Runbook](./runbooks/high-error-rate.md)

**Investigating Performance**:
- Common Operations Tasks → Investigating Performance Issues
- [Logging & Audit](./monitoring/logging-and-audit.md)

**Backup/Restore**:
- [Disaster Recovery](./deployment/disaster-recovery.md)
- Common Operations Tasks → Backup & Restore

**Scaling**:
- [Deployment Guide](./deployment/board-game-ai-deployment-guide.md) → Auto-scaling section
- Common Operations Tasks → Scaling Services

---

## 📊 Monitoring Dashboards

**Grafana Dashboards** (http://grafana:3001):

1. **System Overview**
   - Overall health (green/yellow/red)
   - Request rate, error rate, latency
   - Resource usage (CPU, memory, disk)

2. **API Performance**
   - Endpoint latency (P50, P95, P99)
   - Throughput (requests/sec)
   - Error rate by endpoint

3. **RAG Pipeline**
   - Retrieval latency
   - Confidence score distribution
   - Hallucination rate (<3% target)
   - Citation accuracy

4. **PDF Processing**
   - Processing time (Stage 1, 2, 3)
   - Success rate (95%+ target)
   - Quality score distribution

5. **Database**
   - Query latency
   - Connection pool usage
   - Slow queries (>500ms)

6. **Infrastructure**
   - CPU, memory, disk per service
   - Network traffic
   - Container restarts

---

## 🤝 Contributing to Operations Docs

### Adding a New Runbook

**When to create a runbook**:
- Recurring incident (happened 2+ times)
- Complex troubleshooting (>5 steps)
- Multiple team members need to respond

**Runbook Template**:
```markdown
# [Runbook Title]

**Severity**: SEV-1 | SEV-2 | SEV-3 | SEV-4
**Response Time**: 15 min | 1 hour | 4 hours | 1 week

## Symptoms
- What users see
- Alert conditions
- Metrics thresholds

## Investigation
Step-by-step diagnostic commands

## Resolution
Step-by-step fix procedures

## Prevention
Long-term fixes

## Related Runbooks
Links to related procedures
```

### Updating Deployment Guide

**When to update**:
- New service added
- Deployment process changed
- New environment added (staging, canary)
- Kubernetes manifests updated

---

## 🔗 Related Documentation

- **[CLAUDE.md](../../CLAUDE.md)** - Complete development guide
- **[Infrastructure](../../infra/README.md)** - Infrastructure setup
- **[Architecture](../01-architecture/)** - System architecture
- **[API Specification](../03-api/board-game-ai-api-specification.md)** - API docs

---

**Last Updated**: 2025-11-15
**Maintainer**: DevOps Team
**On-Call**: See PagerDuty rotation
**Status Page**: https://status.meepleai.dev
