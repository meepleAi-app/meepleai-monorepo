# SharedGameCatalog Bounded Context

**Community Catalog, Publication Workflow, Approval System, BGG Integration, Soft-Delete**

---

## Responsibilities

- Community-driven game catalog
- Publication workflow (Draft → PendingApproval → Published)
- Admin approval system + review locking
- Soft-delete workflow + audit trail (ADR-019)
- PostgreSQL Full-Text Search (Italian + English - ADR-018)
- BoardGameGeek API integration
- Share request management (user proposals)
- Document versioning + RAG approval
- Game state template generation
- FAQ, Errata, Quick Questions
- Badge system (contributor recognition)
- Bulk operations (batch approval/rejection)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **SharedGame** | Id, Title, Status, BggId, PublishedAt, ApprovedBy, IsDeleted, DeletedAt, Faqs[], Errata[], QuickQuestions[], Documents[] | Aggregate root |
| **ShareRequest** | Id, UserId, PrivateGameId, Status, ReviewedBy, IsReviewLocked, LockedBy, LockExpires | User proposals |
| **DeleteRequest** | Id, SharedGameId, RequestedBy, Status, Reason, ReviewedBy | Editor delete requests |
| **Badge** | Id, Name, Description, Type, RequiredCount, IsHidden | Contributor badges |
| **UserBadge** | UserId, BadgeId, EarnedAt, IsDisplayed | Earned badges |

**Value Objects**: PublicationStatus (Draft/PendingApproval/Published/Archived), ShareRequestStatus (Draft/Submitted/UnderReview/ChangesRequested/Approved/Rejected), DeleteRequestStatus (Pending/Approved/Rejected), BadgeType (Contribution/Milestone/Achievement)

**Domain Methods**: `SubmitForApproval()`, `Approve()`, `Reject()`, `Archive()`, `SoftDelete()`, `Restore()`, `AddFaq()`, `AcquireReviewLock()`

---

## API Operations (69 total)

**46 Commands**: Create/Update/Delete SharedGame, SubmitForApproval, Approve/RejectPublication, BatchApprove/Reject, RequestDelete, ApproveDeleteRequest, Add/Update/Delete Faq, Add/Update/Delete Errata, GenerateQuickQuestions, AddManualQuickQuestion, AddDocument, SetActiveDocumentVersion, ApproveDocumentForRag, GenerateGameStateTemplate, SearchBggGames, ImportGameFromBgg, UpdateFromBgg, BulkImportGames, CreateShareRequest, ApproveShareRequest, RejectShareRequest, RequestChanges, WithdrawShareRequest, BulkApproveShareRequests, +22 others

**23 Queries**: SearchSharedGames, GetSharedGameById, GetGameFaqs, GetQuickQuestions, GetCategories, GetMechanics, GetAllSharedGames, GetPendingApprovalGames, GetPendingDeleteRequests, GetDocumentsBySharedGame, GetActiveDocuments, GetActiveRulebookAnalysis, CheckBggDuplicate, GetByBggId, GetPendingShareRequests, GetShareRequestDetails, GetMyActiveReviews, GetApprovalQueue, GetAllBadges, GetUserBadges, GetUserContributions, GetBadgeLeaderboard, GetGameContributors

---

## Key Endpoints (80+)

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| GET | `/api/v1/shared-games` | 🟢 Public | Search catalog (FTS) |
| GET | `/api/v1/shared-games/{id}` | 🟢 Public | Game details |
| GET | `/api/v1/games/{gameId}/faqs` | 🟢 Public | FAQs (paginated) |
| POST | `/api/v1/faqs/{faqId}/upvote` | 🟢 Public | Upvote FAQ |
| POST | `/api/v1/admin/shared-games` | Editor+ | Create game (draft) |
| PUT | `/api/v1/admin/shared-games/{id}` | Editor+ | Update game |
| DELETE | `/api/v1/admin/shared-games/{id}` | Admin (direct) / Editor (request) | Delete game |
| POST | `/api/v1/admin/shared-games/{id}/submit-for-approval` | Editor+ | Submit for review |
| POST | `/api/v1/admin/shared-games/{id}/approve-publication` | Admin | Approve → Published |
| POST | `/api/v1/admin/shared-games/{id}/reject-publication` | Admin | Reject → Draft |
| POST | `/api/v1/admin/shared-games/batch-approve` | Admin | Batch approve |
| POST | `/api/v1/admin/shared-games/{id}/faq` | Editor+ | Add FAQ |
| POST | `/api/v1/admin/shared-games/{id}/quick-questions/generate` | Editor+ | AI-generate Q&A |
| POST | `/api/v1/admin/shared-games/{id}/documents/{docId}/approve` | Admin | Approve for RAG |
| GET | `/api/v1/admin/shared-games/bgg/search` | Editor+ | Search BGG |
| POST | `/api/v1/admin/shared-games/import-bgg` | Editor+ | Import from BGG |
| POST | `/api/v1/share-requests` | User | Propose private game |
| GET | `/api/v1/admin/share-requests` | Admin | Pending requests |
| POST | `/api/v1/admin/share-requests/{id}/approve` | Admin | Approve request |
| GET | `/api/v1/admin/shared-games/approval-queue` | Admin | Smart approval queue |
| GET | `/api/v1/badges` | 🟢 Public | All badges |
| GET | `/api/v1/badges/leaderboard` | 🟢 Public | Badge leaderboard |

