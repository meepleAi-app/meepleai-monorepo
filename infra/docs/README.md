# Infrastructure Documentation

## Contenuto

Documentazione relativa all'infrastruttura, deployment e operazioni.

## Struttura

```
docs/
└── archive/                  Documentazione archiviata
    └── README.md             (vedi sottocartella)
```

## Documentazione Principale

La documentazione principale dell'infrastruttura si trova in:

### `/infra` (Root)

- **README.md**: Overview completo infrastruttura, quick start, servizi
- **INFRASTRUCTURE.md**: Documentazione dettagliata (50+ pagine)
  - Architecture
  - Service configurations
  - Networking
  - Security
  - Troubleshooting

### `/docs` (Repository Root)

Documentazione organizzata per topic:

#### Deployment & Operations
- `docs/05-operations/deployment/`
  - `multi-environment-strategy.md` - Dev, Staging, Production strategy
  - `board-game-ai-deployment-guide.md` - Deployment completo
  - `disaster-recovery.md` - DR procedures
  - `blue-green-deployment.md` - Zero-downtime deployment

#### Runbooks
- `docs/05-operations/runbooks/`
  - `api-down.md` - API non risponde
  - `database-down.md` - PostgreSQL failure
  - `high-error-rate.md` - Error rate elevato
  - `n8n-workflow-failure.md` - Workflow n8n failure
  - `qdrant-performance-degradation.md` - Qdrant slow

#### Monitoring
- `docs/05-operations/monitoring/`
  - `prometheus-setup.md` - Configurazione Prometheus
  - `alerting-strategy.md` - Alert rules e routing
  - `grafana-dashboards.md` - Dashboard overview
  - `metrics-reference.md` - Custom metrics reference

#### Security
- `docs/06-security/`
  - `secrets-management.md` - Docker secrets, Key Vault
  - `oauth-security.md` - OAuth integration security
  - `environment-variables-production.md` - Prod env vars
  - `code-scanning-remediation-summary.md` - Security scan results

## Archive (archive/)

Vedi `archive/README.md` per documentazione archiviata:
- Versioni outdated
- Pre-refactor documentation
- Historical reference

## Navigazione Documentazione

### Per Ruolo

**Developer**:
1. `/infra/README.md` - Quick start
2. `docs/02-development/` - Development guides
3. `/infra/env/README.md` - Environment setup

**DevOps/SRE**:
1. `/infra/INFRASTRUCTURE.md` - Architecture deep dive
2. `docs/05-operations/deployment/` - Deployment strategies
3. `docs/05-operations/runbooks/` - Incident response
4. `/infra/prometheus/README.md` - Monitoring

**Security Engineer**:
1. `docs/06-security/` - Security documentation
2. `/infra/secrets/README.md` - Secrets management
3. `SECURITY.md` (repo root) - Security policy

### Per Task

**Setup Development**:
1. `/infra/README.md` → Quick Start (Development)
2. `/infra/env/README.md` → Environment variables
3. `apps/api/README.md` → API setup
4. `apps/web/README.md` → Web setup

**Deploy to Production**:
1. `docs/05-operations/deployment/board-game-ai-deployment-guide.md`
2. `/infra/secrets/README.md` → Secrets setup
3. `/infra/README.md` → Production section
4. `docs/05-operations/deployment/disaster-recovery.md` → Backup plan

**Troubleshoot Issue**:
1. Identify issue category (API, DB, n8n, etc.)
2. Check relevant runbook: `docs/05-operations/runbooks/<issue>.md`
3. Check service-specific README: `/infra/<service>/README.md`
4. Check logs: Seq, Grafana, Prometheus

**Add New Service**:
1. `docs/01-architecture/overview/system-architecture.md` → Architecture patterns
2. `/infra/README.md` → Service structure
3. `/infra/prometheus/README.md` → Add monitoring
4. Update `docker-compose.yml`

## Documentazione Mancante?

Se non trovi documentazione per un topic:

1. **Check Index**: `docs/INDEX.md` - 115 docs indexed
2. **Search Repo**: `grep -r "keyword" docs/`
3. **Check Archive**: Potrebbe essere archiviato
4. **Ask Team**: Slack #engineering
5. **Create Issue**: Label `documentation`

