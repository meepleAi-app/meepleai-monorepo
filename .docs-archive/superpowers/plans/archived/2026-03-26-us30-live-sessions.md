# US-30: Live Game Sessions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate the live game session system allowing users to create, join, play, score, and share game sessions with real-time SSE updates.

**Architecture:** Frontend-only activation. All 11+ session pages, Zustand store, SSE hooks, and components already exist but are hidden behind alpha mode. Need to remove alpha gating, verify API wiring, and add entry points from game detail and library.

**Tech Stack:** Next.js 16, React 19, Zustand, SSE (EventSource), Tailwind 4, shadcn/ui

---

## File Structure

### Files to Modify
- `apps/web/src/config/navigation.ts` — add sessions to `ALPHA_NAV_IDS`
- `apps/web/src/middleware.ts` or `apps/web/src/proxy.ts` — add `/sessions` to alpha route prefixes
- `apps/web/src/components/game-detail/GameDetailActions.tsx` (or equivalent) — add "Start Session" button

### Files to Verify (already exist)
- `apps/web/src/app/(authenticated)/sessions/page.tsx` — sessions hub
- `apps/web/src/app/(authenticated)/sessions/new/page.tsx` — create session
- `apps/web/src/app/(authenticated)/sessions/join/page.tsx` — join session
- `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx` — live session
- `apps/web/src/lib/api/clients/sessionsClient.ts` — API client (verify exists)
- `apps/web/src/stores/sessionStore.ts` — Zustand store (verify exists)

### Files to Create (if missing)
- `apps/web/src/lib/api/clients/sessionsClient.ts` — API client for session endpoints
- `apps/web/src/__tests__/sessions/sessions-page.test.tsx` — page-level tests

---

### Task 1: Remove Alpha Mode Gating for Sessions

**Files:**
- Modify: `apps/web/src/config/navigation.ts`
- Modify: `apps/web/src/proxy.ts` (or wherever alpha route prefixes are defined)

- [ ] **Step 1: Read current navigation config**

Read `apps/web/src/config/navigation.ts` and find `ALPHA_NAV_IDS`.

- [ ] **Step 2: Add sessions to ALPHA_NAV_IDS**

Add `'sessions'` to the `ALPHA_NAV_IDS` Set so the nav item appears in alpha mode.

- [ ] **Step 3: Read proxy.ts for alpha route prefixes**

Read `apps/web/src/proxy.ts` and find `ALPHA_ROUTE_PREFIXES` or equivalent.

- [ ] **Step 4: Add /sessions to alpha route prefixes**

Add `'/sessions'` to the allowed route prefixes so the middleware doesn't redirect.

- [ ] **Step 5: Verify sessions nav item exists in _ALL_NAV_ITEMS**

Confirm there is a nav item with `id: 'sessions'` and correct href/icon. If missing, add:

```typescript
{
  id: 'sessions',
  href: '/sessions',
  icon: Gamepad2,
  iconName: 'gamepad-2',
  label: 'Sessioni',
  ariaLabel: 'Navigate to game sessions',
  priority: 5,
  testId: 'nav-sessions',
  visibility: { authOnly: true },
}
```

- [ ] **Step 6: Test navigation renders**

