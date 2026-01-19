# Guida Visual Studio Code - MeepleAI

Guida completa per lavorare con VS Code nel progetto MeepleAI, task automation, troubleshooting, e best practices.

**Ultima revisione**: 2026-01-18
**Issue correlate**: #2570 (Secrets), Docker workflow optimization

---

## 📋 Indice

1. [Task VS Code](#task-vs-code)
2. [Troubleshooting Terminale](#troubleshooting-terminale)
3. [Docker Workflow](#docker-workflow)
4. [Secrets Management](#secrets-management)
5. [Best Practices](#best-practices)
6. [Shortcuts Utili](#shortcuts-utili)

---

## Task VS Code

### Setup Completo

Il file `.vscode/tasks.json` contiene task pre-configurati per tutti i workflow comuni.

### Come Eseguire Task

**Metodo 1: Command Palette** (Raccomandato)
```
1. Ctrl+Shift+P (o F1)
2. Digita: "Tasks: Run Task"
3. Seleziona il task dalla lista
```

**Metodo 2: Menu Terminale**
```
1. Terminal → Run Task...
2. Seleziona il task
```

**Metodo 3: Keybinding Custom** (Opzionale)
Aggiungi a `.vscode/keybindings.json`:
```json
[
  {
    "key": "ctrl+shift+d ctrl+b",
    "command": "workbench.action.tasks.runTask",
    "args": "docker-build-no-cache"
  },
  {
    "key": "ctrl+shift+d ctrl+u",
    "command": "workbench.action.tasks.runTask",
    "args": "docker-up-dev"
  }
]
```

### Task Disponibili

#### 🐳 Docker - Operazioni Base

| Task | Descrizione | Uso |
|------|-------------|-----|
| `docker-build-no-cache` | Build completo senza cache + pull immagini base | Fresh build, fix cache issues |
| `docker-up` | Start servizi core (postgres, redis, qdrant) | Development standard |
| `docker-up-dev` | Start con profilo dev (include Grafana, monitoring) | Development + observability |
| `docker-down` | Stop servizi (mantiene volumi/data) | Pausa lavoro |
| `docker-down-volumes` | Stop + elimina volumi ⚠️ DATA LOSS | Reset completo database |
| `docker-logs` | Logs tutti i servizi (follow mode) | Debugging generale |
| `docker-logs-api` | Logs solo API backend | Debugging backend |
| `docker-ps` | Status containers attivi | Quick health check |
| `docker-restart-service` | Restart servizio specifico (prompt) | Restart selettivo |

#### 🔧 Docker - Workflow Compound

Task che eseguono sequenze automatiche:

| Task | Workflow | Durata Stimata |
|------|----------|----------------|
| `docker-fresh-start` | down-volumes → setup-secrets → build-no-cache → up-dev | 10-20 min |
| `docker-quick-rebuild` | down → rebuild-all (force-recreate) | 5-10 min |
| `docker-rebuild-all` | Build + force-recreate + no-cache in un comando | 8-15 min |

**Quando usare**:
- `docker-fresh-start`: Primo setup, problemi persistenti, reset totale
- `docker-quick-rebuild`: Dopo modifiche Dockerfile, conflitti immagini
- `docker-rebuild-all`: Alternative a fresh-start senza cancellare volumi

#### 🔐 Secrets Management

| Task | Descrizione |
|------|-------------|
| `setup-secrets` | Esegue `infra/secrets/setup-secrets.ps1 -SaveGenerated` |

#### 💻 Backend (.NET)

| Task | Descrizione | Uso Tipico |
|------|-------------|------------|
| `build-api` | Build backend .NET (Debug config) | Pre-test, verifica compilazione |
| `clean-api` | Clean build artifacts | Fix build issues |
| `test-api` | Test suite completa backend | CI/CD, pre-commit |
| `watch-api` | Hot reload development mode | Development attivo backend |

#### 🎨 Frontend (Next.js)

| Task | Descrizione | Uso Tipico |
|------|-------------|------------|
| `dev` | Next.js dev server (hot reload) | Development attivo frontend |
| `build` | Production build | Pre-deploy, verifica build |
| `test` | Test suite Vitest | CI/CD, pre-commit |
| `lint` | ESLint + Prettier check | Quality check, pre-commit |

#### 📊 Database

| Task | Descrizione | Input Richiesto |
|------|-------------|-----------------|
| `ef-migrations-add` | Crea nuova migration EF Core | Nome migration (prompt) |
| `ef-database-update` | Applica migrations pending | - |

#### 🔄 Compound Tasks

Task che coordinano operazioni multiple:

| Task | Workflow |
|------|----------|
| `build-all` | build-api + build (frontend) |
| `test-all` | test-api + test (frontend) |
| `start-all-dev` | docker-up + watch-api + dev |

---

## Troubleshooting Terminale

### Problema: Comando `docker compose build no-cache --pull` fallisce

**Sintomo**:
```bash
no such service: no-cache
```

**Causa**: Terminale VS Code usa Git Bash (Unix-style) invece di PowerShell/CMD

**Soluzione 1: Cambia Terminale Predefinito**

1. Nel terminale VS Code, click sulla **freccia giù** (in alto a destra)
2. Seleziona **"Select Default Profile"**
3. Scegli **"PowerShell"** o **"Command Prompt"**
4. Chiudi e riapri terminale (`Ctrl+J` per toggle)

**Soluzione 2: Usa Task VS Code**

Invece di digitare comandi, usa i task:
```
Ctrl+Shift+P → "Tasks: Run Task" → docker-build-no-cache
```

**Soluzione 3: PowerShell Esplicito**

```powershell
powershell -Command "cd D:\Repositories\meepleai-monorepo-frontend\infra; docker compose build --no-cache --pull"
```

### Problema: Path Windows non riconosciuto (WSL/Git Bash)

**Sintomo**:
```bash
cd: D:\Repositories\...: No such file or directory
```

**Causa**: Git Bash usa path Unix-style (`/mnt/d/...`)

**Soluzione**: Usa PowerShell come terminale predefinito (vedi sopra)

### Problema: Encoding caratteri strani

**Sintomo**: Output con simboli illeggibili (░▒▓)

**Soluzione**: Imposta UTF-8
```powershell
# PowerShell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Aggiungi a $PROFILE per permanenza
Add-Content $PROFILE "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8"
```

---

## Docker Workflow

### Quick Start Development

**Workflow raccomandato per inizio giornata**:

```bash
# Opzione A: Start veloce (mantiene data)
Ctrl+Shift+P → docker-up-dev

# Opzione B: Fresh start (reset totale)
Ctrl+Shift+P → docker-fresh-start
```

### Build Completo No-Cache

**Quando necessario**:
- Primo setup progetto
- Modifiche a Dockerfile
- Problemi cache Docker
- Aggiornamenti immagini base

**Esecuzione**:
```
Ctrl+Shift+P → docker-build-no-cache
```

**Comando equivalente**:
```bash
cd infra
docker compose build --no-cache --pull
```

### Warning Grafana: `GRAFANA_ADMIN_PASSWORD variable is not set`

**Spiegazione**: Warning innocuo durante build

**Causa Tecnica**:
- Grafana è dietro profilo `[dev, observability, full]`
- Docker Compose valida TUTTE le variabili prima del build
- `${GRAFANA_ADMIN_PASSWORD}` non è nell'ambiente HOST durante build
- Valore viene caricato da `infra/secrets/monitoring.secret` al runtime

**Risoluzione**: Ignora il warning ✅

Il servizio Grafana caricherà correttamente il secret dal file quando viene avviato:
```yaml
grafana:
  profiles: [dev, observability, full]
  env_file:
    - ./secrets/monitoring.secret  # ← Caricato al runtime
  environment:
    GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD}
```

**Verifica Funzionamento**:
```bash
# Start con profilo dev
docker compose --profile dev up -d grafana

# Check logs (nessun errore di password)
docker compose logs grafana | grep -i password

# Verifica secret file
cat infra/secrets/monitoring.secret | grep GRAFANA
```

### Profiles Docker Compose

**Profiles disponibili**:
- `core`: Servizi essenziali (postgres, redis, qdrant)
- `dev`: Core + monitoring (Grafana, Prometheus)
- `observability`: Monitoring avanzato + tracing
- `full`: Tutti i servizi (inclusi opzionali)

**Uso**:
```bash
# Start con profilo specifico
docker compose --profile dev up -d

# Task VS Code già configurato
Ctrl+Shift+P → docker-up-dev
```

### Gestione Volumi e Data

**Attenzione**: Operazioni distruttive marcate con ⚠️

| Comando | Effetto | Data Loss |
|---------|---------|-----------|
| `docker-down` | Stop servizi | ❌ No |
| `docker-down-volumes` | Stop + elimina volumi | ⚠️ Sì |
| `docker-fresh-start` | Reset totale + rebuild | ⚠️ Sì |

**Best Practice**: Backup prima di operazioni distruttive
```bash
# Export database (se necessario)
docker compose exec postgres pg_dump -U meepleai meepleai > backup.sql
```

### Restart Servizi Specifici

**Quando necessario**:
- Modifiche a file `.secret`
- Reload configurazione
- Debug servizio specifico

**Metodi**:
```bash
# Via task (con prompt)
Ctrl+Shift+P → docker-restart-service → [nome servizio]

# Via comando diretto
cd infra
docker compose restart redis
docker compose restart api
```

---

## Secrets Management

### Setup Automatico

**Task**: `setup-secrets`
```
Ctrl+Shift+P → setup-secrets
```

**Equivalente**:
```bash
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated
```

**Cosa fa**:
1. Copia `.secret.example` → `.secret`
2. Genera password/API keys sicuri (16-64 caratteri)
3. Sostituisce `change_me...` con valori reali
4. (Opzionale) Salva backup `.generated-values-*.txt`

### File Secrets Generati

**CRITICAL** (startup blockers):
- `admin.secret` (ADMIN_PASSWORD + INITIAL_ADMIN_PASSWORD devono coincidere)
- `database.secret` (POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB)
- `jwt.secret`
- `qdrant.secret`
- `redis.secret`
- `embedding-service.secret`

**IMPORTANT** (warnings se mancanti):
- `openrouter.secret`
- `unstructured-service.secret`
- `bgg.secret`
- `monitoring.secret` (include GRAFANA_ADMIN_PASSWORD)

**OPTIONAL** (features avanzate):
- `oauth.secret`
- `email.secret`
- `storage.secret`
- `n8n.secret`
- `traefik.secret`

### Aggiornare Secrets

**Workflow Corretto** (restart NON ricarica env_file!):
```bash
# 1. Modifica file .secret
notepad infra/secrets/redis.secret

# 2. Ricrea container (stop + rm + up)
docker compose stop redis
docker compose rm -f redis
docker compose up -d redis

# 3. Verifica logs
docker compose logs redis | tail -20
```

**Task VS Code rapido**:
```
Ctrl+Shift+P → docker-restart-service → redis
# Task esegue automaticamente: stop → rm → up
```

### Fix Comuni

**Problema: API container unhealthy - password authentication failed**

```
Errore: password authentication failed for user "meeple"
```

**Causa**: Database creato con password diversa da quella in `database.secret`

**Soluzione**:
```bash
cd infra
docker compose stop postgres
docker compose rm -f postgres
docker volume rm meepleai_postgres_data
docker compose up -d postgres
# Script init/postgres-init.sql crea automaticamente user meeple
```

**Problema: INITIAL_ADMIN_PASSWORD not configured**

```
Errore: INITIAL_ADMIN_PASSWORD is not configured
```

**Causa**: Manca `INITIAL_ADMIN_PASSWORD` in `admin.secret`

**Soluzione**: I due campi devono coincidere:
```bash
# infra/secrets/admin.secret
ADMIN_PASSWORD=<same-password>
INITIAL_ADMIN_PASSWORD=<same-password>  # ← Deve essere uguale!
```

Poi restart API:
```bash
docker compose restart api
```

### Workflow Secrets (NO Modifica Manuale .env)

**REGOLA IMPORTANTE**: Non modificare mai `.env` files manualmente!

**Workflow Corretto**:
```bash
# 1. Modifica SOLO il file .secret
notepad infra/secrets/redis.secret

# 2. Ricrea container (restart NON ricarica env_file)
docker compose stop redis
docker compose rm -f redis
docker compose up -d redis

# 3. Verifica
docker compose logs redis
```

**Gerarchia Secrets** (source of truth):
```
infra/secrets/*.secret  →  env_file caricato al create container
                       →  NO sync manuale richiesta
                       →  NO modifica .env files
```

**Riferimenti**:
- [Secrets Management Guide](../04-deployment/secrets-management.md)
- [Local Secrets Setup](local-secrets-setup.md)

---

## Best Practices

### Workflow Giornaliero Raccomandato

**Inizio giornata**:
1. `git pull` (aggiorna repo)
2. `Ctrl+Shift+P → docker-up-dev` (start infra)
3. `Ctrl+Shift+P → watch-api` (backend hot reload)
4. `Ctrl+Shift+P → dev` (frontend hot reload)

**Durante sviluppo**:
- Usa `docker-logs-api` per debugging backend
- Usa browser DevTools per frontend
- `docker-ps` per quick health check

**Fine giornata**:
```bash
git add . && git commit -m "feat: description"
Ctrl+Shift+P → docker-down  # Stop containers (mantieni data)
```

### Pre-Commit Checklist

**Automatico** (se git hooks configurati):
- ✅ ESLint frontend
- ✅ Prettier frontend
- ✅ dotnet format backend

**Manuale**:
```
Ctrl+Shift+P → test-all    # Test suite completa
Ctrl+Shift+P → build-all   # Verifica build
```

### Debugging Docker

**Container non parte**:
```bash
# 1. Check logs
docker compose logs [service-name]

# 2. Check status
docker compose ps

# 3. Restart specifico
docker compose restart [service-name]

# 4. Se persistente, rebuild
Ctrl+Shift+P → docker-quick-rebuild
```

**Database connection issues**:
```bash
# 1. Check postgres running
docker compose ps postgres

# 2. Check logs
docker compose logs postgres

# 3. Apply migrations
Ctrl+Shift+P → ef-database-update

# 4. Se fallisce, reset database
Ctrl+Shift+P → docker-down-volumes
Ctrl+Shift+P → docker-up-dev
```

**API non risponde**:
```bash
# 1. Check API logs
Ctrl+Shift+P → docker-logs-api

# 2. Check health endpoint
curl http://localhost:8080/health

# 3. Restart API
docker compose restart api

# 4. Rebuild se modifiche Dockerfile
Ctrl+Shift+P → docker-build-no-cache
```

### Performance Tips

**Build più veloci**:
- Usa `docker-quick-rebuild` invece di `docker-fresh-start` quando possibile
- `docker-build-no-cache` solo se necessario (cache issues)
- `.dockerignore` correttamente configurato

**Development più veloce**:
- `watch-api` (hot reload backend) invece di restart manuale
- `dev` (Next.js Fast Refresh) per frontend
- Profili Docker: usa `dev` non `full` se non serve tutto

---

## Shortcuts Utili

### VS Code Generici

| Shortcut | Azione |
|----------|--------|
| `Ctrl+Shift+P` | Command Palette |
| `Ctrl+J` | Toggle terminale |
| `Ctrl+Shift+'` | Nuovo terminale |
| `Ctrl+K Ctrl+S` | Keyboard shortcuts |
| `Ctrl+,` | Settings |

### Task Execution

| Shortcut | Azione |
|----------|--------|
| `Ctrl+Shift+P → Tasks: Run Task` | Esegui task |
| `Ctrl+Shift+B` | Build task (default) |
| `Ctrl+Shift+T` | Test task (default) |

### Git Integration

| Shortcut | Azione |
|----------|--------|
| `Ctrl+Shift+G` | Source Control panel |
| `Ctrl+Enter` | Commit staged |
| `Ctrl+Shift+P → Git: Pull` | Pull changes |

### Custom Keybindings (Opzionale)

Aggiungi a `.vscode/keybindings.json`:

```json
[
  {
    "key": "ctrl+shift+d ctrl+b",
    "command": "workbench.action.tasks.runTask",
    "args": "docker-build-no-cache"
  },
  {
    "key": "ctrl+shift+d ctrl+u",
    "command": "workbench.action.tasks.runTask",
    "args": "docker-up-dev"
  },
  {
    "key": "ctrl+shift+d ctrl+d",
    "command": "workbench.action.tasks.runTask",
    "args": "docker-down"
  },
  {
    "key": "ctrl+shift+d ctrl+l",
    "command": "workbench.action.tasks.runTask",
    "args": "docker-logs-api"
  },
  {
    "key": "ctrl+shift+t ctrl+a",
    "command": "workbench.action.tasks.runTask",
    "args": "test-all"
  }
]
```

**Pattern**: `Ctrl+Shift+D` (Docker) + comando
- `B` = Build
- `U` = Up
- `D` = Down
- `L` = Logs

---

## Riferimenti

### Documentazione Correlata

- [Git Workflow](git-workflow.md)
- [Secrets Management](../04-deployment/secrets-management.md)
- [Local Secrets Setup](local-secrets-setup.md)
- [Docker Services Test URLs](docker-services-test-urls.md)
- [Operational Guide](operational-guide.md)

### File Configurazione

- `.vscode/tasks.json` - Task definitions
- `.vscode/launch.json` - Debug configurations
- `.vscode/settings.json` - Workspace settings
- `infra/docker-compose.yml` - Services orchestration
- `infra/secrets/*.secret` - Credentials (gitignored)

### Troubleshooting Avanzato

Per problemi complessi, consulta:
- [Troubleshooting Guide](troubleshooting/)
- [GitHub Issues](https://github.com/your-org/meepleai/issues)
- Team Slack: `#dev-support`

---

**Ultimo aggiornamento**: 2026-01-18
**Contributi**: Aggiungi miglioramenti via PR con label `docs`
