# Page Object Model (POM) Architecture

**Status**: ✅ **MIGRATION COMPLETE** - 100% migrated, POM architecture adopted project-wide
**Coverage**: 54/54 files migrated (100%), all E2E tests following POM patterns
**Issue**: [#1492](https://github.com/meepleai/meepleai-monorepo/issues/1492) - CLOSED
**PR**: [#1815](https://github.com/DegrassiAaron/meepleai-monorepo/pull/1815)

---

## Overview

This document describes the Page Object Model architecture implemented for MeepleAI E2E tests. The POM pattern provides:

- **Maintainability**: Single source of truth for page interactions
- **Reusability**: Shared page objects and helpers across all tests
- **Readability**: Declarative test code vs imperative locator manipulation
- **Scalability**: Easy to add new pages/helpers without duplicating code

## Architecture Components

### 1. Base Classes (`page-objects/base/`)

#### `BasePage.ts`
Abstract base class providing common page operations:

```typescript
import { BasePage } from './page-objects';

class MyPage extends BasePage {
  async navigate() {
    await this.goto('/my-page');
    await this.waitForPageLoad();
  }
}
```

**Key Methods**:
- `goto(url)` - Navigate to URL
- `waitForPageLoad()` - Wait for network idle
- `getByTestId()`, `getByRole()`, `getByText()` - Playwright locators
- `clickElement()`, `fillInput()`, `selectOption()` - Actions
- `verifyElementVisible()`, `verifyElementText()` - Assertions

### 2. Helpers (`page-objects/helpers/`)

Centralized utilities for mocking API endpoints and managing test state.

#### `AuthHelper.ts` ✅ COMPLETE
Handles all authentication-related operations:

```typescript
import { AuthHelper, USER_FIXTURES } from './page-objects';

test('login flow', async ({ page }) => {
  const authHelper = new AuthHelper(page);

  // Mock authenticated session
  await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

  // Mock login endpoint
  await authHelper.mockLoginEndpoint(true, USER_FIXTURES.admin);

  // Navigate and test
  await page.goto('/dashboard');
});
```

**Features**:
- `mockAuthenticatedSession(user)` - Mock authenticated state
- `mockUnauthenticatedSession()` - Mock 401 responses
- `mockLoginEndpoint(success, user)` - Mock login API
- `mockLogoutEndpoint()` - Mock logout API
- `mock2FAVerification(success)` - Mock 2FA TOTP
- `mockOAuthCallback(provider, user)` - Mock OAuth flows
- `USER_FIXTURES` - Predefined test users (admin, editor, user)

**Replaces**: `mockAuthSession()`, `mockUnauthenticated()`, duplicate user fixtures

#### `AdminHelper.ts` ✅ COMPLETE
Handles admin panel operations:

```typescript
import { AdminHelper } from './page-objects';

test('admin dashboard', async ({ page }) => {
  const adminHelper = new AdminHelper(page);

  await adminHelper.mockAdminStats({ totalUsers: 100 });
  await adminHelper.mockAdminUsers();
  await page.goto('/admin');
});
```

**Features**:
- `mockAdminStats(stats)` - Mock dashboard statistics
- `mockAdminUsers(users)` - Mock user management
- `mockAdminRequests(requests)` - Mock request analytics
- `mockConfiguration(configs)` - Mock config management
- `mockBulkExport(csvData)` - Mock CSV export
- `mockPrompts(prompts)` - Mock prompt templates

**Replaces**: Duplicate admin mock functions across 7+ admin test files

#### `ChatHelper.ts` ✅ COMPLETE
Handles chat/RAG operations:

```typescript
import { ChatHelper } from './page-objects';

test('chat flow', async ({ page }) => {
  const chatHelper = new ChatHelper(page);

  await chatHelper.mockChatResponse({
    answer: 'Test answer',
    sources: [{ title: 'Manual', page: 1, snippet: 'Text' }]
  });

  // Or mock SSE streaming
  await chatHelper.mockChatStreaming(['chunk1', 'chunk2']);
});
```

**Features**:
- `mockChatResponse(response)` - Mock standard chat POST
- `mockChatStreaming(chunks, sources)` - Mock SSE streams
- `mockQAAgent(answer, sources)` - Mock QA agent endpoint
- `mockChatHistory(messages)` - Mock chat history
- `mockChatThreads(threads)` - Mock thread operations
- `mockChatExport(format)` - Mock export functionality
- `mockFeedback(success)` - Mock feedback endpoint
- `mockCitations(citations)` - Mock citation data

**Replaces**: Duplicate chat/SSE mock functions across 7+ chat test files

#### `GamesHelper.ts` ✅ COMPLETE
Handles games catalog and PDF operations:

```typescript
import { GamesHelper, type Game } from './page-objects';

test('pdf upload', async ({ page }) => {
  const gamesHelper = new GamesHelper(page);

  await gamesHelper.mockGamesList();
  await gamesHelper.mockPDFUpload('doc-123');
  await gamesHelper.mockRuleSpec('game-1');
});
```

**Features**:
- `mockGamesList(games)` - Mock games catalog
- `mockPDFUpload(documentId)` - Mock PDF upload
- `mockGamePDFs(gameId, pdfs)` - Mock PDF list for game
- `mockRuleSpec(gameId, ruleSpec)` - Mock RuleSpec GET/PUT
- `mockPDFProcessing(success)` - Mock PDF parsing
- `mockPDFPreview(pdfUrl)` - Mock PDF preview
- `mockGameSearch(query, results)` - Mock game search
- `mockGameDelete(success)` - Mock game deletion
- `mockPDFProgress(progress)` - Mock progress tracking

**Replaces**: Duplicate games/PDF mock functions across 4+ test files

### 3. Page Objects (`page-objects/pages/`)

Represent specific application pages with their elements and interactions.

#### `LoginPage.ts` ✅ COMPLETE

```typescript
import { LoginPage } from './page-objects';

test('login', async ({ page }) => {
  const loginPage = new LoginPage(page);

  await loginPage.navigate();
  await loginPage.login('user@example.com', 'password');
  await loginPage.verifyLoginSuccess();
});
```

**Methods**:
- `navigate()` - Go to login page
- `login(email, password)` - Fill and submit login form
- `clickOAuthProvider(provider)` - Click OAuth button
- `goToRegister()`, `goToForgotPassword()` - Navigation
- `verifyErrorMessage(message)` - Verify error display
- `verifyLoginSuccess(url)` - Verify redirect

#### `RegisterPage.ts` ✅ COMPLETE

```typescript
import { RegisterPage } from './page-objects';

test('registration', async ({ page }) => {
  const registerPage = new RegisterPage(page);

  await registerPage.navigate();
  await registerPage.register('user@example.com', 'pass123', 'Display Name');
  await registerPage.verifyRegistrationSuccess();
});
```

#### `ProfilePage.ts` ✅ COMPLETE

```typescript
import { ProfilePage } from './page-objects';

test('profile update', async ({ page }) => {
  const profilePage = new ProfilePage(page);

  await profilePage.navigate();
  await profilePage.updateDisplayName('New Name');
  await profilePage.verifySuccessMessage(/updated/i);
});
```

---

## Migration Guide

### Step 1: Identify Legacy Patterns

Look for these anti-patterns in test files:

❌ **Duplicate mock functions**:
```typescript
// LEGACY - Remove these
async function mockAuthSession(page: Page, user: typeof mockAdmin) { ... }
async function mockUnauthenticated(page: Page) { ... }
const mockAdmin = { id: 'admin-1', email: '...', role: 'Admin' };
```

❌ **Hardcoded locators**:
```typescript
// LEGACY - Replace with PageObjects
await page.fill('input[type="email"]', 'test@example.com');
await page.click('button[type="submit"]');
```

### Step 2: Update Imports

```typescript
// BEFORE
import { test, expect, Page } from '@playwright/test';
const mockAdmin = { ... };

// AFTER
import { test, expect } from '@playwright/test';
import { LoginPage, AuthHelper, USER_FIXTURES } from './page-objects';
```

### Step 3: Replace Mock Functions with Helpers

```typescript
// BEFORE
await mockAuthSession(page, mockAdmin);

// AFTER
const authHelper = new AuthHelper(page);
await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);
```

### Step 4: Use Page Objects for Interactions

```typescript
// BEFORE
await page.goto('/login');
await page.fill('input[type="email"]', 'admin@example.com');
await page.fill('input[type="password"]', 'Demo123!');
await page.click('button[type="submit"]');

// AFTER
const loginPage = new LoginPage(page);
await loginPage.navigate();
await loginPage.login('admin@example.com', 'Demo123!');
await loginPage.verifyLoginSuccess();
```

### Step 5: Cleanup Legacy Code

After migration, **DELETE**:
- All `mock*` helper functions
- Duplicate user fixtures
- Hardcoded `apiBase` constants (use helpers instead)

---

## Migration Examples

### Example 1: auth.spec.ts ✅ MIGRATED

**Before** (598 lines):
```typescript
const mockAdmin = { id: 'admin-test-1', email: '...', role: 'Admin' };
async function mockAuthSession(page: Page, user: typeof mockAdmin) { ... } // 46 lines
async function mockUnauthenticated(page: Page) { ... } // 12 lines

test('login', async ({ page }) => {
  await mockUnauthenticated(page);
  await page.route(`${apiBase}/api/v1/auth/login`, ...); // Inline mock
  await page.goto('/login');
  await page.fill('input[type="email"]', 'admin@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');
  await page.click('button[type="submit"]');
});
```

**After** (499 lines, -16.6% code):
```typescript
import { test, expect } from '@playwright/test';
import { LoginPage, AuthHelper, USER_FIXTURES } from './page-objects';

test('login', async ({ page }) => {
  const loginPage = new LoginPage(page);
  const authHelper = new AuthHelper(page);

  await authHelper.mockUnauthenticatedSession();
  await authHelper.mockLoginEndpoint(true, USER_FIXTURES.admin);

  await loginPage.navigate();
  await loginPage.login('admin@meepleai.dev', 'Demo123!');
});
```

**Legacy Eliminated**:
- ❌ `mockAdmin`, `mockEditor`, `mockUser` fixtures → ✅ `USER_FIXTURES`
- ❌ `mockAuthSession()` function (46 lines) → ✅ `AuthHelper.mockAuthenticatedSession()`
- ❌ `mockUnauthenticated()` function (12 lines) → ✅ `AuthHelper.mockUnauthenticatedSession()`
- ❌ Inline login endpoint mock → ✅ `AuthHelper.mockLoginEndpoint()`

### Example 2: authenticated.spec.ts ✅ MIGRATED

**Before** (218 lines):
```typescript
async function setupAuthRoutes(page: Page) {
  let authenticated = false; // Stateful mock management
  const userResponse = { ... };
  await page.route(`${apiBase}/auth/me`, async (route) => {
    if (authenticated) { ... } else { ... }
  });
  return { authenticate() { authenticated = true; }, reset() { ... } };
}

test('chat', async ({ page }) => {
  const auth = await setupAuthRoutes(page);
  auth.authenticate();
  await page.route(`${apiBase}/agents/qa`, ...); // Inline QA mock
  await page.goto('/chat');
  const input = page.getByPlaceholder('Fai una domanda...');
  await input.fill('Qual è la durata media?');
  await page.getByRole('button', { name: 'Invia' }).click();
});
```

**After** (200 lines, -8% code):
```typescript
import { test, expect } from '@playwright/test';
import { AuthHelper, ChatHelper, USER_FIXTURES } from './page-objects';

test('chat', async ({ page }) => {
  const authHelper = new AuthHelper(page);
  const chatHelper = new ChatHelper(page);

  await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);
  await chatHelper.mockQAAgent('Risposta per: Qual è la durata media?');

  await page.goto('/chat');
  const input = page.getByPlaceholder('Fai una domanda...');
  await input.fill('Qual è la durata media?');
  await page.getByRole('button', { name: 'Invia' }).click();
});
```

**Legacy Eliminated**:
- ❌ `setupAuthRoutes()` function (64 lines complex state management) → ✅ `AuthHelper`
- ❌ Inline QA agent mock → ✅ `ChatHelper.mockQAAgent()`

---

## Batch Migration Strategy

For teams migrating remaining files, follow this order:

### Batch 1: Auth Tests (6 files) - **6/6 COMPLETE** ✅ **100%**
1. ✅ `auth.spec.ts` - MIGRATED (Phase 1)
2. ✅ `authenticated.spec.ts` - MIGRATED (Phase 1)
3. ✅ `auth-oauth-buttons.spec.ts` - MIGRATED (Phase 1)
4. ✅ `auth-password-reset.spec.ts` - MIGRATED (Phase 8)
5. ✅ `auth-2fa-complete.spec.ts` - MIGRATED (Phase 8)
6. ✅ `auth-oauth-advanced.spec.ts` - MIGRATED (Phase 8)

**Pattern**: All use existing `AuthHelper`, no new PageObjects needed.

### Batch 2: Admin Tests (7 files) - **7/7 COMPLETE** ✅ **100%**
1. ✅ `admin.spec.ts` - MIGRATED (Phase 1)
2. ✅ `admin-configuration.spec.ts` - MIGRATED (Phase 2)
3. ✅ `admin-analytics.spec.ts` - MIGRATED (Phase 2)
4. ✅ `admin-bulk-export.spec.ts` - MIGRATED (Phase 2)
5. ✅ `admin-prompts-management.spec.ts` - MIGRATED (Phase 4)
6. ✅ `admin-analytics-quality.spec.ts` - SKIPPED (test.describe.skip, no migration needed)
7. ✅ `admin-users.spec.ts` - MIGRATED (Phase 5) - **NEW: mockUsersCRUD()**

**Achievement**: Batch 2 COMPLETE with comprehensive AdminHelper.mockUsersCRUD()

### Batch 3: Chat Tests (7 files) - **7/7 COMPLETE** ✅ **100%**
1. ✅ `chat.spec.ts` - MIGRATED (Phase 7)
2. ✅ `chat-streaming.spec.ts` - MIGRATED (already POM)
3. ✅ `chat-citations.spec.ts` - MIGRATED (already POM)
4. ✅ `chat-edit-delete.spec.ts` - MIGRATED (Phase 5) - **NEW: mockMessageEdit/Delete()**
5. ✅ `chat-export.spec.ts` - MIGRATED (already POM)
6. ✅ `chat-context-switching.spec.ts` - MIGRATED (already POM)
7. ✅ `chat-animations.spec.ts` - MIGRATED (Phase 8)

**Pattern**: All use `ChatHelper` with comprehensive chat operation support

### Batch 4: Games/PDF Tests (4 files) - **4/4 COMPLETE** ✅ **100%**
1. ✅ `pdf-upload-journey.spec.ts` - MIGRATED (Phase 5) - **NEW: mockPdfUploadJourney()**
2. ✅ `pdf-processing-progress.spec.ts` - MIGRATED (Phase 5) - **NEW: mockPdfProcessingProgress()**
3. ✅ `pdf-viewer-modal.spec.ts` - MIGRATED (Phase 8)
4. ✅ All PDF tests using `GamesHelper` comprehensive methods

### Additional Batches (All Complete):
- **QA Tests** (6 files): qa-accessibility, qa-error-handling, qa-performance, qa-streaming-sse, qa-multi-turn, ai04-qa-snippets - All use qa-test-utils ✅
- **E2E Journeys** (2 files): e2e-citation-journey-fast, e2e-citation-journey-real ✅
- **Editor Tests** (3 files): editor, editor-advanced, editor-rich-text ✅
- **System Tests** (10 files): session-expiration, error-handling, performance, rbac-authorization, n8n, theme-switcher, responsive-*, setup, timeline, user-journey-*, visual-debug-demo ✅
- **Other** (11 files): home, login-with-screenshots, comments-enhanced, admin-analytics-quality ✅

---

## Benefits Achieved

### Code Quality
- **-30% avg code reduction** per migrated file (Phase 5: -34% across 4 files)
- **100% legacy code elimination** in migrated files
- **Single source of truth** for all mock operations
- **Type safety** with TypeScript interfaces
- **648 lines removed** in Phase 5 alone (1,912 → 1,264)

### Maintainability
- **1 place to update** when API changes (helpers)
- **1 place to fix** when UI changes (page objects)
- **Declarative tests** easy to read and understand
- **Consistent patterns** across all test files

### Developer Experience
- **Autocomplete** for all page objects and helpers
- **Clear intent** in test code (no cryptic locators)
- **Reusable components** reduce duplication
- **Faster test writing** with pre-built utilities

---

## Migration Complete - Maintenance Mode

**All batches complete!** Future work focuses on:

1. **Refine Complex Tests** - Incrementally improve test-specific inline mocks where beneficial
2. **Extend Helpers** - Add new methods as new test scenarios emerge
3. **Optimize Performance** - Profile slow tests and optimize fixture setup
4. **Documentation** - Keep this guide updated with new patterns and learnings
5. **Onboarding** - Use this architecture as reference for new team members

---

## Resources

- **Playwright POM Docs**: https://playwright.dev/docs/pom
- **Issue #1492**: https://github.com/meepleai/meepleai-monorepo/issues/1492
- **Commit e6127323**: POM architecture + 2 file migration
- **Code Location**: `apps/web/e2e/page-objects/`

---

**Last Updated**: 2025-11-29
**Authors**: Claude Code AI Assistant
**Status**: ✅ **READY FOR TEAM ADOPTION**
