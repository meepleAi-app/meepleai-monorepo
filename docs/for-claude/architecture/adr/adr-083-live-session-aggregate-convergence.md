# ADR-083 — Convergenza degli aggregati "sessione live" su LiveGameSession

**Date**: 2026-06-23
**Status**: Proposed (direzione da ratificare dall'owner)
**Issue**: #2501 (epic) — spawned dalla validazione user story #2506
**Related**: ADR-060 (live session persistence), ADR-065 (sessions namespace split), ADR-071 (live-session 5-state FSM)

## Context

La validazione end-to-end della user story «serata di gioco» (#2506) ha scoperchiato una frammentazione del concetto di "sessione live" più profonda di una semplice duplicazione di route (quella era già documentata in ADR-065). Esistono **tre nozioni di sessione su due bounded context**, con superfici UI distribuite in modo incoerente e — soprattutto — un **disallineamento di aggregato non documentato**.

Questo ADR mappa lo stato reale (verificato sul codice) e fissa la **direzione di convergenza**. Non esegue la migrazione: definisce la decisione e il piano a fasi (gli step esecutivi avranno i propri plan).

## Mappa verificata

| Nozione | Aggregato / tabella | BC | Endpoint REST | Creato da | UI |
|---|---|---|---|---|---|
| **GameSession** | `GameSession` / `GameSessions` | GameManagement | `/api/v1/sessions/*` (`GetGameSessionByIdQuery`, `StartGameSessionCommand`) | `SessionSetupModal` (`api.sessions.start`) | — (loader residuo di SessionLiveView) |
| **Session (tracking)** | SessionTracking | SessionTracking | `/api/v1/game-sessions/*` (SSE `stream/v2`, `agent/chat` RAG, `media`, `chat`, diary, invite/join, tools) | (vedi sotto) | accessori real-time |
| **LiveGameSession** | `LiveGameSession` / `live_game_sessions` | GameManagement | `/api/v1/live-sessions/*` (`start`, `players` guest, `scores` polimorfici, `disputes`, `setup-checklist`) | **i wizard** (`api.liveSessions.createSession`) | PlayModeMobile, layout `/sessions/[id]`, SessionLiveView (parziale) |

**File di riferimento (verificati):**
- Loader residuo: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx:328` → `useSession` → `useActiveSessions.ts:58-70` → `api.sessions.getById` → `GameSessionDto` (aggregato GameSession).
- Loader del **layout** (corretto): `apps/web/src/app/(authenticated)/sessions/[id]/layout.tsx:35,43` → `useSessionStore.loadSession` → `session-store.ts:126` → `api.liveSessions.getSession` → `LiveSessionDto` (LiveGameSession).
- Funnel di creazione: `CreateSessionStep.tsx:97` e `session-wizard-mobile.tsx:218` → `api.liveSessions.createSession` (LiveGameSession).
- Backend aggregati distinti, id NON condiviso: `GetGameSessionByIdQueryHandler` (tabella `GameSessions`) vs `GetLiveSessionQuery` (tabella `live_game_sessions`).
- Endpoint ricchi `/game-sessions/*` importano `BoundedContexts.SessionTracking.Application` (`SessionQueryEndpoints.cs:2-5`).

## Il disallineamento (core finding)

Nella stessa route `/sessions/[id]/live` (Wave D, ADR-065) convivono **due fonti dati**:
- il **layout** carica `LiveGameSession` (corretto),
- `SessionLiveView` carica `GameSession` (`api.sessions.getById`).

Poiché i wizard creano `LiveGameSession` e i due aggregati non condividono l'id, `SessionLiveView` **non riesce a caricare** la sessione appena creata (`GET /api/v1/sessions/{liveId}` → 404). Questo spiega perché la superficie più moderna (epic #2354) gira su fixtures (`IS_VISUAL_TEST_BUILD`) e non è collegata al funnel reale.

## Decisione

**LiveGameSession è l'aggregato canonico delle sessioni live.** SessionLiveView va allineato a LiveGameSession; il loader su GameSession è trattato come **residuo da correggere**.

### Razionale

1. **Intento del team documentato**: ADR-060 (2026-06, *implementato* in EPIC #2097) ha portato `LiveGameSession` a persistenza EF + concorrenza `xmin`; ADR-071 ne definisce la 5-state FSM. È l'aggregato su cui il progetto investe attivamente. Il 404 che originò ADR-060 era *"navigando a `/sessions/{sessionId}`"* → risolto persistendo LiveGameSession: la route `/sessions/[id]` è dunque intesa per risolvere LiveGameSession.
2. **Il funnel reale crea LiveGameSession** (entrambi i wizard) e il **layout** di `/sessions/[id]` lo carica già.
3. **Capability di dominio** più ricche su LiveGameSession: scoring polimorfico, disputes v2, setup-checklist RAG, players con guest, turn/teams, snapshots.

### Alternativa considerata e scartata

**Sistema A — GameSession + SessionTracking canonico.** Scartata: SessionTracking (`/game-sessions/*`) **appare essere il sistema precedente** a LiveGameSession (da confermare nella Open Question #1 — gli ADR recenti 060/071 investono su LiveGameSession, non su SessionTracking); fare canonico GameSession significherebbe ricablare il funnel a un aggregato meno investito e ri-costruire scoring polimorfico/disputes/setup-checklist lì. Il valore apparente di SessionTracking (SSE/chat-RAG/media/diary già presenti) si recupera meglio **portando quelle capability su LiveGameSession** (vedi Fase 2) o riusando gli endpoint se condividono l'id (vedi Open Questions).

## Open questions (da risolvere in Fase 1 — bloccanti per dimensionare Fase 2)

1. **Gli endpoint SessionTracking `/api/v1/game-sessions/{id}/*` (SSE `stream/v2`, `agent/chat` RAG con citazioni, `media`, diary) operano su un id condiviso con LiveGameSession, o solo su GameSession/Session?** Determina se la chat RAG (#2500), le foto (#2503) e l'SSE sono *già raggiungibili* da una LiveGameSession (→ Fase 2 minima) oppure vanno **portati** sul BC GameManagement/LiveGameSession (→ Fase 2 sostanziale).
2. **`useSessionLiveStream` SSE** (`use-session-live-stream.ts:214`) è hardcoded su `/api/v1/game-sessions/{id}/stream/v2`: va verificato se accetta un LiveGameSession id.
3. **`turnOrderType`**: consumato da SessionLiveView ma assente in `LiveSessionDto` — richiede aggiunta allo schema o derivazione.

## Piano a fasi

- **Fase 0** — *questo ADR*: mappa + direzione + piano. ✅
- **Fase 1** — *Allineamento loader + verifica integrazione*: risolvere le Open Questions; correggere il loader di SessionLiveView (`api.sessions.getById` → dati LiveGameSession, riusando ciò che il layout già carica); adattare `composeSessionLiveState` al `LiveSessionDto` (players `displayName`/`id`); ricablare il **wizard desktop** a `/sessions/[id]/live`. Esito: SessionLiveView mostra le sessioni reali del funnel su desktop.
- **Fase 2** — *Colmare i gap feature su LiveGameSession*: in base alla Open Question #1, rendere disponibili su LiveGameSession la **chat RAG con citazioni** (assorbe #2500), **media/foto** (assorbe #2503), diary e SSE. Se gli endpoint `/game-sessions` sono riusabili via id condiviso, la fase è di sola integrazione FE; altrimenti include lavoro BE.
- **Fase 3** — *Add-player UI* (#2505) su LiveGameSession (endpoint `POST /api/v1/live-sessions/{id}/players` già presente, guest incluso) + ricablaggio **wizard mobile** previa parità offline-sync (gap noto: `useSyncWorker` assente in SessionLiveView).
- **Fase 4** — *Deprecazione legacy*: ritiro graduale del loader GameSession e, se confermato ridondante, delle superfici `/sessions/[id]/play` (`LiveSessionView`) e degli endpoint `/api/v1/sessions/*` non più usati. Richiede ADR/decisione di follow-up.

### Issue indipendenti (non bloccate dall'epic)
- **#2504** (`/agents/setup` propaga `playerCount`) — backend puro, implementabile in parallelo.
- **#2502** (test data: gioco seed con KB Ready) — fixture/seed, implementabile in parallelo.

## Conseguenze

### Positive
- Una sola superficie live canonica (SessionLiveView su LiveGameSession), collegata al funnel reale → la user story #2506 diventa testabile E2E.
- Le issue #2500/#2503/#2505 si agganciano a fasi dell'epic invece di essere implementate su superfici destinate alla deprecazione.
- Allineamento con l'investimento già fatto (ADR-060/071).

### Negative / debt
- Epic multi-fase (settimane). Fasi 2/4 dimensionabili solo dopo le Open Questions di Fase 1.
- Rischio di doppia manutenzione finché il legacy non è deprecato (Fase 4).
- Parità mobile (offline-sync) è un prerequisito separato per ricablare il wizard mobile.

## Riferimenti
- Epic #2501; user story di validazione #2506; gap issue #2500/#2503/#2505/#2504/#2502.
- ADR-060 (LiveGameSession persistence, EPIC #2097), ADR-065 (namespace split), ADR-071 (5-state FSM).
- Superfici: `SessionLiveView.tsx` (Wave D), `play-mode-mobile.tsx` (Improvvisata), `LiveSessionView` (`components/game-night`, `/sessions/[id]/play`).
