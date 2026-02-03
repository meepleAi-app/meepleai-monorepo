# Implementation Plan: Private Games & Catalog Proposal

## Overview

Phased implementation approach with MVP-first delivery, maintaining backward compatibility.

## Phase 1: Data Model & Core Infrastructure (MVP)

**Duration**: ~3-4 days
**Goal**: Enable private game creation without breaking existing functionality

### Tasks

#### 1.1 Database Migration
```bash
# Create migration
dotnet ef migrations add AddPrivateGameSupport
```

**Changes:**
- [ ] Make `UserLibraryEntryEntity.SharedGameId` nullable
- [ ] Add `IsPrivateGame` boolean field
- [ ] Add private game metadata fields (Title, MinPlayers, MaxPlayers, etc.)
- [ ] Add `PrivateGameSource` enum field
- [ ] Add check constraint for data integrity
- [ ] Add index on `PrivateBggId`

**Validation:**
- Existing data remains valid (all have SharedGameId)
- New entries can be either SharedGame-linked or Private

#### 1.2 Domain Layer Updates

**Files to modify:**
- [ ] `UserLibrary/Domain/Entities/UserLibraryEntry.cs` - Add private game value objects
- [ ] `UserLibrary/Domain/ValueObjects/` - Create `PrivateGameTitle`, `PlayerCount` VOs
- [ ] `UserLibrary/Domain/Repositories/IUserLibraryRepository.cs` - Add new query methods

**New files:**
- [ ] `UserLibrary/Domain/ValueObjects/PrivateGameData.cs`
- [ ] `UserLibrary/Domain/Enums/PrivateGameSource.cs`

#### 1.3 Entity Configuration

**File:** `Infrastructure/EntityConfigurations/UserLibrary/UserLibraryEntryEntityConfiguration.cs`

- [ ] Update SharedGameId to optional
- [ ] Add configurations for new columns
- [ ] Add check constraint

#### 1.4 Repository Updates

**File:** `UserLibrary/Infrastructure/Persistence/UserLibraryRepository.cs`

- [ ] Update existing queries to handle nullable SharedGameId
- [ ] Add `GetPrivateGamesByUserAsync()`
- [ ] Add `ExistsByBggIdForUserAsync()`

### Deliverables Phase 1
- ✅ Migration applied successfully
- ✅ Existing functionality unchanged
- ✅ Database ready for private games

---

## Phase 2: BGG Search & Private Game Creation

**Duration**: ~4-5 days
**Goal**: Users can search BGG and add private games

### Tasks

#### 2.1 Public BGG Search Endpoint

**New files:**
- [ ] `BoundedContexts/SharedGameCatalog/Application/Queries/SearchBggPublic/SearchBggPublicQuery.cs`
- [ ] `BoundedContexts/SharedGameCatalog/Application/Queries/SearchBggPublic/SearchBggPublicQueryHandler.cs`
- [ ] `BoundedContexts/SharedGameCatalog/Application/Queries/SearchBggPublic/SearchBggPublicQueryValidator.cs`

**Endpoint:** `GET /api/v1/bgg/search`

**Rate limiting:**
- [ ] Add `BggPublicSearch` rate limit policy (10 req/min)

#### 2.2 Public BGG Details Endpoint

**New files:**
- [ ] `BoundedContexts/SharedGameCatalog/Application/Queries/GetBggGamePublic/GetBggGamePublicQuery.cs`
- [ ] `BoundedContexts/SharedGameCatalog/Application/Queries/GetBggGamePublic/GetBggGamePublicQueryHandler.cs`

**Endpoint:** `GET /api/v1/bgg/games/{bggId}`

#### 2.3 Add Private Game Command

**New files:**
- [ ] `BoundedContexts/UserLibrary/Application/Commands/AddPrivateGame/AddPrivateGameCommand.cs`
- [ ] `BoundedContexts/UserLibrary/Application/Commands/AddPrivateGame/AddPrivateGameCommandHandler.cs`
- [ ] `BoundedContexts/UserLibrary/Application/Commands/AddPrivateGame/AddPrivateGameCommandValidator.cs`

