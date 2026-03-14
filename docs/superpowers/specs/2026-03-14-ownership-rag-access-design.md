# Ownership-Gated RAG Access — Design Spec

**Date**: 2026-03-14
**Status**: Reviewed
**User Story**: User adds shared game to library → declares ownership → accesses RAG → creates tutor agent → starts chat
**Review**: Spec review completed, all critical/major issues addressed (see Review Log at bottom)

## Overview

When a user declares ownership of a game in their library, they unlock access to RAG content (rulebooks, FAQ, strategies) for that game. An admin can override this gate per-game for copyright-free manuals. After declaring ownership, the user can quick-create a tutor agent or customize one via the existing wizard, then start a chat session powered by RAG.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Popup timing | Post-add (separate action) | Game added as Novo first, ownership is a conscious decision |
| Popup type | Informative + checkbox + CTA | Balances value communication, legal protection, and UX |
| Ownership verification | Self-declaration with explicit checkbox | Industry standard (iTunes model), zero friction, shifts liability |
| RAG access model | Ownership-gated + admin override per-game | Default: ownership required. Admin can flag copyright-free games as public |
| Post-confirmation CTA | Quick-create + Customize | "Crea veloce" (auto-defaults → chat) or "Personalizza" (wizard step 2) |
| Enforcement scope | Full — all agent/chat/KB endpoints | Approach B: closes all existing security gaps |
| Audit trail | OwnershipDeclaredAt timestamp + domain event | Demonstrates good faith, traceable per user/game |

## Data Model Changes

### SharedGame (SharedGameCatalog BC)

New field:
- `IsRagPublic: bool` (default `false`) — Admin override for copyright-free manuals
- `SetRagPublicAccess(bool)` — Domain method to toggle

### UserLibraryEntry (UserLibrary BC)

New field:
- `OwnershipDeclaredAt: DateTime?` — Audit timestamp, null = not declared

