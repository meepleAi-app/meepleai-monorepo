# Operations Manual Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a single centralized operations manual covering all MeepleAI services, with CI-generated PDF and cleanup of 24 duplicate docs.

**Architecture:** Single markdown file (`docs/operations/operations-manual.md`) covering 14 services with uniform template. GitHub Actions workflow generates PDF via md-to-pdf on push. Existing scattered runbooks are migrated then deleted.

**Tech Stack:** Markdown, md-to-pdf (Node.js/Chromium), GitHub Actions, softprops/action-gh-release

**Spec:** `docs/superpowers/specs/2026-03-16-operations-manual-design.md`

---

## Chunk 1: Setup & Infrastructure

### Task 1: Branch + Skeleton + CI Files

**Files:**
- Create: `docs/operations/operations-manual.md` (skeleton with all section headers)
- Create: `docs/operations/.md-to-pdf.js`
- Create: `.github/workflows/generate-operations-pdf.yml`

- [ ] **Step 1: Create feature branch**

```bash
git checkout main-dev && git pull
git checkout -b feature/operations-manual
git config branch.feature/operations-manual.parent main-dev
```

- [ ] **Step 2: Count actual Docker volumes**

Read `infra/docker-compose.yml` and all `compose.*.yml` files. Count every `volumes:` top-level definition (named volumes). Record the exact list with service mappings.

- [ ] **Step 3: Create skeleton `docs/operations/operations-manual.md`**

Write the full document skeleton with all 18 sections as defined in the spec (Section 4). Each section should have the standard service template subsection headers (Overview, Daily Operations, Backup & Restore, Scaling & Performance Tuning, Upgrade & Migration, Troubleshooting, Disaster Recovery, Cost Notes) with `TODO` placeholders.

- [ ] **Step 4: Create `docs/operations/.md-to-pdf.js`**

Copy the exact config from the spec (Section 7, "PDF Styling Config"). Key points:
- `h1:not(:first-of-type)` for page breaks (no blank first page)
- `launch_options.args: ['--no-sandbox', '--disable-setuid-sandbox']` for CI compatibility

- [ ] **Step 5: Create `.github/workflows/generate-operations-pdf.yml`**

Copy the exact workflow from the spec (Section 7). Key points:
- Triggers on `docs/operations/operations-manual.md` and `.md-to-pdf.js` changes
- Installs `libgbm-dev libnss3 libatk-bridge2.0-0 libxss1` for Chromium
- Uploads as workflow artifact (90-day retention)
- On `main` branch: also creates GitHub Release via `softprops/action-gh-release@v2`

- [ ] **Step 6: Commit**

```bash
git add docs/operations/operations-manual.md docs/operations/.md-to-pdf.js .github/workflows/generate-operations-pdf.yml
git commit -m "docs(ops): add operations manual skeleton + CI pipeline for PDF generation"
```

---

## Chunk 2: Quick Reference & Docker Environment (Sections 1-2)

### Task 2: Quick Reference (Section 1)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/deployment-cheatsheet.md`, `docs/deployment/deployment-quick-reference.md`, `docs/deployment/health-checks.md`, `docs/deployment/docker-services.md`

- [ ] **Step 1: Read source files**

Read all 4 source files listed above. Extract: service table (name, port, volume, health endpoint, restart command), Docker commands cheat sheet, service dependency map.

- [ ] **Step 2: Write Section 1 — Quick Reference**

Replace the TODO placeholder in `docs/operations/operations-manual.md` Section 1 with:

1. **Service Table** — all services with columns: Service | Port | Volume | Health Check | Restart Command
2. **Essential Docker Commands** — start/stop/restart/logs/stats/prune organized by task
3. **Service Dependency Map** — text diagram showing: PostgreSQL ← API → Redis, API → Qdrant, Traefik → API/Web, etc.

Use data from the extracted source files. Ensure every service (PostgreSQL, Redis, Qdrant, Traefik, Prometheus, Grafana, Alertmanager, Loki, Fluent Bit, n8n, Ollama, Embedding, Reranker, SmolDocling, Unstructured, API, Web) is in the table.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Quick Reference section with service table and commands"
```

### Task 3: Docker Environment (Section 2)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/docker-quickstart.md`, `docs/deployment/docker-services.md`, `docs/deployment/docker-volume-management.md`, `infra/docker-compose.yml`, all `infra/compose.*.yml`

- [ ] **Step 1: Read source files**

