# Epic Specification: User Private Library & Collections Management

**Epic ID**: TBD (da creare su GitHub)
**Priority**: 🔴 Critical
**Total Issues**: 4
**Total Story Points**: 16 SP
**Target Duration**: Week 2 (parallel con admin flow)

---

## 📋 Overview

### Obiettivo
Permettere agli utenti di gestire collezioni personali di giochi con PDF privati, abilitando il flusso completo:
```
Dashboard Personale → Add Game to Collection → Upload Private PDF → View Collection Stats
```

### Context
- **Shared Library**: Giochi pubblici con PDF condivisi (gestiti da admin)
- **Private Collection**: Giochi personali dell'utente con PDF privati (non condivisi)
- **User Journey**: User vuole aggiungere giochi custom o annotazioni personali separate dal catalogo pubblico

### Scope
- ✅ View personal game collection
- ✅ Add games with private PDFs
- ✅ Collection statistics dashboard
- ✅ Integration with existing UserLibraryEntry entity
- ❌ Multiple collections per user (future)
- ❌ Collection sharing (future)
- ❌ Collection tags/categories (future)

---

## 🎯 User Stories

### As a User
1. **View Collection**: "I want to see all games in my personal collection with quick stats"
2. **Add Game**: "I want to add a game from shared catalog or create a custom one"
3. **Upload Private PDF**: "I want to upload my personal rulebook/house rules"
4. **Track Progress**: "I want to see how many games I've added and PDFs I've uploaded"

### As a Developer
1. **Data Model**: "I need to extend UserLibraryEntry to support private PDFs"
2. **API Endpoints**: "I need dedicated endpoints for private PDF upload and collection management"
3. **Frontend Components**: "I need reusable components for collection dashboard and wizard"

---

## 🏗️ Architecture

### Data Model Changes

#### UserLibraryEntry Extension
```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs

public class UserLibraryEntry : AggregateRoot
{
    // Existing fields
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public bool HasPdfDocuments { get; private set; }
    public DateTime AddedAt { get; private set; }

    // NEW: Private PDF support
    public Guid? PrivatePdfId { get; private set; }
    public bool HasPrivatePdf => PrivatePdfId.HasValue;

    // Navigation properties
    public virtual Game Game { get; private set; }
    public virtual PdfDocumentEntity? PrivatePdf { get; private set; }

    // NEW: Methods
    public void AssociatePrivatePdf(Guid pdfId)
    {
        PrivatePdfId = pdfId;
        HasPdfDocuments = true;
        AddDomainEvent(new PrivatePdfAssociatedEvent(Id, pdfId));
    }
}
```

#### PdfDocumentEntity Extension
```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocumentEntity.cs

public class PdfDocumentEntity : AggregateRoot
{
    // Existing fields...

    // NEW: Private PDF support
    public bool IsPrivate { get; private set; }
    public Guid? UserLibraryEntryId { get; private set; }

    public static PdfDocumentEntity CreatePrivate(
        Guid ownerId,
        string fileName,
        long fileSize,
        Guid userLibraryEntryId)
    {
        return new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = fileName,
            FileSize = fileSize,
            IsPrivate = true,
            UserLibraryEntryId = userLibraryEntryId,
            UploadedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Pending
        };
    }
}
```

### API Endpoints

#### Collection Management
```
GET    /api/v1/users/{userId}/library           - Get user collection
GET    /api/v1/users/{userId}/library/stats     - Get collection statistics
POST   /api/v1/users/{userId}/library/entries   - Add game to collection
DELETE /api/v1/users/{userId}/library/entries/{id} - Remove from collection
```

#### Private PDF Upload
```
POST   /api/v1/users/{userId}/library/entries/{entryId}/pdf - Upload private PDF
GET    /api/v1/users/{userId}/library/entries/{entryId}/pdf - Get PDF metadata
DELETE /api/v1/users/{userId}/library/entries/{entryId}/pdf - Delete private PDF
```

### Frontend Components

