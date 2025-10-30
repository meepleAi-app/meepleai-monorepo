# Use Cases — MeepleAI

## Attori
- **User**: utente autenticato che interagisce con agenti e giochi.
- **Admin**: utente con privilegi elevati (es. indexing chess knowledge).
- **System**: servizi interni (RAG, logging, qualità, feature flags).

## UC1 — Q&A regole gioco (RAG)
- **Attori**: User
- **Trigger**: domanda sulle regole di un gioco
- **Precondizioni**: sessione attiva; `gameId` valido
- **Flusso principale**:
  1. User invia query a `/agents/qa` (opz. `chatId`)
  2. API valida e chiama `IRagService.AskWithHybridSearchAsync(...)`
  3. Opzionale: genera follow-up con `IFollowUpQuestionService`
  4. Persistenza messaggi via `ChatService`
  5. Logging con `AiRequestLogService`
- **Postcondizioni**: risposta + eventuali citazioni/snippets; follow-up questions
- **Errori**: 401 se sessione mancante; 400 se `gameId` mancante; 500 on failure

## UC2 — Spiegazione guidata (Explain)
- **Attori**: User
- **Flusso**: `/agents/explain` → `I(R)agService.ExplainAsync` → chat/log → risposta con outline e stima tempi

## UC3 — QA Streaming SSE
- **Attori**: User
- **Flusso**: `/agents/qa/stream` → `IStreamingQaService.AskStreamAsync` → eventi Token/Citations/Complete (+ FollowUp async) → chat/log SSE
- **Vincoli**: feature gating via `IFeatureFlagService`

## UC4 — Setup Guide
- **Attori**: User
- **Flusso**: `/agents/setup` → `SetupGuideService.GenerateSetupGuideAsync` (gated da FeatureFlag) → chat/log

## UC5 — Feedback su risposte
- **Attori**: User
- **Flusso**: `/agents/feedback` → `AgentFeedbackService.RecordFeedbackAsync`

## UC6 — Ricerca BoardGameGeek (BGG)
- **Attori**: User
- **Flusso**: `/bgg/search`, `/bgg/games/{bggId}` → `IBggApiService`

## UC7 — Gestione conoscenza Scacchi
- **Attori**: Admin
- **Flusso**: `/chess/index` (indicizzazione), `/chess/search`, `/chess/index` DELETE → `IChessKnowledgeService`

## UC8 — Gestione PDF & Chunks
- **Attori**: Admin/Editor [Inference sui ruoli]
- **Flusso**: upload → estrazione testo/metadati → chunking (db + vector) → stato/liveness
- **Entità**: `PdfDocumentEntity`, `TextChunkEntity`
