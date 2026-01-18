# MeepleAI - Guida Operativa per Sviluppatori

**Ultima revisione**: 2026-01-18
**Ambiente**: Windows 10/11 + Docker Desktop + PowerShell 7+

Guida completa per deploy, test manuali, debug e analisi dei log nel progetto MeepleAI.

> **💡 Nota**: Tutti i comandi sono ottimizzati per **PowerShell 7+** su **Windows** con **Docker Desktop**.
> Per Linux/macOS, converti path (`\` → `/`) e usa equivalenti bash (`Get-Content` → `cat`, `Select-String` → `grep`).

---

## Indice

1. [Deploy](#deploy)
   - [Deploy Locale (Development)](#deploy-locale-development)
   - [Deploy Staging/Production](#deploy-stagingproduction)
   - [Verifica Deploy](#verifica-deploy)

2. [Test Manuali](#test-manuali)
   - [Test Backend API](#test-backend-api)
   - [Test Frontend](#test-frontend)
   - [Test Integrazione](#test-integrazione)

3. [Debug](#debug)
   - [Debug Backend (.NET)](#debug-backend-net)
   - [Debug Frontend (Next.js)](#debug-frontend-nextjs)
   - [Debug Infrastruttura](#debug-infrastruttura)

4. [Logging & Troubleshooting](#logging--troubleshooting)
   - [Struttura Log](#struttura-log)
   - [Comandi Utili](#comandi-utili)
   - [Pattern Comuni](#pattern-comuni)

---

## Deploy

### Deploy Locale (Development)

#### Prerequisiti
```powershell
# Verifica prerequisiti installati
dotnet --version      # Richiesto: .NET 9.0+
node --version        # Richiesto: Node 20+
pnpm --version        # Richiesto: pnpm 8+
docker --version      # Richiesto: Docker Desktop 24+
docker compose version # Richiesto: Docker Compose 2.0+
```

#### Setup Iniziale Secrets

**IMPORTANTE**: Prima di avviare i servizi, configura i secrets con il sistema consolidato:

```powershell
# 1. Generazione automatica secrets (RACCOMANDATO - 15-30 minuti risparmio)
cd infra\secrets
.\setup-secrets.ps1 -SaveGenerated

# Output atteso:
# ✅ Generati 11 secrets in .secret files
# ✅ Backup salvato in .generated-values-TIMESTAMP.txt
# ⚠️  Configurazione manuale necessaria per: bgg.secret, openrouter.secret (opzionale)

# 2. Verifica secrets CRITICAL (bloccanti se mancanti)
Get-ChildItem *.secret | Select-String -Pattern "admin|database|jwt|qdrant|redis|embedding"
# Deve mostrare 6 files

# 3. Configurazione opzionale BGG (solo se serve scraper)
# Modifica manualmente: infra\secrets\bgg.secret
# BGG_USERNAME=your_username
# BGG_PASSWORD=your_password

# 4. Configurazione opzionale OpenRouter (solo se serve AI)
# Modifica manualmente: infra\secrets\openrouter.secret
# OPENROUTER_API_KEY=sk-or-v1-your-key
# OPENROUTER_DEFAULT_MODEL=anthropic/claude-3.5-sonnet
```

**Dettagli Sistema Secrets**:
- **Documentazione completa**: `docs/04-deployment/secrets-management.md`
- **Audit report**: `docs/claudedocs/secret-audit-2026-01-17.md`
- **Implementazione**: Issue #2570, PR #2572

#### Avvio Servizi

```powershell
# 1. Avvia infrastruttura base
cd infra
docker compose up -d postgres redis qdrant

# 2. Verifica servizi attivi
docker compose ps
# Stato atteso:
# postgres: Up (healthy)
# redis: Up (healthy)
# qdrant: Up

# 3. Avvia backend API (nuovo terminale PowerShell)
cd ..\apps\api\src\Api
dotnet run

# Verifica API attiva:
# - API base: http://localhost:8080
# - Swagger UI: http://localhost:8080/scalar/v1
# - Health check: http://localhost:8080/health

# 4. (Terminale separato) Avvia frontend
cd ..\..\..\..\apps\web
pnpm dev

# Verifica frontend attivo:
# - Web UI: http://localhost:3000
# - Dev tools: Inspect Element → Console (no errors)
```

#### Configurazione Ambiente Frontend

```powershell
# Copia template
cd apps\web
Copy-Item .env.development.example .env.local

# Modifica .env.local se necessario (default funzionanti):
# NEXT_PUBLIC_API_BASE_URL=http://localhost:8080
# NEXT_PUBLIC_WS_URL=ws://localhost:8080
```

#### Verifica Deploy Locale

```powershell
# 1. Health check backend
Invoke-RestMethod http://localhost:8080/health
# Risposta attesa: {"status":"Healthy","components":{...}}

# 2. Health check frontend
Invoke-WebRequest http://localhost:3000
# Risposta attesa: HTML page (200 OK)

# 3. Test database
docker compose exec postgres psql -U meepleai -d meepleai_dev -c "SELECT 1;"
# Risposta attesa: 1 row

# 4. Test Redis
$redisPassword = (Get-Content infra\secrets\redis.secret | Select-String "REDIS_PASSWORD").Line.Split('=')[1]
docker compose exec redis redis-cli -a $redisPassword PING
# Risposta attesa: PONG

# 5. Test Qdrant
Invoke-RestMethod http://localhost:6333/collections
# Risposta attesa: {"result":{"collections":[]}}
```

---

### Deploy Staging/Production

#### Preparazione Ambiente

```powershell
# 1. Crea secrets per production (NON riutilizzare dev!)
cd infra\secrets
.\setup-secrets.ps1 -SaveGenerated -Environment production

# 2. Configura variabili ambiente specifiche
# Modifica docker-compose.prod.yml:
# - ASPNETCORE_ENVIRONMENT=Production
# - ConnectionStrings con host production
# - JWT_ISSUER/AUDIENCE con dominio reale

# 3. Build immagini Docker
docker compose -f docker-compose.prod.yml build
```

#### Deploy con Docker Compose

```powershell
# 1. Avvia stack production
cd infra
docker compose -f docker-compose.prod.yml up -d

# 2. Applica migrations
docker compose -f docker-compose.prod.yml exec api `
  dotnet ef database update --project /app/Api.csproj

# 3. Verifica servizi
docker compose -f docker-compose.prod.yml ps
```

#### Deploy con Traefik (Reverse Proxy)

```powershell
# 1. Configura Traefik (già configurato in infra\traefik\)
# - traefik.yml: configurazione base
# - dynamic.yml: routing rules

# 2. Avvia Traefik
docker compose -f docker-compose.prod.yml up -d traefik

# 3. Verifica dashboard
# https://traefik.yourdomain.com/dashboard/
# (credenziali in infra\secrets\traefik.secret)

# 4. Verifica routing
Invoke-RestMethod https://api.yourdomain.com/health
Invoke-WebRequest https://app.yourdomain.com
```

---

### Verifica Deploy

#### Checklist Pre-Deploy

- [ ] Tutti i secrets configurati (6 CRITICAL + opzionali necessari)
- [ ] Build backend: `dotnet build` (0 warnings, 0 errors)
- [ ] Build frontend: `pnpm build` (0 warnings, TypeScript OK)
- [ ] Test suite: `dotnet test` (100% pass rate)
- [ ] E2E tests: `pnpm test:e2e` (critical flows pass)
- [ ] Database migrations applicate
- [ ] Health checks attivi

#### Checklist Post-Deploy

- [ ] API health check: `curl /health` → 200 OK
- [ ] Frontend load: `curl /` → 200 OK
- [ ] Database connectivity: query test OK
- [ ] Redis connectivity: PING → PONG
- [ ] Qdrant connectivity: collections endpoint OK
- [ ] Logs backend: no CRITICAL/ERROR nei primi 5 minuti
- [ ] Logs frontend: no console errors
- [ ] Monitoring: Grafana dashboard OK (se configurato)

#### Script Automatico Verifica

```powershell
# Esegui script di validazione
cd infra
.\validate-deployment.ps1

# Output atteso:
# ✅ PostgreSQL: Healthy
# ✅ Redis: Healthy
# ✅ Qdrant: Healthy
# ✅ API: Healthy (http://localhost:8080/health)
# ✅ Frontend: Healthy (http://localhost:3000)
# ✅ All checks passed!
```

---

## Test Manuali

### Test Backend API

#### Usando Swagger UI (Scalar)

```powershell
# 1. Apri Swagger UI
# http://localhost:8080/scalar/v1

# 2. Test autenticazione
POST /api/v1/auth/register
{
  "email": "test@example.com",
  "password": "Test123!@#",
  "displayName": "Test User"
}
# Risposta attesa: 200 OK con JWT token

POST /api/v1/auth/login
{
  "email": "test@example.com",
  "password": "Test123!@#"
}
# Risposta attesa: 200 OK con JWT token

# 3. Test endpoint protetto
GET /api/v1/games
Authorization: Bearer <token>
# Risposta attesa: 200 OK con array di games

# 4. Test upload PDF
POST /api/v1/documents/upload
Authorization: Bearer <token>
Content-Type: multipart/form-data
file: [select PDF]
# Risposta attesa: 200 OK con documentId
```

#### Usando cURL

```powershell
# Variabili ambiente
$env:API_URL=http://localhost:8080
$env:TOKEN=""

# 1. Login
$env:TOKEN=$(curl -s -X POST "$env:API_URL/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!@#"}' \
  | ConvertFrom-Json -AsHashtable).token

# 2. Get games
curl -X GET "$env:API_URL/api/v1/games" \
  -H "Authorization: Bearer $env:TOKEN"

# 3. Create game
curl -X POST "$env:API_URL/api/v1/games" \
  -H "Authorization: Bearer $env:TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Game",
    "minPlayers": 2,
    "maxPlayers": 4,
    "playingTime": 60
  }'

# 4. Upload PDF
curl -X POST "$env:API_URL/api/v1/documents/upload" \
  -H "Authorization: Bearer $env:TOKEN" \
  -F "file=@path/to/rulebook.pdf"
```

#### Test Database Diretto

```powershell
# Connetti a PostgreSQL
docker compose exec postgres psql -U meepleai -d meepleai_dev

# Query utili
\dt                     -- Lista tabelle
\d+ games              -- Struttura tabella games
SELECT * FROM games LIMIT 5;
SELECT * FROM users WHERE email = 'test@example.com';

-- Test full-text search (SharedGameCatalog)
SELECT title, similarity_score
FROM shared_games
WHERE search_vector @@ websearch_to_tsquery('english', 'strategy')
ORDER BY similarity_score DESC
LIMIT 10;

-- Exit
\q
```

---

### Test Frontend

#### Test Manuale UI

```powershell
# 1. Apri browser
# http://localhost:3000

# 2. Test login flow
# - Naviga a /login
# - Inserisci credenziali: test@example.com / Test123!@#
# - Verifica redirect a dashboard
# - Verifica presenza token in localStorage (DevTools → Application)

# 3. Test game search
# - Naviga a /games
# - Inserisci termine ricerca: "Catan"
# - Verifica risultati visualizzati
# - Click su game card → verifica dettaglio

# 4. Test upload PDF
# - Naviga a /documents/upload
# - Drag & drop PDF rulebook
# - Verifica progress bar
# - Verifica success message

# 5. Test responsive design
# - Apri DevTools (F12)
# - Toggle device toolbar (Ctrl+Shift+M)
# - Test Mobile (375px), Tablet (768px), Desktop (1920px)
```

#### Test Performance (Lighthouse)

```powershell
# 1. Apri Chrome DevTools
# F12 → Lighthouse tab

# 2. Configura audit
# - Mode: Navigation
# - Device: Desktop/Mobile
# - Categories: Performance, Accessibility, Best Practices, SEO

# 3. Run audit
# - Target score: >90 per tutte le categorie
# - Verifica metriche:
#   - FCP (First Contentful Paint): <1.8s
#   - LCP (Largest Contentful Paint): <2.5s
#   - TBT (Total Blocking Time): <200ms
#   - CLS (Cumulative Layout Shift): <0.1
```

#### Test Accessibilità

```powershell
# 1. Screen reader test (NVDA/JAWS)
# - Installa screen reader
# - Naviga sito solo con tastiera
# - Verifica annunci corretti

# 2. Keyboard navigation
# - Tab: navigazione tra elementi interattivi
# - Enter/Space: attivazione buttons/links
# - Esc: chiusura modals/dropdowns
# - Arrow keys: navigazione liste/menu

# 3. Contrast checker
# - DevTools → Lighthouse → Accessibility
# - Verifica ratio contrasto: >4.5:1 (text), >3:1 (UI components)

# 4. Automated accessibility audit
cd apps\web
pnpm test:a11y   # (se configurato Axe)
```

---

### Test Integrazione

#### Scenario: Upload PDF → RAG → Chat

```powershell
# 1. Upload rulebook PDF
curl -X POST "http://localhost:8080/api/v1/documents/upload" \
  -H "Authorization: Bearer $env:TOKEN" \
  -F "file=@catan_rulebook.pdf"
# Salva documentId dalla risposta

# 2. Verifica embedding generation (attendi 30s)
curl -X GET "http://localhost:8080/api/v1/documents/$DOC_ID/status" \
  -H "Authorization: Bearer $env:TOKEN"
# Stato atteso: "Completed"

# 3. Test RAG search
curl -X POST "http://localhost:8080/api/v1/knowledge/search" \
  -H "Authorization: Bearer $env:TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "How do I win in Catan?",
    "documentId": "'$DOC_ID'",
    "topK": 5
  }'

# 4. Test chat con contesto
curl -X POST "http://localhost:8080/api/v1/chat/send" \
  -H "Authorization: Bearer $env:TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "How do I win in Catan?",
    "documentId": "'$DOC_ID'"
  }'
```

#### Scenario: Shared Game Catalog Search

```powershell
# 1. Search by title (FTS + vector)
curl -X GET "http://localhost:8080/api/v1/shared-games/search?query=strategy&limit=10" \
  -H "Authorization: Bearer $env:TOKEN"

# 2. Filter by complexity
curl -X GET "http://localhost:8080/api/v1/shared-games/search?complexity=Medium&minPlayers=2" \
  -H "Authorization: Bearer $env:TOKEN"

# 3. Verify soft-delete behavior
# Create game
$GAME_ID=$(curl -s -X POST "http://localhost:8080/api/v1/shared-games" \
  -H "Authorization: Bearer $env:TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Game","bggId":123}' \
  | ConvertFrom-Json -AsHashtable).id

# Soft delete
curl -X DELETE "http://localhost:8080/api/v1/shared-games/$GAME_ID" \
  -H "Authorization: Bearer $env:TOKEN"

# Verify not in results
curl -X GET "http://localhost:8080/api/v1/shared-games/search?query=Test Game" \
  -H "Authorization: Bearer $env:TOKEN"
# Risposta attesa: 0 results

# Admin can see deleted
curl -X GET "http://localhost:8080/api/v1/admin/shared-games/all?includeDeleted=true" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

---

## Debug

### Debug Backend (.NET)

#### Configurazione VS Code

```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (API)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-api",
      "program": "${workspaceFolder}/apps/api/src/Api/bin/Debug/net9.0/Api.dll",
      "args": [],
      "cwd": "${workspaceFolder}/apps/api/src/Api",
      "stopAtEntry": false,
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development"
      },
      "console": "integratedTerminal"
    }
  ]
}
```

#### Breakpoint Debugging

```csharp
// 1. Inserisci breakpoint in VS Code/Rider
// Click sul margine sinistro della riga

