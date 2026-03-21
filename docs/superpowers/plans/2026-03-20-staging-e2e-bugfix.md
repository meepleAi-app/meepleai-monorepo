# Staging E2E Flow Bugfix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 2 confirmed bugs blocking admin access and superadmin routing in staging, then verify the full user flow E2E.

**Architecture:** Two client-side fixes in the Next.js frontend: (1) AuthModal redirect logic doesn't recognize `superadmin` role, (2) a helper function `isAdminRole()` to centralize the admin role check pattern across both AuthModal and proxy.ts. No backend changes required.

**Tech Stack:** Next.js 16, React 19, TypeScript, Vitest

---

### Task 1: Create centralized `isAdminRole` helper

**Why:** The admin role check (`role === 'admin' || role === 'superadmin'`) appears in 3+ places (AuthModal x2, proxy.ts). A single helper prevents future drift.

**Files:**
- Create: `apps/web/src/lib/utils/roles.ts`
- Test: `apps/web/src/__tests__/lib/utils/roles.test.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/__tests__/lib/utils/roles.test.ts
import { describe, expect, it } from 'vitest';

import { isAdminRole } from '@/lib/utils/roles';

describe('isAdminRole', () => {
  it('returns true for "Admin"', () => {
    expect(isAdminRole('Admin')).toBe(true);
  });

  it('returns true for "admin" (lowercase)', () => {
    expect(isAdminRole('admin')).toBe(true);
  });

  it('returns true for "superadmin"', () => {
    expect(isAdminRole('superadmin')).toBe(true);
  });

  it('returns true for "SuperAdmin" (mixed case)', () => {
    expect(isAdminRole('SuperAdmin')).toBe(true);
  });

  it('returns false for "User"', () => {
    expect(isAdminRole('User')).toBe(false);
  });

  it('returns false for "Editor"', () => {
    expect(isAdminRole('Editor')).toBe(false);
  });

  it('returns false for undefined', () => {
    expect(isAdminRole(undefined)).toBe(false);
  });

  it('returns false for null', () => {
    expect(isAdminRole(null)).toBe(false);
  });

  it('returns false for empty string', () => {
    expect(isAdminRole('')).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/lib/utils/roles.test.ts`
Expected: FAIL — module `@/lib/utils/roles` not found

- [ ] **Step 3: Write minimal implementation**

```typescript
// apps/web/src/lib/utils/roles.ts
/**
 * Centralized admin role check.
 * Returns true for 'admin' and 'superadmin' roles (case-insensitive).
 *
 * Usage sites: AuthModal redirect, proxy.ts middleware, RequireRole component.
 */
export function isAdminRole(role: string | null | undefined): boolean {
  if (!role) return false;
  const normalized = role.toLowerCase();
  return normalized === 'admin' || normalized === 'superadmin';
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/lib/utils/roles.test.ts`
Expected: PASS — all 9 tests green

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/utils/roles.ts apps/web/src/__tests__/lib/utils/roles.test.ts
git commit -m "feat(auth): add centralized isAdminRole helper for admin/superadmin checks"
```

---

### Task 2: Fix AuthModal superadmin redirect (BUG-2)

**Why:** `AuthModal.tsx` lines 107 and 155 only check `role === 'admin'`, so superadmin users get redirected to `/dashboard` instead of `/admin` after login.

**Files:**
- Modify: `apps/web/src/components/auth/AuthModal.tsx:107` (login redirect)
- Modify: `apps/web/src/components/auth/AuthModal.tsx:155` (2FA redirect)

- [ ] **Step 1: Apply fix at line 107 (login redirect)**

Change:
```typescript
const targetUrl = response.user.role?.toLowerCase() === 'admin' ? '/admin' : redirectTo;
```
To:
```typescript
import { isAdminRole } from '@/lib/utils/roles';
// ... (add import at top of file)

