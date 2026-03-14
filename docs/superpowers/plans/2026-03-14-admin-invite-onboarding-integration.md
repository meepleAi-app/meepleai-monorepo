# Admin Invite + Onboarding Integration — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the existing accept-invite page to the existing onboarding wizard, verify admin UI, and write an E2E test covering the full user journey.

**Architecture:** The accept-invite page (`/accept-invite`) currently redirects to `/dashboard` after password setup. We create a new `/onboarding` route page that renders the existing `OnboardingWizard` component starting at step 2 (skipping password, already done). Admin invitation UI and voice already exist — just verify. One serial E2E Playwright test validates the journey.

**Tech Stack:** Next.js 16 (App Router), React 19, Zustand, React Query, Playwright, Tailwind 4

**Spec:** `docs/superpowers/specs/2026-03-14-admin-invite-voice-onboarding-design.md`

---

## Scope Reality Check

After codebase exploration, most components already exist:

| Component | Status | Work |
|-----------|--------|------|
| Accept-invite page | ✅ Exists | Change redirect target |
| OnboardingWizard (5 steps) | ✅ Exists | Add `startStep` prop |
| `/onboarding` route | ❌ Missing | Create new page under `(authenticated)` |
| Middleware | ✅ Exists | Change redirect target from `/accept-invite` to `/onboarding` |
| Admin invitation UI | ✅ Exists | Verify only |
| Voice in chat | ✅ Exists | Verify only |
| Role management | ✅ Exists | Verify only |
| Audit logs | ✅ Exists | Verify only |
| E2E test | ❌ Missing | Write new test |

**Actual new code: ~150 lines. Integration + verification + E2E test.**

---

## Chunk 1: Accept-Invite → Onboarding Integration

### Task 1: Add `startStep` prop to OnboardingWizard

**Files:**
- Modify: `apps/web/src/components/onboarding/OnboardingWizard.tsx`
- Test: `apps/web/src/components/onboarding/__tests__/OnboardingWizard.test.tsx`

- [ ] **Step 1: Read the existing OnboardingWizard test file**

Check what tests exist already to understand test patterns.

- [ ] **Step 2: Write failing test for startStep prop**

```tsx
it('starts at step 2 when startStep=2 is provided', () => {
  render(<OnboardingWizard token="test-token" role="User" startStep={2} />);
  expect(screen.getByTestId('progress-step-1')).toHaveClass('bg-amber-500'); // completed
  expect(screen.getByText('Profile')).toBeInTheDocument();
  expect(screen.queryByText('Password')).not.toBeInTheDocument(); // step label
});
```

Run: `cd apps/web && pnpm vitest run src/components/onboarding/__tests__/OnboardingWizard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement startStep prop**

In `OnboardingWizard.tsx`:

```tsx
export interface OnboardingWizardProps {
  token: string;
  role: string;
  startStep?: number; // NEW: skip to this step (default: 1)
}

