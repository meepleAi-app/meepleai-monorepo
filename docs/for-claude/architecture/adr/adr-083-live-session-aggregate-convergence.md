# ADR-083 — Convergenza degli aggregati "sessione live" su LiveGameSession

**Date**: 2026-06-23
**Status**: Accepted (2026-06-23) — direzione ratificata dall'owner: **Direzione A** = `LiveGameSession` canonico + scoring live **round-based**. Open Questions risolte (codice). Vedi § "Update 2026-06-23 (2)".
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

> **✅ TUTTE E TRE RISOLTE 2026-06-23** (confidence alta, verificate sul codice via investigazione parallela) — vedi § "Update 2026-06-23 (2)" per risoluzioni ed evidenze `file:line`. Sintesi: nessun id condiviso tra i tre aggregati; gli endpoint ricchi `/game-sessions/*` risolvono solo su `session_tracking_sessions`; modelli di scoring genuinamente incompatibili. Il dimensionamento di Fase 2 è ora determinato (Direzione A ≈ 1.5-2 settimane).

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

## Update 2026-06-23 — Finding bloccante: conflitto di modello scoring

Durante la stesura del piano di Fase 1 è emerso che i due sistemi hanno modelli di scoring **incompatibili**:

- **GameSession/SessionTracking** — scoring **polimorfico** (`scoringType` + `scoreData` JSON). `SessionLiveView` lo consuma da `GameSessionDto` (`SessionLiveView.tsx:875` scoring hydration, `:897` `turnOrderType`). Shippato di recente: **#2389 G5a Block A/B/C (2026-06-19)** + **#2483 turnOrderType (2026-06-15)** — *più recenti di ADR-060*.
- **LiveGameSession** — scoring **round-based** (`scoringConfig` dimensions + `roundScores` per-round). `LiveSessionDto` (`live-sessions.schemas.ts`) **non** espone `scoringType`/`scoreData`/`turnOrderType`.

Correggere il loader di SessionLiveView a `LiveSessionDto` (la "correzione semplice" prevista in Fase 1) **perderebbe** i campi polimorfici su cui poggia il lavoro #2389/#2483 appena shippato.

