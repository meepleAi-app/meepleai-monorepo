# Library Phase 3b #1593 — Rail Upgrade + Drawer Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the existing `RecentActivityRail` from library-only to cross-entity (consuming the new BE-3 `GET /api/v1/activity`), wire the `AdvancedFiltersDrawer` (Phase 3a) into `LibraryHub`, and complete the final structural task of Epic #1585.

**Architecture:** (1) Rename legacy `useActivityFeed` (dashboard) → `useDashboardActivityFeed` to free the name (E1 prereq). (2) Create greenfield Zod + client + `useActivityFeed` for the new endpoint. (3) Adapter `toActivityItem(dto)` maps `eventType → kind` (expanding `ActivityKind` with `agent`/`chat`) and derives `entityTitle = dto.title ?? i18nFallback(eventType)`. (4) Upgrade rail with i18n connect, error state, and isLoading forward. (5) Mount `AdvancedFiltersDrawer` in LibraryHub with state + "Più filtri" chip in `CrossEntityFilters`.

**Tech Stack:** Next.js 16 · React 19 · TanStack Query · Zod · Vitest · React Testing Library · react-intl

**Spec source:** Issue #1593 body (refined 2026-05-28 via /sc:spec-panel discussion; 5 locked decisions R1-R5). BE-3 #1590 + Phase 3a #1606 + Phase 2b #1592 all MERGED.

**⚠️ Branch policy:** parent = `main-dev`. PR target = `main-dev`. Commit messages end with `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>`. Headers ≤80 chars.

---

## File Structure

| File | Responsibility | Status |
|---|---|---|
| `apps/web/src/hooks/useDashboardActivityFeed.ts` | Renamed from `useActivityFeed.ts` (PlayRecords+Badges dashboard hook) | Rename |
| `apps/web/src/hooks/__tests__/useDashboardActivityFeed.test.ts` | Renamed from `useActivityFeed.test.ts` + import path | Rename |
| `apps/web/src/components/profile/ActivityFeed.tsx` | Import path update: `useActivityFeed` → `useDashboardActivityFeed` | Modify |
| `apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx` | Mock path update | Modify |
| `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx` | Mock path update | Modify |
| `apps/web/src/lib/api/schemas/activity.schemas.ts` | Zod for `ActivityItemDto` (10 fields) + envelope `{success, items, count}` | Create |
| `apps/web/src/lib/api/clients/activityClient.ts` | Client `listActivity({limit?, since?})` | Create |
| `apps/web/src/lib/api/clients/index.ts` | Barrel re-export of `activityClient` | Modify |
| `apps/web/src/lib/api/index.ts` | Register `activity: ActivityClient` in `ApiClient` + factory | Modify |
| `apps/web/src/hooks/useActivityFeed.ts` | **NEW** hook consuming `/api/v1/activity` (after the rename above frees the slot) | Create |
| `apps/web/src/lib/library/activity-adapter.ts` | Pure adapter `toActivityItem(dto)` — `eventType → kind`, title fallback | Create |
| `apps/web/src/lib/library/__tests__/activity-adapter.test.ts` | Unit tests for adapter | Create |
| `apps/web/src/hooks/__tests__/useActivityFeed.test.tsx` | Tests for new hook | Create |
| `apps/web/src/components/features/library/RecentActivityRail.tsx` | Upgrade: i18n connect + expand `ActivityKind` w/ `'agent'`+`'chat'` + error state + accept `error?: Error \| null` prop | Modify |
| `apps/web/src/components/features/library/__tests__/RecentActivityRail.test.tsx` | Update: i18n keys + cross-entity kinds + error state | Modify |
| `apps/web/src/components/features/library/index.ts` | Re-export `AdvancedFiltersDrawer` from barrel | Modify |
| `apps/web/src/components/features/library/CrossEntityFilters.tsx` | Add "Più filtri" chip + `onMoreFilters?: () => void` callback | Modify |
| `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` | Switch `useLibraryActivity` → new `useActivityFeed`; pass `isLoading`+`error` to rail; mount `<AdvancedFiltersDrawer>`; add `drawerOpen`+`activeFilters` state; wire "Più filtri" chip | Modify |
| `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx` | Update: drawer open/close, "Più filtri" click, cross-entity items, isLoading+error forwarding | Modify |
| `apps/web/src/locales/it.json` + `en.json` | Add `pages.library.activityRail.error` + 5 `pages.library.activityRail.fallback.{agent,chat,kbIndexed,play,removed}` keys | Modify |

---

## Verified reference facts (P74-grounded, do not re-discover)

- **BE-3 `/api/v1/activity` envelope**: `{ success: boolean, items: ActivityItemDto[], count: number }` (`count` = page size, NOT global total).
- **`ActivityItemDto` 10 fields** (all camelCase, NO `payload`, NO `message`): `id` uuid, `eventId` uuid, `eventType` string, `userId` uuid, `entityType` string, `entityId` uuid, **`title` string nullable**, `timestamp` ISO datetime, `loggedAt` ISO datetime, `payloadVersion` int.
- **Query params**: `limit` 1-100 default 20; `since` ISO datetime upper-bound cursor (optional). No `page`/`before`/`offset`. 90-day retention server-side.
- **Auth**: `userId` auto-scoped from session; 401 if unauth, 422 invalid params.
- **Current rail file**: `apps/web/src/components/features/library/RecentActivityRail.tsx` (120 lines). `ActivityKind` union (line 31): `'play' | 'add' | 'kb-indexed' | 'rating-changed' | 'removed'`. `ActivityItem` (line 33): `{ id, kind, entityTitle, timestamp }`. Hardcoded strings: "Recent activity" (line 79), "Activity feed prossimamente" (line 96).
- **Current `useActivityFeed` legacy** (`apps/web/src/hooks/useActivityFeed.ts`, 110 lines): consumes `api.playRecords.getHistory` + `api.badges.getMyBadges`, exposes its own `ActivityItem = { id, type, title, subtitle?, timestamp, iconEmoji }`. 3 call-sites: `components/profile/ActivityFeed.tsx:4`, `components/profile/__tests__/ActivityFeed.test.tsx:5`, `app/(authenticated)/profile/__tests__/page.test.tsx:46`.
- **`AdvancedFiltersDrawerProps`** (`apps/web/src/components/features/library/AdvancedFiltersDrawer/types.ts:63-74`): `{ open: boolean, onOpenChange: (open: boolean) => void, entityScope: HybridHubEntity, activeFilters: LibraryFilters, onApply: (filters: LibraryFilters) => void, onClear: () => void }`.
- **`LibraryFilters`** discriminated union (5 variants — `'game'`/`'agent'`/`'session'`/`'kb'`/`'chat'`).
- **`HybridHubEntity`** (`apps/web/src/lib/library/hybrid-hub.types.ts:30`): `'game' | 'agent' | 'kb' | 'session' | 'chat'`. **`HybridHubTab`** (`hybrid-hub.derive.ts:14`): `'all' | 'games' | 'agents' | 'kb' | 'sessions' | 'chat'` (plural for non-all). The tab→entity mapping: `games→game`, `agents→agent`, `kb→kb`, `sessions→session`, `chat→chat`, `all → default to 'game'` (or hide drawer trigger on `all` tab).
- **`LibraryHub.tsx`** current state lines (verified):
  - line 17: `useState` imports
  - line 40: `RecentActivityRail` import
  - line 55: `useLibraryActivity` import
  - line 91-106: state hooks (tab, selectionMode, selected, query, sortKey, gameStateFilter, gamesStatus, gamesSort, gamesQuery, gamesView)
  - line 140: `const activityQuery = useLibraryActivity(20);`
  - line 179-210: `activityItems = useMemo<readonly ActivityItem[]>(() => { ... switch on event.type ... })` mapping from `LibraryActivityItem[]`
  - line 413: `setGameStateFilter({ states: [], withKb: false });` (reset on tab change?)
  - line 479-480: `<CrossEntityFilters tab={tab} gameStateFilter={gameStateFilter} onGameStateFilterChange={setGameStateFilter} />`
  - line 558: `<RecentActivityRail items={activityItems} />` (does NOT pass isLoading/error — gap)
- **i18n keys ALREADY present** in `apps/web/src/locales/it.json:1755-1761` and `en.json:1705-1711`:
  - `pages.library.activityRail.title` = "Attività recente"
  - `pages.library.activityRail.subtitle` = "Le ultime aggiunte e modifiche alla tua libreria."
  - `pages.library.activityRail.empty` = "Nessuna attività recente."
  - `pages.library.activityRail.viewAll` = "Vedi tutto"
  - `pages.library.activityRail.viewAllAriaLabel` = "Vai allo storico completo dell'attività"
- **i18n keys for filters** ALREADY present (consumed by `AdvancedFiltersDrawer`): `pages.library.filters.title`, `description`, `apply`, `clear`. New "Più filtri" chip can reuse `pages.library.filters.title` ("Più filtri").
- **`api` registry pattern** (from Phase 2b Task 2): `apps/web/src/lib/api/index.ts` declares interface field + `createApi` factory entry; `apps/web/src/lib/api/clients/index.ts` barrel re-export.
- **`httpClient.get<T>(url, schema)`** pattern: returns `Promise<T | null>` → use null-coalescing fallback in client.

---

## ⚠️ Brownfield risks

- **R1**: Renaming `useActivityFeed.ts` then creating a NEW `useActivityFeed.ts` at the same path is a 2-step file operation. Use `git mv` to preserve history on the rename, then create the new file. Verify no stale import remains by grep BEFORE the second step.
- **R2**: `LibraryHub.tsx:179-210` has a `useMemo` mapping `LibraryActivityItem[]` → `ActivityItem[]` with a switch on `event.type` for the legacy 4 library kinds. This block will be **removed** — the new `useActivityFeed` returns the adapted items directly via the adapter.
- **R3**: `CrossEntityFilters` currently renders nothing for non-`games` tabs (line 50: `if (tab !== 'games') return null;`). The "Più filtri" chip must render on ALL tabs (the drawer is the entity-scope mechanism for non-game filters too). Adjust the early-return to allow the chip to render universally, OR move the chip outside the early-return block.
- **R4**: `AdvancedFiltersDrawer.entityScope: HybridHubEntity` is one of `'game'|'agent'|'kb'|'session'|'chat'`. The `'all'` tab has no entity — strategy: when `tab === 'all'`, default entityScope to `'game'` (most-used) and document this in the chip's behavior. Alternative: hide the chip on `'all'` tab. **Plan choice**: hide on `'all'` (no entity-specific filters make sense in cross-entity view; users switch tab first).
- **R5**: Existing `gameStateFilter` state in LibraryHub (states + withKb) overlaps with the drawer's `GameLibraryFilters.states`/`withKb`. To avoid dual-source-of-truth, the drawer's `activeFilters: LibraryFilters` becomes the authoritative state; the chips in `CrossEntityFilters` read/write the SAME state via the discriminated-union narrowing. **Plan choice**: keep `gameStateFilter` for now (the chip row is the always-visible quick toggle; the drawer is the deep-filter UI), and the Apply callback writes ONLY `LibraryFilters` to the new state — keeping two-way sync simple. Detailed sync logic in Task 9.

---

## Task 1: E1 rename — `useActivityFeed` → `useDashboardActivityFeed`

**Goal**: Free the `useActivityFeed.ts` slot before creating the new rail hook. Three call-sites updated.

**Files:**
- Rename: `apps/web/src/hooks/useActivityFeed.ts` → `useDashboardActivityFeed.ts`
- Rename: `apps/web/src/hooks/__tests__/useActivityFeed.test.ts` → `useDashboardActivityFeed.test.ts`
- Modify: `apps/web/src/components/profile/ActivityFeed.tsx` (import path)
- Modify: `apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx` (mock path)
- Modify: `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx` (mock path)

- [ ] **Step 1: Verify the 3 call-sites**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
grep -rn "useActivityFeed\b\|'@/hooks/useActivityFeed'\|\"@/hooks/useActivityFeed\"" apps/web/src 2>&1 | head -20
```
Expected: exactly 5 lines (hook file itself line 53, hook test, ActivityFeed.tsx line 4, ActivityFeed.test.tsx, profile/page.test.tsx). If MORE than 3 consumer call-sites, STOP and re-evaluate.

- [ ] **Step 2: Rename source + test files (preserve history)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git mv apps/web/src/hooks/useActivityFeed.ts apps/web/src/hooks/useDashboardActivityFeed.ts
git mv apps/web/src/hooks/__tests__/useActivityFeed.test.ts apps/web/src/hooks/__tests__/useDashboardActivityFeed.test.ts
```

- [ ] **Step 3: Rename the exported function + tests**

In `apps/web/src/hooks/useDashboardActivityFeed.ts` line 53, change:
```typescript
export function useActivityFeed(limit = 10): UseActivityFeedResult {
```
to:
```typescript
export function useDashboardActivityFeed(limit = 10): UseDashboardActivityFeedResult {
```

Also rename the interface `UseActivityFeedResult` → `UseDashboardActivityFeedResult` (line 25) and update the return-type annotation on line 53.

In `apps/web/src/hooks/__tests__/useDashboardActivityFeed.test.ts`, find all `useActivityFeed` references (function name + import) and replace with `useDashboardActivityFeed`. Use:
```bash
grep -n "useActivityFeed" apps/web/src/hooks/__tests__/useDashboardActivityFeed.test.ts
```
Then `Edit` each line. The import becomes `import { useDashboardActivityFeed } from '../useDashboardActivityFeed';`.

- [ ] **Step 4: Update the 3 consumer call-sites**

In `apps/web/src/components/profile/ActivityFeed.tsx`, replace:
```typescript
import { useActivityFeed } from '@/hooks/useActivityFeed';
```
with:
```typescript
import { useDashboardActivityFeed } from '@/hooks/useDashboardActivityFeed';
```
And rename the call `const { items, isLoading, error } = useActivityFeed(...)` → `useDashboardActivityFeed(...)`. Use grep first to find the exact lines.

In `apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx`, update:
- The `vi.mock` path: `vi.mock('@/hooks/useActivityFeed', ...)` → `vi.mock('@/hooks/useDashboardActivityFeed', ...)`
- The import: `import { useActivityFeed } from '@/hooks/useActivityFeed';` → `import { useDashboardActivityFeed } from '@/hooks/useDashboardActivityFeed';`
- `vi.mocked(useActivityFeed)` → `vi.mocked(useDashboardActivityFeed)`

In `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx`, apply the same 3 edits (mock path, import, mocked reference).

- [ ] **Step 5: Run the renamed test file + the 2 consumer test files**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/hooks/__tests__/useDashboardActivityFeed.test.ts" "src/components/profile/__tests__/ActivityFeed.test.tsx" "src/app/(authenticated)/profile/__tests__/page.test.tsx" 2>&1 | tail -10
```
Expected: ALL existing tests PASS. The rename is pure refactor — no behavior change.

- [ ] **Step 6: Typecheck full project (catches any stale import I missed)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -6
```
Expected: 0 errors. If there's a stale import (e.g. test mock somewhere I missed), grep again and fix.

- [ ] **Step 7: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add -A
git -c commit.gpgsign=false commit -m "refactor(hooks): #1593 rename useActivityFeed to useDashboardActivityFeed (E1)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: Zod `ActivityItemDto` schema + envelope

**Goal**: Define the FE Zod schema mirroring BE-3's `ActivityItemDto` + envelope.

**Files:**
- Create: `apps/web/src/lib/api/schemas/activity.schemas.ts`
- Create: `apps/web/src/lib/api/schemas/__tests__/activity.schemas.test.ts`

- [ ] **Step 1: Write the failing schema test**

Create `apps/web/src/lib/api/schemas/__tests__/activity.schemas.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import {
  ActivityFeedResponseSchema,
  ActivityItemDtoSchema,
  type ActivityItemDto,
} from '../activity.schemas';

describe('activity schemas', () => {
  const validItem: ActivityItemDto = {
    id: '11111111-1111-1111-1111-111111111111',
    eventId: '22222222-2222-2222-2222-222222222222',
    eventType: 'agent.created',
    userId: '33333333-3333-3333-3333-333333333333',
    entityType: 'Agent',
    entityId: '44444444-4444-4444-4444-444444444444',
    title: 'Catan Tutor',
    timestamp: '2026-05-28T11:00:00+00:00',
    loggedAt: '2026-05-28T11:00:01+00:00',
    payloadVersion: 1,
  };

  it('accepts a full populated item', () => {
    expect(() => ActivityItemDtoSchema.parse(validItem)).not.toThrow();
  });

  it('accepts title=null', () => {
    const parsed = ActivityItemDtoSchema.parse({ ...validItem, title: null });
    expect(parsed.title).toBeNull();
  });

  it('rejects items missing eventId (BE-3 contract has eventId for idempotency)', () => {
    const { eventId, ...rest } = validItem;
    expect(() => ActivityItemDtoSchema.parse(rest)).toThrow();
  });

  it('rejects items with a "payload" field (BE-3 explicitly does not project payload)', () => {
    expect(() => ActivityItemDtoSchema.parse({ ...validItem, payload: { foo: 'bar' } })).toThrow();
  });

  it('rejects items with a "message" field (no message in BE-3 DTO)', () => {
    expect(() => ActivityItemDtoSchema.parse({ ...validItem, message: 'hi' })).toThrow();
  });

  it('rejects malformed timestamp', () => {
    expect(() => ActivityItemDtoSchema.parse({ ...validItem, timestamp: 'yesterday' })).toThrow();
  });

  it('parses the envelope { success, items, count }', () => {
    const envelope = { success: true, items: [validItem], count: 1 };
    const parsed = ActivityFeedResponseSchema.parse(envelope);
    expect(parsed.items).toHaveLength(1);
    expect(parsed.count).toBe(1);
  });

  it('rejects envelope without success flag', () => {
    expect(() =>
      ActivityFeedResponseSchema.parse({ items: [], count: 0 })
    ).toThrow();
  });
});
```

