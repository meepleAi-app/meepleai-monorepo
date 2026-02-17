# UserLibrary Bounded Context

**Personal Collections, Wishlist, Played History, Labels, Private PDFs, Sharing**

---

## Responsibilities

- Personal game collection (add/remove/organize)
- Wishlist management
- Played history (sessions + stats)
- Custom labels & categorization
- Private PDFs per game
- Library sharing (public links)
- Game state tracking (ownership, condition, loans)
- Agent configuration per game
- Quota enforcement (tier-based)
- Private games (Phase 2 - pending)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **UserLibraryEntry** | Id, UserId, GameId, TimesPlayed, LastPlayedAt, PrivatePdfDocumentId, AgentConfig, LabelIds[] | Aggregate root |
| **Label** | Id, UserId, Name, Color, IsSystem | Custom labels |
| **LibraryShareLink** | Id, UserId, ShareToken, IsActive, ExpiresAt, MaxViews, ViewCount | Public sharing |

**Value Objects**: AgentConfiguration (ModelType, Temperature, RagStrategy)

**Domain Methods**: `RecordPlay()`, `AssociatePrivatePdf()`, `ConfigureAgent()`, `LoanTo()`, `ReturnFromLoan()`, `AddLabel()`

---

## API Operations (42 total)

**24 Commands**: AddGameToLibrary, RemoveGameFromLibrary, UpdateLibraryEntry, UploadCustomGamePdf, ResetGamePdf, RemovePrivatePdf, ConfigureGameAgent, ResetGameAgent, SaveAgentConfig, CreateCustomLabel, DeleteCustomLabel, AddLabelToGame, RemoveLabelFromGame, CreateLibraryShareLink, UpdateLibraryShareLink, RevokeLibraryShareLink, RecordGameSession, SendLoanReminder, +6 private game commands (pending)

**15 Queries**: GetUserLibrary, GetLibraryStats, GetLibraryQuota, GetGameInLibraryStatus, GetGamePdfs, GetGameAgentConfig, GetLabels, GetGameLabels, GetLibraryShareLink, GetSharedLibrary, GetGameDetail, GetGameChecklist, +3 private game queries (pending)

---

## Key Endpoints

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| GET | `/api/v1/library` | Session | List collection (paginated, filtered) |
| GET | `/api/v1/library/stats` | Session | Dashboard stats |
| GET | `/api/v1/library/quota` | Session | Tier quota usage |
| POST | `/api/v1/library/games/{gameId}` | Session | Add game |
| DELETE | `/api/v1/library/games/{gameId}` | Session | Remove game |
| PATCH | `/api/v1/library/games/{gameId}` | Session | Update entry |
| POST | `/api/v1/library/games/{gameId}/pdf` | Session | Upload private PDF |
| GET | `/api/v1/library/{entryId}/pdf/progress` | Session | SSE progress |
| PUT | `/api/v1/library/games/{gameId}/agent` | Session | Configure agent |
| POST | `/api/v1/library/labels` | Session | Create label |
| POST | `/api/v1/library/share` | Session | Create share link |
| GET | `/api/v1/library/shared/{shareToken}` | 🟢 Public | View shared library |

---

## Quota Limits

| Tier | Max Games |
|------|-----------|
| Free | 10 |
| Basic | 50 |
| Premium | 500 |
| Enterprise | Unlimited |

---

## PDF Management (Issue #3489)

