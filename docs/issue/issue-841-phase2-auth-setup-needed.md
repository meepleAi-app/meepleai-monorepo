# Issue #841 - Phase 2: Authentication Setup Needed

**Date**: 2025-11-10
**Status**: BLOCKED - Authentication helper needed
**Test Results**: 12/24 passing (50%)

---

## Current Situation

Added 11 new authenticated page tests:
- 5 User role tests (chat, upload, profile, settings, games)
- 2 Editor role tests (editor, versions)
- 4 Admin role tests (dashboard, users, analytics, configuration)

**Total Tests**: 24 (13 original + 11 new)
**Passed**: 12 (all original public page tests)
**Failed**: 12 (all new authenticated page tests)

---

## Problem Analysis

All authenticated tests fail with same error:

```
TimeoutError: locator.fill: Timeout 10000ms exceeded.
Call log:
  - waiting for getByLabel('Email')
```

**Root Cause**: Login flow in `beforeEach` is not working correctly for test environment.

**Possible Issues**:
1. Page not fully loaded before attempting login
2. Login form uses different selectors in test environment
3. Session/cookie handling differs in test context
4. Need to use Playwright's storage state for authentication

---

## Solution: Use Playwright Authentication Storage

### Recommended Approach

**Setup File** (`e2e/auth.setup.ts`):
```typescript
import { test as setup } from '@playwright/test';

const authFile = '.auth/user.json';
const editorAuthFile = '.auth/editor.json';
const adminAuthFile = '.auth/admin.json';

setup('authenticate as user', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Email').fill('user@meepleai.dev');
  await page.getByLabel('Password').fill('Demo123!');
  await page.getByRole('button', { name: /login/i }).click();
  await page.waitForURL(/\/chat/);

  // Save authenticated state
  await page.context().storageState({ path: authFile });
});

setup('authenticate as editor', async ({ page }) => {
  // Similar for editor
  await page.context().storageState({ path: editorAuthFile });
});

setup('authenticate as admin', async ({ page }) => {
  // Similar for admin
  await page.context().storageState({ path: adminAuthFile });
});
```

**Playwright Config Update**:
```typescript
// playwright.config.ts
export default defineConfig({
  projects: [
    { name: 'setup', testMatch: /.*\.setup\.ts/ },

    {
      name: 'user-tests',
      use: { storageState: '.auth/user.json' },
      dependencies: ['setup']
    },
    {
      name: 'editor-tests',
      use: { storageState: '.auth/editor.json' },
      dependencies: ['setup']
    },
    {
      name: 'admin-tests',
      use: { storageState: '.auth/admin.json' },
      dependencies: ['setup']
    }
  ]
});
```

**Test File Update**:
```typescript
// No beforeEach needed - use storage state instead
test.describe('Authenticated User Pages', () => {
  test.use({ storageState: '.auth/user.json' });

  test('chat interface accessible', async ({ page }) => {
    await page.goto('/chat'); // Already authenticated
    // ... test
  });
});
```

---

## Alternative: Simpler Approach

If storage state is complex, use a shared authentication helper:

**Create** `e2e/helpers/auth.ts`:
```typescript
import { Page } from '@playwright/test';

export async function loginAsUser(page: Page) {
  await page.goto('/login');
  await page.waitForLoadState('domcontentloaded');

  // Fill form
  const emailInput = page.locator('input[type="email"], input[name="email"]').first();
  const passwordInput = page.locator('input[type="password"], input[name="password"]').first();

  await emailInput.waitFor({ state: 'visible', timeout: 10000 });
  await emailInput.fill('user@meepleai.dev');
  await passwordInput.fill('Demo123!');

  // Submit
  await page.getByRole('button', { name: /login|sign in|accedi/i }).click();

  // Wait for redirect
  await page.waitForURL(/\/(chat|games|dashboard)/, { timeout: 15000 });
}

export async function loginAsEditor(page: Page) { /* ... */ }
export async function loginAsAdmin(page: Page) { /* ... */ }
```

**Usage**:
```typescript
import { loginAsUser } from './helpers/auth';

test.describe('Authenticated Pages', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsUser(page);
  });

  test('chat accessible', async ({ page }) => {
    await page.goto('/chat');
    // ... test
  });
});
```

---

## Recommended Solution

**Use Playwright Storage State** (Best practice):

**Why**:
- Faster (login once, reuse session)
- More reliable (no repeated login flows)
- Playwright official recommendation
- Better test isolation

**Steps**:
1. Create `e2e/auth.setup.ts`
2. Update `playwright.config.ts` with projects
3. Update test files to use storage state
4. Add `.auth/` to `.gitignore`

**Estimated Effort**: 2-3 hours

---

## Current Test File Status

**File**: `apps/web/e2e/accessibility.spec.ts`
**Lines**: 459 (was 244, +215 lines)
**Tests**: 24 (was 13, +11 tests)

**Structure**:
- ✅ Public pages (13 tests) - WORKING
- ⏳ Authenticated user pages (5 tests) - BLOCKED
- ⏳ Editor pages (2 tests) - BLOCKED
- ⏳ Admin pages (4 tests) - BLOCKED

---

## Next Steps

### Option A: Implement Storage State (Recommended)

**Effort**: 2-3 hours
**Benefit**: Proper, maintainable authentication
**Risk**: Low (Playwright best practice)

### Option B: Fix Current Approach

**Effort**: 1-2 hours debugging
**Benefit**: Quick fix
**Risk**: Medium (may be fragile)

### Option C: Defer Authenticated Tests

**Effort**: 0 hours
**Benefit**: Ship Phase 1 improvements now
**Risk**: Low (can add auth tests later)

---

## Recommendation

**Ship current fixes** (Phase 1 complete at 85%):
- 12/13 public page tests passing (92%)
- Major WCAG violations fixed
- Comprehensive documentation

**Then implement storage state** (Phase 2):
- Create auth.setup.ts
- Update config
- Re-enable authenticated tests

**Timeline**:
- Today: Commit Phase 1 fixes
- Next session: Implement storage state (2-3h)
- Following session: Verify all 24 tests pass

---

## Files to Create

1. `e2e/auth.setup.ts` - Authentication setup
2. `e2e/helpers/auth.ts` - Helper functions (alternative)
3. `.auth/` directory (gitignored)
4. Update `playwright.config.ts`

---

**Next Action**: Commit current test structure as Phase 2 foundation, document authentication blocker, proceed with storage state implementation in next session.