Read all source files. Extract: compose profiles with services and resource totals, network topology, named volumes with purpose and backup priority, lifecycle commands.

- [ ] **Step 2: Write Section 2 — Docker Environment**

Replace TODO with:

1. **Compose Profiles** — table: Profile | Services | RAM | CPU | Use Case (minimal, dev, observability, ai, automation, full)
2. **Network Topology** — which services communicate, internal vs exposed ports
3. **Named Volumes** — table with actual count from docker-compose.yml: Volume | Service | Purpose | Backup Priority (critical/daily/weekly/none)
4. **Lifecycle** — commands for start, stop, rebuild, prune, profile switching
5. **Docker Daemon Log Rotation** — `/etc/docker/daemon.json` config with `log-driver: json-file`, `max-size: 10m`, `max-file: 3`

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Docker Environment section with profiles, volumes, lifecycle"
```

---

## Chunk 3: Core Data Services (Sections 3-5)

### Task 4: PostgreSQL (Section 3)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/runbooks/backup-restore.md`, `docs/deployment/runbooks/scaling.md`, `docs/deployment/runbooks/troubleshooting.md`, `docs/deployment/capacity-planning.md`, `docs/deployment/README.md` (connection pooling table), `infra/docker-compose.yml` (postgres service)

- [ ] **Step 1: Read source files and extract PostgreSQL-specific content**

- [ ] **Step 2: Write Section 3 — PostgreSQL**

Fill all 8 subsections of the standard service template:

**Overview**: image `postgres:16-alpine`, port 5432, volume `postgres_data`, config via env vars in `database.secret`, depends on nothing, health check `pg_isready`.

**Daily Operations**: Check connections (`pg_stat_activity`), review slow queries (`pg_stat_statements`), monitor WAL size, check disk usage.

**Backup & Restore**: Daily 3:00 AM via `pg_dumpall | gzip`, 7-day retention. Manual backup command. Restore: stop API → `gunzip | psql` → verify → restart. Integrity check with `pg_dump --clean --if-exists`.

**Scaling & Performance Tuning**: 3-tier config (small/medium/large with specific `shared_buffers`, `work_mem`, `effective_cache_size` values). Connection pooling: Npgsql params (`Pooling=true`, `MinPoolSize`, `MaxPoolSize`, `ConnectionIdleLifetime`). Resource limits from docker-compose.

**Upgrade & Migration**: `docker compose pull postgres`, pre-upgrade: backup + test restore, major version upgrade needs `pg_upgrade` or dump/restore. Rollback: restore from backup.

**Troubleshooting**: Table with: connection refused → check container status; too many connections → kill idle (`pg_terminate_backend`); slow queries → `EXPLAIN ANALYZE` + add indexes; disk full → `VACUUM FULL` + increase volume; WAL growth → check replication slots.

**Disaster Recovery**: Restore from latest backup (RTO 30-60min, RPO 24h). Commands for complete DB rebuild from backup.

**Cost Notes**: RAM 2-5GB depending on tier, disk ~39MB/user for storage. Hetzner storage €0.052/GB/month.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write PostgreSQL section with backup, tuning, troubleshooting"
```

### Task 5: Redis (Section 4)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `infra/redis/redis.conf`, `infra/docker-compose.yml` (redis service), runbooks

- [ ] **Step 1: Read redis.conf and docker-compose redis config**

- [ ] **Step 2: Write Section 4 — Redis**

**Overview**: image `redis:7-alpine`, port 6379, volume `redis_data`, config `infra/redis/redis.conf`, password from `redis.secret`.

**Daily Operations**: `redis-cli ping`, `INFO stats` (hit/miss ratio), `DBSIZE`, memory usage check.

**Backup & Restore**: `BGSAVE` + copy `dump.rdb`, restore by replacing file + restart.

**Scaling**: Max memory 768MB, eviction `allkeys-lru`. Persistence: AOF + RDB. Monitor evicted keys/sec.

**Upgrade**: Pull new image, restart. Redis is backward-compatible for minor versions.

**Troubleshooting**: High memory → check `--bigkeys` → flush if cache-only; connection refused → check password in secret; slow → check `SLOWLOG GET 10`.

**Disaster Recovery**: Cache rebuilds automatically on restart. Only persistent data (sessions) needs backup.

**Cost Notes**: RAM 768MB fixed, minimal CPU. Negligible cost.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Redis section"
```

### Task 6: Qdrant (Section 5)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `infra/docker-compose.yml` (qdrant service), runbooks, capacity-planning

