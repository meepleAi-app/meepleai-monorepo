# Environment Strategy

> Guida completa alla gestione degli ambienti Development, Staging e Production

## Overview

MeepleAI utilizza una strategia a **3 ambienti** che separa chiaramente lo sviluppo locale dal deployment su server.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            LOCALE                                        │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  🟢 DEVELOPMENT                                                    │  │
│  │  Branch: feature/* → main-dev                                      │  │
│  │  Scopo: Sviluppo attivo, debug, test rapidi                       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ git push + CI/CD
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           SERVER/CLOUD                                   │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  🟡 STAGING                                                        │  │
│  │  Branch: main-staging                                              │  │
│  │  URL: staging.meepleai.com                                         │  │
│  │  Scopo: QA, UAT, test integrazione reale                          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                    │                                     │
│                                    │ Approvazione QA                     │
│                                    ▼                                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  🔴 PRODUCTION                                                     │  │
│  │  Branch: main                                                      │  │
│  │  URL: meepleai.com                                                 │  │
│  │  Scopo: Utenti reali, massima stabilità                           │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Ambienti in Dettaglio

### 🟢 Development (Locale)

**Dove**: Macchina dello sviluppatore
**Branch**: `feature/*` → merge su `main-dev`
**Scopo**: Sviluppo quotidiano, debug, iterazione rapida

#### Caratteristiche

| Aspetto | Configurazione |
|---------|----------------|
| `ASPNETCORE_ENVIRONMENT` | Development |
| Database | PostgreSQL locale (Docker) |
| Cache L2 (Redis) | Disabilitata |
| Logging | Debug (verboso) |
| Error details | Completi (stack trace visibili) |
| Feature flags | Tutte abilitate + sperimentali |
| AI Provider | Ollama (locale, gratuito) |
| SSL/HTTPS | Non richiesto |
| Hot reload | Abilitato |

#### Setup

```bash
# 1. Setup secrets (una tantum)
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# 2. Avvia infrastruttura
cd infra
docker compose --profile dev up -d

# 3. Backend
cd apps/api/src/Api
dotnet run

# 4. Frontend (nuovo terminale)
cd apps/web
cp .env.development.example .env.local
pnpm dev
```

#### URL Locali

| Servizio | URL |
|----------|-----|
| Frontend | http://localhost:3000 |
| Backend API | http://localhost:8080 |
| API Docs (Scalar) | http://localhost:8080/scalar/v1 |
| Mailpit (email) | http://localhost:8025 |
| Grafana | http://localhost:3100 |
| Prometheus | http://localhost:9090 |

#### File di Configurazione

```
.env.development.example          # Template variabili ambiente
apps/api/src/Api/appsettings.json # Config base .NET
apps/web/.env.local               # Config frontend (da .env.development.example)
infra/docker-compose.yml          # Infrastruttura Docker
infra/secrets/*.secret            # Secrets locali
```

---

### 🟡 Staging (Server)

**Dove**: Server cloud (AWS/Azure/VPS)
**Branch**: `main-staging`
**URL**: `https://staging.meepleai.com`
**Scopo**: Validazione pre-produzione, QA, UAT

#### Caratteristiche

| Aspetto | Configurazione |
|---------|----------------|
| `ASPNETCORE_ENVIRONMENT` | Staging |
| Database | Managed DB con SSL preferito |
| Cache L2 (Redis) | Abilitata |
| Logging | Information |
| Error details | Limitati |
| Feature flags | Tutte + sperimentali |
| AI Provider | Mix (Ollama + OpenRouter) |
| SSL/HTTPS | Raccomandato |
| Monitoring | Completo (Prometheus, Grafana, Seq) |

#### Scopo di Staging

Staging serve per testare ciò che **non puoi testare localmente**:

| Test | Perché su Staging |
|------|-------------------|
| Deploy process | Verifica che il deploy funzioni |
| SSL/TLS | Certificati reali |
| DNS e networking | Routing reale |
| OAuth callbacks | URL callback reali |
| Performance | Hardware simile a prod |
| Integrazioni esterne | API key di staging |
| QA review | Accessibile al team QA |
| UAT | Stakeholder possono provare |

#### Deploy su Staging

```bash
# Automatico via CI/CD quando merge su main-staging
git checkout main-staging
git merge main-dev
git push origin main-staging
# → GitHub Actions deploya automaticamente
```

#### File di Configurazione

```
.env.staging.example                      # Template (NON committare valori reali)
apps/api/src/Api/appsettings.Staging.json # Override .NET per staging
```

---

### 🔴 Production (Server)

**Dove**: Server cloud (infrastruttura production-grade)
**Branch**: `main`
**URL**: `https://meepleai.com`
**Scopo**: Utenti reali, massima stabilità e sicurezza

#### Caratteristiche

| Aspetto | Configurazione |
|---------|----------------|
| `ASPNETCORE_ENVIRONMENT` | Production |
| Database | Managed DB con SSL **obbligatorio** |
| Cache L2 (Redis) | Abilitata (aggressiva) |
| Logging | Warning only |
| Error details | Disabilitati |
| Feature flags | Solo feature stabili |
| AI Provider | OpenAI + OpenRouter (cloud) |
| SSL/HTTPS | **Obbligatorio** |
| Monitoring | Enterprise (+ alerting) |
| Backup | Automatico (30 giorni retention) |
| Replicas | Multiple (alta disponibilità) |

#### Sicurezza Production

| Misura | Implementazione |
|--------|-----------------|
| Secrets | Docker Secrets o AWS Secrets Manager |
| Credentials | Mai in env vars, sempre file secrets |
| Accesso DB | Solo da VPC interna |
| Rate limiting | Strict, per-tier |
| WAF | Abilitato |
| DDoS protection | Cloudflare/AWS Shield |

#### Deploy su Production

```bash
# Solo dopo approvazione QA su staging
git checkout main
git merge main-staging
git tag v1.2.3
git push origin main --tags
# → GitHub Actions deploya con approval gate
```

#### File di Configurazione

```
.env.production.example                      # Template (MAI committare valori reali!)
apps/api/src/Api/appsettings.Production.json # Override .NET per production
```

---

## Git Workflow

### Branch Strategy

```
main                 ← Production (protetto, solo PR)
  │
  └── main-staging   ← Staging (protetto, solo PR)
        │
        └── main-dev ← Development (integrazione)
              │
              └── feature/issue-XXX-description
              └── fix/issue-XXX-description
              └── hotfix/critical-bug
```

### Flow Standard

```bash
# 1. Crea feature branch da main-dev
git checkout main-dev && git pull
git checkout -b feature/issue-123-add-search

# 2. Sviluppa (ambiente: Development locale)
# ... coding ...

# 3. Push e PR verso main-dev
git push -u origin feature/issue-123-add-search
# Crea PR: feature/issue-123-add-search → main-dev

# 4. Dopo merge, promuovi a staging
git checkout main-staging && git pull
git merge main-dev
git push
# → CI/CD deploya su staging

# 5. QA testa su staging.meepleai.com

# 6. Dopo approvazione QA, promuovi a production
git checkout main && git pull
git merge main-staging
git tag v1.2.3
git push && git push --tags
# → CI/CD deploya su production

# 7. Cleanup
git checkout main-dev
git branch -D feature/issue-123-add-search
```

### Hotfix Flow

Per bug critici in production:

```bash
# 1. Crea hotfix da main
git checkout main && git pull
git checkout -b hotfix/critical-auth-bug

# 2. Fix rapido
# ... fix ...

# 3. PR diretto verso main (bypass staging per urgenza)
git push -u origin hotfix/critical-auth-bug
# Crea PR: hotfix/critical-auth-bug → main
# → Deploy immediato dopo review

# 4. Backport su staging e dev
git checkout main-staging && git merge main && git push
git checkout main-dev && git merge main && git push
```

---

## Confronto Ambienti

### Configurazione Tecnica

| Configurazione | Development | Staging | Production |
|----------------|-------------|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | Development | Staging | Production |
| `NODE_ENV` | development | production | production |
| Database SSL | No | Preferred | Required |
| Redis password | Opzionale | Obbligatoria | Obbligatoria |
| HTTPS | No | Sì | Sì (enforced) |
| CORS | localhost | staging domain | prod domain |
| Rate limiting | Permissivo | Moderato | Strict |

### Risorse

| Risorsa | Development | Staging | Production |
|---------|-------------|---------|------------|
| API CPU | 0.5-1 core | 1-2 cores | 2-4 cores |
| API Memory | 1-2 GB | 2-4 GB | 4-8 GB |
| API Replicas | 1 | 1-2 | 2-3 |
| DB connections | 10-20 | 20-50 | 50-100 |
| Redis memory | 512 MB | 1 GB | 2 GB |

### Monitoring e Logging

| Aspetto | Development | Staging | Production |
|---------|-------------|---------|------------|
| Log level | Debug | Information | Warning |
| Error details | Full stack | Partial | Hidden |
| Metrics | Basic | Full | Full + alerts |
| Tracing | Opzionale | Abilitato | Abilitato |
| Alerting | Disabilitato | Email + Slack | Multi-channel |
| Retention | 1 giorno | 7 giorni | 30-90 giorni |

### Feature Flags

| Feature | Development | Staging | Production |
|---------|-------------|---------|------------|
| Core features | ✅ | ✅ | ✅ |
| Experimental | ✅ | ✅ | ❌ |
| Debug tools | ✅ | ❌ | ❌ |
| Swagger UI | ✅ | ✅ | ❌ |

---

## File di Configurazione

### Mappa Completa

```
meepleai-monorepo/
├── .env.development.example     # 🟢 Dev - variabili ambiente
├── .env.staging.example         # 🟡 Staging - variabili ambiente
├── .env.production.example      # 🔴 Prod - variabili ambiente
│
├── apps/
│   ├── api/src/Api/
│   │   ├── appsettings.json            # Base (tutti gli ambienti)
│   │   ├── appsettings.Development.json # 🟢 Override dev (se esiste)
│   │   ├── appsettings.Staging.json     # 🟡 Override staging
│   │   └── appsettings.Production.json  # 🔴 Override production
│   │
│   └── web/
│       ├── .env.local                   # 🟢 Dev (gitignored)
│       ├── .env.development.example     # 🟢 Template dev
│       └── .env.test.example            # Test E2E
│
└── infra/
    ├── docker-compose.yml               # 🟢 Dev infrastructure
    ├── docker-compose.traefik.yml       # Reverse proxy
    └── secrets/                         # Secret files
```

### Gerarchia di Caricamento (.NET)

```
appsettings.json                    # Base
  └── appsettings.{Environment}.json # Override per ambiente
        └── Environment variables    # Override finale
              └── Docker Secrets     # Production secrets
```

### Gerarchia di Caricamento (Next.js)

```
.env                      # Base (tutti)
  └── .env.local          # Override locale (gitignored)
        └── .env.{NODE_ENV}.local  # Override per NODE_ENV
```

---

## Checklist per Ambiente

### ✅ Development Setup

- [ ] Docker Desktop installato e running
- [ ] `pwsh setup-secrets.ps1 -SaveGenerated` eseguito
- [ ] `docker compose --profile dev up -d` avviato
- [ ] `.env.local` creato da `.env.development.example`
- [ ] `dotnet run` funziona su porta 8080
- [ ] `pnpm dev` funziona su porta 3000
- [ ] Database migrations applicate

### ✅ Staging Deployment

- [ ] Server/VM provisionato
- [ ] DNS configurato (`staging.meepleai.com`)
- [ ] SSL certificate installato
- [ ] Secrets configurati (non in env vars!)
- [ ] Database managed creato
- [ ] Redis managed creato
- [ ] CI/CD pipeline configurata
- [ ] Monitoring attivo
- [ ] Accesso QA team verificato

### ✅ Production Deployment

- [ ] Infrastruttura production-grade
- [ ] DNS configurato (`meepleai.com`)
- [ ] SSL certificate (auto-renewal)
- [ ] Docker Secrets o Secrets Manager
- [ ] Database con backup automatico
- [ ] Redis con persistenza
- [ ] Load balancer configurato
- [ ] WAF/DDoS protection
- [ ] Monitoring + alerting
- [ ] Runbook documentati
- [ ] Disaster recovery testato

---

## CI/CD Pipeline

### Overview

Il deployment è completamente automatizzato tramite GitHub Actions:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CI/CD PIPELINE                                     │
│                                                                              │
│  feature/* ──PR──► main-dev ──merge──► main-staging ──merge──► main         │
│      │                │                     │                    │           │
│      ▼                ▼                     ▼                    ▼           │
│  ┌────────┐      ┌────────┐           ┌─────────┐          ┌─────────┐      │
│  │  CI    │      │  CI    │           │ Deploy  │          │ Deploy  │      │
│  │ Tests  │      │ Tests  │           │ Staging │          │  Prod   │      │
│  └────────┘      └────────┘           │ (auto)  │          │(manual) │      │
│                                       └─────────┘          └─────────┘      │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Workflow Files

| File | Trigger | Scopo |
|------|---------|-------|
| `ci.yml` | Push/PR su main, main-dev, frontend-dev | Test, lint, build |
| `deploy-staging.yml` | Push su `main-staging` | Deploy automatico staging |
| `deploy-production.yml` | Push/tag su `main` | Deploy con approval gate |
| `security.yml` | Push + weekly | Security scanning |

---

### 🟡 Deploy Staging

**Trigger**: Push su branch `main-staging`
**Workflow**: `.github/workflows/deploy-staging.yml`
**Approval**: Automatico (nessuna approvazione richiesta)

#### Pipeline Steps

```
1. Pre-deploy Tests     → Esegue ci.yml (skip con workflow_dispatch)
2. Build Images         → Builda e pusha Docker images su GHCR
3. Deploy               → SSH/K8s/CloudRun deploy
4. Validate             → Health checks + smoke tests
5. Notify               → Slack notification
```

#### Trigger Manuale

```bash
# Via git
git checkout main-staging
git merge main-dev
git push

# Via GitHub CLI
gh workflow run deploy-staging.yml
```

#### Immagini Docker

```
ghcr.io/<org>/meepleai/api:staging-YYYYMMDD-<sha>
ghcr.io/<org>/meepleai/api:staging-latest
ghcr.io/<org>/meepleai/web:staging-YYYYMMDD-<sha>
ghcr.io/<org>/meepleai/web:staging-latest
```

---

### 🔴 Deploy Production

**Trigger**: Push/tag su branch `main`
**Workflow**: `.github/workflows/deploy-production.yml`
**Approval**: **Richiesta** (environment `production-approval`)

#### Pipeline Steps

```
1. Verify Staging       → Controlla che staging sia healthy
2. Pre-prod Tests       → Esegue ci.yml completo
3. Build Images         → Builda immagini production
4. ⏸️ Approval Gate     → Attende approvazione manuale
5. Deploy (Blue-Green)  → Deploy con zero-downtime
6. Validate             → Health + smoke + performance tests
7. Create Release       → GitHub Release (se tag)
8. Notify               → Slack + summary
```

#### Trigger con Tag (Raccomandato)

```bash
# Crea release con semantic versioning
git checkout main
git merge main-staging
git tag v1.2.3
git push origin main --tags
# → Pipeline parte automaticamente
# → Attende approval gate
# → Deploya dopo approvazione
```

#### Emergency Deploy (Hotfix)

```bash
# Bypass staging check per hotfix urgenti
gh workflow run deploy-production.yml \
  --field skip_staging_check=true \
  --field version=v1.2.4-hotfix
```

#### Immagini Docker

```
ghcr.io/<org>/meepleai/api:v1.2.3
ghcr.io/<org>/meepleai/api:latest
ghcr.io/<org>/meepleai/web:v1.2.3
ghcr.io/<org>/meepleai/web:latest
```

---

### GitHub Environments Setup

Per abilitare i workflow, configura gli environments in GitHub:

#### 1. Staging Environment

**Settings → Environments → New environment: `staging`**

| Setting | Valore |
|---------|--------|
| Deployment branches | `main-staging` only |
| Wait timer | 0 (nessun delay) |
| Required reviewers | Nessuno (auto-deploy) |

**Secrets richiesti**:
```
STAGING_HOST          # IP/hostname del server
STAGING_USER          # SSH username
STAGING_SSH_KEY       # Private key per SSH
SLACK_WEBHOOK_URL     # (opzionale) Notifiche Slack
```

**Variables**:
```
DEPLOY_METHOD=ssh     # ssh | kubernetes | cloudrun
```

#### 2. Production Approval Environment

**Settings → Environments → New environment: `production-approval`**

| Setting | Valore |
|---------|--------|
| Required reviewers | Almeno 1-2 team leads |
| Wait timer | 0-5 minuti |
| Deployment branches | `main` only |

#### 3. Production Environment

**Settings → Environments → New environment: `production`**

| Setting | Valore |
|---------|--------|
| Deployment branches | `main` only |
| Required reviewers | Nessuno (già approvato) |

**Secrets richiesti**:
```
PRODUCTION_HOST       # IP/hostname produzione
PRODUCTION_USER       # SSH username
PRODUCTION_SSH_KEY    # Private key per SSH
SLACK_WEBHOOK_URL     # Notifiche Slack
```

---

### Deploy Methods

Il workflow supporta 3 metodi di deploy configurabili via `vars.DEPLOY_METHOD`:

#### SSH (VPS/Dedicated Server)

```yaml
# Repository Variables
DEPLOY_METHOD=ssh
```

Requisiti server:
- Docker + Docker Compose installati
- SSH key configurata
- Directory `/opt/meepleai` con docker-compose files
- Accesso a GHCR (`docker login ghcr.io`)

#### Kubernetes (AKS/EKS/GKE)

```yaml
# Repository Variables
DEPLOY_METHOD=kubernetes
```

Requisiti:
- Cluster Kubernetes configurato
- `kubectl` configurato nel workflow
- Kubernetes manifests in `infra/k8s/`
- Service account con permessi deploy

#### Cloud Run (GCP)

```yaml
# Repository Variables
DEPLOY_METHOD=cloudrun
```

Requisiti:
- GCP project configurato
- Workload Identity Federation
- Cloud Run services creati

---

### Server Setup (SSH Method)

#### Struttura Directory Server

```
/opt/meepleai/
├── docker-compose.staging.yml   # o docker-compose.prod.yml
├── .env                         # Environment variables
├── secrets/                     # Docker secrets
│   ├── database.secret
│   ├── redis.secret
│   └── ...
├── traefik/                     # Reverse proxy config
│   └── traefik.yml
└── backups/                     # Database backups
```

#### docker-compose.staging.yml (Server)

```yaml
name: meepleai-staging

services:
  api:
    image: ${API_IMAGE:-ghcr.io/org/meepleai/api:staging-latest}
    restart: unless-stopped
    env_file:
      - .env
      - ./secrets/database.secret
      - ./secrets/redis.secret
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=Host(`staging.meepleai.com`) && PathPrefix(`/api`)"

  web:
    image: ${WEB_IMAGE:-ghcr.io/org/meepleai/web:staging-latest}
    restart: unless-stopped
    environment:
      NODE_ENV: production
      NEXT_PUBLIC_API_BASE: https://staging.meepleai.com
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=Host(`staging.meepleai.com`)"

  # ... altri servizi (postgres, redis, traefik)
```

---

### Rollback

#### Rollback Automatico (Failure)

Se il deploy fallisce, il workflow:
1. Rileva il failure nel health check
2. Mantiene i container precedenti
3. Notifica via Slack
4. Crea issue per investigation

#### Rollback Manuale

```bash
# SSH al server
ssh user@staging.meepleai.com

# Rollback a immagine precedente
cd /opt/meepleai
export API_IMAGE=ghcr.io/org/meepleai/api:staging-20250125-abc1234
export WEB_IMAGE=ghcr.io/org/meepleai/web:staging-20250125-abc1234
docker compose -f docker-compose.staging.yml up -d
```

#### Rollback via GitHub

1. Trova il workflow run precedente successful
2. Click "Re-run all jobs"
3. Oppure usa l'immagine specifica:

```bash
gh workflow run deploy-staging.yml --field version=staging-20250125-abc1234
```

---

### Monitoring Deploys

#### GitHub Actions UI

- **Actions tab** → Visualizza tutti i workflow runs
- **Environments** → Vedi deployment history per ambiente
- **Deployments** → Timeline di tutti i deploy

#### Slack Notifications

Configura `SLACK_WEBHOOK_URL` per ricevere:
- ✅ Deploy successful
- ❌ Deploy failed
- ⏸️ Approval pending
- 🔄 Rollback triggered

#### Deployment Summary

Ogni workflow genera un summary con:
- Version deployata
- Image tags
- URL ambiente
- Timestamp
- Actor

---

### Secrets Required

#### GitHub Repository Secrets

```bash
# Container Registry (automatico con GITHUB_TOKEN)
# Nessun secret aggiuntivo per GHCR

# Staging
STAGING_HOST=staging.meepleai.com
STAGING_USER=deploy
STAGING_SSH_KEY=-----BEGIN OPENSSH PRIVATE KEY-----...

# Production
PRODUCTION_HOST=meepleai.com
PRODUCTION_USER=deploy
PRODUCTION_SSH_KEY=-----BEGIN OPENSSH PRIVATE KEY-----...

# Notifications (opzionale)
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/...

# Code Coverage (opzionale)
CODECOV_TOKEN=...
```

#### Generare SSH Key

```bash
# Genera key pair per deploy
ssh-keygen -t ed25519 -C "github-actions-deploy" -f deploy_key

# Aggiungi public key al server
cat deploy_key.pub >> ~/.ssh/authorized_keys

# Aggiungi private key a GitHub Secrets
cat deploy_key  # Copia tutto, incluso BEGIN/END
```

---

## Troubleshooting

### Development

| Problema | Soluzione |
|----------|-----------|
| Container non partono | `docker compose logs <service>` |
| DB connection refused | Verifica che postgres sia healthy |
| API non risponde | Controlla `ASPNETCORE_URLS` e porta |
| Frontend 500 error | Verifica `NEXT_PUBLIC_API_BASE` in `.env.local` |
| Secrets mancanti | Ri-esegui `setup-secrets.ps1` |

### Staging/Production

| Problema | Soluzione |
|----------|-----------|
| Deploy fallito | Controlla GitHub Actions logs |
| SSL error | Verifica certificato e configurazione Traefik |
| 502 Bad Gateway | Controlla health check dei container |
| Performance lenta | Verifica resource limits e scaling |
| Secrets non caricati | Verifica path Docker Secrets |

---

## Riferimenti

- [Secret Management](./secrets/README.md)
- [CI/CD Pipeline](../.github/workflows/README.md)
- [Monitoring Setup](./monitoring/README.md)
- [Traefik Configuration](./traefik/README.md)
- [Disaster Recovery](./runbooks/disaster-recovery.md)

---

*Ultimo aggiornamento: 2026-01-26*