#### Page Structure
```
/dashboard/collection
├── CollectionDashboard (Issue D)
│   ├── HeroStats (4 KPI cards)
│   ├── CollectionGrid (game cards)
│   ├── ActivityFeed (recent actions)
│   └── QuickActions (add game button)
└── AddGameWizard (Issue E)
    ├── Step1: SearchSelectGame
    ├── Step2: GameDetails (if custom)
    ├── Step3: UploadPrivatePDF
    └── Step4: ReviewConfirm
```

---

## 📦 Issues Breakdown

### Issue D: User Collection Dashboard (Frontend - 5 SP)

**Priority**: 🔴 Critical
**Area**: Frontend
**Dependencies**: Epic #3306 (Dashboard Hub skeleton)

#### Description
Implementare dashboard collezioni personali con statistiche, lista giochi, e quick actions.

#### Acceptance Criteria
- [ ] Hero Stats mostra: Total Games, Private PDFs, Active Chats, Reading Time
- [ ] Collection Grid visualizza giochi con MeepleCard component (#3325)
- [ ] Sort by: Date Added, Title, Last Played, Chat Activity
- [ ] Filter by: Has PDF, Has Active Chat, Category
- [ ] Quick Action "Add Game" apre wizard (Issue E)
- [ ] Activity Feed mostra azioni recenti (riuso #3311 component)
- [ ] Responsive design (mobile/tablet/desktop)
- [ ] Loading states e error handling

#### Technical Tasks
1. Create `CollectionDashboard.tsx` page component
2. Implement `CollectionStats` hook (fetch stats API)
3. Create `CollectionGrid` component (reuse MeepleCard)
4. Implement sort/filter controls
5. Integrate ActivityFeed component
6. Add unit tests (Vitest)
7. Add E2E test (Playwright): "User views collection dashboard"

#### Files to Create/Modify
**Create**:
- `apps/web/src/app/dashboard/collection/page.tsx`
- `apps/web/src/components/collection/CollectionDashboard.tsx`
- `apps/web/src/components/collection/CollectionGrid.tsx`
- `apps/web/src/components/collection/CollectionStats.tsx`
- `apps/web/src/hooks/useCollectionStats.ts`
- `apps/web/src/__tests__/components/CollectionDashboard.test.tsx`
- `apps/web/e2e/collection-dashboard.spec.ts`

**Modify**:
- `apps/web/src/app/dashboard/layout.tsx` (add collection route)

#### API Integration
```typescript
// useCollectionStats.ts
interface CollectionStats {
  totalGames: number;
  privatePdfsCount: number;
  activeChats: number;
  totalReadingMinutes: number;
  recentActivity: ActivityItem[];
}

const { data, isLoading, error } = useQuery({
  queryKey: ['collectionStats', userId],
  queryFn: () => api.get<CollectionStats>(`/users/${userId}/library/stats`)
});
```

---

### Issue E: Add Game to Collection Wizard (Frontend - 5 SP)

**Priority**: 🔴 Critical
**Area**: Frontend
**Dependencies**: Issue F (backend support), Issue G (private PDF upload)

#### Description
Wizard multi-step per aggiungere giochi alla collezione con upload PDF privato.

#### Acceptance Criteria
- [ ] Step 1: Search/select game from SharedGameCatalog OR create custom
- [ ] Step 2: Game details form (if custom game)
- [ ] Step 3: Upload private PDF with real-time progress (SSE)
- [ ] Step 4: Review and confirm (game info + PDF name)
- [ ] Wizard state management con Zustand
- [ ] Validation per ogni step
- [ ] Navigation: Next/Back/Cancel
- [ ] Success state → redirect to collection dashboard
- [ ] Error handling con retry logic
- [ ] Responsive design

#### Technical Tasks
1. Create `AddGameWizard.tsx` with 4 steps
2. Implement `useWizardState` Zustand store
3. Create `SearchSelectGame` step component
4. Create `GameDetailsForm` step component (conditional)
5. Create `UploadPrivatePDF` step component (reuse PDF upload patterns)
6. Create `ReviewConfirm` step component
7. Integrate with backend APIs (Issue F, G)
8. Add unit tests for wizard state
9. Add E2E test: "User adds game with private PDF"

#### Wizard State Management
```typescript
// stores/addGameWizardStore.ts
interface AddGameWizardState {
  step: 1 | 2 | 3 | 4;
  selectedGame?: GameData;
  customGameData?: Partial<CreateGameRequest>;
  uploadedPdfId?: string;
  isProcessing: boolean;

  // Actions
  setStep: (step: number) => void;
  selectGame: (game: GameData) => void;
  setCustomGameData: (data: Partial<CreateGameRequest>) => void;
  setUploadedPdfId: (id: string) => void;
  reset: () => void;
}
```

#### Files to Create/Modify
**Create**:
- `apps/web/src/components/collection/wizard/AddGameWizard.tsx`
- `apps/web/src/components/collection/wizard/steps/SearchSelectGame.tsx`
- `apps/web/src/components/collection/wizard/steps/GameDetailsForm.tsx`
- `apps/web/src/components/collection/wizard/steps/UploadPrivatePDF.tsx`
- `apps/web/src/components/collection/wizard/steps/ReviewConfirm.tsx`
- `apps/web/src/stores/addGameWizardStore.ts`
- `apps/web/src/hooks/useAddGameWizard.ts`
- `apps/web/src/__tests__/components/AddGameWizard.test.tsx`
- `apps/web/e2e/add-game-wizard.spec.ts`

---

### Issue F: UserLibraryEntry PDF Association (Backend - 3 SP)

**Priority**: 🔴 Critical
**Area**: Backend
**Dependencies**: None (extends existing entity)

#### Description
Estendere `UserLibraryEntry` entity per supportare associazione con PDF privati.

#### Acceptance Criteria
- [ ] Add `PrivatePdfId` nullable field to UserLibraryEntry
- [ ] Add `HasPrivatePdf` computed property
- [ ] Add navigation property to PdfDocumentEntity
- [ ] Create EF Core migration
- [ ] Update repository methods (Add, Get, Update)
- [ ] Add domain event `PrivatePdfAssociatedEvent`
- [ ] Unit tests: entity methods
- [ ] Integration tests: repository operations
- [ ] Database migration tested in dev environment

#### Technical Tasks
1. Update `UserLibraryEntry` domain entity
2. Create `PrivatePdfAssociatedEvent` domain event
3. Update `UserLibraryRepository` interface
4. Update `UserLibraryRepository` implementation
5. Create EF Core migration `AddPrivatePdfToUserLibraryEntry`
6. Update DbContext configuration
7. Add unit tests for entity
8. Add integration tests for repository
9. Update OpenAPI documentation

#### Database Migration
```sql
-- Migration: AddPrivatePdfToUserLibraryEntry
ALTER TABLE UserLibraryEntries
ADD PrivatePdfId uniqueidentifier NULL;

ALTER TABLE UserLibraryEntries
ADD CONSTRAINT FK_UserLibraryEntries_PdfDocuments_PrivatePdfId
FOREIGN KEY (PrivatePdfId) REFERENCES PdfDocuments(Id)
ON DELETE SET NULL;

CREATE INDEX IX_UserLibraryEntries_PrivatePdfId
ON UserLibraryEntries(PrivatePdfId);
```

#### Files to Create/Modify
**Modify**:
- `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs`
- `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IUserLibraryRepository.cs`
- `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/Repositories/UserLibraryRepository.cs`
- `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/Data/UserLibraryDbContext.cs`

**Create**:
- `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/PrivatePdfAssociatedEvent.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/UserLibraryEntryTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Infrastructure/UserLibraryRepositoryTests.cs`

#### API Contract Updates
```csharp
// Commands
internal record AddGameToLibraryCommand(
    Guid UserId,
    Guid GameId,
    Guid? PrivatePdfId = null
) : ICommand<UserLibraryEntryDto>;

// DTOs
public record UserLibraryEntryDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    string GameTitle,
    bool HasPrivatePdf,
    Guid? PrivatePdfId,
    DateTime AddedAt
);
```

---

### Issue G: Private PDF Upload Endpoint (Backend - 3 SP)

**Priority**: 🔴 Critical
**Area**: Backend
**Dependencies**: Issue F (UserLibraryEntry changes)

#### Description
Endpoint dedicato per upload di PDF privati associati a UserLibraryEntry.

#### Acceptance Criteria
- [ ] POST endpoint `/users/{userId}/library/entries/{entryId}/pdf`
- [ ] Validate: user owns the library entry
- [ ] Validate: PDF file constraints (max 50MB, .pdf extension)
- [ ] Mark PDF as `IsPrivate = true`
- [ ] Associate PDF with UserLibraryEntry (Issue F)
- [ ] Trigger processing pipeline (extract → chunk → embed)
- [ ] Vector storage in private namespace: `private_{userId}_{gameId}`
- [ ] Return processing status with SSE support (#3324)
- [ ] Error handling: unauthorized, invalid file, processing failure
- [ ] Integration test: full upload → processing → association flow

#### Technical Tasks
1. Create `UploadPrivatePdfCommand` and handler
2. Create `PrivatePdfUploadValidator` (FluentValidation)
3. Add endpoint in `PdfDocumentsEndpoints.cs`
4. Update `PdfProcessingService` for private namespace
5. Update Qdrant collection naming for private vectors
6. Add authorization check (user owns entry)
7. Update OpenAPI documentation
8. Add unit tests for command handler
9. Add integration test with Testcontainers
10. Add E2E test with real PDF file

#### API Contract
```csharp
// Command
internal record UploadPrivatePdfCommand(
    Guid UserId,
    Guid UserLibraryEntryId,
    IFormFile PdfFile
) : ICommand<PrivatePdfUploadResult>;

// Result
public record PrivatePdfUploadResult(
    Guid PdfId,
    string FileName,
    long FileSize,
    ProcessingStatus Status,
    string? SseStreamUrl
);

// Endpoint
app.MapPost("/api/v1/users/{userId}/library/entries/{entryId}/pdf",
    async (
        Guid userId,
        Guid entryId,
        IFormFile file,
        IMediator mediator,
        CancellationToken ct
    ) =>
    {
        var command = new UploadPrivatePdfCommand(userId, entryId, file);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    })
    .RequireAuthorization()
    .DisableAntiforgery()
    .Produces<PrivatePdfUploadResult>(200)
    .Produces(401)
    .Produces(400);
```

#### Files to Create/Modify
**Create**:
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPrivatePdfCommand.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/UploadPrivatePdfCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPrivatePdfValidator.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Handlers/UploadPrivatePdfHandlerTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Integration/PrivatePdfUploadIntegrationTests.cs`

**Modify**:
- `apps/api/src/Api/Routing/PdfDocumentsEndpoints.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfProcessingService.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/QdrantVectorStore.cs`

#### Vector Namespace Strategy
```csharp
// Shared PDF vectors
string collectionName = $"shared_{gameId}";

// Private PDF vectors
string collectionName = $"private_{userId}_{gameId}";

// Query strategy: Search private first, fallback to shared
var privateResults = await qdrant.SearchAsync($"private_{userId}_{gameId}", query);
if (privateResults.Count < threshold)
{
    var sharedResults = await qdrant.SearchAsync($"shared_{gameId}", query);
    return privateResults.Concat(sharedResults);
}
```

---

## 🔗 Dependencies

### External Dependencies
| Epic/Issue | Type | Reason |
|------------|------|--------|
| #3324 | BLOCKER | SSE infrastructure for real-time PDF progress |
| #3370 | BLOCKER | Frontend hook for PDF processing progress |
| #3306 | Foundation | Dashboard Hub skeleton and navigation |
| #3325 | Component | MeepleCard for collection grid |
| #3311 | Component | ActivityFeed for recent actions |

### Internal Dependencies
| Issue | Depends On | Reason |
|-------|------------|--------|
| Issue E | Issue F, G | Wizard needs backend endpoints |
| Issue D | Issue F | Dashboard needs extended entity |
| Issue G | Issue F | Upload needs UserLibraryEntry association |

### Integration Points
| Area | Integration | Details |
|------|-------------|---------|
| Agent System | #3376, #3375 | Chat creation uses UserLibraryEntry games |
| PDF Processing | Existing pipeline | Reuse extract → chunk → embed flow |
| Vector Search | Qdrant | Private namespaces for user-specific vectors |
| Dashboard Hub | #3306 | Collection dashboard extends hub layout |

---

## 🎯 Definition of Done

### Epic-Level DoD
- [ ] All 4 issues completed and merged
- [ ] User can view collection dashboard with stats
- [ ] User can add game with private PDF via wizard
- [ ] Private PDF correctly associated with UserLibraryEntry
- [ ] Private PDF vectors stored in user-specific namespace
- [ ] E2E test: Complete user journey (add game → upload PDF → view collection)
- [ ] Documentation updated (sequenza.md, roadmap.md)
- [ ] No regression in existing features
- [ ] Performance: Collection dashboard loads < 2s

### Issue-Level DoD (per issue)
- [ ] Code review approved
- [ ] Unit tests passing (>85% coverage)
- [ ] Integration tests passing (where applicable)
- [ ] E2E tests passing
- [ ] TypeScript/ESLint checks passing (frontend)
- [ ] Semgrep security scan passing (backend)
- [ ] OpenAPI documentation updated (backend)
- [ ] Accessibility audit passed (frontend)
- [ ] PR merged to main-dev branch

---

## 📊 Success Metrics

### User Engagement
- **Target**: 70% of active users add at least 1 game to collection within 30 days
- **Metric**: Collection creation rate

### Feature Adoption
- **Target**: 50% of collection games have private PDFs uploaded
- **Metric**: Private PDF upload rate

### Performance
- **Target**: Collection dashboard loads < 2s (P95)
- **Metric**: Page load time

### Quality
- **Target**: <5% error rate on wizard completion
- **Metric**: Wizard success/failure ratio

---

## 🚀 Implementation Timeline

### Week 2 - Parallel Execution
```
Day 1-2: Issue F (Backend - UserLibraryEntry extension)
Day 1-3: Issue D (Frontend - Collection Dashboard)
Day 3-4: Issue G (Backend - Private PDF upload)
Day 4-5: Issue E (Frontend - Add Game Wizard)
Day 5: Integration testing and fixes
```

### Parallelization Strategy
**Stream 1 (Frontend)**:
- Day 1-3: Issue D (Collection Dashboard)
- Day 4-5: Issue E (Wizard)

**Stream 2 (Backend)**:
- Day 1-2: Issue F (Entity extension)
- Day 3-4: Issue G (Upload endpoint)

**Efficiency**: 5 days vs 10 days sequential (50% time saving)

---

## 📝 Notes

### Design Decisions
1. **Private vs Shared PDFs**: Separate vector namespaces to prevent cross-user data leakage
2. **UserLibraryEntry Extension**: Preferred over new entity to maintain existing relationships
3. **Wizard Pattern**: Reuse Agent Creation Wizard (#3376) state management pattern
4. **Component Reuse**: Leverage MeepleCard (#3325) and ActivityFeed (#3311)

### Future Enhancements (Out of Scope)
- Multiple collections per user (e.g., "To Play", "Favorites", "Archive")
- Collection sharing with other users
- Collection import/export (JSON, CSV)
- Collection tags and categories
- Bulk operations (add multiple games, delete collection)
- Collection sorting presets
- Advanced search with filters (players, duration, complexity)

### Risks & Mitigation
| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| SSE infrastructure delays (#3324) | High | Medium | Fallback: Polling mechanism temporary |
| Private PDF namespace complexity | Medium | Low | Spike: Validate Qdrant collection naming |
| Wizard state management bugs | Medium | Medium | Comprehensive unit tests for store |
| UserLibraryEntry migration issues | High | Low | Test migration in dev environment first |

---

## 🔍 References

### Related Epic/Issues
- Epic #3306: User Dashboard Hub Core - MVP
- Epic #3386: Agent Creation & Testing Flow
- #3324: SSE Infrastructure (BLOCKER)
- #3370: usePdfProcessingProgress hook
- #3325: MeepleCard - Universal Card System
- #3311: ActivityFeed Timeline

### Documentation
- `docs/01-architecture/bounded-contexts/UserLibrary.md`
- `docs/01-architecture/bounded-contexts/DocumentProcessing.md`
- `docs/02-development/frontend/wizard-patterns.md`
- `docs/claudedocs/sequenza.md`

### Technical Specs
- ADR-015: Private PDF Isolation Strategy
- ADR-022: Vector Namespace Convention
- ADR-031: Wizard State Management with Zustand