- [ ] **Step 2: Run to verify FAIL**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/api/schemas/__tests__/activity.schemas.test.ts 2>&1 | tail -10
```
Expected: FAIL — module `'../activity.schemas'` does not exist.

- [ ] **Step 3: Create the schema**

Create `apps/web/src/lib/api/schemas/activity.schemas.ts`:

```typescript
/**
 * Activity Feed Schemas (Issue #1593 Phase 3b)
 *
 * Zod schemas for the cross-entity activity feed endpoint (BE-3 #1590):
 * GET /api/v1/activity?limit=&since=.
 *
 * Matches `ActivityItemDto` from
 * apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ActivityFeed/ActivityItemDto.cs.
 *
 * Important contract notes (verified 2026-05-28 against PR #1641):
 * - NO `payload` field — the BE intentionally hides the raw event payload.
 * - NO `message` field — the FE derives display text from `eventType` + `title?`.
 * - Envelope is `{success, items, count}` where `count` is the page size (NOT a global total).
 */

import { z } from 'zod';

/**
 * ActivityItemDto — 10 fields, all camelCase, only `title` is nullable.
 */
export const ActivityItemDtoSchema = z
  .object({
    id: z.string().uuid(),
    eventId: z.string().uuid(),
    eventType: z.string().min(1),
    userId: z.string().uuid(),
    entityType: z.string().min(1),
    entityId: z.string().uuid(),
    title: z.string().nullable(),
    timestamp: z.string().datetime({ offset: true }),
    loggedAt: z.string().datetime({ offset: true }),
    payloadVersion: z.number().int().positive(),
  })
  .strict();

export type ActivityItemDto = z.infer<typeof ActivityItemDtoSchema>;

/**
 * Envelope. `count` is the page size returned, NOT a global total.
 */
export const ActivityFeedResponseSchema = z
  .object({
    success: z.boolean(),
    items: z.array(ActivityItemDtoSchema),
    count: z.number().int().nonnegative(),
  })
  .strict();

export type ActivityFeedResponse = z.infer<typeof ActivityFeedResponseSchema>;
```

- [ ] **Step 4: Run to verify PASS**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/api/schemas/__tests__/activity.schemas.test.ts 2>&1 | tail -10
```
Expected: 8 PASS.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/api/schemas/activity.schemas.ts apps/web/src/lib/api/schemas/__tests__/activity.schemas.test.ts
git -c commit.gpgsign=false commit -m "feat(activity): #1593 add ActivityItemDto Zod schema (Phase 3b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: `activityClient.listActivity` + api registry wiring

**Goal**: Client method calling `GET /api/v1/activity` with Zod validation, registered in the api root.

**Files:**
- Create: `apps/web/src/lib/api/clients/activityClient.ts`
- Modify: `apps/web/src/lib/api/clients/index.ts` (barrel re-export)
- Modify: `apps/web/src/lib/api/index.ts` (interface field + factory registration)

- [ ] **Step 1: Create the client**

Create `apps/web/src/lib/api/clients/activityClient.ts`:

```typescript
/**
 * Activity Client (Issue #1593 Phase 3b)
 *
 * Modular client for the cross-entity activity feed endpoint (BE-3 #1590).
 * Single method: `listActivity({limit?, since?})`.
 */

import {
  ActivityFeedResponseSchema,
  type ActivityFeedResponse,
} from '../schemas/activity.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateActivityClientParams {
  httpClient: HttpClient;
}

export interface ListActivityParams {
  /** 1-100 inclusive. Server default 20. */
  limit?: number;
  /** Optional ISO datetime upper-bound cursor: returns events where loggedAt < since. */
  since?: string;
}

export interface ActivityClient {
  listActivity(params?: ListActivityParams): Promise<ActivityFeedResponse>;
}

export function createActivityClient({ httpClient }: CreateActivityClientParams): ActivityClient {
  return {
    async listActivity(params: ListActivityParams = {}): Promise<ActivityFeedResponse> {
      const qs = new URLSearchParams();
      if (params.limit !== undefined) qs.append('limit', String(params.limit));
      if (params.since !== undefined) qs.append('since', params.since);

      const url = `/api/v1/activity${qs.toString() ? `?${qs.toString()}` : ''}`;
      const response = await httpClient.get<ActivityFeedResponse>(url, ActivityFeedResponseSchema);
      // Pattern from libraryClient: defensive fallback when response is null.
      return response ?? { success: true, items: [], count: 0 };
    },
  };
}
```

- [ ] **Step 2: Add to the barrel**

In `apps/web/src/lib/api/clients/index.ts`, find the existing `export * from './kbDocsClient';` line (Phase 2b reference) and add:

```typescript
export * from './activityClient';
```

- [ ] **Step 3: Register in the api root**

In `apps/web/src/lib/api/index.ts`:

1. Add the import alongside other client imports (look for `createKbDocsClient, type KbDocsClient` as reference):
```typescript
import { createActivityClient, type ActivityClient } from './clients/activityClient';
```
Or, since the barrel re-exports it, add to the destructured import block:
```typescript
import {
  // ... existing names ...
  createActivityClient,
  type ActivityClient,
} from './clients';
```

2. Add the field to the `ApiClient` interface (alphabetically after `agents` or wherever consistent):
```typescript
/** Cross-entity activity feed (Issue #1593 Phase 3b) */
activity: ActivityClient;
```

3. Register it in the `createApiClient` factory:
```typescript
activity: createActivityClient({ httpClient }),
```

(Read `apps/web/src/lib/api/index.ts` first to find the exact neighbor lines — mirror Phase 2b Task 2's edit pattern.)

- [ ] **Step 4: Typecheck**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -6
```
Expected: 0 errors. `api.activity.listActivity(...)` is now type-safe.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/api/clients/activityClient.ts apps/web/src/lib/api/clients/index.ts apps/web/src/lib/api/index.ts
git -c commit.gpgsign=false commit -m "feat(activity): #1593 add activityClient.listActivity (Phase 3b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: `toActivityItem` adapter — eventType → kind + title fallback

**Goal**: Pure adapter mapping `ActivityItemDto` → `ActivityItem` (the rail's shape). Handles eventType → kind discriminant + `title ?? i18nFallback(eventType)`. Pure, unit-testable, no React.

**Files:**
- Create: `apps/web/src/lib/library/activity-adapter.ts`
- Create: `apps/web/src/lib/library/__tests__/activity-adapter.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/library/__tests__/activity-adapter.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { toActivityItem } from '../activity-adapter';
import type { ActivityItemDto } from '@/lib/api/schemas/activity.schemas';

const dtoBase: Omit<ActivityItemDto, 'eventType' | 'entityType' | 'title'> = {
  id: '11111111-1111-1111-1111-111111111111',
  eventId: '22222222-2222-2222-2222-222222222222',
  userId: '33333333-3333-3333-3333-333333333333',
  entityId: '44444444-4444-4444-4444-444444444444',
  timestamp: '2026-05-28T11:00:00+00:00',
  loggedAt: '2026-05-28T11:00:01+00:00',
  payloadVersion: 1,
};

const fallback = {
  agent: 'Nuovo agent creato',
  chat: 'Nuova chat',
  kbIndexed: 'Documento indicizzato',
  play: 'Sessione',
  removed: 'Rimosso dalla libreria',
};

describe('toActivityItem — eventType → kind mapping (R2)', () => {
  it('maps agent.created → kind="agent"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'agent.created', entityType: 'Agent', title: 'Catan Tutor' },
      fallback
    );
    expect(result.kind).toBe('agent');
    expect(result.entityTitle).toBe('Catan Tutor');
  });

  it('maps chat.session.created → kind="chat"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'chat.session.created', entityType: 'ChatSession', title: 'Catan chat' },
      fallback
    );
    expect(result.kind).toBe('chat');
    expect(result.entityTitle).toBe('Catan chat');
  });

  it('maps kb.doc.indexed → kind="kb-indexed"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'kb.doc.indexed', entityType: 'PdfDocument', title: 'rules.pdf' },
      fallback
    );
    expect(result.kind).toBe('kb-indexed');
  });

  it('maps session.created → kind="play"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'session.created', entityType: 'Session', title: null },
      fallback
    );
    expect(result.kind).toBe('play');
  });

  it('maps session.finalized → kind="play"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'session.finalized', entityType: 'Session', title: null },
      fallback
    );
    expect(result.kind).toBe('play');
  });

  it('maps library.entry.removed → kind="removed"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'library.entry.removed', entityType: 'UserLibraryEntry', title: null },
      fallback
    );
    expect(result.kind).toBe('removed');
  });

  it('maps library.session.recorded → kind="play"', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'library.session.recorded', entityType: 'UserLibraryEntry', title: null },
      fallback
    );
    expect(result.kind).toBe('play');
  });

  it('unknown eventType falls back to kind="add" (safe default)', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'something.unknown', entityType: 'Other', title: null },
      fallback
    );
    expect(result.kind).toBe('add');
  });
});

