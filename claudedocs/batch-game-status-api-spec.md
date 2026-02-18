# Batch Game Library Status API

## Overview

Batch endpoint to check library status for multiple games in a single request.
Eliminates N+1 problem when displaying game grids.

## Endpoint

```
GET /api/v1/library/games/batch-status?gameIds=id1,id2,id3
```

## Request

**Query Parameters**:
- `gameIds` (required): Comma-separated list of game GUIDs
- Max: 100 game IDs per request

**Example**:
```
GET /api/v1/library/games/batch-status?gameIds=a365898b-d06f-44f9-97aa-248931ef35aa,324c20dd-4767-4b30-8537-1481cbbfb21b
```

## Response

**Status**: 200 OK

**Body**:
```json
{
  "results": {
    "a365898b-d06f-44f9-97aa-248931ef35aa": {
      "inLibrary": true,
      "isOwned": true,
      "isWishlist": false,
      "addedAt": "2026-02-15T10:30:00Z"
    },
    "324c20dd-4767-4b30-8537-1481cbbfb21b": {
      "inLibrary": false,
      "isOwned": false,
      "isWishlist": false,
      "addedAt": null
    }
  },
  "totalChecked": 2
}
```

## Error Responses

**400 Bad Request**: Invalid game IDs or too many IDs
```json
{
  "error": "Maximum 100 game IDs allowed"
}
```

**401 Unauthorized**: No valid session

## Performance

**Before**: N API calls for N games (e.g., 20 games = 20 calls)
**After**: 1 API call for N games (20 games = 1 call)

**Expected Improvement**: 95% reduction in API calls for game grids

## Backend Implementation

**Bounded Context**: UserLibrary
**Query**: `BatchCheckGamesInLibraryQuery`
**Handler**: `BatchCheckGamesInLibraryQueryHandler`
**Endpoint**: `LibraryEndpoints.MapBatchStatus()`

**SQL Optimization**: Single query with IN clause
```sql
SELECT GameId, IsOwned, IsInWishlist, AddedAt
FROM UserLibraryGames
WHERE UserId = @userId AND GameId IN (@gameIds)
```

## Frontend Integration

**Hook**: `useBatchGameStatus(gameIds: string[])`

**Usage**:
```tsx
// Page level - fetch all statuses once
const gameIds = games.map(g => g.id);
const { data: statuses } = useBatchGameStatus(gameIds);

// Card level - read from batch result
const status = statuses?.[game.id];
```

## Migration Path

1. ✅ Create batch endpoint (backward compatible)
2. ✅ Create batch hook
3. ✅ Update GameGrid to use batch hook
4. ✅ Update MeepleGameCatalogCard to accept status prop (optional)
5. ⏭️ Deprecate individual `useGameInLibraryStatus` (future)

---

**Created**: 2026-02-18
**Issue**: N+1 API calls in game grids
**Impact**: 95% reduction in API traffic
