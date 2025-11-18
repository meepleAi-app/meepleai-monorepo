# Setup & Test Environment Script

## 🎯 Overview

Automated script per avviare l'ambiente di sviluppo MeepleAI con database pulito, build completa e test.

## 🚀 Quick Start

### Modo Veloce (senza test)
```bash
./quick-start.sh
```

### Setup Completo con Test
```bash
./tools/setup-test-environment.sh
```

### Preview (Dry Run)
```bash
./tools/setup-test-environment.sh --dry-run
```

## 📋 Opzioni Disponibili

| Opzione | Descrizione |
|---------|-------------|
| `--dry-run` | Mostra cosa verrà eseguito senza eseguire realmente |
| `--skip-cleanup` | Salta la pulizia iniziale di Docker e build artifacts |
| `--skip-frontend` | Non avvia il server frontend (solo backend) |
| `--skip-tests` | Non esegue i test dopo il setup |
| `--full` | Esegue tutti i test inclusi E2E (richiede più tempo) |
| `--verbose, -v` | Output dettagliato per debugging |
| `--help, -h` | Mostra la guida completa |

## 📖 Esempi d'Uso

### 1. Setup Base (Backend + Frontend + Test Unitari)
```bash
./tools/setup-test-environment.sh
```

**Cosa fa:**
- ✅ Ferma e pulisce container Docker
- ✅ Pulisce build artifacts (.NET bin/obj, Next.js .next)
- ✅ Avvia servizi Docker (PostgreSQL, Qdrant, Redis, Seq)
- ✅ Aspetta che PostgreSQL sia pronto
- ✅ Builda e avvia API (porta 8080)
- ✅ Avvia frontend dev server (porta 3000)
- ✅ Esegue test backend e frontend

**Tempo stimato:** ~3-5 minuti

### 2. Quick Start - Solo Ambiente
```bash
./quick-start.sh
```
Equivalente a `./tools/setup-test-environment.sh --skip-tests`

**Tempo stimato:** ~2-3 minuti

### 3. Setup Completo + Test E2E
```bash
./tools/setup-test-environment.sh --full
```

**Include:**
- Tutto del setup base
- Test E2E con Playwright
- Test di performance

**Tempo stimato:** ~8-12 minuti

### 4. Solo Backend (per sviluppo API)
```bash
./tools/setup-test-environment.sh --skip-frontend --skip-tests
```

**Tempo stimato:** ~1-2 minuti

### 5. Preview Senza Eseguire
```bash
./tools/setup-test-environment.sh --dry-run
```

Mostra tutti i comandi che verrebbero eseguiti senza eseguirli realmente.

### 6. Setup Veloce (senza pulizia iniziale)
```bash
./tools/setup-test-environment.sh --skip-cleanup
```

Utile quando Docker è già configurato correttamente.

## 🔧 Cosa Fa lo Script in Dettaglio

### Step 1: Docker Cleanup
- Ferma tutti i container con `docker compose down -v`
- Rimuove volumi per database pulito

### Step 2: Build Artifacts Cleanup
- Pulisce directory `.NET` (bin/, obj/)
- Rimuove `.next` cache di Next.js

### Step 3: Docker Services Start
- Avvia: PostgreSQL, Qdrant, Redis, Seq
- Attende che PostgreSQL sia pronto con `pg_isready`

### Step 4: API Build & Start
- Build con `dotnet build --configuration Release`
- Avvio in background con log in `api.log`
- Health check su `http://localhost:8080/health`
- **Applica automaticamente le migrations EF Core**
- **Carica dati demo (admin/editor/user@meepleai.dev)**

### Step 5: Frontend Start
- Installa dipendenze con `pnpm install` (se necessario)
- Avvia dev server con `pnpm dev`
- Log in `web.log`
- Health check su `http://localhost:3000`

### Step 6: Tests
- **Backend:** `dotnet test` (unit + integration)
- **Frontend:** `pnpm test` (unit tests)
- **E2E (--full):** `pnpm test:e2e` (Playwright)

