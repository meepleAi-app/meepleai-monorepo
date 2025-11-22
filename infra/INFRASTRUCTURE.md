# Infrastructure Directory - MeepleAI

**Versione**: 1.0
**Ultimo aggiornamento**: 2025-11-22
**Stato**: Production Ready

---

## Indice

1. [Panoramica](#panoramica)
2. [Struttura Directory](#struttura-directory)
3. [File Docker Compose](#file-docker-compose)
4. [Environment Files](#environment-files)
5. [Secrets Management](#secrets-management)
6. [Observability](#observability)
7. [Workflows n8n](#workflows-n8n)
8. [Scripts Utility](#scripts-utility)
9. [Quick Start](#quick-start)
10. [Deployment](#deployment)

---

## Panoramica

La directory `infra/` contiene tutta l'infrastruttura Docker, configurazioni di monitoring, workflow automation e script utility per MeepleAI.

### Componenti Principali

- **15 servizi Docker**: Postgres, Redis, Qdrant, Ollama, API, Web, n8n, Seq, Jaeger, Prometheus, Grafana, Alertmanager, Unstructured, SmolDocling, Embedding
- **3 ambienti**: Development, Staging, Production
- **7 Grafana dashboards**: Performance, errori, cache, AI quality
- **8 Prometheus alert rules**: API, database, cache, PDF processing
- **14 n8n workflow templates**: Automation, backup, monitoring
- **Docker Secrets**: Gestione sicura credenziali

---

## Struttura Directory

```
infra/
├── docker-compose.yml              # Compose principale (development)
├── docker-compose.dev.yml          # Override development (alternativo)
├── compose.staging.yml             # Override staging
├── compose.prod.yml                # Override production
├── compose.test.yml                # Override test/CI
├── docker-compose.infisical.yml    # POC secret management
│
├── env/                            # Environment variables per servizio
│   ├── README.md                   # Guida env files
│   ├── api.env.dev.example        # API development
│   ├── api.env.staging.example    # API staging
│   ├── api.env.prod.example       # API production
│   ├── api.env.ci.example         # API CI/CD
│   ├── web.env.dev.example        # Web development
│   ├── web.env.staging.example    # Web staging
│   ├── web.env.prod.example       # Web production
│   ├── n8n.env.dev.example        # n8n development
│   ├── n8n.env.staging.example    # n8n staging
│   ├── n8n.env.prod.example       # n8n production
│   ├── alertmanager.env.example   # Alertmanager (tutti ambienti)
│   └── infisical.env.example      # Infisical POC
│
├── secrets/                        # Docker Secrets (gitignored)
│   ├── README.md                   # Guida secrets management
│   ├── dev/                        # Secrets development
│   ├── staging/                    # Secrets staging
│   └── prod/                       # Secrets production
│
├── dashboards/                     # Grafana dashboards (7 files)
│   ├── api-performance.json       # API metrics
│   ├── error-monitoring.json      # Error rates, 5xx
│   ├── cache-optimization.json    # Redis cache stats
│   ├── ai-quality-monitoring.json # LLM quality metrics
│   ├── ai-rag-operations.json     # RAG pipeline
│   ├── infrastructure.json        # Resource usage
│   └── quality-metrics-gauges.json
│
├── prometheus/                     # Prometheus configuration
│   ├── prometheus.yml             # Main config
│   ├── prometheus-rules.yml       # Recording rules
│   └── alerts/                    # Alert rules (8 files)
│       ├── api-performance.yml
│       ├── database-health.yml
│       ├── cache-performance.yml
│       ├── infrastructure.yml
│       ├── pdf-processing.yml
│       ├── quality-metrics.yml
│       ├── vector-search.yml
│       └── prompt-management.yml
│
├── n8n/                            # n8n workflow automation
│   ├── templates/                  # Workflow templates (14 files)
│   │   ├── backup-automation.json
│   │   ├── bgg-game-sync.json
│   │   ├── cache-warming.json
│   │   ├── daily-reports.json
│   │   ├── data-export.json
│   │   ├── discord-webhook.json
│   │   ├── email-notification.json
│   │   ├── error-alerting.json
│   │   ├── health-monitor.json
│   │   ├── integration-slack-notifications.json
│   │   ├── pdf-processing-pipeline.json
│   │   ├── slack-notification.json
│   │   └── user-onboarding.json
│   └── workflows/                  # Active workflows
│       └── agent-explain-orchestrator.json
│
├── observability/                  # Monitoring config avanzata
│   └── grafana/
│       ├── dashboards/
│       │   └── http-retry-metrics.json
│       └── provisioning/
│           └── alerting/
│               └── http-retry-alerts.yaml
│
├── scripts/                        # Utility scripts
│   └── load-secrets-env.sh        # Load secrets into environment
│
├── init/                           # Initialization scripts
│   ├── postgres-init.sql          # DB init
│   └── n8n/                       # n8n webhooks init
│       ├── agent-explain-webhook.json
│       └── agent-qa-webhook.json
│
├── grafana-datasources.yml        # Prometheus + Jaeger datasources
├── grafana-dashboards.yml         # Dashboard provisioning
├── alertmanager.yml               # Alert routing config
├── INFRASTRUCTURE.md              # Questo file
└── README.md                       # Quick start guide

TOTALE:
- 5 docker-compose files
- 12+ env file examples
- 7 Grafana dashboards
- 8 Prometheus alert groups
- 14 n8n workflow templates
```

---

## File Docker Compose

### `docker-compose.yml` (Principale - Development)

**Scopo**: Stack completo per sviluppo locale

**Servizi** (15):
1. `postgres` - PostgreSQL 16.4
2. `qdrant` - Vector database v1.12.4
3. `redis` - Cache 7.4.1
4. `ollama` - LLM locale
5. `ollama-pull` - Scarica modello embedding
6. `embedding-service` - Embedding multilingua locale
7. `unstructured-service` - PDF extraction (Stage 1)
8. `smoldocling-service` - PDF extraction VLM (Stage 2)
9. `seq` - Log aggregation
10. `jaeger` - Distributed tracing
11. `prometheus` - Metrics collection
12. `alertmanager` - Alert routing
13. `grafana` - Dashboards
14. `n8n` - Workflow automation
15. `api` - ASP.NET API
16. `web` - Next.js frontend

**Utilizzo**:
```bash
# Avvia tutti i servizi
docker compose up -d

# Solo core services
docker compose up -d postgres qdrant redis ollama api web

# Con env_file specifico
docker compose --env-file ../env.development up -d
```

**Caratteristiche**:
- HTTP only (no SSL overhead)
- Logging verbose
- Health checks configurabili
- Volumes persistenti
- Network bridge isolato

### `docker-compose.dev.yml` (Development Override)

**Scopo**: Alternativa a docker-compose.yml con alcune ottimizzazioni

**Differenze**:
- Usa file `env/api.env.dev`, `env/web.env.dev`, `env/n8n.env.dev`
- Configurazioni più dettagliate per debugging
- Variabili environment esplicite

**Utilizzo**:
```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

### `compose.staging.yml` (Staging Override)

**Scopo**: Configurazione pre-produzione per QA testing

**Override**:
- `ASPNETCORE_ENVIRONMENT=Staging`
- `NODE_ENV=staging`
- Database: `meepleai_staging`
- Volumes separati: `pgdata-staging`, `redisdata-staging`
- Retention Prometheus: 60 giorni
- Restart policy: `unless-stopped`
- Logging: 10MB max, 3 files

**Utilizzo**:
```bash
# Componi base + staging
docker compose -f docker-compose.yml -f compose.staging.yml up -d

# Con env file
docker compose -f docker-compose.yml -f compose.staging.yml --env-file .env.staging up -d
```

**Variabili richieste**:
- `STAGING_API_URL` - URL API staging (es: `https://staging-api.meepleai.dev`)
- `REDIS_PASSWORD` - Password Redis
- `SEQ_PASSWORD_HASH` - Hash password Seq admin

### `compose.prod.yml` (Production Override)

**Scopo**: Configurazione produzione con HA e sicurezza massima

**Override**:
- `ASPNETCORE_ENVIRONMENT=Production`
- `NODE_ENV=production`
- `ASPNETCORE_URLS=https://+:8080;http://+:8081` (HTTPS)
- Database: `meepleai_prod`
- Volumes separati: `pgdata-prod`, `redisdata-prod`
- Retention Prometheus: 90 giorni (50GB)
- Restart policy: `always`
- Logging: 50MB max, 10 files
- **Resource limits**:
  - API: 4 CPU, 8GB RAM
  - Web: 2 CPU, 4GB RAM
  - Postgres: 2 CPU, 4GB RAM
  - Qdrant: 2 CPU, 8GB RAM

**Utilizzo**:
```bash
# Componi base + production
docker compose -f docker-compose.yml -f compose.prod.yml up -d

# Con env file
docker compose -f docker-compose.yml -f compose.prod.yml --env-file .env.production up -d
```

**Variabili richieste**:
- `PRODUCTION_API_URL` - URL API produzione (es: `https://api.meepleai.dev`)
- `REDIS_PASSWORD` - Password Redis forte
- `SEQ_PASSWORD_HASH` - Hash password Seq admin
- `GRAFANA_ROOT_URL` - URL Grafana produzione

**Secrets richiesti**:
- `postgres-password` (16+ caratteri)
- `openrouter-api-key`
- `n8n-encryption-key` (CRITICO - backup!)
- `grafana-admin-password`

### `compose.test.yml` (Test/CI Override)

**Scopo**: Configurazione per CI/CD pipeline (GitHub Actions)

**Caratteristiche**:
- Database temporaneo
- No persistence (ephemeral volumes)
- Fast startup
- Testcontainers compatible

**Utilizzo**:
```bash
# CI environment
docker compose -f docker-compose.yml -f compose.test.yml up -d
```

### `docker-compose.infisical.yml` (POC)

**Scopo**: Proof of Concept per Infisical secret management

**Servizi**:
- `infisical` - Secret management UI
- `postgres-infisical` - Database Infisical
- `redis-infisical` - Cache Infisical

**NON usato in produzione** - solo per testing Infisical come alternativa a Docker Secrets.

---

## Environment Files

Directory: `infra/env/`

### Struttura File per Ambiente

| File | Ambiente | Servizio | Utilizzo |
|------|----------|----------|----------|
| `api.env.dev.example` | Development | API | `docker-compose.yml` |
| `api.env.staging.example` | Staging | API | Override manuale |
| `api.env.prod.example` | Production | API | Override manuale |
| `api.env.ci.example` | CI/CD | API | GitHub Actions |
| `web.env.dev.example` | Development | Web | `docker-compose.yml` |
| `web.env.staging.example` | Staging | Web | Override manuale |
| `web.env.prod.example` | Production | Web | Override manuale |
| `web.env.ci.example` | CI/CD | Web | GitHub Actions |
| `n8n.env.dev.example` | Development | n8n | `docker-compose.yml` |
| `n8n.env.staging.example` | Staging | n8n | Override manuale |
| `n8n.env.prod.example` | Production | n8n | Override manuale |
| `n8n.env.ci.example` | CI/CD | n8n | GitHub Actions |
| `alertmanager.env.example` | All | Alertmanager | Comune a tutti |

### Due Approcci per Environment Variables

#### Approccio 1: File Root (Raccomandato per setup rapido)

**File**: `.env.development`, `.env.staging`, `.env.production` nella root del repository

**Pro**:
- ✅ Un solo file per ambiente
- ✅ Visione completa configurazione
- ✅ Docker Compose carica automaticamente `.env` dalla directory corrente
- ✅ Ideale per documentazione e onboarding

**Contro**:
- ❌ File molto grandi
- ❌ Difficile separare configurazione per servizio

**Utilizzo**:
```bash
# Docker Compose cerca .env automaticamente nella directory corrente
cd infra
docker compose up -d  # Usa .env se presente

# O specifica esplicitamente
docker compose --env-file ../.env.development up -d
```

#### Approccio 2: File Modularizzati in `infra/env/` (Usato nel codebase)

**Pro**:
- ✅ File piccoli e focalizzati
- ✅ Facile manutenzione separata per servizio
- ✅ Può essere committato selettivamente (.example files)
- ✅ Più granularità

**Contro**:
- ❌ Più file da gestire
- ❌ Deve essere referenziato esplicitamente in docker-compose.yml

**Utilizzo**:
```bash
# File referenziati in docker-compose.yml
services:
  api:
    env_file:
      - ./env/api.env.dev
  web:
    env_file:
      - ./env/web.env.dev
```

### Quale Approccio Usare?

| Scenario | Approccio | Motivazione |
|----------|-----------|-------------|
| **Quick start development** | File root | Setup in 5 minuti |
| **Production deployment** | File modularizzati | Più controllo, sicurezza |
| **CI/CD** | File modularizzati | Integrazione in pipeline |
| **Team onboarding** | File root | Documentazione chiara |
| **Multi-service customization** | File modularizzati | Granularità per servizio |

**RACCOMANDAZIONE**: Usa **entrambi**!
- File root per **documentazione** e **quick start**
- File modularizzati per **deployment effettivo**

---

## Secrets Management

Directory: `infra/secrets/`

### Struttura Secrets per Ambiente

```
infra/secrets/
├── README.md              # Guida completa
├── dev/                   # Development (password semplici)
│   ├── postgres-password.txt
│   ├── openrouter-api-key.txt (opzionale)
│   ├── n8n-encryption-key.txt
│   └── ...
├── staging/               # Staging (password forti)
│   ├── postgres-password.txt
│   ├── redis-password.txt
│   ├── openrouter-api-key.txt
│   ├── n8n-encryption-key.txt
│   ├── grafana-admin-password.txt
│   └── ...
└── prod/                  # Production (password ultra-forti)
    ├── postgres-password.txt
    ├── redis-password.txt
    ├── qdrant-api-key.txt (se Qdrant Cloud)
    ├── openrouter-api-key.txt
    ├── openai-api-key.txt
    ├── n8n-encryption-key.txt (⚠️ CRITICO - backup!)
    ├── grafana-admin-password.txt
    ├── google-oauth-secret.txt
    ├── discord-oauth-secret.txt
    ├── github-oauth-secret.txt
    ├── slack-webhook-url.txt
    ├── pagerduty-integration-key.txt
    ├── sentry-auth-token.txt
    └── ...
```

### Inizializzazione Secrets

```bash
# Development
cd infra/secrets
./init-secrets.sh --environment dev

# Staging
./init-secrets.sh --environment staging

# Production
./init-secrets.sh --environment prod
# Poi editare manualmente i file per inserire valori reali
```

### Secrets in Docker Compose

```yaml
services:
  api:
    secrets:
      - postgres-password
      - openrouter-api-key
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres-password
      OPENROUTER_API_KEY_FILE: /run/secrets/openrouter-api-key

secrets:
  postgres-password:
    file: ./secrets/dev/postgres-password.txt
  openrouter-api-key:
    file: ./secrets/dev/openrouter-api-key.txt
```

### Rotazione Secrets

```bash
# Rotazione singolo secret
cd infra/secrets
./rotate-secret.sh postgres-password --environment prod

# Processo:
# 1. Genera nuova password
# 2. Aggiorna file secret
# 3. Riavvia servizi (rolling restart)
# 4. Verifica connessione
# 5. Backup vecchia password (recovery)
```

**Calendario Rotazione**:
- **Development**: Mai (ricreare se necessario)
- **Staging**: Ogni 6 mesi
- **Production**: Ogni 90 giorni (compliance)

---

## Observability

### Grafana Dashboards

Directory: `infra/dashboards/`

| Dashboard | Metriche | Utilizzo |
|-----------|----------|----------|
| `api-performance.json` | Request rate, latency (P50/P95/P99), error rate | Monitoring API performance |
| `error-monitoring.json` | 4xx/5xx rates, error breakdown by endpoint | Debugging errori |
| `cache-optimization.json` | Redis hit rate, evictions, memory usage | Ottimizzazione cache |
| `ai-quality-monitoring.json` | LLM confidence, hallucination rate, citation accuracy | Quality RAG |
| `ai-rag-operations.json` | RAG pipeline metrics, vector search latency | Performance RAG |
| `infrastructure.json` | CPU, RAM, disk, network per servizio | Resource planning |
| `quality-metrics-gauges.json` | KPI complessivi sistema | Executive overview |

**Accesso**: `http://localhost:3001` (dev) o `https://metrics.meepleai.dev` (prod)

**Default login**: `admin` / vedi `grafana-admin-password` secret

### Prometheus Alert Rules

Directory: `infra/prometheus/alerts/`

| Alert Group | Alerts | Severità | Azione |
|-------------|--------|----------|--------|
| `api-performance.yml` | High latency (>500ms P95), Error rate >5% | Critical | PagerDuty + Auto-scale |
| `database-health.yml` | Connection pool exhausted, Slow queries | Warning | Email + Slack |
| `cache-performance.yml` | Redis memory >90%, Hit rate <70% | Warning | Slack |
| `infrastructure.yml` | CPU >80%, RAM >85%, Disk >90% | Critical | Auto-scale + PagerDuty |
| `pdf-processing.yml` | Queue >100, Processing failures >10% | Warning | Slack |
| `quality-metrics.yml` | RAG confidence <0.7, Hallucination rate >3% | Critical | Email team AI |
| `vector-search.yml` | Qdrant latency >100ms, Search failures | Warning | Slack |
| `prompt-management.yml` | Prompt retrieval failures | Info | Log only |

**Alert Routing** (vedi `infra/alertmanager.yml`):
- **Critical**: PagerDuty + Slack + Email
- **Warning**: Slack + Email
- **Info**: Slack only

### Logging (Seq)

**URL**: `http://localhost:8081` (dev) o `https://logs.meepleai.dev` (prod)

**Retention**:
- Development: 7 giorni
- Staging: 30 giorni
- Production: 90 giorni

**Query Examples**:
```sql
-- Errori API ultimi 15 minuti
@Level = 'Error' AND @Timestamp > Now() - 15m

-- Slow queries
@Message LIKE '%SlowQuery%' AND Duration > 1000

-- Fallimenti PDF processing
Application = 'UnstructuredService' AND @Level = 'Error'
```

### Tracing (Jaeger)

**URL**: `http://localhost:16686` (dev) o `https://traces.meepleai.dev` (prod)

**Retention**:
- Development: Ephemeral (in-memory)
- Staging: 7 giorni (Elasticsearch)
- Production: 30 giorni (Elasticsearch)

**Sampling**:
- Development: 100% (tutti i trace)
- Staging: 10% (1 su 10 requests)
- Production: 10% (cost optimization)

---

## Workflows n8n

Directory: `infra/n8n/`

### Templates Disponibili

| Template | Scopo | Trigger | Azioni |
|----------|-------|---------|--------|
| `backup-automation.json` | Backup automatici | Cron (3 AM daily) | Dump DB, upload S3, cleanup old |
| `bgg-game-sync.json` | Sync BoardGameGeek | Cron (weekly) | Fetch games, update catalog |
| `cache-warming.json` | Pre-load cache | Cron (ogni 6h) | Query top 50 queries, populate cache |
| `daily-reports.json` | Report giornalieri | Cron (9 AM daily) | Stats aggregation, email report |
| `data-export.json` | Export dati utenti | On-demand | Export user data (GDPR) |
| `discord-webhook.json` | Notifiche Discord | Webhook | Alert → Discord channel |
| `email-notification.json` | Email alerts | Webhook | Template email + send SMTP |
| `error-alerting.json` | Alert su errori | Webhook (Seq) | Parse log → Slack/Email |
| `health-monitor.json` | Health checks | Cron (ogni 5min) | Check services, alert if down |
| `integration-slack-notifications.json` | Slack notifications | Webhook | Format + post Slack |
| `pdf-processing-pipeline.json` | Pipeline PDF | Webhook (upload) | Validate → Extract → Embed → Store |
| `slack-notification.json` | Notifiche Slack | Webhook | Alert → Slack channel |
| `user-onboarding.json` | Onboarding utenti | Webhook (signup) | Welcome email, setup guide |

### Import Templates

```bash
# Accedi a n8n
open http://localhost:5678

# Import workflow
# 1. Click "Add workflow"
# 2. Click menu (3 dots) → "Import from File"
# 3. Select file from infra/n8n/templates/
# 4. Activate workflow
```

### Workflows Attivi

Directory: `infra/n8n/workflows/`

- `agent-explain-orchestrator.json` - Orchestrazione AI agents per generazione spiegazioni

---

## Scripts Utility

Directory: `infra/scripts/`

### `load-secrets-env.sh`

**Scopo**: Carica secrets Docker in variabili d'ambiente

**Utilizzo**:
```bash
# In docker-compose.yml
services:
  alertmanager:
    volumes:
      - ./scripts/load-secrets-env.sh:/scripts/load-secrets-env.sh:ro
    entrypoint: ["/scripts/load-secrets-env.sh"]
    command: ['/bin/alertmanager', '--config.file=/etc/alertmanager/alertmanager.yml']
```

**Funzione**:
1. Legge secrets da `/run/secrets/`
2. Esporta come variabili d'ambiente
3. Esegue comando originale

**Esempio**:
```bash
# Secret file: /run/secrets/gmail-app-password
# Diventa variabile: GMAIL_APP_PASSWORD=contenuto_file
```

---

## Quick Start

### Development (Locale)

```bash
# 1. Clone repository
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo

# 2. Inizializza secrets
cd infra/secrets
./init-secrets.sh --environment dev
cd ../..

# 3. Copia env files (opzionale, già ci sono defaults)
cd infra/env
cp api.env.dev.example api.env.dev
cp web.env.dev.example web.env.dev
cp n8n.env.dev.example n8n.env.dev
cd ../..

# 4. Avvia stack
cd infra
docker compose up -d

# 5. Verifica
curl http://localhost:8080/health  # API
curl http://localhost:3000          # Web
open http://localhost:3001          # Grafana
open http://localhost:8081          # Seq
```

### Staging

```bash
# 1. Copia e configura env files
cp .env.staging.example .env.staging
vim .env.staging  # Sostituisci <REPLACE_*>

# O usa file modularizzati
cd infra/env
cp api.env.staging.example api.env.staging
cp web.env.staging.example web.env.staging
# ... etc

# 2. Inizializza secrets staging
cd ../secrets
./init-secrets.sh --environment staging
# Editare manualmente i file per valori reali

# 3. Deploy
cd ..
docker compose -f docker-compose.yml -f compose.staging.yml --env-file ../.env.staging up -d

# 4. Verifica
curl https://staging.meepleai.dev/health
```

### Production

```bash
# 1. Configura env (usa secret manager, non file!)
# AWS Secrets Manager / Azure Key Vault / HashiCorp Vault

# 2. Inizializza secrets production
cd infra/secrets
./init-secrets.sh --environment prod
# Editare tutti i file con valori production-grade

# 3. Backup n8n encryption key (CRITICO!)
gpg --encrypt --recipient ops@meepleai.dev prod/n8n-encryption-key.txt
# Store in safe deposit box

# 4. Deploy (Blue-Green recommended)
cd ..
docker compose -f docker-compose.yml -f compose.prod.yml up -d

# 5. Verifica health
curl https://api.meepleai.dev/health

# 6. Monitor per 30 minuti
open https://metrics.meepleai.dev
# Check error rate <1%, latency P95 <200ms
```

---

## Deployment

### Pipeline Deployment

```
┌─────────────┐
│   git push  │
└──────┬──────┘
       │
       ▼
┌────────────────────┐
│  CI/CD (GitHub)    │
│  - Lint            │
│  - Test            │
│  - Build images    │
└──────┬─────────────┘
       │
       ▼
┌────────────────────┐
│  Auto-deploy       │
│  to Staging        │
└──────┬─────────────┘
       │
       ▼
┌────────────────────┐
│  QA Testing        │
│  (1-2 days)        │
└──────┬─────────────┘
       │ Manual approval
       ▼
┌────────────────────┐
│  Deploy Production │
│  (Blue-Green)      │
└────────────────────┘
```

### Docker Compose Multi-Environment

```bash
# Development
docker compose up -d

# Staging (base + override)
docker compose -f docker-compose.yml -f compose.staging.yml up -d

# Production (base + override)
docker compose -f docker-compose.yml -f compose.prod.yml up -d

# Test/CI
docker compose -f docker-compose.yml -f compose.test.yml up -d
```

### Update Services

```bash
# Pull nuove immagini
docker compose pull

# Recreate solo servizi modificati
docker compose up -d

# Recreate tutti
docker compose up -d --force-recreate

# Recreate singolo servizio
docker compose up -d --force-recreate api
```

### Rollback

```bash
# Rollback a versione specifica
git checkout v1.2.2

# Rebuild immagini
docker compose build api web

# Deploy
docker compose up -d api web

# Verifica
curl http://localhost:8080/health
```

---

## Troubleshooting

### Servizio non si avvia

```bash
# Check logs
docker compose logs -f api

# Check health
docker compose ps

# Inspect container
docker compose exec api bash
ls -la /run/secrets/  # Verifica secrets montati
```

### Secret non trovato

```bash
# Verifica file secret esiste
ls -la infra/secrets/dev/postgres-password.txt

# Verifica mount in container
docker compose exec api cat /run/secrets/postgres-password

# Ricrea secret
cd infra/secrets
./init-secrets.sh --environment dev
```

### Env file non caricato

```bash
# Verifica env_file in docker-compose.yml
docker compose config | grep env_file

# Verifica file esiste
ls -la infra/env/api.env.dev

# Test con env_file esplicito
docker compose --env-file ../env.development up -d
```

### Port già in uso

```bash
# Find processo che usa porta
lsof -i :8080

# Kill processo
kill -9 <PID>

# O cambia porta in env
# API_PORT=8081
```

### Volume permission issues

```bash
# Fix permissions
sudo chown -R $USER:$USER infra/volumes/

# O ricrea volumes
docker compose down -v
docker compose up -d
```

---

## Best Practices

### 1. Separazione Ambienti

✅ **Fare**:
```bash
# Volumes separati per ambiente
pgdata-dev
pgdata-staging
pgdata-prod
```

❌ **Non fare**:
```bash
# Condividere volumes tra ambienti
pgdata  # Usato da dev e prod
```

### 2. Secrets Management

✅ **Fare**:
```bash
# Docker Secrets per production
POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password
```

❌ **Non fare**:
```bash
# Plaintext in environment
POSTGRES_PASSWORD=mypassword123
```

### 3. Resource Limits

✅ **Fare** (Production):
```yaml
deploy:
  resources:
    limits:
      cpus: '4'
      memory: 8G
```

❌ **Non fare** (Development):
```yaml
# No limits - permetti flessibilità
```

### 4. Logging

✅ **Fare**:
```yaml
logging:
  driver: "json-file"
  options:
    max-size: "50m"
    max-file: "10"
```

❌ **Non fare**:
```yaml
# No logging limits = disk full
```

---

## Riferimenti

- **Multi-Environment Strategy**: `../docs/05-operations/deployment/multi-environment-strategy.md`
- **Environment Variables**: `../docs/06-security/environment-variables-production.md`
- **Deployment Guide**: `../docs/05-operations/deployment/board-game-ai-deployment-guide.md`
- **Secrets Management**: `./secrets/README.md`
- **Env Files Guide**: `./env/README.md`

---

**Domande?**: devops@meepleai.dev
**Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues
