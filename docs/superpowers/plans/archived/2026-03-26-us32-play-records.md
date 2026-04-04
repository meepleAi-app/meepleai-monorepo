# US-32: Play Records Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate play record tracking, allowing users to log game plays, add players, record multi-dimensional scores, view history, and see statistics.

**Architecture:** Frontend-only activation. Pages already exist at `/play-records/*` but are alpha-gated. Wire API client, activate navigation, add "Log Play" entry points from game detail and library pages.

**Tech Stack:** Next.js 16, React 19, Zustand, Tailwind 4, shadcn/ui

---

## File Structure

### Files to Modify
- `apps/web/src/config/navigation.ts` — add `'play-records'` to `ALPHA_NAV_IDS`
- `apps/web/src/proxy.ts` — add `/play-records` to alpha route prefixes

### Files to Verify (already exist)
- `apps/web/src/app/(authenticated)/play-records/page.tsx` — list view
- `apps/web/src/app/(authenticated)/play-records/new/page.tsx` — create form
- `apps/web/src/app/(authenticated)/play-records/[id]/page.tsx` — detail
- `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx` — edit
- `apps/web/src/app/(authenticated)/play-records/stats/page.tsx` — statistics

### Files to Create (if missing)
- `apps/web/src/lib/api/clients/playRecordsClient.ts` — API client

---

### Task 1: Remove Alpha Mode Gating

**Files:**
- Modify: `apps/web/src/config/navigation.ts`
- Modify: `apps/web/src/proxy.ts`

- [ ] **Step 1: Read navigation.ts and find ALPHA_NAV_IDS**

- [ ] **Step 2: Add play-records to ALPHA_NAV_IDS**

Add `'play-records'` to the Set.

- [ ] **Step 3: Read proxy.ts and find alpha route prefixes**

- [ ] **Step 4: Add /play-records to alpha route prefixes**

- [ ] **Step 5: Verify play-records nav item exists in _ALL_NAV_ITEMS**

If missing, add:

```typescript
{
  id: 'play-records',
  href: '/play-records',
  icon: ClipboardList,
  iconName: 'clipboard-list',
  label: 'Partite',
  ariaLabel: 'Navigate to play records',
  priority: 6,
  testId: 'nav-play-records',
  visibility: { authOnly: true },
}
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/config/navigation.ts apps/web/src/proxy.ts
git commit -m "feat(play-records): remove alpha mode gating for play records"
```

---

### Task 2: Verify/Create Play Records API Client

**Files:**
- Verify/Create: `apps/web/src/lib/api/clients/playRecordsClient.ts`

- [ ] **Step 1: Search for existing play records client**

Search for `playRecordsClient` or `createPlayRecordsClient` in `apps/web/src/lib/api/`.

- [ ] **Step 2: If missing, create the client**

```typescript
import type { HttpClient } from '../core/httpClient';

export interface CreatePlayRecordRequest {
  gameId?: string;
  gameName: string;
  sessionDate: string;
  visibility: 'Public' | 'Private' | 'Group';
  groupId?: string;
  scoringDimensions?: string[];
  dimensionUnits?: Record<string, string>;
}

export interface AddPlayerRequest {
  userId?: string;
  displayName: string;
}

export interface RecordScoreRequest {
  playerId: string;
  dimension: string;
  value: number;
  unit?: string;
}

export function createPlayRecordsClient({ httpClient }: { httpClient: HttpClient }) {
  const base = '/api/v1/game-management/play-records';

  return {
    async create(data: CreatePlayRecordRequest) {
      return httpClient.post<string>(`${base}`, data);
    },
    async addPlayer(recordId: string, data: AddPlayerRequest) {
      return httpClient.post(`${base}/${recordId}/players`, data);
    },
    async recordScore(recordId: string, data: RecordScoreRequest) {
      return httpClient.post(`${base}/${recordId}/scores`, data);
    },
    async start(recordId: string) {
      return httpClient.post(`${base}/${recordId}/start`, {});
    },
    async complete(recordId: string, manualDuration?: string) {
      return httpClient.post(`${base}/${recordId}/complete`, { manualDuration });
    },
    async update(recordId: string, data: { sessionDate?: string; notes?: string; location?: string }) {
      return httpClient.put(`${base}/${recordId}`, data);
    },
    async getById(recordId: string) {
      return httpClient.get(`${base}/${recordId}`);
    },
    async getHistory(params?: { page?: number; pageSize?: number; gameId?: string }) {
      const qs = new URLSearchParams();
      if (params?.page) qs.append('page', params.page.toString());
      if (params?.pageSize) qs.append('pageSize', params.pageSize.toString());
      if (params?.gameId) qs.append('gameId', params.gameId);
      return httpClient.get(`${base}/history?${qs}`);
    },
    async getStatistics(params?: { startDate?: string; endDate?: string }) {
      const qs = new URLSearchParams();
      if (params?.startDate) qs.append('startDate', params.startDate);
      if (params?.endDate) qs.append('endDate', params.endDate);
      return httpClient.get(`${base}/statistics?${qs}`);
    },
  };
}
```

