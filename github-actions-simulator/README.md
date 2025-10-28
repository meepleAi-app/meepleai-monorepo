# 🎭 GitHub Actions Simulator

Ambiente Docker isolato per testare GitHub Actions workflows in locale prima del push.

## 📋 Indice

- [Caratteristiche](#-caratteristiche)
- [Architettura](#-architettura)
- [Setup Rapido](#-setup-rapido)
- [Uso](#-uso)
- [Script Helper](#-script-helper)
- [Troubleshooting](#-troubleshooting)
- [Log Viewer Web](#-log-viewer-web)

## ✨ Caratteristiche

- ✅ **Ambiente completo**: .NET 9, Node 20, pnpm, k6, Semgrep, actionlint
- ✅ **Docker-in-Docker**: Esegue act con supporto container reali
- ✅ **Servizi integrati**: PostgreSQL, Redis, Qdrant (come su GitHub)
- ✅ **Logging completo**: Tutti i log persistenti con timestamp
- ✅ **Artifacts**: Salvataggio artifacts come su GitHub Actions
- ✅ **Cache**: Cache persistente per velocizzare i test
- ✅ **Web UI**: Dozzle per visualizzare log in tempo reale
- ✅ **Isolato**: Completamente separato dal progetto principale

## 🏗️ Architettura

```
github-actions-simulator/
├── Dockerfile              # Immagine con tutti gli strumenti
├── docker-compose.yml      # Orchestrazione servizi
├── scripts/                # Script helper
│   ├── run-workflow.sh     # Esegui workflow completo
│   ├── run-job.sh          # Esegui singolo job
│   ├── view-logs.sh        # Visualizza log con filtri
│   ├── validate-workflows.sh # Valida sintassi YAML
│   ├── health-check.sh     # Verifica ambiente
│   └── cleanup.sh          # Pulisci log/artifacts vecchi
├── config/                 # Configurazione
│   ├── .actrc              # Variabili ambiente per act
│   └── .secrets            # Secrets (gitignored)
├── logs/                   # Log persistenti
├── artifacts/              # Artifacts GitHub Actions
├── cache/                  # Cache act
└── .act/                   # Configurazione act runtime
```

## 🚀 Setup Rapido

### 1. Build dell'ambiente

```bash
cd github-actions-simulator
docker compose build
```

**Tempo**: ~5-10 minuti (prima volta)

### 2. Configura i secrets

```bash
# Copia il template
cp config/.secrets.example config/.secrets

# Modifica con i tuoi secrets
nano config/.secrets  # o usa il tuo editor preferito
```

### 3. Avvia l'ambiente

```bash
# Avvia tutti i servizi (PostgreSQL, Redis, Qdrant, Log Viewer)
docker compose up -d

# Entra nel container act-runner
docker compose exec act-runner bash
```

### 4. Verifica lo stato

```bash
# All'interno del container
health-check.sh
```

## 💻 Uso

### Eseguire workflow completi

```bash
# Dentro il container act-runner

# Workflow CI completo (default)
run-workflow.sh

# Workflow specifico
run-workflow.sh .github/workflows/security-scan.yml

# Con event type specifico
run-workflow.sh .github/workflows/ci.yml pull_request
```

### Eseguire singoli job

```bash
# Job specifico dal CI
run-job.sh .github/workflows/ci.yml ci-api

# Job frontend
run-job.sh .github/workflows/ci.yml ci-web

# Job con event type
run-job.sh .github/workflows/ci.yml ci-api push
```

### Validare workflow

```bash
# Valida tutti i workflow YAML
validate-workflows.sh
```

### Visualizzare log

```bash
# Lista tutti i log
view-logs.sh

# Visualizza ultimo log CI
view-logs.sh ci-api

# Visualizza log specifico per data
view-logs.sh 20241026

# Visualizza log completo
view-logs.sh ci-api --full
```

### Pulizia

```bash
# Rimuovi log/artifacts > 7 giorni
cleanup.sh

# Rimuovi log/artifacts > 3 giorni
cleanup.sh 3
```

## 🛠️ Script Helper

| Script | Descrizione | Esempio |
|--------|-------------|---------|
| `run-workflow.sh` | Esegue workflow completo | `run-workflow.sh .github/workflows/ci.yml` |
| `run-job.sh` | Esegue singolo job | `run-job.sh .github/workflows/ci.yml ci-api` |
| `view-logs.sh` | Visualizza log con filtri | `view-logs.sh ci-api` |
| `validate-workflows.sh` | Valida sintassi YAML | `validate-workflows.sh` |
| `health-check.sh` | Verifica ambiente | `health-check.sh` |
| `cleanup.sh` | Pulisci vecchi file | `cleanup.sh 7` |

## 🌐 Log Viewer Web

**Dozzle** fornisce una UI web per visualizzare log in tempo reale:

```
http://localhost:9999
```

Caratteristiche:
- ✅ Log in tempo reale di tutti i container
- ✅ Filtri e ricerca
- ✅ Multi-container view
- ✅ Download log

## 🔍 Troubleshooting

### ❌ "Docker socket not found"

**Soluzione**: Assicurati che Docker Desktop sia in esecuzione.

```bash
# Windows: Controlla Docker Desktop
# Linux: Verifica il socket
ls -la /var/run/docker.sock
```

### ❌ "Connection refused to postgres/redis/qdrant"

**Soluzione**: Attendi che i servizi siano pronti.

```bash
# Controlla lo stato dei servizi
docker compose ps

# Attendi che tutti siano "healthy"
docker compose logs postgres redis qdrant
```

### ❌ "act: command not found"

**Soluzione**: Rebuild del container.

```bash
docker compose build --no-cache act-runner
docker compose up -d
```

### ❌ Log troppo grandi

**Soluzione**: Usa la pulizia automatica.

```bash
# Dentro il container
cleanup.sh 3  # Rimuovi log > 3 giorni
```

### ❌ "OPENROUTER_API_KEY not set"

**Soluzione**: Configura i secrets.

```bash
# Modifica config/.secrets
nano config/.secrets

# Aggiungi:
OPENROUTER_API_KEY=sk-or-v1-your-actual-key
```

## 📊 Esempi Comuni

### Test completo CI prima del push

```bash
# Avvia ambiente
docker compose up -d

# Entra nel container
docker compose exec act-runner bash

# Valida workflow
validate-workflows.sh

# Esegui CI completo
run-workflow.sh .github/workflows/ci.yml

# Visualizza eventuali errori
view-logs.sh ci
```

### Debug di un job specifico

```bash
# Esegui solo il job problematico
run-job.sh .github/workflows/ci.yml ci-api

# Visualizza log dettagliato
view-logs.sh ci-api --full

# Controlla artifacts generati
ls -la /artifacts/
```

### Test di security scan

```bash
# Valida workflow security
validate-workflows.sh

# Esegui scan completo
run-workflow.sh .github/workflows/security-scan.yml

# Visualizza risultati
view-logs.sh security-scan
```

### Test di load testing

```bash
# Esegui load test
run-workflow.sh .github/workflows/load-test.yml workflow_dispatch

# Visualizza risultati k6
ls -la /artifacts/
view-logs.sh load-test
```

## 🔐 Sicurezza

### ⚠️ Mai committare secrets!

Il file `config/.secrets` è gitignored per sicurezza. Non rimuovere mai questa protezione.

### 🔒 Permissions

Gli script sono eseguibili solo dentro il container. Non esporli su reti pubbliche.

### 🛡️ Isolamento

L'ambiente è completamente isolato dal progetto principale:
- Porta PostgreSQL: 5433 (vs 5432)
- Porta Redis: 6380 (vs 6379)
- Porta Qdrant: 6335/6336 (vs 6333/6334)

## 📈 Performance

### Cache

L'ambiente mantiene cache per:
- ✅ NuGet packages (.NET)
- ✅ npm/pnpm packages (Node)
- ✅ Docker layers
- ✅ k6 binary
- ✅ act runner images

**Tempo primo run**: ~10-15 min
**Tempo run successivi**: ~3-5 min

### Ottimizzazione

Per velocizzare ulteriormente:

```bash
# Pre-warm cache
docker compose exec act-runner bash -c "
  cd /workspace/meepleai/apps/api && dotnet restore
  cd /workspace/meepleai/apps/web && pnpm install
"
```

## 🆘 Supporto

### Log system

Tutti i log sono in `/logs` con formato:
```
YYYYMMDD_HHMMSS_workflow-name.log
```

### Health check completo

```bash
docker compose exec act-runner health-check.sh
```

### Reset completo

```bash
# Stop tutto
docker compose down -v

# Pulisci volumi
rm -rf logs/* artifacts/* cache/*

# Rebuild
docker compose build --no-cache
docker compose up -d
```

## 🎯 Best Practices

1. **Valida prima di eseguire**: `validate-workflows.sh` prima di `run-workflow.sh`
2. **Test incrementali**: Testa singoli job con `run-job.sh` prima di workflow completi
3. **Pulisci regolarmente**: `cleanup.sh` settimanalmente
4. **Monitora risorse**: Usa Dozzle per monitorare log in real-time
5. **Usa cache**: Non ricostruire l'immagine se non necessario

## 📚 Riferimenti

- [act Documentation](https://github.com/nektos/act)
- [Docker Compose](https://docs.docker.com/compose/)
- [GitHub Actions](https://docs.github.com/en/actions)
- [Dozzle](https://dozzle.dev/)
- [MeepleAI Documentation](../docs/)
