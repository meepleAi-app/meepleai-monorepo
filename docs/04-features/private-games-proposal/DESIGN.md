# Private Games & Catalog Proposal System

> **Status**: Draft
> **Author**: Claude Code + User Brainstorming Session
> **Date**: 2026-01-28
> **Related Issues**: TBD

## Executive Summary

Questa feature permette agli utenti di aggiungere giochi alla propria libreria anche quando non esistono nel catalogo condiviso (SharedGames), con la possibilità opzionale di proporre il gioco per l'aggiunta al catalogo pubblico.

## Problem Statement

### Situazione Attuale
- `UserLibraryEntry` richiede FK a `SharedGameEntity` (constraint obbligatorio)
- Solo Admin/Editor possono importare giochi da BGG in SharedGames
- Gli utenti normali non hanno accesso alla ricerca BGG
- Se un gioco non è nel catalogo, l'utente non può tracciarlo nella propria libreria

### User Stories Bloccate
1. *"Voglio aggiungere un gioco indie che ho comprato a una fiera"*
2. *"Ho trovato un gioco su BGG ma non è nel vostro catalogo"*
3. *"Ho un prototipo di un amico game designer che voglio tracciare"*

## Solution Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           USER FLOW                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Utente cerca gioco                                                      │
│       │                                                                  │
│       ├──► Trovato in SharedGames ──► Aggiungi a libreria (esistente)   │
│       │                                                                  │
│       └──► NON trovato                                                   │
│              │                                                           │
│              ├──► Cerca su BGG ──► Trovato ──► Crea PrivateGame (BGG)   │
│              │                                                           │
│              └──► Crea manualmente ──► Crea PrivateGame (manual)        │
│                                                                          │
│                         │                                                │
│                         ▼                                                │
│              ┌─────────────────────┐                                     │
│              │   PrivateGame in    │                                     │
│              │   UserLibrary       │                                     │
│              └──────────┬──────────┘                                     │
│                         │                                                │
│                         ├──► Upload PDF ──► Chat AI disponibile         │
│                         │                                                │
│                         └──► "Proponi al catalogo" (opzionale)          │
│                                    │                                     │
│                                    ▼                                     │
│                         ┌─────────────────────┐                          │
│                         │   ShareRequest      │                          │
│                         │   (NewGameProposal) │                          │
│                         └──────────┬──────────┘                          │
│                                    │                                     │
│                                    ▼                                     │
│                         Admin Review & Approve                           │
│                                    │                                     │
│                                    ▼                                     │
│                         Nuovo SharedGame creato                          │
│                         (PrivateGame resta separato)                     │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## Design Decisions

### DD-1: Estensione UserLibraryEntry
**Decisione**: Estendere `UserLibraryEntryEntity` invece di creare nuova tabella.

**Rationale**:
- Minimo impatto sul codice esistente
- Riutilizzo di logica esistente (quota, sessioni, checklist)
- FK a SharedGame diventa nullable

### DD-2: Auto-redirect a SharedGame
**Decisione**: Se l'utente aggiunge un gioco con BggId che esiste già in SharedGames, usa automaticamente SharedGame.

**Rationale**:
- Evita duplicati inutili
- UX fluida senza scelte confuse
- Mantiene integrità dati

### DD-3: Giochi manuali permessi
**Decisione**: Utenti possono creare giochi senza BggId (prototipi, indie, custom).

**Campi obbligatori**: Nome, MinPlayers, MaxPlayers

### DD-4: Riuso ShareRequest
**Decisione**: Estendere `ShareRequest` con nuovo `ContributionType.NewGameProposal`.

**Rationale**:
- Riutilizza workflow di review esistente
- Sistema di notifiche già in place
- Admin dashboard già gestisce ShareRequest

### DD-5: Separazione post-approvazione
**Decisione**: Quando un PrivateGame viene approvato e creato come SharedGame, i due restano separati.

