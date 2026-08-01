# Grounding come invariante di contratto — Design

**Issue**: #3388 (epic P1 #3397, Blocco 1 "pavimento di correttezza") · **Data**: 2026-08-01

## Problema

La chat con l'agente in-sessione instrada su **due backend** a seconda che il messaggio contenga immagini, con contratti divergenti:
- **Solo testo** → SSE `ChatWithSessionAgentCommandHandler` (BC KnowledgeBase): RAG con citazioni; grounding **inferito dal FE** (`isNonGrounded` se citations==0).
- **Con immagini** → `AskSessionAgentCommandHandler` (BC SessionTracking): LLM multimodale, **nessun retrieval**, `citationsJson=null`, `confidence` **hard-coded a `0.85`**; il FE **non** imposta `isNonGrounded` → **nessun disclaimer**.

Lo scenario di punta ("scatto una foto del tavolo e chiedo una regola") restituisce una risposta autorevole (`confidence 0.85`) **silenziosamente non-grounded**. Il grounding è oggi proprietà della *modalità di input*, non un invariante di sistema.

## Decisioni (brainstorming 2026-08-01)

- **`enum GroundingStatus { Grounded, Partial, Ungrounded }`** in `Api.SharedKernel` (Published Language neutra, condivisa da KnowledgeBase + SessionTracking). `Partial` è nel contratto per l'epic futura multimodale→RAG (#3390) ma **nessun path lo emette ora**.
- **Derivato dai citations, nessuna migration**: `groundingStatus = citations.Count > 0 ? Grounded : Ungrounded`, calcolato a response-time e **ri-derivabile da `CitationsJson`** già persistito su `SessionChatMessage` al reload. Nessun campo/migration EF.
- **`confidence` fabbricata rimossa** → `null` onesto (nessuna metrica reale disponibile; "numero fabbricato mostrato come misurato" per 3 lenti del panel).
- **Disclaimer calibrato per modalità** (non generico, per evitare banner-blindness).

## Componenti

### BE-1 — enum condiviso
`Api.SharedKernel/.../GroundingStatus.cs`: `enum GroundingStatus { Grounded, Partial, Ungrounded }`. Sul wire il **valore** è il nome dell'enum in **PascalCase** (`"Grounded"`/`"Ungrounded"`) — via `JsonStringEnumConverter` sul path REST e come stringa letterale sul path SSE (che serializza gli enum numericamente); la **chiave** JSON è camelCase (`groundingStatus`). Il FE confronta `=== 'Ungrounded'`.

### BE-2 — path immagine/text-only (`AskSessionAgentCommandHandler`, BC SessionTracking)
- Rimuovere `confidence=0.85f` (`ChatCommandHandlers.cs:201`, `:224`) → `confidence=null`.
- Aggiungere `GroundingStatus` (non-nullable) a `AskSessionAgentResult`; valore `Ungrounded` per costruzione (`citationsJson=null`, nessun retrieval). L'endpoint serializza il result → il campo appare nel JSON.

### BE-3 — path RAG SSE (`ChatWithSessionAgentCommandHandler`, BC KnowledgeBase)
- Emettere `groundingStatus` **server-side** nel contratto/evento terminale: `Grounded` se citations>0, `Ungrounded` se 0. Promuove la logica da FE-derivata a invariante di contratto.

### FE-1 — lettura uniforme
- Tipizzare `groundingStatus: 'grounded'|'partial'|'ungrounded'` in `ChatMessage` (`useSessionAgentChat.ts`); leggerlo da **entrambi** i path (SSE + immagine `SessionLiveView.tsx:1256-1269`), non più inferirlo da citations.

### FE-2 — disclaimer + rimozione confidence fabbricata
- `LiveAgentChat.tsx:240`: mostrare il disclaimer per **ogni** risposta `ungrounded`, su entrambe le modalità, con copy **per-modalità**:
  - immagine: *"Ho risposto dalla foto e dalla mia conoscenza del gioco, non dal regolamento ufficiale — verifica sul manuale"*.
  - testo (0 citazioni): copy esistente del disclaimer RAG.
- Chiavi i18n in `it.json`/`en.json`.
- Rimuovere qualsiasi `confidence` fabbricata renderizzata come "misurata".

## Definition of Done (dalla issue)

- [ ] `groundingStatus` non-nullable esposto da **entrambi** i path.
- [ ] Path immagine mappa `citationsJson=null` → `Ungrounded`.
- [ ] FE mostra il disclaimer per ogni `ungrounded`, su entrambe le modalità.
- [ ] Nessuna `confidence` fabbricata restituita/mostrata.
- [ ] Test di **parità di modalità**: stessa domanda testo-vs-immagine → stesso *tipo* di segnale di grounding (non-nullable).

## Fuori scope

- Chiudere il **gap di capacità** (far passare il multimodale attraverso il retrieval) = epic #3390, non questa issue. Qui si rende **onesto il segnale**, non si aggiunge grounding al path immagine.
- `Partial` producer (nessuno finché #3390 non introduce il grounding parziale).

## Riferimenti

Audit: `docs/for-developers/audits/2026-07-29-in-session-agent-grounding-audit.md` (Fase 1).
