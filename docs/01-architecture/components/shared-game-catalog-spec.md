# SharedGameCatalog - Specifica Tecnica Completa

**Feature**: Database Giochi Condiviso con Gestione Admin/Editor
**Status**: Specifica Brainstorming → Ready for Implementation
**Created**: 2026-01-12
**Target**: Ridurre chiamate BGG, migliorare UX ricerca giochi

---

## 📋 Executive Summary

### Obiettivo
Creare un catalogo centralizzato di giochi da tavolo gestito da Admin/Editor per ridurre dipendenza da BGG API e migliorare esperienza utente con ricerca istantanea e informazioni ricche.

### Value Proposition
- **Performance**: Ricerca istantanea vs chiamate BGG (latenza ridotta 90%)
- **UX**: Informazioni dettagliate (regole, FAQ, errata) sempre disponibili
- **Governance**: Controllo qualità dati tramite curation Admin/Editor
- **Scalabilità**: Caching efficace, database ottimizzato per centinaia di giochi

### Key Decisions
| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Modello Dati** | Extended (regole, FAQ, errata) | Massima ricchezza informativa |
| **Ruoli** | Hybrid (Admin full, Editor con delete approval) | Balance autonomia/controllo |
| **Flusso Utente** | Search-First (locale → BGG fallback) | Performance + opzione completezza |
| **Bounded Context** | Nuovo `SharedGameCatalog` | Separazione ownership, scalabilità |
| **Scala** | Centinaia giochi | Ottimizzazioni PostgreSQL FTS sufficienti |
| **Import** | Manual Curation | Qualità dati controllata |
| **UI Admin** | Sezione dedicata `/admin/games-catalog` | Workflow management completo |

---

## 🏗️ Architettura DDD

### Bounded Context: SharedGameCatalog

**Responsabilità**:
- Gestione catalogo giochi condiviso (CRUD, workflow publish)
- Ricerca full-text giochi
- Import da BGG (manuale on-demand)
- Gestione FAQ e Errata per ogni gioco
- Workflow approvazione delete (Editor → Admin)
- Audit log modifiche

**Context Mapping**:
```
SharedGameCatalog (upstream) → GameManagement (downstream)
  - Shared Kernel: SharedGameId (FK in Games table)
  - Anti-Corruption Layer: GameManagement non dipende da SharedGame domain
```

---

## 📊 Domain Model

### Aggregates & Entities

#### 1. SharedGame (Aggregate Root)
```csharp
public class SharedGame : AggregateRoot<Guid>
{
    // Identity
    public Guid Id { get; private set; }
    public int? BggId { get; private set; }  // Nullable, unique constraint

    // Core Info
    public string Title { get; private set; }
    public int YearPublished { get; private set; }
    public string Description { get; private set; }

    // Gameplay
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }
    public int PlayingTimeMinutes { get; private set; }
    public int MinAge { get; private set; }

    // Ratings
    public decimal? ComplexityRating { get; private set; }  // 1.0 - 5.0
    public decimal? AverageRating { get; private set; }     // 1.0 - 10.0

    // Media
    public string ImageUrl { get; private set; }
    public string ThumbnailUrl { get; private set; }

    // Status & Metadata
    public GameStatus Status { get; private set; }  // Draft, Published, Archived
    public Guid CreatedBy { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ModifiedAt { get; private set; }

    // Full-Text Search
    public string SearchVector { get; private set; }  // tsvector for PostgreSQL FTS

    // Collections (navigation properties)
    public ICollection<GameDesigner> Designers { get; private set; }
    public ICollection<GamePublisher> Publishers { get; private set; }
    public ICollection<GameCategory> Categories { get; private set; }
    public ICollection<GameMechanic> Mechanics { get; private set; }
    public ICollection<GameFaq> Faqs { get; private set; }
    public ICollection<GameErrata> Erratas { get; private set; }

    // Value Objects
    public GameRules Rules { get; private set; }

    // Domain Methods
    public void UpdateInfo(/* params */) { /* validation + event */ }
    public void Publish() { /* status validation + event */ }
    public void Archive() { /* event */ }
    public void AddFaq(GameFaq faq) { /* validation + add */ }
    public void AddErrata(GameErrata errata) { /* validation + add */ }
}

public enum GameStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
```

#### 2. GameDesigner (Entity)
```csharp
public class GameDesigner : Entity<Guid>
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public ICollection<SharedGame> Games { get; private set; }
}
```

#### 3. GamePublisher (Entity)
```csharp
public class GamePublisher : Entity<Guid>
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public ICollection<SharedGame> Games { get; private set; }
}
```

#### 4. GameCategory (Taxonomy Entity)
```csharp
public class GameCategory : Entity<Guid>
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }  // Strategy, Family, Party, etc.
    public string Slug { get; private set; }
    public ICollection<SharedGame> Games { get; private set; }
}
```

#### 5. GameMechanic (Taxonomy Entity)
```csharp
public class GameMechanic : Entity<Guid>
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }  // Deck Building, Worker Placement, etc.
    public string Slug { get; private set; }
    public ICollection<SharedGame> Games { get; private set; }
}
```

#### 6. GameFaq (Entity)
```csharp
public class GameFaq : Entity<Guid>
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public string Question { get; private set; }
    public string Answer { get; private set; }
    public int Order { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public SharedGame SharedGame { get; private set; }
}
```

#### 7. GameErrata (Entity)
```csharp
public class GameErrata : Entity<Guid>
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public string Description { get; private set; }
    public string PageReference { get; private set; }  // "Page 15, Rule 3.2"
    public DateTime PublishedDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public SharedGame SharedGame { get; private set; }
}
```

#### 8. SharedGameDeleteRequest (Entity)
```csharp
public class SharedGameDeleteRequest : Entity<Guid>
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public Guid RequestedBy { get; private set; }  // Editor UserId
    public string Reason { get; private set; }
    public DeleteRequestStatus Status { get; private set; }  // Pending, Approved, Rejected
    public Guid? ReviewedBy { get; private set; }  // Admin UserId
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public SharedGame SharedGame { get; private set; }
}

public enum DeleteRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
```

### Value Objects

#### GameRules
```csharp
public class GameRules : ValueObject
{
    public string Content { get; private set; }  // Rich text (HTML/Markdown)
    public string Language { get; private set; }  // "it", "en"

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Content;
        yield return Language;
    }
}
```