// 2. Esempio: Debug handler
public class GetGameByIdHandler : IRequestHandler<GetGameByIdQuery, GameDto>
{
    public async Task<GameDto> Handle(GetGameByIdQuery request, ...)
    {
        // ⬅️ Breakpoint qui
        var game = await _db.Games.FindAsync(request.GameId);

        if (game == null)
            throw new NotFoundException($"Game {request.GameId} not found"); // ⬅️ O qui

        return MapToDto(game);
    }
}

// 3. Avvia debug: F5
// 4. Trigger richiesta: curl o Swagger UI
// 5. Execution ferma al breakpoint
// 6. Inspect variables: hover su variabili o Debug console
```

#### Logging Debug

```csharp
// Inietta ILogger nel costruttore
private readonly ILogger<GameService> _logger;

public async Task<GameDto> GetGameAsync(Guid gameId)
{
    _logger.LogDebug("GetGameAsync called with gameId={GameId}", gameId);

    var game = await _db.Games.FindAsync(gameId);

    if (game == null)
    {
        _logger.LogWarning("Game not found: {GameId}", gameId);
        throw new NotFoundException($"Game {gameId} not found");
    }

    _logger.LogInformation("Game found: {GameName} ({GameId})", game.Name, gameId);
    return MapToDto(game);
}
```

#### Common Debug Scenarios

**1. Database Query Inspection**

```csharp
// Enable SQL logging in appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}

