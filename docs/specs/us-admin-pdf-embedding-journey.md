# US: Admin PDF Embedding Journey — Full Pipeline Test

## User Story

> Come admin, voglio caricare il PDF di un gioco appena acquistato, monitorare il processo di embedding nella dashboard globale, e testare che l'agente RAG risponda con citazioni dal knowledge base creato.

## Acceptance Criteria (Gherkin)

```gherkin
Feature: Admin testa il pipeline completo PDF → Embedding → RAG Agent

  Background:
    Given l'admin e' autenticato con ruolo "Admin"

  Scenario: 1 — Creazione gioco via wizard BGG
    When l'admin naviga a "/admin/games/new"
    And cerca "Catan" nel campo di ricerca BGG
    And seleziona il risultato "Catan" dalla lista
    And conferma i dettagli del gioco
    Then il gioco "Catan" viene creato nel catalogo condiviso
    And l'admin viene portato allo step di upload PDF

  Scenario: 2 — Upload PDF del regolamento
    Given il gioco "Catan" esiste nel catalogo
    When l'admin carica il file "catan-regolamento.pdf"
    Then il file appare nella lista documenti
    And il documento viene automaticamente accodato per il processing

  Scenario: 3 — Monitoraggio coda embedding nella dashboard globale
    When l'admin naviga a "/admin/knowledge-base/queue"
    Then vede un job con fileName "catan-regolamento.pdf"
    And lo stato del job transita attraverso: Queued → Processing → Completed
    And il contatore documenti processati si aggiorna

  Scenario: 4 — Test agente RAG con citazioni
    Given il documento "catan-regolamento.pdf" e' nello stato "Ready"
    And un agente RAG e' stato creato per "Catan"
    When l'admin apre la pagina di test agente "/admin/games/{id}/agent/test"
    And invia il messaggio "Quanti giocatori possono giocare a Catan?"
    Then riceve una risposta in streaming
    And la risposta contiene informazioni pertinenti
    And appaiono citazioni RAG con riferimenti di pagina (es. "p.15")
```

## Test Strategy

### E2E Mocked (CI — `admin-pdf-embedding-journey.spec.ts`)
- **Auth**: Mock admin session via `page.route()`
- **BGG API**: Mock search results
- **Game creation**: Mock POST → return game ID
- **PDF upload**: Mock upload → return document ID
- **Queue**: Mock list endpoint + SSE stream per transizioni stato
- **Agent**: Mock status (ready) + chat SSE con citazioni RAG
- **Durata**: ~15s

### E2E Integration Smoke (`admin-embedding-flow.spec.ts`)
- **File**: `apps/web/e2e/flows/admin-embedding-flow.spec.ts`
- **Playwright project**: `embedding-flow-local` / `embedding-flow-integration`
- **Richiede**: PostgreSQL + Redis + API + Web + embedding-service + Qdrant + LLM (Ollama)
- **PDF fixture**: `data/rulebook/pandemic_rulebook.pdf` (fallback: `e2e/test-data/sample-rules.pdf`)
- **Timeout**: 300s (5 min) — embedding pipeline puo' richiedere 30-120s
- **Run**: `cd apps/web && npx playwright test --project=embedding-flow-local`
- **Non in CI standard** — run manuale o scheduled

**Acceptance Criteria tracciati nel test** (spec panel review 2026-03-22):
| AC | Criterio | Test step |
|----|----------|-----------|
| AC1 | Upload PDF → document enters processing | T1: Upload PDF fixture |
| AC2 | Job visible in queue with filename/status | T2: Verify job in list |
| AC3 | SSE connection active for real-time updates | T2: Verify SSE indicator |
| AC4 | Processing completes → Ready, chunks > 0 | T2: Wait for completion + chunk preview |
| AC5 | Chat with RAG agent → streaming response | T3: Send question + verify response |
| AC6 | Queue stats bar reflects completed count | T2: Verify stats bar |

## Pages coinvolte

| Step | Page | Componente chiave |
|------|------|-------------------|
| Wizard BGG | `/admin/games/new` | `AdminGameWizard` |
| Upload PDF | Step 3 del wizard | `PdfUploadSection` |
| Queue monitoring | `/admin/knowledge-base/queue` | `QueueDashboardClient` |
| Agent test | `/admin/games/{id}/agent/test` | `AgentTestingPage` |

## API Endpoints da mockare

| Endpoint | Method | Scopo |
|----------|--------|-------|
| `/api/v1/auth/me` | GET | Auth admin |
| `/api/v1/bgg/search?query=Catan` | GET | Ricerca BGG |
| `/api/v1/admin/shared-games` | POST | Crea gioco |
| `/api/v1/ingest/pdf` | POST | Upload PDF |
| `/api/v1/admin/queue/enqueue` | POST | Accoda per processing |
| `/api/v1/admin/queue` | GET | Lista jobs |
| `/api/v1/admin/queue/stream` | GET | SSE real-time updates |
| `/api/v1/agents/{id}/status` | GET | Stato agente |
| `/api/v1/agents/{id}/chat` | POST | Chat SSE con RAG |