### Domain Events
```csharp
public record SharedGameCreated(Guid GameId, string Title, Guid CreatedBy) : DomainEvent;
public record SharedGameUpdated(Guid GameId, Guid ModifiedBy) : DomainEvent;
public record SharedGamePublished(Guid GameId, Guid PublishedBy) : DomainEvent;
public record SharedGameArchived(Guid GameId, Guid ArchivedBy) : DomainEvent;
public record SharedGameDeleteRequested(Guid GameId, Guid RequestId, Guid RequestedBy) : DomainEvent;
public record SharedGameDeleted(Guid GameId, Guid DeletedBy) : DomainEvent;
public record GameFaqAdded(Guid GameId, Guid FaqId) : DomainEvent;
public record GameErrataAdded(Guid GameId, Guid ErrataId) : DomainEvent;
```

---

## 🔄 CQRS Commands & Queries

### Commands (Write Operations)

#### 1. CreateSharedGameCommand
```csharp
public record CreateSharedGameCommand(
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto Rules,
    List<Guid> DesignerIds,
    List<Guid> PublisherIds,
    List<Guid> CategoryIds,
    List<Guid> MechanicIds,
    int? BggId = null
) : IRequest<Guid>;

// Handler: CreateSharedGameCommandHandler
// Authorization: Admin or Editor role
// Validation: Title required, Year > 1900, Players > 0, etc.
// Event: SharedGameCreated
```

#### 2. UpdateSharedGameCommand
```csharp
public record UpdateSharedGameCommand(
    Guid GameId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto Rules,
    List<Guid> DesignerIds,
    List<Guid> PublisherIds,
    List<Guid> CategoryIds,
    List<Guid> MechanicIds
) : IRequest<Unit>;

// Handler: UpdateSharedGameCommandHandler
// Authorization: Admin or Editor role
// Validation: Game exists, not Archived
// Event: SharedGameUpdated
```

#### 3. PublishSharedGameCommand
```csharp
public record PublishSharedGameCommand(Guid GameId) : IRequest<Unit>;

// Handler: PublishSharedGameCommandHandler
// Authorization: Admin or Editor role
// Validation: Game in Draft status, all required fields populated
// Event: SharedGamePublished
```

#### 4. ArchiveSharedGameCommand
```csharp
public record ArchiveSharedGameCommand(Guid GameId) : IRequest<Unit>;

// Handler: ArchiveSharedGameCommandHandler
// Authorization: Admin only
// Event: SharedGameArchived
```

#### 5. RequestDeleteSharedGameCommand
```csharp
public record RequestDeleteSharedGameCommand(
    Guid GameId,
    string Reason
) : IRequest<Guid>;  // Returns DeleteRequestId

// Handler: RequestDeleteSharedGameCommandHandler
// Authorization: Editor role (Admin can delete directly)
// Creates SharedGameDeleteRequest with Pending status
// Event: SharedGameDeleteRequested
```

#### 6. DeleteSharedGameCommand
```csharp
public record DeleteSharedGameCommand(Guid GameId) : IRequest<Unit>;

// Handler: DeleteSharedGameCommandHandler
// Authorization: Admin only (or auto-approve if Admin requests)
// Soft delete: set IsDeleted flag or hard delete (decide)
// Event: SharedGameDeleted
```

#### 7. ApproveDeleteRequestCommand
```csharp
public record ApproveDeleteRequestCommand(Guid RequestId) : IRequest<Unit>;

// Handler: ApproveDeleteRequestCommandHandler
// Authorization: Admin only
// Updates DeleteRequest status to Approved, then calls DeleteSharedGameCommand
```

#### 8. RejectDeleteRequestCommand
```csharp
public record RejectDeleteRequestCommand(Guid RequestId, string Reason) : IRequest<Unit>;

// Handler: RejectDeleteRequestCommandHandler
// Authorization: Admin only
```

#### 9. ImportGameFromBggCommand
```csharp
public record ImportGameFromBggCommand(int BggId) : IRequest<Guid>;

// Handler: ImportGameFromBggCommandHandler
// Authorization: Admin or Editor role
// Calls BggApiService, creates SharedGame in Draft status
// Event: SharedGameCreated
```

#### 10. BulkImportGamesCommand
```csharp
public record BulkImportGamesCommand(
    List<BulkGameImportDto> Games
) : IRequest<BulkImportResultDto>;

public record BulkGameImportDto(
    string Title,
    int YearPublished,
    int? BggId,
    // ... other fields
);

public record BulkImportResultDto(
    int SuccessCount,
    int FailureCount,
    List<string> Errors
);

// Handler: BulkImportGamesCommandHandler
// Authorization: Admin only
// Validates each game, creates in Draft, returns summary
```

#### 11. AddGameFaqCommand
```csharp
public record AddGameFaqCommand(
    Guid GameId,
    string Question,
    string Answer,
    int Order
) : IRequest<Guid>;

// Handler: AddGameFaqCommandHandler
// Authorization: Admin or Editor
// Event: GameFaqAdded
```

#### 12. UpdateGameFaqCommand, DeleteGameFaqCommand
*Similar structure to AddGameFaqCommand*

#### 13. AddGameErrataCommand
```csharp
public record AddGameErrataCommand(
    Guid GameId,
    string Description,
    string PageReference,
    DateTime PublishedDate
) : IRequest<Guid>;

// Handler: AddGameErrataCommandHandler
// Authorization: Admin or Editor
// Event: GameErrataAdded
```

#### 14. UpdateGameErrataCommand, DeleteGameErrataCommand
*Similar structure to AddGameErrataCommand*

---

### Queries (Read Operations)

#### 1. SearchSharedGamesQuery
```csharp
public record SearchSharedGamesQuery(
    string? SearchTerm,
    List<Guid>? CategoryIds,
    List<Guid>? MechanicIds,
    int? MinPlayers,
    int? MaxPlayers,
    int? MaxPlayingTime,
    GameStatus? Status,
    int PageNumber = 1,
    int PageSize = 20,
    string SortBy = "Title",
    bool SortDescending = false
) : IRequest<PagedResult<SharedGameDto>>;

// Handler: SearchSharedGamesQueryHandler
// Uses PostgreSQL FTS (tsvector) for SearchTerm
// Authorization: Public (filters Published only for non-admin)
// Caching: HybridCache L1+L2, TTL 1 hour
```