describe('toActivityItem — title fallback (R2)', () => {
  it('uses dto.title when present', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'agent.created', entityType: 'Agent', title: 'My Agent' },
      fallback
    );
    expect(result.entityTitle).toBe('My Agent');
  });

  it('falls back to i18n key per kind when title is null (kb)', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'kb.doc.indexed', entityType: 'PdfDocument', title: null },
      fallback
    );
    expect(result.entityTitle).toBe('Documento indicizzato');
  });

  it('falls back to i18n key per kind when title is null (play)', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'session.created', entityType: 'Session', title: null },
      fallback
    );
    expect(result.entityTitle).toBe('Sessione');
  });

  it('preserves id and timestamp from the DTO', () => {
    const result = toActivityItem(
      { ...dtoBase, eventType: 'agent.created', entityType: 'Agent', title: 'X' },
      fallback
    );
    expect(result.id).toBe(dtoBase.id);
    expect(result.timestamp).toBe(dtoBase.timestamp);
  });
});
```

- [ ] **Step 2: Run to verify FAIL**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/library/__tests__/activity-adapter.test.ts 2>&1 | tail -10
```
Expected: FAIL — module does not exist.

- [ ] **Step 3: Create the adapter**

Create `apps/web/src/lib/library/activity-adapter.ts`:

```typescript
/**
 * activity-adapter — Phase 3b (#1593) DTO → rail item mapper.
 *
 * Maps `ActivityItemDto` (BE-3) → `ActivityItem` (rail). Pure function,
 * unit-testable. Two responsibilities:
 *   1. Map `eventType` → `ActivityKind` discriminant (rail's icon/styling driver).
 *   2. Resolve `entityTitle = dto.title ?? fallbacks[kindKey]` so `title=null`
 *      cases (no payload-derived display name) still show meaningful copy.
 *
 * The fallback map is INJECTED by the caller (the hook) which provides
 * i18n-translated strings. Keeping the adapter pure (no i18n IO) keeps it
 * unit-testable with literal strings.
 */

import type { ActivityItemDto } from '@/lib/api/schemas/activity.schemas';
import type { ActivityItem, ActivityKind } from '@/components/features/library/RecentActivityRail';

/**
 * i18n strings injected by the consumer (resolved via useTranslation).
 * Keys map 1:1 to ActivityKind variants.
 */
export interface ActivityKindFallbacks {
  readonly agent: string;
  readonly chat: string;
  readonly kbIndexed: string;
  readonly play: string;
  readonly removed: string;
}

/**
 * Map a BE-3 `eventType` to the rail's `ActivityKind`. Unknown event types
 * fall back to `'add'` (safe default — visible but not misleading).
 */
function mapEventTypeToKind(eventType: string): ActivityKind {
  switch (eventType) {
    case 'agent.created':
      return 'agent';
    case 'chat.session.created':
      return 'chat';
    case 'kb.doc.indexed':
      return 'kb-indexed';
    case 'session.created':
    case 'session.finalized':
    case 'library.session.recorded':
      return 'play';
    case 'library.entry.removed':
      return 'removed';
    default:
      // Defensive: unknown event types still render via a neutral kind.
      return 'add';
  }
}

/**
 * Map ActivityKind → fallback i18n string for the title (when dto.title is null).
 */
function fallbackForKind(kind: ActivityKind, fallbacks: ActivityKindFallbacks): string {
  switch (kind) {
    case 'agent':
      return fallbacks.agent;
    case 'chat':
      return fallbacks.chat;
    case 'kb-indexed':
      return fallbacks.kbIndexed;
    case 'play':
      return fallbacks.play;
    case 'removed':
      return fallbacks.removed;
    case 'add':
    case 'rating-changed':
      // Legacy library kinds (no BE-3 mapping today) — return play as a sane neutral.
      return fallbacks.play;
  }
}

export function toActivityItem(dto: ActivityItemDto, fallbacks: ActivityKindFallbacks): ActivityItem {
  const kind = mapEventTypeToKind(dto.eventType);
  const entityTitle = dto.title ?? fallbackForKind(kind, fallbacks);
  return {
    id: dto.id,
    kind,
    entityTitle,
    timestamp: dto.timestamp,
  };
}
```

- [ ] **Step 4: Run to verify PASS**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/library/__tests__/activity-adapter.test.ts 2>&1 | tail -10
```
Expected: 12 PASS.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/library/activity-adapter.ts apps/web/src/lib/library/__tests__/activity-adapter.test.ts
git -c commit.gpgsign=false commit -m "feat(activity): #1593 add toActivityItem adapter (R2 mapping)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: Expand `ActivityKind` + new i18n keys

**Goal**: (a) Expand `ActivityKind` union in `RecentActivityRail.tsx` with `'agent'` + `'chat'`; (b) Add the 5 fallback i18n keys + the error key. NO logic changes yet — that's Task 6.

**Files:**
- Modify: `apps/web/src/components/features/library/RecentActivityRail.tsx` (lines 31, 48-54 — type union + icon map)
- Modify: `apps/web/src/locales/it.json`
- Modify: `apps/web/src/locales/en.json`

- [ ] **Step 1: Expand `ActivityKind` union + icon map**

In `apps/web/src/components/features/library/RecentActivityRail.tsx`:

Replace line 31:
```typescript
export type ActivityKind = 'play' | 'add' | 'kb-indexed' | 'rating-changed' | 'removed';
```
with:
```typescript
export type ActivityKind = 'play' | 'add' | 'kb-indexed' | 'rating-changed' | 'removed' | 'agent' | 'chat';
```

And update the `KIND_ICON` record (lines 48-54) to include the new icons:
```typescript
const KIND_ICON: Record<ActivityKind, string> = {
  play: '🎲',
  add: '➕',
  'kb-indexed': '📖',
  'rating-changed': '⭐',
  removed: '🗑️',
  agent: '🤖',
  chat: '💬',
};
```

- [ ] **Step 2: Add fallback + error i18n keys to `it.json`**

In `apps/web/src/locales/it.json`, find the existing `pages.library.activityRail` block (around line 1755-1761) and add the new keys. The keys are nested under `pages.library.activityRail` as the existing 5 are. Locate the closing `}` of that nested object and add:

```jsonc
{
  "pages": {
    "library": {
      "activityRail": {
        "title": "Attività recente",
        "subtitle": "Le ultime aggiunte e modifiche alla tua libreria.",
        "empty": "Nessuna attività recente.",
        "viewAll": "Vedi tutto",
        "viewAllAriaLabel": "Vai allo storico completo dell'attività",
        "error": "Impossibile caricare l'attività.",
        "fallback": {
          "agent": "Nuovo agent creato",
          "chat": "Nuova chat",
          "kbIndexed": "Documento indicizzato",
          "play": "Sessione",
          "removed": "Rimosso dalla libreria"
        }
      }
    }
  }
}
```

(Use Read on `it.json:1755-1770` first to find the EXACT existing structure; use Edit with a unique `old_string` matching the closing `}` of the `activityRail` object and add the 2 new entries `"error": ...` and `"fallback": { ... }` keys.)

- [ ] **Step 3: Add the same keys to `en.json` (English translations)**

In `apps/web/src/locales/en.json` (around line 1705-1711), apply the same structural change with English values:

```jsonc
{
  "activityRail": {
    "title": "Recent activity",
    "subtitle": "Latest additions and changes to your library.",
    "empty": "No recent activity.",
    "viewAll": "View all",
    "viewAllAriaLabel": "Go to full activity history",
    "error": "Failed to load activity.",
    "fallback": {
      "agent": "New agent created",
      "chat": "New chat",
      "kbIndexed": "Document indexed",
      "play": "Session",
      "removed": "Removed from library"
    }
  }
}
```

- [ ] **Step 4: Typecheck (catches structural JSON errors via the i18n type generator if any)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -6
```
Expected: 0 errors. If the project has a generated i18n type definition file that requires regeneration, run the project's i18n type-gen command (check `package.json` scripts for `i18n` / `gen` / `intl` keywords). Otherwise types are inferred at use-site and typecheck passes.

