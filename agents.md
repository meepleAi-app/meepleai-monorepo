# agents.md — Standard Operativo per Agenti (Codex/Claude Code) nel Monorepo MeepleAI / MaapleAI

> Questo file definisce **ruoli, regole, prompt, deliverable e flussi** per agenti di coding (OpenAI Codex, Claude Code, ecc.) che lavorano nel monorepo. È pensato per **team di 1 persona** con automazioni locali (Docker Desktop) e integrazione GitHub Flow.
>
> **Ultima revisione**: 2025-10-04 — Allineato con gli standard attuali del repository (npm, Jest, Playwright)

---

## 0) Executive Summary
- **Obiettivo:** permettere all’agente di analizzare il repo *meepleai-monorepo*, creare/risolvere issue, generare codice (TS/C#), scrivere test, aprire PR (draft), aggiornare documentazione, e mantenere qualità (lint, security, CI).
- **Tecnologie target:** TypeScript/Node per `apps/web`, C#/.NET per `apps/api`, Docker Compose in `infra/`, n8n per automazioni, Qdrant/Postgres/Redis come servizi di base, GitHub Actions per CI.
- **Flusso chiave (single‑dev):** Issue → Branch → Implementazione + Test locali → PR Draft → CI → Code Review (agente) → Merge → Release.

---

## 1) Regole Generali & Convenzioni

### 1.1 Linguaggi & Stile
- **TypeScript:** ESLint + Prettier. Strict mode `"strict": true` in `tsconfig.json`. Preferire funzioni pure e tipi espliciti.
- **C#/.NET:** abilitare Nullable Reference Types, analizzatori, StyleCop. Layering pulito: `Api`, `Application`, `Domain`, `Infrastructure`.
- **Test:** Vitest/Jest (TS) e xUnit (C#). Copertura minima consigliata: 80% su unit.

### 1.2 Naming & Struttura repository (MeepleAI)
- **Radice monorepo**:
  - `/apps/web` — Frontend (Next.js/TS). `package.json`, `package-lock.json`.
  - `/apps/api` — Backend .NET (C#). `MeepleAI.Api.csproj` ([Unverified] nome file se diverso). Soluzione: `MeepleAI.sln` in radice.
  - `/packages` — librerie condivise (TS), es. `@meeple/shared`.
  - `/infra` — Docker Compose, config runtime, seeds.
    - `/infra/docker-compose.yml`
    - `/infra/env/` variabili di esempio (`.env.dev.example` / `.env.ci.example` per web/api/n8n)
  - `/tools` — script locali (PowerShell/Bash), es. `create-issues.ps1`.
  - `/docs` — documentazione e audit.
  - `/.github/workflows` — CI/CD (ci.yml, e2e.yml).
  - `/tests` — E2E/UX (Puppeteer/Playwright) e integrazione API.
- Ogni app espone `README.md` locale con comandi di build/test e template `.env.dev.example`.

> Nota: Se nel repo attuale esistono cartelle come `Api/` o `docker/`, migrare/aliasare verso la struttura sopra o aggiornare i path in `infra/docker-compose.yml`.

### 1.3 Git & Commit
- **Branch naming:** `feature/<scope>-<short-desc>`, `fix/<scope>-<bug>`, `chore/<scope>-...`.
- **Conventional Commits:** `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `perf:`.
- **PR Template:** includere obiettivo, scope, checklist test, note breaking change, screenshot/log.

### 1.4 Qualità & Sicurezza
- Lint, type-check e test devono passare localmente **prima** della PR.
- Secrets: mai committare; usare `.env` solo locale/CI (masked). Ruotare PAT n8n periodicamente.
- Rate limit su API minime: **Redis token bucket** o limitazione per IP/org.
- RLS/tenancy: test E2E obbligatori (utente A non vede B; Editor != Admin; owner vs non-owner su sessions).

---

## 2) Ruoli dell’Agente

### 2.1 Auditor
- Scansiona repo, identifica difetti (build rotta, test mancanti, cicli dipendenze), stende **Report Audit** markdown in `/docs/audit/<date>.md`.

### 2.2 Issue Engineer
- Rileva problemi, apre issue con **riproducibilità**, **ipotesi radice**, **definizione done**, **criteri accettazione**, **impatti**.
- Salva un log parallelo in `/docs/issues-log.csv` (id, titolo, labels, percorso, stato, autore).

### 2.3 Implementer (TS/C#)
- Genera o migliora moduli, preserva API pubbliche stabili, aggiunge test unit/E2E.

### 2.4 Reviewer
- Apre review su PR Draft: note line-by-line, suggerimenti patch, matrice rischi, e **auto-checklist**.

### 2.5 Documentarista
- Aggiorna `README.md`, `agents.md` se introduce nuove prassi, e doc dei moduli.

---

## 3) GitHub Flow Operativo (single-dev)

1. **Crea/Seleziona Issue** (o Audit → Issue). Aggiungi labels (`area`, `type`, `priority`, `tenant`).
2. **Branch:** `feature/<scope>-<desc>` collegata all’issue (`Fixes #ID`).
3. **Implementazione locale:**
   - Sincronizza `main` → rebase.
   - Sviluppa con TDD quando possibile. Mantieni commit piccoli e descrittivi.
4. **Test locali:** esegui unit + e2e (vedi §6). Correggi fino a verde.
5. **PR Draft:** titolo = Issue + breve outcome; include checklist (§5) e output test.
6. **CI:** Lint, build, test, SCA (security). Correggi failing checks.
7. **Review dell’Agente:** commenti, diff suggestions, verifiche.
8. **Merge (squash preferito)** → **Tag/Release** se necessario.

---

## 4) Criteri di Accettazione (per ogni Issue)
- Riproducibilità documentata (steps + dato minimo).
- Copertura test per il codice toccato.
- Lint/type-check zero error.
- Non introduce **secrets** o **RLS leaks**.
- Performance: nessuna regressione (>10%) misurata sui benchmark locali (se presenti).
- Docs aggiornate: README/CHANGELOG se cambia comportamento pubblico.

---

## 5) Checklist PR (copia/incolla nella PR)
- [ ] Issue linkata (`Fixes #...`).
- [ ] Lint + typecheck ok.
- [ ] Unit test verdi (TS/C#) con copertura ≥ 80% sul delta.
- [ ] E2E/UX test passano (puppeteer/playwright per web; http e2e per API).
- [ ] Nessun secret in diff; `.env` aggiornato nei template `.env.dev.example`/`.env.ci.example` se serve.
- [ ] Multi-tenant: test RLS/permessi aggiornati.
- [ ] Performance: no regressioni note.
- [ ] Docs aggiornate.

---

## 6) Testing Standard (MeepleAI)

### 6.1 Unit
- **TS (apps/web):** Jest; mock I/O rete. Script: `npm test`.
- **C# (apps/api):** xUnit; `dotnet test`. Abilita raccolta coverage.

### 6.2 Integration & E2E
- **API (apps/api):** avvia stack via `infra/docker-compose.yml`; test con xUnit + `WebApplicationFactory` o `RestClient`.
- **Web (apps/web):** **Puppeteer** per flussi utente; salva screenshot in `/tests/e2e/__artifacts__`.
- **Data:** semi deterministici; fixture per multi‑tenant.

### 6.3 Qualità continua
- **GitHub Actions**: job separati `ci-web` (Node 20 + npm), `ci-api` (.NET 8), `e2e` (services: postgres/redis/qdrant).

## 7) Come l’Agente usa i file *.md (Codex/Claude Code)

- **agents.md (questo):** sorgente di verità per regole, prompt e definizioni di done. L’agente deve leggerlo prima di agire.
- **README.md per app/modulo:** comandi `dev`, `test`, `build`, env richieste.
- **CONTRIBUTING.md:** standard commit, PR, coding style; se assente l’agente lo crea.
- **SECURITY.md:** come segnalare vulnerabilità e policy di secret.
- **docs/audit/**: report periodici; l’agente li consulta per priorità tecniche.

> Gli agenti DEVONO rispettare le policy di sicurezza e non introdurre dipendenze non approvate senza nota in PR.

---

## 8) Libreria Prompt (copia/incolla)

> **Nota:** mantenere output concisi, con diffs minimi e test. Se non hai certezza su uno step, proponi opzioni e marca con `[Unverified]` eventuali ipotesi.

### 8.1 Prompt — Auditor del Repo
```
Sei un Auditor del monorepo MeepleAI. Obiettivi: 1) elenco problemi build/test/lint; 2) debiti tecnici prioritizzati; 3) suggerimenti concreti.
Output: markdown brevissimo con sezioni: Summary, Findings (priorità P0/P1/P2), Quick Wins (≤ 5), Rischi, Prossimi passi. Indica file/righe.
Mantieni compatibilità TS/C# e docker-compose. Non proporre refactor massivi senza piano incrementale.
```

### 8.2 Prompt — Creazione Issue da Codice
```
Ruolo: Issue Engineer. Analizza il modulo <path> e i relativi test. Se trovi bug, debito o mancanza test, crea 1 issue.
Output issue:
- Titolo (chiaro, azionabile)
- Contesto (repo path, versione)
- Passi per riprodurre (se bug)
- Root cause ipotizzata
- Definizione di Done
- Criteri di Accettazione (test richiesti)
- Impatti (performance, sicurezza, UX)
- Labels suggerite (type:bug|feat|refactor, area:<x>, priority:P1)
Aggiungi al file /docs/issues-log.csv il record dell’issue.
```

### 8.3 Prompt — Implementazione (TS/C#) con Test
```
Ruolo: Implementer. Branch: feature/<scope>. Modifica solo i file necessari. Mantieni API pubbliche. Aggiungi test unit (TS/C#) e, se toccata l’API, test E2E.
Output:
- Diff patch minimale
- Spiegazione (≤ 8 righe) con rischi e rollback plan
- Comandi per test locali
- Note di migrazione (se schema/config cambia)
```

### 8.4 Prompt — PR Draft + Review Automatica
```
Crea PR Draft per Issue #<id>.
Include:
- Riassunto cambi
- Checklist PR (incolla la lista §5 con esito)
- Rischi/failure modes e mitigazioni
- Screenshot/log dei test
Poi esegui una self-review: commenti puntuali (file:linea) e 3 suggerimenti di hardening.
```

### 8.5 Prompt — Documentazione Aggiornata
```
Ruolo: Documentarista. Aggiorna README/CHANGELOG per riflettere i cambi. Mantieni esempi eseguibili, comandi test, variabili d’ambiente.
Output: patch ai md, con sommario delle modifiche.
```

---

## 9) Integrazione con n8n e Automazioni Locali (MeepleAI)
- **Workflow n8n** (in `infra/n8n/flows/`):
  - **/agent/issue-scan** → esegue prompt Auditor, salva issue in GitHub e logga su `/docs/issues-log.csv`.
  - **/agent/fix** → genera patch (TS/C#) e apre PR Draft.
  - **/agent/review** → commenta PR con checklist §5 e hardening tips.
- **Env richieste** (in `infra/env/n8n.env.dev.example` / `.ci.example`): `GITHUB_TOKEN`, `REPO_SLUG`, `OPENROUTER_API_KEY` ([Unverified] se usi OpenRouter), `STACK_BASE_URL`.
- **Esecuzione locale**: `docker compose --profile n8n up -d`.

---

## 10) Standard Tenancy & Dati
- Ogni record ha `tenant_id` obbligatorio. Query **devono** filtrare per `tenant_id`.
- Indici: per `tenant_id`, `game_id`, e chiavi di ricerca testuali.
- Qdrant esterno consigliato per dataset grandi; HNSW `M=32, ef=96` come default pragmatico.

---

## 11) Performance & Costi
- Cache per risposte frequenti (per gioco). Precompute Explain/Setup per ridurre latenza.
- Evitare allucinazioni in Q&A: quando il contesto non copre, rispondere "Not specified" e mostrare snippet.
- Misurare tempo medio request e token usage dove disponibile.

---

## 12) Rischi & Failure Modes (e Mitigazioni)
- **RLS errata / tenant leak:** test E2E obbligatori, revisione query, policy DB.
- **Secrets leakage:** `.env` non committato; variables in CI masked; rotate keys.
- **Rate limit insufficiente:** Redis token bucket; backoff.
- **Timeout workflow n8n:** job asincroni o retry con soglia; notifiche.
- **PDF eterogenei → RuleSpec sporco:** validatori + correzione manuale assistita; parser tabelle (Camelot/Tabula) quando servono.
- **Dataset vettoriale in crescita:** migrare a Qdrant esterno e reindicizzazione pianificata.

---

## 13) Piano di Implementazione (Step-by-step)
1. Allinea repo a struttura §1.2; aggiungi lint/formatter/config TS e C#.
2. Aggiungi GitHub Actions (lint/build/test/e2e/security).
3. Integra `/docs/issues-log.csv` e PR template con checklist §5.
4. Configura n8n con i tre webhook base (scan/fix/review).
5. Aggiungi test E2E Puppeteer/Playwright per flussi critici.
6. Stabilisci policy secrets e template `.env.dev.example`/`.env.ci.example`.

---

## 14) Appendice — Comandi Locali (PowerShell) — monorepo MeepleAI
```powershell
# Posizionati nella root del repo

# Frontend (apps/web)
pushd .\apps\web
npm install
npm run lint && npm run typecheck && npm test
popd

# Backend .NET (apps/api)
pushd .\apps\api
# Verifica che esista Api.csproj; se il path differisce, aggiorna questo agents.md
if (-Not (Test-Path .\src\Api\Api.csproj)) { Write-Error "Api.csproj non trovato in apps/api/src/Api" }
dotnet restore .\src\Api\Api.csproj
dotnet build .\src\Api\Api.csproj -warnaserror
# Test con copertura
 dotnet test .\tests\Api.Tests\Api.Tests.csproj --collect:"XPlat Code Coverage"
popd

# Avvio stack docker (infra)
pushd .\infra
# Costruisci immagini locali e avvia in background
docker compose up -d --build
# Log rapidi
docker compose ps
popd

# E2E web (tests)
pushd .\tests
# Playwright per E2E testing
npm run test:e2e
popd
```

**Nota PowerShell:** se `tools/create-issues.ps1` dà errore tipo "Termine 'if' non riconosciuto", assicurati che il file non contenga BOM/CRLF corrotti e che lo shebang non confonda PowerShell. Esegui con: `pwsh -File .\tools\create-issues.ps1 -DryRun`.

---

## 15) Note per l’Agente (Regole dure)
- Non toccare `/public` o asset generati.
- Non introdurre dipendenze globali non necessarie.
- Ogni modifica **deve** includere test adeguati e aggiornamento doc.
- Se qualcosa non è verificabile, **marca come** `[Unverified]` e proponi 2 alternative.

---

## 16) Template Issue (da copiare)
```
Titolo: <chiaro e breve>
Contesto: path=<apps/...>, versione=<git sha>
Passi per riprodurre (se bug): <1..n>
Root cause ipotizzata: <testo>
Definizione di Done: <criteri>
Criteri di Accettazione: <test descrittivo>
Impatti: <performance|sicurezza|UX>
Labels: type:<bug|feat|refactor>, area:<web|api|infra|docs|tests>, priority:P1
```

### 16.1 File `/docs/issues-log.csv` (intestazione)
```
id,title,labels,area,path,status,author,created_at,linked_pr
```

### 16.2 PR Template `.github/pull_request_template.md`
```
# Descrizione
Cosa è cambiato e perché.

# Checklist
- [ ] Issue linkata (Fixes #...)
- [ ] Lint/typecheck ok (web/api)
- [ ] Test unit/integrazione/e2e verdi
- [ ] Nessun secret in diff; template .env.dev.example/.env.ci.example aggiornati
- [ ] Tenancy e RLS testate
- [ ] No regressioni performance
- [ ] Docs aggiornate

# Rischi / Failure Modes
- ...

# Evidenze test
- <log/screenshot>
```

### 16.3 CODEOWNERS
```
# Default: manutentore
* @DegrassiAaron
/apps/web/ @DegrassiAaron
/apps/api/ @DegrassiAaron
/tests/ @DegrassiAaron
```

## 17) Template PR (da copiare)
```
# Descrizione
Cosa è cambiato e perché.

# Checklist
- [ ] Issue linkata (Fixes #...)
- [ ] Lint/typecheck ok
- [ ] Test unit/integrazione/e2e verdi
- [ ] Secrets assenti; template .env.dev.example/.env.ci.example aggiornati
- [ ] Tenancy testata
- [ ] No regressioni di performance
- [ ] Docs aggiornate

# Rischi / Failure Modes
- ...

# Evidenze test
- <log/screenshot>
```

---

## 18) CI/CD — GitHub Actions per MeepleAI

### 18.1 `.github/workflows/ci.yml`
```yaml
name: ci
on:
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]
  push:
    branches: [main]

jobs:
  ci-web:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: apps/web
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: 20, cache: 'npm' }
      - run: npm ci
      - run: npm run lint && npm run typecheck && npm test -- --ci

  ci-api:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: apps/api
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore
      - run: dotnet build -warnaserror
      - run: dotnet test --collect:"XPlat Code Coverage"

  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: aquasecurity/trivy-action@0.24.0
        with:
          scan-type: fs
          format: table
          exit-code: '0'
```

### 18.2 `.github/workflows/e2e.yml`
```yaml
name: e2e
on: [pull_request]

jobs:
  e2e:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: postgres
        ports: ["5432:5432"]
        options: >-
          --health-cmd="pg_isready -U postgres" --health-interval=10s --health-timeout=5s --health-retries=5
      redis:
        image: redis:7
        ports: ["6379:6379"]
      qdrant:
        image: qdrant/qdrant:latest
        ports: ["6333:6333","6334:6334"]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: 20, cache: 'npm' }
      - run: npm ci
        working-directory: apps/web
      - run: npm run test:e2e
        working-directory: apps/web
```

### 18.3 `infra/docker-compose.yml` (scheletro)
```yaml
version: '3.9'
services:
  web:
    build: ../apps/web
    env_file:
      - ./env/web.env.dev
    depends_on: [api]
    ports: ["3000:3000"]
  api:
    build: ../apps/api
    env_file:
      - ./env/api.env.dev
    depends_on: [postgres, redis]
    ports: ["8080:8080"]
  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: postgres
    ports: ["5432:5432"]
    volumes:
      - pgdata:/var/lib/postgresql/data
  redis:
    image: redis:7
    ports: ["6379:6379"]
  qdrant:
    image: qdrant/qdrant:latest
    ports: ["6333:6333","6334:6334"]
  n8n:
    image: n8nio/n8n:latest
    env_file:
      - ./env/n8n.env.dev
    ports: ["5678:5678"]
volumes:
  pgdata:
```

---

## 19) Aggiornamenti futuri
- Allineare nomi esatti dei progetti .NET (csproj/sln) e dei package TS nel repo.
- Aggiungere step di pubblicazione Docker (buildx + push su GHCR) e release tagging.
- Portare Playwright headless per cross‑browser se Puppeteer non basta.

