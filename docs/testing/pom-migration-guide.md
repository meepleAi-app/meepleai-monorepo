# Page Object Model Migration Guide

**Target Audience**: Developers migrating existing E2E tests to POM architecture
**Estimated Time**: 30-60 minutes per test file
**Related**: [POM Architecture Design](./pom-architecture-design.md), [POM Coding Standards](./pom-coding-standards.md)

---

## Overview

This guide provides step-by-step instructions for migrating existing Playwright E2E tests from direct DOM manipulation to the Page Object Model pattern.

**Benefits of Migration**:
- ✅ **50% less code**: Eliminate duplicated selectors and setup logic
- ✅ **2x faster to write new tests**: Reuse page objects and fixtures
- ✅ **95%+ test reliability**: Semantic waits and robust selectors
- ✅ **Single source of truth**: Update selectors in one place

---

## Migration Process

### Step 1: Identify Target Test File

**Select a test file for migration**:
```bash
cd apps/web/e2e
ls *.spec.ts  # List all test files
```

**Prioritize**:
1. **High duplication**: Files with repeated selectors (e.g., `admin-users.spec.ts`)
2. **Frequent failures**: Tests with flaky selectors or timing issues
3. **High traffic**: Most-run tests in CI

---

### Step 2: Analyze Existing Test Structure

**Read the test file** and identify:

1. **Pages visited**: Which URLs does the test navigate to?
2. **Selectors used**: What `page.getByRole()`, `page.locator()` calls exist?
3. **Actions performed**: Clicks, fills, selects, file uploads
4. **Assertions made**: What conditions are verified?
5. **Setup code**: Mock routes, authentication, data seeding

**Example Analysis** (`admin-users.spec.ts`):

```typescript
// BEFORE: Direct DOM manipulation
test('complete user lifecycle: create → edit → delete', async ({ page }) => {
  // Setup
  await mockAuthenticatedAdmin(page);  // ❌ Duplicated in 8+ files
  await page.route(...);                // ❌ Duplicated mock setup

  // Navigation
  await page.goto('http://localhost:3000/admin/users');  // ❌ Hardcoded URL

  // Actions
  await page.getByTestId('open-create-user-modal').click();  // ❌ Low-level
  await page.getByLabel('Email').fill('newuser@example.com');  // ❌ Low-level
  await page.getByTestId('submit-user-form').click();  // ❌ Low-level

  // Assertions
  await expect(page.getByText(/created successfully/)).toBeVisible();  // ❌ Repeated
});
```

**Problems Identified**:
- ❌ Duplicated auth setup
- ❌ Hardcoded URLs
- ❌ Low-level DOM interactions
- ❌ No abstraction for common actions

---

### Step 3: Check if Page Object Exists

**Look for existing page object**:
```bash
ls apps/web/e2e/pages/*/*.ts
```

**If page object exists**:
- ✅ Use it! Jump to Step 5.

**If page object doesn't exist**:
- ❌ Create it (Step 4).

---

### Step 4: Create Page Object (If Needed)

**A. Choose appropriate directory**:

```
apps/web/e2e/pages/
├── auth/        # Login, register, OAuth, 2FA
├── game/        # Game list, details, PDF upload
├── chat/        # Chat interface
├── editor/      # RuleSpec editor
└── admin/       # Admin pages (users, analytics, config)
```

**B. Create page object file**:

```bash
# Example: Create UserManagementPage
mkdir -p apps/web/e2e/pages/admin
touch apps/web/e2e/pages/admin/UserManagementPage.ts
```

**C. Implement page object** (see [POM Coding Standards](./pom-coding-standards.md)):

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';
import { User } from '../../types/pom-interfaces';

export class UserManagementPage extends BasePage {
  // 1. Define locators (private getters)
  private get createUserButton(): Locator {
    return this.page.getByTestId('open-create-user-modal');
  }

  // 2. Navigation method
  async goto(): Promise<void> {
    await this.page.goto('/admin/users');
    await this.waitForLoad();
  }

  // 3. Semantic action methods
  async createUser(user: User): Promise<void> {
    await this.click(this.createUserButton);
    await this.fillCreateUserForm(user);
    await this.submitUserForm();
    await this.waitForSuccessToast(/created successfully/i);
  }