#### 2. GetSharedGameByIdQuery
```csharp
public record GetSharedGameByIdQuery(Guid GameId) : IRequest<SharedGameDetailDto>;

// Handler: GetSharedGameByIdQueryHandler
// Includes: Designers, Publishers, Categories, Mechanics, FAQs, Errata
// Authorization: Public (if Published), Admin/Editor (all statuses)
// Caching: HybridCache L1, TTL 30 minutes
```

#### 3. GetAllSharedGamesQuery
```csharp
public record GetAllSharedGamesQuery(
    GameStatus? Status,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<SharedGameDto>>;

// Handler: GetAllSharedGamesQueryHandler
// For admin UI list view
// Authorization: Admin or Editor
```

#### 4. GetGameCategoriesQuery
```csharp
public record GetGameCategoriesQuery : IRequest<List<GameCategoryDto>>;

// Handler: GetGameCategoriesQueryHandler
// Returns all categories for filters/dropdowns
// Authorization: Public
// Caching: HybridCache, TTL 24 hours
```

#### 5. GetGameMechanicsQuery
```csharp
public record GetGameMechanicsQuery : IRequest<List<GameMechanicDto>>;

// Handler: GetGameMechanicsQueryHandler
// Returns all mechanics for filters/dropdowns
// Authorization: Public
// Caching: HybridCache, TTL 24 hours
```

#### 6. GetPendingDeleteRequestsQuery
```csharp
public record GetPendingDeleteRequestsQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<DeleteRequestDto>>;

// Handler: GetPendingDeleteRequestsQueryHandler
// For admin approval UI
// Authorization: Admin only
```

#### 7. GetGameAuditLogQuery
```csharp
public record GetGameAuditLogQuery(
    Guid GameId,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<PagedResult<AuditLogEntryDto>>;

// Handler: GetGameAuditLogQueryHandler
// Shows change history for a game
// Authorization: Admin only
// Implementation: DomainEvents table or dedicated AuditLog table
```

---

## 🌐 HTTP Endpoints

### Public Endpoints (User-Facing)

```
GET  /api/v1/shared-games
     → SearchSharedGamesQuery
     Query Params: search, categoryIds, mechanicIds, minPlayers, maxPlayers,
                   maxPlayingTime, pageNumber, pageSize, sortBy, sortDescending
     Response: PagedResult<SharedGameDto>
     Auth: None (filters Published only)

GET  /api/v1/shared-games/{id}
     → GetSharedGameByIdQuery
     Response: SharedGameDetailDto (includes FAQ, Errata, Relations)
     Auth: None (if Published)

GET  /api/v1/shared-games/categories
     → GetGameCategoriesQuery
     Response: List<GameCategoryDto>
     Auth: None

GET  /api/v1/shared-games/mechanics
     → GetGameMechanicsQuery
     Response: List<GameMechanicDto>
     Auth: None
```

### Admin/Editor Endpoints (Protected)

```
POST   /api/v1/admin/shared-games
       → CreateSharedGameCommand
       Body: CreateSharedGameDto
       Response: { gameId: Guid }
       Auth: Admin or Editor

PUT    /api/v1/admin/shared-games/{id}
       → UpdateSharedGameCommand
       Body: UpdateSharedGameDto
       Response: 204 No Content
       Auth: Admin or Editor

POST   /api/v1/admin/shared-games/{id}/publish
       → PublishSharedGameCommand
       Response: 204 No Content
       Auth: Admin or Editor

POST   /api/v1/admin/shared-games/{id}/archive
       → ArchiveSharedGameCommand
       Response: 204 No Content
       Auth: Admin only

DELETE /api/v1/admin/shared-games/{id}
       → DeleteSharedGameCommand (Admin) | RequestDeleteSharedGameCommand (Editor)
       Body: { reason: string } (if Editor)
       Response: 204 No Content (Admin) | { requestId: Guid } (Editor)
       Auth: Admin or Editor (different behavior)

POST   /api/v1/admin/shared-games/import-bgg
       → ImportGameFromBggCommand
       Body: { bggId: int }
       Response: { gameId: Guid }
       Auth: Admin or Editor

POST   /api/v1/admin/shared-games/bulk-import
       → BulkImportGamesCommand
       Body: { games: BulkGameImportDto[] }
       Response: BulkImportResultDto
       Auth: Admin only

POST   /api/v1/admin/shared-games/{id}/faq
       → AddGameFaqCommand
       Body: { question, answer, order }
       Response: { faqId: Guid }
       Auth: Admin or Editor

PUT    /api/v1/admin/shared-games/{id}/faq/{faqId}
       → UpdateGameFaqCommand
       Auth: Admin or Editor

DELETE /api/v1/admin/shared-games/{id}/faq/{faqId}
       → DeleteGameFaqCommand
       Auth: Admin or Editor

POST   /api/v1/admin/shared-games/{id}/errata
       → AddGameErrataCommand
       Body: { description, pageReference, publishedDate }
       Response: { errataId: Guid }
       Auth: Admin or Editor

PUT    /api/v1/admin/shared-games/{id}/errata/{errataId}
       → UpdateGameErrataCommand
       Auth: Admin or Editor

DELETE /api/v1/admin/shared-games/{id}/errata/{errataId}
       → DeleteGameErrataCommand
       Auth: Admin or Editor

GET    /api/v1/admin/shared-games/pending-deletes
       → GetPendingDeleteRequestsQuery
       Query Params: pageNumber, pageSize
       Response: PagedResult<DeleteRequestDto>
       Auth: Admin only

POST   /api/v1/admin/shared-games/approve-delete/{requestId}
       → ApproveDeleteRequestCommand
       Response: 204 No Content
       Auth: Admin only

POST   /api/v1/admin/shared-games/reject-delete/{requestId}
       → RejectDeleteRequestCommand
       Body: { reason: string }
       Response: 204 No Content
       Auth: Admin only

GET    /api/v1/admin/shared-games/{id}/audit-log
       → GetGameAuditLogQuery
       Query Params: pageNumber, pageSize
       Response: PagedResult<AuditLogEntryDto>
       Auth: Admin only
```

---

## 💾 Database Schema

### Tables

