# Piano di Implementazione - Epic #4746: Live Game Session System

## Stato Attuale

| Fase | Totale | Completate | Rimanenti |
|------|--------|-----------|-----------|
| Phase 1 - Core MVP | 6 | 4 (BE) | 2 (FE) |
| Phase 2 - Toolkit | 5 | 0 | 5 (3 BE + 2 FE) |
| Phase 3 - Advanced | 5 | 0 | 5 (3 BE + 2 FE) |
| Phase 4 - SSE | 4 | 0 | 4 (3 BE + 1 FE) |
| **Totale** | **20** | **4** | **16** |

### Codice Esistente (Phase 1 BE - completato)
- **Domain**: `GameManagement/Domain/Entities/LiveGameSession.cs`, `LiveSessionPlayer.cs`, `LiveSessionTeam.cs`
- **Enums**: `LiveSessionStatus.cs` (Setup, InProgress, Paused, Completed)
- **Events**: 12 domain events (`LiveSessionCreatedEvent`, `StartedEvent`, etc.)
- **Commands**: 10 commands (Create, Start, Pause, Resume, RecordScore, EditScore, etc.)
- **Queries**: 5 queries (GetSession, GetByCode, GetActiveSessions, GetPlayers, GetScores)
- **Handlers**: 6 handler files (Create, Lifecycle, Player, ScoreAndTurn, Team, Query)
- **Validators**: 5 validator files
- **Repository**: `ILiveSessionRepository` + `LiveSessionRepository`
- **Endpoints**: `Routing/LiveSessionEndpoints.cs`
- **Migration**: `20260219112100_AddLiveGameSession`
- **Tests**: `LiveSessionCommandHandlerTests.cs`, `LiveSessionQueryHandlerTests.cs`, `LiveSessionValidatorTests.cs`

---

## Piano di Esecuzione

### Branch Strategy
- **Base branch**: `main-dev`
- Per ogni issue: `feature/issue-{id}-{desc}` → PR a `main-dev`

---

## Phase 1 - Core MVP (Frontend) — 2 issue

### Issue 1: #4751 - MeepleCard Session Front + Relationship Links Footer
**Tipo**: Frontend | **Dipende da**: #4749 (✅ completata)
**Branch**: `feature/issue-4751-meeplecard-session-front`

**Scope**:
- Estendere `meeple-card.tsx` con `entity="session"`
- Status badge per 4 stati (Setup, InProgress, Paused, Completed)
- Score table (Player x Round matrix)
- Turn sequence (player chips con colori)
- Action buttons context-sensitive per stato
- Relationship Links Footer (4 visible + overflow dropdown)
- Mini MeepleCard Player popup su hover
- Score editing modal

**Agenti `/implementa`**:
- `frontend-architect` → implementazione componenti
- `quality-engineer` → test Vitest componenti

---

### Issue 2: #4752 - MeepleCard Session Back + Tests + Code Review
**Tipo**: Frontend + Testing | **Dipende da**: #4751
**Branch**: `feature/issue-4752-meeplecard-session-back`

**Scope**:
- Card back con statistiche, ranking, timeline
- Status-specific back content (4 varianti)
- Backend unit tests ≥90% coverage
- Frontend component tests (Vitest)
- Code review automatico (5 agenti paralleli)

**Agenti `/implementa`**:
- `frontend-architect` → card back
- `quality-engineer` → test suite completa
- `code-review:code-review` → review automatica

---

## Phase 2 - Toolkit + Snapshot (5 issue)

### Issue 3: #4753 - GameToolkit Bounded Context - Domain Model + CQRS
**Tipo**: Backend | **Dipende da**: Phase 1
**Branch**: `feature/issue-4753-game-toolkit-bc`

**Scope**:
- Nuovo BC `GameToolkit` (Domain + Application + Infrastructure)
- Entities: `GameToolkit`, `ToolkitTool`
- CQRS: Commands/Queries/Handlers per CRUD toolkit
- Repository + EF Configuration
- DI registration

**Agenti `/implementa`**:
- `backend-architect` → domain model + CQRS pattern
- `quality-engineer` → unit tests

---

### Issue 4: #4754 - ToolState Entity + Toolkit ↔ Session Integration
**Tipo**: Backend | **Dipende da**: #4753
**Branch**: `feature/issue-4754-toolstate-integration`

**Scope**:
- `ToolState` entity (stato strumenti per sessione)
- Integration tra GameToolkit e LiveGameSession
- Domain events per toolkit state changes
- Handlers per sincronizzazione

**Agenti `/implementa`**:
- `backend-architect` → entity + integration
- `quality-engineer` → integration tests

---

### Issue 5: #4755 - SessionSnapshot - Delta-based History + State Reconstruction
**Tipo**: Backend | **Dipende da**: #4754
**Branch**: `feature/issue-4755-session-snapshot`