  // 4. Helper methods (private if reusable only within page)
  private async fillCreateUserForm(user: User): Promise<void> {
    await this.fill(this.page.getByLabel(/email/i), user.email);
    await this.fill(this.page.getByLabel(/password/i), user.password || 'DefaultPass123!');
    await this.fill(this.page.getByLabel(/display name/i), user.displayName);
    await this.selectOption(this.page.getByLabel(/role/i), user.role);
  }

  // 5. Assertion methods
  async assertUserVisible(email: string): Promise<void> {
    await this.waitForElement(
      this.page.getByRole('cell', { name: email, exact: true })
    );
  }
}
```

**Key Principles**:
- Locators are private getters (lazy evaluation)
- Action methods are semantic (`createUser()` not `clickCreateButton()`)
- Assertions start with `assert` prefix
- Use `BasePage` utilities (`fill()`, `click()`, `waitForElement()`)

---

### Step 5: Refactor Test Using Page Object

**BEFORE** (Direct DOM):
```typescript
test('create user', async ({ page }) => {
  await mockAuthenticatedAdmin(page);
  await page.goto('http://localhost:3000/admin/users');

  await page.getByTestId('open-create-user-modal').click();
  await page.getByLabel('Email').fill('newuser@example.com');
  await page.getByLabel('Password').fill('SecurePass123!');
  await page.getByLabel('Display Name').fill('New User');
  await page.getByLabel('Role').selectOption('Editor');
  await page.getByTestId('submit-user-form').click();

  await expect(page.getByText(/created successfully/)).toBeVisible();
  await expect(page.getByRole('cell', { name: 'newuser@example.com', exact: true })).toBeVisible();
});
```

**AFTER** (POM):
```typescript
import { test, expect } from './fixtures/auth';
import { UserManagementPage } from './pages/admin/UserManagementPage';
import { DataFactory } from './fixtures/data';

test('create user', async ({ adminPage }) => {  // ✅ Use adminPage fixture
  const userPage = new UserManagementPage(adminPage);
  await userPage.goto();

  const newUser = DataFactory.createUser({  // ✅ Use data factory
    email: 'newuser@example.com',
    displayName: 'New User',
    role: 'Editor',
  });

  await userPage.createUser(newUser);  // ✅ Semantic method
  await userPage.assertUserVisible(newUser.email);  // ✅ Semantic assertion
});
```

**Improvements**:
- ✅ **20 lines → 12 lines** (40% reduction)
- ✅ No duplicated auth setup (fixture)
- ✅ No hardcoded URLs (page object)
- ✅ No low-level DOM interactions (semantic methods)
- ✅ Reusable data factory

---

### Step 6: Update Fixtures (If Needed)

**Check if fixture exists**:
```bash
ls apps/web/e2e/fixtures/*.ts
```

**Existing fixtures**:
- `auth.ts` - Mock authentication (already exists)
- `i18n.ts` - Internationalization support (already exists)

**New fixtures to create** (if not exists):
- `data.ts` - Test data factories
- `cleanup.ts` - Cleanup utilities

**Example: Data Factory** (`fixtures/data.ts`):

```typescript
import { User } from '../types/pom-interfaces';

export class DataFactory {
  private static userIdCounter = 1;

  static createUser(overrides?: Partial<User>): User {
    return {
      email: `user${this.userIdCounter++}@example.com`,
      displayName: `Test User ${this.userIdCounter}`,
      role: 'User',
      password: 'SecurePass123!',
      ...overrides,
    };
  }

  static createAdminUser(overrides?: Partial<User>): User {
    return this.createUser({ role: 'Admin', ...overrides });
  }

  static reset(): void {
    this.userIdCounter = 1;
  }
}
```

**Update `auth.ts` to export POM fixtures**:

```typescript
import { test as base, Page } from '@playwright/test';
import { AuthPage } from '../pages/auth/AuthPage';

export const test = base.extend<{
  adminPage: Page;
  authPage: AuthPage;  // NEW: Add page object fixtures
}>({
  adminPage: async ({ page }, use) => {
    await loginAsAdmin(page);
    await use(page);
  },
  authPage: async ({ page }, use) => {  // NEW
    const authPage = new AuthPage(page);
    await use(authPage);
  },
});
```

---

### Step 7: Remove Duplicated Code

**Before migration**:
- Auth setup in 8+ test files
- Game API mocking in 5+ test files
- User creation logic in 3+ test files

**After migration**:
- Auth setup: 1 fixture (`fixtures/auth.ts`)
- Game API mocking: 1 page object method
- User creation: 1 data factory + 1 page object method

**Clean up test file**:
```typescript
// ❌ REMOVE: Duplicated mock setup
async function setupAuthRoutes(page: Page) { ... }
async function mockAuthenticatedAdmin(page: Page) { ... }

// ✅ KEEP: Test-specific mocks (if any)
// ✅ KEEP: Test data unique to this test
```

---

### Step 8: Run Tests and Verify

**Run migrated test**:
```bash
cd apps/web
pnpm test:e2e admin-users.spec.ts
```

**Check for issues**:
- ❌ Timeout errors → Increase `waitForElement()` timeout
- ❌ Element not found → Verify selector in page object
- ❌ Test fails but UI works → Check assertion logic

**Common fixes**:

| Issue | Fix |
|-------|-----|
| Timeout waiting for element | Add explicit wait: `await page.waitForTimeout(1000)` |
| Selector not found | Use `page.pause()` to debug, update locator |
| Toast overlaps button | Wait for toast to disappear: `await page.waitForTimeout(1000)` |
| Modal doesn't close | Use `{ force: true }` in click: `await this.click(button, { force: true })` |

---

### Step 9: Update Test Documentation

**Add JSDoc comments**:
```typescript
/**
 * E2E Test: User Management Complete Lifecycle
 *
 * Scenario: Admin creates, edits, and deletes a user
 *
 * Given: An authenticated admin user
 * When: Admin creates a new user, updates their role, then deletes them
 * Then: All operations succeed and UI reflects changes
 *
 * @see UserManagementPage for page object implementation
 * @see DataFactory for test data generation
 */