#### SharedGames
```sql
CREATE TABLE SharedGames (
    Id UUID PRIMARY KEY,
    BggId INT UNIQUE NULL,
    Title VARCHAR(500) NOT NULL,
    YearPublished INT NOT NULL,
    Description TEXT NOT NULL,
    MinPlayers INT NOT NULL,
    MaxPlayers INT NOT NULL,
    PlayingTimeMinutes INT NOT NULL,
    MinAge INT NOT NULL,
    ComplexityRating DECIMAL(3,2) NULL,
    AverageRating DECIMAL(4,2) NULL,
    ImageUrl VARCHAR(1000) NOT NULL,
    ThumbnailUrl VARCHAR(1000) NOT NULL,
    Status INT NOT NULL DEFAULT 0,  -- 0=Draft, 1=Published, 2=Archived
    RulesContent TEXT NULL,
    RulesLanguage VARCHAR(10) NULL,
    SearchVector TSVECTOR NULL,
    CreatedBy UUID NOT NULL,
    ModifiedBy UUID NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    ModifiedAt TIMESTAMP NULL,
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT CHK_SharedGames_YearPublished CHECK (YearPublished > 1900 AND YearPublished <= 2100),
    CONSTRAINT CHK_SharedGames_Players CHECK (MinPlayers > 0 AND MaxPlayers >= MinPlayers),
    CONSTRAINT CHK_SharedGames_PlayingTime CHECK (PlayingTimeMinutes > 0),
    CONSTRAINT CHK_SharedGames_MinAge CHECK (MinAge >= 0),
    CONSTRAINT CHK_SharedGames_Complexity CHECK (ComplexityRating IS NULL OR (ComplexityRating >= 1.0 AND ComplexityRating <= 5.0)),
    CONSTRAINT CHK_SharedGames_Rating CHECK (AverageRating IS NULL OR (AverageRating >= 1.0 AND AverageRating <= 10.0))
);

CREATE INDEX IX_SharedGames_BggId ON SharedGames(BggId) WHERE BggId IS NOT NULL;
CREATE INDEX IX_SharedGames_Status ON SharedGames(Status) WHERE IsDeleted = FALSE;
CREATE INDEX IX_SharedGames_SearchVector ON SharedGames USING GIN(SearchVector);
CREATE INDEX IX_SharedGames_Title ON SharedGames(Title) WHERE IsDeleted = FALSE;

-- Trigger per aggiornare SearchVector automaticamente
CREATE OR REPLACE FUNCTION update_shared_games_search_vector()
RETURNS TRIGGER AS $$
BEGIN
    NEW.SearchVector := to_tsvector('italian', COALESCE(NEW.Title, '') || ' ' || COALESCE(NEW.Description, ''));
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_search_vector
BEFORE INSERT OR UPDATE ON SharedGames
FOR EACH ROW
EXECUTE FUNCTION update_shared_games_search_vector();
```

#### GameDesigners
```sql
CREATE TABLE GameDesigners (
    Id UUID PRIMARY KEY,
    Name VARCHAR(200) NOT NULL UNIQUE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_GameDesigners_Name ON GameDesigners(Name);
```

#### GamePublishers
```sql
CREATE TABLE GamePublishers (
    Id UUID PRIMARY KEY,
    Name VARCHAR(200) NOT NULL UNIQUE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_GamePublishers_Name ON GamePublishers(Name);
```

#### GameCategories
```sql
CREATE TABLE GameCategories (
    Id UUID PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    Slug VARCHAR(100) NOT NULL UNIQUE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);
```

#### GameMechanics
```sql
CREATE TABLE GameMechanics (
    Id UUID PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    Slug VARCHAR(100) NOT NULL UNIQUE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);
```

#### Junction Tables (Many-to-Many)
```sql
CREATE TABLE SharedGameDesigners (
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    GameDesignerId UUID NOT NULL REFERENCES GameDesigners(Id) ON DELETE CASCADE,
    PRIMARY KEY (SharedGameId, GameDesignerId)
);

CREATE TABLE SharedGamePublishers (
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    GamePublisherId UUID NOT NULL REFERENCES GamePublishers(Id) ON DELETE CASCADE,
    PRIMARY KEY (SharedGameId, GamePublisherId)
);

CREATE TABLE SharedGameCategories (
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    GameCategoryId UUID NOT NULL REFERENCES GameCategories(Id) ON DELETE CASCADE,
    PRIMARY KEY (SharedGameId, GameCategoryId)
);

CREATE TABLE SharedGameMechanics (
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    GameMechanicId UUID NOT NULL REFERENCES GameMechanics(Id) ON DELETE CASCADE,
    PRIMARY KEY (SharedGameId, GameMechanicId)
);
```

#### GameFaqs
```sql
CREATE TABLE GameFaqs (
    Id UUID PRIMARY KEY,
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    Question VARCHAR(500) NOT NULL,
    Answer TEXT NOT NULL,
    "Order" INT NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_GameFaqs_SharedGameId ON GameFaqs(SharedGameId);
CREATE INDEX IX_GameFaqs_Order ON GameFaqs(SharedGameId, "Order");
```

#### GameErrata
```sql
CREATE TABLE GameErrata (
    Id UUID PRIMARY KEY,
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    Description TEXT NOT NULL,
    PageReference VARCHAR(100) NULL,
    PublishedDate DATE NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_GameErrata_SharedGameId ON GameErrata(SharedGameId);
CREATE INDEX IX_GameErrata_PublishedDate ON GameErrata(PublishedDate DESC);
```

#### SharedGameDeleteRequests
```sql
CREATE TABLE SharedGameDeleteRequests (
    Id UUID PRIMARY KEY,
    SharedGameId UUID NOT NULL REFERENCES SharedGames(Id) ON DELETE CASCADE,
    RequestedBy UUID NOT NULL,
    Reason TEXT NOT NULL,
    Status INT NOT NULL DEFAULT 0,  -- 0=Pending, 1=Approved, 2=Rejected
    ReviewedBy UUID NULL,
    ReviewComment TEXT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    ReviewedAt TIMESTAMP NULL
);

CREATE INDEX IX_DeleteRequests_Status ON SharedGameDeleteRequests(Status);
CREATE INDEX IX_DeleteRequests_SharedGameId ON SharedGameDeleteRequests(SharedGameId);
```

### Migration to Existing Schema

#### Alter Games Table (GameManagement Context)
```sql
ALTER TABLE Games
ADD COLUMN SharedGameId UUID NULL
REFERENCES SharedGames(Id) ON DELETE SET NULL;

CREATE INDEX IX_Games_SharedGameId ON Games(SharedGameId) WHERE SharedGameId IS NOT NULL;
```

**Rationale**: FK nullable per backward compatibility. Giochi personali possono esistere senza link a SharedGame (legacy) o linkare se provengono dal catalogo condiviso.

---

## 🎨 Frontend UI Structure

### Pages & Routes

#### Admin Section: `/admin/games-catalog`

