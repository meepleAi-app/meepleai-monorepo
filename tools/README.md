# MeepleAI Development Tools

Strumenti per sviluppatori per il progetto MeepleAI.

## Strumenti Disponibili

### 1. Standardizzazione Markdown

**File**: `standardize-markdown.js`

Standardizza la formattazione dei file Markdown della documentazione:
- Aggiunge tag linguaggio ai code blocks
- Verifica e segnala problemi con heading hierarchy
- Assicura formattazione consistente

**Uso**:

```bash
node tools/standardize-markdown.js
```

**Output**:
```bash
Standardizing Markdown files...

✓ docs\ai-06-rag-evaluation.md: code blocks
✓ docs\code-coverage.md: code blocks
...

✓ Processed 93 files, modified 70
```

### 2. Generazione Documentazione API

**File**: `generate-api-docs.js`

Genera documentazione API da OpenAPI/Swagger spec.

**Prerequisiti**:
- API deve essere in esecuzione in development mode (`http://localhost:8080`)
- Swagger endpoint disponibile a `/swagger/v1/swagger.json`

**Uso**:

```bash
# Avvia l'API in development mode (Terminal 1)
cd apps/api/src/Api
dotnet run

# Genera la documentazione (Terminal 2)
node tools/generate-api-docs.js
```

**Output**:
- `docs/api-reference.json` - OpenAPI spec JSON completo
- `docs/api-reference.md` - Documentazione Markdown generata

**Variabili d'ambiente**:
- `API_BASE_URL` - URL base dell'API (default: `http://localhost:8080`)

**Esempio**:

```bash
API_BASE_URL=http://localhost:8080 node tools/generate-api-docs.js
```

### 3. Ricerca Documentazione

**File**: `search-docs.js`

Strumento CLI per ricercare nella documentazione con ranking e highlighting.

**Uso**:

```bash
node tools/search-docs.js "query" [--limit N]
```

**Esempi**:

```bash
# Cerca "authentication" (default limit: 10)
node tools/search-docs.js "authentication"

# Cerca "RAG pipeline" con limite di 5 risultati
node tools/search-docs.js "RAG pipeline" --limit 5

# Cerca "streaming SSE"
node tools/search-docs.js "streaming SSE"

# Cerca termini multipli
node tools/search-docs.js "API versioning endpoints"
```

**Output**:

```bash
Building documentation index...

Indexed 94 documents

Found 3 results for: "authentication"

================================================================================

1. Authentication & Security
   Path: SECURITY.md
   Score: 19.50

   Snippets:
   1) Line 22:
      ## Authentication Methods

      MeepleAI supports dual authentication:
      ...

--------------------------------------------------------------------------------
```

**Caratteristiche**:
- Ricerca full-text nei file Markdown
- Ranking basato su rilevanza (titolo > heading > contenuto)
- Snippet con contesto (2 righe prima e dopo)
- Supporto per query multi-termine

### 4. Misurazione Coverage

**File**: `measure-coverage.ps1`

Script PowerShell per misurare la code coverage di API e Web.

**Uso**:

```powershell
# Coverage API
pwsh tools/measure-coverage.ps1 -Project api

# Coverage Web
pwsh tools/measure-coverage.ps1 -Project web

# Coverage API con report HTML
pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml

# Coverage completo (API + Web)
pwsh tools/measure-coverage.ps1
```

**Output**:
- Report di coverage in console
- File di coverage in `apps/api/coverage/` e `apps/web/coverage/`
- Report HTML (opzionale) in `apps/{api|web}/coverage/html/`

Vedi `docs/code-coverage.md` per dettagli completi.

### 5. Cleanup Cache

**File**: `cleanup-caches.sh` (bash) / `cleanup-caches.ps1` (PowerShell)

Script per pulire periodicamente le directory di cache accumulate durante lo sviluppo.

**Directory pulite**:
- `.serena/` - Cache Serena MCP
- `codeql-db/` - Database CodeQL
- `.playwright-mcp/` - Cache Playwright MCP
- `apps/api/obj`, `apps/api/bin` - Build artifacts .NET (opzionale)
- `apps/web/.next` - Build cache Next.js (opzionale)

**Uso (Linux/Mac)**:

```bash
# Dry run (mostra cosa verrebbe eliminato)
bash tools/cleanup-caches.sh --dry-run

# Esegui pulizia con conferma
bash tools/cleanup-caches.sh

# Esegui senza conferma
bash tools/cleanup-caches.sh --yes

# Pulisci solo cache (salta build artifacts)
bash tools/cleanup-caches.sh --skip-build

# Output dettagliato
bash tools/cleanup-caches.sh --verbose
```

**Uso (Windows PowerShell)**:

```powershell
# Dry run (mostra cosa verrebbe eliminato)
pwsh tools/cleanup-caches.ps1 -DryRun

# Esegui pulizia con conferma
pwsh tools/cleanup-caches.ps1

# Esegui senza conferma
pwsh tools/cleanup-caches.ps1 -Yes

# Pulisci solo cache (salta build artifacts)
pwsh tools/cleanup-caches.ps1 -SkipBuild

# Output dettagliato
pwsh tools/cleanup-caches.ps1 -Verbose
```

