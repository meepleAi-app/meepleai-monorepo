# Spec — Epic #2501 SP2: SSE nativo `/live-sessions/{id}/stream` (gateway aggregatore)

**Date**: 2026-06-28
**Status**: Design ratificato (2026-06-28) — **Opzione A** scelta dall'owner (riuso + enhance infra condivisa: ACL su `SessionBroadcastService` + Redis INCR seq + replay backed-Redis; il legacy `/stream/v2` eredita il fix seq). Pronto per `writing-plans`.
**Issue**: #2501 (epic) Fase 2 · sotto-fase **SP2** · realignment SSE + stream nativo
**Related**: [ADR-083](../../for-claude/architecture/adr/adr-083-live-session-aggregate-convergence.md) (Direzione A, SP0 decisioni) · [spec Fase 2](2026-06-23-epic-2501-fase2-live-session-feature-gaps.md) (AC-SSE-1..6, AC-OBS-1) · SP0 #2551 (companion `TrackingSessionId`) · SP1 #2519 (chat-RAG) · SP4 #2558 (foto)
**Metodo**: spec-panel ancorato a 3 ricognizioni read-only del codice (FE-stream, BE-SSE/SignalR, LiveSession+companion).

---

## TL;DR

La spec Fase 2 dimensionava SP2 come **alto rischio** (4-6 gg, +1-2 se backplane Redis) temendo di costruire infra SSE greenfield su GameManagement. La ricognizione ribalta la premessa:

1. **L'infrastruttura SSE esiste già ed è matura** (`SessionBroadcastService`, SessionTracking): pool connessioni, heartbeat 30s, 429@20conn, rate-limit 10ev/s, replay buffer, **Redis Pub/Sub opzionale cross-instance già cablato**. Redis è già in DI.
2. **Il ponte d'identità è già stato costruito in SP0**: ogni `LiveGameSession` con GameId possiede una `Session` companion (`TrackingSessionId`). Il gateway riusa l'infra esistente **keyed sul companion**, non la riscrive.
3. **L'unico gap reale di broadcast** è che i 14 domain-event di `LiveGameSession` non sono inoltrati allo stream → serve un **forwarder `INotificationHandler<>`**, non un nuovo trasporto.

**Conseguenza**: SP2 diventa prevalentemente **integrazione + 2 enhancement mirati** (sequence monotono, replay durevole), non costruzione di trasporto. Resta **una** decisione architetturale per l'owner: *dove* applicare gli enhancement (infra condivisa vs servizio parallelo) — vedi § "Decisione aperta".

---

## Mappa verificata (file:line)

### Esistente — SSE SessionTracking (da deprecare)
- Endpoint `GET /api/v1/game-sessions/{sessionId:guid}/stream/v2` — `SessionQueryEndpoints.cs:320-418`, valida `session_tracking_sessions` (`GetSessionStreamQueryHandler.cs:26-43`).
- Event-id `$"{sessionId:N}-{DateTime.UtcNow.Ticks:x}"` — `SessionBroadcastService.cs:141` → **non monotono cross-instance**.
- Replay `CircularEventBuffer` (100 eventi, in-process per-nodo) — `SessionBroadcastService.cs:94-105, 439-497` → **fallisce cross-instance**.
- Heartbeat 30s `SessionQueryEndpoints.cs:373`; 429@20 conn `:351`; rate-limit 10ev/s `SessionBroadcastService.cs:32`.
- **Redis Pub/Sub opzionale già presente**: ctor `SessionBroadcastService(ILogger, IConnectionMultiplexer? redis = null)`; se `redis.IsConnected` → `GetSubscriber()` + `SubscribeToRedisChannels()` (fan-out cross-instance già operativo; manca solo il replay durevole).
- Tassonomia BE (`SseEventTypeMapper.cs:14-86`): `session:state|score|player|toolkit|chat|conflict|whiteboard|timer|gamenight`.
- Redis in DI: `IConnectionMultiplexer` singleton — `InfrastructureServiceExtensions.cs:278`.
- SignalR `GameStateHub` (score/turn/dispute/...) — **single-instance, no backplane** (`Program.cs:525`).