**1. Games List Page** - `/admin/games-catalog`
```tsx
// Components:
- GamesCatalogTable (sortable, filterable)
  - Columns: Title, Year, Status, Categories, Actions
  - Filters: Status, Category, Mechanic, Search
  - Actions: Edit, Publish, Archive, Delete (with role check)
- Bulk Actions Toolbar (multi-select)
  - Publish Selected, Archive Selected
- Pagination Controls
```

**2. Create Game Page** - `/admin/games-catalog/new`
```tsx
// Form Sections:
- Basic Info (Title, Year, Description)
- Gameplay (Players, Time, Age, Complexity, Rating)
- Taxonomy (Categories multi-select, Mechanics multi-select)
- People (Designers multi-select, Publishers multi-select)
- Media (Image upload with preview + crop, Thumbnail auto-generate)
- Rules (Rich text editor - TipTap)
- BGG Integration (optional BggId input)
- Actions: Save as Draft, Publish Now
```

**3. Edit Game Page** - `/admin/games-catalog/{id}/edit`
```tsx
// Same form as Create + additional tabs:
- FAQ Management Tab
  - FAQ List with CRUD
  - Reorder FAQs (drag-drop)
- Errata Management Tab
  - Errata List with CRUD
  - Sort by Published Date
- Audit Log Tab (Admin only)
  - Change History Timeline
```

**4. Bulk Import Page** - `/admin/games-catalog/import`
```tsx
// Components:
- CSV Upload Section
  - File drop zone
  - CSV template download link
  - Preview table before import
- BGG List Import Section
  - Textarea for BGG XML list or IDs (comma-separated)
  - Fetch metadata from BGG
  - Preview games table
- Import Configuration
  - Status for new games (Draft/Published)
  - Conflict resolution (Skip/Overwrite)
- Import Progress Modal
  - Progress bar
  - Success/Error log
```

**5. Pending Deletes Page** - `/admin/games-catalog/pending-deletes` (Admin only)
```tsx
// Components:
- Delete Requests Table
  - Columns: Game Title, Requested By, Reason, Date
  - Actions: Approve, Reject (with comment modal)
- Filters: Date Range, Requester
```

### User-Facing Search

**Search Component** - Integrated in existing game search UI
```tsx
// Flow:
1. User types "Catan" in search input
2. Frontend calls: GET /api/v1/shared-games?search=Catan
3. Display results from SharedGameCatalog (instant)
4. If no results:
   - Show message: "No games found in our catalog"
   - Button: "Search on BoardGameGeek" → triggers BGG API call
5. User selects game from BGG results
6. Save to personal Games list (POST /api/v1/games with BggId, optionally SharedGameId if match found)
```

**Game Detail Modal** - When user clicks on game
```tsx
// Sections:
- Header: Title, Year, Image
- Overview: Description, Players, Time, Age, Complexity, Rating
- Taxonomy: Categories badges, Mechanics badges
- People: Designers, Publishers
- Rules: Expandable rich text
- FAQ: Accordion with Q&A
- Errata: List with page references and dates
- Actions: "Add to My Collection" button
```

### Component Library
- **Shadcn/ui** (already in use): Button, Input, Select, Dialog, Table, Tabs
- **Radix Primitives**: Accordion, DropdownMenu, RadioGroup
- **React Hook Form** + **Zod**: Form validation
- **TipTap**: Rich text editor for Rules/Description
- **React Dropzone**: Image upload
- **React Easy Crop**: Image cropping
- **React DnD**: FAQ/Errata reordering

---

## ⚡ Performance Optimizations

### 1. Caching Strategy (HybridCache)

```csharp
// L1: In-Memory Cache (IMemoryCache)
// L2: Distributed Cache (Redis via IDistributedCache)

// GetSharedGameByIdQuery
services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        LocalCacheExpiration = TimeSpan.FromMinutes(30),  // L1
        Expiration = TimeSpan.FromHours(2)  // L2
    };
});

// In Handler:
var game = await _cache.GetOrCreateAsync(
    $"shared-game:{gameId}",
    async cancellationToken => await _repository.GetByIdAsync(gameId, cancellationToken)
);

// Cache Invalidation on SharedGameUpdated event:
await _cache.RemoveAsync($"shared-game:{gameId}");
```

```csharp
// SearchSharedGamesQuery
var cacheKey = $"search:{searchTerm}:{categoryIds}:{mechanicIds}:{minPlayers}:{pageNumber}";
var results = await _cache.GetOrCreateAsync(
    cacheKey,
    async cancellationToken => await ExecuteSearchAsync(query, cancellationToken),
    new HybridCacheEntryOptions
    {
        LocalCacheExpiration = TimeSpan.FromMinutes(15),  // L1
        Expiration = TimeSpan.FromHours(1)  // L2
    }
);
```

### 2. Full-Text Search (PostgreSQL FTS)

```sql
-- Query ottimizzata con ranking
SELECT sg.*,
       ts_rank(sg.SearchVector, plainto_tsquery('italian', @searchTerm)) AS rank
FROM SharedGames sg
WHERE sg.IsDeleted = FALSE
  AND sg.Status = 1  -- Published
  AND sg.SearchVector @@ plainto_tsquery('italian', @searchTerm)
ORDER BY rank DESC, sg.Title
LIMIT @pageSize OFFSET @offset;
```

```csharp
// In Repository/Infrastructure:
public async Task<PagedResult<SharedGame>> SearchAsync(
    string searchTerm,
    /* filters */,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken)
{
    var query = _context.SharedGames
        .AsNoTracking()
        .Where(g => !g.IsDeleted && g.Status == GameStatus.Published);

    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        // PostgreSQL FTS con ranking
        query = query.Where(g =>
            EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
            .Matches(EF.Functions.PlainToTsQuery("italian", searchTerm))
        )
        .OrderByDescending(g =>
            EF.Functions.TsRank(g.SearchVector, EF.Functions.PlainToTsQuery("italian", searchTerm))
        );
    }
    else
    {
        query = query.OrderBy(g => g.Title);
    }

    // Apply other filters (Categories, Mechanics, Players, etc.)
    // ...

    return await query.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
}
```

### 3. Image Handling

**Storage Options**:

**Option A: Filesystem** (recommended per start)
```
infra/storage/game-images/
  ├── originals/      (full resolution)
  └── thumbnails/     (300x300 auto-generated)

Backend:
- Upload endpoint: POST /api/v1/admin/shared-games/upload-image
- Returns: { imageUrl, thumbnailUrl }
- Process: Save original → Generate thumbnail (SkiaSharp/ImageSharp) → Return URLs
```

