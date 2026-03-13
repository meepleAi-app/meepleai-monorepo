# Game Night API Contracts — Sprint 2

**Issue**: #211
**Status**: Draft — Pending backend team review
**Date**: 2026-03-13

## Overview

JSON request/response schemas for Sprint 2 Game Night endpoints. All endpoints follow CQRS: `GET` → Query handlers, `POST/PATCH/DELETE` → Command handlers via MediatR.

## Endpoints

### 1. `GET /api/v1/game-nights`

Paginated list of game nights for the authenticated user.

**Query Parameters**:

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number (1-based) |
| `pageSize` | int | 20 | Items per page (max 100) |
| `status` | string? | — | Filter: `planned`, `active`, `completed`, `cancelled` |
| `from` | datetime? | — | Start date filter (ISO 8601) |
| `to` | datetime? | — | End date filter (ISO 8601) |

**Response** `200 OK`:
```json
{
  "items": [
    {
      "id": "uuid",
      "title": "Friday Catan Night",
      "status": "planned",
      "scheduledAt": "2026-03-14T19:00:00Z",
      "hostId": "uuid",
      "hostDisplayName": "Marco",
      "playerCount": 4,
      "maxPlayers": 6,
      "games": [
        { "sharedGameId": "uuid", "title": "Catan", "thumbnailUrl": "https://..." }
      ],
      "createdAt": "2026-03-10T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3
}
```

### 2. `POST /api/v1/game-nights`

Create a new game night.

**Request**:
```json
{
  "title": "Friday Catan Night",
  "scheduledAt": "2026-03-14T19:00:00Z",
  "maxPlayers": 6,
  "sharedGameIds": ["uuid"],
  "notes": "Bring snacks!"
}
```

**Validation**:

| Field | Rule |
|-------|------|
| `title` | Required, 1-200 chars |
| `scheduledAt` | Required, must be in the future |
| `maxPlayers` | Required, 2-20 |
| `sharedGameIds` | Required, 1-10 valid game IDs |
| `notes` | Optional, max 2000 chars |

**Response** `201 Created`:
```json
{
  "id": "uuid",
  "title": "Friday Catan Night",
  "status": "planned",
  "scheduledAt": "2026-03-14T19:00:00Z",
  "createdAt": "2026-03-13T15:00:00Z"
}
```

### 3. `GET /api/v1/game-nights/{id}`

Full detail with players, games, and timeline.

**Response** `200 OK`:
```json
{
  "id": "uuid",
  "title": "Friday Catan Night",
  "status": "active",
  "scheduledAt": "2026-03-14T19:00:00Z",
  "startedAt": "2026-03-14T19:05:00Z",
  "endedAt": null,
  "host": {
    "userId": "uuid",
    "displayName": "Marco",
    "avatarUrl": "https://..."
  },
  "players": [
    {
      "userId": "uuid",
      "displayName": "Luca",
      "avatarUrl": "https://...",
      "joinedAt": "2026-03-14T19:02:00Z",
      "role": "player"
    }
  ],
  "games": [
    {
      "sharedGameId": "uuid",
      "title": "Catan",
      "imageUrl": "https://...",
      "minPlayers": 3,
      "maxPlayers": 4
    }
  ],
  "timeline": [
    {
      "eventId": "uuid",
      "type": "player_joined",
      "data": { "userId": "uuid", "displayName": "Luca" },
      "timestamp": "2026-03-14T19:02:00Z"
    }
  ],
  "maxPlayers": 6,
  "notes": "Bring snacks!",
  "createdAt": "2026-03-13T15:00:00Z",
  "updatedAt": "2026-03-14T19:05:00Z"
}
```

### 4. `PATCH /api/v1/game-nights/{id}`

Partial update (only provided fields are changed).

**Request** (all fields optional):
```json
{
  "title": "Updated Title",
  "scheduledAt": "2026-03-15T20:00:00Z",
  "maxPlayers": 8,
  "notes": "New notes",
  "status": "active"
}
```

**Status transitions**:

| From | To | Action |
|------|----|--------|
| `planned` | `active` | Starts session, sets `startedAt` |
| `planned` | `cancelled` | Cancels, notifies players |
| `active` | `completed` | Ends session, sets `endedAt` |

**Response** `200 OK`: Returns updated game night (same schema as GET detail).

### 5. `DELETE /api/v1/game-nights/{id}`

Soft delete. Only the host can delete. Cannot delete `active` sessions.

**Response** `204 No Content`

## Error Responses

All errors follow a consistent format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Game night with ID 'uuid' was not found."
}
```

| Status | Exception | When |
|--------|-----------|------|
| `404` | `NotFoundException` | Game night ID not found |
| `422` | `ValidationException` | Invalid input (details in `errors` array) |
| `409` | `ConflictException` | Invalid status transition, duplicate title |
| `403` | `ForbiddenException` | Non-host tries to modify/delete |

## TypeScript Types

```typescript
// Inferred from API contracts — use as base for frontend schemas
interface GameNightSummary {
  id: string;
  title: string;
  status: 'planned' | 'active' | 'completed' | 'cancelled';
  scheduledAt: string;
  hostId: string;
  hostDisplayName: string;
  playerCount: number;
  maxPlayers: number;
  games: { sharedGameId: string; title: string; thumbnailUrl: string }[];
  createdAt: string;
}

interface PaginatedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
```
