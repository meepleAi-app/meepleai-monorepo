# US-41: User Notifications Frontend Activation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Activate the user notifications system (bell icon, notification panel, preferences) by wiring existing frontend components to the backend API and removing alpha mode restrictions.

**Architecture:** Frontend-only activation. Components already exist and are fully wired. The NotificationBell is already rendered in both `UserTopNav` and `UnifiedHeader`. The `/notifications` page already exists at `apps/web/src/app/(authenticated)/notifications/page.tsx`. The API client, Zustand store, SSE hook, and Zod schemas are all complete. The main work is: (1) adding notifications to the sidebar navigation, (2) adding the emoji mapping, (3) building a notification preferences page, and (4) verifying all existing tests pass.

**Tech Stack:** Next.js 16, React 19, Zustand, Tailwind 4, shadcn/ui

**Parent Branch:** `frontend-dev`
**Feature Branch:** `feature/us-41-notifications`

---

## Current State Analysis

### Already Complete (no changes needed)
- **API Client**: `apps/web/src/lib/api/clients/notifications.ts` — all endpoints wired
- **Schemas**: `apps/web/src/lib/api/schemas/notifications.schemas.ts` — NotificationDto, preferences, all response types
- **Store**: `apps/web/src/store/notification/store.ts` — Zustand with devtools + persist + immer
- **SSE Hook**: `apps/web/src/hooks/useNotificationSSE.ts` — real-time updates with backoff
- **NotificationBell**: Already rendered in `UserTopNav` (line 161) and `UnifiedHeader` (line 70)
- **NotificationCenter**: Sheet drawer with NUOVE/PRECEDENTI sections, KB-ready CTA
- **NotificationPanel**: Dropdown panel (legacy, replaced by NotificationCenter)
- **NotificationItem**: Individual notification card with deep linking
- **Notifications Page**: `apps/web/src/app/(authenticated)/notifications/page.tsx` — full page with tabs, filters, pagination
- **Tests**: Bell, Panel, Item, DeepLink tests exist and cover core scenarios

### Needs Work
1. Navigation config: notifications not in `_ALL_NAV_ITEMS` (no sidebar entry)
2. Emoji config: no `/notifications` mapping in `SECTION_EMOJI`
3. Notification Preferences page: no UI for `getPreferences()`/`updatePreferences()` — only API client exists
4. Test verification: ensure all existing tests pass after changes

---

## Task 1: Add Notifications to Navigation Config

**Files:**
- Modify: `apps/web/src/config/navigation.ts`
- Modify: `apps/web/src/config/navigation-emoji.ts`

### Steps

- [ ] Step 1: In `apps/web/src/config/navigation.ts`, add `Bell` to the lucide-react import:
  ```typescript
  import {
    Bell,
    BookOpen,
    Brain,
    // ... existing imports
  } from 'lucide-react';
  ```

- [ ] Step 2: Add a notifications nav item to `_ALL_NAV_ITEMS` array, after the `chat` item (priority 4):
  ```typescript
  {
    id: 'notifications',
    href: '/notifications',
    icon: Bell,
    iconName: 'bell',
    label: 'Notifiche',
    ariaLabel: 'Navigate to notifications',
    priority: 4,
    testId: 'nav-notifications',
    activePattern: /^\/notifications/,
    visibility: { authOnly: true },
    hideFromMainNav: true, // Bell icon in navbar is primary access; sidebar is secondary
  },
  ```
  Note: `hideFromMainNav: true` keeps it out of the bottom action bar but visible in the sidebar.

- [ ] Step 3: In `apps/web/src/config/navigation-emoji.ts`, add the notifications emoji mapping:
  ```typescript
  '/notifications': '🔔',
  ```

- [ ] Step 4: Verify the build compiles:
  ```bash
  cd apps/web && pnpm typecheck
  ```

**Commit:** `feat(nav): add notifications to sidebar navigation and emoji config`

---

## Task 2: Create Notification Preferences Component

**Files:**
- Create: `apps/web/src/components/notifications/NotificationPreferences.tsx`
- Modify: `apps/web/src/components/notifications/index.ts`

### Steps