- [ ] **Step 3: Register in API barrel export**

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(play-records): create play records API client"
```

---

### Task 3: Verify Existing Pages Render

**Files:**
- Verify: All 5 play-records pages

- [ ] **Step 1: Start dev server and navigate to /play-records**

Verify the list page renders (empty state or play records).

- [ ] **Step 2: Navigate to /play-records/new**

Verify the create form renders with game selection, date picker, player fields.

- [ ] **Step 3: Navigate to /play-records/stats**

Verify statistics page renders (empty state or charts).

- [ ] **Step 4: Document any rendering issues**

Note errors for fixing in subsequent steps.

---

### Task 4: Add "Log Play" Entry Points

**Files:**
- Modify: Game detail page actions
- Modify: Library game detail page

- [ ] **Step 1: Find game action buttons**

Search for action buttons in game detail pages (library and shared).

- [ ] **Step 2: Add "Log Play" button**

```tsx
<Button variant="outline" asChild>
  <Link href={`/play-records/new?gameId=${game.id}&gameName=${encodeURIComponent(game.title)}`}>
    <ClipboardList className="mr-2 h-4 w-4" />
    Log Play
  </Link>
</Button>
```

- [ ] **Step 3: Verify /play-records/new reads query params**

Confirm the create page pre-fills game info from `gameId` and `gameName` query params.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/
git commit -m "feat(play-records): add Log Play entry points from game detail"
```

---

### Task 5: Write Tests

**Files:**
- Test: `apps/web/src/__tests__/play-records/`

- [ ] **Step 1: Check existing play-records tests**

- [ ] **Step 2: Run existing tests**

```bash
cd apps/web && pnpm test -- --grep "play-record" --run
```

- [ ] **Step 3: Write play records list page test**

Test that the page renders, shows empty state, has "New Play" button.

- [ ] **Step 4: Run all tests**

```bash
cd apps/web && pnpm test --run
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/__tests__/
git commit -m "test(play-records): add play records page tests"
```

---

### Task 6: Quality Checks and PR

- [ ] **Step 1: Run typecheck, lint, tests**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run
```

- [ ] **Step 2: Fix any issues**

- [ ] **Step 3: Push and create PR**

```bash
git push -u origin feature/us32-play-records
gh pr create --base frontend-dev --title "feat(play-records): activate play record tracking (US-32)" --body "## Summary
- Remove alpha mode gating for /play-records routes
- Create play records API client
- Add Log Play entry points from game detail pages
- Verify all existing play record pages render

## User Story
US-32: Come utente, voglio tracciare le partite giocate

## Test Plan
- [ ] /play-records list renders
- [ ] /play-records/new creates a play record
- [ ] /play-records/stats shows statistics
- [ ] Log Play button on game detail works"
```

---

## Backend Endpoints Reference

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/game-management/play-records` | Create play record |
| POST | `/api/v1/game-management/play-records/{id}/players` | Add player |
| POST | `/api/v1/game-management/play-records/{id}/scores` | Record score |
| POST | `/api/v1/game-management/play-records/{id}/start` | Start record |
| POST | `/api/v1/game-management/play-records/{id}/complete` | Complete record |
| PUT | `/api/v1/game-management/play-records/{id}` | Update details |
| GET | `/api/v1/game-management/play-records/{id}` | Get by ID |
| GET | `/api/v1/game-management/play-records/history` | Paginated history |
| GET | `/api/v1/game-management/play-records/statistics` | Player statistics |