// Output nei log:
// Executing DbCommand [...]
// SELECT [g].[Id], [g].[Name] FROM [Games] AS [g] WHERE [g].[Id] = @__gameId_0
```

**2. HTTP Request/Response Logging**

```powershell
# Attiva middleware logging dettagliato
cd apps/api/src/Api
$env:Logging__LogLevel__Microsoft.AspNetCore.HttpLogging="Information"
dotnet run

# Output nei log:
# Request starting HTTP/1.1 GET http://localhost:8080/api/v1/games
# Request finished in 45.67ms with status code 200
```

**3. Exception Breakpoints**

```powershell
# VS Code: Debug sidebar → Breakpoints section
# ✅ Check "All Exceptions"
# Execution ferma a TUTTE le exceptions, caught o uncaught
```

---

### Debug Frontend (Next.js)

#### Configurazione VS Code

```json
// .vscode/launch.json
{
  "configurations": [
    {
      "name": "Next.js: debug client-side",
      "type": "chrome",
      "request": "launch",
      "url": "http://localhost:3000",
      "webRoot": "${workspaceFolder}/apps/web"
    },
    {
      "name": "Next.js: debug server-side",
      "type": "node",
      "request": "launch",
      "runtimeExecutable": "pnpm",
      "runtimeArgs": ["dev"],
      "cwd": "${workspaceFolder}/apps/web",
      "serverReadyAction": {
        "pattern": "started server on .+, url: (https?://.+)",
        "uriFormat": "%s",
        "action": "debugWithChrome"
      }
    }
  ]
}
```

#### Browser DevTools

```powershell
# 1. Apri DevTools: F12 o Ctrl+Shift+I

