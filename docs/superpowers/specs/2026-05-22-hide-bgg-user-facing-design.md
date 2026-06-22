# Hide BGG mentions from user-facing UI — Design

**Date**: 2026-05-22
**Author**: Claude (paired with @DegrassiAaron)
**Status**: Approved
**Branch**: `feature/hide-bgg-user-facing`

## Context

Backend BGG integration (`/api/v1/bgg/*`) is licensed for admin-only use. The admin wizard for creating SharedGames in the catalog uses BGG search/import to populate metadata. However, the user-facing UI still references BGG in several places:

- Catalog card subtitle shows `ID: {bggId}` (raw BGG ID exposed to users)
- Public shared-game detail page shows a "View on BGG" link → `boardgamegeek.com/boardgame/{bggId}`
- User settings page lists "BoardGameGeek" as an integration service alongside Google/Discord
- A duplicate `BggSearchPanel` component lives under `components/games/` (orphan; the real one is `components/admin/shared-games/BggSearchPanel.tsx`)

The goal is to **hide every BGG mention from the non-admin UI surface** while preserving the admin-side workflow (admin wizard continues to use BGG to populate the catalog).

## Goals

- Zero BGG/BoardGameGeek mentions visible to non-admin users
- Backend BggEndpoints untouched (still consumed by admin wizard)
- `Game.bggId` field stays in DTOs (still used by admin)
- Admin BggSearchPanel + bggClient + import workflows untouched

## Non-goals

- Restricting `bggId` field visibility at the API layer (would require response-shape gating; out of scope)
- Replacing BGG with another provider
- Cleaning up `BGG-style` rating-scale comments (cosmetic, no UI impact)

## Acceptance criteria

1. `/shared-games/{id}` (public detail) — no "View on BGG" link, no BGG mention
2. Catalog cards — no `ID: <number>` BGG subtitle
3. `/settings/services` (user) — no "BoardGameGeek" integration entry
4. No `BggSearchPanel` exported from `@/components/games`
5. `pnpm typecheck` passes; existing user-facing tests still pass
6. Admin pages (`/admin/(dashboard)/shared-games/*`) still function with BGG search/import

## Files to change

| # | File | Change |
|---|------|--------|
| 1 | `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx` | Remove `if (game.bggId) subtitleParts.push(\`ID: ${game.bggId}\`)` block (lines 194-195) |
| 2 | `apps/web/src/components/shared-games/SharedGameDetailModal.tsx` | Remove the "View on BGG" anchor block (lines 345-348 region) — it linked to `boardgamegeek.com/boardgame/{bggId}` |
| 3 | `apps/web/src/app/(authenticated)/settings/services/page.tsx` | Remove `{ id: 'bgg', label: 'BoardGameGeek', ... }` entry from services array |
| 4 | `apps/web/src/components/games/BggSearchPanel.tsx` | DELETE (orphan duplicate) |
| 5 | `apps/web/src/__tests__/components/games/BggSearchPanel.test.tsx` | DELETE (test of orphan) |
| 6 | `apps/web/src/components/games/index.ts` | Remove `export { BggSearchPanel } from './BggSearchPanel';` |

## Files to leave untouched

- `apps/api/src/Api/Routing/BggEndpoints.cs` (admin proxy)
- `apps/api/src/Api/Infrastructure/ExternalServices/BoardGameGeek/*` (BGG API client)
- `apps/web/src/lib/api/clients/bggClient.ts` (used by admin wizard)
- `apps/web/src/components/admin/shared-games/BggSearchPanel.tsx` (admin tool)
- `apps/web/src/types/bgg.ts` (admin types)
- `apps/web/src/app/admin/(dashboard)/shared-games/new/client.tsx` (admin wizard)
- `Game.bggId` schema field (still present in API responses)

## Testing

### Local
- `pnpm typecheck` → 0 errors
- `pnpm exec eslint <touched files>` → 0 errors
- `pnpm exec vitest run` mirato sui consumer di MeepleGameCatalogCard, SharedGameDetailModal, settings services
- `pnpm exec vitest run` su admin BggSearchPanel test (must still pass — proves admin path untouched)

### Browser smoke check
1. `/shared-games/{any-id}` → no "View on BGG" link, no `ID: <number>`
2. `/library` → catalog cards no BGG subtitle
3. `/settings/services` → no "BoardGameGeek" card
4. `/admin/(dashboard)/shared-games/new` → BGG search still works (admin path)

## Out of scope (follow-up candidates)

- Strict isolation: filter `bggId` from non-admin DTO responses at API layer
- Semantic rename: "BGG rating" → "Community rating" in commenti residui
- BGG removal: full delete of admin BGG path (would require alternate catalog-population workflow)

## Risk

- **Low**. Pure UI hide, no backend/DTO changes. Worst case: a user-facing test that asserts on the removed link/badge fails — easily updated.