**Conseguenza sulla decisione**: questo dimostra che **entrambi i sistemi hanno investimento recente** (epic #2354/#2389 su GameSession/SessionLiveView è *più recente* di ADR-060 su LiveGameSession), con modelli dati confliggenti. La direzione di convergenza **non era determinabile dal solo codice** — richiedeva l'intento del team/owner. La Fase 1 non è una correzione di ore: include la **riconciliazione dei modelli di scoring** (decidere quale modello sopravvive). La raccomandazione di questo ADR (LiveGameSession) è stata quindi **declassata a "da decidere con l'owner alla luce di questo finding"** — Status `Proposed` **fino alla ratifica**.

> **→ Risolto.** L'owner ha ratificato la **Direzione A** il 2026-06-23: lo scoring live resta **round-based** (modello `LiveGameSession`); il polimorfico `SessionTracking` (Points/Ranking/BinaryWin/Objectives) resta confinato ai play-records storici. Vedi § "Update 2026-06-23 (2)".

## Update 2026-06-23 (2) — Open Questions risolte + decisione owner ratificata

Le 3 Open Questions sono state risolte con un'**investigazione parallela ancorata al codice** (5 agenti di lettura + sintesi adversariale). Confidence **alta** su tutte. La sintesi ha anche scoperto una contraddizione tra due agenti e l'ha risolta verificando direttamente lo schema (`live-sessions.schemas.ts`).

### Scoperta strutturale: gli aggregati sono **TRE**, non due

Il disallineamento è tra **tre** aggregati su **tre** prefissi di route — il loader residuo (`api.sessions.getById`) punta a un terzo aggregato, un guscio quasi vuoto:

| # | Aggregato | Tabella | Route | Scoring |
|---|---|---|---|---|
| 1 | `GameSession` (GameManagement) | `game_sessions` | `/api/v1/sessions/*` | **nessuno** (guscio, 4 stati) — *ciò che SessionLiveView carica davvero* |
| 2 | `Session` (SessionTracking) | `session_tracking_sessions` | `/api/v1/game-sessions/*` | **polimorfico** (Points/Ranking/BinaryWin/Objectives) — detiene SSE/chat-RAG/media/diary |
| 3 | `LiveGameSession` (GameManagement) | `live_game_sessions` | `/api/v1/live-sessions/*` | **round-based** (dimensioni × round) — *ciò che i wizard creano* + FSM 5-stati + Xmin |

### Open Questions — risoluzioni

1. **OQ1 (id condiviso)** → **NO.** Gli endpoint `/api/v1/game-sessions/{id}/*` risolvono **esclusivamente** su `session_tracking_sessions` (`GetSessionDetailsQueryHandler.cs:22`, `SessionRepository.cs:24`). Nessun FK/colonna verso `live_game_sessions` (`LiveGameSessionEntityConfiguration.cs:172-180` referenzia solo `shared_games`+`users`; `LiveGameSession.cs:40-68` non ha `SessionId`). Passare un `live_game_sessions.Id` a quegli endpoint → **404**. SSE/chat-RAG/media/diary **non** sono raggiungibili da una LiveGameSession senza lavoro.
2. **OQ2 (SSE)** → **NO.** `useSessionLiveStream` (`use-session-live-stream.ts:214`) è hardcoded su `/api/v1/game-sessions/{id}/stream/v2`; l'handler (`SessionQueryEndpoints.cs:321-348` → `GetSessionStreamQueryHandler`) valida **solo** su `session_tracking_sessions`. Oggi SessionLiveView gli passa l'id di `GameSession` (`SessionLiveView.tsx:328-331`) → 404. Fallirebbe anche con id `LiveGameSession`.
3. **OQ3 (scoring)** → **genuinamente incompatibili.** `GameSessionDto` (GameManagement) **non ha** alcun campo scoring; i campi FE `scoringType`/`scoreData`/`turnOrderType` sono `.optional()` (`games.schemas.ts:98-116`) quindi la validazione passa ma arrivano `undefined` e la hydration di `SessionLiveView.tsx:876-900` **si auto-cortocircuita in silenzio**. Il polimorfico vive in `SessionTracking.Session` (4 `IScoringStrategy`); il round-based in `LiveGameSession` (`scoringConfig.enabledDimensions` + `roundScores`, `live-sessions.schemas.ts:105-137`). Discriminated-union JSON vs matrice round×dimensione×player: nessuno è superset dell'altro.

> **Conseguenza verificata**: lo scoring polimorfico shippato di recente in SessionLiveView (#2389 G5a, #2483) gira sul guscio vuoto `GameSessionDto` → non è mai stato collegato a dati reali (spiega perché #2354 funziona solo su fixtures `IS_VISUAL_TEST_BUILD`).

### Decisione ratificata dall'owner — **Direzione A**

**`LiveGameSession` è l'aggregato canonico delle sessioni live; lo scoring live resta round-based.** Razionale: riusa l'aggregato già più maturo (FSM 5-stati ADR-071, concorrenza Xmin ADR-060, dispute, setup-checklist RAG, teams, snapshot, guest players) ed è quello che il funnel reale crea; il lavoro localizzato (chat/diary/media) costa ~1.5-2 settimane contro i 30-42 giorni del big-bang della Direzione B (fusion di 3 entità da un `GameSession` vuoto, con migrazione DB e drop di `live_game_sessions` ad alto rischio — sconsigliata dall'analisi stessa).

**Conseguenza su #2389/#2483**: lo scoring polimorfico in `SessionLiveView` va **ri-orientato ai play-records storici** (dove il modello `SessionTracking` è la source of truth), non mantenuto sul path live. Da tracciare in Fase 1/2.

### Piano a fasi — ridimensionato (post-risoluzione OQ)

- **Fase 0** ✅ — questo ADR (mappa + direzione + OQ risolte + ratifica).
- **Fase 1** — *Allineamento loader a LiveGameSession*: in `SessionLiveView` sostituire `useSession()`/`api.sessions.getById` (GameSession) con il `LiveSessionDto` che il **layout già carica** (`session-store.ts:126`); adattare `composeSessionLiveState` ai players `displayName`/`id` di LiveGameSession; allineare `useSessionLiveStream` all'id corretto. Esito: SessionLiveView mostra le sessioni reali del funnel. **Niente riconciliazione scoring** (decisa: round-based) → scope ridotto rispetto al timore precedente.
- **Fase 2** — *Colmare i gap feature su LiveGameSession* (chat-RAG citazioni #2500, media/foto #2503, diary #2502, SSE realignment). Spec dettagliata: [`docs/for-developers/specs/2026-06-23-epic-2501-fase2-live-session-feature-gaps.md`](../../../for-developers/specs/2026-06-23-epic-2501-fase2-live-session-feature-gaps.md) (spec-panel: 27 finding, 23 AC, 6 sub-fasi). La "decisione di design aperta" è stata **risolta in SP0** — vedi § "Update 2026-06-23 (3)".
- **Fase 3** — *Add-player UI* (**#2505**) su `POST /api/v1/live-sessions/{id}/players` (già presente, guest inclusi) + ricablaggio wizard mobile previa parità offline-sync.
- **Fase 4** — *Deprecazione legacy*: ritiro del loader GameSession e, se confermato ridondante, delle superfici `/sessions/[id]/play` e degli endpoint `/api/v1/sessions/*` non più usati.

### Issue indipendenti (non bloccate)
- **#2504** (`/agents/setup` propaga `playerCount`) — quick-win BE, parallelizzabile.
- **#2502** (seed gioco con KB `Ready`) — fixture/seed, parallelizzabile.

## Update 2026-06-23 (3) — SP0 decision-record (Fase 2)

La Fase 2 è stata analizzata via `/sc:spec-panel` (7 esperti, [spec dedicata](../../../for-developers/specs/2026-06-23-epic-2501-fase2-live-session-feature-gaps.md)). Il panel ha smontato la "decisione aperta" dell'Update (2): la dicotomia «endpoint nativi vs ponte `ChatSessionId`» era una **falsa scelta su premessa errata** — `ChatSessionId` punta a un ChatThread di **KnowledgeBase** (non SessionTracking) ed è **dead code** (`SetAgentMode` chiamato solo da test); e **#2500 è già un servizio cross-BC end-to-end** (`ChatWithSessionAgentCommandHandler` produce già `CitationDto` + evento SSE con citazioni). La decisione va **decomposta per capability**, non presa monoliticamente.

### Decisioni owner ratificate (2026-06-23)

| # | Open Question | Decisione |
|---|---|---|
| OQ1 | Correlazione identità | **Session-companion canonica**: `LiveGameSession` crea/possiede una `SessionTracking.Session` companion at-creation via **Saga**, con un nuovo `TrackingSessionId` **non-nullable** garantito. È l'identità autoritativa che correla chat/diary/media. |
| OQ2 | Transport realtime | **SSE nativo** `/api/v1/live-sessions/{id}/stream` (GameManagement): heartbeat 30s + Last-Event-ID resume + **sequence number monotono persistente**. Si deprecata `/game-sessions/{id}/stream/v2`. SignalR resta solo per i suoi usi esistenti. |
| OQ3 | `ChatSessionId` + `SetAgentMode` | **Rimuovere** (dead code, falso ponte verso KnowledgeBase): da dominio, DTO e schema FE. La chat-RAG è già keyed su `LiveGameSession.Id`. |

### Riconciliazione OQ1 ↔ OQ2 (tensione risolta)

Le due scelte creano una tensione: con la **Session-companion** (OQ1) chat/diary/media si correlano su `SessionTracking` (via `TrackingSessionId`), ma lo **stream è nativo su GameManagement** (OQ2). Risoluzione:

- Lo stream `/api/v1/live-sessions/{id}/stream` è un **SSE gateway/aggregatore** su GameManagement, keyed su `LiveGameSession.Id`. Il BE risolve `TrackingSessionId` e fonde in **un Canonical Event Model** gli eventi: domain-events di `LiveGameSession` (score/turn/phase) + eventi della `Session` companion SessionTracking (diary/media) + eventi chat-RAG da KnowledgeBase (chat + `citations[]`). Il FE consuma **un solo URL**.
- **`TrackingSessionId` è il ponte REALE** (non `ChatSessionId`): non-nullable, popolato dalla Saga at-creation → niente null-window, niente race.
- **chat-RAG** resta servizio KnowledgeBase (wiring FE), keyed su `LiveGameSession.Id` — invariato.
- **diary/media**: con la Session-companion garantita, **riusano** la `SessionTracking.Session` companion via `TrackingSessionId` (no duplicazione nativa su GameManagement), raggiunti attraverso il gateway/ACL di GameManagement.

### Invarianti e policy (SP0)

- **No cicli**: `GameManagement` **non importa mai** tipi/servizi di `KnowledgeBase`. La dipendenza `KnowledgeBase → GameManagement` (chat-RAG) è già stabilita e aciclica; resta unidirezionale. La dipendenza `GameManagement → SessionTracking` (gateway/ACL per diary/media/stream) è unidirezionale via interfaccia dedicata (anti-corruption layer), no HTTP self-call.
- **Naming/routing**: convenzione path→aggregato proprietario. URL canonico FE sotto `/live-sessions/*`; le rotte agent keyed su `LiveGameSession.Id` migrano sotto `/live-sessions/{id}/agent/*` (alias routing, stesso handler), deprecando l'alias `game-sessions`.
- **Versioning eventi SSE**: payload con `type`+`version`, additive-only per non-breaking, nuovo path versionato solo per breaking change, finestra di sunset.
- **Semantica di consegna SSE**: **at-least-once + dedup-by-monotonic-id**; il sequence number per-sessione (Redis INCR o colonna append-only) è posizione assoluta replayabile da qualsiasi istanza.
- **Out of scope Fase 2**: path agente non-canonico `/sessions/live/[sessionId]/agent` + `AskSessionAgentCommandHandler` (no-RAG) → da deprecare; scoring polimorfico in live (ri-orientato ai play-records).

### Conseguenze (impatto onesto)

La combinazione scelta (Session-companion + SSE nativo) è la **più robusta ma anche la più costosa** tra quelle valutate: la Saga at-creation + il `TrackingSessionId` garantito + il **backfill/coexistence** delle sessioni in-flight + il **gateway SSE** su GameManagement portano la stima della Fase 2 **oltre** i ~19-27 gg della spec base (la stima ADR originale di "1.5-2 settimane" è superata). Vantaggio: correlazione garantita ovunque (niente null-window su chat/diary/media) + un solo canale realtime con contratto chiaro. La Saga al create introduce un punto di atomicità cross-BC da coprire con test (failure → nessuna LiveGameSession orfana senza companion). Questi costi vanno riflessi nelle sub-fasi SP0/SP2/SP4/SP5 della spec.

## Update 2026-06-29 (SP3) — Storage del diary: NATIVO su LiveGameSession (emenda la direttiva SP0 "riusa companion")

L'Update (3) SP0 stabiliva che diary/media "riusano la `SessionTracking.Session` companion via `TrackingSessionId`". Per il **diary** (SP3, issue #2570) questa direttiva è **emendata**: il diary append-only è un'**entità nativa su `LiveGameSession`** (GameManagement-owned), non sul companion.

### Razionale (code-grounded, da de-risk read-only)
1. **L'eventing forza comunque un domain-event di `LiveGameSession`.** Il forwarder SP2 (`LiveSessionStreamForwarder`) inoltra allo stream **solo** domain-event di `LiveGameSession`; non esiste alcun handler `SessionTracking → ILiveSessionStreamGateway`. Quindi `session:diary` deve nascere come domain-event di `LiveGameSession` *a prescindere da dove è memorizzato*. Memorizzare il diary altrove (companion) **separerebbe** il write-path (SessionTracking) dall'event-path (GameManagement) → problema dual-source/dedup ("chi è autoritativo sul replay").
2. **Semantica incompatibile.** Le note del companion (`SessionEvent`/`SessionNote`, SessionTracking) sono **crittografate-at-rest, private per-partecipante** — semanticamente NON un diario pubblico append-only multi-autore. Riusarle sovraccaricherebbe quell'aggregato.
3. **Lowest-risk.** Append-only + 409-guard + forwarder è un clone 1:1 del pattern `RecordScore` esistente; co-locare storage ed evento elimina i rischi dual-write/dedup.

### Scope dell'emendamento
- Si applica **solo al diary** (SP3). Il **media** (SP4, già shipped come foto in EndgameDialog #2558, FE-only su PlayRecord) e la **chat** (SP1 #2500, KnowledgeBase) NON sono toccati da questo emendamento. La direttiva SP0 "Session-companion canonica" resta valida come **identità correlante** (`TrackingSessionId`), ma il diary non vi memorizza i propri dati.
- Conseguenza: `LiveGameSession` ha `Notes` (single-string, host-level, overwrite) **e** una collezione `DiaryEntry` (append-only, multi-autore) — due store distinti per due job distinti (OQ#6 risolta: coesistenza, nessuna deprecazione di Notes).

### Decisione ratificata dall'owner (2026-06-29)
**Option A — entità `DiaryEntry` nativa su `LiveGameSession`.** Endpoint `POST/GET /api/v1/live-sessions/{id}/diary`; `LiveSessionDiaryEntryAddedEvent` → forwarder → `session:diary` (`{ entryId, authorId, content, timestamp(ISO-8601) }`); 409 su Completed (allow Paused). De-risk: `.superpowers/sdd/sp3-diary-derisk-brief.md`.

## Riferimenti
- Epic #2501; user story di validazione #2506; gap issue #2500/#2503/#2505/#2504/#2502.
- **Spec Fase 2**: `docs/for-developers/specs/2026-06-23-epic-2501-fase2-live-session-feature-gaps.md` (spec-panel).
- ADR-060 (LiveGameSession persistence, EPIC #2097), ADR-065 (namespace split), ADR-071 (5-state FSM).
- Scoring polimorfico recente: #2389 (G5a Block A/B/C), #2483 (turnOrderType), Asse A #1896.
- Superfici: `SessionLiveView.tsx` (Wave D), `play-mode-mobile.tsx` (Improvvisata), `LiveSessionView` (`components/game-night`, `/sessions/[id]/play`).
