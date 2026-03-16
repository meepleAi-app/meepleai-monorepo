# Game Night Improvvisata — Vertical Slice Design

**Date**: 2026-03-15
**Approach**: Vertical Slice — one complete game night, end-to-end
**Effort**: ~12-15 days
**Status**: Design approved

## Overview

A complete user journey from "friends arrive unexpectedly" to "save unfinished game for later". The user finds a game via BGG, adds it to their private library, uploads the rulebook PDF, gets an AI agent auto-created, and uses it during the game night for setup help, score tracking, rule arbitration, and session save/resume.

### Design Decisions (from brainstorming)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Entry point | Library → Add Game wizard | Reuses existing AddGameSheet multi-step wizard |
| Agent creation | Auto-create with personalization optional | New AutoCreateAgentOnPdfReadyHandler + good defaults |
| Session UI | CardStack with child cards | One Card = One Function, consistent with "Carte in Mano" |
| Guest access | Lightweight via link/QR (or email invite) | No login required, PIN + UUID token via SessionInvite |
| Rule disputes | Chat libre + official "Arbitro" mode | Casual questions + structured verdicts with citations |
| Session save | Structured state + agent memory + recap | AgentConversationSummary enables auto-recap on resume |
| Agent access | Host only | Guests see scores and propose, but don't query the agent |

---

## Section 1: Onboarding Flow — BGG → PrivateGame → PDF → Agent

### Existing Infrastructure
- `AddGameSheet` wizard: GameSourceStep → GameSearchResults → PdfUploadZone → KnowledgeBaseStep → SuccessState
- `ImportBggGameCommand` creates `PrivateGame` with tier enforcement
- `AcceptCopyrightDisclaimerCommand` for PDF copyright acceptance
- `AutoCreateAgentOnPdfReadyHandler` — referenced in cross-context integration tests but **not yet wired in production code**; needs to be created as a full handler

### Changes Required

#### Backend

**1. Create `AutoCreateAgentOnPdfReadyHandler`** *(Note: handler exists only in test stubs, must be fully implemented)*
- Subscribes to `PdfProcessingCompletedEvent` (or equivalent domain event from DocumentProcessing)
- Trigger for user PDFs associated with a PrivateGame
- Default agent prompt:
  ```
  Sei un assistente esperto per il gioco {GameName}. Aiuti i giocatori con setup,
  regole e punteggi. Cita sempre la pagina del regolamento quando rispondi su regole.
  Se una regola è ambigua, spiega le possibili interpretazioni.
  ```
- Links auto-created agent to PrivateGame's Toolkit

**2. `AgentAutoCreatedEvent` → Notification**
- New domain event `AgentAutoCreatedEvent(AgentDefinitionId, PrivateGameId, GameName)`
- Handler creates in-app `Notification` with deep link to `/library/private/{id}/toolkit`
- SSE push to connected user