**Output**:
```
╔════════════════════════════════════════════╗
║   MeepleAI Cache Cleanup                   ║
╚════════════════════════════════════════════╝

📊 Calculating current sizes...
📁 Target directories:
  - .serena
  - codeql-db
  - .playwright-mcp
  - apps/api build artifacts
  - apps/web/.next

🧹 Cleaning cache directories...
Deleting: .serena (45.2 MB) - Serena MCP cache
Deleting: codeql-db (62.8 MB) - CodeQL database cache
...

╔════════════════════════════════════════════╗
║   Cleanup Summary                          ║
╚════════════════════════════════════════════╝

✅ Cleanup complete!
  Space freed: 120.5 MB (0.12 GB)
```

**Quando usarlo**:
- Mensilmente per manutenzione ordinaria
- Quando il disco si riempie
- Prima di operazioni git pesanti (pull, merge di branch grandi)
- Dopo aggiornamenti di dipendenze o tooling

**Note Tecniche**:
- Lo script usa `set -e` per interrompersi in caso di errore
- La funzione `clean_directory` ritorna `0` anche quando una directory non esiste, per prevenire uscite premature
- Gli sviluppatori tipicamente avranno solo un subset delle cache, quindi il comportamento di skip è normale
- Il calcolo `du` può impiegare tempo su repository grandi

### 6. Gestione Processi Test

**File**: `cleanup-test-processes.ps1`

Script PowerShell per terminare processi di test rimasti in esecuzione.

**Uso**:

```powershell
# Dry run (mostra processi senza terminarli)
pwsh tools/cleanup-test-processes.ps1 -DryRun -Verbose

# Termina processi di test
pwsh tools/cleanup-test-processes.ps1 -Verbose
```

**Note**:
- Eseguito automaticamente da `apps/web/package.json` dopo i test
- Solo Windows (richiede PowerShell)
- Terminazione sicura di processi node/jest rimasti attivi

### 7. Gestione Qdrant

**File**: `delete-qdrant-collection.ps1`

Elimina collection dal database vettoriale Qdrant.

**Uso**:

```powershell
# Elimina collection specifica
pwsh tools/delete-qdrant-collection.ps1 -CollectionName "meepleai_vectors"
```

**Prerequisiti**: Qdrant deve essere in esecuzione (`docker compose up qdrant`)

### 8. Configurazione n8n

**File**: `register-n8n-webhook.ps1`, `setup-n8n-service-account.ps1`

Configurazione e registrazione webhook per n8n workflow automation.

**Uso**:

```powershell
# Setup account servizio n8n
pwsh tools/setup-n8n-service-account.ps1

# Registra webhook
pwsh tools/register-n8n-webhook.ps1 -WebhookUrl "http://localhost:5678/webhook/xxx"
```

### 9. Configurazione Ollama

**File**: `setup-ollama.ps1`

Configura Ollama per modelli LLM locali.

**Uso**:

```powershell
pwsh tools/setup-ollama.ps1
```

**Output**: Scarica e configura modelli LLM per sviluppo locale

### 10. Generazione Issue Admin Console

**File**: `create-admin-console-issues.{js,ps1,sh}`

Genera issue GitHub per features Admin Console.

**Uso**:

```bash
# Bash (Linux/Mac/WSL)
./tools/create-admin-console-issues.sh [--dry-run]

# PowerShell (Windows)
pwsh tools/create-admin-console-issues.ps1 [-DryRun]

# Node.js (cross-platform)
node tools/create-admin-console-issues.js [--dry-run]
```

Vedi `tools/README-admin-console-issues.md` per documentazione completa.

### 11. Setup GitHub Labels

**File**: `setup-github-labels.sh`

Crea label e milestone GitHub per il progetto.

**Uso**:

```bash
bash tools/setup-github-labels.sh
```

**Prerequisiti**: GitHub CLI (`gh`) installato e autenticato

### 12. Analisi Complessità Codice

**File**: `analyze-complexity.ps1`

Analizza complessità del codebase per file più grandi.

**Uso**:

```powershell
# Analizza file C# (top 20)
pwsh tools/analyze-complexity.ps1 -Path apps/api -Pattern "*.cs" -Top 20

# Analizza file TypeScript
pwsh tools/analyze-complexity.ps1 -Path apps/web -Pattern "*.ts,*.tsx" -Top 15
```

**Output**: Tabella ordinata per numero di righe

**Quando usarlo**: Analisi trimestrale complessità, identificazione candidati refactoring

### 13. Validazione Documentazione

**File**: `validate-docs.ps1`

Valida integrità e consistenza della documentazione.

**Uso**:

```powershell
pwsh tools/validate-docs.ps1
```

### 14. Tracking Coverage Trends

**File**: `coverage-trends.{sh,ps1}`

Traccia trend di code coverage nel tempo.

**Uso**:

```bash
# Bash (Linux/Mac)
bash tools/coverage-trends.sh

# PowerShell (Windows)
pwsh tools/coverage-trends.ps1
```

**Output**: Appende metriche a `coverage-history.json`

### 15. Migrazione Repository

**File**: `migrate-to-private.ps1`

Migra repository da public a private (o viceversa).

**Uso**:

```powershell
pwsh tools/migrate-to-private.ps1
```

**Warning**: Richiede permessi admin sul repository

### 16. Dual VS Code Launcher

**File**: `open-dual-vscode.{ps1,sh}`

Apre due istanze VS Code per sviluppo API + Web.

**Uso**:

```bash
# Bash
bash tools/open-dual-vscode.sh

# PowerShell
pwsh tools/open-dual-vscode.ps1
```

## Sviluppo

### Aggiungere Nuovi Strumenti

1. Crea un nuovo file JavaScript o PowerShell in `tools/`
2. Aggiungi header di documentazione con:
   - Descrizione
   - Prerequisiti
   - Uso
   - Output
3. Rendi eseguibile (Linux/Mac): `chmod +x tools/your-script.js`
4. Aggiungi shebang per script Node.js: `#!/usr/bin/env node`
5. Documenta in questo README

### Best Practices

- **Usare solo moduli built-in di Node.js** quando possibile (no dipendenze esterne)
- **Output chiaro**: usa emoji o simboli per successo (✓) ed errori (✗)
- **Error handling**: gestisci errori comuni con messaggi utili
- **Aiuto**: supporta `--help` flag per mostrare uso
- **Variabili d'ambiente**: usa env vars per configurazione (con default sensati)

## Troubleshooting

### Script Node.js non si avvia

**Problema**: `node: command not found`

**Soluzione**: Installa Node.js 20+ da https://nodejs.org/

### Errore permessi (Linux/Mac)

**Problema**: `Permission denied`

**Soluzione**:

```bash
chmod +x tools/your-script.js
```

### API non raggiungibile (generate-api-docs.js)

**Problema**: `ECONNREFUSED`

**Soluzione**: Assicurati che l'API sia in esecuzione:

```bash
cd apps/api/src/Api
dotnet run
```

Verifica che l'API risponda:

```bash
curl http://localhost:8080/health
```

## Riferimenti

- **Documentazione principale**: `../docs/README.md`
- **Guida sviluppatori**: `../CLAUDE.md`
- **Code coverage**: `../docs/code-coverage.md`
- **OpenAPI/Swagger**: Disponibile a `/api/docs` in development mode

### 17. GitHub Issues Strategic Triage

**File**: `run-issue-triage.sh` (master script)

Sistema completo per analisi strategica e pulizia issue GitHub con decisioni data-driven.

**Componenti**:
- `run-issue-triage.sh` - Script master (esegue tutti gli step)
- `cleanup-duplicate-issues.sh` - Chiude issue duplicate
- `assign-infrastructure-milestones.sh` - Assegna milestone a 11 issue
- `phase-2-issue-labels.sh` - Assicura label consistency

**Documentazione**:
- `EXECUTIVE_SUMMARY_ISSUE_TRIAGE.md` (316 righe) - Overview esecutivo
- `issue-triage-analysis.md` (262 righe) - Analisi dettagliata
- `TRIAGE_QUICKREF.md` (128 righe) - Quick reference
- `triage-summary.txt` - Summary visuale ASCII

**Uso Quick Start**:

```bash
# Esegui triage completo (5 minuti, interattivo)
cd D:/Repositories/meepleai-monorepo-backend
bash tools/run-issue-triage.sh
```

**Cosa fa**:
1. Crea milestone (Month 3, 4, 6, Phase 2)
2. Chiude #709 come duplicate di #706
3. Assegna 11 issue infrastructure a milestone
4. Assicura label `deferred` e `priority-low` su issue Phase 2
5. Mostra metriche before/after

**Risultati Attesi**:

| Metric | Before | After |
|--------|--------|-------|
| Issues senza milestone | 19 | 0 |
| Issue duplicate | 1 | 0 |
| Milestone assignments | 0 | 11 |

**Analisi Strategiche**:
- **Admin Console**: 49 issue (330h, 8.2 settimane) - Defer to Phase 2
- **Infrastructure**: 11 issue (92h) - Schedule Month 3-6 + Phase 2
- **Timeline Impact**: Risparmio 8.2 settimane in Phase 1 (50%)

**Verification**:

```bash
# Verifica 0 issue senza milestone
gh issue list --search "is:open no:milestone"

# Verifica issue deferred
gh issue list --search "label:deferred is:open"

# Verifica Phase 2 work
gh issue list --search "milestone:'Phase 2' is:open"
```

**Quando usarlo**:
- Strategic planning session (trimestrale)
- Issue accumulation >50 senza milestone
- Pre-planning Phase 2
- Repository cleanup prima di milestone review

**Prerequisiti**: GitHub CLI (`gh`) installato e autenticato

**Documentazione Completa**: Vedi `EXECUTIVE_SUMMARY_ISSUE_TRIAGE.md`