# 2. Console tab
# - Verifica no errors (rossi)
# - Test comandi: console.log(), debugger statement

# 3. Network tab
# - Verifica API calls: Status 200, response OK
# - Verifica timing: <200ms ideale
# - Filter: XHR per API calls, JS per script load

# 4. Sources tab
# - Breakpoint: Click su numero riga
# - Conditional breakpoint: Right-click → Add conditional breakpoint
# - Logpoint: Console output senza fermare execution

# 5. Application tab
# - LocalStorage: Verifica token JWT
# - Session Storage: Verifica stato temporaneo
# - Cookies: Verifica session cookies
```

#### React DevTools

```powershell
# 1. Installa estensione: React Developer Tools (Chrome/Firefox)

# 2. Components tab
# - Ispeziona component tree
# - Verifica props/state
# - Highlight component su hover

# 3. Profiler tab
# - Record session
# - Interact with app
# - Stop recording
# - Analyze render times per component
```

#### Common Debug Scenarios

**1. API Call Failures**

```typescript
// Aggiungi interceptor logging in lib/api-client.ts
apiClient.interceptors.response.use(
  response => {
    console.log('API Response:', response.config.url, response.status);
    return response;
  },
  error => {
    console.error('API Error:', error.config?.url, error.response?.status);
    console.error('Error details:', error.response?.data);
    return Promise.reject(error);
  }
);
```

**2. State Mutation Issues (Zustand)**

```typescript
// Debug store in Browser Console
window.store = useGameStore.getState();

// Inspect state
console.log(window.store.games);

// Trigger actions manually
window.store.fetchGames();

// Subscribe to changes
const unsubscribe = useGameStore.subscribe(
  state => console.log('State changed:', state)
);
```

**3. Rendering Performance**

```typescript
// Add performance markers
import { Profiler } from 'react';

<Profiler id="GameList" onRender={(id, phase, actualDuration) => {
  console.log(`${id} rendered in ${actualDuration}ms (${phase})`);
}}>
  <GameList games={games} />
</Profiler>

// Check slow renders:
// - actualDuration >16ms: ottimizza (60fps target)
// - Molti renders: verifica memo/useMemo/useCallback
```

---

### Debug Infrastruttura

#### Docker Container Logs

```powershell
# Tutti i container
docker compose logs -f

# Singolo servizio
docker compose logs -f api
docker compose logs -f postgres
docker compose logs -f redis

# Filtra per livello
docker compose logs api | Select-String -Pattern "ERROR"
docker compose logs api | Select-String -Pattern "WARN"

# Ultime N righe
docker compose logs --tail=100 api

# Follow con timestamp
docker compose logs -f --timestamps api
```

#### Container Shell Access

```powershell
# PostgreSQL
docker compose exec postgres bash
psql -U meepleai -d meepleai_dev

# Redis
docker compose exec redis sh
redis-cli -a <password>
KEYS *
GET some:key

# Qdrant
docker compose exec qdrant sh
curl http://localhost:6333/collections

# API (debugging)
docker compose exec api bash
dotnet --info
ls /app
cat /app/appsettings.json
```

#### Health Check Troubleshooting

```powershell
# 1. Verifica health endpoint API
curl -v http://localhost:8080/health

# 2. Se fallisce, check logs
docker compose logs --tail=50 api | Select-String -Pattern "health\|error"

# 3. Verifica dipendenze
# PostgreSQL
docker compose exec postgres pg_isready -U meepleai

# Redis
docker compose exec redis redis-cli -a <password> PING

# Qdrant
curl http://localhost:6333/healthz

# 4. Verifica connessioni da API container
docker compose exec api bash
apt-get update && apt-get install -y postgresql-client
psql -h postgres -U meepleai -d meepleai_dev -c "SELECT 1;"
```

#### Performance Monitoring

```powershell
# Docker stats
docker stats --no-stream

# Output:
# CONTAINER    CPU %    MEM USAGE / LIMIT     MEM %    NET I/O
# api          15.2%    256MB / 2GB           12.8%    1.2MB / 856KB
# postgres     5.1%     512MB / 4GB           12.8%    450KB / 320KB

# Verifica resource limits
docker inspect api | ConvertFrom-Json | Select-Object -ExpandProperty HostConfig | Select-Object -ExpandProperty Memory
docker inspect postgres | ConvertFrom-Json | Select-Object -ExpandProperty HostConfig | Select-Object -ExpandProperty Memory

# Grafana monitoring (se configurato)
# http://localhost:3001
# Dashboard: MeepleAI System Overview
```

---

## Logging & Troubleshooting

### Struttura Log

#### Backend (.NET) - Serilog

```json
// Formato log entry
{
  "@t": "2026-01-18T10:30:45.123Z",        // Timestamp (UTC)
  "@l": "Information",                      // Level: Trace, Debug, Information, Warning, Error, Fatal
  "@mt": "Game created: {GameName}",       // Message template
  "GameName": "Catan",                      // Structured properties
  "GameId": "a1b2c3d4...",
  "UserId": "user123",
  "SourceContext": "MeepleAI.GameManagement.Application.Handlers.CreateGameHandler",
  "RequestId": "0HM7...ABC",                // Trace ID per correlazione
  "MachineName": "api-container"
}
```

#### Log Levels Usage

```csharp
// Trace: Informazioni dettagliatissime (solo debug profondo)
_logger.LogTrace("Entering method with parameters: {Param1}, {Param2}", p1, p2);