---

## Publication Workflow (Issue #2514)

**Status Flow**:
```
Draft → SubmitForApproval → PendingApproval
                                 ↓
                     ApprovePublication → Published
                                 ↓
                      RejectPublication → Draft (with feedback)

Published → Archive → Archived
```

**ApproveSharedGamePublicationCommand**:
- Sets: Status = Published, PublishedAt = UtcNow, ApprovedBy = AdminId
- Raises: `GamePublishedEvent` → UserNotifications (notify submitter)
- Side Effect: Increment submitter contribution count (badge system)

**BatchApproveGamesCommand** (Issue #3350):
- Partial success allowed (continues on failures)
- Returns: `{ successCount, failedCount, errors[] }`

---

## Soft-Delete Workflow (ADR-019)

**Fields**: IsDeleted, DeletedAt, DeletedBy

**Global Filter**: `modelBuilder.Entity<SharedGame>().HasQueryFilter(g => !g.IsDeleted);`

**Editor**: Creates DeleteRequest (awaits admin approval)

**Admin**: Direct soft-delete (sets IsDeleted=true immediately)

**Restore**: `Restore()` method resets IsDeleted, DeletedAt, DeletedBy

---

## PostgreSQL FTS (ADR-018)

**Index**:
```sql
CREATE INDEX idx_sharedgames_fts ON SharedGames
USING gin(to_tsvector('italian', Title || ' ' || Description || ' ' || COALESCE(Rules, '')));
```

**SearchSharedGamesQuery**:
- Query params: `q` (search term), `categories[]`, `mechanics[]`, `minPlayers`, `maxPlayers`, `playingTime`, `page`, `pageSize`
- Searches: Title, Description, Rules content
- Fallback: English FTS if Italian finds no results
- Target: <100ms P95

---

## Share Request Workflow

**Status Flow**:
```
Draft → Submitted → UnderReview (lock acquired)
                         ↓
              RequestChanges → User revises → Re-submitted
                         ↓
                    Approved → SharedGame created (Published)
                         ↓
                    Rejected (with reason)
```

**Review Lock**:
- Duration: 30 minutes (configurable)
- Auto-release if admin doesn't complete
- Conflict: 409 if another admin holds lock

**ApproveShareRequest**:
1. Creates SharedGame from request data
2. Sets SharedGame.Status = Published
3. Transfers documents to SharedGame
4. Sets ShareRequest.Status = Approved
5. Awards badge to submitter (if eligible)
6. Raises `ShareRequestApprovedEvent`

---

## BGG Integration

**ImportGameFromBggCommand**:
- Rate limiting: 2 req/s to BGG API
- Creates SharedGame with Status = Draft
- Optionally imports: categories, mechanics, image

**CheckBggDuplicateQuery**:
- Returns: `{ exists: bool, gameId: guid, differences: {...} }`
- Use case: Prevent duplicate imports

**BulkImportGamesCommand**:
- Input: CSV file OR BGG ID list
- Implementation: Queue job system (Hangfire)
- Returns: Job ID for tracking

---

## Approval Queue (Issue #3533)

**GetApprovalQueueQuery**:
- Combines: Pending publication + pending share requests
- Features: Document status, submitter reputation, urgency flags
- Filters: urgency (Low/Medium/High), submitter, hasPdfs

**Response includes**:
- Document status: hasPdfs, pdfCount, ragProcessed, qualityScore
- Submitter reputation: totalContributions, approvalRate, badges
- Urgency: ageInDays, urgency level

---

## Badge System (Issue #2736)

**Badge Types**:
- Contribution: 1st FAQ, 10th FAQ, 100th FAQ, First Game Published
- Milestone: 50 Contributions, 100 Contributions, Quality Contributor (90%+ approval)
- Achievement: Top Contributor (monthly), BGG Importer (10+ games)

**GetBadgeLeaderboardQuery**:
- Query params: period (week/month/year/allTime), badgeType, page, pageSize
- Returns: Ranked users by contribution count or badge count

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `SharedGameCreatedEvent` | Game created | Administration (audit) |
| `GamePublishedEvent` | Approved | UserNotifications, Administration (badge calc) |
| `ShareRequestCreatedEvent` | User submits | UserNotifications (notify admins) |
| `ShareRequestApprovedEvent` | Admin approves | UserNotifications, Administration (badge) |
| `ShareRequestRejectedEvent` | Admin rejects | UserNotifications |
| `DeleteRequestCreatedEvent` | Editor requests delete | Administration |
| `GameSoftDeletedEvent` | Game deleted | KnowledgeBase (cleanup vectors) |
| `DocumentApprovedEvent` | Doc approved for RAG | DocumentProcessing |
| `BadgeEarnedEvent` | Badge awarded | UserNotifications |

---

## Integration Points

**Inbound**:
- GameManagement → SharedGameId linking
- UserLibrary → ShareRequest proposals

**Outbound**:
- DocumentProcessing → PDF processing, RAG indexing
- KnowledgeBase → AI generation (Q&A, state templates, analysis)
- BoardGameGeek API → Import metadata (rate limited: 2 req/s)

---

## Security & Authorization

| Endpoint Pattern | Anonymous | User | Editor | Admin |
|------------------|-----------|------|--------|-------|
| GET /shared-games | ✅ | ✅ | ✅ | ✅ |
| POST /share-requests | ❌ | ✅ | ✅ | ✅ |
| POST /admin/shared-games | ❌ | ❌ | ✅ | ✅ |
| POST /admin/.../approve | ❌ | ❌ | ❌ | ✅ |
| DELETE /admin/.../... (direct) | ❌ | ❌ | ❌ | ✅ |
| DELETE /admin/.../... (request) | ❌ | ❌ | ✅ | ✅ |

**Data Access**:
- Published: Public (searchable by all)
- Draft: Creator + admins
- PendingApproval: Admins only
- Soft-Deleted: Hidden (global filter)

---

## Performance

**Caching**:
- SearchSharedGames: Redis 5m (invalidate: GamePublishedEvent, GameUpdatedEvent)
- GetSharedGameById: Redis 30m (invalidate: GameUpdatedEvent)
- GetPendingApprovals: Redis 1m (invalidate: SubmitForApprovalEvent, ApproveEvent)
- GetBadgeLeaderboard: Redis 1h (invalidate: BadgeEarnedEvent)

---

## Testing

**Unit Tests** (`tests/Api.Tests/SharedGameCatalog/`):
- SharedGame_Tests.cs (publication workflow, soft-delete)
- ShareRequest_Tests.cs (review lock, status transitions)
- DeleteRequest_Tests.cs (approval workflow)
- Badge_Tests.cs (earning logic)

**Integration Tests** (Testcontainers):
1. Create draft → submit → approve → verify published
2. Share request → lock → approve → verify SharedGame created
3. BGG import → check duplicate → import → verify draft created
4. Soft-delete (editor creates request, admin approves, verify IsDeleted=true)
5. Badge earning (approve 10 games, verify badge awarded)

---

## Code Location

`apps/api/src/Api/BoundedContexts/SharedGameCatalog/`

---

## Related Documentation

**ADRs**:
- [ADR-018: PostgreSQL FTS](../../01-architecture/adr/adr-018-postgresql-fts-for-shared-catalog.md)
- [ADR-019: Soft-Delete Workflow](../../01-architecture/adr/adr-019-shared-catalog-delete-workflow.md)
- [ADR-025: SharedGameCatalog Bounded Context](../../01-architecture/adr/adr-025-shared-catalog-bounded-context.md)

**Contexts**:
- [GameManagement](./game-management.md) - Private → Shared linking
- [UserLibrary](./user-library.md) - Share request workflow
- [DocumentProcessing](./document-processing.md) - PDF processing
- [KnowledgeBase](./knowledge-base.md) - RAG indexing

---

**Status**: ✅ Production
**Commands**: 46 | **Queries**: 23 | **Endpoints**: 80+ | **Workflow Areas**: 11 | **Integration Points**: 5 contexts + BGG API
