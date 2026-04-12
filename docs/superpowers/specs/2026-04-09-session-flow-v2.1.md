# Session Flow + Contextual Hand + Ad-Hoc GameNight

**Feature Specification v2.1**
**Data**: 2026-04-09
**Status**: Draft — pronto per plan generation
**Supersedes**:
- `docs/frontend/my-hand-spec.md` v1.0 (frozen)
- `docs/superpowers/specs/2026-04-09-session-gameplay-flow-v2.md` v2.0 (superseded, vedi §12)

**BC toccati**: `SessionTracking` (extend), `GameManagement` (extend GameNightEvent), `KnowledgeBase` (query), `GameToolkit` (query), `UserLibrary` (query)

---

## 1. Vision

Un utente con almeno un gioco in libreria e KB indicizzata deve poter:

1. Avviare in un click una **Partita** (= `Session` esistente) su un gioco con KB pronta
2. Avere automaticamente in mano le card contestuali (Game, KB, Agent, Toolkit, Session) — derivazione client-side dallo stato backend
3. Aggiungere **guest player** al tavolo (stringhe libere come `Participant.DisplayName`)
4. Determinare ordine di turno (manuale o RNG auditato)
5. Tirare dadi server-side via endpoint dedicato, con ogni tiro loggato nel Diario (`SessionEvent`)
6. Aggiornare punteggi con storia modifiche nel Diario
7. Mettere in pausa la Session per iniziarne un'altra dello stesso o altro gioco, il sistema auto-raggruppa le Session in un `GameNightEvent` "ad-hoc" (nuovo flusso sul GameNightEvent esistente)
8. Rivedere il Diario di una singola Session o dell'intera serata (UNION delle Session del GameNight)

**Mono-utente**: l'host è l'unico account loggato; gli altri giocatori al tavolo sono guest (zero auth, zero real-time multi-client).

---

## 2. Principio guida: NON reinventare

La v2.0 proponeva un nuovo BC `GamePlay`. La ricognizione del codebase ha rivelato che **il 90% del modello richiesto esiste già**. La v2.1 riscrive la feature come **orchestrazione + gap closure**.

### Entità riusate (as-is, zero modifiche)

| Entità | BC | Riuso |
|---|---|---|
| `Session` | `SessionTracking` | Partita singola. Riuso `Create`, `AddParticipant`, `Pause`, `Resume`, `Finalize`, `Status`, `Participants` |
| `Participant` | `SessionTracking` | Guest player via `DisplayName`, `IsOwner`, `UserId?` nullable |
| `SessionEvent` | `SessionTracking` | Diary append-only. Ha già `SessionId` + `GameNightId?` per cross-night queries |
| `DiceRoll` | `SessionTracking` | RNG crittografico server-side (`RandomNumberGenerator`), formula parser, entity persistita |
| `ScoreEntry` | `SessionTracking` | Score per `(SessionId, ParticipantId, RoundNumber?, Category?)` |
| `GameNightEvent` | `GameManagement` | Meta-aggregatore serata. Status `Draft→Published→Completed/Cancelled` |
| `GameNightSession` | `GameManagement` | Link GameNightEvent ↔ Session con `PlayOrder`, `GameNightSessionStatus` |
| `Agent` + `AgentDefinition` | `KnowledgeBase` | Auto-creato da `AutoCreateAgentOnPdfReadyHandler` quando PDF indicizzato. Al momento dell'avvio Session è già pronto |
| `Toolkit` (dashboard) + widgets | `GameToolkit` | Auto-creato su `GameAddedToLibraryEvent`, accessibile via `GetActiveToolkitQuery` |

### Modifiche minime alle entità esistenti

| Entità | Modifica | Motivazione |
|---|---|---|
| `GameNightEvent` | Nuovo stato `AdHoc` (o flag `IsAdHoc: bool`) + factory `CreateAdHoc(userId, firstSessionId)` che salta `Draft/Published` flow | Supportare serate spontanee senza RSVP/scheduling |
| `GameNightEvent.StartedAt?` (nuovo campo nullable) | Timestamp primo `GameNightSession.Start()` | Distinguere "pianificato" da "già in corso" |

### Campi già presenti che andrebbero valorizzati (bug fix / completamento)