// Debug: Informazioni di debug (development only)
_logger.LogDebug("Cache hit for key {CacheKey}", key);

// Information: Eventi applicativi normali
_logger.LogInformation("User {UserId} created game {GameName}", userId, gameName);

// Warning: Situazioni anomale ma gestibili
_logger.LogWarning("Rate limit approached for user {UserId}: {RequestCount}/100", userId, count);

// Error: Errori gestiti dall'applicazione
_logger.LogError(ex, "Failed to process PDF {DocumentId}", documentId);

// Critical/Fatal: Errori che richiedono intervento immediato
_logger.LogCritical("Database connection pool exhausted");
```

#### Frontend (Next.js) - Console

```typescript
// Development logging
if (process.env.NODE_ENV === 'development') {
  console.log('[GameList] Rendering with', games.length, 'games');
  console.debug('[API] Request:', { url, method, payload });
}

// Production errors (sempre attivi)
console.error('[API] Request failed:', error.message, { context });

// Warning per situazioni anomale
console.warn('[Auth] Token expires in', expiresIn, 'seconds');

// Structured logging con context
const logContext = { userId, action: 'game_create', timestamp: Date.now() };
console.info('[Audit]', logContext);
```

---

### Comandi Utili

#### Ricerca nei Log

```powershell
# Backend: cerca errori ultimi 100 log entries
docker compose logs --tail=100 api | Select-String -Pattern "ERROR"

# Backend: cerca per RequestId (trace ID)
docker compose logs api | Select-String -Pattern "0HM7ABC"

# Backend: cerca per UserId
docker compose logs api | Select-String -Pattern "user123"

# Backend: cerca exception stack traces
docker compose logs api | Select-String -Pattern "Exception:" -Context 20

# Frontend: verifica errori browser
# DevTools → Console → Filter: Errors

# Database: query slow logs (>1s)
docker compose exec postgres psql -U meepleai -d meepleai_dev -c \
  "SELECT query, total_time FROM pg_stat_statements WHERE total_time > 1000 ORDER BY total_time DESC LIMIT 10;"
```

#### Export Log per Analisi

```powershell
# Export ultimi 1000 log entries
docker compose logs --tail=1000 api > api_logs_$(Get-Date -Format "yyyyMMdd_HHmmss").log

# Export con timestamp
docker compose logs --timestamps --tail=500 api > api_logs_timestamped.log

# Export JSON format (se Serilog configurato per JSON)
docker compose logs api | ConvertFrom-Json > api_logs_structured.json
```

---

### Pattern Comuni

#### Pattern 1: API 500 Internal Server Error

**Sintomo**:
```powershell
curl http://localhost:8080/api/v1/games/123
# 500 Internal Server Error
```

**Debug**:
```powershell
# 1. Check logs API per exception
docker compose logs --tail=50 api | Select-String -Pattern "exception\|error"

# Output possibile:
# System.InvalidOperationException: Sequence contains no elements
#   at GameHandler.Handle(GetGameQuery request)

# 2. Identifica causa
# - NotFoundException non gestita → verifica middleware error handling
# - Database connection failed → verifica connessione DB
# - Null reference → verifica validazione input

# 3. Fix comune: Aggiungi try-catch in handler
try {
    var game = await _db.Games.FirstAsync(g => g.Id == request.GameId);
    return MapToDto(game);
}
catch (InvalidOperationException) {
    throw new NotFoundException($"Game {request.GameId} not found");
}
```

#### Pattern 2: Frontend API Call Timeout

**Sintomo**:
```typescript
// Browser Console:
// Error: Network request failed (timeout)
```

**Debug**:
```powershell
# 1. Verifica backend raggiungibile
curl -v http://localhost:8080/health
# Se fallisce: backend non in ascolto

# 2. Check CORS headers
curl -v -X OPTIONS http://localhost:8080/api/v1/games \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET"
# Verifica presenza header: Access-Control-Allow-Origin

# 3. Verifica timeout configurato
# apps/web/lib/api-client.ts
apiClient.defaults.timeout = 30000; // 30s (aumenta se necessario)

# 4. Check slow queries backend
docker compose logs api | Select-String -Pattern "slow\|timeout"
```

#### Pattern 3: Database Migration Failed

**Sintomo**:
```powershell
dotnet ef database update
# Failed executing DbCommand (...)
# Violation of PRIMARY KEY constraint
```

**Debug**:
```powershell
# 1. Check stato migrations
dotnet ef migrations list

# Output:
# 20260101000000_Initial (Applied)
# 20260110000000_AddGames (Pending)
# 20260115000000_AddSharedCatalog (Pending) ⬅️ Questa fallisce

# 2. Rollback a migration precedente
dotnet ef database update 20260110000000_AddGames

# 3. Drop problematic migration
dotnet ef migrations remove

# 4. Ricrea migration (dopo fix)
dotnet ef migrations add AddSharedCatalog

# 5. Applica nuovamente
dotnet ef database update
```

#### Pattern 4: Container Crash Loop

**Sintomo**:
```powershell
docker compose ps
# api: Restarting (1) 3 seconds ago

docker compose logs api
# Unhandled exception. System.ArgumentNullException: Value cannot be null
# Container exits with code 134
```

**Debug**:
```powershell
# 1. Identifica causa crash
docker compose logs --tail=100 api

# 2. Verifica dipendenze (spesso causa: DB non pronto)
docker compose exec postgres pg_isready -U meepleai
# Output: postgres:5432 - accepting connections

# 3. Fix: aggiungi health check e depends_on
# docker-compose.yml
services:
  api:
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy

