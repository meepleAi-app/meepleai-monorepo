---
description: Guida per avviare tutti i servizi MeepleAI in locale con PowerShell
---

# Guida Completa: Avvio Locale Servizi MeepleAI (PowerShell)

Questa guida mostra come avviare tutti i servizi del monorepo MeepleAI in locale su Windows utilizzando PowerShell.

## Prerequisiti

Prima di iniziare, verifica di avere installato:

1. **Docker Desktop** - Per eseguire i container dei servizi
2. **.NET 9 SDK** - Per il backend API
3. **Node.js 20+** - Per il frontend web
4. **pnpm 9** - Package manager per il frontend
5. **Git** - Version control

Verifica le installazioni:

```powershell
docker --version
dotnet --version
node --version
pnpm --version
git --version
```

## Passaggio 1: Configurazione Ambiente

### 1.1 Crea il file di configurazione locale

```powershell
# Dalla root del progetto
cd d:\Repositories\meepleai-monorepo-frontend\meepleai-monorepo

# Copia il file di esempio
Copy-Item -Path .env.development.example -Destination .env.development
```

### 1.2 Inizializza i Docker Secrets

```powershell
# Vai alla directory dei secrets
cd tools\secrets

# Esegui lo script di inizializzazione (se disponibile)
# Altrimenti crea manualmente i file nella directory infra/secrets/

# Torna alla root
cd ..\..
```

### 1.3 Crea i file segreti manualmente (se necessario)

```powershell
# Vai alla directory secrets
cd infra\secrets

# Crea i file segreti richiesti con valori di default per lo sviluppo
"meeplepass" | Out-File -FilePath postgres-password.txt -NoNewline -Encoding ASCII
"changeme-redis-dev-please-set" | Out-File -FilePath redis-password.txt -NoNewline -Encoding ASCII
"" | Out-File -FilePath openrouter-api-key.txt -NoNewline -Encoding ASCII
"dev1234567890abcdef1234567890abcdef1234567890abcdef1234567890ab" | Out-File -FilePath n8n-encryption-key.txt -NoNewline -Encoding ASCII
"admin" | Out-File -FilePath n8n-basic-auth-password.txt -NoNewline -Encoding ASCII
"" | Out-File -FilePath gmail-app-password.txt -NoNewline -Encoding ASCII
"admin" | Out-File -FilePath grafana-admin-password.txt -NoNewline -Encoding ASCII
"Demo123!" | Out-File -FilePath initial-admin-password.txt -NoNewline -Encoding ASCII

# Torna alla root
cd ..\..
```

## Passaggio 2: Avvio Servizi Docker

### 2.1 Opzione A - Profilo Minimal (Più veloce, solo servizi core)

```powershell
cd infra

# Avvia solo i servizi essenziali: postgres, redis, qdrant, api, web
docker compose --profile minimal up -d

# Verifica lo stato
docker compose ps
```

**Servizi inclusi:**

- PostgreSQL (database)
- Redis (cache)
- Qdrant (vector database)
- API (backend)
- Web (frontend)

### 2.2 Opzione B - Profilo Dev (Raccomandato per sviluppo)

```powershell
cd infra

# Avvia servizi core + monitoring base
docker compose --profile dev up -d

# Verifica lo stato
docker compose ps
```

**Servizi inclusi:**

- Tutti i servizi minimal +
- Prometheus (metriche)
- Grafana (dashboard)
- Mailpit (test email)

### 2.3 Opzione C - Profilo AI (Include servizi AI/ML)

```powershell
cd infra

# Avvia servizi AI/ML
docker compose --profile minimal --profile ai up -d

# Verifica lo stato
docker compose ps
```

**Servizi AI inclusi:**

- Ollama (LLM locale)
- Embedding Service (embedding multilingue)
- Unstructured Service (estrazione PDF - Stage 1)
- SmolDocling Service (estrazione PDF - Stage 2)
- Reranker Service (reranking per RAG)

### 2.4 Opzione D - Stack Complete (Tutti i servizi)

```powershell
cd infra

# Avvia tutti i servizi
docker compose --profile full up -d

# Oppure usa lo script helper
.\start-full.sh  # Richiede Git Bash o WSL

# Verifica lo stato
docker compose ps
```

## Passaggio 3: Avvio API Backend (Sviluppo locale)

Se vuoi eseguire l'API in locale invece che in Docker:

