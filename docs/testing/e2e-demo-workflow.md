# E2E Demo Workflow - MeepleAI

Guida step-by-step per il flusso E2E completo dell'ambiente di sviluppo MeepleAI.

## Prerequisiti

- Docker Desktop installato e running
- Node.js 18+ e pnpm installati
- .NET 9 SDK installato
- PowerShell 7+ (per script automatizzato)

## Quick Start (Automatizzato)

```powershell
# Esegui tutto il flusso
cd scripts
.\e2e-demo-setup.ps1

# Oppure fasi specifiche
.\e2e-demo-setup.ps1 -Phase infra    # Solo infrastruttura
.\e2e-demo-setup.ps1 -Phase ai       # Solo servizi AI
.\e2e-demo-setup.ps1 -Phase build    # Solo build app
.\e2e-demo-setup.ps1 -Phase seed     # Solo seed dati
```

---

## Flusso Manuale Dettagliato

### Fase 1: Avvio Servizi Critici

```bash
cd infra

# 1. Verifica/genera secrets
cd secrets && pwsh setup-secrets.ps1 -SaveGenerated && cd ..

# 2. Avvia servizi critici
docker compose --profile minimal up -d postgres redis qdrant

# 3. Verifica health
docker ps --format "table {{.Names}}\t{{.Status}}"
# Attendi che tutti mostrino "(healthy)"
```

**Servizi Critici:**
| Servizio | Porta | Health Check |
|----------|-------|--------------|
| postgres | 5432 | `pg_isready` |
| redis | 6379 | `redis-cli ping` |
| qdrant | 6333 | HTTP `/readyz` |

### Fase 2: Avvio Servizi AI (Opzionali)

```bash
# Build senza cache (prima volta o dopo modifiche)
docker compose build --no-cache unstructured-service smoldocling-service

# Avvia servizi AI
docker compose --profile ai up -d unstructured-service smoldocling-service

# Verifica (può richiedere 2-5 minuti per download modelli)
docker logs -f meepleai-unstructured
docker logs -f meepleai-smoldocling
```

**Servizi AI:**
| Servizio | Porta | Tempo Avvio |
|----------|-------|-------------|
| unstructured | 8001 | ~2 min |
| smoldocling | 8002 | ~5 min (download modello) |

### Fase 3: Build e Avvio Applicazioni

#### Opzione A: Tutto in Docker

```bash
cd infra
docker compose --profile minimal up -d --build traefik frontend

# Backend (se configurato in docker-compose)
docker compose up -d api
```

#### Opzione B: Ibrido (Raccomandato per sviluppo)

```bash
# Terminal 1: Backend
cd apps/api/src/Api
dotnet run
# → http://localhost:8080

# Terminal 2: Frontend
cd apps/web
pnpm install
pnpm dev
# → http://localhost:3000
```

**Verifica:**
```bash
# Health check API
curl http://localhost:8080/health

# Verifica frontend
curl -I http://localhost:3000
```

### Fase 4: Login Admin

#### Metodo 1: Cookie Dev Mode

```bash
# Imposta cookie nel browser (DevTools → Application → Cookies)
# Nome: user_role
# Valore: Admin
# Dominio: localhost
```

#### Metodo 2: Login UI

1. Vai a http://localhost:3000/login
2. Usa credenziali admin (da `admin.secret`)
3. Verifica redirect a dashboard

### Fase 5: Inizializzazione Shared Games

#### Via API (Bulk Import)

```bash
# Import singolo gioco da BGG
curl -X POST "http://localhost:8080/api/v1/admin/shared-games/import-bgg" \
  -H "Content-Type: application/json" \
  -H "Cookie: user_role=Admin" \
  -d '{"bggId": 13}'  # Catan
```

#### Via UI

1. Vai a http://localhost:3000/admin/shared-games/add-from-bgg
2. Cerca "Catan" nella barra di ricerca
3. Seleziona il gioco dai risultati
4. Clicca "Conferma Importazione"