**Scope**:
- `SessionSnapshot` entity con delta compression
- State reconstruction dal chain di snapshots
- Trigger automatici per snapshot (score change, turn advance)
- Query per timeline/history

**Agenti `/implementa`**:
- `backend-architect` → snapshot system
- `quality-engineer` → unit + integration tests

---

### Issue 6: #4757 - ExtraMeepleCard Component Base + Session Tabs
**Tipo**: Frontend | **Dipende da**: #4752
**Branch**: `feature/issue-4757-extra-meeplecard`

**Scope**:
- `ExtraMeepleCard` component (expanded view)
- Tab system per session: Overview, Scores, Tools, Timeline
- Integration con toolkit API
- Responsive design

**Agenti `/implementa`**:
- `frontend-architect` → component system
- `quality-engineer` → Vitest tests

---

### Issue 7: #4758 - Snapshot History Slider UI + Time Travel Mode + Phase 2 Tests
**Tipo**: Frontend + Testing | **Dipende da**: #4755, #4757
**Branch**: `feature/issue-4758-snapshot-slider`

**Scope**:
- History slider UI (timeline scrubber)
- Time travel mode (visualizza stato a punto specifico)
- Test completi Phase 2 (BE + FE)

**Agenti `/implementa`**:
- `frontend-architect` → slider + time travel
- `quality-engineer` → test suite Phase 2

---

## Phase 3 - Advanced Toolkit + Media + AI (5 issue)

### Issue 8: #4759 - CardToolConfig + TimerToolConfig + StateTemplate
**Tipo**: Backend | **Dipende da**: #4753
**Branch**: `feature/issue-4759-tool-configs`

**Scope**:
- `CardToolConfig` domain model (deck management)
- `TimerToolConfig` domain model (timers per turno/fase)
- `StateTemplate` per configurazione stati personalizzati
- CQRS per gestione configurazioni

**Agenti `/implementa`**:
- `backend-architect` → domain models
- `quality-engineer` → unit tests

---

### Issue 9: #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
**Tipo**: Backend | **Dipende da**: #4755
**Branch**: `feature/issue-4760-media-rag-chat`

**Scope**:
- `SessionMedia` entity (foto/video/audio per sessione)
- Integration con RAG agent (KnowledgeBase BC)
- Shared chat per sessione (real-time messaging)
- Domain events per media + chat

**Agenti `/implementa`**:
- `backend-architect` → media + RAG integration
- `quality-engineer` → tests

---

### Issue 10: #4761 - Turn Phases from TurnTemplate + Event-Triggered Snapshots
**Tipo**: Backend | **Dipende da**: #4759, #4755
**Branch**: `feature/issue-4761-turn-phases`

**Scope**:
- `TurnTemplate` con fasi strutturate (draw, action, scoring)
- Event-triggered snapshots automatici
- Turn phase progression logic
- Domain events per cambio fase

**Agenti `/implementa`**:
- `backend-architect` → turn system
- `quality-engineer` → tests

---

### Issue 11: #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
**Tipo**: Frontend | **Dipende da**: #4757, #4760
**Branch**: `feature/issue-4762-extra-tabs`

**Scope**:
- Media Tab (gallery foto/video/audio)
- AI Tab (chat con RAG agent)
- Support per altri entity types nel ExtraMeepleCard
- Integration con API media e chat

**Agenti `/implementa`**:
- `frontend-architect` → tabs + integration
- `quality-engineer` → Vitest tests

---

### Issue 12: #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
**Tipo**: Frontend + Testing | **Dipende da**: #4759, #4762
**Branch**: `feature/issue-4763-interactive-cards-timer`

**Scope**:
- Interactive card deck UI (draw, discard, shuffle)
- Timer component (countdown, countup, per-turn)
- Events timeline UI (visual event log)
- Test completi Phase 3

**Agenti `/implementa`**:
- `frontend-architect` → interactive components
- `quality-engineer` → test suite Phase 3

---

## Phase 4 - Multi-Device Real-time SSE (4 issue)

### Issue 13: #4764 - SSE Streaming Infrastructure + Session State Broadcasting
**Tipo**: Backend | **Dipende da**: Phase 2 BE
**Branch**: `feature/issue-4764-sse-infrastructure`

**Scope**:
- SSE endpoint infrastructure (.NET 9)
- Session state broadcasting (push updates a client)
- Connection management (heartbeat, reconnect)
- Event serialization + filtering

**Agenti `/implementa`**:
- `backend-architect` → SSE infrastructure
- `quality-engineer` → integration tests

---

### Issue 14: #4765 - Player Action Endpoints + Host Validation + Conflict Resolution
**Tipo**: Backend | **Dipende da**: #4764
**Branch**: `feature/issue-4765-player-actions`

**Scope**:
- Player action endpoints (move, vote, roll, etc.)
- Host validation (solo host può certe azioni)
- Conflict resolution (concurrent edits)
- Optimistic concurrency control