**Logic:**
1. Check quota
2. If BggId provided:
   - Check SharedGames for existing → redirect
   - Else fetch BGG data → create private
3. If manual → validate required fields → create private

#### 2.4 Endpoint Registration

**File:** `Routing/BggEndpoints.cs` (new)

```csharp
public static RouteGroupBuilder MapBggEndpoints(this RouteGroupBuilder group)
{
    group.MapGet("/bgg/search", HandleSearchBgg)
        .RequireAuthorization()
        .RequireRateLimiting("BggPublicSearch");

    group.MapGet("/bgg/games/{bggId:int}", HandleGetBggGame)
        .RequireAuthorization()
        .RequireRateLimiting("BggPublicSearch");

    return group;
}
```

**File:** `Routing/UserLibraryEndpoints.cs` (modify)

- [ ] Add `POST /user-library/private-games` endpoint

#### 2.5 Update GetUserLibrary Query

**File:** `UserLibrary/Application/Handlers/GetUserLibraryQueryHandler.cs`

- [ ] Handle both SharedGame and Private game entries
- [ ] Return unified DTO with game info from appropriate source

#### 2.6 Update DTOs

**File:** `UserLibrary/Application/DTOs/UserLibraryEntryDto.cs`

- [ ] Add `IsPrivateGame` flag
- [ ] Add `PrivateGameSource` field
- [ ] Add `CanProposeToСatalog` flag

### Deliverables Phase 2
- ✅ Users can search BGG
- ✅ Users can add BGG games as private
- ✅ Users can add manual games
- ✅ Library shows both shared and private games

---

## Phase 3: Proposal System

**Duration**: ~3-4 days
**Goal**: Users can propose private games for catalog

### Tasks

#### 3.1 Extend ContributionType Enum

**File:** `SharedGameCatalog/Domain/ValueObjects/ContributionType.cs`

```csharp
public enum ContributionType
{
    NewGame = 0,
    AdditionalContent = 1,
    NewGameProposal = 2  // NEW
}
```

#### 3.2 Extend ShareRequest Entity

**File:** `SharedGameCatalog/Domain/Entities/ShareRequest.cs`

- [ ] Add `SourceLibraryEntryId` property
- [ ] Add factory method for NewGameProposal

#### 3.3 Propose Command

**New files:**
- [ ] `UserLibrary/Application/Commands/ProposePrivateGame/ProposePrivateGameCommand.cs`
- [ ] `UserLibrary/Application/Commands/ProposePrivateGame/ProposePrivateGameCommandHandler.cs`
- [ ] `UserLibrary/Application/Commands/ProposePrivateGame/ProposePrivateGameCommandValidator.cs`

**Validation:**
- Entry must be private game
- Entry must belong to user
- No pending proposal for same entry

#### 3.4 Endpoint

**File:** `Routing/UserLibraryEndpoints.cs`

- [ ] Add `POST /user-library/{entryId}/propose-to-catalog`

#### 3.5 Admin Review Extension

**File:** `SharedGameCatalog/Application/Commands/ApproveShareRequestCommandHandler.cs`

- [ ] Handle `NewGameProposal` contribution type
- [ ] Create SharedGame from private game data
- [ ] Fetch fresh BGG data if BggId available

### Deliverables Phase 3
- ✅ Users can propose private games
- ✅ Proposals appear in admin review queue
- ✅ Admins can approve/reject proposals
- ✅ Approved proposals create SharedGames

---

## Phase 4: Notifications

**Duration**: ~2-3 days
**Goal**: Users receive notifications about proposal status

### Tasks

#### 4.1 New Notification Types

**File:** `UserNotifications/Domain/Enums/NotificationType.cs`

```csharp
GameProposalSubmitted = 20,
GameProposalInReview = 21,
GameProposalApproved = 22,
GameProposalRejected = 23,
GameProposalChangesRequested = 24
```