test('complete user lifecycle: create → edit → delete', async ({ adminPage }) => {
  // ...
});
```

---

## Complete Migration Example

### Before: `chat.spec.ts` (Old)

```typescript
import { test, expect } from '@playwright/test';

test.describe('Chat Page', () => {
  test('should require authentication', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Should show login required message
    await expect(page.getByRole('heading', { name: /login required/i })).toBeVisible();
    await expect(page.getByText(/please log in/i)).toBeVisible();

    // Should have login link
    await expect(page.getByRole('link', { name: /go to login/i })).toBeVisible();
  });

  test('should have return to home link', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('link', { name: /back to home/i })).toBeVisible();
  });
});
```

### After: `chat.spec.ts` (New)

```typescript
import { test, expect } from './fixtures/auth';
import { ChatPage } from './pages/chat/ChatPage';

test.describe('Chat Page', () => {
  test('should require authentication', async ({ page }) => {
    const chatPage = new ChatPage(page);
    await chatPage.goto();

    await chatPage.assertLoginRequired();
  });

  test('authenticated user can ask questions', async ({ userPage }) => {
    const chatPage = new ChatPage(userPage);
    await chatPage.goto();

    await chatPage.askQuestionAndWait('How do I castle in chess?');

    await chatPage.assertAnswerContains('king');
    await chatPage.assertAnswerContains('rook');
    await chatPage.assertCitationVisible('Chess Rules');
  });
});
```

**Changes**:
- ✅ Use `ChatPage` for all interactions
- ✅ Use `userPage` fixture for auth
- ✅ Semantic methods: `askQuestionAndWait()`, `assertAnswerContains()`
- ✅ No explicit `waitForLoadState()` (handled by page object)

---

## Migration Checklist

**For Each Test File**:

- [ ] **Step 1**: Identify target test file
- [ ] **Step 2**: Analyze existing test structure
- [ ] **Step 3**: Check if page object exists
- [ ] **Step 4**: Create page object (if needed)
- [ ] **Step 5**: Refactor test using page object
- [ ] **Step 6**: Update fixtures (if needed)
- [ ] **Step 7**: Remove duplicated code
- [ ] **Step 8**: Run tests and verify
- [ ] **Step 9**: Update test documentation

**Quality Gates**:

- [ ] All tests pass locally (`pnpm test:e2e`)
- [ ] No hardcoded URLs in tests
- [ ] No direct `page.locator()` or `page.getByRole()` in tests
- [ ] No duplicated auth/mock setup
- [ ] Semantic method names (not `clickButton()`, use `createUser()`)
- [ ] JSDoc comments added to tests

---

## Common Migration Patterns

### Pattern 1: Authentication Setup

**Before**:
```typescript
async function setupAuthRoutes(page: Page) {
  await page.route(`${apiBase}/auth/me`, async (route) => {
    await route.fulfill({ status: 200, body: JSON.stringify({ user: { ... } }) });
  });
}