**Rationale**:
- L'utente mantiene i propri dati/note/sessioni
- Può decidere autonomamente se migrare
- Evita side-effect inattesi

## Data Model Changes

### UserLibraryEntryEntity (Extended)

```csharp
public class UserLibraryEntryEntity
{
    // ... existing fields ...

    /// <summary>
    /// Reference to SharedGame (NOW NULLABLE).
    /// Null when using PrivateGame data.
    /// </summary>
    public Guid? SharedGameId { get; set; }  // Changed: was required

    // ============ NEW: Private Game Fields ============

    /// <summary>
    /// Indicates this entry uses private game data instead of SharedGame.
    /// </summary>
    public bool IsPrivateGame { get; set; } = false;

    /// <summary>
    /// BGG ID for private games (optional, for BGG-sourced private games).
    /// </summary>
    public int? PrivateBggId { get; set; }

    /// <summary>
    /// Game title (required for private games).
    /// </summary>
    public string? PrivateGameTitle { get; set; }

    /// <summary>
    /// Year published (optional).
    /// </summary>
    public int? PrivateYearPublished { get; set; }

    /// <summary>
    /// Minimum players (required for private games).
    /// </summary>
    public int? PrivateMinPlayers { get; set; }

    /// <summary>
    /// Maximum players (required for private games).
    /// </summary>
    public int? PrivateMaxPlayers { get; set; }

    /// <summary>
    /// Playing time in minutes (optional).
    /// </summary>
    public int? PrivatePlayingTimeMinutes { get; set; }

    /// <summary>
    /// Game description (optional, from BGG or manual).
    /// </summary>
    public string? PrivateDescription { get; set; }

    /// <summary>
    /// Image URL (optional, from BGG or uploaded).
    /// </summary>
    public string? PrivateImageUrl { get; set; }

    /// <summary>
    /// Thumbnail URL (optional, from BGG).
    /// </summary>
    public string? PrivateThumbnailUrl { get; set; }

    /// <summary>
    /// Complexity rating (optional, from BGG).
    /// </summary>
    public decimal? PrivateComplexityRating { get; set; }

    /// <summary>
    /// Source of private game data.
    /// </summary>
    public PrivateGameSource? PrivateGameSource { get; set; }

    /// <summary>
    /// When BGG data was last synced (for BGG-sourced private games).
    /// </summary>
    public DateTime? PrivateBggSyncedAt { get; set; }

    // Navigation (unchanged)
    public SharedGameEntity? SharedGame { get; set; }  // Now nullable
}

public enum PrivateGameSource
{
    Manual = 0,      // User entered all data manually
    BoardGameGeek = 1 // Data fetched from BGG API
}
```

### ShareRequest Extension

```csharp
public enum ContributionType
{
    NewGame = 0,           // Existing: new game from user library
    AdditionalContent = 1, // Existing: PDF/FAQ for existing game
    NewGameProposal = 2    // NEW: Private game proposed to catalog
}

// ShareRequest entity - add field for private game reference
public class ShareRequest
{
    // ... existing fields ...

    /// <summary>
    /// Reference to UserLibraryEntry when proposing a private game.
    /// Only set for ContributionType.NewGameProposal.
    /// </summary>
    public Guid? SourceLibraryEntryId { get; set; }
}
```

## API Endpoints

### New Endpoints

#### 1. Public BGG Search (User)
```
GET /api/v1/bgg/search?query={query}&exact={bool}
Authorization: Bearer (any authenticated user)
Rate Limit: 10 req/min

Response: BggSearchResultDto[]
```

#### 2. Get BGG Game Details (User)
```
GET /api/v1/bgg/games/{bggId}
Authorization: Bearer (any authenticated user)
Rate Limit: 20 req/min

Response: BggGameDetailsDto
```