# 4. Restart con clean slate
docker compose down -v  # ⚠️ Rimuove dati!
docker compose up -d
```

#### Pattern 5: JWT Token Invalid/Expired

**Sintomo**:
```powershell
curl -H "Authorization: Bearer <token>" http://localhost:8080/api/v1/games
# 401 Unauthorized
```

**Debug**:
```powershell
# 1. Verifica token nel browser
# DevTools → Application → Local Storage → http://localhost:3000
# Cerca chiave: auth_token

# 2. Decode token manualmente
# https://jwt.io
# Paste token → verifica exp (expiration) timestamp

# 3. Verifica configurazione JWT backend
# appsettings.Development.json
{
  "Jwt": {
    "SecretKey": "<secret>",     # Match con infra/secrets/jwt.secret
    "Issuer": "MeepleAI",
    "Audience": "MeepleAI"
  }
}

# 4. Test login → ottieni nuovo token
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!@#"}'
```

---

## Comandi PowerShell Nativi Avanzati

### Alternative Native a cURL

```powershell
# 🎯 RACCOMANDATO: Invoke-RestMethod per API JSON
$response = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/games" `
  -Method Get `
  -Headers @{ Authorization = "Bearer $env:TOKEN" }
$response | ConvertTo-Json -Depth 5

# POST con body JSON
$body = @{
  email = "test@example.com"
  password = "Test123!@#"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/auth/login" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body

$env:TOKEN = $response.token

# Upload file multipart/form-data
$form = @{
  file = Get-Item -Path "C:\path\to\rulebook.pdf"
}

Invoke-RestMethod -Uri "http://localhost:8080/api/v1/documents/upload" `
  -Method Post `
  -Headers @{ Authorization = "Bearer $env:TOKEN" } `
  -Form $form
```

### Gestione Processi e Servizi

```powershell
# Trova processi che bloccano porte
Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue |
  Select-Object OwningProcess, State |
  ForEach-Object { Get-Process -Id $_.OwningProcess }

# Kill processo per porta
$processId = (Get-NetTCPConnection -LocalPort 8080).OwningProcess
Stop-Process -Id $processId -Force

# Cleanup testhost processes (Issue #2593)
Get-Process -Name testhost -ErrorAction SilentlyContinue | Stop-Process -Force

# Verifica no processi testhost
if (Get-Process testhost -ErrorAction SilentlyContinue) {
  Write-Warning "⚠️  testhost processes still running!"
} else {
  Write-Host "✅ Clean - no testhost processes" -ForegroundColor Green
}
```

### Monitoring e Health Checks

```powershell
# Health check con retry e timeout
function Test-ServiceHealth {
  param([string]$Url, [int]$MaxRetries = 3)

  for ($i = 0; $i -lt $MaxRetries; $i++) {
    try {
      $response = Invoke-RestMethod -Uri $Url -TimeoutSec 5
      Write-Host "✅ $Url is healthy" -ForegroundColor Green
      return $true
    }
    catch {
      Write-Warning "Attempt $($i+1)/$MaxRetries failed"
      Start-Sleep -Seconds 2
    }
  }
  Write-Error "❌ $Url is unhealthy"
  return $false
}

# Uso
Test-ServiceHealth "http://localhost:8080/health"
Test-ServiceHealth "http://localhost:3000"

# Verifica tutti i servizi
$services = @{
  "API" = "http://localhost:8080/health"
  "Frontend" = "http://localhost:3000"
  "Qdrant" = "http://localhost:6333/collections"
}

$services.GetEnumerator() | ForEach-Object {
  Write-Host "`nTesting $($_.Key)..." -ForegroundColor Cyan
  Test-ServiceHealth $_.Value
}
```

### Analisi Log Avanzata

```powershell
# Estrai log con parsing JSON strutturato
docker compose logs --tail=500 api |
  Where-Object { $_ -match '^\{' } |
  ForEach-Object { $_ | ConvertFrom-Json } |
  Where-Object { $_.'@l' -eq 'Error' } |
  Select-Object '@t', '@l', '@mt', SourceContext |
  Format-Table -AutoSize

# Raggruppa errori per tipo
docker compose logs --tail=1000 api |
  Select-String -Pattern "Exception:" |
  Group-Object { ($_ -split ':')[0].Trim() } |
  Sort-Object Count -Descending |
  Select-Object Count, Name

# Export log filtrati per data/ora
$startTime = Get-Date "2026-01-18 10:00:00"
docker compose logs --timestamps api |
  Where-Object {
    $timestamp = [DateTime]::Parse(($_ -split ' ')[0])
    $timestamp -gt $startTime
  } > filtered_logs.txt

# Statistiche log per livello
docker compose logs api |
  Select-String -Pattern '\[(DBG|INF|WRN|ERR|FTL)\]' -AllMatches |
  ForEach-Object { $_.Matches.Value } |
  Group-Object |
  Sort-Object Count -Descending |
  Format-Table Name, Count
```

### Docker Compose Operations

```powershell
# Restart singolo servizio (reload env vars)
docker compose restart api

# Force recreate con nuove configurazioni
docker compose up -d --force-recreate api

# Scale servizi (se supportato)
docker compose up -d --scale worker=3

# Verifica health status con loop
while ($true) {
  $status = docker compose ps --format json | ConvertFrom-Json
  $unhealthy = $status | Where-Object { $_.Health -ne "healthy" }

  if ($unhealthy) {
    Write-Warning "Unhealthy: $($unhealthy.Service -join ', ')"
  } else {
    Write-Host "✅ All services healthy" -ForegroundColor Green
    break
  }
  Start-Sleep -Seconds 5
}

# Cleanup completo (⚠️ DISTRUTTIVO)
docker compose down -v --remove-orphans
docker volume prune -f
docker network prune -f
```

### Database Operations

```powershell
# Backup database
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
docker compose exec -T postgres pg_dump -U meepleai meepleai_dev > "backup_$timestamp.sql"

# Restore database
Get-Content backup_20260118_103045.sql | docker compose exec -T postgres psql -U meepleai -d meepleai_dev

