# Guida Setup Locale - MeepleAI

**Versione**: 1.0
**Ultima Modifica**: 2025-11-15
**Stato Progetto**: Alpha (DDD Migration 99% Complete)

---

## 📋 Indice

1. [Panoramica](#-panoramica)
2. [Prerequisiti](#-prerequisiti)
3. [Setup Iniziale](#-setup-iniziale)
4. [Configurazione Ambiente](#-configurazione-ambiente)
5. [Avvio Servizi](#-avvio-servizi)
6. [Verifica Installazione](#-verifica-installazione)
7. [Test Disponibili](#-test-disponibili)
8. [Sviluppo Quotidiano](#-sviluppo-quotidiano)
9. [Troubleshooting](#-troubleshooting)
10. [Risorse Aggiuntive](#-risorse-aggiuntive)

---

## 🎯 Panoramica

**MeepleAI** è un assistente AI per regolamenti di giochi da tavolo, con focus sul mercato italiano.

### Stack Tecnologico

| Componente | Tecnologia | Versione |
|------------|-----------|----------|
| **Backend** | ASP.NET Core | 9.0 |
| **Frontend** | Next.js + React | 16.0 + 19.2 |
| **Database** | PostgreSQL | 16 |
| **Vector DB** | Qdrant | Latest |
| **Cache** | Redis | Latest |
| **LLM** | OpenRouter API | - |
| **Embedding** | Ollama | Latest |
| **PDF Processing** | Unstructured + SmolDocling + Docnet | - |
| **Workflow** | n8n | Latest |
| **Observability** | Seq + Jaeger + Prometheus + Grafana | Latest |

### Architettura

**Domain-Driven Design (DDD) - 99% Completo**:
- 7 Bounded Contexts con pattern CQRS/MediatR
- 72+ handlers (Commands/Queries)
- 60+ endpoints migrati
- 2,070 righe di codice legacy eliminate

**Bounded Contexts**:
1. **Authentication**: Autenticazione, sessioni, API keys, OAuth, 2FA
2. **GameManagement**: Catalogo giochi, sessioni di gioco
3. **KnowledgeBase**: RAG, vettori, chat (Hybrid: vector+keyword RRF)
4. **DocumentProcessing**: Upload PDF, estrazione, validazione
5. **WorkflowIntegration**: Workflow n8n, logging errori
6. **SystemConfiguration**: Configurazione runtime, feature flags
7. **Administration**: Gestione utenti, alert, audit, analytics

---

## 💻 Prerequisiti

### Software Richiesto

#### 1. Docker & Docker Compose

**Linux/macOS**:
```bash
# Verifica installazione
docker --version   # Richiesto: >= 24.0
docker compose version  # Richiesto: >= 2.20

# Se non installato, segui: https://docs.docker.com/get-docker/
```

**Windows**:
- Installa **Docker Desktop** da https://www.docker.com/products/docker-desktop/
- Verifica che WSL 2 sia abilitato

#### 2. .NET SDK

```bash
# Installa .NET 9 SDK
# Scarica da: https://dotnet.microsoft.com/download/dotnet/9.0

# Verifica installazione
dotnet --version   # Richiesto: >= 9.0.0
```

#### 3. Node.js & pnpm

```bash
# Installa Node.js 20+ da: https://nodejs.org/

# Verifica installazione
node --version     # Richiesto: >= 20.0.0

# Installa pnpm globalmente
npm install -g pnpm@9

# Verifica installazione
pnpm --version     # Richiesto: >= 9.0.0
```

#### 4. Git

```bash
# Verifica installazione
git --version      # Richiesto: >= 2.30

# Se non installato:
# Ubuntu/Debian: sudo apt install git
# macOS: brew install git
# Windows: https://git-scm.com/download/win
```

### Hardware Raccomandato

| Componente | Minimo | Raccomandato |
|------------|--------|--------------|
| **CPU** | 4 core | 8+ core |
| **RAM** | 8 GB | 16+ GB |
| **Disco** | 20 GB liberi | 50+ GB SSD |
| **Rete** | Connessione stabile | Banda ≥ 10 Mbps |

### Account Richiesti

1. **OpenRouter** (per LLM):
   - Registrati su https://openrouter.ai
   - Aggiungi credito ($10 minimo per test)
   - Genera una API key

2. **GitHub** (opzionale, per contribuire):
   - Account con accesso al repository
   - Token personale per clonare repo privati

---

## 🚀 Setup Iniziale

### 1. Clone del Repository

```bash
# Clone del repository (HTTPS)
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo

# OPPURE con SSH (se hai configurato le chiavi)
git clone git@github.com:DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo

# Verifica che sei sul branch principale
git branch
# Output atteso: * main
```

### 2. Inizializzazione Docker Secrets

**IMPORTANTE**: Il progetto utilizza Docker Secrets per gestire credenziali sensibili.

```bash
# Naviga nella cartella secrets
cd tools/secrets

# Esegui lo script di inizializzazione
./init-secrets.sh

# Lo script ti chiederà di configurare:
# - Password PostgreSQL
# - Chiavi di crittografia
# - Credenziali admin iniziali
# - API keys (OpenRouter, ecc.)

# Puoi usare i valori di default per sviluppo locale premendo ENTER
```

**Nota**: I secrets vengono salvati in `infra/secrets/*.txt` (gitignored).

### 3. Installazione Dipendenze Backend

```bash
# Torna alla root del progetto
cd ../..

# Naviga al progetto API
cd apps/api

# Restore delle dipendenze NuGet
dotnet restore

# Verifica che non ci siano errori
dotnet build

# Output atteso: Build succeeded. 0 Warning(s). 0 Error(s).
```

### 4. Installazione Dipendenze Frontend

```bash
# Torna alla root
cd ../..

# Naviga al progetto Web
cd apps/web

# Installa dipendenze con pnpm
pnpm install

# Questo può richiedere 2-5 minuti
```

---

## ⚙️ Configurazione Ambiente

### 1. Variabili d'Ambiente - Backend (API)

```bash
# Naviga alla cartella infra/env
cd ../../infra/env

# Copia il template per l'ambiente di sviluppo
cp api.env.dev.example api.env.dev

# Modifica il file con il tuo editor
nano api.env.dev  # oppure code api.env.dev per VS Code
```

**Configurazione `api.env.dev`** (parametri principali):

> **⚠️ IMPORTANTE**: Se esegui l'meepleai-api in Docker Compose, sostituisci `localhost` con i nomi dei servizi Docker:
> - `localhost` → `postgres` (database)
> - `localhost:6333` → `http://meepleai-qdrant:6333` (vector DB)
> - `localhost:6379` → `meepleai-redis:6379` (cache)
> - `localhost:8081` → `http://seq:5341` (logging - porta ingestion)
> - `localhost:8001` → `http://unstructured:8001` (PDF processing)
> - `localhost:8002` → `http://smoldocling:8002` (PDF processing)

```bash
# ============================================
# DATABASE
# ============================================
# Se API in Docker: Host=meepleai-postgres
# Se API fuori Docker: Host=localhost
CONNECTIONSTRINGS__POSTGRES=Host=localhost;Port=5432;Database=meepleai;Username=meepleai_user;Password=meepleai_dev_password

# ============================================
# SERVIZI ESTERNI
# ============================================
# Se API in Docker: http://meepleai-qdrant:6333 e meepleai-redis:6379
# Se API fuori Docker: http://localhost:6333 e localhost:6379
QDRANT_URL=http://localhost:6333
REDIS_URL=localhost:6379

# ============================================
# AI/LLM
# ============================================
OPENROUTER_API_KEY=sk-or-v1-YOUR_API_KEY_HERE

# Provider: "OpenRouter" o "Ollama"
AI__PROVIDER=OpenRouter

# Modelli
AI__OPENROUTER__EMBEDDINGMODEL=text-embedding-3-large
AI__OPENROUTER__CHATMODEL=anthropic/claude-3.5-sonnet

# ============================================
# OSSERVABILITÀ
# ============================================
# Se API in Docker: http://seq:5341 (porta ingestion, NON 8081!)
# Se API fuori Docker: http://localhost:5341
# Porta 8081 è solo per la UI web di Seq
SEQ_URL=http://localhost:5341
JAEGER_AGENT_HOST=localhost
JAEGER_AGENT_PORT=6831

# ============================================
# AMMINISTRAZIONE
# ============================================
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=Admin123!

# ============================================
# PDF PROCESSING
# ============================================
PDFPROCESSING__EXTRACTOR__PROVIDER=Orchestrator
# Se API in Docker: http://unstructured:8001 e http://smoldocling:8002
# Se API fuori Docker: http://localhost:8001 e http://localhost:8002
PDFPROCESSING__UNSTRUCTURED__BASEURL=http://localhost:8001
PDFPROCESSING__SMOLDOCLING__BASEURL=http://localhost:8002

# ============================================
# FEATURE FLAGS
# ============================================
FEATURES__ENABLE2FA=true
FEATURES__ENABLEOAUTH=true
```

### 2. Variabili d'Ambiente - Frontend (Web)

```bash
# Copia il template
cp web.env.dev.example web.env.dev

# Modifica il file
nano web.env.dev
```

**Configurazione `web.env.dev`**:

```bash
# ============================================
# API BACKEND
# ============================================
NEXT_PUBLIC_API_BASE=http://localhost:8080

# ============================================
# AMBIENTE
# ============================================
NODE_ENV=development

# ============================================
# FEATURES
# ============================================
NEXT_PUBLIC_ENABLE_ANALYTICS=false
```

### 3. Variabili d'Ambiente - n8n (Workflow)

```bash
# Copia il template
cp n8n.env.dev.example n8n.env.dev

# Modifica se necessario (i default vanno bene per sviluppo locale)
```

---

## 🐳 Avvio Servizi

### Architettura dei Servizi

Il progetto utilizza 15 servizi Docker orchestrati con Docker Compose:

**Core Services**:
- `postgres` (5432) - Database principale
- `qdrant` (6333) - Vector database
- `redis` (6379) - Cache

**AI/ML Services**:
- `ollama` (11434) - Embedding locale
- `embedding` (8000) - Servizio embedding
- `unstructured` (8001) - PDF extraction (Stage 1)
- `smoldocling` (8002) - PDF extraction VLM (Stage 2)

**Observability Services**:
- `seq` (8081) - Logging centralizzato
- `jaeger` (16686) - Distributed tracing
- `prometheus` (9090) - Metriche
- `alertmanager` (9093) - Alert management
- `grafana` (3001) - Dashboards

**Workflow**:
- `n8n` (5678) - Automazione workflow

**Application**:
- `api` (8080) - Backend ASP.NET
- `web` (3000) - Frontend Next.js

### Avvio Completo (Tutte le dipendenze)

```bash
# Dalla root del progetto
cd infra

# Avvia TUTTI i servizi
docker compose up -d

# Verifica che tutti i container siano running
docker compose ps

# Output atteso: tutti i servizi con stato "Up"
```

**Nota**: Il primo avvio può richiedere 10-15 minuti per scaricare tutte le immagini Docker (~5-8 GB).

### Avvio Servizi Core (Setup Minimo)

Per sviluppo leggero, avvia solo i servizi essenziali:

```bash
cd infra

# Avvia solo i servizi core
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-seq

# Verifica
docker compose ps
```

### Verifica Salute Servizi

```bash
# PostgreSQL
docker compose logs meepleai-postgres | tail -20
# Cerca: "database system is ready to accept connections"

# Qdrant
curl http://localhost:6333/healthz
# Output atteso: {"title":"healthz","version":"1.x.x"}

# Redis
docker compose exec meepleai-redis meepleai-redis-cli ping
# Output atteso: PONG

# Seq
curl http://localhost:8081/api/health
# Output atteso: Status 200

# Ollama (se avviato)
curl http://localhost:11434/api/tags
# Output atteso: JSON con lista modelli
```

### Avvio Backend (API)

**Opzione 1: Con Docker Compose** (raccomandato per produzione):

```bash
# Dalla root
cd infra
docker compose up -d meepleai-api

# Verifica logs
docker compose logs -f meepleai-api
```

**Opzione 2: Direttamente con dotnet** (raccomandato per sviluppo):

```bash
# Dalla root
cd apps/api/src/Api

# Avvia l'applicazione
dotnet run

# Output atteso:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:8080
# info: Microsoft.Hosting.Lifetime[0]
#       Application started. Press Ctrl+C to shut down.
```

**Nota**: Le migration vengono applicate automaticamente all'avvio.

### Avvio Frontend (Web)

```bash
# Dalla root
cd apps/web

# Avvia il development server
pnpm dev

# Output atteso:
# ▲ Next.js 16.0.1
# - Local:        http://localhost:3000
# - Ready in 2.5s
```

### Download Modelli Ollama (Se usi Ollama per LLM)

```bash
# Scarica il modello per embedding
docker exec -it ollama ollama pull nomic-embed-text

# Scarica modelli per chat (opzionale, se non usi OpenRouter)
docker exec -it ollama ollama pull mistral:7b-instruct-v0.3-q4_K_M

# Verifica modelli installati
docker exec -it ollama ollama list
```

---

## ✅ Verifica Installazione

### 1. Health Check API

```bash
# Verifica endpoint di health
curl http://localhost:8080/health

# Output atteso (JSON):
{
  "status": "Healthy",
  "checks": {
    "postgres": "Healthy",
    "redis": "Healthy",
    "qdrant": "Healthy"
  }
}
```

### 2. Verifica Frontend

Apri il browser e naviga a:

```
http://localhost:3000
```

Dovresti vedere la homepage di MeepleAI.

### 3. Test Login Admin

```bash
# Usa le credenziali configurate in api.env.dev
# Default:
# Email: admin@meepleai.dev
# Password: Admin123!
```

1. Vai su http://localhost:3000/login
2. Inserisci le credenziali admin
3. Dovresti essere reindirizzato alla dashboard

### 4. Verifica API Endpoints

```bash
# Test endpoint pubblico
curl http://localhost:8080/api/v1/health

# Test endpoint autenticato (dopo login, usa il cookie di sessione)
curl -b cookies.txt http://localhost:8080/api/v1/games
```

### 5. Verifica Servizi Osservabilità

**Seq (Logs)**:
```
http://localhost:8081
```

**Jaeger (Tracing)**:
```
http://localhost:16686
```

**Grafana (Dashboards)**:
```
http://localhost:3001
Username: admin
Password: admin
```

**Prometheus (Metriche)**:
```
http://localhost:9090
```

**n8n (Workflow)**:
```
http://localhost:5678
```

---

## 🧪 Test Disponibili

### Panoramica Test

| Tipo | Framework | Count | Coverage |
|------|-----------|-------|----------|
| **Backend Unit** | xUnit | 162 | 90%+ |
| **Frontend Unit** | Jest | 4,033 | 90.03% |
| **E2E** | Playwright | 30+ | - |
| **Integration** | Testcontainers | Inclusi in Backend | - |
| **TOTALE** | - | **4,225+** | **90%+** |

### Test Backend (API)

#### Eseguire Tutti i Test

```bash
# Dalla root
cd apps/api

# Esegui tutti i test
dotnet test

# Output atteso:
# Passed!  - Failed:     0, Passed:   162, Skipped:     0, Total:   162, Duration: 45s
```

#### Test con Coverage

```bash
# Test con coverage report
dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=opencover

# Genera report HTML (richiede reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator \
  -reports:"**/coverage.opencover.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:"Html"

# Apri il report
open coverage-report/index.html  # macOS
xdg-open coverage-report/index.html  # Linux
start coverage-report/index.html  # Windows
```

#### Test per Categoria

**1. Test Domain Models & Value Objects**:
```bash
dotnet test --filter "FullyQualifiedName~Domain"

# Esempio test specifico
dotnet test --filter "FullyQualifiedName~EmailTests"
```

**2. Test CQRS Handlers**:
```bash
# Test Commands
dotnet test --filter "FullyQualifiedName~CommandHandler"

# Test Queries
dotnet test --filter "FullyQualifiedName~QueryHandler"

# Esempio specifico: Login
dotnet test --filter "FullyQualifiedName~LoginCommandHandlerTests"
```

**3. Test Integration (Testcontainers)**:
```bash
# Tutti i test di integrazione
dotnet test --filter "FullyQualifiedName~Integration"

# Test PDF Processing
dotnet test --filter "FullyQualifiedName~UnstructuredPdfExtractionIntegrationTests"

# Test OAuth
dotnet test --filter "FullyQualifiedName~OAuthIntegrationTests"
```

**4. Test Cross-Context (DDD)**:
```bash
# Test interazioni tra bounded contexts
dotnet test --filter "FullyQualifiedName~CrossContext"

# Esempio: Authentication + GameManagement
dotnet test --filter "FullyQualifiedName~AuthenticationGameManagementCrossContextTests"
```

**5. Test RAG & AI**:
```bash
# Test RAG Service
dotnet test --filter "FullyQualifiedName~RagService"

# Test Performance RAG
dotnet test --filter "FullyQualifiedName~RagServicePerformanceTests"
```

**6. Test PDF Pipeline**:
```bash
# Test orchestratore PDF (3-stage)
dotnet test --filter "FullyQualifiedName~EnhancedPdfProcessingOrchestrator"

# Test E2E PDF pipeline
dotnet test --filter "FullyQualifiedName~ThreeStagePdfPipelineE2ETests"
```

### Test Frontend (Web)

#### Eseguire Tutti i Test

```bash
# Dalla root
cd apps/web

# Esegui tutti i test
pnpm test

# Output atteso:
# Test Suites: 450 passed, 450 total
# Tests:       4033 passed, 4033 total
```

#### Test con Coverage

```bash
# Test con coverage
pnpm test:coverage

# Report viene generato in coverage/
# Apri coverage/lcov-report/index.html nel browser
```

#### Test in Watch Mode

```bash
# Utile durante sviluppo
pnpm test:watch

# Premi 'a' per eseguire tutti i test
# Premi 'p' per filtrare per nome file
# Premi 'q' per uscire
```

#### Test E2E con Playwright

```bash
# Esegui test E2E
pnpm test:e2e

# Esegui in UI mode (interattivo)
pnpm test:e2e:ui

# Genera report
pnpm test:e2e:report
```

**Categorie Test E2E Disponibili**:

1. **Autenticazione**:
   - `auth-2fa-complete.spec.ts` - Test 2FA completo
   - `auth-oauth-advanced.spec.ts` - OAuth flow avanzato
   - `auth-password-reset.spec.ts` - Reset password

2. **Chat**:
   - `chat.spec.ts` - Funzionalità chat base
   - `chat-streaming.spec.ts` - Streaming risposte
   - `chat-edit-delete.spec.ts` - Editing messaggi
   - `chat-export.spec.ts` - Export conversazioni

3. **Admin**:
   - `admin.spec.ts` - Dashboard admin
   - `admin-users.spec.ts` - Gestione utenti
   - `admin-configuration.spec.ts` - Configurazione sistema
   - `admin-analytics.spec.ts` - Analytics

4. **Accessibilità**:
   - `accessibility.spec.ts` - Test WCAG compliance

#### Test Specifici

```bash
# Test componente specifico
pnpm test -- ChatMessage

# Test file specifico
pnpm test -- apps/web/__tests__/components/ChatMessage.test.tsx
```

#### Test di Accessibilità

```bash
# Test solo accessibilità
pnpm test:a11y

# Audit completo accessibilità
pnpm audit:a11y
```

### Script di Test Automatizzati

#### Measure Coverage (PowerShell)

```bash
# Dalla root
pwsh tools/measure-coverage.ps1

# Solo API con report HTML
pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml

# Solo Web
pwsh tools/measure-coverage.ps1 -Project web
```

#### Coverage Trends

```bash
# Bash (Linux/macOS)
bash tools/coverage-trends.sh

# PowerShell (Windows)
pwsh tools/coverage-trends.ps1
```

### CI/CD Test Workflow

I test vengono eseguiti automaticamente su GitHub Actions:

**Workflow**: `.github/workflows/ci.yml`

**Jobs**:
1. `ci-web`: Lint → Typecheck → Test → Coverage
2. `ci-api`: Build → Test → Coverage

**Trigger**:
- Push su `main`
- Pull Request verso `main`

---

## 💡 Sviluppo Quotidiano

### Workflow Tipico

**1. Inizio Giornata**:
```bash
# Aggiorna il repository
git pull origin main

# Avvia servizi core
cd infra
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-seq

# Avvia API (in un terminale)
cd apps/api/src/Api
dotnet run

# Avvia Web (in un altro terminale)
cd apps/web
pnpm dev
```

**2. Durante Sviluppo**:
```bash
# Test watch mode (opzionale)
pnpm test:watch

# Verifica linting
pnpm lint

# Typecheck
pnpm typecheck
```

**3. Prima di Committare**:
```bash
# Backend
cd apps/api
dotnet build
dotnet test

# Frontend
cd apps/web
pnpm lint
pnpm typecheck
pnpm test
```

**4. Commit**:
```bash
git add .
git commit -m "feat: descrizione feature"

# I pre-commit hooks verificheranno:
# - Nessun secret committato
# - Linting passato
# - Test passati
```

### Hot Reload

- **Backend**: dotnet watch run (auto-reload su modifiche .cs)
- **Frontend**: pnpm dev (auto-reload su modifiche .tsx/.ts)

### Debug

**Backend (VS Code)**:
1. Apri `apps/api` in VS Code
2. Premi F5 (launch.json preconfigurato)
3. Imposta breakpoint nei file .cs

**Frontend (Browser DevTools)**:
1. Apri http://localhost:3000
2. Apri DevTools (F12)
3. Source maps abilitati di default

### Gestione Migrations

```bash
# Crea nuova migration
cd apps/api/src/Api
dotnet ef migrations add NomeMigrazione

# Applica migration
dotnet ef database update

# Rollback all'ultima migration
dotnet ef database update PreviousMigrationName

# Rimuovi ultima migration (se non ancora applicata)
dotnet ef migrations remove
```

### Pulizia Cache

```bash
# Dalla root
bash tools/cleanup-caches.sh --dry-run  # Preview
bash tools/cleanup-caches.sh            # Esegui

# Pulisce:
# - .serena/
# - codeql-db/
# - .playwright-mcp/
# - Build artifacts
```

---

## 🔧 Troubleshooting

### Problemi Comuni

#### 1. Errore: "Connection refused" a PostgreSQL

**Sintomo**:
```
Npgsql.NpgsqlException: Connection refused at localhost:5432
```

**Soluzione**:
```bash
# Verifica che PostgreSQL sia running
docker compose ps meepleai-postgres

# Se non è running, avvialo
docker compose up -d meepleai-postgres

# Verifica logs
docker compose logs meepleai-postgres

# Assicurati che la porta 5432 non sia occupata
lsof -i :5432  # macOS/Linux
netstat -ano | findstr :5432  # Windows
```

#### 2. Errore: "Qdrant not found"

**Sintomo**:
```
HttpRequestException: No such host is known (meepleai-qdrant:6333)
```

**Soluzione**:
```bash
# Avvia Qdrant
docker compose up -d meepleai-qdrant

# Verifica health
curl http://localhost:6333/healthz

# Se usi Docker, assicurati che API sia nella stessa rete
# Modifica api.env.dev:
QDRANT_URL=http://localhost:6333  # se API gira fuori Docker
QDRANT_URL=http://meepleai-qdrant:6333     # se API gira in Docker
```

#### 3. Frontend non si connette all'API

**Sintomo**:
```
NetworkError: Failed to fetch http://localhost:8080
```

**Soluzione**:
```bash
# Verifica che API sia running
curl http://localhost:8080/health

# Verifica NEXT_PUBLIC_API_BASE in web.env.dev
echo $NEXT_PUBLIC_API_BASE
# Deve essere: http://localhost:8080

# Riavvia frontend
cd apps/web
pnpm dev
```

#### 4. Migration errors

**Sintomo**:
```
The migration '20250101_Migration' has already been applied to the database
```

**Soluzione**:
```bash
# Opzione 1: Rollback e riapplica
dotnet ef database update PreviousMigration
dotnet ef database update

# Opzione 2: Reset database (ATTENZIONE: perdi dati)
docker compose down meepleai-postgres
docker volume rm infra_postgres-data
docker compose up -d meepleai-postgres
dotnet ef database update
```

#### 5. Test falliscono su Testcontainers

**Sintomo**:
```
Docker is not running or not accessible
```

**Soluzione**:
```bash
# Verifica che Docker sia running
docker ps

# Su Linux, assicurati che il tuo utente sia nel gruppo docker
sudo usermod -aG docker $USER
newgrp docker

# Su macOS/Windows, verifica Docker Desktop
```

#### 6. OpenRouter API 401 Unauthorized

**Sintomo**:
```
HttpRequestException: 401 Unauthorized from api.openrouter.ai
```

**Soluzione**:
```bash
# Verifica API key in api.env.dev
grep OPENROUTER_API_KEY infra/env/api.env.dev

# Assicurati che inizi con: sk-or-v1-

# Verifica credito su OpenRouter
# Vai su: https://openrouter.ai/credits

# Riavvia API dopo modifica .env
```

#### 7. npm/pnpm errori di installazione

**Sintomo**:
```
ERR_PNPM_FETCH_404  GET https://registry.npmjs.org/package - Not found
```

**Soluzione**:
```bash
# Pulisci cache pnpm
pnpm store prune

# Rimuovi node_modules e reinstalla
rm -rf node_modules pnpm-lock.yaml
pnpm install

# Se persiste, verifica connessione internet e proxy
```

#### 8. Port già in uso

**Sintomo**:
```
Error: listen EADDRINUSE: address already in use :::3000
```

**Soluzione**:
```bash
# Trova processo che usa la porta
# Linux/macOS
lsof -ti:3000 | xargs kill -9

# Windows
netstat -ano | findstr :3000
taskkill /PID <PID> /F

# Oppure cambia porta
# Frontend: pnpm dev -p 3001
# Backend: dotnet run --urls "http://localhost:8081"
```

#### 9. Ollama modelli mancanti

**Sintomo**:
```
Error: Model 'nomic-embed-text' not found
```

**Soluzione**:
```bash
# Scarica il modello
docker exec -it ollama ollama pull nomic-embed-text

# Verifica
docker exec -it ollama ollama list
```

#### 10. Seq non mostra logs

**Sintomo**:
- Seq UI vuota su http://localhost:8081

**Soluzione**:
```bash
# Verifica che Seq sia running
docker compose ps meepleai-seq

# Verifica SEQ_URL in api.env.dev
grep SEQ_URL infra/env/api.env.dev
# IMPORTANTE: Deve puntare alla porta di INGESTION (5341), NON alla UI (8081)!
# Se API fuori Docker: http://localhost:5341
# Se API in Docker: http://seq:5341

# Riavvia API dopo la modifica
```

### Log Debugging

```bash
# Logs API
cd infra
docker compose logs -f meepleai-api

# Logs specifico servizio
docker compose logs -f meepleai-postgres
docker compose logs -f meepleai-qdrant

# Logs tutte le risorse
docker compose logs --tail=100

# Salva logs in file
docker compose logs > debug.log
```

### Reset Completo (Ultimo Resort)

```bash
# ATTENZIONE: Elimina TUTTI i dati

# 1. Stop tutti i servizi
docker compose down

# 2. Rimuovi volumi (PERDE DATI)
docker volume prune -f

# 3. Rimuovi immagini (opzionale)
docker image prune -a -f

# 4. Ricrea ambiente
cd tools/secrets
./init-secrets.sh

cd ../../infra
docker compose up -d

# 5. Reinstalla dipendenze
cd ../apps/api
dotnet restore
dotnet build

cd ../web
pnpm install

# 6. Riavvia tutto
```

---

## 📚 Risorse Aggiuntive

### Documentazione Interna

| Risorsa | Path | Descrizione |
|---------|------|-------------|
| **Architecture Overview** | `docs/01-architecture/overview/system-architecture.md` | Architettura completa (60+ pagine) |
| **API Specification** | `docs/03-api/board-game-ai-api-specification.md` | Spec REST API completa (40 pagine) |
| **Test Writing Guide** | `docs/02-development/testing/test-writing-guide.md` | Come scrivere test |
| **DDD Quick Reference** | `docs/01-architecture/ddd/quick-reference.md` | Pattern DDD |
| **ADR Hybrid RAG** | `docs/01-architecture/adr/adr-001-hybrid-rag.md` | Decisioni architetturali RAG |
| **ADR PDF Processing** | `docs/01-architecture/adr/adr-003b-unstructured-pdf.md` | Pipeline PDF |
| **Security** | `SECURITY.md` | Best practices sicurezza |
| **Index Completo** | `docs/INDEX.md` | Navigazione documenti (115 docs) |

### ADR (Architecture Decision Records)

1. **ADR-001**: Hybrid RAG (Vector + Keyword)
2. **ADR-002**: Multilingual Embedding
3. **ADR-003**: PDF Processing Strategy
4. **ADR-003b**: Unstructured PDF (3-stage pipeline)
5. **ADR-004**: AI Agents
6. **ADR-004b**: Hybrid LLM (OpenRouter + Ollama)

### Tool Esterni

| Tool | Docs | Uso |
|------|------|-----|
| **Qdrant** | https://qdrant.tech/documentation/ | Vector DB |
| **OpenRouter** | https://openrouter.ai/docs | LLM API |
| **Unstructured** | https://docs.unstructured.io/ | PDF extraction |
| **Ollama** | https://ollama.ai/docs | Local LLM |
| **n8n** | https://docs.n8n.io/ | Workflow automation |
| **Seq** | https://docs.datalust.co/docs | Log management |
| **Jaeger** | https://www.jaegertracing.io/docs/ | Distributed tracing |
| **Grafana** | https://grafana.com/docs/ | Dashboards |

### Community & Support

- **Issues GitHub**: Tag con `documentation`, `bug`, o `question`
- **Discussioni**: GitHub Discussions
- **Email**: engineering@meepleai.dev

### Video Tutorial (Pianificati)

- [ ] Setup locale completo (Q1 2025)
- [ ] Architettura DDD walkthrough (Q1 2025)
- [ ] Debugging pipeline RAG (Q1 2025)
- [ ] Contribuire al progetto (Q1 2025)

---

## ✅ Checklist Setup Completo

**Prerequisiti**:
- [ ] Docker & Docker Compose installati e funzionanti
- [ ] .NET 9 SDK installato
- [ ] Node.js 20+ e pnpm 9 installati
- [ ] Git installato
- [ ] Account OpenRouter con credito
- [ ] 16+ GB RAM disponibile (raccomandato)

**Setup Iniziale**:
- [ ] Repository clonato
- [ ] Docker secrets inizializzati (`tools/secrets/init-secrets.sh`)
- [ ] Dipendenze backend installate (`dotnet restore`)
- [ ] Dipendenze frontend installate (`pnpm install`)

**Configurazione**:
- [ ] `infra/env/api.env.dev` configurato (PostgreSQL, OpenRouter, ecc.)
- [ ] `infra/env/web.env.dev` configurato (API base URL)
- [ ] Secrets Docker creati in `infra/secrets/`

**Servizi Avviati**:
- [ ] PostgreSQL running (porta 5432)
- [ ] Qdrant running (porta 6333)
- [ ] Redis running (porta 6379)
- [ ] API backend running (porta 8080)
- [ ] Frontend web running (porta 3000)
- [ ] Seq running (porta 8081) - opzionale
- [ ] Ollama running (porta 11434) - se usi Ollama

**Verifica**:
- [ ] `/health` endpoint risponde (http://localhost:8080/health)
- [ ] Frontend carica (http://localhost:3000)
- [ ] Login admin funziona
- [ ] Test backend passano (`dotnet test`)
- [ ] Test frontend passano (`pnpm test`)

**Sei pronto a sviluppare! 🚀**

---

## 📝 Note Finali

### Versioning

- **Applicazione**: 1.0-rc (Release Candidate)
- **DDD Migration**: 99% completo
- **Coverage**: 90%+ enforced

### Prossimi Step

1. **Familiarizza con DDD**: Leggi `docs/01-architecture/ddd/quick-reference.md`
2. **Esplora Bounded Contexts**: `apps/api/src/Api/BoundedContexts/`
3. **Studia pattern CQRS**: Esamina handlers in `Application/Handlers/`
4. **Prova RAG**: Carica un PDF e fai domande via chat

### Contribuire

1. Crea un branch: `git checkout -b feature/nome-feature`
2. Sviluppa e testa
3. Commit seguendo conventional commits
4. Push: `git push origin feature/nome-feature`
5. Apri Pull Request su GitHub

### Roadmap

- **Q1 2025**: Final polish (1% DDD), Beta testing
- **Q2 2025**: Produzione, target 10,000 MAU
- **Q3-Q4 2025**: Scale, partnership editori

---

**Documento Creato**: 2025-11-15
**Versione**: 1.0
**Maintainer**: MeepleAI Engineering Team
**Prossimo Review**: 2025-12-15

---

**Buon Sviluppo! 🎲🤖**