export function OnboardingWizard({ token, role: _role, startStep = 1 }: OnboardingWizardProps) {
  const router = useRouter();
  const [state, setState] = useState<WizardState>({
    currentStep: startStep,
    // IMPORTANT: passwordCompleted must be true when starting past step 1.
    // This controls two behaviors:
    // 1. goToPrev() clamps minimum step to 2 (prevents going back to password)
    // 2. "Skip wizard" link only renders when passwordCompleted is true
    passwordCompleted: startStep > 1,
    addedGameId: null,
    addedGameName: null,
  });
  // ... rest unchanged
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/__tests__/OnboardingWizard.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/OnboardingWizard.tsx apps/web/src/components/onboarding/__tests__/OnboardingWizard.test.tsx
git commit -m "feat(onboarding): add startStep prop to OnboardingWizard"
```

---

### Task 2: Create `/onboarding` route page + fix middleware

**Files:**
- Create: `apps/web/src/app/(authenticated)/onboarding/page.tsx`
- Modify: `apps/web/src/middleware.ts` (change onboarding redirect target)

The `(authenticated)` route group already exists with 80+ pages (dashboard, library, agents, etc.) so this is the correct location.

- [ ] **Step 1: Fix middleware redirect target**

In `middleware.ts` (line 55), the `onboarding_completed === 'false'` branch currently redirects to `/accept-invite`. This must change to `/onboarding` so that:
- Users who haven't completed onboarding get redirected to the wizard, not back to accept-invite
- The accept-invite page is only for initial password setup (one-time)

```tsx
// Before (line 55):
const onboardingUrl = new URL('/accept-invite', request.url);

// After:
const onboardingUrl = new URL('/onboarding', request.url);
```

Also add `/onboarding` to `PUBLIC_PATHS` to prevent infinite redirect loop (middleware would redirect `/onboarding` → `/onboarding`):

```tsx
const PUBLIC_PATHS = new Set([
  '/',
  '/login',
  '/register',
  '/forgot-password',
  '/reset-password',
  '/games',
  '/accept-invite',
  '/verify-email',
  '/onboarding', // NEW: prevent redirect loop for onboarding
]);
```

**Note**: Adding `/onboarding` to `PUBLIC_PATHS` is safe here because the middleware only does onboarding redirect checks — it does NOT enforce authentication. Auth enforcement happens via the `(authenticated)` route group's layout/server components checking session cookies.

- [ ] **Step 3: Create the onboarding page**

```tsx
// apps/web/src/app/(authenticated)/onboarding/page.tsx
'use client';

import { OnboardingWizard } from '@/components/onboarding';

export default function OnboardingPage() {
  return (
    <div className="flex min-h-[80vh] items-center justify-center px-4">
      <div className="w-full max-w-lg">
        <OnboardingWizard token="" role="" startStep={2} />
      </div>
    </div>
  );
}
```

Note: `token` is empty because password is already set. The wizard's PasswordStep won't render since we start at step 2.

- [ ] **Step 4: Verify page renders in dev**

Run: `cd apps/web && pnpm dev` → navigate to `http://localhost:3000/onboarding`
Expected: Wizard shows Profile step (step 2), "Skip wizard" link visible

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/(authenticated)/onboarding/page.tsx apps/web/src/middleware.ts
git commit -m "feat(onboarding): create /onboarding route page with wizard at step 2"
```

---

### Task 3: Change accept-invite redirect to `/onboarding`

**Files:**
- Modify: `apps/web/src/app/(public)/accept-invite/page.tsx`

- [ ] **Step 1: Change redirect target**

In `accept-invite/page.tsx`, inside `handleSubmit` callback (line 140):

```tsx
// Before:
setTimeout(() => router.push('/dashboard'), 1500);

// After:
setTimeout(() => router.push('/onboarding'), 1500);
```

Also update the `SuccessCard` function body (line 211, same file — it's a local component with hardcoded text):

```tsx
// Before:
<CardDescription>
  Your password has been set successfully. Redirecting to your dashboard...
</CardDescription>

// After:
<CardDescription>
  Your password has been set successfully. Setting up your account...
</CardDescription>
```

- [ ] **Step 2: Run existing accept-invite tests**

Run: `cd apps/web && pnpm vitest run --reporter=verbose 2>&1 | head -50`
Check for any accept-invite tests that assert the `/dashboard` redirect.

- [ ] **Step 3: Update tests if needed**

If tests assert `router.push('/dashboard')`, update to `router.push('/onboarding')`.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/(public)/accept-invite/page.tsx
git commit -m "feat(onboarding): redirect accept-invite to /onboarding instead of /dashboard"
```

---

## Chunk 2: Verification

### Task 4: Verify admin invitation UI

**Files:** None to modify (verification only)

- [ ] **Step 1: Verify InviteUserDialog renders**

Run the existing test:
```bash
cd apps/web && pnpm vitest run src/components/admin/invitations/__tests__/InviteUserDialog.test.tsx
```

- [ ] **Step 2: Verify BulkInviteDialog renders**

```bash
cd apps/web && pnpm vitest run src/components/admin/invitations/__tests__/BulkInviteDialog.test.tsx
```

- [ ] **Step 3: Verify invitations page renders**

```bash
cd apps/web && pnpm vitest run src/app/admin/\\(dashboard\\)/users/invitations/__tests__/page.test.tsx
```

- [ ] **Step 4: Document verification results**

If all pass → no work needed. If any fail → fix and commit.

---

### Task 5: Verify voice integration in ChatThreadView

**Files:** None to modify (verification only)

- [ ] **Step 1: Check VoiceMicButton is rendered in ChatThreadView**

```bash
cd apps/web && grep -n "VoiceMicButton\|TtsSpeakerButton" src/components/chat-unified/ChatThreadView.tsx
```

Expected: Both components are imported and rendered.

- [ ] **Step 2: Verify VoiceMicButton data-testid**

The `VoiceMicButton` uses dynamic testid: `data-testid={voice-mic-${state}}` where state is `idle|requesting|listening|processing|error`. In E2E tests, use `[data-testid^="voice-mic-"]` selector to match any state.

- [ ] **Step 3: Run voice hook tests**

```bash
cd apps/web && pnpm vitest run src/hooks/__tests__/ --reporter=verbose 2>&1 | grep -i voice
```

- [ ] **Step 4: Document verification results**

Voice is confirmed complete. No new work needed.

---

## Chunk 3: E2E Test

### Task 6: Write E2E Playwright test

**Files:**
- Create: `apps/web/e2e/admin/admin-invite-onboarding-e2e.spec.ts`

- [ ] **Step 1: Check existing E2E test patterns**

Read existing E2E tests:
```bash
ls apps/web/e2e/admin/
ls apps/web/e2e/onboarding/
```

Read `apps/web/e2e/admin/admin-invite-flow.spec.ts` and `apps/web/e2e/onboarding/accept-invite.spec.ts` for patterns.

- [ ] **Step 2: Write the serial E2E test**

```typescript
// apps/web/e2e/admin/admin-invite-onboarding-e2e.spec.ts
import { expect, test } from '@playwright/test';
import path from 'path';

const ADMIN_STATE = path.join(__dirname, '../.auth/admin-state.json');
const USER_STATE = path.join(__dirname, '../.auth/invited-user-state.json');

test.describe.serial('Admin invite → onboarding → voice → audit', () => {
  const testEmail = `e2e-invite-${Date.now()}@test.meepleai.com`;

  test('Step 1: Admin sends invitation', async ({ page, context }) => {
    // Login as admin (adapt auth flow from existing E2E patterns)
    await page.goto('/login');
    // ... fill admin credentials from env
    await page.goto('/admin/users');

    // Open invite dialog
    await page.getByRole('button', { name: /invite/i }).click();
    await page.getByLabel(/email/i).fill(testEmail);
    await page.getByRole('button', { name: /send/i }).click();

    // Verify toast success
    await expect(page.getByText(/invitation sent/i)).toBeVisible();

    // Save admin session for reuse in Step 5
    await context.storageState({ path: ADMIN_STATE });
  });

  test('Step 2: User accepts invitation', async ({ page, context }) => {
    // Get token via admin API or construct URL
    // Navigate to accept-invite page
    await page.goto(`/accept-invite?token=test-token`);

    // Fill password
    await page.getByLabel(/^password$/i).fill('TestPassword1!');
    await page.getByLabel(/confirm/i).fill('TestPassword1!');
    await page.getByRole('button', { name: /create account/i }).click();

    // Verify redirect to onboarding
    await expect(page).toHaveURL(/\/onboarding/);

    // Save user session for reuse in Steps 3-4
    await context.storageState({ path: USER_STATE });
  });

  test('Step 3: Onboarding wizard', async ({ browser }) => {
    // Restore user session
    const context = await browser.newContext({ storageState: USER_STATE });
    const page = await context.newPage();
    await page.goto('/onboarding');

    // Should start at Profile step (step 2)
    await expect(page.getByText('Profile')).toBeVisible();

    // Fill display name
    await page.getByLabel(/display name/i).fill('E2E Test User');
    await page.getByRole('button', { name: /continue|next/i }).click();

    // Skip interests
    await page.getByTestId('wizard-skip').click();

    // Skip game (or add one)
    await page.getByTestId('wizard-skip').click();

    // Should redirect to dashboard
    await expect(page).toHaveURL(/\/dashboard/);

    await context.close();
  });

  test('Step 4: Voice button visible in chat', async ({ browser }) => {
    const context = await browser.newContext({ storageState: USER_STATE });
    const page = await context.newPage();

    // Navigate to ask page (voice-first)
    await page.goto('/ask');

    // Verify mic button present (testid is dynamic: voice-mic-{state})
    await expect(page.locator('[data-testid^="voice-mic-"]')).toBeVisible();

    await context.close();
  });

  test('Step 5: Admin verifies audit log', async ({ browser }) => {
    // Restore admin session
    const context = await browser.newContext({ storageState: ADMIN_STATE });
    const page = await context.newPage();

    // Navigate to admin analytics
    await page.goto('/admin/analytics');

    // Check audit tab
    await page.getByRole('tab', { name: /audit/i }).click();

    // Verify invitation accepted entry exists
    await expect(page.getByText(/invitation/i)).toBeVisible();

    await context.close();
  });
});
```

**Note:** This is a template. Exact selectors and auth flow depend on test infrastructure (seed data, auth helpers). Adapt based on patterns found in existing E2E tests (see `e2e/admin/admin-invite-flow.spec.ts` and `e2e/onboarding/accept-invite.spec.ts`).

- [ ] **Step 3: Run the E2E test in headed mode to debug**

```bash
cd apps/web && pnpm playwright test e2e/admin/admin-invite-onboarding-e2e.spec.ts --headed
```

- [ ] **Step 4: Fix any selector/timing issues**

Iterate until test passes reliably.

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/admin/admin-invite-onboarding-e2e.spec.ts
git commit -m "test(e2e): admin invite → onboarding → voice → audit journey"
```

---

## Chunk 4: Final

### Task 7: Update spec status + typecheck

- [ ] **Step 1: Run full typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 2: Run lint**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 3: Update spec status to "Implemented"**

In `docs/superpowers/specs/2026-03-14-admin-invite-voice-onboarding-design.md`:
Change `**Status**: Revised (post spec-review)` to `**Status**: Implemented`

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "docs(spec): mark admin-invite-voice-onboarding as implemented"
```

### Task 8: Create PR

- [ ] **Step 1: Create feature branch from current branch**

```bash
git checkout -b feature/admin-invite-onboarding-integration
git config branch.feature/admin-invite-onboarding-integration.parent $(git branch --show-current)
```

- [ ] **Step 2: Push and create PR to parent branch**

Detect parent branch with `git config branch.feature/admin-invite-onboarding-integration.parent` and use it as `--base`. Do NOT hardcode `main-dev`.

```bash
PARENT=$(git config branch.feature/admin-invite-onboarding-integration.parent)
git push -u origin feature/admin-invite-onboarding-integration
gh pr create --base "$PARENT" --title "feat: connect accept-invite to onboarding wizard" --body "..."
```

---

## Dependencies

```
Task 1 (OnboardingWizard startStep) → Task 2 (create /onboarding page) → Task 3 (change redirect)
Task 4, 5 (verification) → independent, can run in parallel
Task 6 (E2E) → depends on Tasks 1-3
Task 7, 8 (final) → depends on all
```

## Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Middleware redirect loop on `/onboarding` | Add to `PUBLIC_PATHS` + change redirect target in Task 2 |
| Auth cookie not set after accept-invite | Test in headed browser, check Set-Cookie header |
| E2E test flaky due to token generation | Use admin API to create invitation, extract token |
| OnboardingWizard PasswordStep renders with empty token | `startStep=2` skips it entirely; add defensive guard comment |
| PR targets wrong branch | Use `git config branch.<name>.parent` to detect, never hardcode |