```powershell
# Ferma il container API se è in esecuzione
cd infra
docker compose stop api

# Vai alla directory API
cd ..\apps\api\src\Api

# Configura le variabili d'ambiente (crea appsettings.Development.json se necessario)
# Oppure usa variabili d'ambiente:
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai;Username=meeple;Password=meeplepass"
$env:QDRANT_URL="http://localhost:6333"
$env:REDIS_URL="localhost:6379,password=changeme-redis-dev-please-set,abortConnect=false"

# Avvia l'API
dotnet run

# L'API sarà disponibile su http://localhost:8080
```

## Passaggio 4: Avvio Frontend Web (Sviluppo locale)

Se vuoi eseguire il frontend in locale invece che in Docker:

```powershell
# Ferma il container web se è in esecuzione
cd infra
docker compose stop web

# Vai alla directory web
cd ..\apps\web

# Installa le dipendenze (solo la prima volta)
pnpm install

# Configura le variabili d'ambiente
$env:NODE_ENV="development"
$env:NEXT_PUBLIC_API_BASE="http://localhost:8080"

# Avvia il server di sviluppo
pnpm dev

# Il frontend sarà disponibile su http://localhost:3000
```

## Passaggio 5: Verifica Stato Servizi

### 5.1 Verifica Container Docker

```powershell
cd infra

# Visualizza tutti i container in esecuzione
docker compose ps

# Visualizza i log di tutti i servizi
docker compose logs -f

# Visualizza i log di un servizio specifico
docker compose logs -f api
docker compose logs -f postgres
docker compose logs -f web
```

### 5.2 Verifica Health Endpoints

```powershell
# API Health Check
Invoke-WebRequest -Uri "http://localhost:8080/health" -UseBasicParsing | Select-Object StatusCode, Content

# Web Frontend
Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing | Select-Object StatusCode

# Prometheus
Invoke-WebRequest -Uri "http://localhost:9090/-/healthy" -UseBasicParsing | Select-Object StatusCode

# Qdrant
Invoke-WebRequest -Uri "http://localhost:6333/healthz" -UseBasicParsing | Select-Object StatusCode
```

## Passaggio 6: Accesso alle Interfacce Web

Dopo l'avvio, accedi alle seguenti interfacce:

| Servizio         | URL                             | Credenziali (dev)             |
| ---------------- | ------------------------------- | ----------------------------- |
| **Frontend Web** | http://localhost:3000           | admin@meepleai.dev / Demo123! |
| **API Swagger**  | http://localhost:8080/swagger   | N/A                           |
| **Grafana**      | http://localhost:3001           | admin / admin                 |
| **Prometheus**   | http://localhost:9090           | N/A                           |
| **n8n**          | http://localhost:5678           | admin / admin                 |
| **Mailpit**      | http://localhost:8025           | N/A                           |
| **Qdrant**       | http://localhost:6333/dashboard | N/A                           |

## Passaggio 7: Database Setup

### 7.1 Applica le Migrazioni (se necessario)

```powershell
cd apps\api\src\Api

# Applica le migrazioni al database
dotnet ef database update

# Verifica lo stato delle migrazioni
dotnet ef migrations list
```

### 7.2 Verifica Connessione Database

```powershell
# Connettiti al database PostgreSQL
docker exec -it meepleai-postgres psql -U meeple -d meepleai

# Dentro psql:
# \dt - lista le tabelle
# \q - esci
```

## Passaggio 8: Avvio Servizi Opzionali

### 8.1 Ollama (LLM Locale)

```powershell
cd infra

# Avvia Ollama
docker compose --profile ai up -d ollama

# Scarica un modello (dopo che Ollama è pronto)
docker exec -it meepleai-ollama ollama pull nomic-embed-text

# Verifica i modelli installati
docker exec -it meepleai-ollama ollama list
```

## Passaggio 9: Rebuild dopo Modifiche al Codice

Quando modifichi il codice, devi ribuilddare i container Docker o riavviare i servizi locali.

### 9.1 Rebuild Container Docker

#### Rebuild di un Singolo Servizio

```powershell
cd infra

# Rebuild e riavvio API
docker compose up -d --build api

# Rebuild e riavvio Web
docker compose up -d --build web

# Rebuild servizi AI/ML
docker compose up -d --build unstructured-service
docker compose up -d --build smoldocling-service
docker compose up -d --build embedding-service
```

