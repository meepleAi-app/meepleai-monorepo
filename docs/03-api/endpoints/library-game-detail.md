# Library Game Detail Endpoint

**Issue**: #3511 - Game Detail Page
**Bounded Context**: UserLibrary
**Last Updated**: 2026-02-06

## Endpoint

```
GET /api/v1/library/games/{id}
```

Retrieves comprehensive details for a game in the user's library.

## Authentication

**Required**: Yes (User session via cookie)

## Parameters

### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | `Guid` | Yes | The game ID from user's library |

## Response

### Success (200 OK)

Returns `LibraryGameDetailDto`:

```typescript
interface LibraryGameDetailDto {
  // Game Identity
  gameId: string;
  gameTitle: string;
  gamePublisher: string | null;
  gameYearPublished: number | null;
  gameDescription: string | null;

  // Media
  gameImageUrl: string | null;
  gameThumbnailUrl: string | null;

  // Game Stats
  minPlayers: number | null;
  maxPlayers: number | null;
  playingTimeMinutes: number | null;
  complexityRating: number | null;  // 0-5 scale
  averageRating: number | null;     // BGG rating

  // Taxonomies
  gameCategories: string[];
  gameMechanics: string[];
  gameDesigners: string[];

  // User-Specific Data
  currentState: 'Owned' | 'Wishlist' | 'Nuovo' | 'InPrestito';
  isFavorite: boolean;
  notes: string | null;

  // Play Statistics
  timesPlayed: number;
  lastPlayed: string | null;  // ISO 8601 date
  winRate: number | null;     // 0.0-1.0
  avgDuration: number | null; // minutes

  // Associated Content
  pdfDocuments: PdfDocumentDto[];
  socialLinks: SocialLinkDto[];
}

interface PdfDocumentDto {
  id: string;
  name: string;
  type: 'rulebook' | 'errata' | 'homerule';
  version: string;
  uploadedAt: string;  // ISO 8601
  url: string;
}

interface SocialLinkDto {
  id: string;
  name: string;
  url: string;
  type: 'bgg' | 'official' | 'forum' | 'video' | 'other';
}
```

### Error Responses

#### 401 Unauthorized
```json
{
  "error": "Authentication required"
}
```

#### 404 Not Found
```json
{
  "error": "Game not found in user's library",
  "gameId": "123e4567-e89b-12d3-a456-426614174000"
}
```

#### 500 Internal Server Error
```json
{
  "error": "Internal server error",
  "message": "Failed to retrieve game details"
}
```

## Example Request

```bash
curl -X GET 'https://api.meepleai.com/api/v1/library/games/123e4567-e89b-12d3-a456-426614174000' \
  -H 'Cookie: session=...' \
  -H 'Accept: application/json'
```

## Example Response

```json
{
  "gameId": "123e4567-e89b-12d3-a456-426614174000",
  "gameTitle": "Catan",
  "gamePublisher": "CATAN Studio",
  "gameYearPublished": 1995,
  "gameDescription": "Players assume the roles of settlers...",
  "gameImageUrl": "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/...",
  "gameThumbnailUrl": "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/...",
  "minPlayers": 3,
  "maxPlayers": 4,
  "playingTimeMinutes": 90,
  "complexityRating": 2.32,
  "averageRating": 7.12,
  "gameCategories": ["Negotiation"],
  "gameMechanics": ["Dice Rolling", "Hexagon Grid", "Trading"],
  "gameDesigners": ["Klaus Teuber"],
  "currentState": "Owned",
  "isFavorite": true,
  "notes": "Great with expansions!",
  "timesPlayed": 15,
  "lastPlayed": "2026-02-01T19:30:00Z",
  "winRate": 0.6,
  "avgDuration": 85,
  "pdfDocuments": [
    {
      "id": "pdf-001",
      "name": "Catan Rulebook 5th Edition",
      "type": "rulebook",
      "version": "5.0",
      "uploadedAt": "2026-01-15T10:00:00Z",
      "url": "https://storage.meepleai.com/pdfs/catan-rules-5ed.pdf"
    }
  ],
  "socialLinks": [
    {
      "id": "link-bgg",
      "name": "BoardGameGeek",
      "url": "https://boardgamegeek.com/boardgame/13",
      "type": "bgg"
    }
  ]
}
```

## Implementation

**Handler**: `GetLibraryGameDetailQueryHandler.cs`
**Query**: `GetLibraryGameDetailQuery.cs`
**Route**: `UserLibraryEndpoints.cs`

## Related Endpoints

- `PUT /api/v1/library/games/{id}/state` - Update game state
- `POST /api/v1/library/games/{id}/favorite` - Toggle favorite
- `PUT /api/v1/library/games/{id}/notes` - Update notes
- `POST /api/v1/library/games/{id}/labels` - Add label
- `DELETE /api/v1/library/games/{id}/labels/{labelId}` - Remove label
- `DELETE /api/v1/library/games/{id}` - Remove from library

## Frontend Integration

**Page**: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`
**Hook**: `useLibraryGameDetail(gameId)` from `@/hooks/queries/useLibrary`

### Usage Example

```typescript
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

export default function GameDetailPage() {
  const { gameId } = useParams();
  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);

  if (isLoading) return <LoadingSkeleton />;
  if (error) return <ErrorAlert error={error} />;
  if (!gameDetail) return <NotFoundAlert />;

  return (
    <>
      <GameDetailHero gameDetail={gameDetail} />
      <GameSideCard
        gameId={gameDetail.gameId}
        gameTitle={gameDetail.gameTitle}
        bggId={gameDetail.bggId}
      />
      <UserActionSection gameDetail={gameDetail} />
    </>
  );
}
```

## Notes

- All timestamps returned in ISO 8601 UTC format
- Rating values are nullable (not all games have ratings)
- PDF documents and social links arrays may be empty
- Play statistics (timesPlayed, winRate, etc.) are user-specific aggregations
- The endpoint joins data from SharedGameCatalog (public) and UserLibraryEntry (user-specific)

## Testing

**Unit Tests**: `GetLibraryGameDetailQueryHandlerTests.cs`
**Integration Tests**: Test with real database via Testcontainers
**E2E Tests**: `apps/web/__tests__/e2e/library/game-detail.spec.ts`