| Campo | Entità | Stato attuale | Uso v2.1 |
|---|---|---|---|
| `SessionEvent.GameNightId?` | esistente, nullable | Probabilmente mai valorizzato | Popolato quando la `Session` appartiene a un `GameNightEvent` ad-hoc |

---

## 3. Decisioni architetturali (consolidate dal brainstorming)

| # | Decisione | Note |
|---|---|---|
| D1 | Scope ambizioso (β) — full session flow, non MVP | |
| D2 | Mono-utente + guest come stringhe | `Participant.DisplayName` già supporta |
| D3 | Hand dinamica contestuale (client-side, non persistita) | Zustand + derivation |
| D4 | Session:Game = 1:1 (revised β') | **Session esistente = partita singola** |
| D4bis | Multi-game via `GameNightEvent` esistente in modalità ad-hoc | Riuso BC `GameManagement` |
| D5 | GameNight implicito, Session primaria user-facing | UI mostra "Partita"; "Serata" emerge solo al secondo game |
| D6 | Diary per-Session + UNION query per GameNight | `SessionEvent.GameNightId` già supporta |
| D7 | Agent = quello già auto-creato al PDF ready | Nessuna clonazione aggiuntiva |
| D8 | Lifecycle `Active↔Paused`, `Finalized` terminale | Già implementato su Session |
| D8bis | Invariante: per `(userId, gameNightEventId)` max 1 Session Active | Nuova business rule, enforced lato command |
| D9 | KB ready = `∃ PdfDocument.ProcessingState=Ready ∧ ∃ VectorDocument (IndexingStatus="completed")`, `Failed` non blocca ma warn. ⚠️ **Corretto in sede implementativa**: `VectorDocument` è 1 per PDF, non per chunk | Nuova query |
| D10 | RNG server-side crittografico (già implementato), seed per turn_order random | Esiste per dadi; da aggiungere per shuffle turn order |

---

## 4. Domain Model Changes

### 4.1 Nuovo flusso ad-hoc su `GameNightEvent`

Aggiunta al file `GameNightEvent.cs` (BC `GameManagement`):

```csharp
// Nuovo enum value (o flag)
public enum GameNightStatus {
    Draft = 0,
    Published = 1,
    InProgress = 2,    // NEW: ad-hoc serata in corso
    Completed = 3,
    Cancelled = 4
}

// Nuova factory
public static GameNightEvent CreateAdHoc(
    Guid organizerId,
    string title,            // "Serata del 09/04 20:00" auto-generato
    Guid firstGameId)
{
    var evt = new GameNightEvent(
        Guid.NewGuid(),
        organizerId,
        title,
        scheduledAt: DateTimeOffset.UtcNow,
        gameIds: [firstGameId]
    );
    evt.Status = GameNightStatus.InProgress;  // salta Draft/Published
    return evt;
}

// Nuovo metodo per promuovere da "single session" a "ad-hoc night"
public void AttachAdditionalGame(Guid gameId) {
    if (Status != GameNightStatus.InProgress)
        throw new ConflictException(...);
    if (!GameIds.Contains(gameId))
        GameIds.Add(gameId);
}
```

### 4.2 Regola invariante nuova: `1 Active per GameNightEvent`

Enforced a livello **command handler**, non entity:

```csharp
// StartSessionCommandHandler pseudocode
if (request.GameNightEventId.HasValue)
{
    var activeSessions = await _sessionRepo.GetActiveByGameNightAsync(request.GameNightEventId.Value);
    if (activeSessions.Any())
        throw new ConflictException("ACTIVE_SESSION_EXISTS_IN_NIGHT", new { sessionIds = activeSessions });
}
```

Client deve chiamare prima `PauseSessionCommand` sulla sessione corrente, poi riprovare.

### 4.3 Nuovo Command `StartSessionCommand` (sostituisce eventuali entry point esistenti)

```csharp
public record StartSessionCommand(
    Guid UserId,
    Guid GameId,
    Guid? GameNightEventId,      // null = crea o riusa night implicita
    string? SessionLocation,
    List<string> InitialGuestNames  // opzionale, aggiungibili dopo
) : IRequest<StartSessionResult>;

public record StartSessionResult(
    Guid SessionId,
    Guid GameNightEventId,        // sempre valorizzato (implicit se serve)
    bool GameNightWasCreated,     // true se è stato creato new
    Guid AgentId,                 // dal PrivateGame del gioco
    Guid ToolkitId                // dal GetActiveToolkitQuery
);
```

**Handler pseudocode**:
```
1. Validate: game in user library
2. Validate: KB ready (GetKbReadinessQuery) → else 422 KB_NOT_READY
3. Resolve GameNightEventId:
   a. If provided and exists: verify InProgress status, verify no other Active Session
   b. If null: check if user has a GameNightEvent in InProgress with state < 1h old
      - If yes: attach to that (AttachAdditionalGame if needed)
      - If no: create new AdHoc
4. Session.Create(userId, gameId, SessionType.GameSpecific, location)
5. Add guest players as Participants
6. Create GameNightSession link (PlayOrder = count + 1)
7. Emit SessionEvent(session_created) with GameNightId populated
8. Return StartSessionResult
```

### 4.4 Nuovo Command `SetTurnOrderCommand`

```csharp
public record SetTurnOrderCommand(
    Guid SessionId,
    TurnOrderMethod Method,       // Manual | Random
    List<Guid>? ManualOrder       // richiesto se Method=Manual
) : IRequest<TurnOrderResult>;

public enum TurnOrderMethod { Manual, Random }
```

**Handler**:
- Random: genera seed (`Guid.NewGuid().GetHashCode()`), Fisher-Yates shuffle dei `Participant.Id`
- Persistenza ordine: nuovo campo `Session.TurnOrderJson: string?` (array JSON di ParticipantId) + `Session.TurnOrderMethod: string?` + `Session.TurnOrderSeed: int?`
- Emit `SessionEvent(turn_order_set)` payload `{method, seed?, participantIds[]}`

### 4.5 Nuovo Command `RollDiceInSessionCommand`

`DiceRoll` entity esiste già con factory `Create(sessionId, participantId, formula, label?)`. Serve solo:
- Command handler che chiama `DiceRoll.Create(...)`, persiste, emette `SessionEvent(dice_rolled)`
- **Endpoint HTTP** (oggi manca): `POST /api/v1/sessions/{id}/dice-rolls`

### 4.6 Nuovo Command `UpdateScoreCommand`

```csharp
public record UpdateScoreCommand(
    Guid SessionId,
    Guid ParticipantId,
    decimal NewValue,
    int? RoundNumber,
    string? Category,
    string? Reason
) : IRequest;
```

**Handler**:
1. Cerca `ScoreEntry` esistente per `(sessionId, participantId, roundNumber, category)`
2. Se esiste: leggi `oldValue = entry.ScoreValue`, poi `entry.UpdateScore(newValue)`
3. Se non esiste: `ScoreEntry.Create(...)`, `oldValue = 0` (o null se category è opzionale)
4. Emit `SessionEvent(score_updated)` con payload `{participantId, oldValue, newValue, roundNumber?, category?, reason?}`
5. **Invariante storia**: la modifica della `ScoreEntry.ScoreValue` è OK perché la storia vive nel Diario (SessionEvent è append-only)

> ⚠️ **Decisione OQ-2**: storia modifiche punteggi vive nel **Diario (SessionEvent)**, non in append-only di `ScoreEntry`. `ScoreEntry` resta mutable (è una proiezione). Undo = nuovo `UpdateScoreCommand` con valore precedente (che genera un altro `SessionEvent`).

### 4.7 KB Readiness Query

```csharp
public record GetKbReadinessQuery(Guid GameId) : IRequest<KbReadinessResult>;

public record KbReadinessResult(
    bool IsReady,
    string State,              // PdfProcessingState enum values (Uploaded, Extracting, ..., Ready, Failed) + "None" / "PartiallyReady" / "VectorPending" aggregates
    int VectorDocumentCount,
    int FailedPdfCount,
    string[] Warnings
);
```

Logica: `IsReady = ∃ PdfDocument.ProcessingState == PdfProcessingState.Ready ∧ ∃ VectorDocument(pdfId) con IndexingStatus=="completed"`. `FailedPdfCount > 0` produce warning ma non blocca. Nota: `VectorDocumentEntity` ha UNIQUE su `PdfDocumentId` (1 row per PDF, campo `ChunkCount`/`IndexingStatus`), quindi si usa `.Any(...)` non `.Count(...)`.

---

## 5. Diary Events Schema

Riuso `SessionEvent` esistente. Nuovi `EventType` (string, max 50 chars):

| eventType | payload | source |
|---|---|---|
| `session_created` | `{gameId, gameName, gameNightEventId, wasNightCreated}` | `system` |
| `session_paused` | `{}` | `system` |
| `session_resumed` | `{}` | `system` |
| `session_finalized` | `{winnerId?, durationSeconds}` | `system` |
| `participant_added` | `{participantId, displayName, joinOrder}` | `user` |
| `participant_removed` | `{participantId, displayName}` | `user` |
| `turn_order_set` | `{method, seed?, participantIds[]}` | `user` |
| `turn_advanced` | `{fromParticipantId, toParticipantId, fromIndex, toIndex}` | `user` |
| `dice_rolled` | `{diceRollId, formula, rolls[], modifier, total, label?, participantId}` | `user` |
| `score_updated` | `{participantId, oldValue, newValue, roundNumber?, category?, reason?}` | `user` |
| `note_added` | `{sessionNoteId, text}` | `user` |
| `agent_query` | `{question, responseSummary, agentId}` | `user` |
| `agent_downgrade` | `{fromModel, toModel, reason}` | `system` |
| `gamenight_created` | `{gameNightEventId, title, isAdHoc}` | `system` |
| `gamenight_game_added` | `{gameNightEventId, addedGameId}` | `system` |
| `gamenight_completed` | `{durationSeconds, sessionCount}` | `system` |

Tutti gli eventi di una Session appartenente a un GameNight hanno `GameNightId` valorizzato.

---

## 6. API Endpoints

Tutti tramite CQRS `IMediator.Send()` (CLAUDE.md rule).

### 6.1 Session start & lifecycle

```
POST   /api/v1/sessions
       Body: { gameId: Guid, gameNightEventId?: Guid, location?: string, guestNames?: string[] }
       Precondition: KB ready per gameId, user owns game, no active session in night
       → 201 { sessionId, gameNightEventId, gameNightWasCreated, agentId, toolkitId }
       → 422 KB_NOT_READY { state, vectorDocumentCount }
       → 409 ACTIVE_SESSION_EXISTS_IN_NIGHT { existingSessionIds[] }
       → 404 GAME_NOT_IN_LIBRARY

GET    /api/v1/sessions/{id}
       → 200 { session (con participants), game, agent, kb (ready state), toolkit, gameNightEvent? }

POST   /api/v1/sessions/{id}/pause          → 200
POST   /api/v1/sessions/{id}/resume         → 200   (auto-pausa altre Active in GameNight)
POST   /api/v1/sessions/{id}/finalize
       Body: { winnerId?: Guid }
       → 200

GET    /api/v1/sessions/current
       → 200 { session? }        // ultima Active/Paused dell'utente
```

### 6.2 Participants (guest-friendly)

```
POST   /api/v1/sessions/{id}/participants
       Body: { displayName: string }   // guest: no UserId
       → 201 { participantId, displayName, joinOrder }

DELETE /api/v1/sessions/{id}/participants/{pid}
       → 204

PUT    /api/v1/sessions/{id}/turn-order
       Body: { method: "manual" | "random", order?: Guid[] }
       → 200 { method, seed?, order: Guid[] }
```

### 6.3 Diary-generating actions

```
POST   /api/v1/sessions/{id}/dice-rolls   ← NUOVO endpoint (command esisteva, endpoint no)
       Body: { formula: string, label?: string, participantId: Guid }
       → 201 { diceRollId, formula, rolls, modifier, total, timestamp }

POST   /api/v1/sessions/{id}/scores
       Body: { participantId, newValue, roundNumber?, category?, reason? }
       → 200 { scoreEntryId, oldValue, newValue }

POST   /api/v1/sessions/{id}/notes
       Body: { participantId, text }
       → 201 { sessionNoteId }
```

### 6.4 Diary queries

```
GET    /api/v1/sessions/{id}/diary?eventTypes=&since=&limit=&cursor=
       → 200 { entries: SessionEventDto[], nextCursor? }

GET    /api/v1/game-nights/{id}/diary?...
       → 200 { entries: [] }   // UNION SessionEvent WHERE GameNightId = {id}

GET    /api/v1/sessions/{id}/scoreboard
       → 200 { entries: [{participantId, displayName, rounds: {}, categories: {}, total}] }
```

### 6.5 KB & Game meta

```
GET    /api/v1/games/{id}/kb-readiness
       → 200 { isReady, state, vectorDocumentCount, failedPdfCount, warnings[] }
```

### 6.6 GameNight (ad-hoc)

```
GET    /api/v1/game-nights/{id}
       → 200 { gameNightEvent (con sessions[]), diarySummary }

POST   /api/v1/game-nights/{id}/complete
       → 200   // marca tutte le Session come Finalized, GameNight → Completed
```

---

## 7. User Flows (Gherkin)

```gherkin
Feature: Session flow con KB ready, diary e multi-game ad-hoc

  Background:
    Given sono autenticato come "Marco"
    And ho in libreria "Terraforming Mars" con KB isReady=true e Agent auto-creato
    And il Toolkit default di TM è auto-creato

  Scenario: Avvio prima Session (crea GameNight ad-hoc implicita)
    Given nessun GameNight in InProgress per Marco
    When POST /api/v1/sessions { gameId: TM }
    Then viene creato GameNightEvent in stato InProgress con title auto-generato
    And viene creata Session con status=Active, gameId=TM
    And Participant "Owner" (Marco) viene aggiunto automaticamente
    And GameNightSession link è creato con PlayOrder=1
    And SessionEvent: session_created (con GameNightId valorizzato)
    And SessionEvent: gamenight_created (source=system)
    And response: { sessionId, gameNightEventId, gameNightWasCreated: true, agentId, toolkitId }

  Scenario: KB non pronta blocca avvio
    Given "Root" ha PdfDocument.ProcessingState=Extracting e nessun VectorDocument (IndexingStatus="completed")
    When POST /api/v1/sessions { gameId: Root }
    Then response 422 con body { code: "KB_NOT_READY", state: "Extracting" }
    And nessuna Session viene creata
    And nessun SessionEvent viene emesso

  Scenario: Aggiunta guest player (stringa libera)
    Given Session sess1 Active
    When POST /api/v1/sessions/sess1/participants { displayName: "Luca" }
    Then Participant viene creato con UserId=null, DisplayName="Luca", JoinOrder=2
    And SessionEvent: participant_added { participantId, displayName: "Luca", joinOrder: 2 }

  Scenario: Turn order random riproducibile nell'audit
    Given Session sess1 con 3 Participants [Marco, Luca, Sara]
    When PUT /api/v1/sessions/sess1/turn-order { method: "random" }
    Then il server genera seed, applica Fisher-Yates con quel seed
    And Session.TurnOrderJson contiene i 3 ParticipantId ordinati
    And SessionEvent: turn_order_set { method: "random", seed: <int>, participantIds: [...] }

  Scenario: Tiro dado server-side
    Given Session sess1 Active, Luca è partecipante
    When POST /api/v1/sessions/sess1/dice-rolls { formula: "2d6", participantId: lucaId }
    Then DiceRoll entity viene persistita con RNG crittografico
    And response: { diceRollId, formula: "2D6", rolls: [a,b], modifier: 0, total: a+b, timestamp }
    And SessionEvent: dice_rolled { diceRollId, formula: "2D6", rolls: [a,b], total, participantId }

  Scenario: Aggiornamento punteggio con storia nel Diario
    Given Session sess1, Luca ha ScoreEntry con ScoreValue=0, roundNumber=1
    When POST /api/v1/sessions/sess1/scores { participantId: lucaId, newValue: 45, roundNumber: 1 }
    Then ScoreEntry.ScoreValue diventa 45
    And SessionEvent: score_updated { participantId, oldValue: 0, newValue: 45, roundNumber: 1 }
    When POST /api/v1/sessions/sess1/scores { participantId: lucaId, newValue: 52, roundNumber: 1, reason: "correzione" }
    Then ScoreEntry.ScoreValue diventa 52
    And SessionEvent: score_updated { oldValue: 45, newValue: 52, reason: "correzione" }
    And GET /diary mostra entrambe le entries ordinate

  Scenario: Seconda partita stessa serata
    Given Session sess1(TM) Active in GameNight gn1
    When POST /api/v1/sessions { gameId: Azul, gameNightEventId: gn1 }
    Then response 409 ACTIVE_SESSION_EXISTS_IN_NIGHT { existingSessionIds: [sess1] }
    When client chiama POST /api/v1/sessions/sess1/pause
    Then sess1.Status = Paused, SessionEvent: session_paused
    When client re-invoca POST /api/v1/sessions { gameId: Azul, gameNightEventId: gn1 }
    Then sess2 creata Active, GameNightEvent.GameIds aggiorna con Azul
    And GameNightSession link sess2 con PlayOrder=2
    And SessionEvent: gamenight_game_added { addedGameId: Azul }

  Scenario: Riprendi Session in pausa
    Given sess1 Paused, sess2 Active, entrambe in gn1
    When POST /api/v1/sessions/sess1/resume
    Then sess2 auto-pausata (SessionEvent: session_paused)
    And sess1.Status = Active (SessionEvent: session_resumed)
    And invariante: count(Active) nella gn1 = 1

  Scenario: Diario della serata (UNION cross-session)
    Given gn1 con sess1(TM, completed) e sess2(Azul, active), ~50 eventi totali
    When GET /api/v1/game-nights/gn1/diary
    Then response contiene tutti e 50 gli eventi ordinati cronologicamente
    And ogni entry è taggata con sessionId e GameNightId=gn1

  Scenario: Chiusura serata
    Given gn1 con sess1 Finalized, sess2 Active
    When POST /api/v1/game-nights/gn1/complete
    Then sess2 viene Finalized automaticamente
    And gn1.Status = Completed
    And SessionEvent: gamenight_completed { sessionCount: 2, durationSeconds }
```

---

## 8. Contextual Hand (Frontend)

### 8.1 Principio

**La Hand NON è persistita**. È una proiezione client-side derivata da `GET /api/v1/sessions/current`.

### 8.2 Stati contestuali

| Contesto | Cards visibili | Sorgente |
|---|---|---|
| **Idle** (nessuna Session Active/Paused) | 0 cards, CTA "Avvia partita" con game picker | `/sessions/current` → null |
| **Session Active/Paused** | `[Session, Game, KB, Agent, Toolkit]` + quick actions | `/sessions/{id}` response composito |
| **GameNight con N Session** | Sopra le card: selettore orizzontale GameNightSession list + "+ Nuova partita" | `/game-nights/{id}` |

### 8.3 Layout

Riuso tokens e shell design da `docs/frontend/my-hand-spec.md` v1 (design tokens, sidebar desktop, bottom bar mobile). Solo la **fonte dei dati** cambia.

**Desktop**: sidebar destra 280px. Ogni slot è una `MeepleCard` (compact variant, entity type corretto).
**Mobile**: bottom bar 64px collapsed, tap espande in sheet.

### 8.4 Quick actions per card

| Card | Action | Endpoint |
|---|---|---|
| Session | Pausa/Riprendi | `POST /sessions/{id}/pause|resume` |
| Session | Scoreboard (inline panel) | `GET /sessions/{id}/scoreboard` |
| Session | Note rapida | `POST /sessions/{id}/notes` |
| Game | Vai al gioco | navigate `/library/games/{id}` |
| KB | Vedi stato | `GET /games/{gameId}/kb-readiness` |
| Agent | Chiedi (chat panel) | SSE `/agents/{id}/chat` (esistente) |
| Toolkit | Tira dado | `POST /sessions/{id}/dice-rolls` |
| Toolkit | Avanza turno | `POST /sessions/{id}/turn/advance` (da implementare) |

### 8.5 Persistenza "ultima attività"

`localStorage["meeple.lastActiveSessionId"]` aggiornato a ogni render con Session Active/Paused. Al login/reload, se presente, frontend fa redirect a `/sessions/{id}`.

---

## 9. Non-Functional Requirements

| ID | Requirement | Metric |
|---|---|---|
| NFR-1 | `StartSessionCommand` p95 | < 500ms (KB readiness cached) |
| NFR-2 | `RollDiceCommand` p95 | < 150ms |
| NFR-3 | `UpdateScoreCommand` p95 | < 150ms |
| NFR-4 | Diary event order | `Timestamp` monotono via `TimeProvider`, no gap |
| NFR-5 | RNG fairness | Chi-square distribution test per `RollDice` |
| NFR-6 | Optimistic concurrency | `Session.RowVersion` (già presente) catch `DbUpdateConcurrencyException` → 409 |
| NFR-7 | Observability metriche | `session.started`, `session.paused`, `session.finalized`, `dice.rolled`, `score.updated`, `diary.entries.written`, `gamenight.created`, `gamenight.completed` con tag `eventType` |
| NFR-8 | Agent LLM failure | SSE sanitized (PR #5249/#5250), `agent_downgrade` entry nel Diary |
| NFR-9 | Session orphan recovery | `GET /sessions/current` per restore post-crash client |
| NFR-10 | DB migration impact | Solo 3 campi nuovi: `Session.TurnOrderJson?`, `Session.TurnOrderMethod?`, `Session.TurnOrderSeed?` (+ eventuale `GameNightStatus.InProgress` enum value) — **zero breaking changes** |

---

## 10. Open Questions — Risolte

| # | Question | Resolution |
|---|---|---|
| OQ-1 | `GameToolbox` vs `GameToolkit` | Risolto: sono BC **complementari**, non duplicati. v2.1 usa solo `GameToolkit.Toolkit` (dashboard) via `GetActiveToolkitQuery`. `GameToolbox.SharedContext` non serve. Consolidamento/rinomina fuori scope |
| OQ-2 | Score: optimistic concurrency / append-only? | **Decisione**: `ScoreEntry` resta mutable (proiezione), storia vive nel `SessionEvent` Diary append-only. `Session.RowVersion` già in place per concurrency |
| OQ-3 | SSE diary stream per v2.1 iniziale? | **Deferred a v2.2**. Mono-utente, reload manuale/polling 30s sufficiente. Endpoint SSE non nel primo plan |
| OQ-4 | Agent cloning interaction con flussi esistenti | **Non si clona**. `Agent` è già auto-creato al PDF ready. `StartSessionCommand` legge `PrivateGame.AgentDefinitionId` e lo passa nella response |
| OQ-5 | `currentTurnIndex` persistito o derivato? | **Persistito** in `Session.CurrentTurnIndex: int?` (nuovo campo) per query semplici; in alternativa ultimo `SessionEvent(turn_advanced)` come proiezione. Decisione per semplicità: persistito |

---

## 11. Gap Analysis — Cosa va davvero implementato

| # | Gap | Tipo | Effort |
|---|---|---|---|
| G1 | `StartSessionCommand` + handler + validator + endpoint POST `/sessions` con logica ad-hoc GameNight | Backend — core | M |
| G2 | `GameNightStatus.InProgress` enum value + `CreateAdHoc` factory + `AttachAdditionalGame` method | Backend — extension | S |
| G3 | `GetKbReadinessQuery` + handler + endpoint GET `/games/{id}/kb-readiness` | Backend — new query | S |
| G4 | `SetTurnOrderCommand` + handler + endpoint + campi `Session.TurnOrder{Json,Method,Seed,CurrentIndex}` + migration | Backend — core | M |
| G5 | `RollDiceInSessionCommand` handler (entity esiste) + endpoint POST `/sessions/{id}/dice-rolls` | Backend — wrap existing | S |
| G6 | `UpdateScoreCommand` handler che emette `SessionEvent(score_updated)` con oldValue | Backend — extension | S |
| G7 | `GetSessionDiaryQuery` + `GetGameNightDiaryQuery` (UNION) + endpoints | Backend — queries | S |
| G8 | `PauseSessionCommand` + `ResumeSessionCommand` con invariante 1 Active per GameNight (handler level) | Backend — extension | S |
| G9 | `AdvanceTurnCommand` + endpoint + `SessionEvent(turn_advanced)` | Backend — new | S |
| G10 | `FinalizeSessionCommand` emission + `CompleteGameNightCommand` cascade | Backend — extension | S |
| G11 | Frontend: `useContextualHandStore` Zustand derivato da `/sessions/current` + `/sessions/{id}` | Frontend — core | M |
| G12 | Frontend: `<ContextualHandSidebar>` + `<ContextualHandBottomBar>` componenti | Frontend — UI | M |
| G13 | Frontend: pagina `/sessions/[id]` con MeepleCard layout, quick actions, diary panel | Frontend — page | L |
| G14 | Frontend: pagina `/game-nights/[id]` con lista Session, diary UNION | Frontend — page | M |
| G15 | Frontend: game picker dialog con KB readiness check, disabilitazione giochi non pronti | Frontend — UI | S |
| G16 | Test: unit (domain transitions, RNG fairness), integration (commands), E2E (happy path completo, rivincita, pausa/riprendi) | All — QA | L |

**Totale stimato**: ~10 unità backend + ~5 frontend (dimensioni relative, non tempi assoluti).

---

## 12. Differences from v2.0

| Aspetto | v2.0 | v2.1 |
|---|---|---|
| Aggregate root | Nuovo `Session` con `GamePlay[]` | Riuso `Session` esistente (partita singola) |
| Multi-game container | Nuovo `Session` concept | Riuso `GameNightEvent` con nuovo flow ad-hoc |
| Diary entity | Nuovo `DiaryEntry` | Riuso `SessionEvent` (`GameNightId?` già presente) |
| Agent auto-creation | Nuovo template + clone | Riuso auto-creation esistente (PDF ready trigger) |
| Dice roll | Nuovo `RollDiceCommand` + entity | Riuso `DiceRoll` entity + command esistenti, manca solo endpoint |
| Score history | Nuovo `score_updated` event sourcing | `ScoreEntry` mutable + storia in `SessionEvent` Diary |
| BC footprint | Nuovo BC GamePlay | Extensions a SessionTracking + GameManagement |
| Migration impact | 3-4 settimane refactor | **~3 campi aggiunti a Session** + nuovo enum value |
| Effort stimato | L+ | M |

---

## 13. Definition of Done

- [ ] Tutti i 16 gap (§11) implementati
- [ ] Migration `AddSessionTurnOrderFields` + `AddGameNightInProgressStatus` generate e testate
- [ ] Coverage: ≥90% backend, ≥85% frontend (CLAUDE.md)
- [ ] Tutti gli scenari Gherkin §7 passano come E2E Playwright
- [ ] Observability NFR-7 verificata
- [ ] KB readiness integrato nel game picker
- [ ] `docs/frontend/my-hand-spec.md` cancellato o marcato definitivamente obsoleto
- [ ] `docs/superpowers/plans/2026-04-09-my-hand-feature.md` cancellato
- [ ] `docs/superpowers/specs/2026-04-09-session-gameplay-flow-v2.md` cancellato
- [ ] ADR scritta in `docs/architecture/adr/` per "Session Flow + Contextual Hand + Ad-hoc GameNight" che cita v2.1 come autoritativa
- [ ] Plan eseguibile derivato via `superpowers:writing-plans`

---

## 14. Next Step

Generare plan eseguibile da questa spec usando `superpowers:writing-plans`. Il plan dovrebbe essere strutturato in ~8-10 task con dipendenze, partendo da:

1. Task 1: Schema changes (migration `Session.TurnOrder*` fields, `GameNightStatus.InProgress`)
2. Task 2: `GameNightEvent.CreateAdHoc` + `AttachAdditionalGame` + tests
3. Task 3: `GetKbReadinessQuery` + endpoint
4. Task 4: `StartSessionCommand` (cuore della feature) + tests
5. Task 5: `PauseSessionCommand` + `ResumeSessionCommand` + invariante
6. Task 6: `SetTurnOrderCommand` + `RollDiceInSessionCommand` endpoint + `UpdateScoreCommand`
7. Task 7: Diary queries (session + game-night UNION)
8. Task 8: Frontend Zustand store + ContextualHand components
9. Task 9: Session page + GameNight page
10. Task 10: E2E Playwright + observability wiring