- [ ] **Step 1: Read qdrant config from docker-compose**

- [ ] **Step 2: Write Section 5 — Qdrant**

**Overview**: image `qdrant/qdrant:latest`, ports 6333 (HTTP) + 6334 (gRPC), volume `qdrant_data`, API key from `qdrant.secret`. Collection `meepleai_documents`, embedding dimension 1024, HNSW M=16.

**Daily Operations**: Health check `curl localhost:6333/healthz`, collection info, point count.

**Backup & Restore**: Snapshot API `POST /snapshots`, restore via snapshot recover endpoint. Daily backup, 7-day retention.

**Scaling**: RAM ~390KB per user (156 vectors × 2.5KB). Thresholds: <10K users = 4GB in-memory; 10K-50K = mmap or quantization; >50K = sharding. Enable quantization with `scalar` type for 75% memory reduction.

**Upgrade**: Pull new image, restart. Check release notes for breaking changes.

**Troubleshooting**: Slow search → check HNSW params, consider quantization; high memory → enable mmap; collection not found → verify collection name.

**Disaster Recovery**: Restore from snapshot (RTO ~15min for small collections).

**Cost Notes**: RAM 1-6GB depending on vector count. Disk minimal (<1GB for <10K users).

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Qdrant section"
```

---

## Chunk 4: Infrastructure Services (Sections 6-8)

### Task 7: Traefik (Section 6)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `infra/traefik/`, `infra/docker-compose.yml` (traefik service), runbooks

- [ ] **Step 1: Read Traefik config files**

Read `infra/traefik/` directory contents, dynamic config, TLS settings.

- [ ] **Step 2: Write Section 6 — Traefik**

**Overview**: image, ports 80/443/8080 (dashboard), config in `infra/traefik/`, TLS via Let's Encrypt.

**Daily Operations**: Dashboard at `:8080`, check router/service status, access logs.

**Certificate Management** (extra subsection): Let's Encrypt auto-renewal (90 days). Check expiry: `openssl s_client -connect domain:443 | openssl x509 -noout -dates`. Manual renewal: restart Traefik. Troubleshooting: rate limits, DNS propagation.

**Troubleshooting**: 502 Bad Gateway → check backend service health; certificate errors → check ACME config; routing issues → check dynamic config labels.

Fill remaining subsections with standard template content.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Traefik section with certificate management"
```

### Task 8: Prometheus, Grafana, Alertmanager & Sidecars (Section 7)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `infra/prometheus.yml`, `infra/alertmanager.yml`, `infra/prometheus/alerts/*.yml`, `docs/deployment/monitoring-quickstart.md`, `docs/deployment/monitoring-reference.md`, `infra/grafana/`

- [ ] **Step 1: Read all monitoring config files**

Read Prometheus config (scrape targets, retention), Alertmanager config (routes, receivers), all alert rule files (12 categories), Grafana provisioning.

- [ ] **Step 2: Write Section 7 — Prometheus, Grafana, Alertmanager & Observability Sidecars**

**Overview**: Prometheus (9090), Grafana (3001), Alertmanager (9093). Volumes: `prometheus_data`, `grafana_data`, `alertmanager_data`.

**Daily Operations**: Check Prometheus targets (`/targets`), Grafana dashboards, active alerts (`/api/v1/alerts`).

**Backup & Restore**: Grafana: backup `grafana_data` volume (dashboards, datasources). Prometheus: retention 30 days/5GB, rarely needs backup. Alertmanager: config in git.

**Alert Catalog**: Table of all pre-built alerts from `infra/prometheus/alerts/` with: Alert Name | Condition | Duration | Severity.

**Key PromQL Queries**: Error rate, latency P95, cache hit rate, DB connections, disk usage (copy exact queries from extracted content).

**Sidecars**: Brief subsection for each:
- cadvisor: container metrics, port, no config needed
- node-exporter: host metrics, port 9100
- HyperDX: unified observability, ports 8180/14317/14318, 4GB RAM, volumes
- socket-proxy: Docker socket security proxy

**Troubleshooting**: Prometheus OOM → reduce retention/scrape interval; Grafana dashboard missing → check provisioning; alerts not firing → check Alertmanager routes.

Fill remaining standard subsections: Scaling & Performance Tuning, Upgrade & Migration, Disaster Recovery, Cost Notes.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write monitoring stack section with alerts catalog and PromQL queries"
```

### Task 9: Loki & Fluent Bit (Section 8)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/log-aggregation-guide.md`, `infra/compose.logging.yml` (if exists), Loki/Fluent Bit config