**Agenti `/implementa`**:
- `backend-architect` → endpoints + validation
- `security-engineer` → authorization review
- `quality-engineer` → tests

---

### Issue 15: #4766 - Session Join via Code + Active Player Roles
**Tipo**: Backend | **Dipende da**: #4765
**Branch**: `feature/issue-4766-join-code-roles`

**Scope**:
- Join session via alphanumeric code
- Player roles (Host, Player, Spectator)
- Role-based access control per session
- Auto-assignment di ruoli

**Agenti `/implementa`**:
- `backend-architect` → join + roles system
- `security-engineer` → RBAC review
- `quality-engineer` → tests

---

### Issue 16: #4767 - SSE Client + Player/Spectator Mode UI + Real-time Notifications + Phase 4 Tests
**Tipo**: Frontend + Testing | **Dipende da**: #4764, #4766
**Branch**: `feature/issue-4767-sse-client-ui`

**Scope**:
- SSE client (EventSource + reconnect logic)
- Player mode UI (interactive, can take actions)
- Spectator mode UI (read-only, real-time updates)
- Real-time notification toasts
- Test completi Phase 4 (BE + FE + E2E)

**Agenti `/implementa`**:
- `frontend-architect` → SSE client + modes
- `quality-engineer` → test suite Phase 4
- `code-review:code-review` → review finale

---

## Dependency Graph

```
Phase 1 BE (✅ done)
    ├── #4751 (FE: Session Front)
    │     └── #4752 (FE: Session Back + Tests)
    │           └── #4757 (FE: ExtraMeepleCard)
    │                 └── #4758 (FE: Snapshot Slider)
    │                 └── #4762 (FE: Media/AI Tabs)
    │                       └── #4763 (FE: Interactive Cards)
    ├── #4753 (BE: GameToolkit BC)
    │     ├── #4754 (BE: ToolState Integration)
    │     │     └── #4755 (BE: Snapshots)
    │     │           ├── #4758 (FE: Snapshot Slider)
    │     │           ├── #4760 (BE: Media + RAG)
    │     │           └── #4761 (BE: Turn Phases)
    │     └── #4759 (BE: Tool Configs)
    │           └── #4761 (BE: Turn Phases)
    │           └── #4763 (FE: Interactive Cards)
    └── #4764 (BE: SSE Infrastructure)
          └── #4765 (BE: Player Actions)
                └── #4766 (BE: Join + Roles)
                      └── #4767 (FE: SSE Client)
```

## Ordine di Esecuzione Ottimale

L'ordine rispetta le dipendenze e massimizza il parallelismo BE/FE:

| Step | Issue | Tipo | Parallelo con |
|------|-------|------|---------------|
| 1 | #4751 | FE | #4753 (BE) |
| 2 | #4752 | FE | #4754 (BE) |
| 3 | #4753 | BE | #4751 (FE) |
| 4 | #4754 | BE | #4752 (FE) |
| 5 | #4755 | BE | #4757 (FE) |
| 6 | #4757 | FE | #4755 (BE) |
| 7 | #4759 | BE | #4758 (FE) |
| 8 | #4758 | FE | #4759 (BE) |
| 9 | #4760 | BE | — |
| 10 | #4761 | BE | #4762 (FE) |
| 11 | #4762 | FE | #4761 (BE) |
| 12 | #4763 | FE | #4764 (BE) |
| 13 | #4764 | BE | #4763 (FE) |
| 14 | #4765 | BE | — |
| 15 | #4766 | BE | — |
| 16 | #4767 | FE | — |

## Workflow per ogni Issue (`/implementa`)

Per ogni issue, il workflow `/implementa` esegue:

1. **Setup**: Crea branch `feature/issue-{id}-{desc}` da `main-dev`, setta parent
2. **Analisi**: Legge issue GitHub, identifica requirements e acceptance criteria
3. **Implementazione**: Usa agenti specializzati (backend-architect, frontend-architect)
4. **Testing**: quality-engineer per unit/integration/component tests
5. **Code Review**: code-review automatica
6. **PR**: Crea PR verso `main-dev` con summary
7. **Merge**: Dopo review, merge e cleanup branch
8. **Chiusura**: Aggiorna stato issue locale + GitHub

## Rischi e Mitigazioni

| Rischio | Mitigazione |
|---------|-------------|
| Incompatibilità con SessionTracking BC esistente | Verificare che GameManagement e SessionTracking non abbiano conflitti |
| SSE performance con molti client | Rate limiting + connection pooling in Phase 4 |
| Delta snapshot complexity | Iniziare con full snapshots, ottimizzare dopo |
| MeepleCard component size | Lazy loading dei tab, code splitting |
| Conflitti merge tra issue parallele | Merge frequente di main-dev, piccoli PR |
