# Strategia Multi-Ambiente - MeepleAI

**Versione**: 1.0
**Ultimo aggiornamento**: 2025-11-22
**Autore**: DevOps Team
**Stato**: Production Ready

---

## Indice

1. [Introduzione](#introduzione)
2. [Panoramica dei 3 Ambienti](#panoramica-dei-3-ambienti)
3. [Perché Servono 3 Ambienti](#perché-servono-3-ambienti)
4. [Matrice di Confronto Ambienti](#matrice-di-confronto-ambienti)
5. [Development Environment](#development-environment)
6. [Staging Environment](#staging-environment)
7. [Production Environment](#production-environment)
8. [Flusso di Deployment](#flusso-di-deployment)
9. [Best Practices](#best-practices)
10. [Gestione Secrets Multi-Ambiente](#gestione-secrets-multi-ambiente)
11. [Troubleshooting](#troubleshooting)

---

## Introduzione

MeepleAI utilizza una **strategia multi-ambiente a 3 livelli** per garantire:
- **Qualità del codice** attraverso testing progressivo
- **Sicurezza** isolando ambienti e credenziali
- **Affidabilità** testando deployment prima della produzione
- **Velocità di sviluppo** senza compromettere la stabilità

Questa guida spiega **come e perché** configurare correttamente i 3 ambienti.

---

## Panoramica dei 3 Ambienti

### 🛠️ Development (Locale)
- **Scopo**: Sviluppo attivo, debug, sperimentazione
- **Utenti**: Developers (locale su laptop/desktop)
- **Dati**: Dataset minimal, dati di test
- **Uptime**: Non rilevante
- **Costo**: Gratuito (servizi locali)

### 🧪 Staging (Pre-Produzione)
- **Scopo**: QA, testing integrazione, demo, performance testing
- **Utenti**: QA team, stakeholders, beta testers
- **Dati**: Copia anonimizzata dati produzione
- **Uptime**: 95%+ (best effort)
- **Costo**: Medio (infrastruttura cloud ridotta)

### 🚀 Production (Live)
- **Scopo**: Servizio agli utenti finali
- **Utenti**: Clienti reali (target: 10,000 MAU)
- **Dati**: Dati reali, sensibili, GDPR-compliant
- **Uptime**: 99.5%+ (SLA enforcement)
- **Costo**: Alto (infrastruttura HA, monitoraggio, backup)

---

## Perché Servono 3 Ambienti

### ❌ Problema: Ambiente Unico

```
Developer → Commits → PRODUCTION ❌
         (No testing, high risk!)
```

**Conseguenze**:
- Bug in produzione 🐛
- Downtime per utenti reali ⚠️
- Rollback urgenti (stress!) 😰
- Perdita di fiducia utenti 📉
- Violazioni security possibili 🔓

### ✅ Soluzione: Pipeline Multi-Ambiente

```
Developer → Development (locale) → Staging (test) → Production (live) ✅
            ↓ Fast feedback       ↓ QA gate       ↓ Stable
            ✅ Quick iteration    ✅ Integration   ✅ High quality
```

**Benefici**:
- Bug catturati PRIMA della produzione ✅
- Testing realistico (staging = prod-like) ✅
- Deploy sicuri e prevedibili ✅
- Rollback rari (già testato) ✅
- Compliance e audit trail ✅

---

## Matrice di Confronto Ambienti

| Caratteristica | Development | Staging | Production |
|---------------|-------------|---------|------------|
| **Infrastruttura** |
| Hosting | Locale (Docker) | Cloud (VM/Container) | Cloud HA (Multi-AZ) |
| Database | PostgreSQL locale | PostgreSQL managed | PostgreSQL HA (RDS) |
| Cache | Redis locale (no auth) | Redis managed | Redis HA (ElastiCache) |
| Vector DB | Qdrant locale | Qdrant Cloud | Qdrant Cloud HA |
| **Sicurezza** |
| SSL/TLS | ❌ HTTP only | ✅ HTTPS preferred | ✅ HTTPS only |
| Passwords | Semplici (dev) | Forti (12+ chars) | Molto forti (16+ chars) |
| Secret Management | File .env | Docker Secrets | Secret Manager (AWS/Azure) |
| OAuth | Mock credentials | Staging apps | Production apps |
| CORS | `localhost:3000` | Staging domain | Production domain only |
| **Performance** |
| Caching | L1 only (fast startup) | L1 + L2 (Redis) | L1 + L2 + warmup |
| Connection Pooling | 2-20 connections | 5-50 connections | 10-100 connections |
| Rate Limiting | Disabled/lenient | Moderate | Strict |
| Image DPI (PDF) | 150 DPI | 300 DPI | 300 DPI |
| GPU Processing | ❌ CPU only | ⚠️ CPU/GPU | ✅ GPU recommended |
| **Observability** |
| Logging Level | Debug/Verbose | Information | Warning/Error only |
| Log Retention | 7 days | 30 days | 90 days |
| Metrics Retention | 7 days | 30 days | 90 days |
| Error Tracking (Sentry) | ❌ Disabled | ✅ Enabled | ✅ Enabled + sampling |
| Alerting | ❌ Disabled | ⚠️ Slack only | ✅ Slack + Email + PagerDuty |
| **Data Management** |
| Database Size | Small (< 1GB) | Medium (10-50GB) | Large (100GB+) |
| Backup | ❌ No backup | Weekly | Daily (30d retention) |
| Data Anonymization | N/A | ✅ Required | N/A (real data) |
| GDPR Compliance | N/A | Simulated | ✅ Enforced |
| **AI/ML Services** |
| Embedding Provider | Ollama (local, free) | Ollama or OpenRouter | OpenRouter (reliable) |
| LLM Provider | OpenRouter (optional) | OpenRouter (budget) | OpenRouter (premium models) |
| Model Selection | Free models | Free/cheap models | Premium models (user-tier) |
| API Cost | $0 | $50-100/month | $500-1000/month |
| **Deployment** |
| Deploy Frequency | Continuous (N times/day) | 1-2 times/week | 1-2 times/week (planned) |
| Approval Required | ❌ No | ⚠️ QA sign-off | ✅ Multi-level approval |
| Rollback Strategy | N/A (delete & rebuild) | Manual rollback | Automated rollback |
| Health Checks | Basic | Comprehensive | Comprehensive + SLA |
| **Resource Limits** |
| API CPU | 1 core | 2 cores | 4 cores (HA: 3 replicas) |
| API Memory | 2GB | 4GB | 8GB (per replica) |
| Total Cost/Month | $0 (local) | $200-500 | $1500-3000 |

---

## Development Environment

### 🎯 Obiettivi

1. **Velocità**: Sviluppo rapido senza friction
2. **Semplicità**: Setup facile (< 15 minuti)
3. **Debugging**: Errori verbosi, stack traces completi
4. **Flessibilità**: Sperimentare senza conseguenze

### 📋 Configurazione File: `.env.development`

```bash
# Copia file esempio
cp .env.development.example .env.development

# Avvia stack locale
docker compose --env-file .env.development up -d

# Verifica
curl http://localhost:8080/health
```

### 🔑 Caratteristiche Chiave

#### 1. **HTTP Only** (No SSL)
```bash
ASPNETCORE_URLS=http://+:8080
NEXT_PUBLIC_API_BASE=http://localhost:8080
```
**Perché**: SSL aggiunge overhead, certificati complicati, non necessario locale

#### 2. **Logging Verboso**
```bash
LOG_LEVEL=Debug
ASPNETCORE_DETAILEDERRORS=true
```
**Perché**: Debugging più facile con stack traces completi

#### 3. **Credenziali Semplici**
```bash
POSTGRES_PASSWORD=meeplepass
INITIAL_ADMIN_PASSWORD=Demo123!
```
**Perché**: Nessun dato sensibile, convenienza per developer

#### 4. **Nessun Caching L2**
```bash
HYBRIDCACHE_ENABLE_L2=false
```
**Perché**: Startup più veloce, cambiamenti visibili immediatamente

#### 5. **AI Gratuito**
```bash
EMBEDDING_PROVIDER=ollama  # Locale, gratis
# OpenRouter opzionale (free tier)
```
**Perché**: Zero costi durante sviluppo

#### 6. **Nessun Alert**
```bash
ALERTING_ENABLED=false
```
**Perché**: Non vogliamo email/Slack per errori locali

### ⚠️ Mai in Development

- ❌ Dati di produzione
- ❌ Credenziali di produzione
- ❌ API keys a pagamento (se evitabile)
- ❌ Configurazioni complesse (certificati SSL, OAuth reale)

### 💡 Best Practices Development

```bash
# 1. Usa .env.development.local per override personali (gitignored)
cp .env.development .env.development.local
# Modifica solo nel file .local

# 2. Reset database frequentemente
docker compose down -v  # Cancella volumi
docker compose up -d    # Ricostruisce DB

# 3. Hot reload attivo
DOTNET_USE_POLLING_FILE_WATCHER=true

# 4. Testa features in isolamento
FEATURE_EXPERIMENTAL_NEW_RAG=true  # Solo nel tuo .local
```

---

## Staging Environment

### 🎯 Obiettivi

1. **Replicare Produzione**: Configurazione identica (o quasi)
2. **QA Testing**: Ambiente dedicato per Quality Assurance
3. **Demo**: Mostrare features a stakeholders
4. **Performance Testing**: Load testing, stress testing
5. **Penetration Testing**: Security testing senza rischi

### 📋 Configurazione File: `.env.staging`

```bash
# Copia file esempio
cp .env.staging.example .env.staging

# Sostituisci TUTTI i <REPLACE_*> placeholders
vim .env.staging

# Deploy su infrastruttura cloud
docker compose -f compose.staging.yml --env-file .env.staging up -d
```

### 🔑 Caratteristiche Chiave

#### 1. **HTTPS Preferito** (ma HTTP tollerato)
```bash
ASPNETCORE_URLS=https://+:8080;http://+:8180
SSL Mode=Prefer  # Non Require (per debugging)
```
**Perché**: Simula produzione ma permette debugging

#### 2. **Logging Moderato**
```bash
LOG_LEVEL=Information
# Non Debug (troppo), non Warning only (poco)
```
**Perché**: Balance tra debugging e performance

#### 3. **Credenziali Forti** (ma non ultra-complesse)
```bash
POSTGRES_PASSWORD=StagingP@ssw0rd2025!
INITIAL_ADMIN_PASSWORD=Adm1n!St@g1ng123
```
**Perché**: Sicurezza buona, ma resettabile se persa

#### 4. **Caching Abilitato** (produzione-like)
```bash
HYBRIDCACHE_ENABLE_L2=true
CACHE_OPTIMIZATION_ENABLED=true
```
**Perché**: Testare performance reale con cache

#### 5. **AI Budget-Friendly**
```bash
# Ollama locale OR OpenAI (test cloud embedding)
EMBEDDING_PROVIDER=ollama  # Costa zero
# OpenRouter con modelli economici
LLM_ROUTING_USER_MODEL=meta-llama/llama-3.3-70b-instruct:free
```
**Perché**: Simula produzione riducendo costi

#### 6. **Alert Abilitati** (ma meno aggressivi)
```bash
ALERTING_ENABLED=true
ALERTING_SLACK_ENABLED=true
SLACK_CHANNEL=#alerts-staging  # Separato da prod!
```
**Perché**: Testare sistema alerting senza spam team produzione

### 🔐 Sicurezza Staging

```bash
# 1. OAuth Apps SEPARATI da produzione
# Crea staging app in Google Cloud Console
GOOGLE_OAUTH_CLIENT_ID=123456-staging.apps.googleusercontent.com

# 2. Database SEPARATO
POSTGRES_DB=meepleai_staging  # Mai usare DB produzione!

# 3. Dati Anonimizzati
# Script: tools/data/anonymize-prod-data.sh
# - Rimpiazza email con faker
# - Rimuovi PII (Personal Identifiable Information)
# - Mantieni struttura per testing
```

### 📊 Dati in Staging

**NON fare**:
```bash
# ❌ Copiare database produzione direttamente
pg_dump production | psql staging  # MALE! PII esposti
```

**Fare invece**:
```bash
# ✅ Anonimizzare prima di copiare
./tools/data/anonymize-prod-data.sh --source production --target staging
# Email: user@example.com → user123@staging.fake
# Names: "Mario Rossi" → "User 123"
# Preserva: game data, PDF content, vector embeddings
```

### 🧪 Testing in Staging

```bash
# Performance Testing
k6 run tests/performance/load-test.js \
  --env API_BASE=https://staging.meepleai.dev

# Security Scanning
docker run --rm -v $(pwd):/zap/wrk owasp/zap2docker-stable \
  zap-baseline.py -t https://staging.meepleai.dev

# Integration Testing
pnpm test:integration --env=staging
```

### 💡 Best Practices Staging

1. **Reset Periodicamente**
   ```bash
   # Ogni lunedì: refresh dati da produzione (anonimizzati)
   cron: 0 3 * * 1 /scripts/refresh-staging-data.sh
   ```

2. **Separate Monitoring**
   ```bash
   # Grafana dashboard separato
   GF_SERVER_ROOT_URL=https://metrics-staging.meepleai.dev
   # Prometheus namespace separato
   PROMETHEUS_EXTERNAL_LABELS=environment=staging
   ```

3. **Feature Flags**
   ```bash
   # Testa features sperimentali solo in staging
   FEATURE_EXPERIMENTAL_RAG_V2=true
   FEATURE_BETA_NEW_UI=true
   ```

---

## Production Environment

### 🎯 Obiettivi

1. **Affidabilità**: 99.5%+ uptime SLA
2. **Sicurezza**: Zero compromessi, compliance GDPR
3. **Performance**: < 200ms response time P95
4. **Scalabilità**: Auto-scaling 10K → 100K MAU
5. **Auditability**: Tutti gli eventi tracciati

### 📋 Configurazione File: `.env.production`

```bash
# Copia file esempio
cp .env.production.example .env.production

# Sostituisci TUTTI i <REPLACE_*> placeholders
# ⚠️  USA SOLO Docker Secrets per dati sensibili
vim .env.production

# ⚠️  CRITICAL: Backup del file in vault sicuro
# AWS Secrets Manager / Azure Key Vault / HashiCorp Vault

# Deploy
docker compose -f compose.prod.yml --env-file .env.production up -d
```

### 🔑 Caratteristiche Chiave

#### 1. **HTTPS ONLY** (Zero HTTP)
```bash
ASPNETCORE_URLS=https://+:8080  # No fallback HTTP
ConnectionStrings__Postgres=...;SSL Mode=Require;Trust Server Certificate=false
REDIS_CONFIGURATION=...,ssl=true
SESSION_COOKIE_SECURE=true  # Blocca cookie su HTTP
```
**Perché**: Sicurezza obbligatoria, compliance

#### 2. **Logging Minimal** (Performance)
```bash
LOG_LEVEL=Warning  # Solo Warning/Error
LOG_SAMPLING_ENABLED=true
LOG_SAMPLING_RATE=0.1  # 10% dei log (riduce volume)
```
**Perché**: Logging verboso degrada performance 20-30%

#### 3. **Credenziali Ultra-Forti** (Mai in plaintext)
```bash
# ❌ NEVER:
# POSTGRES_PASSWORD=mypassword

# ✅ ALWAYS:
POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password
# File: infra/secrets/prod/postgres-password.txt
# Content: Xy9$kL2m#Pq8vR@nT5wU3bV6cX1z (20+ chars, random)
```
**Perché**: Compliance, audit, security

#### 4. **Caching Aggressivo** (Performance)
```bash
HYBRIDCACHE_ENABLE_L2=true
CACHE_OPTIMIZATION_ENABLED=true
CACHE_WARMING_ENABLED=true  # Pre-load hot queries
CACHE_HOT_QUERY_TTL_HOURS=24
```
**Perché**: 60-80% richieste servite da cache = costi AI ridotti

#### 5. **AI Premium**
```bash
EMBEDDING_PROVIDER=openrouter  # Reliable, managed via OpenRouter
EMBEDDING_MODEL=mxbai-embed-large

# LLM routing per user tier
LLM_ROUTING_ADMIN_MODEL=anthropic/claude-3.5-sonnet  # Best
LLM_ROUTING_USER_MODEL=meta-llama/llama-3.3-70b-instruct:free  # Cost-effective
```
**Perché**: Affidabilità > costo, ma ottimizzato per tier

#### 6. **Alert Multi-Channel** (Incident Response)
```bash
ALERTING_ENABLED=true
ALERTING_EMAIL_ENABLED=true
ALERTING_SLACK_ENABLED=true
ALERTING_PAGERDUTY_ENABLED=true  # Wake up on-call engineer!

# Alert levels
ALERT_CRITICAL_CHANNELS=pagerduty,slack,email  # API down
ALERT_WARNING_CHANNELS=slack,email             # Latency spike
ALERT_INFO_CHANNELS=slack                      # Deployment
```
**Perché**: Incident response < 5 minuti (SLA requirement)

### 🔐 Sicurezza Production (Massima)

#### Docker Secrets (MANDATORY)
```bash
# infra/secrets/prod/
postgres-password.txt
redis-password.txt
openrouter-api-key.txt
n8n-encryption-key.txt  # ⚠️  BACKUP CRITICO
initial-admin-password.txt
grafana-admin-password.txt
google-oauth-secret.txt
slack-webhook-url.txt
pagerduty-integration-key.txt

# Init secrets
cd infra/secrets
./init-secrets.sh --environment production

# Rotate quarterly
./rotate-secret.sh postgres-password --environment production
```

#### Security Headers (OWASP)
```bash
SECURITY_HEADERS_ENABLE_CSP=true  # Content Security Policy
SECURITY_HEADERS_ENABLE_HSTS=true  # HTTP Strict Transport Security
SECURITY_HEADERS_HSTS_MAX_AGE=31536000  # 1 year
SECURITY_HEADERS_HSTS_PRELOAD=true  # Submit to Chrome preload list
```

#### Rate Limiting (Anti-Abuse)
```bash
# Strict limits
RATELIMIT_ANONYMOUS_MAX_TOKENS=60
RATELIMIT_ANONYMOUS_REFILL_RATE=1.0  # 1 request/second max
RATELIMIT_USER_MAX_TOKENS=100
RATELIMIT_ADMIN_MAX_TOKENS=1000  # Admin più permissivi
```

### 📊 Monitoring Production

#### Health Checks
```bash
# Aggressive monitoring
HEALTHCHECK_INTERVAL=10s  # Ogni 10 secondi
HEALTHCHECK_RETRIES=10    # 100s per dichiarare unhealthy

# Monitora:
# - Database connection pool
# - Redis availability
# - Qdrant response time
# - OpenRouter quota remaining
```

#### SLA Targets
```bash
SLA_TARGET_UPTIME=99.5             # 3.6 ore downtime/anno max
SLA_TARGET_RESPONSE_TIME_MS=200    # P95 < 200ms
SLA_TARGET_ERROR_RATE=0.01         # < 1% errori
```

#### Metrics Dashboard (Grafana)
```
- API Request Rate (req/s)
- Response Time (P50, P95, P99)
- Error Rate (% 5xx)
- Database Connection Pool Usage
- Cache Hit Rate
- LLM API Cost ($ per hour)
- Active Users (real-time)
- PDF Processing Queue Length
```

### 💰 Costi Production (Mensili Stimati)

| Servizio | Dimensionamento | Costo Stimato |
|----------|----------------|---------------|
| Database (AWS RDS) | db.m5.large (2 vCPU, 8GB) | $150 |
| Redis (ElastiCache) | cache.m5.large (2 vCPU, 8GB) | $120 |
| Qdrant Cloud | 2GB storage, 10M vectors | $50 |
| Compute (ECS/EKS) | 3x API (4 vCPU, 8GB) + 2x Web (2 vCPU, 4GB) | $400 |
| Load Balancer (ALB) | - | $30 |
| OpenRouter (LLM) | ~100K requests/month | $500 |
| OpenAI (Embeddings) | ~50K embeddings/month | $50 |
| Sentry | 100K events/month | $30 |
| Seq | Self-hosted | $0 |
| Grafana Cloud | Alternative to self-hosted | $50 (opzionale) |
| S3 (Backups + PDFs) | 500GB | $12 |
| CloudWatch Logs | 50GB/month | $25 |
| **TOTALE** | | **~$1,500/month** |

**Ottimizzazione costi**:
- Use LLM free tier models per anonymous users (90% traffic)
- Cache aggressivo (60% cache hit = -$200/month LLM)
- Reserved instances (AWS) = -20% compute
- **Costo ottimizzato: ~$1,000/month**

### 🚨 Incident Response

```bash
# 1. Alert ricevuto (PagerDuty)
# 2. Accedi a dashboard
open https://metrics.meepleai.dev/d/incidents

# 3. Check logs
docker compose -f compose.prod.yml logs -f api --since 5m

# 4. Rollback se necessario
./scripts/rollback-deployment.sh --version v1.2.3

# 5. Post-mortem
# Template: docs/05-operations/runbooks/incident-postmortem-template.md
```

### 💡 Best Practices Production

#### 1. **Blue-Green Deployment**
```bash
# Mantieni 2 stack paralleli
docker compose -f compose.prod.blue.yml up -d   # Vecchia versione
docker compose -f compose.prod.green.yml up -d  # Nuova versione

# Switch load balancer gradualmente
# 10% → 50% → 100% traffic to green

# Rollback istantaneo se problemi
# 100% → 0% back to blue
```

#### 2. **Canary Releases**
```bash
# Deploy a 5% utenti
FEATURE_NEW_RAG_ENABLED=true
FEATURE_NEW_RAG_ROLLOUT_PERCENTAGE=5

# Monitora metriche
# Se error rate < 1% → aumenta a 25% → 50% → 100%
```

#### 3. **Database Migrations**
```bash
# ⚠️  SEMPRE backup prima
pg_dump > backup-pre-migration-$(date +%Y%m%d).sql

# Migration
dotnet ef database update

# Verifica
curl https://api.meepleai.dev/health
# Se problemi: restore immediato
```

#### 4. **Monitoring Proattivo**
```bash
# Alert PRIMA che utenti notino
# Esempio: Latency spike detection
alert: APILatencyHigh
expr: histogram_quantile(0.95, api_request_duration_seconds) > 0.2
for: 5m
annotations:
  summary: "API P95 latency > 200ms per 5 minuti"
actions:
  - pagerduty: wake on-call
  - auto-scale: +2 replicas
```

---

## Flusso di Deployment

### 🔄 Pipeline Completa

```
┌──────────────┐
│  Developer   │
│   Laptop     │
└──────┬───────┘
       │ git push feature/new-rag
       ▼
┌──────────────────────────────────────┐
│  GitHub Actions CI                   │
│  ✅ Lint                             │
│  ✅ Unit Tests (90%+ coverage)       │
│  ✅ Build                            │
│  ✅ Security Scan (CodeQL)           │
└──────────────┬───────────────────────┘
               │ Merge to main
               ▼
┌──────────────────────────────────────┐
│  Auto-Deploy to Staging              │
│  ✅ Deploy containers                │
│  ✅ Run migrations                   │
│  ✅ Integration tests                │
│  ✅ Smoke tests                      │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  QA Testing (Manual)                 │
│  ✅ Functional testing               │
│  ✅ Regression testing               │
│  ✅ Performance testing              │
│  ✅ Security testing                 │
│  ✅ UX testing                       │
└──────────────┬───────────────────────┘
               │ QA Approval
               ▼
┌──────────────────────────────────────┐
│  Create Release Tag                  │
│  v1.2.3 (Semantic Versioning)        │
└──────────────┬───────────────────────┘
               │ Manual trigger
               ▼
┌──────────────────────────────────────┐
│  Deploy to Production                │
│  ⚠️  Manual approval required        │
│  ✅ Blue-Green deployment            │
│  ✅ Database migration               │
│  ✅ Canary 5% → 100%                 │
│  ✅ Health checks                    │
│  ✅ Smoke tests                      │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  Post-Deployment Monitoring          │
│  📊 Error rate                       │
│  📊 Latency (P95)                    │
│  📊 User traffic                     │
│  ⏱️  Monitor for 24h                 │
└──────────────────────────────────────┘
```

### 📅 Frequenza Deployment

| Ambiente | Frequenza | Trigger | Approval |
|----------|-----------|---------|----------|
| **Development** | Continuous (ogni git push) | Automatico | Nessuno |
| **Staging** | 1-2 volte/giorno | Automatico da `main` | Nessuno |
| **Production** | 1-2 volte/settimana | Manuale (tag release) | ✅ Lead + QA |

### ⏱️ Deployment Timing

**Production deployments: solo in orari non-picco**
```
✅ Martedì-Giovedì 10:00-14:00 CET  (Low traffic)
⚠️  Evitare:
   - Lunedì (post-weekend issues)
   - Venerdì (no support weekend)
   - Notte/weekend (no team disponibile)
```

---

## Best Practices

### 1. Isolamento Dati

```bash
# ❌ NEVER mix environments
PROD_DB=meepleai_production
STAGING_DB=meepleai_staging
DEV_DB=meepleai_dev

# Connessioni separate
PROD_HOST=prod-db.internal
STAGING_HOST=staging-db.internal
DEV_HOST=localhost
```

### 2. Nomenclatura Risorse

```bash
# Convenzione: meepleai-{env}-{service}
# Produzione
meepleai-prod-postgres
meepleai-prod-redis
meepleai-prod-api-1
meepleai-prod-api-2

# Staging
meepleai-staging-postgres
meepleai-staging-redis

# Development
meepleai-dev-postgres (locale)
```

### 3. Tagging Docker Images

```bash
# Semantic versioning
meepleai/api:v1.2.3          # Production release
meepleai/api:v1.2.3-staging  # Staging testing
meepleai/api:dev-abc123      # Development build

# Latest tags per ambiente
meepleai/api:latest          # ⚠️  Evitare in prod!
meepleai/api:staging-latest  # Staging
meepleai/api:dev-latest      # Development
```

### 4. Gestione Feature Flags

```bash
# Development: Tutto abilitato (test early)
FEATURE_NEW_RAG=true
FEATURE_BETA_UI=true

# Staging: Features da testare
FEATURE_NEW_RAG=true
FEATURE_BETA_UI=false

# Production: Solo features stabili
FEATURE_NEW_RAG=false  # Wait for QA approval
FEATURE_STABLE_FEATURE=true
```

### 5. Monitoring Separato

```bash
# Dashboard Grafana separati
/d/prod-overview    # Production
/d/staging-overview # Staging
# Dev usa Seq locale (no Grafana)

# Alert channels separati
PROD_SLACK_CHANNEL=#incidents-production
STAGING_SLACK_CHANNEL=#alerts-staging
```

---

## Gestione Secrets Multi-Ambiente

### Struttura Directory

```
infra/secrets/
├── dev/                      # Locale (gitignored)
│   ├── postgres-password.txt       # meeplepass
│   ├── openrouter-api-key.txt      # Optional
│   └── README.md
├── staging/                  # Staging (gitignored)
│   ├── postgres-password.txt       # Xy9$kL2m#Pq8vR@
│   ├── redis-password.txt
│   ├── openrouter-api-key.txt
│   ├── n8n-encryption-key.txt
│   ├── grafana-admin-password.txt
│   └── README.md
└── prod/                     # Production (gitignored)
    ├── postgres-password.txt       # Xy9$kL2m#Pq8vR@nT5wU3bV6cX1z
    ├── redis-password.txt
    ├── openrouter-api-key.txt
    ├── openai-api-key.txt
    ├── n8n-encryption-key.txt      # ⚠️  CRITICO: Backup vault!
    ├── grafana-admin-password.txt
    ├── google-oauth-secret.txt
    ├── slack-webhook-url.txt
    ├── pagerduty-integration-key.txt
    └── README.md
```

### Inizializzazione Secrets

```bash
# Development (semplice)
cd infra/secrets
./init-secrets.sh --environment dev

# Staging (generazione automatica)
./init-secrets.sh --environment staging

# Production (più controllo)
./init-secrets.sh --environment prod
# Poi editare manualmente i file per inserire valori reali
# Esempio: openrouter-api-key.txt con key da dashboard
```

### Rotazione Secrets

```bash
# Calendario rotazione
# - Development: Mai (ricreare da zero se necessario)
# - Staging: Ogni 6 mesi
# - Production: Ogni 90 giorni (compliance)

# Script rotazione
./rotate-secret.sh postgres-password --environment prod
# 1. Genera nuova password
# 2. Aggiorna Docker Secret
# 3. Rolling restart container API
# 4. Verifica connessione DB
# 5. Backup vecchia password (recovery)
```

### Backup Secrets (Production)

```bash
# ⚠️  CRITICAL: N8N_ENCRYPTION_KEY
# Se perso = tutte le credenziali n8n irrecuperabili

# Backup a vault sicuro
aws secretsmanager create-secret \
  --name meepleai/prod/n8n-encryption-key \
  --secret-string "$(cat infra/secrets/prod/n8n-encryption-key.txt)"

# Backup offline (encrypted USB)
gpg --encrypt --recipient ops@meepleai.dev \
  infra/secrets/prod/n8n-encryption-key.txt

# Store in safe deposit box (no joke!)
```

---

## Troubleshooting

### Problema: Servizio non si avvia

```bash
# Check file .env usato
docker compose config  # Mostra config merged

# Check secrets montati
docker compose exec api ls -la /run/secrets/
# Se vuoto → segreti non montati correttamente

# Check logs
docker compose logs api --tail 100
```

### Problema: Credenziali non funzionano

```bash
# Development
# Reset completo
docker compose down -v
rm .env.development
cp .env.development.example .env.development
docker compose up -d

# Staging/Production
# Check secret content
docker compose exec api cat /run/secrets/postgres-password
# Compare con valore atteso (non committato!)
```

### Problema: Deploy production fallisce

```bash
# Rollback immediato
git tag --list  # Trova ultima versione stabile
git checkout v1.2.2
docker compose -f compose.prod.yml up -d

# Investigate in staging
git checkout main
docker compose -f compose.staging.yml logs -f
```

### Problema: Costi Cloud inaspettati

```bash
# Check LLM usage
# OpenRouter: https://openrouter.ai/activity
# OpenAI: https://platform.openai.com/usage

# Audit: quale user tier genera più costi?
SELECT user_tier, COUNT(*) as requests, AVG(tokens) as avg_tokens
FROM chat_logs
WHERE created_at > NOW() - INTERVAL '7 days'
GROUP BY user_tier;

# Ottimizzazione: Riduci free tier model limits
RATELIMIT_ANONYMOUS_MAX_TOKENS=30  # Era 60
```

---

## Checklist Deployment

### ✅ Development → Staging

- [ ] All tests pass (`pnpm test && dotnet test`)
- [ ] No compiler warnings
- [ ] Code reviewed and approved
- [ ] Branch merged to `main`
- [ ] CI/CD pipeline green
- [ ] Auto-deployment to staging successful
- [ ] Smoke tests passed

### ✅ Staging → Production

- [ ] Staging testing completed (2+ days)
- [ ] QA sign-off received
- [ ] Performance testing passed (load tests)
- [ ] Security scan passed (no critical/high vulnerabilities)
- [ ] Database migration tested in staging
- [ ] Rollback plan documented
- [ ] Release notes prepared
- [ ] Stakeholders notified (deployment window)
- [ ] On-call engineer assigned
- [ ] Production secrets rotated (if > 90 days)
- [ ] Backup verified (< 24h old)
- [ ] Monitoring dashboards ready
- [ ] Incident response team briefed
- [ ] Manual approval obtained (Lead + QA)
- [ ] **DEPLOY** (Blue-Green or Canary)
- [ ] Health checks green (all services)
- [ ] Smoke tests passed in production
- [ ] Metrics monitored for 30 minutes
- [ ] Error rate < 1%
- [ ] Latency P95 < 200ms
- [ ] User traffic normal
- [ ] **DEPLOYMENT SUCCESSFUL** ✅

---

## Conclusione

La strategia multi-ambiente **non è overhead** - è **assicurazione contro disastri**.

| Investimento | Ritorno |
|--------------|---------|
| 3x configurazioni | -90% bug in produzione |
| +$500/month staging | -$5000 incident costs |
| +2h setup | -20h debugging urgenti |
| Processo deployment | +Team confidence |

**Ricorda**: _Produzione non è un ambiente di test. Testa PRIMA, deploya SICURO._

---

**Next Steps**:
1. Setup development: `cp .env.development.example .env.development`
2. Read: [Environment Variables Guide](../../06-security/environment-variables-production.md)
3. Deploy staging: [Deployment Guide](./board-game-ai-deployment-guide.md)

**Domande?**: devops@meepleai.dev