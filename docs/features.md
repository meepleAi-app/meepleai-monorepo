# MeepleAI - Features Documentation

> **Target Audience**: Frontend Development Team
> **Last Updated**: 2025-10-14
> **Version**: 1.0.0

## Table of Contents

1. [Overview](#overview)
2. [Current Features](#current-features)
3. [Planned Features](#planned-features)
4. [Technical Architecture](#technical-architecture)
5. [API Integration Guide](#api-integration-guide)

---

## Overview

**MeepleAI** è un assistente AI per regole di giochi da tavolo che utilizza:
- **RAG (Retrieval-Augmented Generation)** per risposte contestuali basate su PDF caricati
- **Vector Search** con embeddings semantici (Qdrant)
- **Multi-agent system** con agenti specializzati per tipo di query
- **Session-based chat** con persistenza della cronologia

### Stack Tecnologico Frontend
- **Framework**: Next.js 14 (Pages Router)
- **Linguaggio**: TypeScript (strict mode)
- **Testing**: Jest (90% coverage) + Playwright E2E
- **API Client**: Custom `@/lib/api` con gestione sessioni cookie-based
- **Styling**: Inline styles (da migrare a Tailwind/CSS Modules in futuro)

---

## Current Features

### 1. Authentication & User Management

#### 🔐 **Autenticazione Cookie-Based**
**Endpoint**: `/auth/*`
**Status**: ✅ Implementato

**Feature Chiave**:
- Registrazione con ruoli (`Admin`, `Editor`, `User`)
- Login/Logout con sessioni persistenti (cookie HttpOnly)
- Session validation automatica con `X-Correlation-Id` per log tracking
- Auto-redirect su 401 (gestito da `api.ts:102`)

**Frontend Pages**:
- `pages/index.tsx` - Form registrazione/login

**API Endpoints**:
- `POST /auth/register` - Registrazione nuovo utente
- `POST /auth/login` - Login
- `POST /auth/logout` - Logout
- `GET /auth/me` - Ottiene utente corrente

**Ruoli Utente**:
| Ruolo | Permessi |
|-------|----------|
| `Admin` | Full access: gestione giochi, PDF, RuleSpec, admin dashboard, n8n config |
| `Editor` | Gestione giochi, PDF, RuleSpec (no admin dashboard) |
| `User` | Read-only: chat, visualizzazione giochi |

**Rate Limiting** (per ruolo):
- Admin: 100 req/min
- Editor: 60 req/min
- User: 30 req/min
- Anonymous (IP-based): 10 req/min

**Response Headers**:
```json
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
Retry-After: 60 (se rate limited)
X-Correlation-Id: <request-trace-id>
```

---

### 2. Chat Interface (UI-01: COMPLETATO)

#### 💬 **Chat Multi-Sessione con RAG**
**Endpoint**: `/chats/*`, `/agents/qa`
**Status**: ✅ Implementato

**Feature Chiave**:
- Chat multipla per gioco/agente
- Sidebar con lista chat e selezione gioco/agente
- Cronologia persistente (salvataggio automatico nel DB)
- Feedback thumbs-up/thumbs-down per ogni risposta AI
- Snippet con citazioni da PDF (pagina e testo)

**Frontend Pages**:
- `pages/chat.tsx` (935 righe) - UI completa

**API Endpoints**:
- `POST /chats` - Crea nuova chat
- `GET /chats` - Lista chat utente (filtro per gameId opzionale)
- `GET /chats/{chatId}` - Ottiene chat con cronologia messaggi
- `DELETE /chats/{chatId}` - Elimina chat
- `GET /games/{gameId}/agents` - Lista agenti disponibili per gioco
- `POST /agents/qa` - Invia domanda all'agente QA (con `chatId` opzionale)
- `POST /agents/feedback` - Invia feedback su risposta (helpful/not-helpful)

**Chat Workflow**:
```
1. Seleziona gioco → carica agenti disponibili
2. Seleziona agente → carica chat esistenti
3. Crea nuova chat o seleziona esistente
4. Invia messaggio → risposta RAG con snippet
5. Feedback opzionale su risposta
```

**Chat Message Format**:
```typescript
type Message = {
  id: string;
  role: "user" | "assistant";
  content: string;
  snippets?: Snippet[];
  feedback?: "helpful" | "not-helpful" | null;
  endpoint: string;
  gameId: string;
  timestamp: Date;
  backendMessageId?: string; // per feedback
};

type Snippet = {
  text: string;
  source: string;
  page?: number;
  line?: number;
};
```

**Features UX**:
- Sidebar collapsabile
- Auto-creazione chat al primo messaggio se nessuna attiva
- Loading states per ogni operazione
- Error handling con messaggi user-friendly
- Timestamp formattati

---

### 3. PDF Upload & Processing (Wizard Completo)

#### 📄 **Wizard Import PDF → RuleSpec**
**Endpoint**: `/ingest/pdf`, `/games`
**Status**: ✅ Implementato

**Feature Chiave**:
- Wizard in 4 step: Upload → Parse → Review → Publish
- Creazione/selezione gioco con conferma
- Upload PDF con validazione mime-type
- Polling automatico dello status processing (ogni 2s)
- Editor inline RuleSpec JSON prima della pubblicazione
- Lista PDF caricati con retry parsing

**Frontend Pages**:
- `pages/upload.tsx` (1185 righe)

**API Endpoints**:
- `GET /games` - Lista giochi
- `POST /games` - Crea nuovo gioco (Admin/Editor)
- `POST /ingest/pdf` - Upload PDF (Admin/Editor)
- `GET /games/{gameId}/pdfs` - Lista PDF per gioco
- `GET /pdfs/{pdfId}/text` - Ottiene testo estratto + status
- `GET /games/{gameId}/rulespec` - Ottiene RuleSpec generato
- `PUT /games/{gameId}/rulespec` - Pubblica RuleSpec (Admin/Editor)

**Processing Pipeline**:
```
1. Upload PDF → backend salva file + crea record DB
2. Background extraction (PDF → text con Docnet.Core)
3. Status polling → [pending|processing|completed|failed]
4. Fetch RuleSpec generato (lista RuleAtom)
5. Review/Edit → modifica JSON manualmente
6. Publish → nuova versione salvata nel DB
```

**Processing Status**:
| Status | Descrizione |
|--------|-------------|
| `pending` | Upload completato, estrazione non iniziata |
| `processing` | Estrazione testo in corso |
| `completed` | Testo estratto con successo |
| `failed` | Errore durante estrazione (mostra errore) |

**PDF Document Fields**:
- `fileName`, `fileSizeBytes`, `contentType`
- `extractedText` (full text)
- `pageCount`, `characterCount`
- `processingStatus`, `processingError`
- `extractedTables`, `extractedDiagrams` (JSON, future)

---

### 4. RuleSpec Editor (JSON Live Editor)

#### ✏️ **Editor Grafico RuleSpec con Undo/Redo**
**Endpoint**: `/games/{gameId}/rulespec`
**Status**: ✅ Implementato (base), 🔄 Miglioramenti pianificati (EDIT-01)

**Feature Chiave**:
- Editor JSON a doppio pannello (editor + preview)
- Validazione real-time con error feedback
- Undo/Redo con history stack
- Preview formattato con metadati regole
- Auto-save su blur

**Frontend Pages**:
- `pages/editor.tsx` (481 righe)

**API Endpoints**:
- `GET /games/{gameId}/rulespec` - Carica RuleSpec
- `PUT /games/{gameId}/rulespec` - Salva modifiche (Admin/Editor)

**RuleSpec Format**:
```json
{
  "gameId": "demo-chess",
  "version": "1.0.0",
  "createdAt": "2025-10-14T12:00:00Z",
  "rules": [
    {
      "id": "rule-1",
      "text": "Each player starts with 16 pieces...",
      "section": "Setup",
      "page": "1",
      "line": "10"
    }
  ]
}
```

**Editor Features**:
- Validazione schema JSON con errori dettagliati
- Undo/Redo illimitato (stack in memoria)
- Preview live con formattazione regole
- Link a storico versioni (`/versions?gameId=...`)

---

### 5. Version History & Diff (RULE-02)

#### 📜 **Storico Versioni RuleSpec con Diff**
**Endpoint**: `/games/{gameId}/rulespec/*`
**Status**: ✅ Implementato (backend), ⚠️ UI minimale (EDIT-02 pianificato)

**Feature Chiave**:
- Storico completo versioni RuleSpec
- Diff tra due versioni qualsiasi
- Metadata versione (createdAt, createdByUserId)

**Frontend Pages**:
- `pages/versions.tsx` - Lista versioni (implementazione minima)

**API Endpoints**:
- `GET /games/{gameId}/rulespec/history` - Lista versioni (Admin/Editor)
- `GET /games/{gameId}/rulespec/versions/{version}` - Ottiene versione specifica
- `GET /games/{gameId}/rulespec/diff?from={v1}&to={v2}` - Diff tra versioni

**Diff Response**:
```json
{
  "from": "1.0.0",
  "to": "1.1.0",
  "changes": [
    {
      "type": "added|modified|deleted",
      "path": "rules[0].text",
      "oldValue": "...",
      "newValue": "..."
    }
  ]
}
```

---

### 6. Admin Dashboard (ADM-01: COMPLETATO)

#### 📊 **Monitoring AI Requests & Stats**
**Endpoint**: `/admin/*`
**Status**: ✅ Implementato

**Feature Chiave**:
- Statistiche aggregate (totale richieste, latenza media, token count, success rate)
- Breakdown per endpoint (qa, explain, setup)
- Feedback analytics (helpful/not-helpful count)
- Tabella richieste con filtri (endpoint, user, game, date range)
- Export CSV con tutti i metadati

**Frontend Pages**:
- `pages/admin.tsx` (398 righe)

**API Endpoints**:
- `GET /admin/requests` - Lista richieste AI (query params: limit, offset, endpoint, userId, gameId, startDate, endDate)
- `GET /admin/stats` - Statistiche aggregate

**Stats Dashboard**:
| Metrica | Descrizione |
|---------|-------------|
| Total Requests | Numero totale richieste AI |
| Avg Latency | Latenza media in ms |
| Total Tokens | Totale token consumati (prompt + completion) |
| Success Rate | % richieste riuscite |
| Endpoint Counts | Breakdown per tipo endpoint |
| Feedback Totali | Count feedback helpful/not-helpful |

**Request Log Fields** (visibili in tabella):
- Timestamp, Endpoint, Game ID
- Latency (ms), Prompt Tokens, Completion Tokens, Total Tokens
- Confidence score (0.0-1.0)
- Model used (es. `gpt-4`, `claude-3.5-sonnet`)
- Finish reason (es. `stop`, `length`)
- Query text, Status (Success/Error)

**Filtri**:
- Search box: filtra per query, endpoint, userId, gameId
- Dropdown: filtra per endpoint specifico

**Export CSV**: include tutti i campi + metadati aggiuntivi

---

### 7. RAG Agents (AI Multi-Modal)

#### 🤖 **Agenti Specializzati per Tipo Query**
**Endpoint**: `/agents/*`
**Status**: ✅ QA, ✅ Explain, ✅ Setup, 🔄 Chess (CHESS-04)

**Agenti Disponibili**:

| Agente | Endpoint | Descrizione | Status |
|--------|----------|-------------|--------|
| **QA** | `POST /agents/qa` | Risponde a domande specifiche con snippet da PDF | ✅ |
| **Explain** | `POST /agents/explain` | Genera spiegazione approfondita con outline e citazioni | ✅ |
| **Setup** | `POST /agents/setup` | Genera guida setup passo-passo con tempo stimato | ✅ |
| **Chess** | `POST /agents/chess` | Agente conversazionale specializzato per scacchi con analisi posizione FEN | 🔄 |

#### **QA Agent** (Question Answering)
**Request**:
```json
{
  "gameId": "demo-chess",
  "query": "How many players?",
  "chatId": "optional-chat-id"
}
```

**Response**:
```json
{
  "answer": "Chess is played by 2 players.",
  "snippets": [
    {
      "text": "Chess is a two-player strategy game...",
      "source": "chess-rulebook.pdf",
      "page": 1,
      "line": 10
    }
  ],
  "promptTokens": 150,
  "completionTokens": 50,
  "totalTokens": 200,
  "confidence": 0.95,
  "metadata": {
    "model": "gpt-4",
    "finish_reason": "stop"
  },
  "messageId": "msg-123" // per feedback
}
```

#### **Explain Agent** (Deep Explanation)
**Request**:
```json
{
  "gameId": "demo-chess",
  "topic": "Castling rules",
  "chatId": "optional-chat-id"
}
```

**Response**:
```json
{
  "script": "# Castling Rules\n\nCastling is a special move...",
  "outline": ["Introduction", "Prerequisites", "Procedure", "Edge Cases"],
  "citations": [
    {
      "text": "Castling can only be performed...",
      "source": "chess-rulebook.pdf",
      "page": 5
    }
  ],
  "estimatedReadingTimeMinutes": 3,
  "promptTokens": 200,
  "completionTokens": 500,
  "totalTokens": 700,
  "confidence": 0.92
}
```

#### **Setup Agent** (Setup Guide)
**Request**:
```json
{
  "gameId": "demo-chess",
  "chatId": "optional-chat-id"
}
```

**Response**:
```json
{
  "gameTitle": "Chess",
  "steps": [
    {
      "stepNumber": 1,
      "title": "Place the board",
      "instruction": "Position the chessboard so each player has a white square on the right...",
      "estimatedTimeMinutes": 1
    }
  ],
  "estimatedSetupTimeMinutes": 5,
  "promptTokens": 100,
  "completionTokens": 300,
  "totalTokens": 400,
  "confidence": 0.98
}
```

#### **Chess Agent** (CHESS-04: Agente Conversazionale Scacchi)
**Status**: 🔄 Backend implementato, UI in pianificazione (CHESS-05)

**Request**:
```json
{
  "question": "What are the best moves in this position?",
  "fenPosition": "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
  "chatId": "optional-chat-id"
}
```

**Response**:
```json
{
  "answer": "In this starting position, common opening moves are...",
  "suggestedMoves": ["e4", "d4", "Nf3", "c4"],
  "analysis": {
    "positionEvaluation": "+0.2",
    "keyThemes": ["center control", "piece development"]
  },
  "sources": [
    {
      "text": "Opening principles: control the center...",
      "category": "openings",
      "page": 12
    }
  ],
  "promptTokens": 180,
  "completionTokens": 220,
  "totalTokens": 400,
  "confidence": 0.89,
  "metadata": {
    "model": "gpt-4",
    "finish_reason": "stop"
  }
}
```

**Chess Knowledge Base** (CHESS-03):
- 200+ pagine da "Complete Book of Chess Strategy"
- Categorizzato: openings, middlegame, endgame, tactics, strategy
- Indicizzato in Qdrant con embeddings semantici

**API Endpoints**:
- `POST /chess/index` - Indicizza knowledge base (Admin only)
- `GET /chess/search?q={query}&limit={n}` - Search semantica
- `DELETE /chess/index` - Cancella knowledge base (Admin only)

---

### 8. Vector Search & Embeddings (AI-01: COMPLETATO)

#### 🔍 **Semantic Search con Qdrant**
**Endpoint**: `/ingest/pdf/{pdfId}/index`
**Status**: ✅ Implementato

**Feature Chiave**:
- Chunking automatico (512 caratteri, 50 overlap)
- Embeddings OpenRouter (`text-embedding-3-small`, 1536 dimensioni)
- Indexing in Qdrant collection `meepleai-rules`
- Search con score e ranking

**API Endpoints**:
- `POST /ingest/pdf/{pdfId}/index` - Indicizza PDF estratto (Admin/Editor)

**Indexing Pipeline**:
```
1. Fetch extractedText dal DB
2. TextChunkingService → split in chunks (512 chars, 50 overlap)
3. EmbeddingService → generate embeddings (OpenRouter API)
4. QdrantService → upsert chunks in collection
5. Salva VectorDocument metadata nel DB
```

**Vector Document Metadata**:
```json
{
  "id": "vec-doc-123",
  "gameId": "demo-chess",
  "pdfDocumentId": "pdf-123",
  "chunkCount": 45,
  "totalCharacters": 23040,
  "indexingStatus": "indexed",
  "indexedAt": "2025-10-14T12:00:00Z",
  "embeddingModel": "text-embedding-3-small",
  "embeddingDimensions": 1536
}
```

**RAG Search Flow** (in `RagService.AskAsync`):
```
1. User query → generate query embedding
2. Qdrant search → top 5 chunks con score > 0.7
3. LLM prompt con chunks come context
4. Return answer + snippets
```

---

### 9. n8n Workflow Integration (ADM-02: COMPLETATO, N8N-03: PIANIFICATO)

#### ⚙️ **Configurazione n8n Workflows**
**Endpoint**: `/admin/n8n/*`
**Status**: ✅ Backend implementato, ⚠️ UI minimale

**Feature Chiave**:
- Gestione configurazioni n8n (base URL, API key encrypted)
- Test connessione
- Webhook URL per trigger esterni

**Frontend Pages**:
- `pages/n8n.tsx` - UI configurazione (implementazione minimale)

**API Endpoints**:
- `GET /admin/n8n` - Lista configurazioni (Admin)
- `GET /admin/n8n/{configId}` - Ottiene config specifica
- `POST /admin/n8n` - Crea nuova configurazione
- `PUT /admin/n8n/{configId}` - Aggiorna configurazione
- `DELETE /admin/n8n/{configId}` - Elimina configurazione
- `POST /admin/n8n/{configId}/test` - Test connessione

**N8n Config Fields**:
```json
{
  "id": "n8n-config-1",
  "name": "Production n8n",
  "baseUrl": "https://n8n.meepleai.com",
  "apiKeyEncrypted": "...",
  "webhookUrl": "https://api.meepleai.com/webhooks/n8n",
  "isActive": true,
  "lastTestedAt": "2025-10-14T12:00:00Z",
  "lastTestResult": "Connection successful"
}
```

---

### 10. Observability & Health Checks (OPS-01: COMPLETATO)

#### 🩺 **Health Monitoring con Seq**
**Endpoint**: `/health/*`
**Status**: ✅ Implementato

**Feature Chiave**:
- Health check endpoints per Kubernetes readiness/liveness
- Seq dashboard per log aggregation con correlation IDs
- Health check dependencies: Postgres, Redis, Qdrant

**Health Endpoints**:
- `GET /health` - Dettagliato con status tutti i servizi
- `GET /health/ready` - Readiness check (DB + cache + vector)
- `GET /health/live` - Liveness check (app running)

**Health Check Response**:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgres",
      "status": "Healthy",
      "description": "PostgreSQL database connection",
      "duration": 12.5,
      "tags": ["db", "sql"]
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": "Redis cache connection",
      "duration": 3.2,
      "tags": ["cache", "redis"]
    },
    {
      "name": "qdrant",
      "status": "Healthy",
      "description": "Qdrant vector database",
      "duration": 8.7,
      "tags": ["vector", "qdrant"]
    },
    {
      "name": "qdrant-collection",
      "status": "Healthy",
      "description": "Qdrant collection exists",
      "duration": 5.1,
      "tags": ["vector", "qdrant", "collection"]
    }
  ],
  "totalDuration": 29.5
}
```json
**Seq Dashboard**:
- URL: `http://localhost:8081` (dev), configurabile via `SEQ_URL`
- Log aggregation con `X-Correlation-Id`
- Filtri: userId, endpoint, correlation ID
- Structured logs con request metadata (IP, UserAgent, UserId, UserEmail)

**Correlation IDs**:
- Ogni request ottiene un `X-Correlation-Id` header nella response
- Tutti i log della request hanno lo stesso correlation ID
- Utile per tracciare errori end-to-end

---

## Planned Features

### 🚧 Immediate Priorities (Q1 2025)

#### CHESS-05: Chess UI with Chessboard Visualization
**Status**: 🔄 In pianificazione
**Estimated Effort**: 2 settimane

**Descrizione**:
- UI dedicata per chat scacchi con visualizzazione scacchiera
- Integrazione libreria chessboard.js o react-chessboard
- Input FEN position (manuale o via editor visuale)
- Display mosse suggerite con highlight sulla scacchiera
- Integrazione con CHESS-04 agent backend

**API Endpoints** (già esistenti):
- `POST /agents/chess`

**UI Components**:
- `pages/chess-chat.tsx` (nuovo)
  - Chessboard component (libreria esterna)
  - FEN input/editor
  - Chat interface con mosse suggerite
  - Position analysis display

**Design Notes**:
- Separare da chat principale per UX ottimizzata
- Mostrare analisi posizione in sidebar
- Suggerimenti mosse cliccabili → aggiorna scacchiera

---

#### CHESS-06: n8n Webhook per Agente Scacchi
**Status**: 🔄 In pianificazione
**Estimated Effort**: 1 settimana

**Descrizione**:
- Webhook n8n per trigger agente scacchi da eventi esterni
- Notifiche Discord/Slack quando giocatore fa mossa
- Automazione analisi posizione scheduled

**API Endpoints** (nuovo):
- `POST /webhooks/n8n/chess` - Webhook receiver

**n8n Workflow Example**:
```
Trigger (webhook)
  → Parse FEN position
  → Call /agents/chess
  → Format response
  → Send to Discord/Slack
```json
---

#### UI-03: Wizard Import PDF→RuleSpec (Enhancement)
**Status**: 🔄 In pianificazione
**Estimated Effort**: 1 settimana

**Descrizione**:
- Migliorare UX wizard esistente
- Preview PDF inline (PDF.js)
- Highlight testo estratto vs. testo originale
- Drag & drop per upload PDF
- Progress bar dettagliata per parsing

**Enhancement su**:
- `pages/upload.tsx`

**Nuove Feature**:
- PDF viewer component (PDF.js)
- Highlight match tra estratto e originale
- Drag & drop zone
- Progress bar con step details

---

#### AI-05: Caching Risposte RAG (PERF-01)
**Status**: 🔄 Backend implementato, frontend integration in pianificazione
**Estimated Effort**: 3 giorni

**Descrizione**:
- Cache risposte RAG identiche in Redis
- TTL configurabile (default: 1 ora)
- Cache key: `gameId:query:hash`
- Header `X-Cache-Status: HIT|MISS` nella response

**API Changes**:
- Nessun cambiamento API client-side
- Response header aggiuntivo: `X-Cache-Status`

**Frontend Integration**:
- Mostrare badge "cached" su risposte da cache
- Analytics dashboard: cache hit rate

---

#### EDIT-01: Editor Grafico RuleSpec (Enhancement)
**Status**: 🔄 In pianificazione
**Estimated Effort**: 2 settimane

**Descrizione**:
- Drag & drop riordino regole
- Visual editor per rule atoms (no raw JSON)
- Syntax highlighting JSON avanzato (CodeMirror/Monaco)
- Split view: grafico + JSON raw
- Search & replace in regole

**Enhancement su**:
- `pages/editor.tsx`

**Nuove Feature**:
- Sortable list per regole (DnD)
- Form-based editor per rule atoms
- CodeMirror/Monaco integration
- Search bar con highlight

---

#### EDIT-02: UI Diff RuleSpec Versions
**Status**: 🔄 In pianificazione
**Estimated Effort**: 1 settimana

**Descrizione**:
- Visualizzazione diff side-by-side
- Highlight changes (added/modified/deleted)
- Filtri per tipo change
- Export diff report (PDF/Markdown)

**Enhancement su**:
- `pages/versions.tsx`

**API Endpoints** (già esistenti):
- `GET /games/{gameId}/rulespec/diff?from={v1}&to={v2}`

**UI Components**:
- Diff viewer (react-diff-viewer o custom)
- Side-by-side comparison
- Change type filters
- Export button

---

### 🔮 Future Enhancements (Q2-Q4 2025)

#### UI-04: Timeline Conversazioni RAG
**Epic**: UI-04
**Status**: 🔮 Backlog

**Descrizione**:
- Timeline visuale conversazioni chat
- Filtri per data, gioco, agente, feedback
- Export conversazione completa (PDF/JSON)
- Statistiche conversazione (token count, latenza media)

---

#### UI-05: Audit Accessibilità Baseline
**Epic**: UI-05
**Status**: 🔮 Backlog

**Descrizione**:
- ARIA labels su tutti gli elementi interattivi
- Keyboard navigation completa
- Screen reader testing
- Color contrast WCAG AA compliance
- Focus indicators visibili

**Tools**:
- Axe DevTools
- Lighthouse accessibility audit
- Manual screen reader testing (NVDA/JAWS)

---

#### AI-06: Valutazione RAG Offline
**Epic**: AI-06
**Status**: 🔮 Backlog

**Descrizione**:
- Dataset di test con query + expected answers
- Metriche: accuracy, recall, BLEU score
- A/B testing tra modelli LLM
- Monitoring quality drift

---

#### AI-07: Versioning Prompt e Chain
**Epic**: AI-07
**Status**: 🔮 Backlog

**Descrizione**:
- Versioning prompt templates nel DB
- A/B testing prompt variants
- Rollback a prompt precedenti
- Analytics per prompt performance

---

#### API-02: Streaming Explain RAG (API-05)
**Epic**: API-02, API-05
**Status**: 🔮 Backlog

**Descrizione**:
- Server-Sent Events (SSE) per streaming risposte lunghe
- Progressive rendering su frontend
- Interrompibilità stream
- Migliora UX per explain agent (risposte lunghe)

**API Changes**:
- `POST /agents/explain` → SSE stream
- Frontend: EventSource per consume stream

---

#### API-03: Gestione API Key e Quota
**Epic**: API-03, API-04
**Status**: 🔮 Backlog

**Descrizione**:
- API key per accesso programmatico
- Quota per utente/API key
- Dashboard quota usage
- Billing integration (opzionale)

---

#### DB-03: Indici Full-Text PDF/RuleSpec
**Epic**: DB-03
**Status**: 🔮 Backlog

**Descrizione**:
- PostgreSQL full-text search su `extractedText`
- tsvector + GIN index
- Hybrid search: vector + full-text
- Boost exact keyword matches

---

#### DOC-03: CONTRIBUTING e SECURITY
**Epic**: DOC-03
**Status**: 🔮 Backlog

**Descrizione**:
- `CONTRIBUTING.md` con linee guida PR
- `SECURITY.md` con vulnerability disclosure
- Code of Conduct

---

#### OPS-02: OpenTelemetry Stack
**Epic**: OPS-02
**Status**: 🔮 Backlog

**Descrizione**:
- Distributed tracing con OpenTelemetry
- Jaeger/Tempo backend
- Metrics export (Prometheus)
- Grafana dashboards

---

#### OPS-03: Error Handling Frontend
**Epic**: OPS-03
**Status**: 🔮 Backlog

**Descrizione**:
- Error boundary React
- Sentry integration
- User-friendly error messages
- Retry logic automatico

---

#### OPS-04: Logging Strutturato
**Epic**: OPS-04
**Status**: 🔮 Backlog

**Descrizione**:
- Structured logs anche su frontend (console.log → logger lib)
- Frontend log forwarding (opzionale)
- Log levels configurabili

---

#### OPS-05: Error Monitoring e Alerting
**Epic**: OPS-05
**Status**: 🔮 Backlog

**Descrizione**:
- Alerting su error rate > threshold
- PagerDuty/Opsgenie integration
- Slack notifications per errori critici

---

#### PDF-03: Parser Tabelle/Flowchart
**Epic**: PDF-03
**Status**: 🔮 Backlog

**Descrizione**:
- Estrazione tabelle strutturate (iText7 TableExtractor)
- OCR per diagrammi/flowchart (Tesseract)
- Indexing contenuti visivi

---

#### PERF-02: Session Caching (Redis)
**Epic**: PERF-02
**Status**: 🔮 Backlog (backend parzialmente implementato)

**Descrizione**:
- Cache session validation in Redis
- Riduce query DB per ogni request
- TTL = session expiry

---

#### APP-01: Integrazione Lovable.dev
**Epic**: APP-01
**Status**: 🔮 Backlog

**Descrizione**:
- Integrazione applicazione Lovable.dev nel monorepo
- Sincronizzazione codice e deployment
- Unified CI/CD pipeline

---

## Technical Architecture

### Frontend Stack Details

**Framework**: Next.js 14 (Pages Router)
- SSR/SSG capabilities (attualmente solo CSR utilizzato)
- API routes per `/api/health` (proxy health check)

**TypeScript Configuration**:
```json
{
  "strict": true,
  "esModuleInterop": true,
  "skipLibCheck": true,
  "moduleResolution": "node",
  "paths": {
    "@/*": ["./src/*"]
  }
}
```json
**Testing Setup**:
- **Unit/Integration**: Jest 29 + React Testing Library
- **E2E**: Playwright
- **Coverage Target**: 90% (enforced)
- **Test Patterns**: `**/__tests__/**/*.[jt]s?(x)`

**Build & Deploy**:
- Build command: `pnpm build`
- Dev server: `pnpm dev` (port 3000)
- Prod server: `pnpm start`
- Linting: `pnpm lint` (ESLint + Next.js config)
- Type checking: `pnpm typecheck`

---

### API Client Architecture

**File**: `apps/web/src/lib/api.ts`

**Features**:
- Automatic retry on network errors (configurable)
- Cookie-based authentication (credentials: "include")
- Auto-redirect to login on 401
- Type-safe request/response (generic TypeScript)
- Base URL from `NEXT_PUBLIC_API_BASE` env var

**Usage Example**:
```typescript
import { api } from "@/lib/api";

// GET request
const games = await api.get<Game[]>("/games");

// POST request
const chat = await api.post<Chat>("/chats", {
  gameId: "demo-chess",
  agentId: "agent-qa"
});

// PUT request
const updated = await api.put<RuleSpec>(`/games/${gameId}/rulespec`, ruleSpec);

// DELETE request
await api.delete(`/chats/${chatId}`);
```

**Error Handling**:
```typescript
try {
  const response = await api.post("/agents/qa", { gameId, query });
} catch (error) {
  // error.message contiene dettagli errore
  console.error(error.message);
}
```

---

### State Management Strategy

**Attualmente**: Local component state (useState)
- Nessun Redux/MobX/Zustand
- Ogni pagina gestisce il proprio stato
- Props drilling minimale (componenti piatti)

**Future**: Considerare Context API o Zustand per:
- User session globale
- Theme/preferences
- Toast notifications

---

### Routing & Navigation

**Router**: Next.js Pages Router

**Routes**:
| Route | Descrizione | Auth Required | Roles |
|-------|-------------|---------------|-------|
| `/` | Home (login/register) | No | All |
| `/chat` | Chat interface | Yes | All |
| `/upload` | PDF upload wizard | Yes | Admin, Editor |
| `/editor?gameId={id}` | RuleSpec editor | Yes | Admin, Editor |
| `/versions?gameId={id}` | Version history | Yes | Admin, Editor |
| `/admin` | Admin dashboard | Yes | Admin |
| `/n8n` | n8n config | Yes | Admin |
| `/logs` | AI request logs | Yes | Admin |

**Navigation Components**:
- Inline `<Link>` components in ogni pagina
- No navbar/sidebar globale (da aggiungere in futuro)

---

### Styling Architecture

**Current**: Inline styles (React `style` prop)
- Nessun CSS Modules, Tailwind, o styled-components
- Ogni componente ha stili inline

**Pros**:
- No build-time CSS processing
- Scoping automatico
- Facile refactoring

**Cons**:
- Verbose
- No hover/pseudo-selectors
- No media queries inline
- Difficile theme consistency

**Future Migration Path**:
```
Inline styles → CSS Modules (breve termine)
CSS Modules → Tailwind CSS (lungo termine)
```

**Design System** (da definire):
- Color palette
- Typography scale
- Spacing system
- Component library (buttons, inputs, cards)

---

## API Integration Guide

### Authentication Flow

**1. Register**:
```typescript
const response = await api.post<AuthResponse>("/auth/register", {
  email: "user@example.com",
  password: "securePass123!",
  displayName: "John Doe",
  role: "User"
});

// Response contiene user + expiresAt
const { user, expiresAt } = response;

// Cookie automaticamente impostato dal backend
// Frontend può salvare user in state/context
```

**2. Login**:
```typescript
const response = await api.post<AuthResponse>("/auth/login", {
  email: "user@example.com",
  password: "securePass123!"
});

const { user, expiresAt } = response;
```

**3. Check Current User**:
```typescript
const response = await api.get<AuthResponse>("/auth/me");

if (response) {
  // User authenticated
  const { user } = response;
} else {
  // Not authenticated → redirect to login
}
```

**4. Logout**:
```typescript
await api.post("/auth/logout");

// Cookie automaticamente rimosso
// Redirect to login page
```

---

### Chat Integration Guide

**1. Load Games**:
```typescript
const games = await api.get<Game[]>("/games");

// games[0].id, games[0].name
```

**2. Load Agents for Game**:
```typescript
const agents = await api.get<Agent[]>(`/games/${gameId}/agents`);

// agents[0].id, agents[0].name, agents[0].kind
```

**3. Create New Chat**:
```typescript
const chat = await api.post<Chat>("/chats", {
  gameId: "demo-chess",
  agentId: "agent-qa"
});

// chat.id → usare per messaggi
```

**4. Load Chat History**:
```typescript
const chatWithHistory = await api.get<ChatWithHistory>(`/chats/${chatId}`);

// chatWithHistory.messages → array ChatMessage
// Convertire in frontend Message format
```

**5. Send Message**:
```typescript
const response = await api.post<QaResponse>("/agents/qa", {
  gameId: "demo-chess",
  query: "How many players?",
  chatId: chatId // opzionale, crea chat se non presente
});

// response.answer, response.snippets, response.messageId
```

**6. Submit Feedback**:
```typescript
await api.post("/agents/feedback", {
  messageId: backendMessageId, // da QaResponse.messageId
  endpoint: "qa",
  outcome: "helpful", // o "not-helpful" o null (clear)
  userId: user.id,
  gameId: "demo-chess"
});
```

**7. Delete Chat**:
```typescript
await api.delete(`/chats/${chatId}`);
```

---

### PDF Upload Integration Guide

**1. Upload PDF**:
```typescript
const formData = new FormData();
formData.append("file", pdfFile);
formData.append("gameId", gameId);

const response = await fetch(`${API_BASE}/ingest/pdf`, {
  method: "POST",
  body: formData,
  credentials: "include"
});

const { documentId } = await response.json();
```

**2. Poll Processing Status**:
```typescript
const pollStatus = async () => {
  const response = await fetch(`${API_BASE}/pdfs/${documentId}/text`, {
    credentials: "include"
  });

  const data = await response.json();

  // data.processingStatus: pending|processing|completed|failed
  // data.processingError: error message se failed

  if (data.processingStatus === "completed") {
    // Proceed to next step
  } else if (data.processingStatus === "failed") {
    // Show error
  } else {
    // Retry after 2 seconds
    setTimeout(pollStatus, 2000);
  }
};

pollStatus();
```

**3. Fetch Generated RuleSpec**:
```typescript
const ruleSpec = await api.get<RuleSpec>(`/games/${gameId}/rulespec`);

// ruleSpec.gameId, ruleSpec.version, ruleSpec.rules
```

**4. Publish RuleSpec**:
```typescript
const updated = await api.put<RuleSpec>(`/games/${gameId}/rulespec`, ruleSpec);

// updated.version → nuova versione salvata
```

**5. Index PDF for Vector Search**:
```typescript
const response = await fetch(`${API_BASE}/ingest/pdf/${pdfId}/index`, {
  method: "POST",
  credentials: "include"
});

const { chunkCount, vectorDocumentId } = await response.json();
```

---

### Admin Dashboard Integration Guide

**1. Fetch Request Logs**:
```typescript
const params = new URLSearchParams({
  limit: "100",
  endpoint: "qa", // opzionale
  userId: "user-123", // opzionale
  gameId: "demo-chess", // opzionale
  startDate: "2025-01-01T00:00:00Z", // opzionale
  endDate: "2025-12-31T23:59:59Z" // opzionale
});

const response = await fetch(`${API_BASE}/admin/requests?${params}`, {
  credentials: "include"
});

const { requests } = await response.json();
```

**2. Fetch Stats**:
```typescript
const response = await fetch(`${API_BASE}/admin/stats`, {
  credentials: "include"
});

const stats = await response.json();

// stats.totalRequests, stats.avgLatencyMs, stats.totalTokens
// stats.successRate, stats.endpointCounts, stats.feedbackCounts
```

---

### Error Handling Best Practices

**1. Network Errors**:
```typescript
try {
  const response = await api.get("/games");
} catch (error) {
  // Check if network error (fetch failed)
  if (error instanceof TypeError) {
    alert("Network error. Please check your connection.");
  } else {
    // API error (4xx, 5xx)
    alert(`Error: ${error.message}`);
  }
}
```

**2. 401 Unauthorized**:
```typescript
// api.ts gestisce automaticamente 401 con redirect a "/"
// Frontend non deve gestire manualmente
```

**3. 403 Forbidden**:
```typescript
try {
  await api.post("/admin/n8n", config);
} catch (error) {
  if (error.message.includes("403")) {
    alert("You don't have permission to perform this action.");
  }
}
```

**4. 429 Rate Limited**:
```typescript
// Backend invia Retry-After header
// Frontend può leggere header e mostrare countdown

try {
  await api.post("/agents/qa", { gameId, query });
} catch (error) {
  if (error.message.includes("429")) {
    const retryAfter = parseInt(error.message.match(/\d+/)?.[0] || "60");
    alert(`Rate limit exceeded. Please try again in ${retryAfter} seconds.`);
  }
}
```

---

### Performance Optimization Tips

**1. Avoid N+1 Queries**:
```typescript
// ❌ Bad: N+1 requests
for (const game of games) {
  const agents = await api.get(`/games/${game.id}/agents`);
}

// ✅ Good: Batch request (se API supporta)
const allAgents = await api.get("/agents?gameIds=demo-chess,catan");
```

**2. Debounce Search Inputs**:
```typescript
import { debounce } from "lodash";

const debouncedSearch = debounce(async (query) => {
  const results = await api.get(`/search?q=${query}`);
  setResults(results);
}, 300);
```

**3. Memoize Expensive Computations**:
```typescript
import { useMemo } from "react";

const filteredRequests = useMemo(() => {
  return requests.filter(req => req.query.includes(filter));
}, [requests, filter]);
```

**4. Lazy Load Components**:
```typescript
import dynamic from "next/dynamic";

const AdminDashboard = dynamic(() => import("../components/AdminDashboard"), {
  loading: () => <p>Loading...</p>,
  ssr: false
});
```

---

## Appendix

### Glossary

| Termine | Definizione |
|---------|-------------|
| **RAG** | Retrieval-Augmented Generation - Pattern AI che combina vector search + LLM |
| **Embedding** | Rappresentazione vettoriale di testo per semantic search |
| **Chunk** | Segmento di testo estratto da PDF (512 caratteri default) |
| **Vector Search** | Search basata su similarità coseno tra embeddings |
| **RuleSpec** | Schema JSON per rappresentare regole giochi da tavolo in formato machine-readable |
| **Agent** | Modulo AI specializzato per tipo di query (QA, Explain, Setup) |
| **Session** | Sessione utente autenticata con cookie HttpOnly |
| **Correlation ID** | ID univoco per tracciare request attraverso log distribuiti |

### Additional Resources

- **CLAUDE.md**: Guida sviluppo completa
- **Database Schema**: `docs/database-schema.md`
- **Observability Guide**: `docs/observability.md`
- **Security Scanning**: `docs/security-scanning.md`
- **RuleSpec Schema**: `schemas/README.md`

### Contact & Support

Per domande su feature specifiche o integration:
- Apri issue su GitHub con label `area/frontend`
- Tag: `question`, `enhancement`, `bug`
