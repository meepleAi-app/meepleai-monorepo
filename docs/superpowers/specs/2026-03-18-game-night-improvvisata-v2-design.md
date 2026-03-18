# Game Night Improvvisata v2 — Design Spec

**Date**: 2026-03-18 | **Status**: Draft | **Parent Epic**: #379 (closed, all 26 issues)

## Overview

Three enhancements to the Game Night Improvvisata journey (100% implemented, PR #485). These extend the existing session lifecycle with proactive setup guidance, structured rule arbitration, and persistent agent memory.

**Approach**: Single spec, phased implementation (Setup Wizard → Arbitro Strutturato → Memoria Agente).

**Prerequisites**: Resolve review issues I2 (reflection repo mapping), I3 (PauseSnapshot FK), I4 (migration cleanup) before implementation begins.

---

## Phase 1: Setup Wizard (RAG-only)

### Concept

When a user creates a game session, the agent automatically generates two artifacts from the PDF rulebook:
1. **Component Checklist** — all materials needed (board, dice, cards, tokens...)
2. **Interactive Setup Guide** — step-by-step sequence adapted to player count

### Flow

```
User creates session (existing flow)
  → Selects player count (new)
  → Backend sends specialized prompt to RAG agent:
     "Extract from the rulebook: 1) complete component list 2) setup procedure for N players"
  → Agent responds with structured JSON
  → Frontend shows:
     - Tab "Components": checkable list
     - Tab "Setup": step-by-step guide with "Done, next step" button
```

### Fallback Cascade

```
1. RAG from PDF (primary)
   ↓ if incomplete
2. BGG data already in DB (imported when game was added)
   ↓ if still incomplete
3. Ask user in chat → save in Agent Memory (Phase 3)
```

- **Feature flag**: `SetupWizard.BggFallback` (default: `disabled`) — controls whether level 2 is active
- **No BGG API calls at runtime** — only data already present in `SharedGame` / `UserGame`
- **User response (level 3) persists** in Agent Memory → available for future sessions

### Data Model

```
SetupChecklistData (mutable JSONB payload class — NOT a DDD value object)
├── Components: List<SetupComponent>
│   ├── Name: string ("Game board")
│   ├── Quantity: int (1)
│   └── Checked: bool (mutable, toggled by user)
├── SetupSteps: List<SetupStep>
│   ├── Order: int
│   ├── Instruction: string ("Deal 5 cards to each player")
│   └── Completed: bool (mutable, toggled by user)
└── PlayerCount: int
```

**Persistence**: New dedicated JSONB column `SetupChecklistJson` on `LiveGameSession` (in **GameManagement**), following the existing `DisputesJson` pattern. Requires a migration to add the column.

**Mutability note**: `SetupChecklistData` contains user-togglable state (`Checked`, `Completed`). It is a mutable JSONB payload class, not an immutable DDD value object. Updates replace the entire JSON document on save (replace-whole semantics).

### Limits

- Quality depends entirely on the PDF. If the rulebook doesn't clearly list components, the checklist will be incomplete — this is expected and acceptable.
- No external source fallback beyond BGG data already in DB (behind feature flag).
- If extraction fails or returns empty, the Setup tab doesn't appear and the user falls back to normal RAG chat.

### Bounded Context

Extends **GameManagement** (`LiveGameSession` entity) with cross-context call to **KnowledgeBase**. The existing `StreamSetupGuideQuery(string GameId)` stub must be extended with a `PlayerCount: int` parameter (or replaced with a new query) to support player-count-adapted setup extraction.

---

## Phase 2: Arbitro Strutturato (Structured Rule Arbitration)

### Concept

Evolution from single-question verdicts to a multi-party dispute system with democratic override and persistent history.

### Existing Implementation (v1)

The current system stores disputes as `RuleDisputeEntry` value objects serialized as JSONB in `LiveGameSession.DisputesJson` within **GameManagement**. Cross-session history already exists via `GetGameDisputeHistoryQuery`, which reads `DisputesJson` across all sessions for a `GameId`. The `SubmitRuleDisputeCommand` handler injects the last 3 disputes into the LLM prompt for context.

**v2 extends (not replaces) the existing system by adding:**
- Structured verdict with confidence and citation (instead of free text)
- Multi-party claims (initiator + respondent)
- Democratic override voting
- Semantic similarity layer for cross-session history surfacing
- Real-time "previously discussed" notification in dispute UI

**Migration strategy**: Existing `DisputesJson` entries remain as-is. v2 disputes are stored in the new `RuleDispute` table (for structured queries, voting, relationships) AND a `RuleDisputeEntry`-compatible subset is appended to `DisputesJson` (verdict, description, references, raisedBy, timestamp) to preserve the existing `GetGameDisputeHistoryQuery` contract. All v2-specific fields (claims, votes, confidence, citations, related IDs) live exclusively in the `RuleDispute` table. The `Arbitro.StructuredDisputes` feature flag controls whether new disputes use the v2 flow or the legacy single-question flow.

### Dispute Flow

```
Player A opens dispute:
  → Describes their interpretation ("I can build 2 buildings per turn")
  → Optionally tags Player B as counterparty

Player B (if tagged) responds:
  → Describes their interpretation ("No, only 1 building per turn")
  → Timeout: client-side 2-minute timer. If B doesn't respond,
     frontend sends RespondentTimeoutCommand → agent proceeds with A's claim only.
     Backend accepts the command with a null RespondentClaim.

Agent analyzes:
  → Searches RAG for relevant rules
  → Queries existing disputes for same GameId by semantic similarity
     → If related disputes found, includes them in context
  → Produces structured verdict:
     - Who is right (A, B, or "ambiguous rule")
     - Citation from rulebook (page, section)
     - Confidence level (high/medium/low)
     - Argumentation for each position
  → Populates RelatedDisputeIds at resolution time (before persisting)

Democratic override (via SignalR, consistent with existing DisputeResolvedEvent broadcast):
  → Verdict broadcast to all players via SignalR
  → Each player sends CastVoteOnDisputeCommand (accept/reject)
  → TallyDisputeVotesCommand triggered when all votes received or after 1-minute vote timeout
  → If majority accepts → FinalOutcome = VerdictAccepted
  → If majority rejects → FinalOutcome = VerdictOverridden
     → Host enters override rule text
     → Dispute saved as "contested" with OverrideRule

History:
  → Every dispute is persisted with verdict, votes, outcome
  → On next dispute about the same rule:
     "This rule was already discussed on 03/15: the verdict was..."
```

### Data Model

```
RuleDispute (entity, dedicated table in GameManagement)
├── Id: Guid
├── SessionId: Guid (FK → LiveGameSession)
├── GameId: Guid (FK → Game)
├── InitiatorPlayerId: Guid
├── RespondentPlayerId: Guid? (optional)
├── InitiatorClaim: string
├── RespondentClaim: string? (null if respondent timed out)
├── Verdict: DisputeVerdict (immutable value object)
│   ├── RulingFor: enum (Initiator | Respondent | Ambiguous)
│   ├── Reasoning: string
│   ├── Citation: string? (page/section from PDF)
│   └── Confidence: enum (High | Medium | Low)
├── Votes: List<DisputeVote> (JSONB)
│   ├── PlayerId: Guid
│   └── AcceptsVerdict: bool
├── FinalOutcome: enum (VerdictAccepted | VerdictOverridden | Pending)
├── OverrideRule: string? ("Majority rule: 1 building")
├── CreatedAt: DateTime
└── RelatedDisputeIds: List<Guid> (JSONB, populated at resolution time via semantic similarity query)
```

### Command/Query Surface

| Command/Query | Purpose |
|---|---|
| `OpenStructuredDisputeCommand` | Player A opens dispute with claim |
| `RespondToDisputeCommand` | Player B submits counterclaim |
| `RespondentTimeoutCommand` | Client-side timer expired, proceed without B |
| `CastVoteOnDisputeCommand` | Player votes accept/reject on verdict |
| `TallyDisputeVotesCommand` | Count votes and finalize outcome |
| `GetDisputeHistoryQuery` | Extends existing `GetGameDisputeHistoryQuery` with v2 fields |

### Differences from Current System

| Aspect | Current (v1) | v2 |
|---|---|---|
| Input | Single question | Two positions (claim/counterclaim) |
| Output | Free text | Structured verdict + citation + confidence |
| Voting | None | Democratic override via SignalR |
| Persistence | JSONB in DisputesJson | Dedicated table + backward-compatible DisputesJson append |
| Cross-session | Yes (GetGameDisputeHistoryQuery) | Extended with semantic similarity + real-time notification |
| Timeout | None | Client-side 2min (respondent) + 1min (voting) |

### Bounded Context

`RuleDispute` lives in **GameManagement** (where `SubmitRuleDisputeCommand`, `RuleDisputeEntry`, `LiveGameSession`, and `DisputeResolvedEvent` already exist). Cross-session history extends the existing `GetGameDisputeHistoryQuery`.

---

## Phase 3: Agent Memory

### Concept

The agent becomes the group's "chronicler." Layered memory across three levels: per-game, per-group, per-player. Guests without accounts can retroactively claim their history.

### Memory Levels

```
1. Game Memory (GameMemory)
   → House rules adopted
   → Disputes and verdicts (from Arbitro system)
   → Custom setups ("we always use the quick variant")
   → User-completed checklists (from Setup Wizard fallback level 3)

2. Group Memory (GroupMemory)
   → Preferences: max duration, preferred complexity, play style
   → Statistics: games played together, favorite games, frequency
   → Dynamics: "Marco always forgets the trade rule"
   → Free notes added by players

3. Player Memory (PlayerMemory)
   → Score history per game (partially existing in GameManagement)
   → Win/loss record
   → Favorite games
   → Guest claim: retroactive name → account association
```

### Relationship to Existing Session Groups

`LiveGameSession` already has a `GroupId: Guid?` property. `GroupMemory.Id` is the **persistent projection** of this transient group concept:

- When a session is created with players who don't belong to any `GroupMemory`, a `GroupMemory` is **not** auto-created. Sessions work without it.
- Users can explicitly create a `GroupMemory` ("Tuesday night crew") and associate members.
- When a session is created, if the player set matches an existing `GroupMemory`, `LiveGameSession.GroupId` is set to that `GroupMemory.Id`.
- Stats accumulate in `GroupMemory` only for sessions where `GroupId` is set.

### Data Model

```
GameMemory (entity)
├── Id: Guid
├── GameId: Guid (FK → Game)
├── OwnerId: Guid (FK → User, memory creator)
├── HouseRules: List<HouseRule> (JSONB)
│   ├── Description: string
│   ├── AddedAt: DateTime
│   └── Source: enum (UserAdded | DisputeOverride)
├── CustomSetup: SetupChecklistData? (JSONB, from user fallback)
└── Notes: List<MemoryNote> (JSONB)

GroupMemory (entity)
├── Id: Guid
├── Name: string ("Tuesday night crew")
├── CreatorId: Guid (FK → User)
├── Members: List<GroupMember> (JSONB)
│   ├── UserId: Guid? (null if guest)
│   ├── GuestName: string? ("Marco")
│   └── JoinedAt: DateTime
├── Preferences: GroupPreferences (JSONB)
│   ├── MaxDuration: TimeSpan?
│   ├── PreferredComplexity: enum? (Light | Medium | Heavy)
│   └── CustomNotes: string?
├── Stats: GroupStats (JSONB, computed)
│   ├── TotalSessions: int
│   ├── FavoriteGames: List<GamePlayCount>
│   └── LastPlayedAt: DateTime?
└── CreatedAt: DateTime

PlayerMemory (entity)
├── Id: Guid
├── UserId: Guid? (null if unclaimed guest)
├── GuestName: string?
├── GroupId: Guid? (FK → GroupMemory, nullable — supports ad-hoc sessions without a named group)
├── GameStats: List<PlayerGameStats> (JSONB)
│   ├── GameId: Guid
│   ├── Wins: int
│   ├── Losses: int
│   ├── TotalPlayed: int
│   └── BestScore: int?
└── ClaimedAt: DateTime? (when guest claimed the account)
```

### Guest Claim Flow

```
Guest "Marco" plays 5 evenings without an account
  → PlayerMemory exists with GuestName="Marco", UserId=null

Marco creates a MeepleAI account
  → In profile section: "Claim previous games"
  → Sees list of groups where a guest "Marco" exists
  → Selects → group host receives confirmation notification
  → Host confirms → PlayerMemory.UserId = Marco's Id
  → Marco now sees all his history
```

### How the Agent Uses Memory

When the agent responds in chat, the context includes:

```
Current session context:
  + GameMemory: house rules, custom setup, previous disputes
  + GroupMemory: group preferences, noted dynamics
  + PlayerMemory: statistics for present players

Enriched response examples:
  "Setup: deal 7 cards (not 5 — you have the quick variant house rule)"
  "Marco, last time you forgot the trade rule (dispute from 03/15)"
  "This group prefers games under 2 hours — I suggest the short variant"
```

### Automatic Feeding

| Event | Memory Updated |
|---|---|
| Dispute with override | GameMemory.HouseRules += new rule |
| Setup completed by user (fallback) | GameMemory.CustomSetup |
| Session completed | PlayerMemory.GameStats, GroupMemory.Stats |
| User adds note in chat | GameMemory.Notes or GroupMemory |

### Bounded Context

New bounded context **AgentMemory** — separate from GameManagement because memory survives sessions and has its own lifecycle. Cross-context events:
- `SessionCompletedEvent` → update Stats
- `DisputeResolvedEvent` with override → add HouseRule
- `SetupCompletedByUserEvent` → save CustomSetup

### Privacy

- Memory belongs to the **creator** (host)
- Group members see their own stats and shared info
- Guest claim requires host confirmation
- Delete: host can delete all group memory

---

## Implementation Order

| Phase | Feature | Dependencies | Estimated Scope |
|---|---|---|---|
| 0 | Fix review issues I2, I3, I4 | None | Prerequisite |
| 1 | Setup Wizard | KnowledgeBase (RAG query) | GameManagement extension |
| 2 | Arbitro Strutturato | Phase 1 not required | GameManagement extension |
| 3 | Memoria Agente | Phase 2 (dispute → house rules) | New bounded context (AgentMemory) |

Phases 1 and 2 are independent and could be parallelized. Phase 3 depends on Phase 2 for the dispute-to-house-rule automatic feeding.

## Feature Flags

All flags are seeded into the `FeatureFlag` table via the existing **SystemConfiguration** bounded context. They are toggled at runtime through `ToggleFeatureFlagCommand` and the admin panel. A seed migration entry is required for each flag.

| Flag | Default | Controls |
|---|---|---|
| `SetupWizard.Enabled` | `true` | Entire Setup Wizard feature |
| `SetupWizard.BggFallback` | `false` | BGG data fallback in setup cascade |
| `Arbitro.StructuredDisputes` | `true` | v2 dispute system (vs legacy single-question) |
| `Arbitro.DemocraticOverride` | `true` | Voting on verdicts |
| `AgentMemory.Enabled` | `true` | Entire memory system |
| `AgentMemory.GuestClaim` | `true` | Guest account claiming |

---

**Last Updated**: 2026-03-18