#### 3. Add Private Game to Library
```
POST /api/v1/user-library/private-games
Authorization: Bearer
Body: AddPrivateGameRequest

Request:
{
  "bggId": 123456,           // Optional: if provided, fetches from BGG
  "title": "My Custom Game", // Required if no bggId
  "minPlayers": 2,           // Required if no bggId
  "maxPlayers": 4,           // Required if no bggId
  "yearPublished": 2024,     // Optional
  "playingTimeMinutes": 60,  // Optional
  "description": "...",      // Optional
  "imageUrl": "...",         // Optional
  "notes": "My notes",       // Optional (library entry notes)
  "isFavorite": false        // Optional
}

Response: UserLibraryEntryDto (201 Created)
```

#### 4. Propose Private Game to Catalog
```
POST /api/v1/user-library/{entryId}/propose-to-catalog
Authorization: Bearer
Body: ProposeGameRequest

Request:
{
  "notes": "This is a great indie game...", // Optional
  "attachedDocumentIds": ["guid1", "guid2"] // Optional: PDFs to include
}

Response: ShareRequestResponse (201 Created)
```

### Modified Endpoints

#### Add Game to Library (Enhanced)
```
POST /api/v1/user-library
Authorization: Bearer
Body: AddGameToLibraryRequest

Request:
{
  "gameId": "guid",           // SharedGame ID (existing flow)
  // OR
  "bggId": 123456,            // BGG ID for auto-resolve
  // OR
  "privateGame": {            // Manual private game
    "title": "...",
    "minPlayers": 2,
    "maxPlayers": 4
  }
}

Logic:
1. If gameId provided → existing flow (SharedGame)
2. If bggId provided:
   a. Check if SharedGame exists with this BggId → use SharedGame
   b. Else → create PrivateGame with BGG data
3. If privateGame provided → create PrivateGame manually
```

## Application Layer

### Commands

```csharp
// New command for adding private games
internal record AddPrivateGameToLibraryCommand(
    Guid UserId,
    int? BggId,
    string? Title,
    int? MinPlayers,
    int? MaxPlayers,
    int? YearPublished,
    int? PlayingTimeMinutes,
    string? Description,
    string? ImageUrl,
    string? Notes,
    bool IsFavorite
) : ICommand<UserLibraryEntryDto>;

// New command for proposing to catalog
internal record ProposePrivateGameToCatalogCommand(
    Guid UserId,
    Guid LibraryEntryId,
    string? Notes,
    List<Guid>? AttachedDocumentIds
) : ICommand<CreateShareRequestResponse>;

// New query for public BGG search
internal record SearchBggGamesQuery(
    string Query,
    bool Exact = false
) : IQuery<List<BggSearchResultDto>>;
```

### Validation Rules

```csharp
// AddPrivateGameToLibraryCommandValidator
public class AddPrivateGameToLibraryCommandValidator
    : AbstractValidator<AddPrivateGameToLibraryCommand>
{
    public AddPrivateGameToLibraryCommandValidator()
    {
        // Must have either BggId OR manual data
        RuleFor(x => x)
            .Must(x => x.BggId.HasValue || !string.IsNullOrWhiteSpace(x.Title))
            .WithMessage("Either BggId or Title must be provided");

        // If manual (no BggId), require player counts
        When(x => !x.BggId.HasValue, () =>
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.MinPlayers)
                .NotNull()
                .InclusiveBetween(1, 100);

            RuleFor(x => x.MaxPlayers)
                .NotNull()
                .InclusiveBetween(1, 100)
                .GreaterThanOrEqualTo(x => x.MinPlayers ?? 1);
        });

        // Optional fields validation
        RuleFor(x => x.YearPublished)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 5)
            .When(x => x.YearPublished.HasValue);

        RuleFor(x => x.PlayingTimeMinutes)
            .InclusiveBetween(1, 10000)
            .When(x => x.PlayingTimeMinutes.HasValue);
    }
}
```

## Handler Logic

### AddPrivateGameToLibraryCommandHandler

