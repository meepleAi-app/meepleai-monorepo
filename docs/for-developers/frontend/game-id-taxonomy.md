# Game ID taxonomy

| Field | Value |
|---|---|
| **Status** | reference |
| **Date** | 2026-05-12 |
| **Author** | WS-B (Issue #1068) Mockup Conformity Nanolith bugfix |
| **Scope** | Frontend ID resolution across game-related routes |

## Overview

The MeepleAI frontend resolves game-related routes against three distinct ID schemas. Confusion between them is the root cause of **Issue #1068**: the unified `/library/[gameId]` route introduced by PR #1037 (IA consolidation) initially served only `SharedGame` lookups, collapsing private-game ownership (e.g. Nanolith dogfood) into a misleading "Gioco non trovato" state.

## ID types

| ID type | Format | Source | Backend table | Endpoint base |
|---|---|---|---|---|
| **BGG ID** | int positive | BoardGameGeek external | (FK only) | `/api/v1/bgg/{id}` |
| **SharedGame.id** | UUID v4 | Community shared catalog | `shared_games` | `/api/v1/shared-games/{id}` |
| **PrivateGame.id** | UUID v4 | User-owned, often dogfood | `private_games` | `/api/v1/private-games/{id}` |
| **UserLibraryEntry.id** | UUID v4 | Join row library ↔ game | `user_library_entries` | (internal) |
| **UserLibraryEntry.gameId** | UUID v4 (FK) | Refers to `SharedGame.id` OR `PrivateGame.id` | (FK) | (internal) |

## Route → ID mapping

| Route | Expected ID type | Hook |
|---|---|---|
| `/library/[gameId]` | `SharedGame.id` OR `PrivateGame.id` | `useLibraryGameDetail(gameId)` — probes three sources |
| `/library/private/[privateGameId]` | `PrivateGame.id` only | `usePrivateGame(privateGameId)` |
| `/shared-games/[id]` (TBD public) | `SharedGame.id` only | `useSharedGame(id)` |

After **PR #1037** IA consolidation collapsed `/library/games/[id]` → `/library/[id]`, the unified path must serve both shared and private games. The `useLibraryGameDetail` hook (Issue #1068) probes three sources in parallel and resolves with priority:

1. **`api.library.getGameDetail(gameId)`** — library entry with shared game join (returns `GameDetailDto`, fastest path for library members with stats + recent sessions).
2. **`api.library.getPrivateGame(gameId)`** — private game ownership (returns `PrivateGameDto`, covers user-owned non-catalog games like Nanolith).
3. **`api.sharedGames.getById(gameId)`** — catalog-only fallback (returns `SharedGame`, powers community browse flow and "Aggiungi alla libreria" CTA).

The mapper `mapPrivateGameToLibraryGameDetail` (in `apps/web/src/hooks/queries/useLibrary.ts`) normalizes `PrivateGameDto` into the same `LibraryGameDetail` shape consumed by the page, using sentinel values for fields the source cannot provide:

- `libraryEntryId === ''` — same sentinel used by catalog fallback, signals `heroVariant === 'community'` downstream
- `currentState === 'Nuovo'` — default state for owned-but-not-yet-classified games
- `timesPlayed === 0` — no play stats available without library entry
- `gamePublisher === null`, `averageRating === null`, etc. — fields not on `PrivateGameDto` schema

## Resolution priority

```
gameDetail > privateGame > sharedGame fallback > null
```

The first non-null source wins. A genuine `not-found` returns `null` only when all three sources return null (URL points to a non-existent UUID across all three tables).

## Failure modes

| Failure mode | Symptom | Fix / Status |
|---|---|---|
| URL contains BGG id instead of UUID | All three sources null → not-found | Resolve BGG id to `SharedGame.id` upstream (e.g. `/api/v1/shared-games/by-bgg/{bggId}` lookup) |
| Private game UUID, hook lacks third probe | Not-found shown for owned game | **Issue #1068 — fixed** by adding `getPrivateGame` probe |
| 5xx network error treated as not-found | Misleading "Gioco non trovato" hides backend outage | **WS-B AC-B.2 deferred** — error vs not-found distinction. `.catch(() => null)` silently swallows all failures; future work re-throws non-404 errors so React Query enters `isError` state |
| Soft-deleted SharedGame still resolved | Stale data returned for deleted catalog entry | Backend `HasQueryFilter(e => !e.IsDeleted)` handles this (see CLAUDE.md "Soft Delete" pattern) |

## References

- **Issue #1068** — WS-B Mockup Conformity Nanolith bugfix
- **PR #1037** — `f3702921d` IA consolidation (`/library/games/[id]` → `/library/[id]`)
- `apps/web/src/hooks/queries/useLibrary.ts` — `useLibraryGameDetail` hook (~line 814) + `mapPrivateGameToLibraryGameDetail` mapper (~line 802)
- `apps/web/src/app/(authenticated)/library/[gameId]/page.tsx` — consumer (collapses `!gameDetail` → not-found at line 76)
- `apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx` — alternate dedicated route for private games
- `apps/web/src/lib/api/schemas/private-games.schemas.ts` — `PrivateGameDto` Zod schema
- Umbrella **#1066** — Mockup Conformity Roadmap