- [ ] **Step 1: Read log aggregation guide and config**

- [ ] **Step 2: Write Section 8 — Loki & Fluent Bit**

**Overview**: Loki (3100) for log storage, Fluent Bit for collection. compose.logging.yml profile. Architecture: containers → Fluent Bit → Loki → Grafana.

**Daily Operations**: Check Loki health, verify log ingestion, Grafana Explore for log queries.

**LogQL Patterns**: API logs, error filtering, Python service logs, slow request extraction (copy from extracted content).

**Retention**: 30 days/720h. Storage management.

Fill remaining subsections.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Loki & Fluent Bit section with LogQL patterns"
```

---

## Chunk 5: Application Services (Sections 9-14)

### Task 10: n8n + Ollama (Sections 9-10)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `infra/docker-compose.yml` (n8n, ollama services), `infra/n8n/`

- [ ] **Step 1: Read n8n and Ollama config from docker-compose**

- [ ] **Step 2: Write Section 9 — n8n**

**Overview**: Workflow automation, port 5678, config in `infra/n8n/`.

**Backup**: Export workflows via API or copy volume. Restore by importing.

**Upgrade**: Pull new image, check breaking changes in n8n changelog.

Fill remaining subsections. n8n is a lighter service — subsections can be brief.

- [ ] **Step 3: Write Section 10 — Ollama**

**Overview**: LLM runtime, volume `ollama_data`, 8GB RAM limit, 4 CPUs.

**Model Management**: `ollama pull <model>`, `ollama list`, `ollama rm <model>`. Init container pattern (`ollama-pull` service).

**Disk Footprint**: 10-50GB per model. Monitor with `docker exec ollama du -sh /root/.ollama/models/`.

**Upgrade**: Pull new Ollama image. Models persist in volume.

Fill remaining subsections.

- [ ] **Step 4: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write n8n and Ollama sections"
```

### Task 11: AI/ML Services (Sections 11-14)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `infra/docker-compose.yml` (embedding, reranker, smoldocling, unstructured services)

- [ ] **Step 1: Read AI service configs from docker-compose**

Extract: image names, ports, volumes, resource limits, environment variables, health checks.

- [ ] **Step 2: Write Sections 11-14 — Embedding, Reranker, SmolDocling, Unstructured**

These are Python services with similar patterns. For each:

**Overview**: Image, port (Embedding: 8000, Reranker: 8003, SmolDocling: 8004, Unstructured: 8001), volumes for models/temp, resource limits.

**Daily Operations**: Health check endpoint, log review.

**Backup**: Model volumes only (models can be re-downloaded). Config in git.

**Scaling**: Resource limits (CPU/memory), batch size tuning.

**Upgrade**: Pull new image. Check model compatibility.

**Troubleshooting**: OOM → increase memory limit or reduce batch size; model load failure → check volume mount; slow inference → check GPU availability or batch size.

**Cost Notes**: RAM 2-4GB each (embedding heaviest). CPU-intensive during inference.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Embedding, Reranker, SmolDocling, Unstructured sections"
```

---

## Chunk 6: Operations & Cross-Cutting (Sections 15-18)

### Task 12: Secrets Management (Section 15)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/howto-secrets.md`, `docs/deployment/secrets-management.md`, `infra/secrets/README.md`

- [ ] **Step 1: Read secrets documentation**

- [ ] **Step 2: Write Section 15 — Secrets Management**

**Secret File System**: Count actual files by reading `infra/secrets/` directory. Organize in 3 tiers (Critical/Important/Optional). Table with: File | Variables | Tier | Rotation.

**Setup**: `pwsh setup-secrets.ps1 -SaveGenerated`, `pwsh generate-env-from-secrets.ps1`.

**Rotation Schedule**: JWT 90d, Database 180d, API keys on compromise, Admin 90d, Other 365d. Zero-downtime rotation procedure for database password.

**Recovery**: From password manager, re-run setup script, restart services.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Secrets Management section"
```

### Task 13: Routine Maintenance (Section 16)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/runbooks/maintenance.md`

- [ ] **Step 1: Read maintenance runbook**

- [ ] **Step 2: Write Section 16 — Routine Maintenance Schedule**

