# MeepleAI Operations Manual — Design Spec

**Date**: 2026-03-16
**Author**: Claude + User
**Status**: Approved
**Scope**: Centralized operations manual covering all services, with CI-generated PDF

---

## 1. Problem Statement

MeepleAI has extensive but scattered operational documentation:
- 6 incident response runbooks in `docs/operations/runbooks/`
- 6 deployment runbooks in `docs/deployment/runbooks/`
- Service-specific docs spread across `docs/deployment/`, `docs/development/`, and `infra/`
- No centralized reference for routine maintenance, service management, or operational procedures
- Existing runbooks are incident-focused; routine procedures (backup schedules, health checks, upgrades) are missing

**Goal**: A single, comprehensive operations manual that serves as the go-to reference for managing all MeepleAI services, with automatic PDF generation for offline access.

## 2. Target Audience

Solo developer (project owner) with full familiarity of the stack. The manual assumes knowledge of Docker, .NET, Node.js, PostgreSQL, and the MeepleAI architecture. It focuses on **procedures and commands**, not explanations of concepts.

## 3. Output Artifacts

| Artifact | Path | Notes |
|----------|------|-------|
| Markdown source | `docs/operations/operations-manual.md` | Single file, ~4000-5500 lines |
| Generated PDF | GitHub Release artifact (on `main`) / Workflow artifact (on `main-dev`, 90-day retention) | |
| CI workflow | `.github/workflows/generate-operations-pdf.yml` | Triggers on manual changes |
| PDF tool config | `docs/operations/.md-to-pdf.js` | md-to-pdf styling config |

## 4. Document Structure

```
# MeepleAI Operations Manual

## 1. Quick Reference
   - Service table (port, volume, health endpoint, restart command)
   - Essential Docker commands cheat sheet
   - Service dependency map

## 2. Docker Environment
   - Compose profiles (minimal, dev, observability, ai, automation, full)
   - Network topology
   - Named volumes (verify actual count from docker-compose.yml, purpose and backup priority)
   - Lifecycle: start, stop, rebuild, prune
   - Docker daemon log rotation (json-file driver max-size/max-file)

## 3. PostgreSQL
   (standard service template)
   + Connection pooling config (Npgsql parameters)

## 4. Redis
   (standard service template)

## 5. Qdrant
   (standard service template)

## 6. Traefik
   (standard service template)
   + Certificate Management subsection (auto-renewal, expiry monitoring, manual renewal)

## 7. Prometheus, Grafana, Alertmanager & Observability Sidecars
   (standard service template)
   Includes: cadvisor, node-exporter, HyperDX (if enabled), socket-proxy
   Brief subsection per sidecar (image, port, volume, resource limits)

## 8. Loki & Fluent Bit (Log Aggregation)
   (standard service template)
   + LogQL query patterns
   + Retention policy (30 days)
   + compose.logging.yml profile

## 9. n8n
   (standard service template)

## 10. Ollama
    (standard service template)
    + Model management (pull, list, delete)
    + ollama-pull init container pattern
    + Disk footprint management (10-50 GB per model)

## 11. Embedding Service
    (standard service template)

## 12. Reranker Service
    (standard service template)

## 13. SmolDocling Service
    (standard service template)

## 14. Unstructured Service
    (standard service template)

## 15. Secrets Management
    - .secret file system (10 files, priority tiers)
    - Setup script usage
    - Rotation schedule (90-day cycle)
    - Recovery from lost secrets

## 16. Routine Maintenance Schedule
    - Daily: health checks, log review, Docker log rotation verification
    - Weekly: disk usage, backup verification
    - Monthly: security updates, dependency audit, SSL cert expiry check
    - Quarterly: secret rotation, capacity review

## 17. Incident Response
    - Diagnostic flowchart
    - Alert thresholds (from Prometheus rules)
    - 6 incident scenarios (migrated from existing runbooks):
      1. Infrastructure monitoring alerts
      2. Error spike (3x baseline)
      3. High error rate (>5%)
      4. Slow performance (P95 >1s)
      5. AI quality degradation (<0.70 confidence)
      6. External dependency down

## 18. Disaster Recovery Master Plan
    - RTO/RPO targets per service
    - Full restore from zero procedure
    - Data priority classification
    - Recovery verification checklist
```

## 5. Standard Service Template

Every service section follows this uniform structure:

