# Integration Mode Improvements (P1–P5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migliorare la modalità "integration" (locale + servizi remoti via SSH tunnel) correggendo 5 gap identificati dal panel di specifica.

**Architecture:** Gli script bash esistenti (`integration-tunnel.sh`, `integration-start.sh`) vengono aggiornati per usare i tunnel locali per i servizi AI (invece di HTTPS pubblico), aggiungere la configurazione Ollama, e un health check pre-avvio. Si aggiunge `appsettings.Integration.json` per i valori statici e si documenta il requisito Git Bash in CLAUDE.md.

**Tech Stack:** Bash, .NET 9 (appsettings.json overrides via env vars), GNU Make

---

## File Map

| File | Azione | Responsabilità |
|------|--------|----------------|
| `CLAUDE.md` | Modify | P1: Aggiungere nota Git Bash su Windows |
| `infra/scripts/integration-start.sh` | Modify | P2+P4: Ollama config + AI services via tunnel |
| `infra/scripts/integration-check.sh` | Create | P3: Health check prerequisiti |
| `infra/Makefile` | Modify | P3: Aggiungere target `integration-check` |
| `apps/api/src/Api/appsettings.Integration.json` | Create | P5: Overrides statici per environment Integration |

---

## Task 1: P1 — Documentare il requisito Git Bash in CLAUDE.md

**Files:**
- Modify: `CLAUDE.md` (sezione Integration/Quick Reference)

- [ ] **Step 1: Aggiungere nota Git Bash nella tabella Quick Reference**

  Trovare la riga `Start Integration` nella tabella Quick Reference e aggiungere la nota Windows:

  ```markdown
  | Start Integration | `make tunnel && make integration` | `infra/` — **Windows: usa Git Bash, non PowerShell** |
  ```

- [ ] **Step 2: Aggiungere sezione prerequisiti nella voce Integration**

  Trovare la sezione "Start Integration" e aggiungere sotto l'intestazione `### Alpha Mode` o in una nuova sottosezione `### Integration Mode (Prerequisiti Windows)`:

  ```markdown
  ### Integration Mode (Windows)

  Gli script di integration usano bash e tool Unix (`lsof`, `ssh`). Su Windows devono essere eseguiti in **Git Bash** (non PowerShell, non CMD).

  ```bash
  # Apri Git Bash (non PowerShell)
  cd infra
  make tunnel          # Apre SSH tunnel verso staging
  make integration     # Avvia API + Web localmente
  ```

  **Prerequisiti**:
  - Git Bash installato (incluso in Git for Windows)
  - SSH key presente in `~/.ssh/meepleai-staging`
  - Staging server attivo
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add CLAUDE.md
  git commit -m "docs(integration): document Git Bash requirement on Windows"
  ```

---

## Task 2: P4 — AI services via tunnel (non HTTPS pubblico)

**Files:**
- Modify: `infra/scripts/integration-start.sh` (funzione `start_api`, righe ~99-108)

**Contesto:** Il tunnel apre `localhost:18000→staging:8000` (embedding) e `localhost:18003→staging:8003` (reranker). Attualmente `integration-start.sh` bypassa il tunnel usando `https://meepleai.app/services/...`. Questo task corregge l'inconsistenza.

- [ ] **Step 1: Aggiornare le variabili embedding e reranker in `start_api()`**

  Trovare e sostituire nel blocco `start_api()`:

  ```bash
  # PRIMA (righe ~104-107):
  export LOCAL_EMBEDDING_URL=https://meepleai.app/services/embedding
  export Embedding__LocalServiceUrl=https://meepleai.app/services/embedding
  export RERANKER_URL=https://meepleai.app/services/reranker
  ```

  ```bash
  # DOPO:
  export LOCAL_EMBEDDING_URL=http://localhost:18000
  export Embedding__LocalServiceUrl=http://localhost:18000
  export RERANKER_URL=http://localhost:18003
  ```

  > **Nota:** i servizi AI sono già tunnelati. Usare localhost evita dipendenza da internet e latenza aggiuntiva.

- [ ] **Step 2: Verificare che Embedding__FallbackEnabled resti false**

  Assicurarsi che la riga seguente sia ancora presente nel blocco (non deve essere rimossa):
  ```bash
  export EMBEDDING_FALLBACK_ENABLED=false
  export Embedding__FallbackEnabled=false
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add infra/scripts/integration-start.sh
  git commit -m "fix(integration): use tunnel ports for embedding/reranker instead of public HTTPS"
  ```

---

