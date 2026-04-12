# Session + GamePlay + Contextual Hand + Diary

**Feature Specification v2.0**
**Data**: 2026-04-09
**Status**: ⚠️ **SUPERSEDED** by `2026-04-09-session-flow-v2.1.md`

> **Nota (2026-04-09)**: Dopo ricognizione codebase è emerso che il modello `Session : GamePlay = 1:N` proposto qui duplicherebbe un BC intero (`SessionTracking` + `GameNightEvent` già presenti in `GameManagement`). La v2.1 riformula la feature come extension dell'esistente, con scope 10x più piccolo e zero breaking changes.
**Supersedes**: `docs/frontend/my-hand-spec.md` (v1.0, freeze)
**Related**: `docs/superpowers/plans/2026-04-09-my-hand-feature.md` (plan v1 on-hold)
**Bounded Contexts toccati**: `SessionTracking` (primary), `KnowledgeBase`, `GameManagement`, `GameToolbox`/`GameToolkit`, `UserLibrary`

---

## 1. Vision

Un utente con una libreria di giochi con KB indicizzate deve poter:

1. Avviare una **Partita** (GamePlay) su un gioco in un click
2. Avere automaticamente in mano le card contestuali (KB, Agent, Toolkit, Partita)
3. Aggiungere giocatori al tavolo (stringhe libere, no account)
4. Determinare ordine di turno (manuale o random auditato)
5. Tirare dadi dal Toolkit e registrare ogni tiro nel Diario
6. Aggiornare punteggi con storia modifiche nel Diario
7. Mettere in pausa la partita per iniziarne un'altra dello stesso gioco o diverso, formando così una **Serata** (Session) che raggruppa più partite