# Reset database completo
docker compose down postgres
docker volume rm infra_pgdata
docker compose up -d postgres
Start-Sleep -Seconds 10  # Attendi avvio
cd apps\api\src\Api
dotnet ef database update

# Query performance analysis
docker compose exec postgres psql -U meepleai -d meepleai_dev -c "
  SELECT
    calls,
    total_time,
    mean_time,
    query
  FROM pg_stat_statements
  WHERE mean_time > 100
  ORDER BY mean_time DESC
  LIMIT 10;
"
```

### Automation Helpers

```powershell
# Script completo: start development environment
function Start-MeepleAIDev {
  Write-Host "🚀 Starting MeepleAI Development Environment..." -ForegroundColor Cyan

  # 1. Start infrastructure
  Set-Location infra
  docker compose up -d postgres redis qdrant
  Write-Host "✅ Infrastructure started" -ForegroundColor Green

  # 2. Wait for health
  Start-Sleep -Seconds 10

  # 3. Start API (background job)
  Set-Location ..\apps\api\src\Api
  $apiJob = Start-Job -ScriptBlock { dotnet run }
  Write-Host "✅ API starting (Job ID: $($apiJob.Id))" -ForegroundColor Green

  # 4. Start Frontend (background job)
  Set-Location ..\..\..\..\apps\web
  $webJob = Start-Job -ScriptBlock { pnpm dev }
  Write-Host "✅ Frontend starting (Job ID: $($webJob.Id))" -ForegroundColor Green

  # 5. Wait and verify
  Start-Sleep -Seconds 15
  Test-ServiceHealth "http://localhost:8080/health"
  Test-ServiceHealth "http://localhost:3000"

  Write-Host "`n🎉 MeepleAI Dev Environment Ready!" -ForegroundColor Green
  Write-Host "API: http://localhost:8080/scalar/v1" -ForegroundColor Yellow
  Write-Host "Web: http://localhost:3000" -ForegroundColor Yellow
}

# Script completo: stop development environment
function Stop-MeepleAIDev {
  Write-Host "🛑 Stopping MeepleAI Development Environment..." -ForegroundColor Cyan

  # Stop jobs
  Get-Job | Stop-Job
  Get-Job | Remove-Job

  # Stop Docker
  Set-Location infra
  docker compose down

  Write-Host "✅ Environment stopped" -ForegroundColor Green
}

# Uso:
# Start-MeepleAIDev
# Stop-MeepleAIDev
```

---

## Troubleshooting Specifico Windows

### Docker Desktop Issues

#### Volume Mount Problems

```powershell
# Sintomo: Files non sincronizzati tra host e container
# Causa: WSL2 filesystem permissions

# Fix: Verifica WSL2 integration
docker info | Select-String -Pattern "WSL"

# Reset Docker Desktop data (⚠️ DISTRUTTIVO)
# Docker Desktop → Settings → Troubleshoot → Clean / Purge data

# Ri-share drive
# Docker Desktop → Settings → Resources → File sharing
# Aggiungi D:\ se necessario
```

#### Port Binding Conflicts

```powershell
# Sintomo: Error binding to port 8080
# Causa: Processo Windows blocca porta

# Trova processo
Get-NetTCPConnection -LocalPort 8080 | ForEach-Object {
  $proc = Get-Process -Id $_.OwningProcess
  Write-Host "Port 8080 used by: $($proc.Name) (PID: $($proc.Id))"
}

# Kill processo
$pid = (Get-NetTCPConnection -LocalPort 8080).OwningProcess
Stop-Process -Id $pid -Force

# Verifica porta libera
Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue
# Output vuoto = porta libera
```

#### WSL2 Performance Issues

```powershell
# Sintomo: Docker containers lenti
# Causa: I/O overhead WSL2

# Ottimizza .wslconfig
$wslConfig = @"
[wsl2]
memory=8GB
processors=4
swap=4GB
localhostForwarding=true
"@

Set-Content -Path "$env:USERPROFILE\.wslconfig" -Value $wslConfig

# Restart WSL2
wsl --shutdown
# Restart Docker Desktop
```

### .NET SDK Issues

#### File Lock da testhost.exe

```powershell
# Sintomo: "The process cannot access the file 'Api.dll'"
# Causa: testhost.exe processi zombie (Issue #2593)

# Soluzione completa
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -Recurse -Force apps\api\src\Api\bin, apps\api\src\Api\obj
dotnet clean apps\api\MeepleAI.sln
dotnet build apps\api\MeepleAI.sln

# Verifica build pulita
if ($LASTEXITCODE -eq 0) {
  Write-Host "✅ Build successful" -ForegroundColor Green
  dotnet test apps\api\MeepleAI.sln
} else {
  Write-Error "❌ Build failed"
}
```

#### Certificate Trust Issues

```powershell
# Sintomo: HTTPS development certificate not trusted

# Fix: Re-trust dev certificate
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# Verifica
dotnet dev-certs https --check --trust
# Output: A valid HTTPS certificate is already present.
```

### Node.js / pnpm Issues

#### pnpm Store Corruption

```powershell
# Sintomo: pnpm install fails con checksum errors

# Fix: Clear pnpm store
pnpm store prune
pnpm install --force

# Verifica store
pnpm store status
```

#### Node Process Lock

```powershell
# Sintomo: Port 3000 already in use

# Kill Node processes
Get-Process node -ErrorAction SilentlyContinue | Stop-Process -Force

# Verifica
Get-Process node -ErrorAction SilentlyContinue
# Output vuoto = no node processes
```

### Windows-Specific Path Issues

#### Long Path Support

```powershell
# Sintomo: "The specified path, file name, or both are too long"

# Fix: Abilita long paths (richiede admin)
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" `
  -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force

# Verifica (riavvio potrebbe essere necessario)
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" `
  -Name "LongPathsEnabled"
```

#### Path Separator Issues

```powershell
# ❌ SBAGLIATO - Path Unix in PowerShell
$path = "infra/secrets/database.secret"