## Task 3: P2 — Configurare Ollama LLM via tunnel

**Files:**
- Modify: `infra/scripts/integration-start.sh` (funzione `start_api()`, dopo il blocco embedding)

**Contesto:** Il tunnel apre `localhost:21434→staging:11434` (Ollama). La config `appsettings.json` ha:
- `OllamaUrl: "http://localhost:11434"` (root level, riga 41)
- `Embedding.OllamaUrl: "http://localhost:11434"` (riga 63)
- `AI.Providers.Ollama.BaseUrl: "http://localhost:11434"` (riga 292)

Tutti puntano alla porta 11434 di default ma il tunnel mappa su **21434**. Bisogna sovrascrivere via env var.

- [ ] **Step 1: Aggiungere blocco Ollama/LLM in `start_api()`**

  Aggiungere subito dopo la sezione RERANKER_URL (dopo riga ~107):

  ```bash
  # LLM - Ollama via SSH tunnel (staging:11434 → localhost:21434)
  export OllamaUrl=http://localhost:21434
  export Embedding__OllamaUrl=http://localhost:21434
  export AI__Providers__Ollama__BaseUrl=http://localhost:21434
  ```

  > Le variabili .NET con `__` corrispondono a `AI.Providers.Ollama.BaseUrl` nel JSON gerarchico.

- [ ] **Step 2: Verificare la sintassi del file con bash**

  ```bash
  bash -n infra/scripts/integration-start.sh
  ```

  Expected: nessun output (nessun errore di sintassi).

- [ ] **Step 3: Commit**

  ```bash
  git add infra/scripts/integration-start.sh
  git commit -m "feat(integration): configure Ollama LLM via SSH tunnel port 21434"
  ```

---

## Task 4: P3 — Script health check + target Makefile

**Files:**
- Create: `infra/scripts/integration-check.sh`
- Modify: `infra/Makefile` (aggiungere target `integration-check`)

**Contesto:** Prima di fare `make integration`, lo sviluppatore deve verificare: SSH key presente, staging raggiungibile, tunnel attivo, porte DB/Redis/AI accessibili.

- [ ] **Step 1: Creare `infra/scripts/integration-check.sh`**

  ```bash
  #!/usr/bin/env bash
  # Integration Check — verifica prerequisiti prima di make integration
  #
  # Usage:
  #   bash infra/scripts/integration-check.sh

  set -e

  SSH_KEY="${HOME}/.ssh/meepleai-staging"
  STAGING_HOST="deploy@204.168.135.69"
  CONTROL_SOCKET="${HOME}/.ssh/meepleai-tunnel.sock"

  PASS=0
  FAIL=0

  check() {
      local label="$1"
      local ok="$2"
      if [ "$ok" = "true" ]; then
          echo "  ✓ $label"
          PASS=$((PASS + 1))
      else
          echo "  ✗ $label"
          FAIL=$((FAIL + 1))
      fi
  }

  echo ""
  echo "=== MeepleAI Integration Mode — Pre-flight Check ==="
  echo ""

  # 1. SSH key
  echo "[ SSH ]"
  if [ -f "$SSH_KEY" ]; then
      check "SSH key presente: $SSH_KEY" "true"
  else
      check "SSH key presente: $SSH_KEY" "false"
      echo "    → Esegui: ssh-keygen -t ed25519 -f $SSH_KEY"
  fi

  # 2. Staging raggiungibile
  echo ""
  echo "[ Staging ]"
  if ssh -i "$SSH_KEY" -o ConnectTimeout=5 -o BatchMode=yes "$STAGING_HOST" "echo ok" &>/dev/null; then
      check "Staging raggiungibile ($STAGING_HOST)" "true"
  else
      check "Staging raggiungibile ($STAGING_HOST)" "false"
      echo "    → Verifica che il server staging sia up e la chiave SSH sia autorizzata"
  fi

  # 3. Tunnel attivo
  echo ""
  echo "[ Tunnel SSH ]"
  if ssh -O check -S "$CONTROL_SOCKET" "$STAGING_HOST" &>/dev/null; then
      check "Tunnel SSH attivo" "true"
  else
      check "Tunnel SSH attivo (non attivo)" "false"
      echo "    → Esegui: make tunnel"
  fi

  # 4. Porte tunnelate
  echo ""
  echo "[ Porte Locali ]"
  check_port() {
      local label="$1"
      local host="$2"
      local port="$3"
      if nc -z -w2 "$host" "$port" &>/dev/null 2>&1; then
          check "$label ($host:$port)" "true"
      else
          check "$label ($host:$port) — non raggiungibile" "false"
      fi
  }

  check_port "PostgreSQL" "localhost" 25432
  check_port "Redis"      "localhost" 26379
  check_port "Embedding"  "localhost" 18000
  check_port "Reranker"   "localhost" 18003
  check_port "Ollama"     "localhost" 21434

  # 5. Tool locali
  echo ""
  echo "[ Tool Locali ]"
  if command -v dotnet &>/dev/null; then
      check "dotnet installato ($(dotnet --version))" "true"
  else
      check "dotnet installato" "false"
      echo "    → Installa .NET 9 SDK: https://dotnet.microsoft.com/download"
  fi

  if command -v pnpm &>/dev/null; then
      check "pnpm installato ($(pnpm --version))" "true"
  else
      check "pnpm installato" "false"
      echo "    → Installa: npm install -g pnpm"
  fi

  # Riepilogo
  echo ""
  echo "================================"
  echo "  Passed: $PASS | Failed: $FAIL"
  echo "================================"
  echo ""

  if [ $FAIL -gt 0 ]; then
      echo "⚠  Risolvi i problemi sopra prima di eseguire 'make integration'"
      exit 1
  else
      echo "✓  Tutto OK. Puoi eseguire 'make integration'"
      exit 0
  fi
  ```

