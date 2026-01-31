# Guida Avvio Locale - Tutti gli Ambienti

**Ultima Revisione**: 2026-01-22
**Versione**: 1.0

---

## 📋 Indice

1. [Panoramica Ambienti](#panoramica-ambienti)
2. [Prerequisiti](#prerequisiti)
3. [Setup Iniziale](#setup-iniziale)
4. [Avvio Ambienti](#avvio-ambienti)
   - [Development](#development)
   - [Staging](#staging)
   - [Production](#production)
5. [Profili Docker Compose](#profili-docker-compose)
6. [Script di Avvio Rapido](#script-di-avvio-rapido)
7. [Verifica Servizi](#verifica-servizi)
8. [Gestione Ambiente](#gestione-ambiente)
9. [Troubleshooting](#troubleshooting)

---

## 🌍 Panoramica Ambienti

MeepleAI supporta 3 ambienti di deployment con configurazioni differenziate:

| Aspetto | Development | Staging | Production |
|---------|------------|---------|------------|
| **Scopo** | Sviluppo locale | Pre-release testing | Deployment production |
| **Secrets** | `.secret` files | Docker secrets | Docker secrets |
| **Database** | `meepleai` | `meepleai_staging` | `meepleai_prod` |
| **Restart Policy** | `unless-stopped` | `unless-stopped` | `always` |
| **Log Retention** | Default | 10MB × 3 files | 50MB × 10 files |
| **HTTPS** | HTTP only | HTTP | HTTPS + HTTP |
| **Resource Limits** | Moderate | Moderate | High |
| **Monitoring** | Basic | Full stack | Full stack + alerts |
| **Data Retention** | 30 giorni | 60 giorni | 90 giorni |

---

## 🔧 Prerequisiti

### Software Richiesto

- **Docker Desktop**: ≥ 4.20 (con Docker Compose V2)
- **PowerShell**: ≥ 7.0 (per script di setup)
- **.NET SDK**: ≥ 9.0 (per sviluppo API)
- **Node.js**: ≥ 20.x + pnpm (per sviluppo frontend)
- **Git**: Per version control

### Risorse Sistema

**Minimo Development**:
- CPU: 4 cores
- RAM: 8 GB
- Disk: 20 GB

**Consigliato Full Stack**:
- CPU: 8+ cores
- RAM: 16+ GB
- Disk: 50+ GB

---

## 🚀 Setup Iniziale

### 1. Clone Repository

```bash
git clone <repository-url>
cd meepleai-monorepo-dev
```

### 2. Genera Secrets (Automatico)

```powershell
cd infra/secrets
.\setup-secrets.ps1 -SaveGenerated
```

**Output**:
- Genera automaticamente password, JWT keys, API keys
- Salva in `infra/secrets/*.secret`
- Tempo risparmiato: 15-30 minuti

**File generati** (10 totali):
- `database.secret` - PostgreSQL credentials ✅ CRITICAL
- `redis.secret` - Redis password ✅ CRITICAL
- `qdrant.secret` - Qdrant API key ✅ CRITICAL
- `jwt.secret` - JWT signing keys ✅ CRITICAL
- `admin.secret` - Admin bootstrap ✅ CRITICAL
- `embedding-service.secret` - Embedding service auth ✅ CRITICAL
- `openrouter.secret` - OpenRouter API key ⚠️ IMPORTANT
- `oauth.secret` - OAuth providers ⚠️ IMPORTANT
- `bgg.secret` - BoardGameGeek API ⚠️ IMPORTANT
- `email.secret` - SMTP credentials (opzionale)

### 3. Configura Frontend (.env.local)

```bash
cd ../../apps/web
cp .env.development.example .env.local
```

**Modifica** `apps/web/.env.local`:
```bash
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

---

## 💻 Avvio Ambienti

### Development

**Caratteristiche**:
- Secrets da file `.secret` (non Docker secrets)
- Hot reload attivo (frontend)
- Password semplici (admin/admin per Grafana)
- Tutti i servizi di debug abilitati (Mailpit, cAdvisor)

#### Opzione 1: Docker Compose (Full Stack)

```bash
cd infra

# Avvio completo (tutti i servizi)
docker compose --profile full up -d

# Verifica stato
docker compose ps
```

#### Opzione 2: Script Rapidi

```bash
cd infra

# Minimal (solo core: Postgres, Redis, Qdrant, API, Web)
./start-minimal.sh

# AI (core + servizi ML)
./start-ai.sh

# Observability (core + monitoring)
./start-observability.sh

# Automation (core + n8n)
./start-automation.sh

# Full (tutti i servizi)
./start-full.sh
```

#### Opzione 3: Sviluppo Nativo (Consigliato)

**Backend**:
```bash
# Avvia solo infrastruttura
cd infra
docker compose up -d postgres qdrant redis

# API in locale (Terminal 1)
cd ../apps/api/src/Api
dotnet run  # http://localhost:8080
```

**Frontend**:
```bash
# Terminal 2
cd apps/web
pnpm dev  # http://localhost:3000
```

**Vantaggi**:
- Hot reload immediato
- Debug diretto in IDE
- Consumo risorse ridotto

---

### Staging

**Caratteristiche**:
- Simula ambiente production
- Docker secrets invece di `.secret` files
- Database separato (`meepleai_staging`)
- Log retention 10MB × 3 files
- Monitoring completo abilitato

#### Setup Secrets Staging

```bash
cd infra/secrets
mkdir -p staging

# Genera password per staging
pwsh -Command "& { [System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 })) }" > staging/redis-password.txt
```

#### Avvio Staging

```bash
cd infra

# Avvio con override staging
docker compose -f docker-compose.yml -f compose.staging.yml up -d

# Verifica
docker compose -f docker-compose.yml -f compose.staging.yml ps
```

**Variabili d'ambiente**:
```bash
# .env per staging (opzionale)
POSTGRES_DB=meepleai_staging
POSTGRES_USER=meeple
STAGING_API_URL=http://api:8080
```

---

### Production

**Caratteristiche**:
- Restart policy `always`
- HTTPS abilitato (certificati richiesti)
- Resource limits alti (CPU: 4, RAM: 8GB per API)
- Log retention 50MB × 10 files
- Data retention 90 giorni
- Richiede variabili obbligatorie

#### Setup Secrets Production

```bash
cd infra/secrets
mkdir -p prod

# Genera password sicure
pwsh -Command "& { [System.Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 })) }" > prod/postgres-password.txt
pwsh -Command "& { [System.Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 })) }" > prod/redis-password.txt
pwsh -Command "& { [System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 })) }" > prod/n8n-encryption-key.txt
```

#### Certificato TLS

```bash
# Genera certificato self-signed (solo per test locale)
cd infra/secrets/prod
openssl req -x509 -newkey rsa:4096 -keyout api-key.pem -out api-cert.pem -days 365 -nodes
openssl pkcs12 -export -out api-cert.pfx -inkey api-key.pem -in api-cert.pem -password pass:YourCertPassword
echo "YourCertPassword" > api-cert-password.txt
```

#### Avvio Production

```bash
cd infra

# Richiede variabili obbligatorie
export PRODUCTION_API_URL=https://your-domain.com
export GRAFANA_ROOT_URL=https://grafana.your-domain.com
export SEQ_PASSWORD_HASH=$(echo -n 'YourSeqPassword' | sha256sum | cut -d' ' -f1)

# Avvio con override production
docker compose -f docker-compose.yml -f compose.prod.yml up -d

# Verifica
docker compose -f docker-compose.yml -f compose.prod.yml ps
```

**⚠️ Variabili Obbligatorie**:
- `PRODUCTION_API_URL` - URL pubblico API
- `GRAFANA_ROOT_URL` - URL pubblico Grafana
- `SEQ_PASSWORD_HASH` - Hash password Seq (SHA256)

---

## 🎯 Profili Docker Compose

MeepleAI usa profili per avviare subset di servizi:

| Profilo | Servizi | Uso |
|---------|---------|-----|
| **minimal** | postgres, qdrant, redis, api, web | Sviluppo core |
| **dev** | minimal + prometheus, grafana, mailpit | Debug + monitoring |
| **ai** | minimal + ollama, embedding, unstructured, smoldocling, reranker | ML/AI development |
| **automation** | minimal + n8n | Workflow development |
| **observability** | dev + alertmanager, cadvisor, node-exporter | Full monitoring |
| **full** | Tutti i servizi | Stack completo |

### Esempi Uso Profili

```bash
# Solo core
docker compose --profile minimal up -d

# Core + AI services
docker compose --profile ai up -d

# Core + Monitoring
docker compose --profile observability up -d

# Tutto
docker compose --profile full up -d
```

---

## ⚡ Script di Avvio Rapido

**Posizione**: `infra/start-*.sh`

| Script | Profilo | Servizi Avviati | RAM Richiesta |
|--------|---------|-----------------|---------------|
| `start-minimal.sh` | minimal | 5 servizi | ~4 GB |
| `start-dev.sh` | dev | 8 servizi | ~6 GB |
| `start-ai.sh` | ai | 10 servizi | ~12 GB |
| `start-observability.sh` | observability | 11 servizi | ~8 GB |
| `start-automation.sh` | automation | 6 servizi | ~5 GB |
| `start-full.sh` | full | 17 servizi | ~18 GB |

### Uso Script

```bash
cd infra

# Rendi eseguibile (prima volta)
chmod +x start-*.sh

# Avvio
./start-dev.sh
```

---

## ✅ Verifica Servizi

### Health Check Automatico

```bash
cd infra

# Stato tutti i servizi
docker compose ps

# Health check specifico
docker compose ps api
docker compose ps web
```

### Test Manuale Endpoints

```bash
# API
curl http://localhost:8080/
curl http://localhost:8080/health

# Frontend
curl http://localhost:3000/

# Database (richiede psql)
psql -h localhost -U postgres -d meepleai -c "SELECT version();"

# Redis (richiede redis-cli)
redis-cli -h localhost -a $(grep REDIS_PASSWORD infra/secrets/redis.secret | cut -d'=' -f2) PING

# Qdrant
curl http://localhost:6333/
```

### URLs Servizi

**Core**:
- **Frontend**: http://localhost:3000
- **API**: http://localhost:8080
- **API Docs**: http://localhost:8080/scalar/v1

**Database**:
- **PostgreSQL**: localhost:5432 (postgres/meeplepass)
- **Redis**: localhost:6379 (password in redis.secret)
- **Qdrant**: http://localhost:6333

**AI Services**:
- **Embedding**: http://localhost:8000/health
- **Reranker**: http://localhost:8003/health
- **Unstructured**: http://localhost:8001/health
- **SmolDocling**: http://localhost:8002/health
- **Ollama**: http://localhost:11434

**Monitoring**:
- **Grafana**: http://localhost:3001 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Alertmanager**: http://localhost:9093
- **cAdvisor**: http://localhost:8082

**Automation**:
- **n8n**: http://localhost:5678 (admin/n8nadmin)
- **Mailpit**: http://localhost:8025

---

## 🛠️ Gestione Ambiente

### Start/Stop Servizi

```bash
cd infra

# Stop
docker compose down

# Stop con rimozione volumi (ATTENZIONE: DATI PERSI!)
docker compose down -v

# Restart servizio specifico
docker compose restart api
docker compose restart web

# Stop servizio specifico
docker compose stop api
```

### Logs

```bash
# Tutti i servizi
docker compose logs -f

# Servizio specifico
docker compose logs -f api
docker compose logs -f web

# Ultimi 100 log
docker compose logs --tail=100 smoldocling-service

# Segui in tempo reale
docker compose logs -f --tail=50 api web
```

### Update Configurazione

```bash
# Dopo modifica docker-compose.yml o .secret
docker compose up -d --force-recreate

# Rebuild immagini (dopo modifica Dockerfile)
docker compose build --no-cache api web
docker compose up -d
```

### Reset Completo

```bash
cd infra

# Stop e rimozione volumi
docker compose down -v

# Rimozione immagini (opzionale)
docker compose down --rmi all

# Rigenerazione secrets
cd secrets
pwsh setup-secrets.ps1 -SaveGenerated

# Restart
cd ..
docker compose --profile full up -d
```

---

## 🔍 Troubleshooting

### Problemi Comuni

#### 1. Servizio Unhealthy

```bash
# Verifica logs
docker compose logs service-name --tail=100

# Restart
docker compose restart service-name

# Health check manuale
docker compose exec service-name curl http://localhost:PORT/health
```

#### 2. Database Connection Failed

```bash
# Verifica PostgreSQL
docker compose ps postgres

# Test connessione
docker compose exec postgres psql -U postgres -c "SELECT version();"

# Controlla secrets
cat infra/secrets/database.secret
```

#### 3. Port Already in Use

```bash
# Windows: trova processo
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Linux/Mac
lsof -i :8080
kill -9 <PID>
```

#### 4. Secrets Non Trovati

```bash
# Verifica esistenza
ls -la infra/secrets/*.secret

# Rigenera
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated
```

#### 5. SmolDocling Warmup Lento

**Normale**: Model download richiede 2-5 minuti

```bash
# Monitora progresso
docker compose logs -f smoldocling-service

# Verifica spazio disco
df -h

# Eventualmente disabilita warmup
# In docker-compose.yml: ENABLE_MODEL_WARMUP=false
```

#### 6. Frontend Proxy Error

**Causa**: API non raggiungibile

```bash
# Verifica API
curl http://localhost:8080/health

# Verifica .env.local
cat apps/web/.env.local
# Deve contenere: NEXT_PUBLIC_API_BASE=http://localhost:8080
```

#### 7. JWT Token Invalid

**Causa**: Secret JWT cambiato

```bash
# Rigenera JWT secret
cd infra/secrets
pwsh -Command "& { [System.Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 })) }" > jwt.secret

# Restart API
cd ..
docker compose restart api
```

### Performance Issues

#### High Memory Usage

```bash
# Verifica uso risorse
docker stats

# Riduci servizi attivi (usa profilo minimal)
docker compose --profile minimal up -d

# Aumenta Docker Desktop RAM limit
# Settings → Resources → Memory
```

#### Slow Startup

**Normale**: Full stack richiede 3-5 minuti

**Ottimizzazioni**:
1. Usa profili specifici invece di `full`
2. Disabilita servizi non necessari
3. Pre-download immagini: `docker compose pull`

---

## 🔨 Comandi Sviluppo

### Backend (.NET API)

#### Esecuzione

```bash
cd apps/api/src/Api

# Avvio API (Development)
dotnet run
# Output: http://localhost:8080

# Avvio con watch (hot reload)
dotnet watch run

# Avvio con configurazione specifica
dotnet run --environment Development
dotnet run --environment Staging
```

#### Build

```bash
cd apps/api/src/Api

# Build
dotnet build

# Build Release
dotnet build -c Release

# Clean + Build
dotnet clean && dotnet build

# Restore dependencies
dotnet restore
```

#### Test

```bash
cd apps/api

# Tutti i test
dotnet test

# Test specifici
dotnet test --filter "FullyQualifiedName~GameTests"
dotnet test --filter "FullyQualifiedName~AuthenticationTests"

# Test con coverage
dotnet test /p:CollectCoverage=true
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Test verbose
dotnet test --logger "console;verbosity=detailed"

# Test con retry (flaky tests)
dotnet test --logger "console;verbosity=normal" -- NUnit.MaxRetries=3
```

#### Migrations

```bash
cd apps/api/src/Api

# Crea migration
dotnet ef migrations add MigrationName

# Applica migrations
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName

# Lista migrations
dotnet ef migrations list

# Rimuovi ultima migration (non applicata)
dotnet ef migrations remove

# Script SQL migration
dotnet ef migrations script
dotnet ef migrations script --output migration.sql
```

#### Code Quality

```bash
cd apps/api

# Format code
dotnet format

# Analisi statica (Roslyn analyzers)
dotnet build /p:TreatWarningsAsErrors=true

# Security scan (se configurato Semgrep)
semgrep --config auto apps/api/
```

---

### Frontend (Next.js)

#### Esecuzione

```bash
cd apps/web

# Development (hot reload)
pnpm dev
# Output: http://localhost:3000

# Production build + start
pnpm build
pnpm start

# Sviluppo con Turbopack (più veloce)
pnpm dev --turbo
```

#### Build

```bash
cd apps/web

# Build production
pnpm build

# Build con analisi bundle
pnpm build --analyze

# Clean + Build
rm -rf .next && pnpm build

# Type check (senza build)
pnpm typecheck

# Lint
pnpm lint

# Lint + fix
pnpm lint --fix
```

#### Test

```bash
cd apps/web

# Unit tests (Vitest)
pnpm test

# Test con UI interattiva
pnpm test:ui

# Test con coverage
pnpm test:coverage

# Test watch mode
pnpm test:watch

# E2E tests (Playwright)
pnpm test:e2e

# E2E con UI interattiva (Playwright UI Mode)
pnpm test:e2e:ui

# E2E headed mode (vedi il browser)
pnpm test:e2e:headed

# E2E debug mode
pnpm test:e2e:debug

# E2E specifico
pnpm test:e2e tests/e2e/login.spec.ts

# E2E su browser specifico
pnpm test:e2e --project=chromium
pnpm test:e2e --project=firefox
pnpm test:e2e --project=webkit

# Genera report Playwright
pnpm playwright show-report
```

#### Code Quality

```bash
cd apps/web

# Type check
pnpm typecheck

# Lint
pnpm lint

# Format (Prettier)
pnpm format

# Fix all
pnpm lint --fix && pnpm format --write
```

#### Generazione API Client

```bash
cd apps/web

# Genera TypeScript client da OpenAPI spec
pnpm generate:api

# Verifica API spec aggiornata
curl http://localhost:8080/swagger/v1/swagger.json > temp.json
```

---

### E2E Testing con UI (Playwright)

#### Setup Playwright

```bash
cd apps/web

# Installa Playwright browsers (prima volta)
pnpm playwright install

# Installa con dependencies OS
pnpm playwright install --with-deps
```

#### Esecuzione E2E UI Mode

```bash
cd apps/web

# UI Mode interattivo (CONSIGLIATO)
pnpm test:e2e:ui

# Features UI Mode:
# - Watch mode automatico
# - Time travel debugging
# - Step-by-step execution
# - Screenshot viewer
# - Network inspector
# - Trace viewer integrato
```

**UI Mode Workflow**:
1. Avvia `pnpm test:e2e:ui`
2. Seleziona test da pannello sinistro
3. Click ▶️ per eseguire test
4. Vedi esecuzione real-time con screenshot
5. Click su step per time-travel debugging
6. Ispeziona DOM, network, console

#### Debug E2E

```bash
cd apps/web

# Debug mode (pausa su errori)
pnpm test:e2e:debug

# Headed mode (vedi browser)
pnpm test:e2e:headed

# Inspector Playwright
pnpm playwright test --debug

# Con browser specifico
pnpm playwright test --debug --project=chromium
```

#### Trace e Report

```bash
cd apps/web

# Esegui con trace (registra tutto)
pnpm test:e2e --trace on

# Mostra report HTML
pnpm playwright show-report

# Apri trace viewer
pnpm playwright show-trace trace.zip
```

---

### Documentazione Screenshot

#### Setup QuestPDF (Backend)

```bash
cd apps/api/src/Api

# Installazione dipendenze
dotnet add package QuestPDF
dotnet add package QuestPDF.Previewer

# Verifica installazione
dotnet list package | grep QuestPDF
```

#### Generazione PDF Documentazione

**Opzione 1: API Endpoint**

```bash
# Avvia API
cd apps/api/src/Api
dotnet run

# Genera documentazione via API
curl -X POST http://localhost:8080/api/v1/documentation/generate \
  -H "Content-Type: application/json" \
  -d '{"type": "system-architecture"}' \
  --output docs/architecture.pdf
```

**Opzione 2: Script Playwright (Screenshot Automation)**

```typescript
// apps/web/scripts/generate-docs-screenshots.ts
import { chromium } from '@playwright/test';

async function captureScreenshots() {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  // Navigate to pages
  await page.goto('http://localhost:3000/admin/dashboard');
  await page.screenshot({
    path: 'docs/screenshots/admin-dashboard.png',
    fullPage: true
  });

  await page.goto('http://localhost:3000/games');
  await page.screenshot({
    path: 'docs/screenshots/games-catalog.png',
    fullPage: true
  });

  await browser.close();
}

captureScreenshots();
```

**Esecuzione**:
```bash
cd apps/web

# Aggiungi script in package.json
# "generate:docs-screenshots": "tsx scripts/generate-docs-screenshots.ts"

# Esegui
pnpm generate:docs-screenshots
```

**Opzione 3: Playwright Codegen (Interattivo)**

```bash
cd apps/web

# Avvia codegen (genera automaticamente script)
pnpm playwright codegen http://localhost:3000

# Workflow:
# 1. Naviga nell'UI
# 2. Playwright genera codice automaticamente
# 3. Aggiungi screenshot commands:
#    await page.screenshot({ path: 'screenshot.png' });
# 4. Salva script generato
```

#### Screenshot con Annotations

```typescript
// Test con screenshot annotati
import { test } from '@playwright/test';

test('Genera screenshot documentazione', async ({ page }) => {
  await page.goto('http://localhost:3000/admin');

  // Fullpage screenshot
  await page.screenshot({
    path: 'docs/screenshots/admin-full.png',
    fullPage: true
  });

  // Element screenshot
  const sidebar = page.locator('[data-testid="sidebar"]');
  await sidebar.screenshot({
    path: 'docs/screenshots/sidebar.png'
  });

  // Screenshot con annotazioni
  await page.evaluate(() => {
    // Aggiungi markers per documentazione
    const el = document.querySelector('.important-feature');
    el?.classList.add('docs-highlight');
  });

  await page.screenshot({
    path: 'docs/screenshots/feature-highlight.png'
  });
});
```

#### Organizzazione Screenshots

```
docs/
├── screenshots/
│   ├── admin/
│   │   ├── dashboard.png
│   │   ├── users-list.png
│   │   └── settings.png
│   ├── games/
│   │   ├── catalog.png
│   │   ├── game-detail.png
│   │   └── search-results.png
│   └── auth/
│       ├── login.png
│       ├── register.png
│       └── oauth-flow.png
└── README.md
```

#### Script Automatico Screenshot

```bash
# apps/web/scripts/screenshot-all-pages.sh
#!/bin/bash

PAGES=(
  "/"
  "/games"
  "/games/search"
  "/admin/dashboard"
  "/admin/users"
  "/settings"
)

for page in "${PAGES[@]}"; do
  filename=$(echo "$page" | sed 's/\//-/g' | sed 's/^-//')
  pnpm playwright screenshot "http://localhost:3000$page" "docs/screenshots/${filename}.png" --full-page
done
```

**Uso**:
```bash
cd apps/web
chmod +x scripts/screenshot-all-pages.sh
./scripts/screenshot-all-pages.sh
```

---

### Workflow Completo Sviluppo

#### 1. Setup Iniziale (Una Volta)

```bash
# Clone + setup
git clone <repo>
cd meepleai-monorepo-dev

# Secrets
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# Frontend env
cd ../../apps/web
cp .env.development.example .env.local

# Dependencies
cd ../api/src/Api && dotnet restore
cd ../../../web && pnpm install
cd ../../../infra && docker compose pull
```

#### 2. Sviluppo Quotidiano

**Terminal 1 - Infrastruttura**:
```bash
cd infra
docker compose --profile minimal up -d
```

**Terminal 2 - Backend**:
```bash
cd apps/api/src/Api
dotnet watch run
```

**Terminal 3 - Frontend**:
```bash
cd apps/web
pnpm dev
```

**Terminal 4 - Test Watch** (opzionale):
```bash
cd apps/web
pnpm test:watch
```

#### 3. Pre-Commit Checks

```bash
# Backend
cd apps/api
dotnet build
dotnet test
dotnet format --verify-no-changes

# Frontend
cd ../web
pnpm typecheck
pnpm lint
pnpm test
pnpm build
```

#### 4. Feature Completa

```bash
# 1. Crea branch
git checkout -b feature/issue-123-add-search

# 2. Sviluppa + Test
# (sviluppo in watch mode)

# 3. Pre-commit
cd apps/api && dotnet test
cd ../web && pnpm typecheck && pnpm lint && pnpm test

# 4. E2E
cd apps/web && pnpm test:e2e

# 5. Commit
git add .
git commit -m "feat(game): add complexity filter"

# 6. Push + PR
git push -u origin feature/issue-123-add-search
```

---

## 📚 Riferimenti

- **Documentazione Completa**: `docs/02-development/README.md`
- **Setup Secrets**: `docs/02-development/local-secrets-setup.md`
- **Test URLs**: `docs/02-development/docker-services-test-urls.md`
- **Git Workflow**: `docs/02-development/git-workflow.md`
- **Troubleshooting**: `docs/02-development/troubleshooting/`

---

## 🎓 Best Practices

1. **Development**: Usa sviluppo nativo (API + Web locali) + Docker solo per infrastruttura
2. **Secrets**: NON committare mai file `.secret`, usa `.gitignore`
3. **Resource Limits**: Monitora con `docker stats`, regola profili
4. **Backup**: Esporta volumi importanti prima di `down -v`
5. **Update**: Mantieni immagini aggiornate con `docker compose pull`
6. **Logs**: Usa `--tail` per evitare sovraccarico console
7. **Profili**: Preferisci profili specifici a `full` per risparmiare risorse

---

**Versione Documento**: 1.1
**Ultima Revisione**: 2026-01-22
**Autore**: MeepleAI Team
**Changelog**: v1.1 - Aggiunti comandi sviluppo, build, test, E2E UI, screenshot automation