- [ ] **Step 5: Run existing rail tests (regression — Task 5 is additive, no test should fail)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/components/features/library/__tests__/RecentActivityRail.test.tsx" 2>&1 | tail -10
```
Expected: existing 10+ tests still PASS (we added 2 kinds + 2 icons + 6 new i18n keys, no removals).

- [ ] **Step 6: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/components/features/library/RecentActivityRail.tsx apps/web/src/locales/it.json apps/web/src/locales/en.json
git -c commit.gpgsign=false commit -m "feat(activity-rail): #1593 expand ActivityKind w/ agent+chat + i18n keys

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: Rail i18n connect + error state

**Goal**: Replace hardcoded strings in `RecentActivityRail.tsx` with `useTranslation` consuming the i18n keys. Add the 4th state `'error'` (R4).

**Files:**
- Modify: `apps/web/src/components/features/library/RecentActivityRail.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/RecentActivityRail.test.tsx`

- [ ] **Step 1: Update test file FIRST (TDD-RED for the i18n + error state changes)**

Read the existing test file:
```bash
cat apps/web/src/components/features/library/__tests__/RecentActivityRail.test.tsx
```

Identify:
- Tests asserting hardcoded "Activity feed prossimamente" → change to assert i18n key resolution.
- Tests asserting hardcoded "Recent activity" heading → change similarly.
- Add new tests for the error state.

The test rendering pattern in the project uses `react-intl` via `@/hooks/useTranslation` (confirmed Phase 3a). For tests, render with a `<TestIntlProvider>` or messages-injection wrapper following the existing patterns (look at how the `AdvancedFiltersDrawer.test.tsx` consumes i18n — Phase 3a did this).

Add tests:

```typescript
// new tests to append
it('shows i18n empty message when items=[] (R4 empty state)', () => {
  // render with provider, assert the empty-state text matches the i18n key value
  // (the test framework's t() returns the message string in test mode)
  renderWithIntl(<RecentActivityRail items={[]} />);
  // The i18n key 'pages.library.activityRail.empty' resolves to "Nessuna attività recente."
  // in the project's test intl provider (it uses real messages).
  expect(screen.getByTestId('library-activity-empty-text')).toHaveTextContent(
    /Nessuna attività recente/i
  );
});

it('shows i18n error message when error prop is set (R4 error state)', () => {
  renderWithIntl(<RecentActivityRail items={[]} error={new Error('500 error')} />);
  expect(screen.getByTestId('library-activity-error')).toHaveTextContent(
    /Impossibile caricare l'attività/i
  );
  // Skeleton should NOT render
  expect(screen.queryByTestId('library-activity-skeleton')).toBeNull();
  // Items list should NOT render
  expect(screen.queryByRole('list')).toBeNull();
});

it('shows i18n title heading from key (no hardcoded "Recent activity")', () => {
  renderWithIntl(<RecentActivityRail items={[]} />);
  expect(screen.getByText(/Attività recente/i)).toBeInTheDocument();
});

