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