const targetUrl = isAdminRole(response.user.role) ? '/admin' : redirectTo;
```

- [ ] **Step 2: Apply fix at line 155 (2FA redirect)**

Change:
```typescript
const targetUrl = user.role?.toLowerCase() === 'admin' ? '/admin' : redirectTo;
```
To:
```typescript
const targetUrl = isAdminRole(user.role) ? '/admin' : redirectTo;
```

- [ ] **Step 3: Run existing AuthModal tests**

Run: `cd apps/web && pnpm vitest run --reporter verbose 2>&1 | grep -i "authmodal\|auth.modal\|auth-modal"`
Expected: Existing tests pass (no regression)

- [ ] **Step 4: Verify typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | tail -5`
Expected: No type errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/auth/AuthModal.tsx
git commit -m "fix(auth): AuthModal redirect recognizes superadmin role for admin dashboard"
```

---

### Task 3: Adopt `isAdminRole` in proxy.ts

**Why:** `proxy.ts:343` has its own inline `admin || superadmin` check. Using the shared helper ensures consistency.

**Files:**
- Modify: `apps/web/src/proxy.ts:343`

- [ ] **Step 1: Replace inline check with helper**

Change (line 343):
```typescript
const isAdmin = isAuthenticated && (userRole === 'admin' || userRole === 'superadmin');
```
To:
```typescript
import { isAdminRole } from '@/lib/utils/roles';
// ... (add import at top of file)

const isAdmin = isAuthenticated && isAdminRole(userRole);
```

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | tail -5`
Expected: No type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/proxy.ts
git commit -m "refactor(proxy): use centralized isAdminRole helper"
```

---

### Task 4: Create PR and merge to staging

**Why:** These fixes must reach the `main-staging` branch to be deployed. PR target is `main-dev` (parent branch per CLAUDE.md rules).

**Files:** None (git operations only)

- [ ] **Step 1: Push branch and create PR**

```bash
git push -u origin feature/fix-superadmin-staging
gh pr create --base main-dev --title "fix(auth): superadmin admin redirect + centralized role check" --body "$(cat <<'EOF'
## Summary
- Fix AuthModal redirect: superadmin users now redirect to `/admin` after login (was going to `/dashboard`)
- Add `isAdminRole()` helper to centralize admin/superadmin role checks
- Adopt helper in proxy.ts for consistency

## Bug Details
- **BUG-2**: `AuthModal.tsx:107,155` only checked `role === 'admin'`, missing `superadmin`
- **Staging impact**: superadmin login → dashboard instead of admin panel

## Test plan
- [ ] `pnpm vitest run src/__tests__/lib/utils/roles.test.ts` — new helper tests
- [ ] `pnpm tsc --noEmit` — no type errors
- [ ] Manual: login as superadmin → redirects to `/admin`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 2: After PR merge, cherry-pick to main-staging**

```bash
git checkout main-staging && git pull
git cherry-pick <merge-commit-hash>
git push origin main-staging
```

Note: The `proxy.ts` file exists on `main-dev` but `main-staging` still uses `middleware.ts`. The proxy.ts changes will only take effect after `main-dev` is fully merged to staging. The AuthModal fix works regardless since it's a client component.

---

### Task 5: Post-deploy staging verification

**Why:** After deploy, verify the full E2E flow works.

**Files:** None (manual testing)

- [ ] **Step 1: Verify admin access**

Navigate to `https://meepleai.app/admin/users/invitations`
Expected: Admin invitations page loads (not redirect to dashboard)

- [ ] **Step 2: Verify superadmin login redirect**

Logout → Login as `admin@meepleai.app`
Expected: Redirect to `/admin` (not `/dashboard`)

- [ ] **Step 3: Verify invite flow**

On admin invitations page, click "Invite User", enter test email, select role, send.
Expected: Toast "Invitation sent" + check server logs for email delivery.

- [ ] **Step 4: Verify game catalog has 30+ games**

Navigate to `https://meepleai.app/games`
Expected: At least 30 games displayed with search/pagination.

- [ ] **Step 5: Verify add-to-collection**

Click Plus icon on a game card.
Expected: AddGameWizard opens, game added to library.

- [ ] **Step 6: Verify chat from card**

After adding game to library, click "Chats" in card footer.
Expected: ChatDrawerSheet opens, can create new thread.

- [ ] **Step 7: Document results**

Record pass/fail for each step. If admin access still fails after deploy, investigate:
- Browser DevTools → Network tab → check `/api/v1/auth/me` response during admin page load
- Check if `getCurrentUser()` throws by adding `console.log` in RequireRole useEffect
- Check if `onboarding_completed` cookie is interfering (staging middleware checks this)