Run: `cd apps/web && pnpm dev`
Verify: Sessions appears in the sidebar navigation.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/config/navigation.ts apps/web/src/proxy.ts
git commit -m "feat(sessions): remove alpha mode gating for sessions routes"
```

---

### Task 2: Verify/Create Sessions API Client

**Files:**
- Verify: `apps/web/src/lib/api/clients/sessionsClient.ts`
- Verify: `apps/web/src/lib/api/schemas/sessions.schemas.ts`

- [ ] **Step 1: Check if sessions API client exists**

Search for `sessionsClient` or `createSessionsClient` in `apps/web/src/lib/api/`.

- [ ] **Step 2: If missing, create sessions API client**

Create `apps/web/src/lib/api/clients/sessionsClient.ts` with methods mapping to backend endpoints:

```typescript
export function createSessionsClient({ httpClient }: { httpClient: HttpClient }) {
  return {
    async create(data: CreateSessionRequest) {
      return httpClient.post<CreateSessionResult>('/api/v1/game-sessions', data);
    },
    async getActive() {
      return httpClient.get<SessionDetailsDto>('/api/v1/game-sessions/active');
    },
    async getById(sessionId: string) {
      return httpClient.get<SessionDetailsDto>(`/api/v1/game-sessions/${sessionId}`);
    },
    async getByCode(code: string) {
      return httpClient.get<SessionDetailsDto>(`/api/v1/game-sessions/code/${code}`);
    },
    async joinByCode(code: string, displayName: string) {
      return httpClient.post<JoinSessionResult>(`/api/v1/game-sessions/code/${code}/join`, { displayName });
    },
    async getHistory(params?: { gameId?: string; limit?: number; offset?: number }) {
      const qs = new URLSearchParams();
      if (params?.gameId) qs.append('gameId', params.gameId);
      if (params?.limit) qs.append('limit', params.limit.toString());
      if (params?.offset) qs.append('offset', params.offset.toString());
      return httpClient.get<PaginatedSessions>(`/api/v1/game-sessions/history?${qs}`);
    },
    async getScoreboard(sessionId: string) {
      return httpClient.get<ScoreboardDto>(`/api/v1/game-sessions/${sessionId}/scoreboard`);
    },
    async updateScore(sessionId: string, data: UpdateScoreRequest) {
      return httpClient.put<UpdateScoreResult>(`/api/v1/game-sessions/${sessionId}/scores`, data);
    },
    async finalize(sessionId: string) {
      return httpClient.post(`/api/v1/game-sessions/${sessionId}/finalize`, {});
    },
    async rollDice(sessionId: string, formula: string, label?: string) {
      return httpClient.post<DiceRollResult>(`/api/v1/game-sessions/${sessionId}/dice`, { formula, label });
    },
    async getStatistics(monthsBack = 6) {
      return httpClient.get<SessionStatisticsDto>(`/api/v1/game-sessions/session-statistics?monthsBack=${monthsBack}`);
    },
    async generateInvite(sessionId: string) {
      return httpClient.post<InviteTokenResponse>(`/api/v1/game-sessions/${sessionId}/generate-invite`, {});
    },
  };
}
```

- [ ] **Step 3: Register client in API barrel export**

Add to `apps/web/src/lib/api/client.ts` or `apps/web/src/lib/api/index.ts`.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(sessions): verify/create sessions API client"
```

---

### Task 3: Verify Sessions Hub Page Works

**Files:**
- Verify: `apps/web/src/app/(authenticated)/sessions/page.tsx`

- [ ] **Step 1: Read the sessions hub page**

Read the page to understand current implementation (active sessions + history tabs).

- [ ] **Step 2: Navigate to /sessions in dev mode**

Start dev server and navigate to `/sessions`. Verify:
- Page renders without errors
- Active session tab shows (or empty state)
- History tab shows past sessions (or empty state)
- "New Session" button is accessible

- [ ] **Step 3: Verify /sessions/new renders**

Navigate to `/sessions/new`. Verify the create session form loads.

- [ ] **Step 4: Verify /sessions/join renders**

Navigate to `/sessions/join`. Verify the join-by-code form loads.

- [ ] **Step 5: Document any issues found**

If any pages fail to render, note the errors for fix in subsequent tasks.

---

### Task 4: Verify SSE Real-Time Streaming

**Files:**
- Verify: `apps/web/src/hooks/useSessionSync.ts` (or similar SSE hook)
- Verify: `apps/web/src/stores/sessionStore.ts`

- [ ] **Step 1: Read the SSE hook implementation**

Find and read the session sync hook that connects to `/api/v1/game-sessions/{sessionId}/stream/v2`.

- [ ] **Step 2: Verify EventSource URL points to correct endpoint**

Confirm the hook uses the v2 SSE endpoint with Last-Event-ID support.

- [ ] **Step 3: Verify Zustand store handles SSE events**

Read the session store and confirm it handles events like `scoreboard:updated`, `participants:joined`.

- [ ] **Step 4: Test with a session (if API is running)**

If the backend is running, create a test session and verify SSE events flow to the frontend.

- [ ] **Step 5: Commit any fixes**

```bash
git add -A && git commit -m "fix(sessions): verify SSE streaming integration"
```