#### Rebuild di Tutti i Servizi

```powershell
cd infra

# Ferma tutti i servizi
docker compose down

# Rebuild completo (include pull delle immagini base)
docker compose --profile dev build --no-cache

# Riavvio con il profilo desiderato
docker compose --profile dev up -d
```

#### Rebuild Rapido (Solo Modifiche Locali)

```powershell
cd infra

# Rebuild senza cache solo per i servizi modificati
docker compose build api web

# Riavvio
docker compose up -d api web
```

### 9.2 Hot Reload (Sviluppo Locale)

Per evitare rebuild continui, usa il **hot reload** eseguendo i servizi in locale:

#### API Backend con Hot Reload

```powershell
cd apps\api\src\Api

# Usa dotnet watch per auto-reload
dotnet watch run

# Ogni volta che salvi un file .cs, l'API si riavvia automaticamente
```

#### Frontend Web con Hot Reload

```powershell
cd apps\web

# Next.js ha hot reload integrato
pnpm dev

# Le modifiche ai file .tsx/.ts vengono caricate automaticamente nel browser
```

### 9.3 Workflow Consigliato per Sviluppo

**Setup Ideale:**

1. **Servizi infrastrutturali in Docker** (postgres, redis, qdrant) - Raramente cambiano
2. **API in locale con `dotnet watch`** - Hot reload immediato
3. **Web in locale con `pnpm dev`** - Hot reload immediato

```powershell
# 1. Avvia solo infrastruttura
cd infra
docker compose --profile minimal up -d postgres redis qdrant mailpit

# 2. Avvia API in locale con hot reload
cd ..\apps\api\src\Api
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet watch run"

# 3. Avvia Web in locale con hot reload
cd ..\..\..\web
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "pnpm dev"
```

### 9.4 Rebuild solo dopo Modifiche Specifiche

| Modifica              | Azione Richiesta                 |
| --------------------- | -------------------------------- |
| File `.cs` (API)      | Nessuna (se usi `dotnet watch`)  |
| File `.tsx/.ts` (Web) | Nessuna (Next.js auto-reload)    |
| `Dockerfile`          | `docker compose build <service>` |
| `package.json`        | `pnpm install` + riavvio         |
| Database migrations   | `dotnet ef database update`      |
| `.env` files          | Riavvio servizio                 |
| `docker-compose.yml`  | `docker compose down && up`      |

### 9.5 Clear Cache e Reset Completo

Se hai problemi dopo le modifiche:

```powershell
cd infra

# 1. Ferma tutto
docker compose down

# 2. Rimuovi immagini vecchie (opzionale)
docker compose down --rmi local

# 3. Rebuild completo senza cache
docker compose build --no-cache

# 4. Riavvia
docker compose --profile dev up -d

# 5. Verifica logs per errori
docker compose logs -f api web
```

### 9.6 Reset Database (ATTENZIONE: Cancella Dati!)

```powershell
cd infra

# Ferma e rimuovi volumi (cancella tutti i dati!)
docker compose down -v

# Riavvia (ricrea database vuoto)
docker compose --profile dev up -d

# Riapplica migrazioni
cd ..\apps\api\src\Api
dotnet ef database update
```

### 9.7 Script Helper per Rebuild

Crea questo script per rebuild rapido:

```powershell
# Salva come: scripts/Rebuild-Services.ps1
param(
    [switch]$All,
    [switch]$Api,
    [switch]$Web,
    [switch]$NoCache
)

Set-Location infra

if ($All) {
    Write-Host "🔨 Rebuild completo..." -ForegroundColor Yellow
    if ($NoCache) {
        docker compose build --no-cache
    } else {
        docker compose build
    }
    docker compose up -d
} elseif ($Api) {
    Write-Host "🔨 Rebuild API..." -ForegroundColor Yellow
    docker compose up -d --build api
} elseif ($Web) {
    Write-Host "🔨 Rebuild Web..." -ForegroundColor Yellow
    docker compose up -d --build web
} else {
    Write-Host "❌ Specifica un parametro: -All, -Api, o -Web" -ForegroundColor Red
    Write-Host "Esempio: .\Rebuild-Services.ps1 -Api" -ForegroundColor Cyan
}

Set-Location ..
```

**Uso:**