```csharp
public async Task<UserLibraryEntryDto> Handle(
    AddPrivateGameToLibraryCommand command,
    CancellationToken cancellationToken)
{
    // 1. Check quota (private games count toward total)
    await CheckUserQuotaAsync(command.UserId, cancellationToken);

    // 2. If BggId provided, check for existing SharedGame first
    if (command.BggId.HasValue)
    {
        var existingSharedGame = await _sharedGameRepository
            .GetByBggIdAsync(command.BggId.Value, cancellationToken);

        if (existingSharedGame != null)
        {
            // Auto-redirect to SharedGame flow
            _logger.LogInformation(
                "BggId {BggId} exists in SharedGames, using shared version",
                command.BggId.Value);

            return await AddSharedGameToLibraryAsync(
                command.UserId,
                existingSharedGame.Id,
                command.Notes,
                command.IsFavorite,
                cancellationToken);
        }

        // Fetch from BGG and create private game
        var bggDetails = await _bggApiService
            .GetGameDetailsAsync(command.BggId.Value, cancellationToken);

        if (bggDetails == null)
        {
            throw new NotFoundException($"Game with BGG ID {command.BggId} not found");
        }

        return await CreatePrivateGameEntryAsync(
            command.UserId,
            bggDetails,
            command.Notes,
            command.IsFavorite,
            cancellationToken);
    }

    // 3. Manual private game creation
    return await CreateManualPrivateGameEntryAsync(command, cancellationToken);
}

private async Task<UserLibraryEntryDto> CreatePrivateGameEntryAsync(
    Guid userId,
    BggGameDetailsDto bggDetails,
    string? notes,
    bool isFavorite,
    CancellationToken cancellationToken)
{
    var entry = new UserLibraryEntryEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SharedGameId = null,  // No SharedGame link
        IsPrivateGame = true,
        PrivateBggId = bggDetails.BggId,
        PrivateGameTitle = bggDetails.Name,
        PrivateYearPublished = bggDetails.YearPublished,
        PrivateMinPlayers = bggDetails.MinPlayers ?? 1,
        PrivateMaxPlayers = bggDetails.MaxPlayers ?? 4,
        PrivatePlayingTimeMinutes = bggDetails.PlayingTime,
        PrivateDescription = bggDetails.Description,
        PrivateImageUrl = bggDetails.ImageUrl,
        PrivateThumbnailUrl = bggDetails.ThumbnailUrl,
        PrivateComplexityRating = bggDetails.AverageWeight.HasValue
            ? (decimal)bggDetails.AverageWeight.Value
            : null,
        PrivateGameSource = PrivateGameSource.BoardGameGeek,
        PrivateBggSyncedAt = DateTime.UtcNow,
        AddedAt = DateTime.UtcNow,
        Notes = notes,
        IsFavorite = isFavorite
    };

    _dbContext.UserLibraryEntries.Add(entry);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return MapToDto(entry);
}
```

## Database Migration

