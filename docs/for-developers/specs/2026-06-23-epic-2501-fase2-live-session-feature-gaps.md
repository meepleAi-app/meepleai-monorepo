# Spec — Epic #2501 Fase 2: feature gaps sessione live (chat-RAG citazioni · media/foto · diary · SSE)

**Date**: 2026-06-23
**Status**: SP0 ratificato (2026-06-23) — decisioni owner prese (Session-companion + SSE nativo + rimozione `ChatSessionId`). Pronta per `writing-plans` di SP1+. Vedi § "SP0 — Decisioni ratificate" e [ADR-083 § Update (3)](../../for-claude/architecture/adr/adr-083-live-session-aggregate-convergence.md).
**Issue**: #2501 (epic) Fase 2 · assorbe #2500 (chat-RAG citazioni), #2503 (foto + salvataggio), #2502 (diary), realignment SSE
**Related**: [ADR-083](../../for-claude/architecture/adr/adr-083-live-session-aggregate-convergence.md) (Direzione A: `LiveGameSession` canonico) · Fase 1 shipped (#2511) · #2504 shipped (retrieval pgvector)
**Metodo**: `/sc:spec-panel` — panel di 7 esperti (Fowler, Newman, Hohpe, Nygard, Wiegers, Adzic, Crispin) su contesto ancorato al codice. 27 finding (9 critical / 12 major / 6 minor), 23 AC.

---

## TL;DR

L'ADR-083 dichiarava la Fase 2 come **una** decisione binaria («endpoint nativi su GameManagement **vs** ponte cross-BC via `ChatSessionId`»). Il panel, verificando il codice, ha stabilito che **questa è una falsa dicotomia su premessa errata**:

1. **L'Opzione B (ponte via `ChatSessionId`) è irrealizzabile**: `ChatSessionId` (`LiveGameSession.cs:65`) è settato solo da `SetAgentMode` (`:790-803`), chiamato **solo da test** (dead code in produzione), e punta a un **ChatThread di KnowledgeBase**, *non* a `SessionTracking.Session`. Non esiste FK né id condiviso: il join che il ponte richiederebbe non esiste.
2. **#2500 (chat-RAG citazioni) è già un servizio cross-BC end-to-end**: `ChatWithSessionAgentCommandHandler` (KnowledgeBase) dipende già da `ILiveSessionRepository`, risolve `agentSession.GameSessionId` contro `LiveGameSession`, produce `CitationDto` copyright-tier-aware (`:246/:546`) e lo streamma in `StreamingComplete.Citations` (`:564`). La dipendenza KnowledgeBase→GameManagement è già **stabilita e aciclica**. #2500 è **wiring FE**, non architettura BE.

**La decisione corretta è decomposta per capability**, non monolitica.

---

## SP0 — Decisioni ratificate (2026-06-23)

L'owner ha chiuso le 3 Open Question bloccanti. **Dove queste divergono dalla raccomandazione del panel più sotto, prevalgono queste.**

| # | Decisione | Note vs raccomandazione panel |
|---|---|---|
| **OQ1 — identità** | **Session-companion canonica**: `LiveGameSession` crea/possiede una `SessionTracking.Session` companion at-creation via **Saga**, con `TrackingSessionId` **non-nullable** garantito. | Il panel raccomandava «nativo + evento» (più leggero). L'owner ha scelto l'opzione **più robusta ma più costosa**: correlazione garantita ovunque, a costo di Saga + backfill. |
| **OQ2 — transport** | **SSE nativo** `/api/v1/live-sessions/{id}/stream` (gateway su GameManagement, sequence monotono persistente, deprecata `/stream/v2`). | Allineato alla raccomandazione. |
| **OQ3 — `ChatSessionId`** | **Rimuovere** (dead code / falso ponte verso KnowledgeBase). | Allineato alla raccomandazione. |

### Architettura risultante (riconciliazione OQ1 ↔ OQ2)

Con la Session-companion, chat/diary/media si correlano su `SessionTracking` via `TrackingSessionId`, ma lo stream è nativo su GameManagement → lo stream `/live-sessions/{id}/stream` è un **SSE gateway/aggregatore** keyed su `LiveGameSession.Id` che risolve `TrackingSessionId` e fonde in **un Canonical Event Model** gli eventi di 3 sorgenti:

| Capability | Owner | Meccanismo (post-SP0) |
|---|---|---|
| Chat-RAG + citazioni (#2500) | KnowledgeBase | **Wiring FE** del path esistente (`/agent/chat` keyed su `LiveGameSession.Id`); `citations[]` nell'evento per il fan-out. Invariato. |
| Diary (#2502) | SessionTracking (companion) | **Riuso** della `Session` companion via `TrackingSessionId` (no entità nativa duplicata su GameManagement), via gateway/ACL di GameManagement. |
| Media + save (#2503) | SessionTracking (companion) + PlayRecord | Media sulla companion via `TrackingSessionId`; save esplicito come boundary event (estende `save-complete`) → proietta a PlayRecord. |
| SSE | GameManagement (gateway) | `/live-sessions/{id}/stream` aggrega domain-events `LiveGameSession` + eventi companion SessionTracking + chat-RAG KB. |

**`TrackingSessionId` è il ponte REALE** (non `ChatSessionId`): non-nullable, popolato dalla Saga at-creation → niente null-window, niente race.

**Invarianti**: `GameManagement` non importa mai `KnowledgeBase`; `KnowledgeBase → GameManagement` (chat-RAG) e `GameManagement → SessionTracking` (gateway diary/media/stream, via ACL/interfaccia dedicata, no HTTP self-call) restano **unidirezionali e acicliche**. Naming path→aggregato; eventi `type`+`version` additive-only; consegna **at-least-once + dedup-by-monotonic-id**.

> **Impatto stima**: la combinazione scelta è la più robusta ma più costosa. Saga at-creation + `TrackingSessionId` + **backfill/coexistence** sessioni in-flight + **gateway SSE** portano la Fase 2 **oltre** i ~19-27 gg base. La Saga introduce un punto di atomicità cross-BC da coprire con test (failure create → nessuna `LiveGameSession` orfana senza companion). Le sub-fasi sotto sono aggiornate di conseguenza.

> ⚠️ La sezione «Decisione architetturale» e le Open Question #1/#2/#3 sotto restano come **traccia dell'analisi del panel**; per le scelte effettive fa fede questa sezione SP0.

---

## Decisione architetturale

### Opzioni valutate

- **A — Endpoint nativi su GameManagement/`LiveGameSession`** (`POST/GET /api/v1/live-sessions/{id}/{diary,media,stream}`): l'aggregato live possiede i propri dati; handoff a SessionTracking/PlayRecord via **evento di dominio** al save esplicito.
- **B — Ponte cross-BC via `ChatSessionId` → SessionTracking**: **RIFIUTATA** (fattualmente impossibile, vedi TL;DR). 6/7 esperti la giudicano irrealizzabile o non-testabile.
- **C — Id condiviso / Session canonica unica** (Hohpe): `LiveGameSession` crea/possiede una `SessionTracking.Session` companion al create (Saga, Correlation Identifier non-nullable). Più pulita concettualmente ma richiede migrazione/backfill e correlazione garantita at-creation oggi inesistente. **Resta aperta** (Open Question #1).
- **D — Wiring-first per la chat-RAG**: nessun nuovo endpoint né ponte — montare il path RAG esistente sulla superficie live. È il caso di #2500.

### Raccomandazione del panel — decomposta per capability

| Capability | Issue | Aggregato proprietario | Meccanismo |
|---|---|---|---|
| **Chat-RAG + citazioni** | #2500 | KnowledgeBase (esistente) | **Opzione D** — wiring FE del path RAG esistente (`/agent/chat` keyed su `LiveGameSession.Id`); consumare `StreamingComplete.Citations`; montare `ChatCitationCard`. **Nessun nuovo BE.** |
| **Diary** | #2502 | GameManagement | **Opzione A** — endpoint nativo `/live-sessions/{id}/diary`, lista **append-only** strutturata, distinta da `SessionTracking.SessionEvent` e dal campo `Notes` single-string. |
| **Media + save esplicito** | #2503 | GameManagement | **Opzione A** — media nativo su `LiveGameSession.Id` + save esplicito come **boundary event** (estende `/live-sessions/{id}/save-complete`, già esistente `:200`) che proietta a PlayRecord/SessionTracking via evento. |
| **SSE realignment** | (#2504/realignment) | GameManagement | **Opzione A** — stream nativo `/live-sessions/{id}/stream` che replica il contratto osservabile (heartbeat 30s + Last-Event-ID resume + 429 semantics). |

**Regola invariante (ADR)**: `GameManagement` **non importa mai** tipi/servizi di `KnowledgeBase`. Endpoint di chat su `/live-sessions` che richiamino il motore RAG creerebbero una **dipendenza circolare** GameManagement→KnowledgeBase: **vietato**. Per un URL canonico FE coerente usare alias/gateway di **routing** (stesso handler, path nuovo), non spostare la dipendenza di codice.

**Rationale**: endpoint nativi su GameManagement danno isolamento dei guasti per capability (bulkhead) e seam di test deterministici (l'id nell'URL = `LiveGameSession.Id`, sempre presente, niente null-window). Un proxy runtime cross-BC verso SessionTracking accoppierebbe la disponibilità della schermata live a un BC non-essenziale — la live-core (scoring/turns) vive su `LiveGameSession` e non ha bisogno di SessionTracking → degradazione inaccettabile.

**Consenso**: convergenza forte (6/7) contro l'Opzione B; unanime (7/7) sul fatto che #2500 è prevalentemente wiring FE; convergenza su diary/media nativi + save-come-boundary-event. **Divergenza residua**: Hohpe sostiene l'Opzione C (Session-companion canonica + consolidamento dei due transport realtime) — da risolvere in Open Question #1.

---

## Open Questions (bloccanti — da chiudere in SP0)

> **Stato post-SP0 (2026-06-23)**: **#1 RISOLTA** (Session-companion canonica) · **#2 RISOLTA** (SSE nativo) · **#3 RISOLTA** (rimuovere `ChatSessionId`) · **#4 RISOLTA** (consegna at-least-once + dedup-by-monotonic-id). Vedi § "SP0 — Decisioni ratificate". Restano di dettaglio per i plan delle sub-fasi: **#5** (backfill/coexistence sessioni in-flight — ora rilevante per la Saga), **#6** (Notes vs diary), **#7** (deep-link MVP), **#8** (out-of-scope path non-canonico — confermato out-of-scope in SP0).

1. **[BLOCCANTE] Correlazione identità**: introdurre la Session-companion canonica (Opzione C, id non-nullable at-creation via Saga) **oppure** `LiveGameSession` resta canonico e diary/media nativi correlano a SessionTracking **solo via evento al save**? I fatti escludono il ponte via `ChatSessionId` ma non determinano tra «nativo puro + handoff evento» e «Session-companion canonica». Determina se serve migrazione/backfill e quanta ACL.
2. **[BLOCCANTE] Transport realtime**: consolidare su **un** canale (SignalR `GameStateHub` + backplane Redis, oppure SSE nativo `/live-sessions`) o mantenere due canali con dedup client nel breve? Verificato: SignalR è single-instance (no backplane Redis, `Program.cs:525`); i domain event di `LiveGameSession` duplicano la SSE taxonomy.
3. **[BLOCCANTE] Destino di `ChatSessionId` + `SetAgentMode`** (dead code): cablarlo a un endpoint reale `POST /live-sessions/{id}/agent-mode` (lega la sessione live a un ChatThread KB) **oppure** rimuoverlo da dominio+DTO+schema FE. Lasciarlo ambiguo è la terza via, la più costosa (debito di interfaccia).
4. Semantica di consegna SSE da dichiarare (at-least-once + dedup-by-monotonic-id vs at-most-once): determina cosa asserire nei test di reconnect e se serve sequence number monotono persistente (oggi l'id è wall-clock tick, non monotono cross-instance).
5. Backfill/coexistence: sessioni create **prima** della Fase 2 (in-flight) — serve una dual-read window per chat/diary/media e la nuova rotta stream?
6. Diary: il campo `Notes` (host-level, single-string) coesiste distinto dal diary append-only o viene deprecato/migrato?
7. `openUrl`/deep-link citazione in MVP: viewer PDF ancorato a pagina (`/documents/{D}#page={N}`) in scope o si nasconde il CTA «Apri regolamento»?
8. Out-of-scope da ratificare: il path non-canonico `/sessions/live/[sessionId]/agent` e `AskSessionAgentCommandHandler` (no-RAG, citazioni null) vengono deprecati in Fase 2?

---

## Finding consolidati (27)

### 🔴 Critical (9)
1. **Premessa ADR errata** — `ChatSessionId` non è un ponte verso SessionTracking ed è dead code → correggere ADR-083 rimuovendo l'Opzione B.
2. **Falsa dicotomia uniforme** — 4 capability eterogenee trattate come una decisione A/B → decomporre (tabella `{capability, owner, meccanismo}`).
3. **#2500 è gap di wiring FE, non architetturale** — il servizio cross-BC chat-RAG con citazioni esiste già end-to-end.
4. **Ambiguità chat** — «chat-RAG» mappa a DUE chat fisiche (sociale SessionTracking vs agente KnowledgeBase) e DUE `CitationDto` divergenti (`SearchResultDto.cs:66` vs `Contracts` copyright-tier-aware) → pinnare nell'ADR: la chat live canonica È la chat agente RAG.
5. **Identity-namespace collision già in produzione (post-Fase-1)** — il route param (`LiveGameSession.Id`) colpisce endpoint SessionTracking (`/game-sessions/{id}/chat`, `/diary`, `/stream/v2`) che validano contro un aggregato senza FK.
6. **Due transport realtime non consolidati** — SSE SessionTracking + SignalR `GameStateHub` (no backplane) alimentano la stessa vista con garanzie asimmetriche e duplicate-delivery.
7. **Garanzie SSE deboli** — id `{sessionId}-{Ticks}` non monotono, replay Last-Event-ID solo su buffer in-process per-nodo → buchi silenziosi su reconnect cross-instance.
8. **Nessun contract/integration test** sul round-trip citazioni (persistenza→trasmissione) né sul path `ChatSessionId` null (che è lo stato di **default**).
9. **Nessun degradation contract** — un proxy cross-BC accoppia la live alla disponibilità di un BC non-essenziale.

### 🟡 Major (12)
10. #2503 save esplicito attraversa 3 aggregati senza orchestrazione/atomicità (EndgameDialog è acknowledge-only; `PlayRecordPhotoUploadDialog` targetizza `PlayRecord.recordId`) → boundary event/Process Manager su `LiveSessionCompletedEvent`, riusa `save-complete`.
11. #2502 diary: `UpdateNotes` single-string-overwrite vs `SessionEvent` append-only → doppia fonte di verità; definire diary append-only nativo distinto.
12. SSE realignment senza contratto evento/equivalenza/migrazione; i test hardcodano l'endpoint da deprecare (`use-session-live-stream.ts:214`, unit asserisce la stringa, E2E aborta `/game-sessions/**` → falso-verde) → refactor test-first + expand-and-contract con header Deprecation/Sunset.
13. Rischio dipendenza circolare GameManagement↔KnowledgeBase se si creano endpoint chat su `/live-sessions` → vietarlo nell'ADR; alias di routing, non spostamento di dipendenza.
14. Payload evento SSE `session:chat` senza campo `citations` → #2500 risolvibile solo per il mittente (response HTTP), non per la vista condivisa → estendere il Canonical Event Model con `citations[]`.
15. `ChatSessionId` correlation identifier opzionale/lazy → race latente, null per sessioni agent-off (default) → se serve correlazione, renderla invariante at-creation, non sovraccaricare `ChatSessionId`.
16. Fail-silently sui write (`catch {}` con conferma via evento) accoppia il write a uno stream rotto → usare la response del POST come ack (Request-Reply), evento per il solo fan-out.
17. AC «≥1 citazione sempre» non realizzabile (copyright tier azzera snippet; domande non-grounded non citano) → quantificatore condizionale (vedi AC-CHAT-1/2/3).
18. Nessuna osservabilità/SLO per il path live chat-RAG né metriche SSE/upload → istogramma latenza, counter retrieval-empty, SSE active-connection+reconnect, photo-upload-failure; SLO p95.
19. #2503 upload-failure/partial-save senza contratto; foto grandi (timeout/size/presigned scaduto) non coperte → DoD media a 3 livelli.
20. SSE drop/reconnection senza copertura test (il seam `MockEventSource.triggerError` esiste ma nessuna asserzione) → 3 test resilienza come DoD.
21. Decisione «aperta» senza criteri/gate temporale blocca la scrittura degli AC → decision-record con criteri + gate (chiusura prima dell'implementazione).

### 🟢 Minor (6)
22. Naming routing: `/game-sessions/*` serve sia SessionTracking sia AgentSession keyed su `LiveGameSession.Id` → convenzione path→aggregato + tabella mapping.
23. Versionamento eventi SSE non specificato (esistono già `/stream` e `/stream/v2`) → policy additive-only + sunset.
24. `ChatCitationCard.openUrl` senza sorgente (no deep-link a pagina) → specificare `/documents/{D}#page={N}` o nascondere il CTA in MVP.
25. Due path agente (canonico senza citazioni + non-canonico `/sessions/live/[sessionId]/agent`) + `AskSessionAgentCommandHandler` no-RAG → convergere su un solo Message Endpoint; sezione Out-of-scope.
26. Asimmetria reconnect/backpressure SSE vs SignalR → consolidare transport o allineare policy + un solo connection-state in UI.
27. Stima 1.5-2 settimane non credibile (non conta degradation contracts, SSE server-contract, metriche, backfill) → splittare per capability con line-item di affidabilità.

---

## Acceptance Criteria

### Chat-RAG + citazioni (#2500)
- **AC-CHAT-0** — *La chat live canonica è la chat agente RAG*. GIVEN `SessionLiveView → ChatAgentPanel → LiveAgentChat`, WHEN l'utente invia un messaggio, THEN la richiesta colpisce il path RAG (`ChatWithSessionAgentCommandHandler` via `/agent/chat` keyed su `gameSessionId=LiveGameSession.Id`), NON il path SessionTracking `AskSessionAgentCommandHandler` (no-RAG). Verificato nell'ADR prima degli altri AC.
- **AC-CHAT-1** — *Happy path con page+snippet*. GIVEN una `LiveGameSession` con PDF KB-Ready, WHEN l'utente chiede una regola groundabile, THEN la risposta arriva via stream con ≥1 citazione, `ChatCitationCard` renderizza per ognuna `documentName + "pag. {N}" (N≥1) + excerpt non vuoto`, AND dopo reload le citazioni sono ancora presenti (persistite, non solo streamate).
- **AC-CHAT-2** — *Tier protetto*. GIVEN un chunk citato con `CopyrightTier != Full`, THEN `SnippetPreview == null`, il FE renderizza `ParaphrasedSnippet` con badge «sintesi», NESSUN testo verbatim al FE (assert sul payload), la card NON renderizza excerpt vuoto.
- **AC-CHAT-3** — *Non-grounded / nessuna fonte*. GIVEN domanda fuori-regolamento o KB assente, THEN `citations == []`, il FE NON renderizza `ChatCitationCard` né header «Fonti (0)» fuorviante, mostra disclaimer esplicito.
- **AC-CHAT-4** — *Mapping `CitationDto` → FE*. `PageNumber → pages[]`, `SnippetPreview` (o `ParaphrasedSnippet` se tier≠Full) `→ excerpt`, `openUrl` solo se definito.
- **AC-CHAT-5** — *Deep-link (MVP)*. SE viewer ancorato implementato → `openUrl = /documents/{D}#page={N}` e il click apre a pag.N; ALTRIMENTI `openUrl` undefined e il CTA «Apri regolamento» NON è mostrato (nessun 404).
- **AC-CHAT-NULL** — *`ChatSessionId` null / agente off (default)*. GIVEN `AgentMode=None`, THEN il pannello renderizza vuoto-ma-funzionale, GET chat ritorna `200` empty deterministico (`X-Warning-Code: chat-not-linked`), MAI 404/500/NRE.

### Media + save esplicito (#2503)
- **AC-MEDIA-1** — GIVEN punteggi finali, WHEN apro `EndgameDialog`, THEN vedo sezione «Aggiungi foto» + CTA primario «Salva e concludi» (distinto dall'Acknowledge attuale). WHEN seleziono 2 foto e salvo, THEN ogni foto è caricata (presigned/2-step, `BlobCategory.PlayRecordPhoto`) e associata, la sessione transita a `Completed` via `save-complete`, il CTA è disabilitato durante l'upload (no doppio submit).
- **AC-MEDIA-2** — *Foto opzionali*. WHEN salvo senza foto, THEN la sessione transita a `Completed` con media list vuota.
- **AC-MEDIA-3** — *Upload disaccoppiato dalla finalizzazione*. GIVEN una foto che fallisce (timeout/size/presigned scaduto), THEN errore inline sulla foto, la finalizzazione NON è bloccata (finalize riesce, foto in coda retry con stato «non caricate»), ritento possibile.
- **AC-MEDIA-4** — *Idempotenza*. GIVEN sessione già `Completed`, WHEN ritento il save, THEN `409 ConflictException`, nessun media/PlayRecord duplicato.
- **AC-MEDIA-5** — *Handoff a PlayRecord via evento*. WHEN `LiveSessionCompleted/SavedEvent` è emesso, THEN un handler proietta media+finalScores+diary alla rappresentazione archiviale via evento (outbox, no dual-write sincrono); `GET /live-sessions/{id}` espone i media.

### Diary (#2502)
- **AC-DIARY-1** — *Append-only*. Aggiungere E2 dopo E1 → diario `[E1, E2]` cronologico, E1 non sovrascritto (distinto da `Notes` single-string).
- **AC-DIARY-2** — *Multi-autore*. Ogni entry riporta `authorId`/`id`/`createdAt`; GET ritorna in ordine cronologico stabile.
- **AC-DIARY-3** — *Bloccato su Completed*. `POST /diary` su sessione `Completed` → `409 ConflictException`.
- **AC-DIARY-4** — *Notes vs diary distinti*. `Notes` (host-level) e lista diary sono campi distinti; documentato che `UpdateNotes` non è il diary strutturato.

### SSE (#2504/realignment)
- **AC-SSE-1** — *Reconnect con replay*. GIVEN client a `/live-sessions/{id}/stream` fino a `Last-Event-ID=42`, WHEN riconnette con `lastEventId=42`, THEN solo eventi `id>42` (no dup, no gap, replay at-least-once durevole), primo evento → `CONNECTED`, `retryCount` azzerato.
- **AC-SSE-2** — *Heartbeat + 429*. Nessun evento per 30s → heartbeat; `429` → backoff esponenziale, no loop stretto.
- **AC-SSE-3** — *Degradazione a polling*. `EventSource` non costruibile → `DEGRADED_POLLING`, il fallback NON punta più alla rotta SessionTracking deprecata.
- **AC-SSE-4** — *Inventario eventi*. Un altro partecipante invia chat/diary/foto/score/turn → ricevo evento tipizzato (`chatMessage+citations`/`diaryEntry`/`mediaAdded`/`scoreUpdate`/`turnChange`) entro N s, UI aggiornata senza reload.
- **AC-SSE-5** — *Refactor test-first*. L'asserzione unit punta alla nuova rotta E un test asserisce che `/game-sessions/**/stream/v2` NON è MAI chiamata dalla superficie live; l'abort cieco E2E è sostituito da mock esplicito della nuova rotta.
- **AC-SSE-6** — *Drop mid-stream con dedup*. Interruzione a metà partita + ripristino → nessun evento perso/duplicato (dedup-by-monotonic-id), indicatore di stato connessione visibile, stato riallineato (recupero ultimo evento o re-idratazione REST).

### Osservabilità
- **AC-OBS-1** — Metriche latenza live-RAG (p50/p95/p99), counter retrieval-empty, `citations_per_grounded_answer`; SLO p95 ≤ path chat esistente; alert su spike retrieval-empty (index drift).

---

## Scomposizione in sub-fasi

| Sub-fase | Scope | Dipende da | Stima |
|---|---|---|---|
| **SP0 — ADR decision-record + identità + naming** | ✅ **Decisioni prese 2026-06-23** ([ADR-083 § Update 3](../../for-claude/architecture/adr/adr-083-live-session-aggregate-convergence.md)): Session-companion canonica (Saga + `TrackingSessionId`); SSE nativo gateway; rimozione `ChatSessionId`; invarianti no-ciclo; naming path→aggregato; versioning eventi; consegna at-least-once+dedup. **Residuo per i plan**: matrice tracciabilità AC↔issue↔componente, **test di guardia** che fallisce se un `LiveGameSession.Id` va a `/game-sessions/{id}/stream/v2`, e **dimensionamento Saga + backfill** sessioni in-flight. | — | decisioni ✅; resta formalizzazione + dimensionamento Saga/backfill |
| **SP1 — #2500 chat-RAG citazioni: WIRING FE** | Puntare `ChatAgentPanel`/`LiveAgentChat` al path RAG esistente; consumare `StreamingComplete.Citations`; mappare `CitationDto` tier-aware → `ChatCitation`; montare `ChatCitationCard`; gestire `AC-CHAT-NULL`; decidere `openUrl` MVP; estendere il payload evento chat con `citations[]` per il fan-out. Test: integration round-trip persistenza→trasmissione (Testcontainers + `IEmbeddingRepository` seed deterministico #2504, NO LLM); FAST CI mockato esteso; `@slow` real-LLM canary. | SP0 | 3-4 gg · **rischio basso (BE esiste)** |
| **SP2 — SSE realignment + stream nativo** | `GET /api/v1/live-sessions/{id}/stream` su GameManagement che replica il contratto (heartbeat 30s, Last-Event-ID resume at-least-once, 429). Sequence number monotono per-sessione (Redis INCR/colonna append-only) + replay persistente cross-instance. Enumerare i tipi-evento. Repuntare `use-session-live-stream.ts:214` (test-first: asserzione unit + «vecchia rotta mai chiamata»). Test resilienza drop/reconnect/dedup. Expand-and-contract: deprecare `/stream/v2`. Risolvere consolidamento transport (OQ #2). | SP0 | 4-6 gg · **rischio alto** (+1-2 gg se backplane Redis) |
| **SP3 — #2502 diary nativo** | Entità diary append-only (`id/authorId/createdAt/text`) su `LiveGameSession` + `POST/GET /live-sessions/{id}/diary`, distinto da `Notes`. Guard `409` su Completed. Evento diary sullo stream (SP2). Coesistenza Notes vs diary (OQ #6). Test: append-not-overwrite, multi-autore, ordinamento, una sola fonte di verità. | SP0, SP2 | 3-4 gg |
| **SP4 — #2503 media + save esplicito** | Endpoint media nativo su `LiveGameSession.Id` (riusa presigned/2-step + `BlobCategory.PlayRecordPhoto` + `PlayRecordPhotoUrlResolver` da #2436). Wire `EndgameDialog`: sezione foto + CTA «Salva e concludi» → estende `save-complete` (`:200`). Save come boundary event/Process Manager (`LiveSessionCompletedEvent` → proietta media+finalScores+diary a PlayRecord/SessionTracking via outbox). Disaccoppiare upload da finalizzazione; idempotenza `409`. Test 3 livelli (unit FE / integration BE 409 / E2E foto-grande errore deterministico). | SP0, SP2, SP3 | 4-6 gg · **rischio medio-alto** (atomicità cross-BC) |
| **SP5 — Affidabilità trasversale** | Degradation contract per-capability (chat/media/diary down → bulkhead, live-core continua); timeout ≤2s + circuit breaker su call cross-BC. Metriche+SLO (AC-OBS-1). Risolvere fail-silently sui write (response come ack, errore visibile). Cleanup `ChatSessionId`/`SetAgentMode` (SP0). Backfill/coexistence sessioni in-flight (dual-read window). DoD a matrice edge-case→test come gate di merge. | SP1-SP4 | 3-4 gg |

**Stima complessiva**: ~19-27 gg (vs «1.5-2 settimane» dell'ADR — il panel giudica la stima originale non credibile: non contava degradation contract, SSE server-contract, metriche, backfill). Critical path: **SP0 → SP2** (lo stream nativo è il vincolo); SP1 è parallelizzabile dopo SP0 ed è il quick-win (BE già esistente).

---

## Out of scope (Fase 2)

- Path agente **non-canonico** `/sessions/live/[sessionId]/agent` e `AskSessionAgentCommandHandler` (no-RAG) → da deprecare/ritirare (ratificare in SP0; vedi finding #25 e OQ #8). Se restano, rischio di chat live senza citazioni.
- Fix del **bake CI gate** `seed-snapshot-bake-ci.yml` (secrets mancanti nel runner) — tracciato separatamente (vedi spec #2502).
- Scoring polimorfico in live (ri-orientato ai play-records per Direzione A, ADR-083).

---

## Riferimenti

- ADR-083 (Direzione A) · Fase 1 (#2511, loader `LiveGameSession`) · #2504 (retrieval pgvector, pattern citazioni riusabile) · #2502 (e2e.yml KB-Ready per gli E2E RAG)
- Issue assorbite: #2500 (chat citazioni), #2503 (foto/salvataggio), #2502 (diary)
- Componenti chiave (verificati): `ChatWithSessionAgentCommandHandler` (KnowledgeBase, RAG+citazioni), `LiveSessionEndpoints.cs:176/200` (notes/save-complete), `use-session-live-stream.ts:214` (SSE), `LiveGameSession.cs:65/790-803` (`ChatSessionId`/`SetAgentMode` dead code), `ChatCitationCard.tsx`, `EndgameDialog.tsx`, `PlayRecordPhotoUploadDialog.tsx`.
