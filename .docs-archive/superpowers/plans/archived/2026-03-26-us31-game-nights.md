# US-31: Game Nights Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate game night planning, allowing users to create events, invite friends, RSVP, and manage game night logistics.

**Architecture:** Frontend-only activation. Pages exist at `/game-nights/*` but are alpha-gated. Wire API client, activate navigation, ensure RSVP flow works end-to-end.

**Tech Stack:** Next.js 16, React 19, Zustand, Tailwind 4, shadcn/ui

---

## File Structure

### Files to Modify
- `apps/web/src/config/navigation.ts` — add `'game-nights'` to `ALPHA_NAV_IDS`
- `apps/web/src/proxy.ts` — add `/game-nights` to alpha route prefixes

### Files to Verify (already exist)
- `apps/web/src/app/(authenticated)/game-nights/page.tsx` — list upcoming nights
- `apps/web/src/app/(authenticated)/game-nights/new/page.tsx` — create event
- `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx` — event detail + RSVP
- `apps/web/src/app/(authenticated)/game-nights/[id]/edit/page.tsx` — edit event
- `apps/web/src/app/(authenticated)/game-nights/_components/GameNightForm.tsx` — reusable form

### Files to Create (if missing)
- `apps/web/src/lib/api/clients/gameNightsClient.ts` — API client

---

### Task 1: Remove Alpha Mode Gating

**Files:**
- Modify: `apps/web/src/config/navigation.ts`
- Modify: `apps/web/src/proxy.ts`

- [ ] **Step 1: Read navigation.ts and find ALPHA_NAV_IDS**

- [ ] **Step 2: Add game-nights to ALPHA_NAV_IDS**

Add `'game-nights'` to the Set.

- [ ] **Step 3: Add /game-nights to alpha route prefixes in proxy.ts**

- [ ] **Step 4: Verify game-nights nav item exists**

If missing, add:

```typescript
{
  id: 'game-nights',
  href: '/game-nights',
  icon: Calendar,
  iconName: 'calendar',
  label: 'Serate',
  ariaLabel: 'Navigate to game nights',
  priority: 5,
  testId: 'nav-game-nights',
  visibility: { authOnly: true },
}
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/config/navigation.ts apps/web/src/proxy.ts
git commit -m "feat(game-nights): remove alpha mode gating for game nights"
```

---

### Task 2: Verify/Create Game Nights API Client

**Files:**
- Verify/Create: `apps/web/src/lib/api/clients/gameNightsClient.ts`

- [ ] **Step 1: Search for existing game nights client**

- [ ] **Step 2: If missing, create the client**

```typescript
import type { HttpClient } from '../core/httpClient';

export interface CreateGameNightRequest {
  title: string;
  scheduledAt: string; // ISO DateTimeOffset
  description?: string;
  location?: string;
  maxPlayers?: number;
  gameIds?: string[];
  invitedUserIds?: string[];
}

export interface UpdateGameNightRequest {
  title: string;
  scheduledAt: string;
  description?: string;
  location?: string;
  maxPlayers?: number;
  gameIds?: string[];
}

export function createGameNightsClient({ httpClient }: { httpClient: HttpClient }) {
  const base = '/api/v1/game-nights';

  return {
    async create(data: CreateGameNightRequest) {
      return httpClient.post<string>(`${base}/`, data);
    },
    async getUpcoming() {
      return httpClient.get(`${base}/`);
    },
    async getMine() {
      return httpClient.get(`${base}/mine`);
    },
    async getById(id: string) {
      return httpClient.get(`${base}/${id}`);
    },
    async update(id: string, data: UpdateGameNightRequest) {
      return httpClient.put(`${base}/${id}`, data);
    },
    async publish(id: string) {
      return httpClient.post(`${base}/${id}/publish`, {});
    },
    async cancel(id: string) {
      return httpClient.post(`${base}/${id}/cancel`, {});
    },
    async invite(id: string, userIds: string[]) {
      return httpClient.post(`${base}/${id}/invite`, { userIds });
    },
    async getRsvps(id: string) {
      return httpClient.get(`${base}/${id}/rsvps`);
    },
    async rsvp(id: string, response: 'Accepted' | 'Declined' | 'Maybe') {
      return httpClient.post(`${base}/${id}/rsvp`, { response });
    },
  };
}
```