- [ ] Step 1: Create `NotificationPreferences.tsx` — a client component that fetches and updates preferences using the existing `api.notifications.getPreferences()` and `api.notifications.updatePreferences()` methods:

  ```typescript
  /**
   * NotificationPreferences Component (Issue #41)
   *
   * Form for managing notification delivery preferences.
   * Uses existing API client: getPreferences() / updatePreferences()
   *
   * Preference groups:
   * - Document Processing: email/push/in-app for ready, failed, retry
   * - Game Nights: email/push/in-app for invitations, reminders
   */

  'use client';

  import { useEffect, useState } from 'react';

  import { Loader2, Save } from 'lucide-react';

  import { Button } from '@/components/ui/primitives/button';
  import { useTranslation } from '@/hooks/useTranslation';
  import { api } from '@/lib/api';
  import type { NotificationPreferences as NotificationPreferencesType } from '@/lib/api';
  import { cn } from '@/lib/utils';

  export function NotificationPreferences() {
    const { t } = useTranslation();
    const [prefs, setPrefs] = useState<NotificationPreferencesType | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);

    useEffect(() => {
      void loadPreferences();
    }, []);

    async function loadPreferences() {
      try {
        setIsLoading(true);
        const data = await api.notifications.getPreferences();
        setPrefs(data);
      } catch {
        setError('Impossibile caricare le preferenze');
      } finally {
        setIsLoading(false);
      }
    }

    async function handleSave() {
      if (!prefs) return;
      try {
        setIsSaving(true);
        setSaved(false);
        const { userId, ...rest } = prefs;
        await api.notifications.updatePreferences(rest);
        setSaved(true);
        setTimeout(() => setSaved(false), 3000);
      } catch {
        setError('Errore durante il salvataggio');
      } finally {
        setIsSaving(false);
      }
    }

    function toggle(key: keyof NotificationPreferencesType) {
      if (!prefs) return;
      setPrefs({ ...prefs, [key]: !prefs[key] });
      setSaved(false);
    }

    // ... render preference groups with toggle switches
  }
  ```

  The component should render three sections:
  - **Documenti** (Document Processing): toggles for `emailOnDocumentReady`, `pushOnDocumentReady`, `inAppOnDocumentReady`, `emailOnDocumentFailed`, `pushOnDocumentFailed`, `inAppOnDocumentFailed`, `emailOnRetryAvailable`, `pushOnRetryAvailable`, `inAppOnRetryAvailable`
  - **Serate di gioco** (Game Nights): toggles for `inAppOnGameNightInvitation`, `emailOnGameNightInvitation`, `pushOnGameNightInvitation`, `emailOnGameNightReminder`, `pushOnGameNightReminder`
  - Each toggle should use a simple checkbox or switch UI from shadcn/ui

- [ ] Step 2: Export from `apps/web/src/components/notifications/index.ts`:
  ```typescript
  export { NotificationPreferences } from './NotificationPreferences';
  ```

- [ ] Step 3: Verify types compile:
  ```bash
  cd apps/web && pnpm typecheck
  ```

**Commit:** `feat(notifications): add NotificationPreferences component for delivery settings`

---

## Task 3: Create Notification Preferences Page

**Files:**
- Create: `apps/web/src/app/(authenticated)/notifications/preferences/page.tsx`

### Steps

- [ ] Step 1: Create the page at `apps/web/src/app/(authenticated)/notifications/preferences/page.tsx`:

  ```typescript
  /**
   * Notification Preferences Page (Issue #41)
   *
   * Accessible from /notifications/preferences
   * Renders NotificationPreferences component within authenticated layout.
   */

  import { NotificationPreferences } from '@/components/notifications';

  export default function NotificationPreferencesPage() {
    return (
      <div className="container max-w-2xl mx-auto py-8">
        <h1 className="text-3xl font-bold mb-1">Preferenze notifiche</h1>
        <p className="text-muted-foreground text-sm mb-6">
          Scegli come ricevere le notifiche
        </p>
        <NotificationPreferences />
      </div>
    );
  }
  ```

- [ ] Step 2: Add a link to preferences from the notifications page header. In `apps/web/src/app/(authenticated)/notifications/page.tsx`, add a Settings icon button in the header section that links to `/notifications/preferences`.

- [ ] Step 3: Verify the page renders:
  ```bash
  cd apps/web && pnpm typecheck
  ```

**Commit:** `feat(notifications): add preferences page at /notifications/preferences`

---

## Task 4: Add Link from NotificationCenter to Preferences

**Files:**
- Modify: `apps/web/src/components/notifications/NotificationCenter.tsx`

### Steps

- [ ] Step 1: In the NotificationCenter Sheet footer (after the "Vedi tutte" link), add a preferences gear icon link:

  ```typescript
  import { Settings } from 'lucide-react';

  // In the footer section, add alongside the "Vedi tutte" link:
  <Link
    href="/notifications/preferences"
    onClick={() => onOpenChange(false)}
    className="flex items-center justify-center gap-1 p-2 text-sm text-muted-foreground hover:text-primary transition-colors rounded-md hover:bg-accent"
    data-testid="notification-center-preferences"
    aria-label={t('notificationCenter.preferences')}
  >
    <Settings className="h-4 w-4" />
  </Link>
  ```

  Place it in a flex row with the "Vedi tutte" link so they sit side by side.