**Daily** (automated + manual check):
- Health checks (all services): verify via Quick Reference table
- Log review: `docker compose logs --since 24h | grep -i error`
- Backup completion: verify files exist and are recent
- Docker log rotation: verify daemon.json config

**Weekly**:
- Disk usage: `docker system df`, `df -h`
- Backup integrity: test restore of latest PostgreSQL backup
- Error log analysis: patterns, recurring issues
- SSL cert expiry: `openssl s_client` check >30 days

**Monthly**:
- Docker image updates: `docker compose pull && docker compose up -d`
- Ubuntu security patches: `apt update && apt upgrade`
- PostgreSQL `VACUUM FULL ANALYZE`
- Performance metrics review in Grafana
- Dependency audit

**Quarterly**:
- Secret rotation (per schedule in Section 15)
- Disaster recovery test: full restore drill
- Capacity review: project growth vs resources
- Security review: check for CVEs in images

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Routine Maintenance Schedule"
```

### Task 14: Incident Response (Section 17)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: all 6 files in `docs/operations/runbooks/`, `docs/deployment/runbooks/incident-response.md`

- [ ] **Step 1: Read all incident runbooks**

Read all 7 incident-related runbooks. Extract: severity levels, alert conditions, diagnostic commands, remediation steps.

- [ ] **Step 2: Write Section 17 — Incident Response**

**Severity Levels**: Table P1-P4 with thresholds and response times.

**Diagnostic Flowchart** (text-based):
```
Service Down? → Check docker compose ps → Container running?
  → No: docker compose up -d <service> → Still down? Check logs
  → Yes: Check health endpoint → Healthy?
    → No: Check logs → DB issue? → PostgreSQL section
    → Yes: Check upstream/downstream dependencies
```

**6 Incident Scenarios** (migrated from runbooks):
1. Infrastructure monitoring alerts — CPU/RAM/disk thresholds, commands
2. Error spike (3x baseline) — endpoint correlation, deployment tracking, rollback
3. High error rate (>5%) — endpoint breakdown, service restart, DB check
4. Slow performance (P95 >1s) — query analysis, cache check, `EXPLAIN ANALYZE`
5. AI quality degradation (<0.70) — embedding quality, reranker scoring, config tuning
6. External dependency down — health checks, fallback activation, circuit breaker

Each scenario: Alert condition → Diagnostic commands → Remediation steps → Post-incident.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Incident Response section with 6 scenarios"
```

### Task 15: Disaster Recovery (Section 18)

**Files:**
- Modify: `docs/operations/operations-manual.md`
- Read: `docs/deployment/runbooks/disaster-recovery.md`

- [ ] **Step 1: Read DR runbook**

- [ ] **Step 2: Write Section 18 — Disaster Recovery Master Plan**

**RTO/RPO Targets**: Table per service: Service | RTO | RPO | Backup Method.
- PostgreSQL: RTO 30-60min, RPO 24h
- Redis: RTO 5min (cache rebuilds), RPO N/A
- Qdrant: RTO 15min, RPO 24h
- Overall system: RTO <2h, RPO <24h

**Data Priority**: P1 (PostgreSQL, secrets), P2 (Qdrant, PDFs), P3 (Grafana dashboards, n8n workflows), P4 (logs, metrics — can be lost).

**Full Restore From Zero**:
1. Provision new VPS (Hetzner)
2. Install Docker + Docker Compose
3. Clone repository
4. Restore secrets from password manager
5. `docker compose pull`
6. Restore PostgreSQL from backup
7. Restore Qdrant from snapshot
8. Restore PDF uploads from S3/backup
9. `docker compose up -d`
10. Verify all health endpoints
11. Update DNS if IP changed

**4 Disaster Scenarios** (from extracted content):
1. VPS Complete Failure (RTO 1-2h)
2. Database Corruption (RTO 30-60min)
3. Ransomware/Compromise (RTO 2-4h)
4. Accidental Data Deletion (RTO 30-60min)

**Recovery Verification Checklist**: List of checks to run after restore.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/operations-manual.md
git commit -m "docs(ops): write Disaster Recovery Master Plan"
```

---

## Chunk 7: Cleanup & PR

### Task 16: Delete Migrated Files

**Files:**
- Delete: 24 files listed in spec Section 6 "Migrated"

- [ ] **Step 1: Verify all content migrated**

For each of the 24 files listed in the spec's "Migrated" table, verify the operations manual contains equivalent content. Grep for key unique terms from each source file in the manual.

- [ ] **Step 2: Delete files**

```bash
# Operations runbooks (6)
rm docs/operations/runbooks/infrastructure-monitoring.md
rm docs/operations/runbooks/error-spike.md
rm docs/operations/runbooks/high-error-rate.md
rm docs/operations/runbooks/slow-performance.md
rm docs/operations/runbooks/ai-quality-low.md
rm docs/operations/runbooks/dependency-down.md