```markdown
## [Service Name]

### Overview
- Docker image and tag
- Port mapping (host:container)
- Named volume(s)
- Config file location
- Dependencies (which services must be running)
- Health check endpoint/command
- TLS/Proxy notes (if exposed via Traefik: route, cert dependency)

### Daily Operations
- Health check commands
- Log review (`docker compose logs <service>`)
- Key metrics to monitor (Grafana dashboard / Prometheus query)

### Backup & Restore
- What to back up (volume, config, data)
- Manual backup command
- Scheduled backup setup
- Restore procedure
- Integrity verification

### Scaling & Performance Tuning
- Key configuration parameters
- Resource limits (CPU, memory)
- Performance optimization tips
- Benchmark baselines

### Upgrade & Migration
- How to upgrade the Docker image
- Pre-upgrade checklist
- Known breaking changes by version
- Rollback procedure

### Troubleshooting
- Common issues table (symptom → diagnosis → fix)
- Log analysis patterns
- Connection debugging

### Disaster Recovery
- Service-specific recovery procedure
- Data loss implications
- Recovery time estimate

### Cost Notes
- Resource consumption (CPU, memory, disk)
- Estimated hosting cost (VPS/cloud)
- Cost optimization opportunities
```

## 6. Content Sources

### Migrated (absorbed then deleted)

| Source | Destination in Manual |
|--------|----------------------|
| `docs/operations/runbooks/infrastructure-monitoring.md` | Section 17 (Incident Response) |
| `docs/operations/runbooks/error-spike.md` | Section 17 |
| `docs/operations/runbooks/high-error-rate.md` | Section 17 |
| `docs/operations/runbooks/slow-performance.md` | Section 17 |
| `docs/operations/runbooks/ai-quality-low.md` | Section 17 |
| `docs/operations/runbooks/dependency-down.md` | Section 17 |
| `docs/deployment/runbooks/backup-restore.md` | Service Backup & Restore sections |
| `docs/deployment/runbooks/disaster-recovery.md` | Section 18 (DR Master Plan) |
| `docs/deployment/runbooks/incident-response.md` | Section 17 |
| `docs/deployment/runbooks/maintenance.md` | Section 16 (Routine Maintenance) |
| `docs/deployment/runbooks/scaling.md` | Service Scaling sections |
| `docs/deployment/runbooks/troubleshooting.md` | Service Troubleshooting sections |
| `docs/deployment/docker-quickstart.md` | Section 2 (Docker Environment) |
| `docs/deployment/docker-services.md` | Section 2 + Service Overviews |
| `docs/deployment/docker-volume-management.md` | Section 2 (Named Volumes) |
| `docs/deployment/monitoring-quickstart.md` | Section 7 (Prometheus/Grafana) |
| `docs/deployment/monitoring-reference.md` | Section 7 |
| `docs/deployment/health-checks.md` | Section 1 (Quick Reference) + Service sections |
| `docs/deployment/capacity-planning.md` | Service Cost Notes + Section 16 |
| `docs/deployment/deployment-cheatsheet.md` | Section 1 (Quick Reference) + Section 2 |
| `docs/deployment/deployment-quick-reference.md` | Section 1 (Quick Reference) |
| `docs/deployment/log-aggregation-guide.md` | Section 8 (Loki & Fluent Bit) |
| `docs/deployment/howto-secrets.md` | Section 15 (Secrets Management) |
| `docs/deployment/secrets-management.md` | Section 15 (Secrets Management) |

### Referenced (not deleted, remain as setup guides)

These stay because they cover initial setup, not operations:
- `docs/deployment/production-deployment-meepleai.md`
- `docs/deployment/setup-guide-*.md`
- `docs/deployment/domain-setup-guide.md`
- `docs/deployment/r2-storage-configuration-guide.md`
- `docs/deployment/email-totp-services.md`
- `docs/deployment/deployment-workflows-guide.md`
- `docs/deployment/cicd-pipeline-hardening.md`

### New content (gap-filling)

Content that does not exist and must be written from scratch:
- PostgreSQL: vacuum, routine maintenance, query monitoring, upgrade procedure, connection pooling config
- Redis: persistence config, memory management, troubleshooting patterns
- Qdrant: collection backup, reindex procedure, performance tuning
- Traefik: certificate management (auto-renewal, expiry check, manual renewal), routing troubleshooting
- Loki & Fluent Bit: operational procedures (retention, query patterns, scaling)
- Ollama: model management, disk footprint, upgrade, init container pattern
- Observability sidecars: cadvisor, node-exporter, HyperDX brief operational notes
- n8n: workflow backup, upgrade procedure
- Embedding/Reranker: model update procedure, performance baselines
- SmolDocling/Unstructured: resource tuning, troubleshooting
- Docker daemon log rotation configuration
- Routine Maintenance Schedule (daily/weekly/monthly/quarterly)
- Disaster Recovery Master Plan (RTO/RPO, full restore from zero)
- Cost Notes for all services

## 7. CI Pipeline

### Workflow: `generate-operations-pdf.yml`