- [ ] Step 2: Verify build:
  ```bash
  cd apps/web && pnpm typecheck
  ```

**Commit:** `feat(notifications): add preferences link in notification center drawer`

---

## Task 5: Verify Existing Tests Pass

**Files:**
- Test: `apps/web/src/components/notifications/__tests__/NotificationBell.test.tsx`
- Test: `apps/web/src/components/notifications/__tests__/NotificationPanel.test.tsx`
- Test: `apps/web/src/components/notifications/__tests__/NotificationItem.test.tsx`
- Test: `apps/web/src/components/notifications/__tests__/NotificationDeepLink.test.tsx`
- Test: `apps/web/src/lib/api/clients/__tests__/notifications.test.ts`
- Test: `apps/web/src/store/notification/__tests__/store.test.ts`

### Steps

- [ ] Step 1: Run all existing notification-related tests:
  ```bash
  cd apps/web && pnpm vitest run --reporter=verbose src/components/notifications/ src/lib/api/clients/__tests__/notifications.test.ts src/store/notification/
  ```

- [ ] Step 2: Fix any failures caused by the navigation or component changes.

- [ ] Step 3: Run full frontend test suite to ensure no regressions:
  ```bash
  cd apps/web && pnpm test
  ```

- [ ] Step 4: Run typecheck and lint:
  ```bash
  cd apps/web && pnpm typecheck && pnpm lint
  ```

**Commit:** `test(notifications): verify all notification tests pass after activation`

---

## Task 6: Add NotificationPreferences Unit Tests

**Files:**
- Create: `apps/web/src/components/notifications/__tests__/NotificationPreferences.test.tsx`

### Steps

- [ ] Step 1: Create test file with these test cases:
  - Renders loading state initially
  - Renders preference toggles after fetch
  - Displays error state when fetch fails
  - Calls updatePreferences on save
  - Shows success message after save
  - Toggles individual preference values

  Mock `@/lib/api` to control `api.notifications.getPreferences()` and `api.notifications.updatePreferences()`.

- [ ] Step 2: Run the new tests:
  ```bash
  cd apps/web && pnpm vitest run --reporter=verbose src/components/notifications/__tests__/NotificationPreferences.test.tsx
  ```

- [ ] Step 3: Ensure coverage is above 85% for the new component.

**Commit:** `test(notifications): add unit tests for NotificationPreferences component`

---

## Task 7: PR and Cleanup

### Steps

- [ ] Step 1: Final typecheck and lint:
  ```bash
  cd apps/web && pnpm typecheck && pnpm lint
  ```

- [ ] Step 2: Run full test suite one more time:
  ```bash
  cd apps/web && pnpm test
  ```

- [ ] Step 3: Create PR targeting `frontend-dev`:
  ```bash
  gh pr create --base frontend-dev --title "feat(notifications): activate user notifications frontend (US-41)" --body "..."
  ```

  PR body should summarize:
  - Added notifications to sidebar navigation config
  - Added notification preferences page and component
  - Added preferences link in notification center drawer
  - All existing notification tests verified passing
  - New tests for NotificationPreferences component

- [ ] Step 4: After code review approval, merge and clean up:
  ```bash
  git checkout frontend-dev && git pull
  git branch -D feature/us-41-notifications
  ```

- [ ] Step 5: Update issue status locally and on GitHub.

**Commit:** N/A (PR creation step)

---

## Summary Table

| Task | Description | Est. Time | Files Changed |
|------|-------------|-----------|---------------|
| 1 | Add to navigation + emoji config | 2 min | 2 modified |
| 2 | NotificationPreferences component | 5 min | 2 (1 create, 1 modify) |
| 3 | Preferences page route | 3 min | 2 (1 create, 1 modify) |
| 4 | Preferences link in drawer | 2 min | 1 modified |
| 5 | Verify existing tests | 3 min | 0 (test run only) |
| 6 | Preferences component tests | 5 min | 1 created |
| 7 | PR and cleanup | 3 min | 0 |
| **Total** | | **~23 min** | **5 new/modified files** |

## Key Decisions

1. **`hideFromMainNav: true`** for the nav item — the NotificationBell in the navbar is the primary interaction point. The sidebar entry is for discoverability and direct URL access.
2. **No store changes needed** — the existing Zustand store already has all required actions (fetch, markRead, markAllRead, SSE addNotification).
3. **Preferences are a separate sub-route** (`/notifications/preferences`) rather than inline on the notifications page, keeping the notifications list page clean and focused.
4. **No alpha mode removal needed** — notifications were never gated by `ALPHA_NAV_IDS` in the navigation config; they simply were not added to `_ALL_NAV_ITEMS`. The bell icon was already rendering unconditionally in the navbar.