- [ ] **Step 3: Register in API barrel export**

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(game-nights): create game nights API client"
```

---

### Task 3: Verify Existing Pages Render

- [ ] **Step 1: Navigate to /game-nights**

Verify the list page renders upcoming game nights (or empty state).

- [ ] **Step 2: Navigate to /game-nights/new**

Verify create form renders with: title, date/time, location, max players, game selection, invite users.

- [ ] **Step 3: Verify GameNightForm component**

Read `_components/GameNightForm.tsx` to confirm it handles the full form lifecycle.

- [ ] **Step 4: Document any issues**

---

### Task 4: Verify RSVP Flow

**Files:**
- Verify: `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx`

- [ ] **Step 1: Read game night detail page**

Verify it shows event details, game list, participant list, RSVP buttons.

- [ ] **Step 2: Verify RSVP buttons call the correct API**

Confirm "Accept", "Decline", "Maybe" buttons call `POST /game-nights/{id}/rsvp`.

- [ ] **Step 3: Verify publish/cancel buttons for organizer**

Confirm the organizer sees "Publish" and "Cancel" actions.

- [ ] **Step 4: Commit fixes if any**

```bash
git add apps/web/src/ && git commit -m "fix(game-nights): verify RSVP and lifecycle flows"
```

---

### Task 5: Add "Plan Game Night" Entry Point

**Files:**
- Modify: Dashboard or library page

- [ ] **Step 1: Add quick action on dashboard**

Add a "Plan Game Night" card or button on the dashboard page.

```tsx
<Button variant="outline" asChild>
  <Link href="/game-nights/new">
    <Calendar className="mr-2 h-4 w-4" />
    Plan Game Night
  </Link>
</Button>
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/ && git commit -m "feat(game-nights): add Plan Game Night entry point"
```

---

### Task 6: Write Tests

- [ ] **Step 1: Check existing game-nights tests**

- [ ] **Step 2: Run existing tests**

```bash
cd apps/web && pnpm test -- --grep "game-night" --run
```

- [ ] **Step 3: Write game nights list page test**

- [ ] **Step 4: Run all tests**

```bash
cd apps/web && pnpm test --run
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/__tests__/ && git commit -m "test(game-nights): add game nights page tests"
```

---

### Task 7: Quality Checks and PR

- [ ] **Step 1: Run typecheck, lint, tests**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run
```

- [ ] **Step 2: Fix any issues**

- [ ] **Step 3: Push and create PR**

```bash
git push -u origin feature/us31-game-nights
gh pr create --base frontend-dev --title "feat(game-nights): activate game night planning (US-31)" --body "## Summary
- Remove alpha mode gating for /game-nights routes
- Create game nights API client
- Verify RSVP flow and lifecycle (Draft → Published → Completed/Cancelled)
- Add Plan Game Night entry point

## User Story
US-31: Come utente, voglio pianificare serate di gioco

## Test Plan
- [ ] /game-nights list renders upcoming events
- [ ] /game-nights/new creates a draft event
- [ ] Publish sends invitations
- [ ] RSVP buttons (Accept/Decline/Maybe) work
- [ ] Cancel flow works for organizer"
```

---

## Backend Endpoints Reference

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/game-nights/` | Create game night (Draft) |
| GET | `/api/v1/game-nights/` | Upcoming published nights |
| GET | `/api/v1/game-nights/mine` | User's nights (organizer + invitee) |
| GET | `/api/v1/game-nights/{id}` | Game night detail |
| PUT | `/api/v1/game-nights/{id}` | Update game night |
| POST | `/api/v1/game-nights/{id}/publish` | Draft → Published |
| POST | `/api/v1/game-nights/{id}/cancel` | Cancel event |
| POST | `/api/v1/game-nights/{id}/invite` | Invite users |
| GET | `/api/v1/game-nights/{id}/rsvps` | Get RSVP list |
| POST | `/api/v1/game-nights/{id}/rsvp` | Submit RSVP response |

## Lifecycle

```
Draft → [Publish] → Published → [Complete/Cancel]
                         ↓
                    Invites sent → Users RSVP (Accepted/Declined/Maybe)
```