```yaml
name: Generate Operations Manual PDF

on:
  push:
    paths:
      - 'docs/operations/operations-manual.md'
      - 'docs/operations/.md-to-pdf.js'
    branches: [main, main-dev]
  workflow_dispatch: # manual trigger

jobs:
  generate-pdf:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - name: Install Chromium dependencies
        run: |
          sudo apt-get update
          sudo apt-get install -y libgbm-dev libnss3 libatk-bridge2.0-0 libxss1
      - run: npm install -g md-to-pdf
      - run: |
          cd docs/operations
          md-to-pdf operations-manual.md --config-file .md-to-pdf.js

      # Always upload as workflow artifact
      - uses: actions/upload-artifact@v4
        with:
          name: operations-manual-pdf
          path: docs/operations/operations-manual.pdf
          retention-days: 90

      # On main branch: also upload as GitHub Release asset
      - name: Upload to GitHub Release
        if: github.ref == 'refs/heads/main'
        uses: softprops/action-gh-release@v2
        with:
          tag_name: ops-manual-${{ github.run_number }}
          name: Operations Manual (Build ${{ github.run_number }})
          files: docs/operations/operations-manual.pdf
          draft: false
          prerelease: false
```

### PDF Styling Config (`.md-to-pdf.js`)

```js
module.exports = {
  stylesheet: [],
  css: `
    body { font-family: 'Segoe UI', sans-serif; font-size: 11pt; }
    h1:not(:first-of-type) { page-break-before: always; }
    h1 { border-bottom: 2px solid #333; }
    h2 { border-bottom: 1px solid #ccc; margin-top: 1.5em; }
    table { border-collapse: collapse; width: 100%; }
    th, td { border: 1px solid #ddd; padding: 6px 10px; }
    code { background: #f4f4f4; padding: 2px 4px; border-radius: 3px; }
    pre { background: #f4f4f4; padding: 12px; border-radius: 4px; }
  `,
  pdf_options: {
    format: 'A4',
    margin: { top: '20mm', bottom: '20mm', left: '15mm', right: '15mm' },
    printBackground: true,
    headerTemplate: '<div style="font-size:8pt;text-align:center;width:100%;">MeepleAI Operations Manual</div>',
    footerTemplate: '<div style="font-size:8pt;text-align:center;width:100%;"><span class="pageNumber"></span> / <span class="totalPages"></span></div>',
    displayHeaderFooter: true,
    launch_options: {
      args: ['--no-sandbox', '--disable-setuid-sandbox'],
    },
  },
  dest: 'operations-manual.pdf',
};
```

## 8. Estimated Size

- Markdown: ~4000-5500 lines (increased due to added services: Ollama, Loki, sidecars)
- PDF: ~60-80 pages
- Migrated content: ~35% (from existing docs)
- New content: ~65% (gap-filling)

## 9. Cleanup Plan

After the manual is complete and verified:

1. Delete `docs/operations/runbooks/` (6 files)
2. Delete `docs/deployment/runbooks/` (6 files)
3. Delete migrated deployment docs (12 files listed in Section 6 "Migrated")
4. Update `docs/deployment/README.md` to reference the operations manual
5. Update `docs/operations/` README or remove if only manual remains
6. Update `CLAUDE.md` troubleshooting section to reference manual
7. Verify no other docs link to deleted files (grep for filenames)

**Total files removed**: ~24 markdown files
**Files added**: 3 (operations-manual.md, workflow yml, pdf config)

## 10. Implementation Notes

### Volume Count
The named volume count must be verified directly from `infra/docker-compose.yml` and all compose override files before writing Section 2. The previous estimate of "13" is likely an undercount given Ollama, HyperDX, and other service volumes.

### Content Migration Process
For each migrated file:
1. Read the source file completely
2. Reorganize content into the standard service template structure
3. Merge overlapping content (e.g., multiple files covering PostgreSQL backup)
4. Fill gaps with new content
5. Verify no unique information is lost
6. Mark source file for deletion

### DB Connection Pooling
The `docs/deployment/README.md` contains a Npgsql connection pooling configuration table. This operational content must be explicitly migrated into the PostgreSQL section (Section 3) of the manual. The README itself stays (it's a setup guide), but the pooling table content is duplicated into the manual.

## 11. Success Criteria

- [ ] Single markdown file covers all services (PostgreSQL, Redis, Qdrant, Traefik, Prometheus/Grafana/Alertmanager + sidecars, Loki/Fluent Bit, n8n, Ollama, Embedding, Reranker, SmolDocling, Unstructured) with uniform template
- [ ] All existing runbook content migrated without loss
- [ ] Gap content written for routine procedures, cost notes, DR plan
- [ ] CI generates valid PDF on push (verified on GitHub Actions)
- [ ] PDF is well-formatted with TOC, headers, footers, page numbers, no blank first page
- [ ] Duplicated source files deleted (24 files)
- [ ] Cross-references in other docs updated
- [ ] Volume count verified from actual docker-compose.yml