```csharp
public partial class AddPrivateGameSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Make SharedGameId nullable
        migrationBuilder.AlterColumn<Guid>(
            name: "SharedGameId",
            table: "UserLibraryEntries",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        // 2. Add private game fields
        migrationBuilder.AddColumn<bool>(
            name: "IsPrivateGame",
            table: "UserLibraryEntries",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "PrivateBggId",
            table: "UserLibraryEntries",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrivateGameTitle",
            table: "UserLibraryEntries",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PrivateYearPublished",
            table: "UserLibraryEntries",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PrivateMinPlayers",
            table: "UserLibraryEntries",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PrivateMaxPlayers",
            table: "UserLibraryEntries",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PrivatePlayingTimeMinutes",
            table: "UserLibraryEntries",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrivateDescription",
            table: "UserLibraryEntries",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrivateImageUrl",
            table: "UserLibraryEntries",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrivateThumbnailUrl",
            table: "UserLibraryEntries",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PrivateComplexityRating",
            table: "UserLibraryEntries",
            type: "numeric(3,2)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PrivateGameSource",
            table: "UserLibraryEntries",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PrivateBggSyncedAt",
            table: "UserLibraryEntries",
            type: "timestamp with time zone",
            nullable: true);

        // 3. Add check constraint
        migrationBuilder.Sql(@"
            ALTER TABLE ""UserLibraryEntries""
            ADD CONSTRAINT ""CK_UserLibraryEntry_GameSource""
            CHECK (
                (""IsPrivateGame"" = false AND ""SharedGameId"" IS NOT NULL) OR
                (""IsPrivateGame"" = true AND ""SharedGameId"" IS NULL AND ""PrivateGameTitle"" IS NOT NULL)
            );
        ");

        // 4. Add index for BggId lookups
        migrationBuilder.CreateIndex(
            name: "IX_UserLibraryEntries_PrivateBggId",
            table: "UserLibraryEntries",
            column: "PrivateBggId");

        // 5. Extend ShareRequest for NewGameProposal
        migrationBuilder.AddColumn<Guid>(
            name: "SourceLibraryEntryId",
            table: "ShareRequests",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ShareRequests_SourceLibraryEntryId",
            table: "ShareRequests",
            column: "SourceLibraryEntryId");

        migrationBuilder.AddForeignKey(
            name: "FK_ShareRequests_UserLibraryEntries_SourceLibraryEntryId",
            table: "ShareRequests",
            column: "SourceLibraryEntryId",
            principalTable: "UserLibraryEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse operations...
    }
}
```

## Notification System

### New Notification Types

```csharp
public enum NotificationType
{
    // ... existing ...

    // New for game proposals
    GameProposalSubmitted = 20,      // User: "Your proposal has been submitted"
    GameProposalInReview = 21,       // User: "An admin is reviewing your proposal"
    GameProposalApproved = 22,       // User: "Your game has been added to the catalog!"
    GameProposalRejected = 23,       // User: "Your proposal was not approved"
    GameProposalChangesRequested = 24 // User: "Changes requested for your proposal"
}
```

### Event Handlers

```csharp
// When ShareRequest with NewGameProposal is created
internal class GameProposalCreatedNotificationHandler
    : INotificationHandler<ShareRequestCreatedEvent>
{
    public async Task Handle(
        ShareRequestCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        if (notification.ContributionType != ContributionType.NewGameProposal)
            return;

        await _notificationService.SendAsync(new UserNotification
        {
            UserId = notification.UserId,
            Type = NotificationType.GameProposalSubmitted,
            Title = "Game Proposal Submitted",
            Message = $"Your proposal for '{notification.GameTitle}' has been submitted for review.",
            RelatedEntityId = notification.ShareRequestId,
            RelatedEntityType = "ShareRequest"
        }, cancellationToken);
    }
}
```

## Frontend Integration Points

### New Pages/Components

1. **BGG Search Dialog** (`/components/library/BggSearchDialog.tsx`)
   - Search input with debounce
   - Results list with game cards
   - "Add to Library" button per result

2. **Add Private Game Form** (`/components/library/AddPrivateGameForm.tsx`)
   - Manual entry form
   - Validation for required fields
   - Image upload option

3. **Propose to Catalog Modal** (`/components/library/ProposeGameModal.tsx`)
   - Notes textarea
   - Document attachment selector
   - Submit confirmation

4. **My Proposals Dashboard** (`/app/library/proposals/page.tsx`)
   - List of user's proposals
   - Status tracking
   - Notification center integration

### API Client Extensions

```typescript
// lib/api/userLibrary.ts

export async function searchBgg(query: string, exact = false): Promise<BggSearchResult[]> {
  return api.get(`/bgg/search`, { params: { query, exact } });
}

export async function getBggGameDetails(bggId: number): Promise<BggGameDetails> {
  return api.get(`/bgg/games/${bggId}`);
}

export async function addPrivateGame(data: AddPrivateGameRequest): Promise<UserLibraryEntry> {
  return api.post(`/user-library/private-games`, data);
}

export async function proposeGameToCatalog(
  entryId: string,
  data: ProposeGameRequest
): Promise<ShareRequestResponse> {
  return api.post(`/user-library/${entryId}/propose-to-catalog`, data);
}
```