La feature è **mono-utente**: un solo account autenticato (l'host) opera l'applicazione, gli altri giocatori al tavolo guardano lo schermo o passano il device. Nessun real-time multi-client.

---

## 2. Glossary

| Termine | Definizione |
|---|---|
| **Session** | Contenitore "serata di gioco". Aggrega 1..N GamePlay. Nasce **implicita** (flag `isImplicit=true`) quando l'utente avvia la prima partita; diventa esplicita al secondo GamePlay aggiunto. |
| **GamePlay** (UI: "Partita") | Singola istanza giocata di un gioco. Ha lifecycle proprio, scoreboard propria, appartiene a una Session. |
| **Guest Player** | Giocatore al tavolo rappresentato da una stringa libera (es. "Luca"). Non è un `User` nel sistema. Persiste solo nell'array `players` del GamePlay e come `actor` nel Diario. |
| **Hand** (UI: "La Mia Mano") | Barra contestuale dinamica di card visibile nella UI. Il set di card dipende dal contesto corrente (nessun gameplay / gameplay attivo / gameplay in pausa). **Non persistita come aggregato dominio**: è derivata client-side dallo stato corrente. |
| **Card** | Rappresentazione visuale di un'entità rilevante (Game, KB, Agent, Toolkit, GamePlay). Ogni card ha quick actions. |
| **Diary** | Event log append-only per Session. Ogni entry tagga il `gamePlayId` di contesto. Le scoreboard sono proiezioni del Diario. |
| **Agent Template** | Seed data system-wide (`SystemDefaultAgentTemplate`) con prompt di default e model config. Clonato in `Agent` per (user, game) alla prima partita. |
| **KB Ready** | `∃ PdfDocument(gameId) con ProcessingState=Indexed ∧ count(VectorDocument) > 0`. PDF `Failed` non bloccano ma generano warning. |

---

## 3. Domain Model

### 3.1 Aggregates

#### Session (aggregate root, BC `SessionTracking`)
```
Session {
  id: Guid
  userId: Guid                  // owner (l'host)
  name: string                  // auto-generato se implicit, editabile dopo
  isImplicit: bool              // true finché ha ≤ 1 GamePlay
  startedAt: DateTime
  endedAt: DateTime?
  gamePlays: GamePlay[]
  state: "Active" | "Ended"
}
```

**Invarianti**:
- `count(gamePlays where state=Active) ≤ 1`
- `isImplicit = true ⟹ count(gamePlays) ≤ 1`
- `endedAt != null ⟺ state=Ended`
- Un `Session` Ended non accetta nuovi GamePlay

#### GamePlay (entità figlia)
```
GamePlay {
  id: Guid
  sessionId: Guid               // required, FK
  gameId: Guid                  // FK to SharedGameCatalog
  agentId: Guid                 // FK (clonato da template al create)
  toolkitRef: ToolkitRef        // snapshot dei tool disponibili (vedi 3.3)
  state: "Setup" | "Active" | "Paused" | "Completed" | "Abandoned"
  players: string[]             // guest names, ordine = turn order
  turnOrderMethod: "manual" | "random" | null
  turnOrderSeed: Guid?          // seed RNG se method=random, per audit
  currentTurnIndex: int?        // indice in players[] del giocatore corrente
  startedAt: DateTime
  pausedAt: DateTime?
  endedAt: DateTime?
}
```

**Transizioni di stato**:
```
Setup → Active          (startGamePlay, richiede players.Count ≥ 1 e turn order definito)
Active → Paused         (pauseGamePlay, auto-triggerata se apri altro GamePlay in Session)
Paused → Active         (resumeGamePlay, auto-pausa eventuale Active corrente)
Active → Completed      (completeGamePlay)
Active → Abandoned      (abandonGamePlay)
Paused → Completed      (consentito: chiudere una partita in pausa)
Paused → Abandoned      (consentito)
Completed → *           ❌ terminale
Abandoned → *           ❌ terminale
```

#### DiaryEntry (value object, append-only)
```
DiaryEntry {
  id: Guid
  sessionId: Guid               // FK, required
  gamePlayId: Guid?             // null per eventi di pura sessione (es. rinomina)
  timestamp: DateTime           // server UTC
  actor: string                 // "Marco" | "Luca" | "system"
  eventType: DiaryEventType
  payload: JSONB                // shape per eventType, vedi §5
  correlationId: Guid?          // raggruppa eventi emessi insieme
}
```

**Invarianti**:
- Append-only: no update, no delete (undo = nuovo entry con valore opposto)
- Ordinamento cronologico per `(timestamp, id)`

### 3.2 Agent (BC `KnowledgeBase`)

```
Agent {
  id: Guid
  userId: Guid
  gameId: Guid                  // unique(userId, gameId) per agent "default"
  name: string
  kbId: Guid                    // link alla KB del gioco
  sourceTemplateId: Guid?       // SystemDefaultAgentTemplate.id, null se custom
  sourceTemplateVersion: int?   // versione del template al clone
  promptOverrides: string?
  modelConfigOverrides: JSONB?
  createdAt, updatedAt
}
```

**Regole**:
- Alla prima partita di Marco su TM: il sistema cerca `Agent where userId=Marco ∧ gameId=TM ∧ sourceTemplateId != null` → se non esiste, clona dal template
- Template upgrade non tocca agent clonati (eventuale migration = comando dedicato fuori scope)
- Marco può avere **1 solo agent "da template default"** per (user, game); può crearne di custom separati

### 3.3 Toolkit

Lo spec eredita la confusione `GameToolbox` vs `GameToolkit` da CLAUDE.md. **Decisione**: in questa feature il "Toolkit" è il set di tool operativi associati a un game (dadi, timer, note, scoreboard). Uso `GameToolkit` come BC autoritativo (da confermare con refactor separato).

```
ToolkitRef {
  toolkitId: Guid               // template del toolkit del gioco
  availableTools: ToolType[]    // ["dice", "timer", "notes", "scoreboard"]
  diceConfigs: DiceConfig[]     // es. [{formula: "2d6", label: "Tiro base"}]
}
```

Il `ToolkitRef` è uno **snapshot** al momento del create GamePlay: se l'admin aggiorna il toolkit template del gioco, le partite in corso non ne risentono.

---

## 4. Contextual Hand (UI state, NOT domain)

### 4.1 Modello concettuale

La "Mano" **non è un aggregato persistito**. È una proiezione client-side che deriva dal contesto corrente. Il backend non conosce il concetto di "card in mano": espone solo le entità (Session, GamePlay, Agent, KB, Toolkit) e il frontend le compone.

**Stati contestuali**:

| Contesto | Card visibili | Fonte |
|---|---|---|
| **Idle** (nessuna Session Active) | 0 card, mostra CTA "Avvia partita" con selezione game da libreria | — |
| **GamePlay Setup** | `[GamePlay, Game, KB, Agent, Toolkit]` | `GET /sessions/{sid}/gameplays/{gpid}` |
| **GamePlay Active** | `[GamePlay, Game, KB, Agent, Toolkit]` + quick actions abilitate (Tira, Score, Chiedi) | idem |
| **GamePlay Paused** | Stesse card ma con badge "In pausa", quick actions disabilitate tranne "Riprendi" | idem |
| **Session con più GamePlay** | Sopra le card del GamePlay corrente, comparsa di un **selettore Session** con lista GamePlay della serata + pulsante "+ Nuova partita" | `GET /sessions/{sid}` |

### 4.2 Transizioni

- **Avvio Partita**: idle → GamePlay Setup, card auto-popolate in Hand
- **Start gameplay**: Setup → Active, quick actions diventano live
- **Nuova partita nella serata**: se Session ha già 1 GamePlay Active → conferma auto-pausa → apre picker game → crea GamePlay2 → Hand si ripopola sulle card di GamePlay2
- **Riprendi da Paused**: selettore Session → clic su GamePlay Paused → conferma auto-pausa corrente → Hand ripopolata

### 4.3 Persistenza "ultima Hand"

Per UX: al reload della pagina, il frontend deve ricordare l'ultimo GamePlay attivo. **Implementazione**: `localStorage` con `lastActiveGamePlayId`. Al login/reload, SE esiste un GamePlay `Active` o `Paused` per l'utente, il frontend naviga automaticamente a quella pagina. Nessuna persistenza server della "Hand state".

---

## 5. Diary Events Schema

### 5.1 Event types

| `eventType` | `gamePlayId` | Payload | Emesso da |
|---|---|---|---|
| `session_created` | null | `{sessionId, name, isImplicit}` | `CreateSessionCommand` |
| `session_renamed` | null | `{oldName, newName}` | `RenameSessionCommand` |
| `session_ended` | null | `{reason}` | `EndSessionCommand` |
| `gameplay_created` | set | `{gameId, gameName}` | `StartGamePlayCommand` |
| `player_added` | set | `{playerName}` | `AddPlayerCommand` |
| `player_removed` | set | `{playerName}` | `RemovePlayerCommand` |
| `turn_order_rolled` | set | `{method, seed?, order: [names]}` | `SetTurnOrderCommand` |
| `gameplay_started` | set | `{}` | `StartGamePlayActivationCommand` |
| `gameplay_paused` | set | `{}` | `PauseGamePlayCommand` |
| `gameplay_resumed` | set | `{}` | `ResumeGamePlayCommand` |
| `gameplay_completed` | set | `{finalScoreboard}` | `CompleteGamePlayCommand` |
| `gameplay_abandoned` | set | `{reason?}` | `AbandonGamePlayCommand` |
| `turn_advanced` | set | `{fromIndex, toIndex}` | `AdvanceTurnCommand` |
| `dice_rolled` | set | `{formula, results: [int], total, seed}` | `RollDiceCommand` |
| `score_updated` | set | `{playerName, oldValue, newValue, reason?}` | `UpdateScoreCommand` |
| `note_added` | set | `{text}` | `AddNoteCommand` |
| `agent_query` | set | `{question, responseSummary}` | RAG agent chat handler |
| `agent_downgrade` | set | `{fromModel, toModel, reason}` | LLM fallback handler (PR #5249/#5250) |

### 5.2 Regole

- **Tutti** gli eventi `dice_rolled` sono server-side. Zero RNG client-side per la session. Seed = `Guid.NewGuid()` convertito in `long` e passato a `Random(seed)` per riproducibilità.
- `score_updated`: il payload contiene SEMPRE `oldValue` anche se null. Undo = inserire entry con `oldValue`/`newValue` scambiati.
- **Scoreboard corrente** = proiezione: per ogni `playerName` in `GamePlay.players`, prendi l'ultimo `score_updated` per quel player; se nessuno, score = 0.

---

## 6. API Endpoints

Tutti gli endpoint seguono CQRS via `IMediator.Send()` (vedi CLAUDE.md).

### 6.1 Session

```
POST   /api/v1/sessions
       Body: { name?: string }
       → 201 { sessionId, isImplicit: true }

GET    /api/v1/sessions/{id}
       → 200 { session: SessionDto (include gamePlays summary) }

PATCH  /api/v1/sessions/{id}
       Body: { name: string }
       → 200 { session }

POST   /api/v1/sessions/{id}/end
       → 200

GET    /api/v1/sessions/current
       → 200 { session? } // ultima Session Active dell'utente
```

### 6.2 GamePlay

```
POST   /api/v1/gameplays
       Body: { sessionId?: Guid, gameId: Guid }   // sessionId null = crea Session implicita
       Precondition: KB ready for gameId (else 422 KB_NOT_READY con dettagli stato)
       Side effects:
         - Se sessionId null → crea Session implicit
         - Se Session ha GamePlay Active → errore 409 ACTIVE_GAMEPLAY_EXISTS (client deve chiedere conferma e chiamare /pause prima)
         - Clone Agent default per (user, game) se non esiste
         - Snapshot ToolkitRef
         - Emit diary: session_created (se nuova), gameplay_created
       → 201 { gameplay: GamePlayDto, session: SessionDto, autoCreatedAgent: bool }

GET    /api/v1/gameplays/{id}
       → 200 { gameplay, game, agent, kb, toolkit }  // tutto ciò che serve alla Hand

POST   /api/v1/gameplays/{id}/players
       Body: { name: string }
       → 200

DELETE /api/v1/gameplays/{id}/players/{name}
       → 204

PUT    /api/v1/gameplays/{id}/turn-order
       Body: { method: "manual" | "random", order?: string[] }
       → 200 { order, seed? }

POST   /api/v1/gameplays/{id}/start           → 200   // Setup → Active
POST   /api/v1/gameplays/{id}/pause           → 200
POST   /api/v1/gameplays/{id}/resume          → 200   // auto-pausa Active corrente
POST   /api/v1/gameplays/{id}/complete        → 200
POST   /api/v1/gameplays/{id}/abandon
       Body: { reason?: string }
       → 200

POST   /api/v1/gameplays/{id}/turn/advance    → 200
```

### 6.3 Gameplay actions (Diary-generating)

```
POST   /api/v1/gameplays/{id}/dice-roll
       Body: { formula: string, actor: string }
       → 200 { results, total, entryId }

POST   /api/v1/gameplays/{id}/scores
       Body: { playerName: string, newValue: int, reason?: string }
       → 200 { entryId, oldValue, newValue }

POST   /api/v1/gameplays/{id}/notes
       Body: { actor: string, text: string }
       → 201
```

### 6.4 Diary

```
GET    /api/v1/sessions/{id}/diary?gamePlayId=&eventTypes=&since=&limit=
       → 200 { entries: DiaryEntryDto[], nextCursor? }

GET    /api/v1/gameplays/{id}/scoreboard
       → 200 { players: [{name, score, lastUpdated}] }   // proiezione
```

### 6.5 KB readiness check

```
GET    /api/v1/games/{id}/kb-readiness
       → 200 {
           isReady: bool,
           state: "None" | "Extracting" | "Indexing" | "Indexed" | "PartiallyIndexed" | "Failed",
           vectorCount: int,
           warnings: string[]
         }
```

---

## 7. User Flows (Gherkin)

```gherkin
Feature: Avvio partita con KB ready e diario

  Background:
    Given sono autenticato come "Marco"
    And ho in libreria "Terraforming Mars" con KB.isReady=true
    And esiste SystemDefaultAgentTemplate v1
    And non ho agent per (Marco, TM)

  Scenario: Avvio primo gameplay (crea Session implicita)
    When invoco POST /api/v1/gameplays con {gameId: TM}
    Then viene creata Session con isImplicit=true, name="Partita del 2026-04-09 20:00"
    And viene clonato Agent per (Marco, TM) da template v1
    And viene creato GamePlay con state=Setup, toolkitRef snapshot
    And il Diario contiene: session_created, gameplay_created
    And la response include autoCreatedAgent=true

  Scenario: KB non pronta blocca avvio
    Given "Root" ha KB.state=Extracting
    When invoco POST /api/v1/gameplays con {gameId: Root}
    Then ricevo 422 con body {code: "KB_NOT_READY", state: "Extracting"}
    And nessuna Session né GamePlay vengono creati

  Scenario: Aggiunta giocatori e turn order random
    Given GamePlay gp1 in state=Setup con players vuoto
    When aggiungo players [Marco, Luca, Sara]
    And invoco PUT /turn-order con method=random
    Then il sistema genera seed, shuffle i 3 nomi
    And GamePlay.turnOrder persiste l'ordine e turnOrderSeed
    And il Diario contiene turn_order_rolled{method:"random", seed, order}

  Scenario: Start gameplay e tiro dado
    Given gp1 in Setup con turn order definito
    When invoco POST /gameplays/gp1/start
    Then gp1.state = Active
    And Diario: gameplay_started
    When Luca (giocatore di turno) invoca dice-roll con formula="2d6", actor="Luca"
    Then ricevo results=[a,b], total=a+b
    And Diario: dice_rolled{formula:"2d6", results:[a,b], total, seed, actor:"Luca"}

  Scenario: Aggiornamento punteggio e storia
    Given gp1 Active, Luca ha scoreboard projection = 0
    When Marco invoca POST /scores con {playerName:"Luca", newValue:45}
    Then Diario: score_updated{playerName:"Luca", oldValue:0, newValue:45}
    When Marco corregge a 52
    Then Diario: score_updated{playerName:"Luca", oldValue:45, newValue:52}
    And GET /scoreboard → Luca=52

  Scenario: Seconda partita nella stessa serata
    Given gp1 Active
    When invoco POST /gameplays con {sessionId: sess1, gameId: Azul}
    Then ricevo 409 ACTIVE_GAMEPLAY_EXISTS con gp1 indicato
    When il client invoca POST /gameplays/gp1/pause
    And re-invoca POST /gameplays con {sessionId: sess1, gameId: Azul}
    Then viene creato gp2, Session.isImplicit diventa false
    And Diario: gameplay_paused(gp1), gameplay_created(gp2)

  Scenario: Riprendi partita in pausa
    Given gp1 Paused, gp2 Active
    When invoco POST /gameplays/gp1/resume
    Then gp2 diventa Paused
    And gp1 diventa Active
    And Diario: gameplay_paused(gp2), gameplay_resumed(gp1)

  Scenario: Seconda partita dello stesso gioco (rivincita)
    Given gp1(TM) Completed nella sessione sess1
    When invoco POST /gameplays con {sessionId: sess1, gameId: TM}
    Then viene creato gp2(TM) che riusa l'Agent esistente di Marco/TM
    And autoCreatedAgent=false
```

---

## 8. Non-Functional Requirements

| ID | Requisito | Metrica |
|---|---|---|
| NFR-1 | Avvio GamePlay end-to-end | p95 < 500ms (esclusa KB readiness se già cached) |
| NFR-2 | Dice roll latenza | p95 < 150ms |
| NFR-3 | Score update | p95 < 150ms |
| NFR-4 | Diary append ordinato senza gap | invariante: `timestamp` monotono per (sessionId) usando `TimeProvider` |
| NFR-5 | RNG audit completo | ogni `dice_rolled` e `turn_order_rolled` deve avere seed loggato e risultati riproducibili |
| NFR-6 | Concorrenza score | optimistic concurrency su scoreboard via `RowVersion` su `GamePlay` o tramite append-only (da decidere in implementazione) |
| NFR-7 | Observability | metriche: `gameplay.started`, `gameplay.completed`, `dice.rolled`, `score.updated`, `diary.entries.written` con tag `eventType` |
| NFR-8 | Failure LLM | se agent LLM fallisce durante `agent_query`, Diario registra `agent_downgrade` o errore classificato (no provider raw leak) |
| NFR-9 | Session orphan recovery | dopo crash del client, `GET /sessions/current` restituisce l'ultima Session Active, client ripristina |

---

## 9. Bounded Context Allocation

| Entità / Comando | BC | Note |
|---|---|---|
| `Session`, `GamePlay`, `DiaryEntry` | `SessionTracking` | aggregato root Session |
| `Agent`, `SystemDefaultAgentTemplate` | `KnowledgeBase` | clonazione interna al BC |
| `Game`, `SharedGame` | `GameManagement` / `SharedGameCatalog` | read-only per questa feature |
| `ToolkitRef` snapshot | `SessionTracking` (embedded) | riferimento a `GameToolkit` letto al create |
| KB readiness check | `KnowledgeBase` | nuova query `IsGameKbReadyQuery` |
| User library check | `UserLibrary` | precondition: gioco in libreria utente |

**Refactor suggerito (fuori scope di questa PR)**: consolidare `GameToolbox` e `GameToolkit` in un unico BC (tracker: creare issue dedicato).

---

## 10. Cosa NON si fa in questa spec

- ❌ Multi-user real-time (WebSocket tra device): scartato per decisione #2
- ❌ Persistenza server-side della "Hand": scartato per decisione #3 (dinamica, client-side)
- ❌ Session templating / serate ricorrenti
- ❌ Statistiche cross-session (es. "quante volte ho giocato TM questo mese")
- ❌ Export Diario PDF/CSV
- ❌ Permessi/ruoli (host/player/spectator): tutto host
- ❌ Undo/redo UX avanzato: undo disponibile solo inserendo nuovo entry inverso
- ❌ Agent template migration tool
- ❌ Refactor GameToolbox/GameToolkit

---

## 11. Test Plan

### Unit (Domain)
- `GamePlay` state transitions (ogni arco del lifecycle)
- Invariante `Session.count(Active) ≤ 1`
- Scoreboard projection: last-write-wins per playerName
- RNG determinismo: stesso seed → stessi risultati

### Integration (Testcontainers PostgreSQL)
- `StartGamePlayCommand`: crea Session implicita + clona Agent + snapshot Toolkit + 2 diary entry in unica transazione
- KB not ready → 422, nessun side effect
- Active gameplay esistente → 409 senza side effect
- Pause/resume: stato consistente su più GamePlay
- Diary append order: insert 1000 entries concorrenti → ordinamento stabile

### E2E (Playwright)
- Happy path completo: login → libreria → avvia TM → aggiungi 3 players → turn order random → start → 3 tiri dado → 2 score update → completa
- Rivincita: completa TM → avvia TM di nuovo nella stessa Session → verifica stesso Agent
- Pausa+riprendi: TM Active → nuovo gameplay Azul → torna a TM → verifica state
- KB non pronta: errore visibile con tooltip stato
- Reload mid-session: verifica restore automatico su ultimo GamePlay Active

### Property-based
- RNG fairness: 10k tiri `1d6`, chi-square test distribution

---

## 12. Migration from v1

Il plan v1 (`2026-04-09-my-hand-feature.md`) aveva implementato:
- `UserHandSlotEntity` con 4 slot fissi persistiti
- Endpoints `GET/PUT/DELETE /users/me/hand/{slotType}`
- Store Zustand + localStorage fallback
- Sidebar desktop + bottom bar mobile

**Azione**: il plan v1 è **congelato**. Non eseguire Task 1+ sulla persistenza `UserHandSlot`.

**Riuso possibile dai file v1**:
- Design tokens slot (colori, icone) → riusabili per la Hand contestuale v2
- Layout sidebar desktop / bottom bar mobile → riusabili, la differenza è che le card sono popolate dal contesto (GamePlay Active) invece che dallo store persistito
- Test patterns e struttura BC UserLibrary → NON riusabili, la feature si sposta a `SessionTracking`

**Cleanup**:
- Marcare `my-hand-spec.md` come superseded (già fatto da questa spec)
- Non creare migration `AddUserHandSlots` (Task 1 del plan v1)
- Eventuali branch esistenti: rebase o abandon

---

## 13. Open Questions

| # | Question | Impact | Target resolution |
|---|---|---|---|
| OQ-1 | `GameToolbox` vs `GameToolkit` consolidamento | Medium — decide da dove leggere `ToolkitRef` | Issue separato prima di implementazione |
| OQ-2 | Optimistic concurrency score via `RowVersion` o append-only? | Low — append-only è più elegante | Durante implementazione domain |
| OQ-3 | SSE diary stream necessario per la v2 iniziale? | Low — mono-utente, reload sufficiente | Defer a v2.1 |
| OQ-4 | Agent clonazione: interazione con esistenti flussi di creation agent? | Medium | Review con owner BC KnowledgeBase |
| OQ-5 | `currentTurnIndex` deve essere persistito o derivato dal Diario? | Low | Persistito per semplicità query |

---

## 14. Acceptance Criteria (Definition of Done)

- [ ] Tutte le 10 decisioni architetturali documentate sono implementate
- [ ] Schema DB con migration per `Session`, `GamePlay`, `DiaryEntry` (BC `SessionTracking`)
- [ ] Seed data `SystemDefaultAgentTemplate`
- [ ] Tutti gli endpoint §6 implementati con CQRS MediatR
- [ ] Coverage: ≥90% backend, ≥85% frontend
- [ ] Tutti gli scenari Gherkin §7 passano come E2E
- [ ] Osservabilità NFR-7 verificata con log e metriche visibili
- [ ] KB readiness check integrato in libreria (pulsante "Gioca" disabilitato se non ready)
- [ ] Plan v1 marcato frozen, v2 plan generato da questa spec via `superpowers:writing-plans`

---

**Next step**: validare questa spec con un secondo passaggio panel (`/sc:spec-panel --mode critique --focus requirements,architecture,testing`) oppure procedere a `superpowers:writing-plans` per derivare il plan eseguibile.