- [ ] **Step 2: Rendere eseguibile lo script**

  ```bash
  chmod +x infra/scripts/integration-check.sh
  ```

- [ ] **Step 3: Aggiungere target `integration-check` nel Makefile**

  Nel file `infra/Makefile`, trovare il blocco dei target integration (riga ~27-33) e aggiungere subito prima di `integration:`:

  ```makefile
  integration-check: ## Check prerequisites before make integration
  	bash scripts/integration-check.sh
  ```

  Aggiungere anche `integration-check` alla riga `.PHONY` (riga ~195):
  ```makefile
  .PHONY: ... integration-check ...
  ```

- [ ] **Step 4: Testare lo script (tunnel deve essere attivo o non attivo — entrambi i casi devono produrre output leggibile)**

  ```bash
  cd infra
  bash scripts/integration-check.sh
  ```

  Expected output (con tunnel non attivo):
  ```
  === MeepleAI Integration Mode — Pre-flight Check ===

  [ SSH ]
    ✓ SSH key presente: /c/Users/<user>/.ssh/meepleai-staging

  [ Staging ]
    ✓ Staging raggiungibile (deploy@204.168.135.69)

  [ Tunnel SSH ]
    ✗ Tunnel SSH attivo (non attivo)
      → Esegui: make tunnel

  [ Porte Locali ]
    ✗ PostgreSQL (localhost:25432) — non raggiungibile
    ...

  ================================
    Passed: 2 | Failed: 6
  ================================
  ⚠  Risolvi i problemi sopra prima di eseguire 'make integration'
  ```

- [ ] **Step 5: Commit**

  ```bash
  git add infra/scripts/integration-check.sh infra/Makefile
  git commit -m "feat(integration): add integration-check health check script"
  ```

---

## Task 5: P5 — Creare `appsettings.Integration.json`

**Files:**
- Create: `apps/api/src/Api/appsettings.Integration.json`

**Contesto:** Con `ASPNETCORE_ENVIRONMENT=Integration`, .NET carica `appsettings.json` + `appsettings.Integration.json`. Attualmente il secondo non esiste. Questo file centralizza gli override statici (URL servizi tunnel), riducendo la dipendenza dagli env vars per i valori noti.

I valori dinamici (credenziali DB/Redis) restano negli env vars perché risolti a runtime via SSH.

- [ ] **Step 1: Creare `apps/api/src/Api/appsettings.Integration.json`**

  ```json
  {
    "$comment": "Integration environment: local API + Web against staging services via SSH tunnel. See infra/scripts/integration-tunnel.sh for port mappings.",
    "OllamaUrl": "http://localhost:21434",
    "Embedding": {
      "OllamaUrl": "http://localhost:21434",
      "LocalServiceUrl": "http://localhost:18000",
      "Provider": "External",
      "FallbackEnabled": false
    },
    "Reranking": {
      "BaseUrl": "http://localhost:18003"
    },
    "AI": {
      "Providers": {
        "Ollama": {
          "BaseUrl": "http://localhost:21434"
        }
      }
    },
    "ResilientRetrieval": {
      "EnableReranking": true
    }
  }
  ```

  > **Nota:** Le credenziali DB/Redis e i valori dinamici non vanno qui — restano negli env vars di `integration-start.sh` perché dipendono dallo stato del server staging.