## Security Considerations

### Rate Limiting

| Endpoint | Rate Limit | Rationale |
|----------|------------|-----------|
| `GET /bgg/search` | 10/min | Prevent BGG API abuse |
| `GET /bgg/games/{id}` | 20/min | Prevent BGG API abuse |
| `POST /user-library/private-games` | 5/min | Prevent spam creation |
| `POST /{id}/propose-to-catalog` | 2/min | Prevent proposal spam |

### Validation

- **BGG ID verification**: Validate BGG IDs exist before creating private games
- **Duplicate prevention**: Check for existing entries with same BggId per user
- **Content validation**: Sanitize user-provided descriptions and titles
- **Image URL validation**: Only allow HTTPS URLs for images

## Testing Strategy

### Unit Tests
- `AddPrivateGameToLibraryCommandHandlerTests`
- `ProposePrivateGameToCatalogCommandHandlerTests`
- `SearchBggGamesQueryHandlerTests`
- Validation tests for all new validators

### Integration Tests
- Private game creation with BGG data
- Private game creation with manual data
- Auto-redirect to SharedGame when BggId exists
- Proposal workflow through ShareRequest
- Notification delivery

### E2E Tests
- Full user journey: Search BGG → Add Private → Upload PDF → Propose → Admin Approve
- Quota enforcement for private games
- Rate limiting verification

## Implementation Phases

### Phase 1: Data Model & Core API (MVP)
- [ ] Database migration for UserLibraryEntry extension
- [ ] AddPrivateGameToLibraryCommand + Handler
- [ ] Public BGG search endpoint
- [ ] Basic validation

### Phase 2: Proposal System
- [ ] Extend ShareRequest for NewGameProposal
- [ ] ProposePrivateGameToCatalogCommand
- [ ] Admin review workflow extension
- [ ] Notification handlers

### Phase 3: Frontend Integration
- [ ] BGG Search Dialog component
- [ ] Add Private Game form
- [ ] Propose to Catalog modal
- [ ] My Proposals dashboard

### Phase 4: Polish & Testing
- [ ] Comprehensive test coverage
- [ ] Rate limiting implementation
- [ ] Documentation updates
- [ ] Performance optimization

## Open Questions

1. **BGG Sync**: Should private games with BggId auto-update when BGG data changes?
2. **Bulk import**: Should users be able to import multiple games from BGG at once?
3. **Game merge**: If a user's private game becomes a SharedGame (by someone else), offer to merge?

## Appendix: DTOs

```csharp
// Response DTO for library entries (updated)
public record UserLibraryEntryDto(
    Guid Id,
    Guid UserId,
    Guid? GameId,              // Nullable now
    bool IsPrivateGame,
    // Game info (from SharedGame OR private fields)
    string GameTitle,
    string? GamePublisher,
    int? GameYearPublished,
    string? GameIconUrl,
    string? GameImageUrl,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    decimal? ComplexityRating,
    PrivateGameSource? PrivateGameSource,
    // Library entry data
    DateTime AddedAt,
    string? Notes,
    bool IsFavorite,
    bool CanProposeToSatalog    // True if private and not already proposed
);

// Request DTOs
public record AddPrivateGameRequest(
    int? BggId,
    string? Title,
    int? MinPlayers,
    int? MaxPlayers,
    int? YearPublished,
    int? PlayingTimeMinutes,
    string? Description,
    string? ImageUrl,
    string? Notes,
    bool IsFavorite = false
);

public record ProposeGameRequest(
    string? Notes,
    List<Guid>? AttachedDocumentIds
);
```