**Upload Flow**:
1. User uploads private PDF
2. DocumentProcessing extracts text
3. SSE progress updates (Issue #3653)
4. PDF associated with library entry

**SSE Events**: `progress` (extraction/chunking), `error`, `complete`, `heartbeat` (30s)

**GetGamePdfs** returns: Shared (community) + Private (user-uploaded) PDFs

---

## Agent Configuration (Issue #3212)

**Purpose**: Per-game AI preferences (model, temperature, RAG strategy)

**Use Case**: User wants GPT-4 for Azul but Haiku for simpler games

**ConfigureGameAgent** accepts:
- ModelType: Valid LLM model
- Temperature: 0.0-2.0
- RagStrategy: Fast | Balanced | Precise | Expert | Consensus

---

## Labels & Organization (Epic #3511)

**System Labels** (pre-defined):
- Favorites (#FF5722)
- Wishlist (#2196F3)
- Played (#4CAF50)

**Custom Labels**: User-created, 1-50 chars, unique per user, hex color

**Filtering**: GetUserLibrary accepts `labelIds` query parameter

---

## Library Sharing

**CreateLibraryShareLink** generates:
- ShareToken (URL-safe)
- ShareUrl: `https://meepleai.dev/library/shared/{token}`
- ExpiresAt, MaxViews

**GetSharedLibrary** (public access):
- Excludes: Purchase prices, loan status, agent configs, private notes
- Excludes: Private PDFs

**Rate Limiting**: 10 share links/day per user

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `GameAddedToLibraryEvent` | Game added | Administration (analytics) |
| `GameRemovedFromLibraryEvent` | Game removed | Administration |
| `PrivatePdfUploadedEvent` | PDF upload started | DocumentProcessing |
| `PlayRecordedEvent` | Session recorded | GameManagement |
| `LabelCreatedEvent` | Custom label created | Administration |
| `ShareLinkCreatedEvent` | Share link generated | Administration |
| `ShareLinkAccessedEvent` | Shared library viewed | UserNotifications |

---

## Integration Points

**Inbound**:
- GameManagement → game metadata (title, image, publisher)
- DocumentProcessing → private PDF processing + SSE progress

**Outbound**:
- DocumentProcessing → trigger PDF extraction
- UserNotifications → loan reminders, share link notifications
- GameManagement → PlayRecordedEvent for stats

---

## Security

**Access Control**:
- User isolation (only own library)
- Admin override (audit only)
- Share links: public with token validation (expiry + view limits)
- Private PDFs: uploader only (not shared)

**Data Privacy (Share Link Sanitization)**:
- Excludes: Purchase prices, loan status, agent configs, private notes, private PDFs

---

## Performance

**Caching**:
- GetUserLibrary: Redis 2m (invalidate: GameAddedEvent, GameRemovedEvent, EntryUpdatedEvent)
- GetLibraryStats: Redis 5m (invalidate: PlayRecordedEvent)
- GetLabels: Redis 30m (invalidate: LabelCreatedEvent, LabelDeletedEvent)
- GetSharedLibrary: Redis 10m (invalidate: ShareLinkRevokedEvent)

**Database Indexes**:
```sql
idx_library_user_game ON UserLibraryEntries(UserId, GameId) WHERE NOT IsDeleted
idx_library_labels ON UserLibraryEntries_Labels(EntryId, LabelId)
idx_library_played ON UserLibraryEntries(UserId, LastPlayedAt DESC NULLS LAST)
idx_sharelinks_token ON LibraryShareLinks(ShareToken) WHERE IsActive = TRUE
```

---

## Testing

**Unit Tests** (`tests/Api.Tests/UserLibrary/`):
- UserLibraryEntry_Tests.cs (domain logic)
- Label_Tests.cs (system vs custom)
- LibraryShareLink_Tests.cs (view count, expiry)
- AgentConfiguration_Tests.cs (validation)

**Integration Tests** (Testcontainers):
1. Add game → configure agent → upload PDF → record play
2. Create custom label → filter library by label
3. Create share link → verify public access → check sanitization
4. Upload private PDF → verify SSE progress → check isolation

---

## Code Location

`apps/api/src/Api/BoundedContexts/UserLibrary/`

---

## Related Documentation

**Contexts**:
- [GameManagement](./game-management.md) - Game catalog source
- [DocumentProcessing](./document-processing.md) - Private PDF processing
- [SharedGameCatalog](./shared-game-catalog.md) - Private game proposals (Phase 4)

---

**Status**: ✅ Production (Private Games: 🔴 Phase 2-5 pending)
**Commands**: 24 | **Queries**: 15 | **Endpoints Mapped**: 34 (8 pending)