### Esistente — LiveGameSession (GameManagement, target del gateway)
- Route map `LiveSessionEndpoints.cs` (la nuova `/stream` va dopo le route literal-before-parameterized `/active:288`, `/code/{code}:295`, prima di `/{sessionId}:302`).
- 14 domain-event (`LiveGameSession.cs:149-647`): create/start/pause/resume/complete/save/**score**(`:556`)/**turn**(`:600`)/**phase**(`:644`)/player±/team/turn-order. **Nessuno broadcastato** oggi.
- SP0 companion: `TrackingSessionId Guid?` (`LiveGameSession.cs:73`, non-null solo con GameId), Saga `CreateLiveSessionCommandHandler.cs:50-60`, ACL `ICompanionSessionService.CreateCompanionAsync` (commit atomico singolo).
- Outbox: `DomainEventOutboxEntity` + `DomainEventOutboxProcessor` polla → `MediatR.Publish` → `INotificationHandler<TEvent>` (seam di sottoscrizione del forwarder).
- Companion events da aggregare: diary `NoteEvents.cs` (NoteSaved/Revealed/Hidden/Updated/Deleted), media `MediaEvents.cs` (MediaUploaded/Deleted).
- Chat-RAG citazioni: `ChatWithSessionAgentCommandHandler.cs:546-564` → `StreamingComplete.Citations` (`CitationDto`: DocumentId, PageNumber, RelevanceScore, SnippetPreview?, CopyrightTier, ParaphrasedSnippet?, IsPublic).

### Esistente — FE (consumer da repuntare)
- `useSessionLiveStream` — `use-session-live-stream.ts:214` URL hardcoded `/api/v1/game-sessions/{sessionId}/stream/v2`. FSM `connecting|connected|reconnecting|degraded-polling|failed` (`:54-59`); retry budget `[1,2,4,8,16]s` (`:81`); heartbeat skip-accumulate (`:136-139`); 429 euristico → `failed` (`:285-294`); `EventSource(url, {withCredentials:true})` (`:220`).
- **3 consumer aggiuntivi con EventSource separato**: `useWidgetSync.ts:103`, `useTurnOrder.ts:200`, `useWhiteboardTool.ts:230` (tutti su `/game-sessions/{id}/stream/v2`).
- Tassonomia FE (`sse-events.ts:42-54`): `session:score|turn|player-join|player-leave|role-change|pause|resume|endgame|chat|tool-execution|diary|heartbeat` → **diverge** dalla BE (`session:player` vs `player-join/leave`, `session:toolkit` vs `tool-execution`). Parser tollerante `parse-sse-event.ts:306-381` (normalizza nomi campo).
- Compositor puro `compose-session-live-state.ts:326-338`; mount in `SessionLiveView.tsx:389-422`.
- Test che hardcodano l'endpoint: `use-session-live-stream.test.ts:149`, `useTurnOrder.test.ts:345`, `useWidgetSync.test.ts:98`, `useSessionStream.test.ts:101` (v1), E2E `session-live.smoke.spec.ts:114-163` (`:128`).
- `/live-sessions/{id}/stream` **non esiste** (greenfield, no conflitto).

---

## Architettura proposta

```
FE (1+ EventSource)
   └─ GET /api/v1/live-sessions/{liveId}/stream?lastEventId=N        [GameManagement, NUOVO]
        │  authz su LiveGameSession.Id (live-core ownership)
        │  risolve TrackingSessionId (companion, SP0)
        ▼
   ILiveSessionStreamGateway  (ACL GameManagement.Application — stesso pattern di ICompanionSessionService)
        │  delega all'infra SSE esistente keyed sul companion id
        ▼
   SessionBroadcastService  (SessionTracking.Infrastructure — RIUSATO)
        ▲   ▲   ▲
        │   │   └─ chat-RAG: evento `session:chat` con `citations[]`  (KnowledgeBase → companion broadcast)
        │   └──── diary/media companion: NoteEvents / MediaEvents       (già su SessionTracking)
        └──────── LiveGameSession domain-events (score/turn/phase/player)
                  via NUOVO forwarder INotificationHandler<LiveSession*Event>
                  → risolve TrackingSessionId → broadcast canonical event
```

**Canonical Event Model** (un solo URL FE, payload `{ type, version, seq, data }`):
- `session:score` ← `LiveSessionScoreRecordedEvent`
- `session:turn` ← `LiveSessionTurnAdvancedEvent`
- `session:phase` ← `LiveSessionPhaseAdvancedEvent`
- `session:player-join|leave|role-change` ← `LiveSessionPlayer*Event`
- `session:pause|resume|endgame` ← `LiveSessionPaused/Resumed/Completed`
- `session:chat` (+ `citations[]`) ← chat-RAG companion broadcast
- `session:diary` ← `NoteSavedEvent` companion
- `session:media` ← `SessionMediaUploadedEvent` companion
- `heartbeat`

**Invarianti ADR-083 rispettate**:
- `GameManagement.Application` dipende solo dall'interfaccia `ILiveSessionStreamGateway` (no import di tipi SessionTracking); l'impl in `GameManagement.Infrastructure` riferisce l'infra SessionTracking — **identico al pattern `CompanionSessionService`** già accettato in SP0.
- `GameManagement` **non importa** `KnowledgeBase`. Le citazioni arrivano allo stream perché il chat handler (KnowledgeBase) broadcasta sul canale del companion (`SessionTracking`), non perché GameManagement chiama KB.
- Naming path→aggregato: URL canonico `/live-sessions/{id}/stream`; `/game-sessions/{id}/stream/v2` deprecato expand-and-contract (header `Deprecation`/`Sunset`).

---

## Decisione aperta — confine di scope (RICHIEDE OWNER)

Gli enhancement ratificati in SP0 (sequence monotono persistente + replay durevole cross-instance) vanno applicati. La ricognizione mostra che possono andare **sull'infra condivisa** o su un **servizio parallelo**. Tensione reale:

| | **Opzione A — Riuso + enhance infra condivisa** *(raccomandata)* | **Opzione B — Servizio broadcast nativo parallelo su GameManagement** |
|---|---|---|
| Sequence monotono | Redis INCR per-sessione dentro `SessionBroadcastService` (Redis già iniettato lì) — sostituisce `Ticks`. **Beneficia anche lo stream legacy** (fix additivo, basso rischio). | Nuovo servizio GM con proprio Redis INCR; legacy resta su `Ticks` fino a deprecazione. |
| Replay durevole | Buffer replay backed da Redis (lista/stream capped per-sessione) nel servizio condiviso; fallback in-process. | Replay durevole solo nel nuovo servizio. |
| ACL | `ILiveSessionStreamGateway` → delega a `ISessionBroadcastService`. | `ILiveSessionStreamGateway` → impl nativa, nessuna dipendenza SessionTracking infra. |
| Blast radius | Tocca il servizio condiviso (legacy stream incluso) — additivo, coperto da test esistenti. | Nessun impatto sul legacy; **duplica ~300-500 LOC** di pool/heartbeat/429/replay. |
| Costo | ~5-6 gg | ~7-8 gg |
| Rischio | medio (modifica condivisa, ma additiva + test) | medio-alto (duplicazione = doppia manutenzione; il legacy resta debole) |

> **C — MVP (riuso as-is, defer monotonic/durable a SP5)**: ~3-4 gg ma **contraddice la ratifica SP0** (sequence monotono persistente). Topology attuale API single-instance ⇒ il replay cross-instance non è esercitato oggi (cfr. #1965 dormiente) → tecnicamente possibile, ma va ratificato dall'owner come ri-scope di SP0.

**Raccomandazione panel**: **Opzione A**. Riusa infra matura, onora SP0, e migliora *anche* il legacy (l'event-id monotono è un fix DRY). Il blast radius sul servizio condiviso è additivo (nuovo id generator + replay backend opzionale dietro feature dei test esistenti).

> **✅ Ratificata dall'owner 2026-06-28 — Opzione A.** Vincoli implementativi: ogni modifica a `SessionBroadcastService` (id-generator, replay backend) deve restare backward-compatible per lo stream legacy `/stream/v2` (i test legacy sono la guardia); il Redis INCR ha fallback in-process quando Redis è assente (no regressione single-instance dev/test).

---

## Acceptance Criteria (da spec Fase 2, ancorati)

- **AC-SSE-1** — Reconnect con replay: `Last-Event-ID=42` → solo `id>42`, no dup/gap, primo evento `CONNECTED`, retryCount azzerato. *(seq monotono persistente)*
- **AC-SSE-2** — Heartbeat 30s + 429 → backoff esponenziale, no loop stretto.
- **AC-SSE-3** — `EventSource` non costruibile → `degraded-polling`; il fallback **non** punta a `/game-sessions/**/stream/v2`.
- **AC-SSE-4** — Inventario eventi tipizzati: chat(+citations)/diary/media/score/turn/phase ricevuti entro N s, UI senza reload.
- **AC-SSE-5** — *Test-first*: l'asserzione unit punta alla nuova rotta E un test asserisce che `/game-sessions/**/stream/v2` **non è MAI** chiamata dalla superficie live; l'abort cieco E2E sostituito da mock esplicito della nuova rotta.
- **AC-SSE-6** — Drop mid-stream con dedup-by-monotonic-id → nessun evento perso/duplicato, indicatore connessione visibile, stato riallineato.
- **AC-OBS-1** — Metriche SSE: active-connection gauge, reconnect counter, latency histogram; SLO p95. (Parte può scivolare in SP5; minimo: connection gauge + reconnect counter.)

### Matrice tracciabilità AC → componente
| AC | Componente BE | Componente FE | Test |
|---|---|---|---|
| SSE-1/6 | seq Redis INCR + replay durevole (Opz A/B) | `useSessionLiveStream` lastEventId | unit replay + integration reconnect |
| SSE-2 | heartbeat/429 (riusati) | FSM backoff | unit 429 |
| SSE-3 | — | fallback polling repuntato | unit + E2E |
| SSE-4 | forwarder + canonical event model + citations | parser + compositor | integration round-trip |
| SSE-5 | endpoint nuovo | 4 consumer repuntati | unit string + E2E mock |
| OBS-1 | metriche Prometheus | connection-state UI | metric assertion |

---

## Scomposizione in task (TDD, test-first)

1. **T1 — Endpoint + authz + companion-resolve** *(BE)*: `GET /api/v1/live-sessions/{sessionId}/stream` in `LiveSessionEndpoints.cs`; authz su LiveGameSession.Id; risolve `TrackingSessionId`; se null (sessione free-form/legacy) → `200` stream vuoto-ma-valido con `X-Warning-Code: stream-not-linked` (no 404/500). Test: authz, companion-resolve, null-window.
2. **T2 — `ILiveSessionStreamGateway` ACL** *(BE)*: interfaccia in GameManagement.Application + impl in Infrastructure (Opz A: delega a `ISessionBroadcastService` keyed sul companion). Test: invariante no-import (ArchUnit-style se presente), delega.
3. **T3 — Forwarder domain-event** *(BE)*: `INotificationHandler<LiveSessionScoreRecordedEvent|TurnAdvanced|PhaseAdvanced|Player*|Paused|Resumed|Completed>` → risolve TrackingSessionId → broadcast canonical event. Test: ogni evento → broadcast 1 evento del tipo atteso; sessione senza companion → no-op silenzioso.
4. **T4 — Sequence monotono** *(BE, Opz A/B)*: Redis INCR per-sessione sostituisce `{Ticks}`; fallback counter in-process. Test: monotonicità, ripartenza, fallback no-Redis.
5. **T5 — Replay durevole** *(BE, Opz A/B)*: buffer replay backed da Redis (capped) per reconnect cross-instance; fallback in-process. Test: replay `id>N`, no gap, cross-"instance" (simulato).
6. **T6 — Citazioni nell'evento chat** *(BE)*: estendere il broadcast `session:chat` con `citations[]` (mapping `CitationDto` tier-aware). Test: tier=Full → snippet; tier≠Full → paraphrased; non-grounded → `citations:[]`.
7. **T7 — Canonical Event Model + riconciliazione tassonomia** *(BE+FE)*: definire i tipi-evento canonici; allineare FE (`sse-events.ts`) e BE (`SseEventTypeMapper`). Test: round-trip parse di ogni tipo.
8. **T8 — Repunta consumer FE (test-first)** *(FE)*: `use-session-live-stream.ts:214`, `useWidgetSync.ts:103`, `useTurnOrder.ts:200`, `useWhiteboardTool.ts:230` → `/live-sessions/{id}/stream`. Aggiorna i 5 test + E2E (mock esplicito). Asserzione "vecchia rotta mai chiamata".
9. **T9 — Metriche/osservabilità** *(BE)*: connection gauge + reconnect counter (+ latency histogram se in scope). Test: metric increment.
10. **T10 — Deprecazione `/stream/v2`** *(BE)*: header `Deprecation`/`Sunset` (expand-and-contract, no rimozione in SP2). Doc.

**Critical path**: T1→T2→T3 (broadcast funzionante) → T4/T5 (affidabilità) → T6/T7 (payload) → T8 (FE) → T9/T10. T6/T7 parallelizzabili dopo T3.

---

## Rischi
- **Modifica infra condivisa** (Opz A): il legacy stream `/stream/v2` usa lo stesso `SessionBroadcastService` → ogni cambio all'id-generator/replay deve restare backward-compatible (i test esistenti del legacy sono la guardia).
- **4 EventSource separati** lato FE: consolidare in 1 connessione è un refactor più ampio (finding #26) → **out-of-scope SP2**, tracciato per SP5; SP2 si limita a repuntare gli URL.
- **Tassonomia FE↔BE divergente**: T7 deve riconciliare senza rompere il parser tollerante esistente.
- **Companion nullable**: sessioni senza GameId non hanno `TrackingSessionId` → stream vuoto-ma-valido (T1), non errore. Backfill sessioni in-flight → SP5 (OQ#5).

## Out of scope SP2
- Consolidamento dei 4 EventSource FE in una connessione unica (→ SP5).
- Rimozione fisica di `/stream/v2` (solo deprecazione header; rimozione in Fase 4).
- Backfill companion per sessioni in-flight pre-SP0 (→ SP5).
- SignalR `GameStateHub`: resta per i suoi usi (dispute/scoring-config); non consolidato in SP2.

## Riferimenti
- ADR-083 (Direzione A, SP0) · spec Fase 2 (AC-SSE) · SP0 #2551 · ricognizioni 2026-06-28 (FE-stream / BE-SSE / LiveSession+companion).