# ✅ CORRETTO - Path Windows
$path = "infra\secrets\database.secret"

# 🎯 BEST PRACTICE - Cross-platform
$path = Join-Path "infra" "secrets" "database.secret"
# Output: infra\secrets\database.secret (Windows)
#         infra/secrets/database.secret (Linux/macOS)
```

### Environment Variables

#### PowerShell vs CMD

```powershell
# PowerShell (RACCOMANDATO)
$env:API_URL = "http://localhost:8080"
$env:TOKEN = "abc123"

# Verifica
Get-ChildItem Env: | Where-Object Name -like "*API*"

# Rimuovi variabile
Remove-Item Env:API_URL

# CMD (legacy - non raccomandato)
# set API_URL=http://localhost:8080
# echo %API_URL%
```

#### Persistenza Variabili Ambiente

```powershell
# Session-only (default)
$env:TOKEN = "abc123"

# User-level persistente
[System.Environment]::SetEnvironmentVariable('API_URL', 'http://localhost:8080', 'User')

# Machine-level (richiede admin)
[System.Environment]::SetEnvironmentVariable('API_URL', 'http://localhost:8080', 'Machine')

# Verifica variabili persistenti
[System.Environment]::GetEnvironmentVariable('API_URL', 'User')
```

---

## Quick Reference

### Comandi Essenziali (Copy-Paste Ready)

```powershell
# 🚀 Start completo ambiente dev
cd D:\Repositories\meepleai-monorepo-frontend
docker compose -f infra\docker-compose.yml up -d postgres redis qdrant
Start-Sleep 10
cd apps\api\src\Api; dotnet run  # Terminal 1
cd apps\web; pnpm dev             # Terminal 2 (separato)

# 🛑 Stop completo
docker compose -f infra\docker-compose.yml down
Get-Job | Stop-Job; Get-Job | Remove-Job

# 🔍 Verifica stato servizi
docker compose ps
Invoke-RestMethod http://localhost:8080/health
Invoke-WebRequest http://localhost:3000

# 🧹 Cleanup testhost (prima di test)
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force

# 📋 Test completo
dotnet test apps\api\MeepleAI.sln
cd apps\web; pnpm test; pnpm test:e2e

# 📊 Log analysis rapida
docker compose logs --tail=100 api | Select-String "ERROR|WARN"

# 🔐 Get JWT token per test
$body = @{ email = "test@example.com"; password = "Test123!@#" } | ConvertTo-Json
$response = Invoke-RestMethod "http://localhost:8080/api/v1/auth/login" `
  -Method Post -ContentType "application/json" -Body $body
$env:TOKEN = $response.token
Write-Host "Token: $env:TOKEN"

# 🗂️ Database quick check
docker compose exec postgres psql -U meepleai -d meepleai_dev -c "SELECT COUNT(*) FROM games;"
```

### Troubleshooting One-Liners

```powershell
# Port 8080 bloccata? Kill processo
(Get-NetTCPConnection -LocalPort 8080).OwningProcess | ForEach-Object { Stop-Process -Id $_ -Force }

# testhost zombie? Kill tutti
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force

# Node processes bloccanti? Kill tutti
Get-Process node -ErrorAction SilentlyContinue | Stop-Process -Force

# Docker containers non partono? Reset completo
docker compose down -v; docker system prune -af; docker compose up -d

# Build .NET fallisce? Clean completo
Remove-Item -Recurse -Force bin, obj; dotnet clean; dotnet build

# pnpm problemi? Reset store
pnpm store prune; Remove-Item -Recurse -Force node_modules, .next; pnpm install

# Secrets mancanti? Auto-genera
cd infra\secrets; .\setup-secrets.ps1 -SaveGenerated

# Database corrotto? Reset completo (⚠️ DATI PERSI)
docker compose down postgres; docker volume rm infra_pgdata; docker compose up -d postgres
```

### Keyboard Shortcuts

| Azione | Shortcut | Contesto |
|--------|----------|----------|
| **Apri DevTools** | `F12` o `Ctrl+Shift+I` | Browser |
| **Device toolbar** | `Ctrl+Shift+M` | Chrome DevTools |
| **Console** | `Ctrl+Shift+J` | Chrome |
| **Network tab** | `Ctrl+Shift+E` | Chrome DevTools |
| **Start Debug** | `F5` | VS Code |
| **Stop Debug** | `Shift+F5` | VS Code |
| **Breakpoint** | `F9` | VS Code |
| **Step Over** | `F10` | VS Code Debug |
| **Step Into** | `F11` | VS Code Debug |
| **Terminal** | ``Ctrl+` `` | VS Code |
| **Command Palette** | `Ctrl+Shift+P` | VS Code |

---

## Risorse Aggiuntive

### Documentazione Correlata

- **Secret Management**: `docs/04-deployment/secrets-management.md`
- **Health Checks**: `docs/04-deployment/health-checks.md`
- **Monitoring Setup**: `docs/04-deployment/monitoring/observability-validation-report.md`
- **Testing Guide**: `docs/05-testing/README.md`
- **E2E Test Guide**: `docs/05-testing/E2E_TEST_GUIDE.md`
- **Troubleshooting**: `docs/02-development/troubleshooting/`

### Script Utili

```powershell
# Validazione deployment
.\infra\validate-deployment.ps1

# Cleanup test processes (Windows)
.\tools\cleanup\cleanup-testhost.ps1

# Verifica processi testhost bloccanti (Issue #2593)
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force

# Database backup
.\scripts\db\backup-database.ps1  # PowerShell version

# Log rotation
.\scripts\tools\rotate-logs.ps1   # PowerShell version
```

### Contatti e Supporto

- **Issues GitHub**: https://github.com/your-org/meepleai/issues
- **Documentazione Live**: `docs/living-documentation.md`
- **ADRs (Architecture Decision Records)**: `docs/01-architecture/adr/`

---

**Ultima revisione**: 2026-01-18
**Maintainer**: MeepleAI Development Team
**License**: Proprietary