## 📊 Output dello Script

Al termine dello script, vengono mostrati:

### URLs dei Servizi
- **Frontend:** http://localhost:3000
- **API:** http://localhost:8080
- **Health Check:** http://localhost:8080/health
- **Seq (Logs):** http://localhost:8081
- **Qdrant Dashboard:** http://localhost:6333/dashboard

### Utenti Demo
- `admin@meepleai.dev` - Password: `Demo123!`
- `editor@meepleai.dev` - Password: `Demo123!`
- `user@meepleai.dev` - Password: `Demo123!`

### Log Files
- `api.log` - Log del server API
- `web.log` - Log del server frontend
- `test-backend.log` - Output test backend
- `test-frontend.log` - Output test frontend
- `test-e2e.log` - Output test E2E (se --full)

## 🛑 Fermare i Servizi

Lo script mantiene i servizi attivi in foreground. Per fermare tutto:

```bash
Ctrl+C
```

Questo attiverà il cleanup automatico che:
1. Ferma il processo frontend
2. Ferma il processo API
3. Mantiene i container Docker attivi (per riutilizzo)

Per fermare anche Docker:
```bash
cd infra && docker compose down
```

## 🔍 Troubleshooting

### PostgreSQL non si avvia
```bash
# Verifica log Docker
cd infra && docker compose logs meepleai-postgres

# Riavvia container
docker compose restart meepleai-postgres
```

### API non passa health check
```bash
# Verifica log API
tail -f api.log

# Verifica manualmente
curl http://localhost:8080/health
```

### Frontend non si avvia
```bash
# Verifica log
tail -f web.log

# Verifica manualmente
curl http://localhost:3000
```

### Test falliscono
```bash
# Backend
cat test-backend.log

# Frontend
cat test-frontend.log

# E2E
cat test-e2e.log
```

### Cleanup manuale completo
```bash
# Ferma tutto e pulisci volumi
cd infra && docker compose down -v

# Pulisci build artifacts
cd ../apps/api/src/Api && dotnet clean
cd ../../../web && rm -rf .next node_modules

# Rimuovi log
rm -f *.log
```

## 🎓 Best Practices

### Per Sviluppo Quotidiano
```bash
# Usa quick-start per sessioni giornaliere
./quick-start.sh
```

### Prima di PR/Commit
```bash
# Esegui suite completa di test
./tools/setup-test-environment.sh --full
```

### Per Debugging
```bash
# Solo backend con verbose output
./tools/setup-test-environment.sh --skip-frontend --verbose
```

### Per CI/CD Simulation
```bash
# Setup completo da zero
./tools/setup-test-environment.sh --full
```

## 📚 Documentazione Correlata

- [CLAUDE.md](../CLAUDE.md) - Guida completa architettura
- [Testing Guide](../docs/02-development/testing/testing-guide.md) - Strategia test
- [Docker Setup](../infra/README.md) - Configurazione Docker
- [API Documentation](../docs/03-api/board-game-ai-api-specification.md) - Specifiche API

## ⚡ Performance Tips

1. **Skip Cleanup** se Docker è già configurato: `--skip-cleanup`
2. **Skip Tests** durante sviluppo attivo: `--skip-tests`
3. **Skip Frontend** per sviluppo solo API: `--skip-frontend`
4. **Usa Quick Start** per iterazioni rapide

## 🔐 Security Notes

- Gli utenti demo hanno password `Demo123!` (solo per sviluppo locale)
- In produzione, le credenziali sono gestite tramite variabili d'ambiente
- Il database viene creato senza dati sensibili
- API keys demo vengono generate automaticamente

## 🆘 Support

Per problemi o domande:
1. Verifica i log file (*.log)
2. Esegui in `--verbose` mode
3. Controlla `/health` endpoint
4. Vedi [Troubleshooting](#-troubleshooting)

---

**Versione:** 1.0
**Ultima Modifica:** 2025-11-18
**Autore:** Engineering Team