test('...', async ({ page }) => {
  await setupAuthRoutes(page);
  await page.goto('/admin/users');
});
```

**After**:
```typescript
import { test } from './fixtures/auth';

test('...', async ({ adminPage }) => {  // ✅ Fixture handles auth
  const userPage = new UserManagementPage(adminPage);
  await userPage.goto();
});
```

### Pattern 2: Form Filling

**Before**:
```typescript
await page.getByLabel('Email').fill('test@example.com');
await page.getByLabel('Password').fill('password123');
await page.getByLabel('Display Name').fill('Test User');
await page.getByRole('button', { name: /submit/i }).click();
```

**After**:
```typescript
const userData = DataFactory.createUser();
await authPage.register(userData);
```

### Pattern 3: Assertions

**Before**:
```typescript
await expect(page.getByText(/success/i)).toBeVisible();
await expect(page.getByRole('cell', { name: 'test@example.com' })).toBeVisible();
```

**After**:
```typescript
await userPage.assertUserVisible('test@example.com');
```

### Pattern 4: Modal Interactions

**Before**:
```typescript
await page.getByTestId('open-modal').click();
await page.getByLabel('Name').fill('Test');
await page.getByRole('button', { name: /confirm/i }).click();
await expect(page.getByRole('heading', { name: /modal title/i })).not.toBeVisible();
```

**After**:
```typescript
const modal = new Modal(page, '[data-testid="user-modal"]');
await modal.waitForOpen();
await modal.fillInput(/name/i, 'Test');
await modal.confirm();
```

---

## Troubleshooting

### Issue: Timeout waiting for element

**Cause**: Element takes longer than 10s to appear

**Fix**:
```typescript
await this.waitForElement(locator, { timeout: 30000 });  // 30s timeout
```

### Issue: Test fails but UI works in browser

**Cause**: Race condition or timing issue

**Fix**:
```typescript
// Add explicit wait before action
await this.page.waitForTimeout(1000);
await this.click(button);
```

### Issue: Page object method not found

**Cause**: TypeScript compilation issue

**Fix**:
```bash
cd apps/web
pnpm typecheck  # Check for errors
```

### Issue: Fixture not available in test

**Cause**: Fixture not exported from `fixtures/auth.ts`

**Fix**:
```typescript
// fixtures/auth.ts
export const test = base.extend<{
  myFixture: MyType;  // ✅ Add fixture type
}>({
  myFixture: async ({ page }, use) => {
    // ...
    await use(fixture);
  },
});
```

---

## Migration Progress Tracking

**Track migration progress** in `docs/testing/pom-migration-status.md`:

```markdown
# POM Migration Status

**Target**: 30 test files
**Completed**: 10 test files (33%)

## Completed
- ✅ `admin-users.spec.ts` (2025-11-10)
- ✅ `chat.spec.ts` (2025-11-10)
- ✅ `editor-rich-text.spec.ts` (2025-11-11)

## In Progress
- 🔄 `pdf-upload-journey.spec.ts` (50% complete)

## Pending
- ⏳ `authenticated.spec.ts`
- ⏳ `setup.spec.ts`
- ... (20 more files)
```

---

## Next Steps

After completing migration:

1. **Update CI/CD**: Ensure tests run in CI with new structure
2. **Document patterns**: Add common patterns to this guide
3. **Train team**: Share migration guide with team
4. **Monitor metrics**: Track test reliability and execution time
5. **Iterate**: Refactor page objects based on feedback

---

## References

- [POM Architecture Design](./pom-architecture-design.md)
- [POM Coding Standards](./pom-coding-standards.md)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [Issue #843](https://github.com/meepleai/meepleai-monorepo/issues/843)