**3. PDF Processing Failure Path** *(spec-panel fix: Wiegers #1)*
- Add `ProcessingStatus` enum: `Queued → Processing → Ready → Failed`
- On failure: create `Notification` with message "PDF processing failed for {GameName}. You can retry or upload a different file."
- Retry action: `RetryPdfProcessingCommand` re-queues the document
- Max 3 retries, then permanent failure with support contact suggestion

**4. `PdfProcessingProgressEvent`**
- SSE events during processing: `{ stage: "chunking" | "embedding" | "indexing" | "ready" | "failed", progress: 0-100 }`
- Frontend can show real-time progress without polling

#### Frontend

**1. Enhanced SuccessState**
- After "game added + PDF uploaded", show processing progress indicator
- Message: "Stiamo analizzando il regolamento... Ti avviseremo quando l'agente è pronto"
- Progress stages visualized: Chunking → Embedding → Indexing → Ready

**2. Notification Toast**
- On `AgentAutoCreatedEvent` via SSE: toast "🤖 Agente per {GameName} pronto!" with "Apri" action
- Click → navigates to PrivateGame card in CardStack with agent selectable

**3. Failure Recovery**
- On `PdfProcessingFailed`: toast "⚠️ Errore nel processing del PDF. Riprova?" with retry action

### Flow Diagram
```
Library → "Aggiungi Gioco" → BGG Search → Select → PrivateGame created
    ↓
Upload PDF → Copyright disclaimer → Accept
    ↓
[Background] Processing: chunking → embedding → indexing
    ├── Success → AutoCreateAgent → Notification "Agent ready!"
    └── Failure → Notification "Processing failed" → Retry option
    ↓
User receives toast → Click → PrivateGame card with Agent ready
```

---

## Section 2: Live Session — CardStack + Child Cards

### Existing Infrastructure
- `LiveGameSession` aggregate root with Status, Players, Teams, RoundScores, TurnRecords, GameState (JSON)
- `CreateLiveSessionCommand`, `AddPlayerToLiveSessionCommand`, `RecordLiveSessionScoreCommand`
- `GameStateHub` SignalR: JoinSession, BroadcastStateChange, ProposeScore → ConfirmScore
- `SessionInvite` with PIN 6-char + UUID link + max uses + expiry
- `liveSessionsClient` frontend CRUD

### Changes Required

#### Backend

**1. `StartImprovvisataSessionCommand`**
- Shortcut command that in a single transaction:
  - Creates `LiveGameSession` from `PrivateGame` (name, gameId, toolkitId)
  - Sets host = current user
  - Generates `SessionInvite` (session code + link, expires in 24h, max 10 uses)
  - Returns: `{ sessionId, inviteCode, shareLink }`
  - Note: QR code is generated **client-side** from the share link using `qrcode.react` (no backend QR library needed)

**2. Extend `SaveCompleteSessionStateCommand`**

> ⚠️ **IMPORTANT — Existing `SessionSnapshot` uses delta-based JSON Patch storage** (`DeltaDataJson`, `IsCheckpoint`, `SnapshotIndex`, `TriggerType`). Our save/resume use case needs full-state snapshots, not deltas. We create a **new entity `PauseSnapshot`** to avoid conflicting with the existing incremental snapshot model.

Creates `PauseSnapshot`:
  ```csharp
  public class PauseSnapshot : Entity  // owned by LiveGameSession
  {
      public Guid LiveGameSessionId { get; }
      public int CurrentTurn { get; }
      public string? CurrentPhase { get; }
      public List<PlayerScoreSnapshot> PlayerScores { get; }
      public List<Guid> AttachmentIds { get; }
      public List<RuleDisputeEntry> Disputes { get; }
      public string? AgentConversationSummary { get; }  // nullable — may arrive async
      public string? GameStateJson { get; }
      public DateTime SavedAt { get; }
      public Guid SavedByUserId { get; }                // host UserId, not player name
      public bool IsAutoSave { get; }
  }
  ```
- `PauseSnapshot` lives in **GameManagement** bounded context (same as `LiveGameSession`)
- Requires new EF migration: `PauseSnapshots` table with FK to `LiveGameSessions`
- `RuleDisputeEntry` collection stored as **JSONB column** on `LiveGameSession` (simpler than owned entities, sufficient for V1)
- `AgentConversationSummary` generation — **async with eventual consistency**:
  - `SessionSaveRequestedEvent` → KnowledgeBase handler generates summary asynchronously
  - `PauseSnapshot` is saved immediately with `AgentConversationSummary = null`
  - When summary is ready: `AgentSummaryGeneratedEvent` → handler updates `PauseSnapshot.AgentConversationSummary`
  - Timeout: if summary not generated within 60s, resume works without it (agent gets raw last-10-messages as fallback context)
  - This avoids direct cross-context coupling (Fowler recommendation) and doesn't block the save flow

**3. `ResumeSessionCommand`**
- Loads `PauseSnapshot` → restores `LiveGameSession` state
- Sets `Status = LiveSessionStatus.InProgress` and clears `PausedAt` timestamp (uses existing status enum, NOT a boolean `IsPaused`)
- Triggers `SessionResumedEvent` → agent uses summary for recap generation (falls back to raw messages if summary is null)

**4. Auto-Save** *(spec-panel fix: Nygard #2)*
- `SessionAutoSaveBackgroundService`: saves session state every 10 minutes for active sessions
- Also triggers on app backgrounding (frontend sends `AppBackgrounded` SignalR event)
- Auto-save creates `PauseSnapshot` with `IsAutoSave = true` and uses existing `SnapshotTrigger` enum (add `AutoSave` value)
- Cleanup rule: on manual save or session completion, **delete all auto-save PauseSnapshots** for that session

**5. Save Concurrency** *(spec-panel fix: Wiegers #3)*
- `SaveCompleteSessionStateCommand` waits for pending score proposals to flush (max 5s timeout)
- If timeout: saves with pending proposals flagged, notify host on resume

#### Frontend — CardStack Structure

```
📱 Card Stack (left panel)
├── 🎲 Catan — In corso              ← Card parent (session)
│   ├── 🤖 Agente Catan              ← Child card: AI chat
│   ├── 📊 Punteggi                   ← Child card: scoreboard
│   ├── 📷 Foto (3)                   ← Child card: photo gallery
│   └── 👥 Giocatori (4)             ← Child card: players + invite
├── 📚 Library
├── 🔍 Discover
└── ...
```

**Card Parent "🎲 Partita"**:
- Game name + status badge (In corso / Pausata / Completata)
- Session timer (elapsed time)
- Connected players count with online indicators
- Quick actions bar: Invita (QR/link), Pausa, Salva & Esci
- Red badge on pending score proposals awaiting confirmation
- Child cards visible only when parent card is active/selected

**Child Card "🤖 Agente"**:
- Chat interface identical to existing toolkit experience
- Session context injected into system prompt (players, turn, scores)
- On resumed session: recap as first message
- "⚖️ Arbitro" button in chat action bar (see Section 3)

**Child Card "📊 Punteggi"**:
- Real-time scoreboard per player (SignalR-synced)
- Host: +/- buttons for quick scoring, exact input field
- Guest proposals: pending proposals shown with ✅/❌ for host to confirm
- Round support: "Nuovo Round" toggle saves current round scores, starts new round
- Visual: player cards with color coding, current leader highlighted

**Child Card "📷 Foto Tavolo"**:
- Camera capture (device camera) or gallery upload
- Photo grid with timestamps
- Photos linked to session via `SessionAttachment` entity (already exists)
- On save: attachment IDs included in PauseSnapshot

**Child Card "👥 Giocatori"**:
- Player list with connection status (online/offline via SignalR presence)
- "Invita" button → shows QR code (rendered client-side via `qrcode.react`) + shareable link + session code
- Admin invite option: send email invitation

**Guest Score Proposal Rules**:
- Guests can propose scores **for themselves only** (not for other players)
- Score delta must be within game-reasonable bounds (configurable, default: -100 to +100 per proposal)
- Max 3 pending proposals per guest at a time (prevents spam in no-auth model)
- Host sees all pending proposals with ✅ Approve / ❌ Reject actions

#### Guest Experience (browser-only)

```
Host shares link → Guest opens in browser (no auth required)
    ↓
Landing page:
  - Game name + host name
  - Input: "Il tuo nome" → joins session as SessionParticipant
    ↓
Guest view (minimal, mobile-optimized):
  - Current turn + phase indicator (Cockburn fix)
  - Scoreboard (read-only, real-time via SignalR)
  - "Proponi punteggio" button → host approves/rejects
  - Connection via SessionParticipant + ConnectionToken (AllowAnonymous)
```

**Guest Reconnection** *(spec-panel fix: Cockburn #2)*:
- `SessionParticipant` persists for invite duration (24h default)
- On reconnect: browser checks localStorage for `participantToken`
- If valid: auto-rejoin without re-entering name
- If expired: re-enter name, new participant created

#### Offline Resilience *(spec-panel fix: Nygard #1)*
- **Local score buffer**: scores entered while offline are queued in localStorage
- On reconnect: buffered scores sync automatically via SignalR
- Visual indicator: "⚡ Offline — punteggi salvati localmente" banner
- Chat messages: queued locally, sent on reconnect (max 10 pending)
- Auto-save still works if connection drops temporarily (retry with exponential backoff)

---

## Section 3: Rule Arbitration + Agent Memory + Save/Resume

### 3A. Chat Libre for Rules

No changes to RAG engine needed. Only inject session context into agent's system prompt during active session:

```
Sei l'assistente per "{GameName}". Sessione attiva con {N} giocatori: {player_names}.
Turno corrente: {turn}. Fase: {phase}. Punteggi: {score_list}.
Quando rispondi su regole, cita SEMPRE la pagina del regolamento.
Se c'è ambiguità nella regola, spiega le possibili interpretazioni.

Verdetti precedenti in questa sessione:
{list_of_previous_disputes_and_verdicts}
```

The last line ensures **dispute consistency** *(spec-panel fix: Adzic #3)* — the agent won't contradict its own previous rulings.

### 3B. Arbitro Mode — Official Disputes

**Backend**:

**`RuleDisputeEntry`** — Value Object in `LiveGameSession`:
```csharp
public record RuleDisputeEntry
{
    public Guid Id { get; init; }
    public string Description { get; init; }          // "Marco says he can play 2 cards"
    public string Verdict { get; init; }               // Agent's ruling
    public List<string> RuleReferences { get; init; }  // ["Page 12, Section 3.2"]
    public string RaisedByPlayerName { get; init; }
    public DateTime Timestamp { get; init; }
}
```

Stored in `LiveGameSession.Disputes` as **JSONB column** (new collection, requires EF migration + JSON column configuration). This is a new domain model addition — not extending an existing collection.

**Dispute History Per-Game** *(cross-session persistence)*:
- In addition to storing disputes within `LiveGameSession`, a repository query `GetDisputesByGameId(gameId)` aggregates disputes across all completed/paused sessions for the same game
- This enables "consultable history" without a separate entity — disputes live in sessions but are queryable per-game
- Query: `SELECT Disputes FROM LiveGameSessions WHERE GameId = @gameId AND Status IN (Paused, Completed)`

**`SubmitRuleDisputeCommand`**:
- Input: `SessionId`, `Description`, `RaisedByPlayerName`
- Queries agent with specialized arbitration prompt:
  ```
  MODALITÀ ARBITRO. Emetti un VERDETTO chiaro su questa disputa.
  Disputa: "{description}"
  Contesto: {session_context}
  Verdetti precedenti: {previous_disputes}

  Rispondi in formato strutturato:
  VERDETTO: [Chi ha ragione e perché — 1-2 frasi]
  REGOLA: [Citazione esatta dal regolamento con pagina/sezione]
  NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]
  ```
- Parses response into `RuleDisputeEntry`
- Saves to `LiveGameSession.Disputes`
- Broadcasts verdict via SignalR to all session participants

**Frontend — Arbitro UI**:
- "⚖️ Arbitro" button in agent chat action bar
- Click → modal:
  - Text field: "Descrivi la disputa"
  - Dropdown: "Chi contesta?" (player list)
  - Submit button
- Loading state: "L'arbitro sta analizzando il regolamento..."
- Verdict displayed as special card in chat:
  - Distinct styling: amber border, ⚖️ icon, structured layout
  - Shows: Verdict, Rule citation, Notes
- Verdict also appears as **banner/toast in card parent** (visible to everyone at the table)

**Frontend — Dispute History**:
- Collapsible section "⚖️ Verdetti precedenti" in agent child card
- List: question, verdict, rule cited, timestamp
- Persists across sessions — when playing the same game again, previous disputes are consultable
- Queried per-game via `GetDisputesByGameId` repository method (aggregates from all past sessions)

### 3C. Agent Memory + Structured Save

#### Save Flow

When host clicks "💾 Salva & Esci":

1. **Frontend**: modal confirmation — "Vuoi salvare lo stato della partita?"
2. **Optional**: "📷 Scatta foto finale del tavolo" prompt
3. **Frontend → Backend**: `CreatePauseSnapshotCommand`
4. **Backend**:
   a. Wait for pending score proposals to flush (max 5s) *(Wiegers fix)*
   b. Capture structured state: turn, phase, scores, attachments, disputes
   c. Create `PauseSnapshot` entity immediately (with `AgentConversationSummary = null`)
   d. Fire `SessionSaveRequestedEvent` → KnowledgeBase handler generates summary asynchronously (cap at **last 50 messages**)
   e. When summary ready: `AgentSummaryGeneratedEvent` updates `PauseSnapshot` (eventual consistency, max 60s)
   f. Set `LiveGameSession.Status = LiveSessionStatus.Paused` + `PausedAt = DateTime.UtcNow`
   g. Disconnect all SignalR participants gracefully
5. **Frontend**: confirmation — "Sessione salvata! Potrai riprenderla dalla Library."

#### Agent Conversation Summary

Generated by KnowledgeBase context via domain event (no direct cross-context reference):

```
Riassumi questa sessione di gioco per un futuro resume.
Cronologia chat (ultimi 50 messaggi): {messages}

Genera un JSON strutturato:
{
  "game_state": "descrizione dello stato della partita",
  "current_situation": "chi sta vincendo, momento critico, etc.",
  "rules_clarified": ["regola 1 chiarita", "regola 2 chiarita"],
  "disputes_resolved": [{"question": "...", "verdict": "..."}],
  "important_notes": ["nota 1", "nota 2"],
  "suggested_recap": "Messaggio di recap da mostrare ai giocatori al resume"
}
```

#### Resume Flow

1. **Library → PrivateGame card** → "Sessioni" section shows paused sessions
2. Paused session card: date, scores preview, photo thumbnail, "▶️ Riprendi" button
3. Click → `ResumeSessionCommand`:
   a. Loads `PauseSnapshot`
   b. Restores `LiveGameSession` state (turn, phase, scores)
   c. Sets `Status = LiveSessionStatus.InProgress`, clears `PausedAt`
   d. Creates new `SessionInvite` (old one may be expired)
   e. Injects `AgentConversationSummary` into agent system prompt
4. **Frontend**: recreates card parent + child cards in CardStack
5. **Agent first message** (auto-generated recap):
   ```
   🔄 Bentornati! Ecco dove eravamo:

   📊 Punteggi: Giulia 45, Tu 42, Marco 38, Leo 29
   🎯 Turno: Round 5, tocca a Marco
   ⚖️ Regole chiarite: "Una sola carta per turno" (pag.12)
   📸 3 foto del tavolo salvate

   Pronti a riprendere?
   ```

**Resume with Different Players** *(spec-panel fix: Adzic #2)*:
- Agent detects missing players from original roster
- Recap mentions: "Nota: Leo non è presente oggi. I suoi punti (29) sono congelati."
- Host can: remove player (freeze score), replace player (transfer score), or add new player (starts at 0)

---

## Cross-Cutting Concerns

### Bounded Context Ownership

| Component | Bounded Context | Rationale |
|-----------|----------------|-----------|
| `LiveGameSession` | GameManagement | Aggregate root for live play |
| `PauseSnapshot` | GameManagement | New entity for save/resume (separate from existing delta-based SessionSnapshot) |
| `RuleDisputeEntry` | GameManagement | Value Object in LiveGameSession |
| `SessionInvite` | GameManagement | Already exists here |
| `SessionParticipant` | GameManagement | Guest identity, no User reference |
| `SessionAttachment` | GameManagement | Already exists here |
| `AgentConversationSummary` | KnowledgeBase → GameManagement | Generated via domain event, stored in snapshot |
| `AgentDefinition` | KnowledgeBase | Unchanged |
| `Notification` | UserNotifications | Unchanged |
| `PrivateGame` | UserLibrary | Unchanged |
| `TierDefinition` | SystemConfiguration | Unchanged |

### SignalR Events (GameStateHub)

Existing:
- `JoinSession` / `LeaveSession`
- `BroadcastStateChange`
- `ProposeScore` → `ConfirmScore`

New:
- `AppBackgrounded` — triggers auto-save (best-effort: browser `visibilitychange`/`pagehide` events are unreliable on mobile; the 10-min periodic auto-save is the reliable fallback. Consider `navigator.sendBeacon` as alternative signal)
- `DisputeResolved` — broadcasts verdict to all participants
- `SessionPaused` — notifies all participants, graceful disconnect
- `SessionResumed` — notifies reconnected participants
- `ScoreUpdated` — real-time score sync (distinct from proposal flow)
- `ParticipantReconnected` — presence update

### Auto-Save Background Service

```csharp
public class SessionAutoSaveBackgroundService : BackgroundService
{
    // Every 10 minutes: find active sessions, create PauseSnapshot (IsAutoSave = true)
    // Cleanup: on manual save or session completion, delete ALL auto-save PauseSnapshots for that session
    // Trigger: also on AppBackgrounded SignalR event (best-effort)
    // Uses existing SnapshotTrigger enum (add AutoSave value if not present)
}
```

### Offline Buffer (Frontend)

```typescript
interface OfflineBuffer {
  scores: Array<{ playerId: string; delta: number; timestamp: number }>;
  chatMessages: Array<{ content: string; timestamp: number }>;
  maxPendingMessages: 10;
}
// Stored in localStorage keyed by sessionId
// Flushed on SignalR reconnect
// Visual: "⚡ Offline" banner with pending count
```

### Retention Policy

| Data | Retention | Tier |
|------|-----------|------|
| Session snapshots (manual) | Indefinite | All |
| Session snapshots (auto-save) | Until next manual save or session complete | All |
| Session photos | 90 days (free), 1 year (premium) | Tier-based |
| Chat history | 30 days (free), 1 year (premium) | Tier-based |
| Dispute history | Indefinite (per-game) | All |
| Guest participant data | 24h after session ends | All |

### Localization
- V1 is **Italian-only**. All UI strings are hardcoded in Italian.
- i18n support is deferred to a future iteration.
- Agent prompts are in Italian by default (language detection from Phase 5 plan is out of scope for vertical slice).

---

## Specification Examples (Gojko Adzic format)

### Happy Path: Complete Game Night

```gherkin
Scenario: Improvised game night end-to-end
  Given I am logged in and on my Library page
  And I have no games in my collection

  When I click "Aggiungi Gioco" and search BGG for "Catan"
  And I select "Catan" from results
  Then a PrivateGame "Catan" is created in my library

  When I upload "catan-rules.pdf" and accept the copyright disclaimer
  Then I see "Processing in corso..." with progress indicator

  When processing completes (chunking → embedding → indexing)
  Then I receive a notification "🤖 Agente per Catan pronto!"
  And an AgentDefinition is auto-created with default game prompt

  When I click "Avvia Sessione" on the Catan card
  Then a LiveGameSession is created with me as host
  And a SessionInvite is generated (PIN + link)
  And a card parent "🎲 Catan — In corso" appears in my CardStack
  With child cards: Agente, Punteggi, Foto, Giocatori

  When I share the invite link with Marco
  And Marco opens the link and enters his name
  Then Marco appears in the Giocatori child card as "online"
  And Marco sees a minimal guest view with live scoreboard

  When I ask the agent "Come si prepara Catan per 3 giocatori?"
  Then the agent responds with setup instructions citing the rulebook

  When I record scores: Me=42, Marco=38, Giulia=45
  Then all connected participants see updated scores in real-time

  When Marco proposes a score "+5 for Marco"
  Then I see a pending proposal badge on the Punteggi card
  And I can confirm or reject the proposal

  When I click "⚖️ Arbitro" and describe "Marco says he can play 2 cards per turn"
  Then the agent analyzes and returns a verdict with rule citation
  And the verdict is saved in the session dispute history
  And a banner appears on the card parent with the ruling

  When I click "💾 Salva & Esci"
  Then I optionally take a final photo
  And the system saves: scores, turn, phase, photos, disputes, agent memory
  And all participants are disconnected gracefully

  When I return days later and open my Library
  Then the Catan card shows "1 sessione in pausa"

  When I click "▶️ Riprendi"
  Then the session is restored with original state
  And the agent shows a recap: "Eravate al round 5, Giulia stava vincendo..."
  And I can continue playing
```

### Error Path: Agent Auto-Creation Failure

```gherkin
Scenario: PDF processes successfully but agent creation fails
  Given I uploaded a valid PDF for "Catan"
  And PDF processing completes successfully
  When agent auto-creation fails (e.g., agent quota reached, KnowledgeBase service down)
  Then I receive a notification "⚠️ Agente non creato automaticamente per Catan"
  And the notification offers "Crea manualmente" linking to agent creation page
  And the processed PDF/KB cards remain available for manual agent creation
```

### Error Path: PDF Processing Failure

```gherkin
Scenario: PDF processing fails
  Given I uploaded a poorly scanned PDF for "Catan"
  When processing fails due to low OCR quality
  Then I receive a notification "⚠️ Processing fallito per Catan"
  And the notification offers "Riprova" and "Carica un altro PDF"
  When I click "Riprova"
  Then the PDF is re-queued for processing (max 3 retries)
```

### Error Path: Offline During Game

```gherkin
Scenario: WiFi drops during game
  Given I am in an active session with 3 players
  When WiFi connection drops
  Then I see "⚡ Offline — punteggi salvati localmente"
  And I can still enter scores (buffered locally)
  When WiFi reconnects
  Then buffered scores sync automatically via SignalR
  And the "Offline" banner disappears
```

### Edge Case: Resume with Missing Players

```gherkin
Scenario: Resume with fewer players
  Given I saved a session with players: Me, Marco, Giulia, Leo
  When I resume the session with only Me and Marco present
  Then the agent recap mentions "Giulia e Leo non sono presenti"
  And their scores are frozen at saved values
  And I can choose to: keep frozen, remove player, or add replacement
```

---

## Implementation Priority (Vertical Slice)

### Must Have (Day 1-8)
1. Create `AutoCreateAgentOnPdfReadyHandler` (full implementation, not just extension) + notification + failure path
2. `StartImprovvisataSessionCommand` shortcut
3. `RuleDisputeEntry` JSONB column on `LiveGameSession` + EF migration
4. `PauseSnapshot` entity + EF migration (separate from existing delta-based `SessionSnapshot`)
5. Card parent + 4 child cards in CardStack (frontend)
6. Scoreboard child card with host +/- and real-time sync
7. Guest landing page (link/session code, no auth, score view + self-proposals with limits)
8. Session context injection into agent prompt
9. `SubmitRuleDisputeCommand` + Arbitro UI
10. `CreatePauseSnapshotCommand` with structured state (async summary)
11. `ResumeSessionCommand` + Library "sessioni in pausa" UI

### Should Have (Day 9-11)
12. `AgentConversationSummary` async generation via domain event
13. Auto-recap on resume (with fallback to raw messages if summary is null)
14. Photo capture child card (`SessionAttachment`)
15. Auto-save background service (10 min interval, cleanup on manual save)
16. Guest score proposal business rules (self-only, delta limits, max pending)

### Nice to Have (Day 12-15)
17. Offline local buffer + sync on reconnect
18. Guest reconnection via localStorage token
19. PDF processing progress SSE events
20. Dispute history query across sessions per-game (`GetDisputesByGameId`)
21. Resume with different players management
22. `navigator.sendBeacon` for reliable app-backgrounding signal

---

## Open Questions for Future Iterations

1. **Multi-game session**: Can a game night have multiple games? (Deferred — single game per session for V1)
2. **Guest agent access**: Should premium tier allow guests to query the agent? (Deferred to tier system)
3. **Voice input**: Could players ask rules verbally instead of typing? (Deferred — future feature)
4. **Spectator mode**: Can someone watch without being a player? (Deferred)
5. **Tournament mode**: Multiple sessions with brackets? (Deferred — different feature entirely)

---

## Spec Review Changelog

**Review 1** (2026-03-15, automated spec-document-reviewer):

| ID | Severity | Fix Applied |
|----|----------|-------------|
| C1 | Critical | Renamed `SessionSnapshot` → `PauseSnapshot` to avoid conflict with existing delta-based `SessionSnapshot` entity |
| C2 | Critical | Replaced `IsPaused = false` with `Status = LiveSessionStatus.InProgress` + clear `PausedAt` |
| C3 | Critical | Changed "Extend" to "Create" for `AutoCreateAgentOnPdfReadyHandler` — only exists in test stubs |
| M1 | Major | Added explicit migration notes for `RuleDisputeEntry` JSONB column and `PauseSnapshot` table |
| M2 | Major | Specified async-with-eventual-consistency for `AgentConversationSummary` (save immediately, summary arrives async, 60s timeout, fallback to raw messages) |
| M3 | Major | Resolved dispute storage contradiction: disputes live in `LiveGameSession` per-session, queryable per-game via `GetDisputesByGameId` repository method |
| M4 | Major | Added guest score proposal business rules: self-only, delta limits (-100/+100), max 3 pending |
| M5 | Major | QR code generated client-side via `qrcode.react`, removed `qrCodeUrl` from backend response |
| m1 | Minor | Replaced "PIN" with "session code" for consistency with existing `SessionCode` |
| m2 | Minor | Added best-effort note for `AppBackgrounded`, `navigator.sendBeacon` alternative |
| m3 | Minor | Clarified auto-save cleanup: delete ALL auto-saves on manual save/completion |
| m4 | Minor | Added i18n section: V1 Italian-only, strings hardcoded |
| m5 | Minor | Updated effort estimate from 8-10 to 12-15 days |
| m6 | Minor | Added agent auto-creation failure error path (Gherkin scenario) |
| m7 | Minor | Changed `SavedByPlayerName` to `SavedByUserId` (host is always saver) |