#### 4.2 Event Handlers

**New files:**
- [ ] `SharedGameCatalog/Application/EventHandlers/GameProposalNotificationHandler.cs`

**Events to handle:**
- ShareRequestCreatedEvent (NewGameProposal type)
- ShareRequestApprovedEvent (NewGameProposal type)
- ShareRequestRejectedEvent (NewGameProposal type)
- ShareRequestChangesRequestedEvent

#### 4.3 Notification Templates

**File:** `UserNotifications/Infrastructure/Templates/`

- [ ] Add templates for new notification types

### Deliverables Phase 4
- ✅ Users receive proposal confirmation
- ✅ Users notified of approval/rejection
- ✅ Users notified of change requests

---

## Phase 5: PDF & Chat Integration

**Duration**: ~3-4 days
**Goal**: Private games support PDF upload and AI chat

### Tasks

#### 5.1 Extend PDF Upload

**File:** `DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`

- [ ] Support linking PDF to UserLibraryEntry (private game)
- [ ] Create GameEntity for private game if needed
- [ ] Or link directly to UserLibraryEntry

**Decision needed:** Create lightweight GameEntity or extend PdfDocument to link to UserLibraryEntry?

#### 5.2 Chat Context Resolution

**File:** `KnowledgeBase/Application/Services/ChatContextService.cs`

- [ ] Resolve context for private games
- [ ] Find user's uploaded PDFs
- [ ] Build RAG context from user's documents

#### 5.3 Endpoint Updates

**File:** `Routing/ChatEndpoints.cs`

- [ ] Allow chat for private game entries
- [ ] Validate user owns the entry

### Deliverables Phase 5
- ✅ Users can upload PDFs for private games
- ✅ AI chat works for private games
- ✅ RAG uses user's uploaded documents

---

## Phase 6: Testing & Polish

**Duration**: ~3-4 days
**Goal**: Comprehensive testing and production readiness

### Tasks

#### 6.1 Unit Tests

- [ ] `AddPrivateGameCommandHandlerTests`
- [ ] `ProposePrivateGameCommandHandlerTests`
- [ ] `SearchBggPublicQueryHandlerTests`
- [ ] `ApproveShareRequestCommandHandler_NewGameProposal_Tests`
- [ ] Validator tests for all new validators

#### 6.2 Integration Tests

- [ ] Private game CRUD operations
- [ ] BGG search and data fetching
- [ ] Auto-redirect to SharedGame
- [ ] Proposal workflow end-to-end
- [ ] Notification delivery

#### 6.3 E2E Tests

- [ ] Full user journey: Search → Add → Upload → Chat
- [ ] Full proposal journey: Propose → Review → Approve
- [ ] Quota enforcement
- [ ] Rate limiting verification

#### 6.4 Documentation

- [ ] Update API documentation (Scalar)
- [ ] Update CLAUDE.md if needed
- [ ] Create user guide for new features

### Deliverables Phase 6
- ✅ 90%+ test coverage for new code
- ✅ All E2E scenarios passing
- ✅ Documentation complete

---

## Summary Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1: Data Model | 3-4 days | 3-4 days |
| Phase 2: BGG & Creation | 4-5 days | 7-9 days |
| Phase 3: Proposals | 3-4 days | 10-13 days |
| Phase 4: Notifications | 2-3 days | 12-16 days |
| Phase 5: PDF & Chat | 3-4 days | 15-20 days |
| Phase 6: Testing | 3-4 days | 18-24 days |

**Total estimated: 18-24 working days**

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| BGG API rate limits | Aggressive caching, user-facing rate limits |
| Data migration issues | Extensive testing, reversible migration |
| Performance with nullable FK | Proper indexing, query optimization |
| Scope creep | Strict MVP focus, defer nice-to-haves |

## Success Metrics

- [ ] Zero regression in existing functionality
- [ ] Private game creation < 2s response time
- [ ] BGG search < 3s response time
- [ ] 95%+ test pass rate
- [ ] No critical bugs in first week post-launch