## Contribuire

### Aggiungere Nuova Documentazione

1. **Determina Location**:
   - Infra-specific → `/infra/docs/`
   - General architecture → `docs/01-architecture/`
   - Operations → `docs/05-operations/`
   - Security → `docs/06-security/`

2. **Format**:
   - Usa Markdown
   - Include TOC per docs >500 righe
   - Add examples
   - Link related docs

3. **Template**:
   ```markdown
   # Title

   **Purpose**: Cosa spiega questo documento

   **Audience**: Chi dovrebbe leggerlo

   **Last Updated**: YYYY-MM-DD

   ## Overview
   ...

   ## Quick Start
   ...

   ## Detailed Guide
   ...

   ## Troubleshooting
   ...

   ## Related Documentation
   - [Link](path)
   ```

4. **Update Index**:
   - Aggiungi entry a `docs/INDEX.md`
   - Link da README rilevanti
   - Update search keywords

5. **Review**:
   - Technical accuracy
   - Spelling/grammar
   - Link funzionanti
   - Examples tested

### Archiviare Documentazione Outdated

Quando doc diventa outdated:

1. **Sposta** da location corrente a `archive/`
2. **Rinomina**: `archive/OUTDATED-<original-name>.md`
3. **Aggiungi Header**:
   ```markdown
   # ⚠️ ARCHIVED DOCUMENTATION

   **Status**: Outdated as of YYYY-MM-DD
   **Reason**: Refactoring, feature removed, superseded by X
   **Replacement**: [New Doc](path)
   ```
4. **Update Links**: Rimuovi o redirect link da altri docs
5. **Commit**: `docs: Archive outdated <doc-name>`

## Documentation Standards

### File Naming

- Lowercase con hyphens: `multi-environment-strategy.md`
- Descrittivo: `prometheus-setup.md` non `setup.md`
- Prefix se necessario: `api-deployment-guide.md`

### Structure

**Required Sections**:
- Overview / Purpose
- Quick Start (se applicabile)
- Detailed Content
- Troubleshooting (se applicabile)
- Related Documentation

**Optional Sections**:
- Prerequisites
- Examples
- Best Practices
- FAQ

### Linking

**Internal Links** (relative):
```markdown
[Deployment Guide](../../05-operations/deployment/board-game-ai-deployment-guide.md)
```

**External Links** (absolute):
```markdown
[Prometheus Docs](https://prometheus.io/docs/)
```

### Code Blocks

Sempre specifica language:
```markdown
```bash
docker compose up -d
`` `

`` `yaml
services:
  api:
    image: meepleai/api:latest
`` `
```

### Screenshots

Store in `docs/assets/images/`:
```markdown
![Grafana Dashboard](../assets/images/grafana-dashboard.png)
```

## Maintenance

### Quarterly Review

Ogni 3 mesi:

1. **Review All Docs**:
   - Check accuracy
   - Update outdated info
   - Fix broken links
   - Add missing examples

2. **Archive Outdated**:
   - Identify superseded docs
   - Move to `archive/`
   - Update links

3. **Update Index**:
   - `docs/INDEX.md`
   - Category organization
   - Search keywords

4. **Metrics**:
   - Docs count
   - Total pages
   - Last update date
   - Coverage gaps

### Ownership

| Category | Owner | Contact |
|----------|-------|---------|
| Infrastructure | DevOps Team | #devops |
| API | Backend Team | #backend |
| Web | Frontend Team | #frontend |
| Security | Security Team | #security |
| Operations | SRE Team | #sre |

## Esempio di utilizzo dell’applicazione

- Quando la dashboard `error-monitoring` (promossa da `infra/dashboards/`) indica che la MeepleAI API ha superato la soglia critica di 500, il team SRE segue `docs/05-operations/runbooks/high-error-rate.md` per eseguire i controlli riportati e verificare i log di `infra/scripts/load-secrets-env.sh`/`infra/prometheus/alerts/api-performance.yml` prima di scalare il servizio.

## Related Documentation

- `archive/README.md` - Archived documentation
- `../../docs/INDEX.md` - Complete documentation index
- `/infra/README.md` - Infrastructure overview
- `/infra/INFRASTRUCTURE.md` - Detailed infrastructure guide