it('error state takes precedence over loading and empty', () => {
  renderWithIntl(<RecentActivityRail items={[]} isLoading={true} error={new Error('e')} />);
  expect(screen.getByTestId('library-activity-error')).toBeInTheDocument();
  expect(screen.queryByTestId('library-activity-skeleton')).toBeNull();
});
```

Adapt the `renderWithIntl` helper to the project's actual test setup — check how the file already renders the component. If it doesn't already use intl, it may need a wrapper.

- [ ] **Step 2: Run to verify FAIL**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/components/features/library/__tests__/RecentActivityRail.test.tsx" 2>&1 | tail -10
```
Expected: 4 new tests FAIL (component doesn't read i18n yet + no error prop).

- [ ] **Step 3: Update `RecentActivityRail.tsx`**

Replace the entire content of `apps/web/src/components/features/library/RecentActivityRail.tsx` with:

```typescript
/**
 * RecentActivityRail — Wave B.3 v2 (Issue #574) upgraded to cross-entity in
 * Phase 3b (Issue #1593).
 *
 * Renders the user's recent activity items from the unified cross-entity feed
 * (BE-3 #1590 `GET /api/v1/activity`). 4 states: loading, empty, populated, error.
 *
 * Phase 3b changes:
 *   - i18n connect: uses `useTranslation` for title/empty/error/aria copy.
 *   - ActivityKind expanded with `'agent'` + `'chat'` (Phase 2b deps are merged).
 *   - 4th state `'error'` for when the activity feed query fails — rail does not
 *     crash; LibraryHub continues to function (Nygard partial-failure).
 *
 * Sidebar width: 280px (`lg:w-72`) hidden under `lg` breakpoint.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useTranslation } from '@/hooks/useTranslation';

export type ActivityKind = 'play' | 'add' | 'kb-indexed' | 'rating-changed' | 'removed' | 'agent' | 'chat';

export interface ActivityItem {
  readonly id: string;
  readonly kind: ActivityKind;
  readonly entityTitle: string;
  readonly timestamp: string;
}

export interface RecentActivityRailProps {
  readonly items: ReadonlyArray<ActivityItem>;
  readonly isLoading?: boolean;
  readonly error?: Error | null;
  readonly className?: string;
}

const SKELETON_LINES = 3;

const KIND_ICON: Record<ActivityKind, string> = {
  play: '🎲',
  add: '➕',
  'kb-indexed': '📖',
  'rating-changed': '⭐',
  removed: '🗑️',
  agent: '🤖',
  chat: '💬',
};

type RailState = 'loading' | 'empty' | 'populated' | 'error';

function resolveState(
  items: ReadonlyArray<ActivityItem>,
  isLoading: boolean,
  error: Error | null | undefined
): RailState {
  // Error takes precedence so failures don't get masked by stale loading flags.
  if (error) return 'error';
  if (isLoading) return 'loading';
  if (items.length === 0) return 'empty';
  return 'populated';
}

export function RecentActivityRail({
  items,
  isLoading = false,
  error = null,
  className,
}: RecentActivityRailProps): ReactElement {
  const { t } = useTranslation();
  const state = resolveState(items, isLoading, error);

  return (
    <aside
      data-slot="library-activity-rail"
      data-state={state}
      aria-busy={isLoading || undefined}
      aria-live="polite"
      className={clsx(
        'hidden flex-col gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm lg:flex lg:w-72',
        className
      )}
    >
      <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
        {t('pages.library.activityRail.title')}
      </h2>

      {state === 'loading' ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: SKELETON_LINES }, (_, index) => (
            <Skeleton
              key={index}
              data-testid="library-activity-skeleton"
              data-slot="library-activity-skeleton"
              className="h-12 w-full rounded-lg"
            />
          ))}
        </div>
      ) : null}

      {state === 'empty' ? (
        <p
          className="text-sm text-muted-foreground"
          data-slot="library-activity-empty"
          data-testid="library-activity-empty-text"
        >
          {t('pages.library.activityRail.empty')}
        </p>
      ) : null}

      {state === 'error' ? (
        <p
          className="text-sm text-destructive"
          role="alert"
          data-slot="library-activity-error"
          data-testid="library-activity-error"
        >
          {t('pages.library.activityRail.error')}
        </p>
      ) : null}

      {state === 'populated' ? (
        <ul className="flex flex-col gap-2">
          {items.map(item => (
            <li
              key={item.id}
              data-slot="library-activity-item"
              data-activity-kind={item.kind}
              className="flex items-center gap-3 rounded-lg border border-border/60 bg-muted/20 px-3 py-2"
            >
              <span aria-hidden="true" className="text-lg">
                {KIND_ICON[item.kind]}
              </span>
              <span className="text-sm text-foreground">{item.entityTitle}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </aside>
  );
}
```

Key changes:
- Added `useTranslation` import + call.
- Added `error?: Error | null` to props.
- Added `'error'` state in the `RailState` union.
- Added the precedence rule in `resolveState` (error > loading > empty > populated).
- Replaced hardcoded "Recent activity" heading with `t('pages.library.activityRail.title')`.
- Replaced "Activity feed prossimamente" with `t('pages.library.activityRail.empty')`.
- Added the error block with `t('pages.library.activityRail.error')`.
- Added `data-testid` attributes for testability.

- [ ] **Step 4: Run to verify PASS**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/components/features/library/__tests__/RecentActivityRail.test.tsx" 2>&1 | tail -10
```
Expected: ALL tests (existing + 4 new) PASS.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/components/features/library/RecentActivityRail.tsx apps/web/src/components/features/library/__tests__/RecentActivityRail.test.tsx
git -c commit.gpgsign=false commit -m "feat(activity-rail): #1593 i18n connect + error state (R4)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: NEW `useActivityFeed` hook (consumes `/api/v1/activity` + uses adapter)

**Goal**: Create the new `useActivityFeed.ts` (the slot freed by Task 1's rename) that calls `api.activity.listActivity`, applies the adapter, and returns `{items, isLoading, error}`.

**Files:**
- Create: `apps/web/src/hooks/useActivityFeed.ts`
- Create: `apps/web/src/hooks/__tests__/useActivityFeed.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/hooks/__tests__/useActivityFeed.test.tsx`:

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useActivityFeed } from '../useActivityFeed';

vi.mock('@/lib/api', () => ({
  api: {
    activity: {
      listActivity: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'pages.library.activityRail.fallback.agent': 'Nuovo agent creato',
        'pages.library.activityRail.fallback.chat': 'Nuova chat',
        'pages.library.activityRail.fallback.kbIndexed': 'Documento indicizzato',
        'pages.library.activityRail.fallback.play': 'Sessione',
        'pages.library.activityRail.fallback.removed': 'Rimosso dalla libreria',
      };
      return map[key] ?? key;
    },
  }),
}));

import { api } from '@/lib/api';

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useActivityFeed', () => {
  beforeEach(() => {
    vi.mocked(api.activity.listActivity).mockReset();
  });

  it('fetches with default limit=20', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({
      success: true,
      items: [],
      count: 0,
    });

    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.activity.listActivity).toHaveBeenCalledWith({ limit: 20 });
  });

  it('adapts dto items to rail ActivityItem via adapter', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({
      success: true,
      items: [
        {
          id: '11111111-1111-1111-1111-111111111111',
          eventId: '22222222-2222-2222-2222-222222222222',
          eventType: 'agent.created',
          userId: '33333333-3333-3333-3333-333333333333',
          entityType: 'Agent',
          entityId: '44444444-4444-4444-4444-444444444444',
          title: 'Catan Tutor',
          timestamp: '2026-05-28T11:00:00+00:00',
          loggedAt: '2026-05-28T11:00:01+00:00',
          payloadVersion: 1,
        },
      ],
      count: 1,
    });

    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.items[0]).toMatchObject({
      kind: 'agent',
      entityTitle: 'Catan Tutor',
      timestamp: '2026-05-28T11:00:00+00:00',
    });
  });

  it('uses i18n fallback when title is null', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({
      success: true,
      items: [
        {
          id: '11111111-1111-1111-1111-111111111111',
          eventId: '22222222-2222-2222-2222-222222222222',
          eventType: 'kb.doc.indexed',
          userId: '33333333-3333-3333-3333-333333333333',
          entityType: 'PdfDocument',
          entityId: '44444444-4444-4444-4444-444444444444',
          title: null,
          timestamp: '2026-05-28T11:00:00+00:00',
          loggedAt: '2026-05-28T11:00:01+00:00',
          payloadVersion: 1,
        },
      ],
      count: 1,
    });

    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items[0].entityTitle).toBe('Documento indicizzato');
  });

  it('forwards a custom limit when provided', async () => {
    vi.mocked(api.activity.listActivity).mockResolvedValue({
      success: true,
      items: [],
      count: 0,
    });

    renderHook(() => useActivityFeed(50), { wrapper });
    await waitFor(() => expect(api.activity.listActivity).toHaveBeenCalled());

    expect(api.activity.listActivity).toHaveBeenCalledWith({ limit: 50 });
  });

  it('exposes error from a failed query', async () => {
    vi.mocked(api.activity.listActivity).mockRejectedValue(new Error('500 server error'));

    const { result } = renderHook(() => useActivityFeed(), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
  });
});
```

- [ ] **Step 2: Run to verify FAIL**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/hooks/__tests__/useActivityFeed.test.tsx 2>&1 | tail -10
```
Expected: FAIL — module `'../useActivityFeed'` does not exist.

- [ ] **Step 3: Create the hook**

Create `apps/web/src/hooks/useActivityFeed.ts`:

```typescript
/**
 * useActivityFeed — Phase 3b (#1593) cross-entity activity feed hook for the
 * `RecentActivityRail` sidebar in LibraryHub.
 *
 * Calls `GET /api/v1/activity?limit=<limit>` (BE-3 #1590), applies the adapter
 * `toActivityItem` (maps `eventType` → `ActivityKind`, derives `entityTitle`
 * with i18n fallback when `title=null`), and returns the rail's `ActivityItem[]`.
 *
 * The legacy `useActivityFeed` (PlayRecords+Badges dashboard) was renamed to
 * `useDashboardActivityFeed` (#1593 E1 prereq) to free this slot.
 */

import { useMemo } from 'react';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { useTranslation } from '@/hooks/useTranslation';
import { toActivityItem } from '@/lib/library/activity-adapter';
import type { ActivityFeedResponse } from '@/lib/api/schemas/activity.schemas';
import type { ActivityItem } from '@/components/features/library/RecentActivityRail';

export interface UseActivityFeedData {
  /** Adapted items (DTO → rail ActivityItem). */
  items: ActivityItem[];
  /** Page size returned by the BE (NOT a global total). */
  count: number;
}

/**
 * Default limit: 20 (BE-3 default). Adjust per call-site if needed.
 */
export function useActivityFeed(limit: number = 20): UseQueryResult<UseActivityFeedData> {
  const { t } = useTranslation();

  const fallbacks = useMemo(
    () => ({
      agent: t('pages.library.activityRail.fallback.agent'),
      chat: t('pages.library.activityRail.fallback.chat'),
      kbIndexed: t('pages.library.activityRail.fallback.kbIndexed'),
      play: t('pages.library.activityRail.fallback.play'),
      removed: t('pages.library.activityRail.fallback.removed'),
    }),
    [t]
  );

  return useQuery({
    queryKey: ['activity', 'feed', { limit }],
    queryFn: async () => {
      const response: ActivityFeedResponse = await api.activity.listActivity({ limit });
      return {
        items: response.items.map(dto => toActivityItem(dto, fallbacks)),
        count: response.count,
      };
    },
    staleTime: 60 * 1000, // 1 minute — aligns with the legacy useLibraryActivity
  });
}
```

- [ ] **Step 4: Run to verify PASS**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/hooks/__tests__/useActivityFeed.test.tsx 2>&1 | tail -10
```
Expected: 5 PASS.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/hooks/useActivityFeed.ts apps/web/src/hooks/__tests__/useActivityFeed.test.tsx
git -c commit.gpgsign=false commit -m "feat(activity): #1593 new useActivityFeed hook for cross-entity rail

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 8: `CrossEntityFilters` "Più filtri" chip + barrel re-export

**Goal**: Add a "Più filtri" chip that opens the drawer via an `onMoreFilters?` callback. Render the chip on ALL tabs (the chip is the entry point to entity-conditional filtering). Re-export `AdvancedFiltersDrawer` from the library barrel.

**Files:**
- Modify: `apps/web/src/components/features/library/CrossEntityFilters.tsx`
- Modify: `apps/web/src/components/features/library/index.ts` (barrel re-export)
- Modify: `apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx` (if exists)

- [ ] **Step 1: Update `CrossEntityFilters.tsx`**

In `apps/web/src/components/features/library/CrossEntityFilters.tsx`:

1. Extend the props interface:
```typescript
export interface CrossEntityFiltersProps {
  readonly tab: HybridHubTab;
  readonly gameStateFilter: GameStateFilter;
  readonly onGameStateFilterChange: (next: GameStateFilter) => void;
  /** Phase 3b: opens the AdvancedFiltersDrawer (when undefined, chip is hidden). */
  readonly onMoreFilters?: () => void;
  /** Phase 3b: number of active drawer filters to badge on the chip (0 = no badge). */
  readonly activeFiltersCount?: number;
  readonly className?: string;
}
```

2. Remove the `if (tab !== 'games') return null;` early-return (line 50). Replace the entire return statement with a structure that renders the STATO chips ONLY on `'games'` tab, and the "Più filtri" chip on ALL tabs:

```typescript
export function CrossEntityFilters({
  tab,
  gameStateFilter,
  onGameStateFilterChange,
  onMoreFilters,
  activeFiltersCount = 0,
  className,
}: CrossEntityFiltersProps): ReactElement | null {
  const { t } = useTranslation();

  const showStato = tab === 'games';
  const showMoreFilters = onMoreFilters !== undefined && tab !== 'all'; // R4: hide on 'all'

  if (!showStato && !showMoreFilters) return null;

  const toggleState = (value: GameStateType) => {
    const has = gameStateFilter.states.includes(value);
    const states = has
      ? gameStateFilter.states.filter(s => s !== value)
      : [...gameStateFilter.states, value];
    onGameStateFilterChange({ ...gameStateFilter, states });
  };

  return (
    <div
      data-slot="cross-entity-filters"
      data-testid="cross-entity-filters-stato"
      className={clsx('flex flex-wrap items-center gap-2', className)}
    >
      {showStato ? (
        <>
          <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {t('pages.library.filters.stato.label')}
          </span>
          {STATE_CHIPS.map(chip => {
            const active = gameStateFilter.states.includes(chip.value);
            return (
              <button
                key={chip.value}
                type="button"
                aria-pressed={active}
                onClick={() => toggleState(chip.value)}
                className={clsx(
                  'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
                  active
                    ? 'border-primary bg-primary/10 text-foreground'
                    : 'border-border bg-background text-muted-foreground hover:text-foreground'
                )}
              >
                {t(chip.i18nKey)}
              </button>
            );
          })}
          <button
            type="button"
            aria-pressed={gameStateFilter.withKb}
            onClick={() =>
              onGameStateFilterChange({ ...gameStateFilter, withKb: !gameStateFilter.withKb })
            }
            className={clsx(
              'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
              gameStateFilter.withKb
                ? 'border-primary bg-primary/10 text-foreground'
                : 'border-border bg-background text-muted-foreground hover:text-foreground'
            )}
          >
            {t('pages.library.filters.stato.withKb')}
          </button>
        </>
      ) : null}

      {showMoreFilters ? (
        <button
          type="button"
          data-testid="cross-entity-filters-more"
          onClick={onMoreFilters}
          className={clsx(
            'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
            activeFiltersCount > 0
              ? 'border-primary bg-primary/10 text-foreground'
              : 'border-border bg-background text-muted-foreground hover:text-foreground'
          )}
        >
          {t('pages.library.filters.title')}
          {activeFiltersCount > 0 ? ` (${activeFiltersCount})` : ''}
        </button>
      ) : null}
    </div>
  );
}
```

The chip uses the existing i18n key `pages.library.filters.title` (= "Più filtri"), already present.

- [ ] **Step 2: Re-export `AdvancedFiltersDrawer` from the library barrel**

In `apps/web/src/components/features/library/index.ts`, find the existing export block and add:

```typescript
export { AdvancedFiltersDrawer } from './AdvancedFiltersDrawer';
export type { AdvancedFiltersDrawerProps, LibraryFilters } from './AdvancedFiltersDrawer/types';
```

(Use Read on the barrel file first to see where to slot the new export — keep alphabetical/categorical grouping consistent with existing patterns.)

- [ ] **Step 3: Update existing `CrossEntityFilters.test.tsx` (if any) + add new tests**

Check if a test file exists:
```bash
ls apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx 2>&1
```

If it exists:
- Existing tests that mounted on `'games'` tab and asserted STATO chips render — still pass (no regression, STATO chip block unchanged).
- Add a new test for the "Più filtri" chip:

```typescript
it('renders "Più filtri" chip on every tab except all when onMoreFilters is provided', () => {
  const onMoreFilters = vi.fn();
  // On 'games' tab: STATO + Più filtri
  const { rerender } = renderWithIntl(
    <CrossEntityFilters
      tab="games"
      gameStateFilter={{ states: [], withKb: false }}
      onGameStateFilterChange={vi.fn()}
      onMoreFilters={onMoreFilters}
    />
  );
  expect(screen.getByTestId('cross-entity-filters-more')).toBeInTheDocument();

  // On 'agents' tab: only Più filtri (no STATO chips)
  rerender(
    <CrossEntityFilters
      tab="agents"
      gameStateFilter={{ states: [], withKb: false }}
      onGameStateFilterChange={vi.fn()}
      onMoreFilters={onMoreFilters}
    />
  );
  expect(screen.getByTestId('cross-entity-filters-more')).toBeInTheDocument();

  // On 'all' tab: drawer chip hidden (no entity-specific scope makes sense)
  rerender(
    <CrossEntityFilters
      tab="all"
      gameStateFilter={{ states: [], withKb: false }}
      onGameStateFilterChange={vi.fn()}
      onMoreFilters={onMoreFilters}
    />
  );
  expect(screen.queryByTestId('cross-entity-filters-more')).toBeNull();
});

it('invokes onMoreFilters when chip is clicked', () => {
  const onMoreFilters = vi.fn();
  renderWithIntl(
    <CrossEntityFilters
      tab="games"
      gameStateFilter={{ states: [], withKb: false }}
      onGameStateFilterChange={vi.fn()}
      onMoreFilters={onMoreFilters}
    />
  );
  fireEvent.click(screen.getByTestId('cross-entity-filters-more'));
  expect(onMoreFilters).toHaveBeenCalledTimes(1);
});

it('displays activeFiltersCount badge when > 0', () => {
  renderWithIntl(
    <CrossEntityFilters
      tab="games"
      gameStateFilter={{ states: [], withKb: false }}
      onGameStateFilterChange={vi.fn()}
      onMoreFilters={vi.fn()}
      activeFiltersCount={3}
    />
  );
  expect(screen.getByTestId('cross-entity-filters-more')).toHaveTextContent(/Più filtri \(3\)/);
});
```

If the test file does NOT exist, create it with a minimal smoke test + the 3 new tests.

- [ ] **Step 4: Run tests + typecheck**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/components/features/library/__tests__/CrossEntityFilters.test.tsx" 2>&1 | tail -10
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -6
```
Expected: tests PASS, typecheck 0 errors.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/components/features/library/CrossEntityFilters.tsx apps/web/src/components/features/library/index.ts apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx
git -c commit.gpgsign=false commit -m "feat(library): #1593 add Più filtri chip + drawer barrel export

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 9: `LibraryHub` integration — switch hook, mount drawer, wire chip

**Goal**: The big wiring step. Switch `useLibraryActivity` → new `useActivityFeed`; pass `isLoading` + `error` to rail; mount `<AdvancedFiltersDrawer>` with `drawerOpen` + `activeFilters` state; wire "Più filtri" chip.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx`

- [ ] **Step 1: Read the current LibraryHub state + relevant blocks**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
sed -n '14,60p' apps/web/src/app/\(authenticated\)/library/_components/LibraryHub.tsx
sed -n '85,115p' apps/web/src/app/\(authenticated\)/library/_components/LibraryHub.tsx
sed -n '135,215p' apps/web/src/app/\(authenticated\)/library/_components/LibraryHub.tsx
sed -n '475,495p' apps/web/src/app/\(authenticated\)/library/_components/LibraryHub.tsx
sed -n '555,565p' apps/web/src/app/\(authenticated\)/library/_components/LibraryHub.tsx
```

Identify the EXACT current shape of: imports block (line 14-60), state declarations (line 85-115), `useLibraryActivity` + `activityItems` useMemo (line 135-215), `<CrossEntityFilters>` mount (line 475-490), `<RecentActivityRail>` mount (line 555-560).

- [ ] **Step 2: Update imports**

Replace the import line `useLibraryActivity` (line 55) with:
```typescript
// Removed: useLibraryActivity (legacy library-only feed, replaced by useActivityFeed Phase 3b #1593)
```
And add (alongside the other hook imports):
```typescript
import { useActivityFeed } from '@/hooks/useActivityFeed';
```

Add the drawer import (alongside other library components imports, near the `RecentActivityRail` import):
```typescript
import { AdvancedFiltersDrawer, type LibraryFilters } from '@/components/features/library';
```

Or, if the barrel doesn't yet export it, do a direct import:
```typescript
import { AdvancedFiltersDrawer } from '@/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer';
import type { LibraryFilters } from '@/components/features/library/AdvancedFiltersDrawer/types';
```

(Task 8 added the barrel re-export, so use the barrel import.)

Also import the entity-scope mapper:
```typescript
import type { HybridHubEntity } from '@/lib/library/hybrid-hub.types';
```

- [ ] **Step 3: Add new state + helper for drawer**

Inside the `LibraryHub` component body, after the existing `useState` declarations (around line 106), add:

```typescript
// Phase 3b #1593: AdvancedFiltersDrawer state
const [drawerOpen, setDrawerOpen] = useState(false);
const [activeFilters, setActiveFilters] = useState<LibraryFilters>({ scope: 'game' });

// Tab → drawer entity-scope mapping (R4). 'all' tab disables the drawer chip.
const drawerEntityScope: HybridHubEntity = useMemo(() => {
  switch (tab) {
    case 'games': return 'game';
    case 'agents': return 'agent';
    case 'kb': return 'kb';
    case 'sessions': return 'session';
    case 'chat': return 'chat';
    case 'all':
    default: return 'game';
  }
}, [tab]);

// When the tab changes, reset activeFilters to the new scope's empty variant.
// Avoids carrying agent filters into a kb tab (per-tab semantics).
useEffect(() => {
  setActiveFilters({ scope: drawerEntityScope } as LibraryFilters);
}, [drawerEntityScope]);

// Active count badge: number of non-empty fields in activeFilters (excluding `scope`).
const activeFiltersCount = useMemo(() => {
  let count = 0;
  for (const [key, value] of Object.entries(activeFilters)) {
    if (key === 'scope') continue;
    if (value === undefined || value === null) continue;
    if (Array.isArray(value) && value.length === 0) continue;
    if (value === false) continue;
    count++;
  }
  return count;
}, [activeFilters]);
```

- [ ] **Step 4: Replace the activity query**

Replace line 140:
```typescript
const activityQuery = useLibraryActivity(20);
```
with:
```typescript
const activityQuery = useActivityFeed(20);
```

Replace the `activityItems` useMemo block (lines 179-210) with a much simpler version (the adapter is now applied inside the hook):

```typescript
const activityItems = useMemo<readonly ActivityItem[]>(
  () => activityQuery.data?.items ?? [],
  [activityQuery.data]
);
```

- [ ] **Step 5: Wire `<CrossEntityFilters>` and mount the drawer**

Update the `<CrossEntityFilters ... />` JSX (around line 477-481) to pass the new props:

```typescript
<CrossEntityFilters
  tab={tab}
  gameStateFilter={gameStateFilter}
  onGameStateFilterChange={setGameStateFilter}
  onMoreFilters={() => setDrawerOpen(true)}
  activeFiltersCount={activeFiltersCount}
/>
```

Update the `<RecentActivityRail items={activityItems} />` (line 558) to forward `isLoading` + `error`:

```typescript
<RecentActivityRail
  items={activityItems}
  isLoading={activityQuery.isLoading}
  error={activityQuery.error}
/>
```

Mount the drawer at the end of the JSX (just before the closing tag of the root `<div data-slot="library-hub-v2">` — outside the rail/grid layout, since it's a modal overlay):

```typescript
<AdvancedFiltersDrawer
  open={drawerOpen}
  onOpenChange={setDrawerOpen}
  entityScope={drawerEntityScope}
  activeFilters={activeFilters}
  onApply={setActiveFilters}
  onClear={() => setActiveFilters({ scope: drawerEntityScope } as LibraryFilters)}
/>
```

- [ ] **Step 6: Typecheck**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -8
```
Expected: 0 errors.

- [ ] **Step 7: Run the LibraryHub test file (it WILL FAIL — Task 10 fixes it)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx" 2>&1 | tail -15
```
Expected: SOME failures (the mock for `useLibraryActivity` is now unused; the rail items mapping expectations changed; drawer state added). DON'T fix here — Task 10 handles it.

- [ ] **Step 8: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add "apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx"
git -c commit.gpgsign=false commit -m "feat(library): #1593 switch rail to useActivityFeed + mount drawer

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 10: Update `LibraryHub.test.tsx` for the new hook + drawer

**Goal**: Update the existing 28+ tests to mock `useActivityFeed` (not `useLibraryActivity`), add tests for drawer open/close + "Più filtri" chip + isLoading/error forwarding.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx`

- [ ] **Step 1: Read the existing test file** + identify the mock pattern for `useLibraryActivity`

```bash
cd /d/Repositories/meepleai-monorepo-frontend
grep -n "useLibraryActivity\|useActivityFeed\|RecentActivityRail\|drawer\|AdvancedFiltersDrawer" apps/web/src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx | head -30
```

Identify the lines that:
- Mock `useLibraryActivity` (will be replaced).
- Set up `useLibraryActivityMock` returns.
- Assert on `data-slot="library-activity-rail"` presence.

- [ ] **Step 2: Replace `useLibraryActivity` mock with `useActivityFeed`**

Find the mock declaration block (likely near the top of the file, around line 99-117) and replace:
```typescript
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: useLibraryMock,
  useLibraryActivity: useLibraryActivityMock,
  // ...
}));
```
with:
```typescript
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: useLibraryMock,
  // (useLibraryActivity removed — replaced by useActivityFeed in Phase 3b #1593)
  // ...
}));

vi.mock('@/hooks/useActivityFeed', () => ({
  useActivityFeed: vi.fn(),
}));

import { useActivityFeed } from '@/hooks/useActivityFeed';
```

Find the `beforeEach` block and add a default mock for `useActivityFeed`:
```typescript
vi.mocked(useActivityFeed).mockReturnValue({
  data: { items: [], count: 0 },
  isLoading: false,
  isSuccess: true,
  isError: false,
  error: null,
} as any);
```

Remove the `useLibraryActivityMock` declaration + default setup (no longer needed).

- [ ] **Step 3: Update existing tests that asserted on activity items**

Any test that previously used `useLibraryActivityMock.mockReturnValue(...)` to inject library activity items must now use `vi.mocked(useActivityFeed).mockReturnValue(...)` with the rail's `ActivityItem[]` shape (kind/entityTitle/timestamp).

If a test was previously asserting that a specific activity item renders in the rail, update the mock data:
```typescript
vi.mocked(useActivityFeed).mockReturnValue({
  data: {
    items: [
      { id: 'item-1', kind: 'agent', entityTitle: 'Catan Tutor', timestamp: '2026-05-28T11:00:00+00:00' },
    ],
    count: 1,
  },
  isLoading: false,
  isSuccess: true,
  isError: false,
  error: null,
} as any);
```

- [ ] **Step 4: Add new tests for drawer + chip + forwarding**

```typescript
describe('Phase 3b — AdvancedFiltersDrawer integration', () => {
  it('opens drawer when Più filtri chip is clicked', async () => {
    const user = userEvent.setup();
    renderWithIntl(<LibraryHub />);

    // Initial state: drawer closed (no role="dialog")
    expect(screen.queryByRole('dialog')).toBeNull();

    // Click Più filtri chip
    await user.click(screen.getByTestId('cross-entity-filters-more'));

    // Drawer is open
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('does not show Più filtri chip on the all tab', () => {
    renderWithIntl(<LibraryHub />);
    // Default tab is 'all'
    expect(screen.queryByTestId('cross-entity-filters-more')).toBeNull();
  });

  it('shows Più filtri chip on the games tab', async () => {
    const user = userEvent.setup();
    renderWithIntl(<LibraryHub />);
    await user.click(screen.getByRole('tab', { name: /giochi/i }));
    expect(screen.getByTestId('cross-entity-filters-more')).toBeInTheDocument();
  });
});

describe('Phase 3b — RecentActivityRail forwarding', () => {
  it('forwards isLoading from useActivityFeed', () => {
    vi.mocked(useActivityFeed).mockReturnValue({
      data: undefined,
      isLoading: true,
      isSuccess: false,
      isError: false,
      error: null,
    } as any);
    renderWithIntl(<LibraryHub />);
    expect(screen.getAllByTestId('library-activity-skeleton').length).toBeGreaterThan(0);
  });

  it('forwards error from useActivityFeed', () => {
    vi.mocked(useActivityFeed).mockReturnValue({
      data: undefined,
      isLoading: false,
      isSuccess: false,
      isError: true,
      error: new Error('500'),
    } as any);
    renderWithIntl(<LibraryHub />);
    expect(screen.getByTestId('library-activity-error')).toBeInTheDocument();
  });

  it('renders activity items from useActivityFeed (cross-entity)', () => {
    vi.mocked(useActivityFeed).mockReturnValue({
      data: {
        items: [
          { id: '1', kind: 'agent', entityTitle: 'Catan Tutor', timestamp: '2026-05-28T11:00:00+00:00' },
          { id: '2', kind: 'kb-indexed', entityTitle: 'rules.pdf', timestamp: '2026-05-28T10:00:00+00:00' },
        ],
        count: 2,
      },
      isLoading: false,
      isSuccess: true,
      isError: false,
      error: null,
    } as any);
    renderWithIntl(<LibraryHub />);
    expect(screen.getByText('Catan Tutor')).toBeInTheDocument();
    expect(screen.getByText('rules.pdf')).toBeInTheDocument();
  });
});
```

(Adapt selectors — `screen.getByRole('tab', { name: ... })` and `data-testid` references — to the project's actual test conventions discovered in Step 1.)

- [ ] **Step 5: Run tests to verify GREEN**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run "src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx" 2>&1 | tail -15
```
Expected: ALL tests PASS (existing 28 + ~5 new).

- [ ] **Step 6: Final full-project regression sanity check**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -4
pnpm vitest run --testNamePattern "ActivityFeed|RecentActivityRail|LibraryHub|CrossEntityFilters|activity-adapter|activity.schemas|useActivityFeed|useDashboardActivityFeed" 2>&1 | tail -10
```
Expected: typecheck 0 errors; all related tests PASS.

- [ ] **Step 7: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add "apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx"
git -c commit.gpgsign=false commit -m "test(library): #1593 update LibraryHub tests for Phase 3b (rail + drawer)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Final acceptance check

After all 10 tasks complete, run:

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck && pnpm lint && pnpm vitest run --testNamePattern "Activity|RecentActivityRail|LibraryHub|CrossEntityFilters|adapter|schema" 2>&1 | tail -20
```

Expected: typecheck 0 errors; lint clean; all related tests PASS.

| AC | Verified by |
|---|---|
| AC3.b.1 (rail consumes cross-entity feed) | Tasks 2-7 (schema/client/adapter/hook) + Task 9 (LibraryHub switch) |
| AC3.b.2 (cold-start empty state via i18n) | Task 6 (i18n connect) |
| AC3.b.3 (forward-only documented) | No code — BE-3 AC3 inherited; FE messaging done in Task 6 |
| AC3.b.4 (drawer wire-in) | Task 8 (chip) + Task 9 (mount + state) |
| AC3.b.5 (E1 rename) | Task 1 |
| AC3.b.6 (i18n connect rail) | Task 6 |
| AC3.b.7 (isLoading forward) | Task 9 (rail prop forwarding) + Task 10 (test) |
| AC3.b.8 (error state R4) | Task 6 (component) + Task 9 (forward) + Task 10 (test) |

---

## Out of scope (follow-ups)

- **#1645 BE enhancement**: explicit `updatedAt` in `UserKbDocDto` (already filed from Phase 2b K1.3).
- **D-mockup polish** (#1488-1492): final visual refinement of the LibraryHub layout (separate Wave).
- **Drawer per-tab filter persistence** (e.g. `gameStateFilter ↔ activeFilters` two-way sync): kept simple in Task 9 (`useEffect` resets activeFilters on tab change to the new scope's empty variant). A future enhancement could persist filters across tab switches via a URL query param or localStorage — out of scope here.
- **Selection-mode for agents/kb tabs**: remains game-scoped per Phase 2a AC9.