New computed property (C# only, NOT a DB column):
- `HasDeclaredOwnership: bool` → `OwnershipDeclaredAt != null` (read-only computed property in entity)

New method:
- `DeclareOwnership()` — Implementation details:
  1. If `CurrentState == GameState.Wishlist` → throw DomainException
  2. If `HasDeclaredOwnership` → return (idempotent, no error)
  3. Set `OwnershipDeclaredAt = DateTime.UtcNow`
  4. Call existing `MarkAsOwned()` internally (reuses existing state machine, emits GameStateChangedEvent)
  5. Emit additional `OwnershipDeclaredEvent` for audit

**State transition clarification**: `DeclareOwnership()` is a superset of `MarkAsOwned()`. It calls `MarkAsOwned()` internally, so it follows the existing state machine. The separate `MarkAsOwned()` method continues to work independently (for admin or other flows) but does NOT set `OwnershipDeclaredAt`.

**InPrestito handling**: Games in `InPrestito` state are already physically owned. `DeclareOwnership()` on InPrestito sets `OwnershipDeclaredAt` but does NOT change the state (remains InPrestito). Backfill migration also covers InPrestito entries.

### State Diagram

```
                    DeclareOwnership()
    Novo ──────────────────────────────────► Owned
     │                                         ▲
     │           MarkAsOwned()                 │
     └─────────────────────────────────────────┘

    InPrestito ──DeclareOwnership()──► InPrestito (sets OwnershipDeclaredAt only)

    Wishlist ──DeclareOwnership()──► throws DomainException

    Already Owned ──DeclareOwnership()──► no-op (idempotent, sets OwnershipDeclaredAt if null)
```

### New: OwnershipDeclaredEvent (UserLibrary BC)

Domain event with: EntryId, UserId, GameId, DeclaredAt. Used for audit trail.

### New: IRagAccessService — Cross-BC Integration

**Interface**: Defined in KnowledgeBase.Application (consumer BC)
**Implementation**: In KnowledgeBase.Infrastructure via read-side query service

**Cross-BC pattern**: The implementation uses direct DB reads (read-model joins) across the SharedGame and UserLibraryEntry tables. This is acceptable because:
- It's a read-only query (no writes across BC boundaries)
- It avoids eventual consistency complexity for a synchronous access check
- The alternative (integration events + local cache) would add complexity without benefit for this use case

Centralized access check with cascading rules:
1. Admin role → always allowed
2. SharedGame.IsRagPublic == true → always allowed
3. UserLibraryEntry.HasDeclaredOwnership == true → allowed
4. Otherwise → denied

Methods:
- `CanAccessRagAsync(Guid userId, Guid gameId, UserRole role): Task<bool>`
- `GetAccessibleKbCardsAsync(Guid userId, Guid gameId, UserRole role): Task<List<Guid>>`

### Migration

EF Core migration (C#, not raw SQL). The SQL below is illustrative — use `dotnet ef migrations add AddOwnershipRagAccess`.

```sql
-- SharedGame: add IsRagPublic
ALTER TABLE shared_games ADD COLUMN is_rag_public BOOLEAN NOT NULL DEFAULT FALSE;

-- UserLibraryEntry: add OwnershipDeclaredAt
ALTER TABLE user_library_entries ADD COLUMN ownership_declared_at TIMESTAMPTZ NULL;

-- Backfill: existing Owned (3) and InPrestito (1) games get OwnershipDeclaredAt = now
-- NOTE: Verify actual enum storage format in EF config before running.
-- GameStateType enum values: Novo=0, InPrestito=1, Wishlist=2, Owned=3
UPDATE user_library_entries SET ownership_declared_at = NOW()
  WHERE current_state IN (1, 3);  -- InPrestito + Owned
```

### FluentValidation

New validators required:
- `DeclareOwnershipCommandValidator` — validates gameId is non-empty UUID
- `QuickCreateAgentCommandValidator` — validates gameId required, sharedGameId optional UUID
- `SetRagPublicAccessCommandValidator` — validates sharedGameId non-empty, isRagPublic is bool

## Backend — New Endpoints

### POST /api/v1/library/{gameId}/declare-ownership

- Auth: RequireSession()
- No request body (userId from session)
- Logic: Get entry → validate not Wishlist → entry.DeclareOwnership() → save → check KB availability
- **Idempotency**: If already declared, returns 200 with current state (no error, no re-emit of event)
- Response 200:
  ```json
  {
    "gameState": "Owned",
    "ownershipDeclaredAt": "2026-03-14T...",
    "hasRagAccess": true,
    "kbCardCount": 3,
    "isRagPublic": false
  }
  ```
- Errors: 404 (not in library), 409 (wishlist state)

### POST /api/v1/agents/quick-create

- Auth: RequireSession() + CanAccessRagAsync()
- Request:
  ```json
  {
    "gameId": "uuid",
    "sharedGameId": "uuid?"
  }
  ```
- **gameId clarification**: This is the `UserLibraryEntry.GameId` (the ID the user's library uses to reference the game). For shared catalog games, this equals `SharedGame.Id`. For private games, this is `PrivateGame.Id`. The `sharedGameId` is provided when the game comes from the shared catalog, to enable KB card lookup via `VectorDocument.SharedGameId`.
- Logic: Verify RAG access → get all indexed KB cards for game (by sharedGameId if provided, else by gameId) → create AgentDefinition (type=Tutor, name="Tutor [GameName]", auto-select KB cards) → create ChatSession → return
- Response 200:
  ```json
  {
    "agentId": "uuid",
    "chatThreadId": "uuid",
    "agentName": "Tutor Catan",
    "kbCardCount": 3
  }
  ```
- Errors: 403 (no RAG access)

### PUT /api/v1/admin/shared-games/{id}/rag-access

- Auth: RequireAdminSession()
- Request: `{ "isRagPublic": true }`
- Logic: Get SharedGame → SetRagPublicAccess(isPublic) → save
- Response: 204 No Content
- Errors: 403 (non-admin), 404 (game not found)

## Backend — Enforcement on Existing Endpoints

All endpoints below get `IRagAccessService` injected into their command handlers. Check runs BEFORE any existing logic. Returns 403 ForbiddenException if denied.

**Agent creation endpoints** (check before creating):

| Endpoint | Check |
|----------|-------|
| POST /api/v1/agents/user | CanAccessRagAsync(userId, cmd.GameId, role) |
| POST /api/v1/agents/create-with-setup | CanAccessRagAsync(userId, cmd.GameId, role) |

**Chat/RAG query endpoints** (check before querying):

| Endpoint | Check |
|----------|-------|
| POST /api/v1/chat/sessions | CanAccessRagAsync(userId, req.GameId, role) |
| POST /api/v1/chat/sessions/{sessionId}/messages | Resolve session.GameId → CanAccessRagAsync |
| POST /api/v1/agents/{id}/chat | Resolve agent.GameId → CanAccessRagAsync |
| POST /api/v1/agents/query | Resolve agent.GameId → CanAccessRagAsync |
| POST /api/v1/agents/{id}/invoke | Resolve agent.GameId → CanAccessRagAsync |
| POST /api/v1/agents/chat/ask | Resolve agent.GameId → CanAccessRagAsync |
| POST /api/v1/agents/tutor/query | Resolve agent.GameId → CanAccessRagAsync |
| POST /api/v1/agents/{id}/arbiter | Resolve agent.GameId → CanAccessRagAsync |
| POST /api/v1/chat-threads/{threadId}/messages | Resolve thread.GameId → CanAccessRagAsync |
| POST /api/v1/game-sessions/{sessionId}/chat/ask-agent | Resolve session.GameId → CanAccessRagAsync |

**Implementation note**: For endpoints that resolve agent/session/thread first, the check adds one extra DB query. To minimize impact, `RagAccessService` should cache results per `(userId, gameId)` within a single request scope (scoped lifetime in DI).

## Frontend — Components

### OwnershipDeclarationDialog

- Type: shadcn AlertDialog
- Location: `components/library/OwnershipDeclarationDialog.tsx`
- Props: `gameId, gameName, open, onOpenChange, onOwnershipDeclared`
- Content: Title "Possiedi [gameName]?", 3 benefits (tutor AI, sessions, lending), checkbox "Confermo di possedere questo gioco", expandable legal disclaimer
- Behavior: "Conferma Possesso" button disabled until checkbox checked. On confirm: calls `POST /library/{gameId}/declare-ownership`, passes result to `onOwnershipDeclared`

### OwnershipConfirmationDialog

- Type: shadcn Dialog
- Location: `components/library/OwnershipConfirmationDialog.tsx`
- Props: `gameId, gameName, sharedGameId?, ownershipResult, open, onOpenChange`
- Two variants based on `ownershipResult.kbCardCount`:
  - **> 0**: Shows KB card chips + "Crea Tutor veloce" + "Personalizza" buttons
  - **== 0**: Shows "Tutor non ancora disponibile" message + "Chiudi" only
- "Crea veloce": calls `POST /agents/quick-create` → on success: `router.push(/chat/[threadId])`
- "Personalizza": `router.push(/chat/agents/create?gameId=X&step=2)`

### DeclareOwnershipButton

- Location: `components/library/DeclareOwnershipButton.tsx`
- Props: `gameId, gameName, sharedGameId?, gameState, onOwnershipDeclared`
- Visibility: renders only when `gameState === 'Nuovo'` (note: GameStateType uses Italian names in the enum: `Nuovo`, `InPrestito`, `Wishlist`, `Owned`)
- On click: opens OwnershipDeclarationDialog

### RagAccessBadge

- Location: `components/library/RagAccessBadge.tsx`
- Props: `hasRagAccess, isRagPublic`
- Shows RAG access status on game cards (locked/unlocked/public)

### Loading and Error States (all dialog components)

- **During API call**: Confirm button shows spinner, is disabled. Checkbox and "Non ancora" also disabled.
- **On network error**: Toast error message, dialog stays open, user can retry.
- **On 409 (wishlist)**: Toast "Non puoi dichiarare il possesso di un gioco nella wishlist", dialog closes.
- **On 404**: Toast "Gioco non trovato nella libreria", dialog closes.
- **Quick-create loading**: "Crea veloce" button shows spinner, both CTA buttons disabled during call.
- **Quick-create error**: Toast error, confirmation dialog stays open, user can retry.

## Frontend — Flow

### Placement

DeclareOwnershipButton appears on:
1. MeepleCard in `/library` (tab Collection) — when game state is Novo
2. Game detail page `/games/[id]` — when game is in library with state Novo

### Navigation Flow

```
/games/catalog → "Add to Library" → game added as Novo
        ↓
/library → game card shows "Dichiara Possesso" button
        ↓
Click → OwnershipDeclarationDialog opens
        ↓
Check checkbox → "Conferma Possesso" → API call
        ↓
OwnershipConfirmationDialog opens (success)
        ↓
"⚡ Crea Tutor veloce" → POST /agents/quick-create → redirect /chat/[threadId]
   OR
"🛠️ Personalizza" → redirect /chat/agents/create?gameId=X&step=2
        ↓
/chat/[threadId] → user writes question → agent responds with RAG content
```

### API Client Additions

```typescript
// libraryClient.ts
declareOwnership(gameId: string): Promise<OwnershipResult>

// agentsClient.ts
quickCreateTutor(gameId: string, sharedGameId?: string): Promise<QuickCreateResult>

// adminClient.ts
setRagPublicAccess(sharedGameId: string, isPublic: boolean): Promise<void>
```

### Existing Response DTO Extensions

The following existing GET endpoints need their response DTOs extended:

| Endpoint | New Fields |
|----------|-----------|
| GET /api/v1/library (list entries) | `ownershipDeclaredAt: string?`, `hasRagAccess: boolean` |
| GET /api/v1/library/{gameId} (single entry) | `ownershipDeclaredAt: string?`, `hasRagAccess: boolean` |
| GET /api/v1/shared-games/{id} | `isRagPublic: boolean` |
| GET /api/v1/shared-games (list) | `isRagPublic: boolean` per item |

These fields enable the frontend to show/hide `DeclareOwnershipButton` and `RagAccessBadge` without additional API calls.

### "Personalizza" Wizard Flow

`router.push(/chat/agents/create?gameId=X&step=2)` navigates to the existing 4-step agent creation wizard. The query params:
- `gameId`: Pre-selects the game, displayed as read-only in step 1
- `step=2`: Skips step 1 (game selection) and starts at step 2 (agent type selection)

The wizard must handle the `step` query param: if `step=2` and `gameId` is provided, render step 2 directly. Steps 2→3→4 proceed normally. The user can still go back to step 1 if they want to change the game.

## Test Plan

### Prerequisites

1. Infra running: `docker compose up -d postgres qdrant redis`
2. API (:8080) + Web (:3000) running
3. Migration applied (includes IsRagPublic + OwnershipDeclaredAt)
4. Admin uploads `data/rulebook/catan_en_rulebook.pdf` to Catan SharedGame, waits for indexing (Ready)
5. Test user account (non-admin role)

### Manual Browser Tests

#### TC-01: Happy Path — Catalog → Ownership → Agent → Chat

| Step | Action | Expected |
|------|--------|----------|
| 1 | Login as user, go to `/games/catalog` | Catalog visible, Catan present |
| 2 | Click "Add to Library" on Catan | Toast confirmation, "Already in library" badge |
| 3 | Go to `/library`, Collection tab | Catan visible with "Novo" badge + "Dichiara Possesso" button |
| 4 | Click "Dichiara Possesso" | Popup: title, 3 benefits, checkbox, button disabled |
| 5 | Check "Confermo di possedere" checkbox | "Conferma Possesso" button becomes active |
| 6 | Expand "Perché lo chiediamo?" | Legal text visible |
| 7 | Click "Conferma Possesso" | Post-confirmation popup: "Fatto!", KB cards visible, 2 CTAs |
| 8 | Click "⚡ Crea Tutor veloce" | Loading, then redirect to `/chat/[threadId]` |
| 9 | Type "Come si gioca a Catan?" and send | Agent responds with RAG content from rulebook |
| 10 | Return to `/library` | Catan shows "Owned" badge, no "Dichiara Possesso" button |

#### TC-02: No Ownership → Cannot Create Agent

| Step | Action | Expected |
|------|--------|----------|
| 1 | Add game to library (stays Novo, don't declare ownership) | OK |
| 2 | Go to `/chat/agents/create`, select that game | Wizard allows navigation |
| 3 | Click "Create Agent" | Error 403: "Devi possedere il gioco per creare un agente" |

#### TC-03: No Ownership → Cannot Chat (API direct)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Call `POST /api/v1/agents/{id}/chat` via DevTools | — |
| 2 | With agentId for a non-owned game | 403: "Accesso RAG non autorizzato" |

#### TC-04: No KB Cards → No Tutor CTA

| Step | Action | Expected |
|------|--------|----------|
| 1 | Add a game WITHOUT uploaded PDFs to library | OK |
| 2 | Declare ownership | Post-confirmation popup |
| 3 | Verify post-confirmation | "Tutor non ancora disponibile" message, only Close button |

#### TC-05: Public RAG → Accessible Without Ownership

| Step | Action | Expected |
|------|--------|----------|
| 1 | Admin: set IsRagPublic = true for a game | OK |
| 2 | User: add that game to library (Novo, NO ownership declared) | OK |
| 3 | User: go to `/chat/agents/create`, create agent for that game | Agent created successfully (IsRagPublic bypass) |

### Automated Tests (~36 total)

#### Backend Unit Tests (~12)

- RagAccessService: Admin bypass → true
- RagAccessService: IsRagPublic bypass → true
- RagAccessService: HasDeclaredOwnership → true
- RagAccessService: No ownership → false
- DeclareOwnership: Novo → Owned transition
- DeclareOwnership: Wishlist → throws DomainException
- DeclareOwnership: Idempotent (already Owned)
- QuickCreateAgent: Happy path (creates agent + chat)
- QuickCreateAgent: 403 without ownership
- QuickCreateAgent: Auto-selects all indexed KB cards
- SetRagPublicAccess: Toggle on/off
- OwnershipDeclaredEvent: Emitted on declare

#### Backend Integration Tests (~12)

- POST /library/{id}/declare-ownership → 200
- POST /library/{id}/declare-ownership → 404 (not in library)
- POST /library/{id}/declare-ownership → 409 (wishlist)
- POST /agents/quick-create → 200 (creates agent + chat)
- POST /agents/quick-create → 403 (no ownership)
- POST /agents/user → 403 (no ownership, enforcement)
- POST /agents/create-with-setup → 403 (enforcement)
- POST /chat/sessions → 403 (no RAG access)
- PUT /admin/shared-games/{id}/rag-access → 204
- PUT /admin/shared-games/{id}/rag-access → 403 (non-admin)
- POST /agents/user → 200 (with IsRagPublic game, no ownership needed)
- Migration backfill: existing Owned entries get OwnershipDeclaredAt

#### Frontend Unit Tests (~12)

- OwnershipDeclarationDialog: Renders, checkbox enables button
- OwnershipDeclarationDialog: Calls declareOwnership on confirm
- OwnershipDeclarationDialog: "Non ancora" closes dialog
- OwnershipConfirmationDialog: Shows KB cards when available
- OwnershipConfirmationDialog: Shows "not available" when no KB
- OwnershipConfirmationDialog: "Crea veloce" calls quickCreate
- OwnershipConfirmationDialog: "Personalizza" navigates to wizard
- DeclareOwnershipButton: Visible only when Nuovo
- DeclareOwnershipButton: Hidden when Owned/Wishlist/InPrestito
- RagAccessBadge: Shows correct state
- libraryClient.declareOwnership: Correct API call
- agentsClient.quickCreateTutor: Correct API call

## Out of Scope

- Partnership program with game publishers (future business initiative)
- Notification when KB becomes available for a game the user owns
- Revoking ownership (user can't un-declare; would need admin action)
- Rate limiting on declare-ownership endpoint (low risk, low volume)
- E2E/Playwright automated tests (manual browser tests cover the critical path; Playwright tests can be added in a follow-up)

## Review Log

### Review 1 (2026-03-14)

**Critical issues fixed:**
- C1: Clarified `DeclareOwnership()` calls `MarkAsOwned()` internally (reuses existing state machine). Added state diagram. Defined InPrestito and idempotency behavior.
- C2: Documented cross-BC integration pattern as read-side query service with explicit rationale for not using integration events.
- C3: Expanded enforcement table from 5 to 12 endpoints covering all RAG-consuming paths. Added request-scoped caching note.

**Major issues fixed:**
- M1: Migration backfill now includes InPrestito (state=1). Added note to verify EF enum storage format.
- M2: Clarified gameId vs sharedGameId semantics with documentation.
- M3: Defined idempotency: 200 with current state, no re-emit of event.
- M4: InPrestito gets OwnershipDeclaredAt set (no state change). Covered in backfill.
- M5: Clarified HasDeclaredOwnership is a C# computed property, NOT a DB column.

**Minor issues fixed:**
- m1: Fixed typo cantan → catan
- m2: Moved RagAccessBadge to components/library/
- m3: Corrected gameState comparison to 'Nuovo' (Italian enum)
- m4: Added loading/error states section for all dialog components
- m5: Added "Existing Response DTO Extensions" section
- m6: Added FluentValidation section for new commands