**Giochi Demo:**
| Gioco | BGG ID | PDF | Azione Demo |
|-------|--------|-----|-------------|
| Catan | 13 | ✅ | PDF Upload |
| Ticket to Ride | 9209 | ✅ | Edit |
| Carcassonne | 822 | ✅ | Remove |
| Wingspan | 266192 | ✅ | PDF SmolDocling |
| Terraforming Mars | 167791 | ✅ | PDF Unstructured |

### Fase 6: Upload PDF Rulebooks

#### Upload Manuale (UI)

1. Vai a http://localhost:3000/admin/shared-games
2. Seleziona un gioco
3. Tab "Documenti" → "Aggiungi Documento"
4. Seleziona PDF da `data/rulebook/`

#### Via API

```bash
# Upload PDF per Catan (ID da fase 5)
curl -X POST "http://localhost:8080/api/v1/admin/shared-games/{gameId}/documents" \
  -H "Cookie: user_role=Admin" \
  -F "file=@data/rulebook/cantan_en_rulebook.pdf" \
  -F "type=rulebook"
```

**PDF Disponibili:**
```
data/rulebook/
├── 7-wonders_rulebook.pdf
├── agricola_rulebook.pdf
├── azul_rulebook.pdf
├── barrage_rulebook.pdf
├── cantan_en_rulebook.pdf
├── carcassone_rulebook.pdf
├── pandemic_rulebook.pdf
├── root_rulebook.pdf
├── splendor_rulebook.pdf
├── terraforming-mars_rulebook.pdf
├── ticket-to-ride_rulebook.pdf
└── wingspan_en_rulebook.pdf
```

### Fase 7: Operazioni CRUD Demo

#### Edit Gioco (Ticket to Ride)

1. http://localhost:3000/admin/shared-games
2. Cerca "Ticket to Ride"
3. Clicca icona modifica (✏️)
4. Modifica descrizione o altri campi
5. Salva

#### Remove Gioco (Carcassonne)

1. http://localhost:3000/admin/shared-games
2. Cerca "Carcassonne"
3. Clicca icona elimina (🗑️)
4. Conferma eliminazione (soft-delete)
5. Verifica in http://localhost:3000/admin/shared-games/pending-deletes

---

## Troubleshooting

### Docker Services Non Partono

```bash
# Verifica logs
docker compose logs postgres
docker compose logs redis
docker compose logs qdrant

# Restart pulito
docker compose down -v
docker compose --profile minimal up -d
```

### API 405 su GET /admin/shared-games

**Bug noto**: L'endpoint GET non è registrato correttamente nell'API.
Verificare in `SharedGameCatalogEndpoints.cs` linea 99.

### PDF Upload Fallisce

1. Verifica servizi AI attivi: `curl http://localhost:8001/health`
2. Verifica dimensione file (max 50MB)
3. Controlla logs: `docker logs meepleai-unstructured`

### Frontend Non Carica

```bash
# Verifica dipendenze
cd apps/web
pnpm install

# Clear cache
rm -rf .next
pnpm dev
```

---

## URLs di Riferimento

| Risorsa | URL |
|---------|-----|
| Frontend | http://localhost:3000 |
| Admin Panel | http://localhost:3000/admin |
| Shared Games | http://localhost:3000/admin/shared-games |
| BGG Import | http://localhost:3000/admin/shared-games/add-from-bgg |
| API Health | http://localhost:8080/health |
| API Docs | http://localhost:8080/scalar/v1 |
| Grafana | http://localhost:3001 |
| Prometheus | http://localhost:9090 |

---

## Script Automazione

```powershell
# Esecuzione completa
.\scripts\e2e-demo-setup.ps1

# Con opzioni
.\scripts\e2e-demo-setup.ps1 -Phase all -SkipBuild  # Usa cache
.\scripts\e2e-demo-setup.ps1 -Phase infra           # Solo infra
.\scripts\e2e-demo-setup.ps1 -Force                 # Ignora errori
```

---

*Ultimo aggiornamento: 2026-01-21*