---

### Task 5: Add "Start Session" Entry Points

**Files:**
- Modify: Game detail page actions component
- Modify: Library game detail page

- [ ] **Step 1: Find game detail action buttons**

Search for "Start Session" or "New Session" buttons in game detail or library game pages.

- [ ] **Step 2: Verify "Start Session" button links to /sessions/new**

Ensure the button passes `gameId` as a query parameter: `/sessions/new?gameId={id}`.

- [ ] **Step 3: If missing, add "Start Session" button to game detail**

Add a button in the game detail actions area:

```tsx
<Button variant="outline" asChild>
  <Link href={`/sessions/new?gameId=${game.id}`}>
    <Gamepad2 className="mr-2 h-4 w-4" />
    Start Session
  </Link>
</Button>
```

- [ ] **Step 4: Verify /sessions/new reads gameId from query params**

Check that the create session page pre-selects the game when `gameId` is in the URL.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/
git commit -m "feat(sessions): add Start Session entry points from game detail"
```

---

### Task 6: Write/Verify Page Tests

**Files:**
- Test: `apps/web/src/__tests__/sessions/`

- [ ] **Step 1: Check existing session tests**

Search for existing test files under sessions.

- [ ] **Step 2: Verify existing tests pass**

```bash
cd apps/web && pnpm test -- --grep "session" --run
```

- [ ] **Step 3: Write sessions hub page test (if missing)**

```typescript
import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn() })),
  usePathname: vi.fn(() => '/sessions'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

describe('Sessions Hub Page', () => {
  it('renders sessions page title', () => {
    // renderWithQuery(<SessionsPage />);
    // expect(screen.getByText(/sessions/i)).toBeInTheDocument();
  });

  it('shows new session button', () => {
    // renderWithQuery(<SessionsPage />);
    // expect(screen.getByRole('link', { name: /new session/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 4: Run all tests**

```bash
cd apps/web && pnpm test --run
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/__tests__/
git commit -m "test(sessions): add sessions hub page tests"
```

---

### Task 7: Quality Checks and PR

- [ ] **Step 1: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 2: Run lint**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 3: Run all tests**

```bash
cd apps/web && pnpm test --run
```

- [ ] **Step 4: Fix any issues**

Address typecheck, lint, or test failures.

- [ ] **Step 5: Final commit and push**

```bash
git push -u origin feature/us30-live-sessions
```

- [ ] **Step 6: Create PR to frontend-dev**

```bash
gh pr create --base frontend-dev --title "feat(sessions): activate live game sessions (US-30)" --body "## Summary
- Remove alpha mode gating for /sessions routes
- Verify sessions API client and SSE streaming
- Add Start Session entry points from game detail
- Verify all existing session pages render correctly

## User Story
US-30: Come utente, voglio creare e gestire sessioni di gioco live

## Test Plan
- [ ] /sessions hub renders with active + history tabs
- [ ] /sessions/new creates a session
- [ ] /sessions/join accepts session codes
- [ ] SSE real-time updates work
- [ ] Scoreboard displays correctly
- [ ] Start Session button on game detail works"
```

---

## Backend Endpoints Reference

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/game-sessions` | Create session |
| GET | `/api/v1/game-sessions/active` | Get active session |
| GET | `/api/v1/game-sessions/{id}` | Get session details |
| GET | `/api/v1/game-sessions/code/{code}` | Get by join code |
| POST | `/api/v1/game-sessions/code/{code}/join` | Join by code |
| POST | `/api/v1/game-sessions/{id}/finalize` | End session |
| PUT | `/api/v1/game-sessions/{id}/scores` | Update score |
| GET | `/api/v1/game-sessions/{id}/scoreboard` | Get scoreboard |
| GET | `/api/v1/game-sessions/{id}/stream/v2` | SSE events |
| POST | `/api/v1/game-sessions/{id}/dice` | Roll dice |
| POST | `/api/v1/game-sessions/{id}/generate-invite` | Generate invite (QR) |
| GET | `/api/v1/game-sessions/history` | Session history |
| GET | `/api/v1/game-sessions/session-statistics` | Statistics |
| GET | `/api/v1/game-sessions/{id}/export/pdf` | PDF export |