```powershell
# Rebuild solo API
.\scripts\Rebuild-Services.ps1 -Api

# Rebuild solo Web
.\scripts\Rebuild-Services.ps1 -Web

# Rebuild tutto
.\scripts\Rebuild-Services.ps1 -All

# Rebuild tutto senza cache
.\scripts\Rebuild-Services.ps1 -All -NoCache
```

## Troubleshooting

### Problema: Porte già occupate

```powershell
# Verifica quali processi usano le porte
Get-NetTCPConnection -LocalPort 3000,8080,5432,6379,6333 | Select-Object LocalPort, State, OwningProcess

# Termina un processo specifico (usa il PID)
Stop-Process -Id <PID> -Force
```

### Problema: Container non si avviano

```powershell
cd infra

# Ferma tutti i container
docker compose down

# Rimuovi volumi (ATTENZIONE: cancella i dati!)
docker compose down -v

# Riavvia
docker compose --profile dev up -d
```

### Problema: Errori di memoria

```powershell
# Aumenta la memoria dedicata a Docker Desktop
# Impostazioni Docker Desktop > Resources > Memory > 8GB+

# Oppure limita i servizi attivi usando profili minimal
cd infra
docker compose down
docker compose --profile minimal up -d
```

## Script Helper Rapidi

### Start-All.ps1 (Crea questo script)

```powershell
# Salva come: scripts/Start-All.ps1
# Esegui con: .\scripts\Start-All.ps1

Write-Host "🚀 Avvio MeepleAI..." -ForegroundColor Cyan

# 1. Avvia Docker Services
Write-Host "`n📦 Avvio servizi Docker (profilo dev)..." -ForegroundColor Yellow
Set-Location infra
docker compose --profile dev up -d
Set-Location ..

# 2. Avvia API (locale)
Write-Host "`n🔧 Avvio API Backend..." -ForegroundColor Yellow
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd apps\api\src\Api; dotnet run"

# 3. Avvia Web (locale)
Start-Sleep -Seconds 5
Write-Host "`n🌐 Avvio Frontend Web..." -ForegroundColor Yellow
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd apps\web; pnpm dev"

Write-Host "`n✅ Tutti i servizi avviati!" -ForegroundColor Green
Write-Host "   Frontend: http://localhost:3000" -ForegroundColor Cyan
Write-Host "   API: http://localhost:8080" -ForegroundColor Cyan
Write-Host "   Grafana: http://localhost:3001" -ForegroundColor Cyan
```

### Stop-All.ps1 (Crea questo script)

```powershell
# Salva come: scripts/Stop-All.ps1
# Esegui con: .\scripts\Stop-All.ps1

Write-Host "🛑 Arresto MeepleAI..." -ForegroundColor Red

# Ferma Docker services
Write-Host "`n📦 Arresto servizi Docker..." -ForegroundColor Yellow
Set-Location infra
docker compose down
Set-Location ..

# Termina processi dotnet e node (se presenti)
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "node" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "`n✅ Tutti i servizi arrestati!" -ForegroundColor Green
```

## Riepilogo Comandi Rapidi

```powershell
# Avvio rapido (solo Docker, profilo dev)
cd infra && docker compose --profile dev up -d

# Visualizza logs
cd infra && docker compose logs -f

# Arresto
cd infra && docker compose down

# Riavvio di un singolo servizio
cd infra && docker compose restart api

# Rebuild di un servizio
cd infra && docker compose up -d --build api
```

## Note Importanti

1. **Credenziali di Sviluppo**: Le credenziali mostrate sono solo per sviluppo locale, NON usarle in produzione
2. **Risorse Hardware**: Profilo `full` richiede almeno 16GB RAM, usa `minimal` o `dev` se hai limitazioni
3. **Prima Esecuzione**: Il primo avvio può richiedere 10-15 minuti per scaricare tutte le immagini Docker
4. **Secrets**: Assicurati che tutti i file in `infra/secrets/` siano creati correttamente
5. **OpenRouter API Key**: Opzionale per sviluppo, necessaria per funzionalità AI/LLM complete

## Documentazione Aggiuntiva

- [README.md](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/README.md) - Documentazione principale
- [.env.README.md](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/.env.README.md) - Spiegazione variabili d'ambiente
- [docker-compose.yml](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/infra/docker-compose.yml) - Configurazione servizi Docker
- [CLAUDE.md](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/CLAUDE.md) - Guida completa per sviluppatori