# Deployment runbooks (6)
rm docs/deployment/runbooks/backup-restore.md
rm docs/deployment/runbooks/disaster-recovery.md
rm docs/deployment/runbooks/incident-response.md
rm docs/deployment/runbooks/maintenance.md
rm docs/deployment/runbooks/scaling.md
rm docs/deployment/runbooks/troubleshooting.md

# Deployment docs (12)
rm docs/deployment/docker-quickstart.md
rm docs/deployment/docker-services.md
rm docs/deployment/docker-volume-management.md
rm docs/deployment/monitoring-quickstart.md
rm docs/deployment/monitoring-reference.md
rm docs/deployment/health-checks.md
rm docs/deployment/capacity-planning.md
rm docs/deployment/deployment-cheatsheet.md
rm docs/deployment/deployment-quick-reference.md
rm docs/deployment/log-aggregation-guide.md
rm docs/deployment/howto-secrets.md
rm docs/deployment/secrets-management.md
```

- [ ] **Step 3: Remove empty directories if any**

```bash
# Remove runbooks dirs if empty
rmdir docs/operations/runbooks 2>/dev/null || true
rmdir docs/deployment/runbooks 2>/dev/null || true
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore(docs): delete 24 migrated files absorbed into operations manual"
```

### Task 17: Update Cross-References

**Files:**
- Modify: `docs/deployment/README.md`
- Modify: `CLAUDE.md`
- Check: any other docs referencing deleted files

- [ ] **Step 1: Find broken references**

```bash
# Search for references to ALL 24 deleted files across the codebase
grep -r "backup-restore\|disaster-recovery\|incident-response\|maintenance\|scaling\|troubleshooting\|docker-quickstart\|docker-services\|docker-volume-management\|monitoring-quickstart\|monitoring-reference\|health-checks\|capacity-planning\|deployment-cheatsheet\|deployment-quick-reference\|log-aggregation-guide\|howto-secrets\|secrets-management\|infrastructure-monitoring\|error-spike\|high-error-rate\|slow-performance\|ai-quality-low\|dependency-down" docs/ --include="*.md" -l
```

- [ ] **Step 2: Update `docs/deployment/README.md`**

Add a note pointing to the operations manual:
```markdown
> **Operations & Maintenance**: See [Operations Manual](../operations/operations-manual.md) for service management, backup/restore, monitoring, incident response, and routine maintenance procedures.
```

Remove any sections that duplicate the operations manual content.

- [ ] **Step 3: Update `CLAUDE.md`**

In the Troubleshooting section, add reference:
```markdown
| Operations Manual | `docs/operations/operations-manual.md` |
```

- [ ] **Step 4: Fix any broken references found in Step 1**

Update links in other docs to point to the operations manual instead of deleted files.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: update cross-references to point to operations manual"
```

### Task 18: PR to main-dev

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/operations-manual
```

- [ ] **Step 2: Create PR**

```bash
gh pr create --base main-dev --title "docs: centralized Operations Manual with CI PDF generation" --body "$(cat <<'EOF'
## Summary
- Single operations manual covering all 14 Docker services with uniform template
- CI pipeline generates PDF via md-to-pdf on push (workflow artifact + GitHub Release)
- Migrated and deleted 24 duplicate/scattered runbook files
- New content: routine maintenance schedule, cost notes, DR master plan, certificate management

## Files
- **Created**: `docs/operations/operations-manual.md`, `.md-to-pdf.js`, `generate-operations-pdf.yml`
- **Deleted**: 24 markdown files (6 ops runbooks + 6 deployment runbooks + 12 deployment guides)
- **Modified**: `docs/deployment/README.md`, `CLAUDE.md` (cross-references)

## Spec
`docs/superpowers/specs/2026-03-16-operations-manual-design.md`

## Test plan
- [ ] Verify all service sections present with uniform template
- [ ] Spot-check migrated content against deleted source files
- [ ] Verify CI workflow runs on push (check GitHub Actions)
- [ ] Download generated PDF and verify formatting
- [ ] Grep for broken references to deleted files
- [ ] Verify no duplicate operational content remains

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