**Option B: Cloud Storage** (future scalability)
```
AWS S3 / Azure Blob Storage + CDN
- Upload to cloud with pre-signed URL
- Automatic thumbnail generation via Lambda/Function
- CDN for fast global delivery
```

### 4. Query Optimizations

```csharp
// Use AsNoTracking() for read-only queries
var games = await _context.SharedGames
    .AsNoTracking()
    .Include(g => g.Categories)
    .Include(g => g.Mechanics)
    .Where(/* filters */)
    .ToListAsync();

// Use AsSplitQuery() for multiple collections to avoid cartesian explosion
var game = await _context.SharedGames
    .AsSplitQuery()
    .Include(g => g.Designers)
    .Include(g => g.Publishers)
    .Include(g => g.Categories)
    .Include(g => g.Mechanics)
    .Include(g => g.Faqs)
    .Include(g => g.Erratas)
    .FirstOrDefaultAsync(g => g.Id == gameId);
```

### 5. Pagination Best Practices

```csharp
// Efficient count query (avoid full table scan)
var totalCount = await query.CountAsync(cancellationToken);

// Use OFFSET/LIMIT con SEEK optimization per grandi dataset (future)
// Per ora: standard Skip/Take sufficiente per centinaia di giochi
var items = await query
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

---

## 🧪 Testing Strategy

### Target Coverage: 90%+

### 1. Unit Tests (Domain + Application)

**Domain Tests** (`SharedGameCatalog.Domain.Tests`):
```csharp
// SharedGame aggregate tests
public class SharedGameTests
{
    [Fact]
    public void Create_ValidData_ReturnsSharedGame() { }

    [Fact]
    public void Publish_WhenDraft_ChangesStatusToPublished() { }

    [Fact]
    public void Publish_WhenAlreadyPublished_ThrowsException() { }

    [Fact]
    public void AddFaq_ValidFaq_AddedToCollection() { }

    [Theory]
    [InlineData(0, 5)]  // Invalid: MinPlayers = 0
    [InlineData(5, 2)]  // Invalid: MaxPlayers < MinPlayers
    public void Create_InvalidPlayers_ThrowsValidationException(int min, int max) { }
}
```

**Application Tests** (`SharedGameCatalog.Application.Tests`):
```csharp
// Command handler tests with mocks
public class CreateSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    [Fact]
    public async Task Handle_ValidCommand_CreatesGameAndPublishesEvent()
    {
        // Arrange
        var command = new CreateSharedGameCommand(/* params */);
        var handler = new CreateSharedGameCommandHandler(_repositoryMock.Object, /* deps */);

        // Act
        var gameId = await handler.Handle(command, CancellationToken.None);

        // Assert
        gameId.Should().NotBeEmpty();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Assert domain event published
    }

    [Fact]
    public async Task Handle_DuplicateBggId_ThrowsException() { }
}