- [ ] **Step 2: Verificare che il file sia valido JSON**

  ```bash
  python3 -c "import json,sys; json.load(open('apps/api/src/Api/appsettings.Integration.json'))" && echo "JSON valido"
  ```

  Expected: `JSON valido`

- [ ] **Step 3: Rimuovere le variabili ora coperte da `appsettings.Integration.json` da `integration-start.sh`**

  Nel file `infra/scripts/integration-start.sh`, le seguenti righe sono ora ridondanti (il file JSON le copre). Rimuoverle dal blocco `start_api()`:

  ```bash
  # RIMUOVERE queste (ora in appsettings.Integration.json):
  export EMBEDDING_PROVIDER=external
  export Embedding__Provider=External
  export EMBEDDING_MODEL=intfloat/multilingual-e5-base
  export EMBEDDING_DIMENSIONS=768
  export Embedding__Dimensions=768
  export LOCAL_EMBEDDING_URL=http://localhost:18000        # già in JSON
  export Embedding__LocalServiceUrl=http://localhost:18000 # già in JSON
  export RERANKER_URL=http://localhost:18003               # già in JSON
  export EMBEDDING_FALLBACK_ENABLED=false
  export Embedding__FallbackEnabled=false
  export OllamaUrl=http://localhost:21434                  # già in JSON
  export Embedding__OllamaUrl=http://localhost:21434       # già in JSON
  export AI__Providers__Ollama__BaseUrl=http://localhost:21434 # già in JSON
  ```

  **Mantenere** le seguenti (valori dinamici o non coperti dal JSON):
  ```bash
  export ASPNETCORE_ENVIRONMENT=Integration
  export ASPNETCORE_URLS="http://+:8080"
  export POSTGRES_HOST=localhost
  export POSTGRES_PORT=25432
  export POSTGRES_USER="$STAGING_POSTGRES_USER"
  export POSTGRES_DB="$STAGING_POSTGRES_DB"
  export POSTGRES_PASSWORD="$STAGING_POSTGRES_PASSWORD"
  export POSTGRES_SSL_MODE=Disable
  export REDIS_HOST=localhost
  export REDIS_PORT=26379
  export REDIS_PASSWORD="$STAGING_REDIS_PASSWORD"
  export SkipMigrations=true
  export ConnectionStrings__Postgres="..."
  ```

  > **Perché:** `ASPNETCORE_ENVIRONMENT=Integration` fa caricare automaticamente `appsettings.Integration.json`. Gli env vars hanno priorità sul JSON, ma è più pulito non duplicarli.

- [ ] **Step 4: Verificare sintassi integration-start.sh dopo la pulizia**

  ```bash
  bash -n infra/scripts/integration-start.sh
  ```

  Expected: nessun output.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/api/src/Api/appsettings.Integration.json infra/scripts/integration-start.sh
  git commit -m "feat(integration): add appsettings.Integration.json and clean up redundant env vars"
  ```

---

## Self-Review

### Copertura spec (P1-P5)

| Gap | Task | Coperto? |
|-----|------|----------|
| P1: Git Bash su Windows non documentato | Task 1 | ✅ |
| P2: Ollama LLM non configurato | Task 3 | ✅ |
| P3: Nessun health check pre-avvio | Task 4 | ✅ |
| P4: AI services via HTTPS invece di tunnel | Task 2 | ✅ |
| P5: `appsettings.Integration.json` mancante | Task 5 | ✅ |

### Ordine di esecuzione corretto

Task 2 (P4) deve essere eseguito **prima** di Task 5 (P5) perché Task 5 rimuove le variabili introdotte in Task 2. In Task 5, Step 3, la lista delle variabili da rimuovere include `LOCAL_EMBEDDING_URL=http://localhost:18000` che è il valore aggiornato da Task 2.

### Placeholder scan

Nessun TODO/TBD/placeholder nei passi. Tutti i blocchi di codice sono completi.

### Type consistency

- `Embedding__LocalServiceUrl` usato coerentemente in Task 2 e Task 5
- `AI__Providers__Ollama__BaseUrl` coerente con struttura JSON in Task 3 e Task 5
- Port 18000 (embedding) e 18003 (reranker) consistenti con `integration-tunnel.sh`
- Port 21434 (Ollama) consistente con `integration-tunnel.sh` (riga 95: `-L 21434:localhost:11434`)

---

*Salvato: `docs/superpowers/plans/2026-04-01-integration-mode-improvements.md`*