// Query handler tests
public class SearchSharedGamesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithSearchTerm_ReturnsMatchingGames() { }

    [Fact]
    public async Task Handle_WithFilters_AppliesFiltersCorrectly() { }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage() { }
}
```

### 2. Integration Tests (Infrastructure + HTTP)

**Infrastructure Tests** (`SharedGameCatalog.Infrastructure.Tests`):
```csharp
// Uses Testcontainers for PostgreSQL
public class SharedGameRepositoryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres;
    private ApplicationDbContext _context;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();
        // Setup DbContext with test container connection string
    }

    [Fact]
    public async Task AddAsync_ValidGame_SavesToDatabase()
    {
        // Arrange
        var game = SharedGame.Create(/* params */);
        var repository = new SharedGameRepository(_context);

        // Act
        await repository.AddAsync(game, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.SharedGames.FindAsync(game.Id);
        saved.Should().NotBeNull();
        saved.Title.Should().Be(game.Title);
    }

    [Fact]
    public async Task SearchAsync_FullTextSearch_ReturnsMatchingGames()
    {
        // Seed database with test games
        // Execute FTS query
        // Assert results
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

**HTTP Tests** (`Api.Tests` or separate project):
```csharp
// Uses WebApplicationFactory
public class SharedGamesEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    [Fact]
    public async Task GET_SharedGames_ReturnsPublishedGames()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SharedGameDto>>();
        result.Items.Should().AllSatisfy(g => g.Status.Should().Be(GameStatus.Published));
    }

    [Fact]
    public async Task POST_AdminCreateGame_WithEditorRole_Succeeds()
    {
        // Arrange: Auth with Editor role
        var command = new CreateSharedGameDto(/* params */);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/shared-games", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DELETE_AdminDeleteGame_WithEditorRole_CreatesDeleteRequest()
    {
        // Editor role should create delete request, not delete directly
    }

    [Fact]
    public async Task DELETE_AdminDeleteGame_WithAdminRole_DeletesDirectly()
    {
        // Admin role should delete immediately
    }
}
```

### 3. E2E Tests (Playwright)

```typescript
// tests/e2e/admin-games-catalog.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Admin Games Catalog', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/admin/login');
    await page.fill('[name="email"]', 'admin@meeple.ai');
    await page.fill('[name="password"]', 'password');
    await page.click('button[type="submit"]');
  });

  test('should create new game', async ({ page }) => {
    await page.goto('/admin/games-catalog/new');

    await page.fill('[name="title"]', 'Test Game');
    await page.fill('[name="yearPublished"]', '2024');
    await page.fill('[name="description"]', 'Test description');
    // Fill other fields...

    await page.click('button:has-text("Save as Draft")');

    await expect(page).toHaveURL(/\/admin\/games-catalog\/[^\/]+\/edit/);
    await expect(page.locator('text=Game created successfully')).toBeVisible();
  });

  test('should search games with filters', async ({ page }) => {
    await page.goto('/admin/games-catalog');

    await page.fill('input[placeholder="Search games..."]', 'Catan');
    await page.selectOption('select[name="category"]', 'Strategy');

    await expect(page.locator('table tbody tr')).toHaveCount(1);
    await expect(page.locator('table tbody tr:first-child')).toContainText('Catan');
  });

  test('should approve delete request as admin', async ({ page }) => {
    await page.goto('/admin/games-catalog/pending-deletes');

    await expect(page.locator('table tbody tr')).toHaveCount(1);

    await page.click('button:has-text("Approve")');
    await page.fill('textarea[name="comment"]', 'Approved for deletion');
    await page.click('button:has-text("Confirm")');

    await expect(page.locator('text=Delete request approved')).toBeVisible();
    await expect(page.locator('table tbody tr')).toHaveCount(0);
  });
});

// tests/e2e/user-game-search.spec.ts
test.describe('User Game Search', () => {
  test('should search local catalog first', async ({ page }) => {
    await page.goto('/');

    await page.fill('input[placeholder="Search games..."]', 'Catan');

    // Should show results from SharedGameCatalog
    await expect(page.locator('.game-result')).toHaveCount(1);
    await expect(page.locator('.game-result:first-child h3')).toContainText('Catan');
  });

  test('should fallback to BGG when no local results', async ({ page }) => {
    await page.goto('/');

    await page.fill('input[placeholder="Search games..."]', 'Nonexistent Game XYZ');

    // Should show no results and BGG button
    await expect(page.locator('text=No games found')).toBeVisible();
    await expect(page.locator('button:has-text("Search on BoardGameGeek")')).toBeVisible();
  });
});
```

---

## 📦 Implementation Roadmap

### Phase 1: Backend Foundation (Week 1-2)

**Tasks**:
1. ✅ Create Bounded Context structure: `apps/api/src/Api/BoundedContexts/SharedGameCatalog`
2. ✅ Define Domain entities, value objects, aggregates
3. ✅ Implement Commands/Queries with handlers (CQRS)
4. ✅ Create Infrastructure layer (Repository, EF Core config)
5. ✅ Database migration: create all tables
6. ✅ Seed initial Categories/Mechanics (from BGG taxonomy)
7. ✅ Unit tests for Domain + Application layers (90% coverage)

**Deliverables**:
- Domain model complete
- CQRS handlers implemented
- Database schema migrated
- Unit tests passing

### Phase 2: API Endpoints & Integration (Week 2-3)

**Tasks**:
1. ✅ Create HTTP endpoints in `apps/api/Routing/SharedGameCatalogEndpoints.cs`
2. ✅ Implement authorization (Admin/Editor role checks)
3. ✅ Integrate with BGG API for import functionality
4. ✅ Implement HybridCache for queries
5. ✅ Add full-text search with PostgreSQL FTS
6. ✅ Integration tests with Testcontainers
7. ✅ Update Scalar/OpenAPI documentation

**Deliverables**:
- All API endpoints functional
- BGG import working
- Caching implemented
- Integration tests passing

### Phase 3: Frontend Admin UI (Week 3-4)

**Tasks**:
1. ✅ Create admin pages structure (`apps/web/pages/admin/games-catalog/`)
2. ✅ Implement Games List page with filters
3. ✅ Implement Create/Edit Game form
4. ✅ Implement FAQ/Errata management
5. ✅ Implement Bulk Import UI (CSV + BGG list)
6. ✅ Implement Pending Deletes approval page (Admin only)
7. ✅ Image upload with preview and crop functionality
8. ✅ Rich text editor for Rules (TipTap integration)
9. ✅ Frontend unit tests (Vitest)

**Deliverables**:
- Complete admin UI functional
- Image upload working
- Bulk import operational
- Frontend tests passing

### Phase 4: User-Facing Features (Week 4)

**Tasks**:
1. ✅ Update game search component with SharedGameCatalog integration
2. ✅ Implement "Search BGG" fallback button
3. ✅ Create Game Detail modal with FAQ/Errata
4. ✅ Link Games table with SharedGames (FK migration)
5. ✅ Update existing game flow to optionally link to SharedGame
6. ✅ E2E tests with Playwright

**Deliverables**:
- User search flow complete
- BGG fallback functional
- Game detail view enhanced
- E2E tests passing

### Phase 5: Polish & Optimization (Week 5)

**Tasks**:
1. ✅ Performance tuning (query optimization, index analysis)
2. ✅ Audit log implementation for admin changes
3. ✅ Monitoring and logging setup
4. ✅ Security audit (input validation, authorization)
5. ✅ Documentation: ADR, API docs, README
6. ✅ Load testing (simulate hundreds of games)
7. ✅ Final QA and bug fixes

**Deliverables**:
- Performance optimized
- Audit log complete
- Documentation updated
- Production-ready

---

## 🔒 Security Considerations

### Authorization Matrix

| Endpoint | Public | User | Editor | Admin |
|----------|--------|------|--------|-------|
| GET /api/v1/shared-games | ✅ (Published only) | ✅ | ✅ | ✅ (all statuses) |
| GET /api/v1/shared-games/{id} | ✅ (Published) | ✅ | ✅ | ✅ (all) |
| POST /api/v1/admin/shared-games | ❌ | ❌ | ✅ | ✅ |
| PUT /api/v1/admin/shared-games/{id} | ❌ | ❌ | ✅ | ✅ |
| DELETE /api/v1/admin/shared-games/{id} | ❌ | ❌ | ✅ (request) | ✅ (direct) |
| POST /api/v1/admin/shared-games/bulk-import | ❌ | ❌ | ❌ | ✅ |
| GET /api/v1/admin/shared-games/pending-deletes | ❌ | ❌ | ❌ | ✅ |
| POST /api/v1/admin/shared-games/approve-delete/{id} | ❌ | ❌ | ❌ | ✅ |

### Input Validation

```csharp
// FluentValidation per Commands
public class CreateSharedGameCommandValidator : AbstractValidator<CreateSharedGameCommand>
{
    public CreateSharedGameCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(500).WithMessage("Title too long");

        RuleFor(x => x.YearPublished)
            .GreaterThan(1900).WithMessage("Invalid year")
            .LessThanOrEqualTo(DateTime.UtcNow.Year + 1);

        RuleFor(x => x.MinPlayers)
            .GreaterThan(0).WithMessage("Min players must be > 0");

        RuleFor(x => x.MaxPlayers)
            .GreaterThanOrEqualTo(x => x.MinPlayers)
            .WithMessage("Max players must be >= Min players");

        RuleFor(x => x.ImageUrl)
            .Must(BeValidUrl).WithMessage("Invalid image URL");

        // ... other validations
    }

    private bool BeValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out _);
}
```

### SQL Injection Prevention
- ✅ EF Core parameterized queries (automatic)
- ✅ PostgreSQL FTS with `plainto_tsquery()` (sanitizes input)

### XSS Prevention
- ✅ Frontend sanitizes rich text content (TipTap with DOMPurify)
- ✅ Backend validates HTML input (AntiXSS library or strip tags)

### CSRF Protection
- ✅ Already implemented in existing API (cookie + token)

### Rate Limiting
```csharp
// Per admin endpoints:
services.AddRateLimiter(options =>
{
    options.AddPolicy("admin-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Apply to endpoints:
app.MapPost("/api/v1/admin/shared-games", /* handler */)
   .RequireRateLimiting("admin-write");
```

---

## 📊 Monitoring & Observability

### Metrics to Track
- **Search Performance**: P50, P95, P99 latency for SearchSharedGamesQuery
- **Cache Hit Rate**: L1 and L2 cache effectiveness
- **API Usage**: Request count per endpoint, error rate
- **Database Performance**: Slow query log, index usage stats
- **BGG Import**: Success/failure rate, import duration

### Logging
```csharp
// Structured logging with Serilog
_logger.LogInformation(
    "SharedGame created: {GameId}, {Title}, CreatedBy: {UserId}",
    game.Id, game.Title, userId
);

_logger.LogWarning(
    "Search query exceeded timeout: {SearchTerm}, Duration: {Duration}ms",
    searchTerm, duration
);

_logger.LogError(
    exception,
    "BGG import failed for BggId: {BggId}",
    bggId
);
```

### Health Checks
```csharp
// Add health check for SharedGameCatalog
builder.Services.AddHealthChecks()
    .AddCheck("shared-games-database", () =>
    {
        // Check if can query SharedGames table
        var canConnect = /* test query */;
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to SharedGames");
    })
    .AddCheck("full-text-search", () =>
    {
        // Test FTS functionality
    });
```

---

## 🚀 Deployment Considerations

### Docker Compose Updates
```yaml
# No changes needed to docker-compose.yml
# SharedGameCatalog uses existing postgres, redis services
```

### Environment Variables
```bash
# No new env vars required (uses existing DB connection)
# Optional: Add BGG_API_RATE_LIMIT if implementing rate limiting
```

### Migration Strategy
```bash
# Phase 1: Deploy backend with new tables (non-breaking)
cd apps/api/src/Api
dotnet ef migrations add AddSharedGameCatalog
dotnet ef database update

# Phase 2: Seed initial data (Categories/Mechanics)
# Run seed script or manually via SQL

# Phase 3: Deploy frontend with new admin UI
cd apps/web
pnpm build && pnpm start

# Phase 4: Alter Games table (add FK, nullable, non-breaking)
dotnet ef migrations add AddSharedGameIdToGames
dotnet ef database update
```

### Rollback Plan
- Database migrations are reversible (EF Core `dotnet ef database update <PreviousMigration>`)
- FK nullable ensures no data loss on rollback
- Frontend can be rolled back independently (admin UI hidden)

---

## 📝 Documentation Updates

### ADR to Create
- **ADR-010**: SharedGameCatalog Bounded Context
  - Decision: New context vs extending GameManagement
  - Rationale: Separation of concerns, scalability, ownership clarity

- **ADR-011**: PostgreSQL Full-Text Search for Game Catalog
  - Decision: PostgreSQL FTS vs Qdrant vs Elasticsearch
  - Rationale: Scale (hundreds), PG FTS sufficient, avoid complexity

- **ADR-012**: Hybrid Workflow for Editor Delete Requests
  - Decision: Editor requests approval, Admin deletes directly
  - Rationale: Balance autonomy and control, audit trail

### README Updates
- Add SharedGameCatalog to Bounded Contexts section
- Update API endpoints documentation
- Add admin UI routes to navigation guide

### API Documentation (Scalar)
- All new endpoints auto-documented via XML comments
- Generate example requests/responses

---

## 🎯 Success Metrics

### Performance
- ✅ Search latency P95 < 200ms (vs BGG API ~2000ms)
- ✅ Cache hit rate > 80% for popular searches
- ✅ Database query P95 < 100ms

### Quality
- ✅ 90%+ test coverage (unit + integration)
- ✅ Zero critical security vulnerabilities
- ✅ <5% error rate on admin endpoints

### User Experience
- ✅ Search results instant (<300ms perceived)
- ✅ >95% games found in local catalog (after initial seed)
- ✅ BGG fallback works seamlessly for edge cases

### Governance
- ✅ 100% games curated by Admin/Editor (no user-generated)
- ✅ Complete audit log for all admin changes
- ✅ Delete approval workflow functional for Editor role

---

## 🔄 Future Enhancements

### Short-Term (Post-MVP)
1. **Advanced Search**: Filters for designer, publisher, rating range
2. **Game Variants**: Support for expansions linked to base games
3. **Multilingual Support**: Rules/descriptions in multiple languages
4. **User Ratings**: Allow users to rate games, aggregate scores
5. **Recommendation Engine**: "Similar games" based on mechanics/categories

### Medium-Term
1. **Cloud Storage**: Migrate images to AWS S3/Azure Blob with CDN
2. **Elasticsearch**: Replace PG FTS for advanced search (if needed at scale)
3. **Batch Sync**: Background job to sync top N games from BGG periodically
4. **Game Comparison**: Side-by-side comparison tool for multiple games
5. **Export/Import**: Bulk export to JSON/XML for backup/migration

### Long-Term
1. **Public API**: Expose SharedGameCatalog as public API for partners
2. **Community Contributions**: Allow verified users to suggest games
3. **AI-Powered Recommendations**: ML-based personalization
4. **Mobile App**: Native iOS/Android with offline catalog

---

## ✅ Next Steps for Implementation

**Immediate Actions**:
1. **Review & Approval**: Validate this spec with stakeholders
2. **Create GitHub Issue**: Track implementation with milestones
3. **Setup Branch**: `feature/shared-game-catalog` from `main`
4. **Start Phase 1**: Domain model and database schema
5. **Daily Standups**: Track progress, blockers, adjustments

**Questions to Resolve**:
1. ✅ Image storage: Filesystem or cloud? (Decision: Start filesystem)
2. ✅ Soft delete vs hard delete for games? (Decision: Soft delete with `IsDeleted` flag)
3. ✅ BGG API rate limits: Need caching strategy? (Decision: Manual import, no auto-sync)
4. ✅ Audit log: Separate table or DomainEvents? (Decision: DomainEvents table reused)
5. ✅ Seed data: How many initial games? (Decision: Start empty, admin seeds manually)

**Ready to Start?** Let me know if you want to dive into Phase 1 implementation! 🚀
