# E2E Test Contribution Guide

**Version**: 1.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Related**: Issue #843 E2E Test Expansion

## Quick Start

### Adding a New E2E Test

1. **Identify the user journey** to test
2. **Check if Page Object exists** in `apps/web/e2e/pages/`
3. **Create or extend Page Object** if needed
4. **Write test** in `apps/web/e2e/[feature].spec.ts`
5. **Run locally** with `pnpm test:e2e [filename]`
6. **Verify pass rate** (target: 90%+ for new tests)

### Example: Adding Chat Feature Test

```typescript
// 1. Extend ChatPage if needed (apps/web/e2e/pages/chat/ChatPage.ts)
export class ChatPage extends BasePage {
  async sendMessage(message: string) {
    await this.fill(this.page.getByLabel(/message/i), message);
    await this.click(this.page.getByRole('button', { name: /send/i }));
  }
}

// 2. Create test file (apps/web/e2e/chat-feature.spec.ts)
import { test, expect } from '@playwright/test';
import { ChatPage } from './pages/chat/ChatPage';

test.describe('Chat Feature', () => {
  let chatPage: ChatPage;

  test.beforeEach(async ({ page }) => {
    chatPage = new ChatPage(page);
    await chatPage.goto();
  });

  test('should send message successfully', async ({ page }) => {
    await chatPage.sendMessage('Hello world');
    await chatPage.assertMessageSent('Hello world');
  });
});
```

## Page Object Model (POM) Standards

### When to Create New Page Object

✅ **Create New**:
- New page or feature area (e.g., `/settings`, `/admin/reports`)
- Distinct UI component with multiple interactions
- Reusable across 3+ tests

❌ **Extend Existing**:
- Small addition to existing page
- Single new interaction method
- Related to existing page object

### Page Object Checklist

- [ ] Extends `BasePage`
- [ ] Implements corresponding interface from `types/pom-interfaces.ts`
- [ ] All selectors use accessibility-first approach (getByRole, getByLabel)
- [ ] Methods grouped logically (navigation, actions, queries, assertions)
- [ ] JSDoc comments for all public methods
- [ ] Type-safe parameters and return types
- [ ] No test logic in page object (only interactions)
- [ ] Locators as class properties (reusable, maintainable)

## Test Quality Standards

### Test Independence

**✅ DO**:
```typescript
test.beforeEach(async ({ page }) => {
  // Create fresh test data
  await setupTestUser();
  await loginAsTestUser(page);
});

test.afterEach(async () => {
  // Clean up test data
  await deleteTestUser();
});
```

**❌ DON'T**:
```typescript
// Shared state across tests
let sharedUserId;

test('first test', async () => {
  sharedUserId = await createUser(); // Bad: affects other tests
});

test('second test', async () => {
  await useUser(sharedUserId); // Bad: depends on first test
});
```

### Selector Strategy

**Priority Order**:
1. **getByRole** (best - accessibility-first)
2. **getByLabel** (good - form inputs)
3. **getByPlaceholder** (acceptable - inputs without label)
4. **getByTestId** (last resort - add `data-testid` to UI)

**✅ Good Selectors**:
```typescript
page.getByRole('button', { name: /submit/i })
page.getByLabel(/email address/i)
page.getByRole('heading', { name: /welcome/i })
```

**❌ Avoid**:
```typescript
page.locator('.btn-primary') // CSS class (fragile)
page.locator('#submit-btn') // ID (coupling)
page.locator('button').first() // Position-based (fragile)
```

### Wait Strategies

**✅ Use Playwright Auto-Waiting**:
```typescript
// Playwright waits automatically
await page.click('button');
await page.fill('input', 'value');
await expect(element).toBeVisible();
```

**❌ Avoid Explicit Sleeps**:
```typescript
await page.waitForTimeout(5000); // Bad: arbitrary delay
```

**✅ Use Explicit Waits When Needed**:
```typescript
// Wait for specific condition
await page.waitForSelector('[data-loaded="true"]');
await page.waitForResponse(resp => resp.url().includes('/api/'));
await expect(element).toBeVisible({ timeout: 15000 }); // Long operation
```

## Common Patterns

### Authentication

```typescript
import { mockAuth } from './fixtures/auth';

test.beforeEach(async ({ page, context }) => {
  // Mock authentication
  await mockAuth(context, { role: 'Admin', email: 'admin@test.com' });
  await page.goto('/admin');
});
```

### Form Submission

```typescript
async submitForm(data: FormData) {
  await this.fill(this.emailInput, data.email);
  await this.fill(this.passwordInput, data.password);
  await this.click(this.submitButton);

  // Wait for success indicator
  await this.waitForElement(this.successMessage);
}
```

### API Mocking

```typescript
test.beforeEach(async ({ page }) => {
  // Mock API responses
  await page.route('**/api/v1/games', route => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({ games: [/* mock data */] })
    });
  });
});
```

### Error Scenarios

```typescript
test('should handle network error gracefully', async ({ page }) => {
  // Mock network failure
  await page.route('**/api/**', route => route.abort('failed'));

  await chatPage.sendMessage('test');

  // Verify error handling
  await expect(page.getByText(/network error/i)).toBeVisible();
});
```

## Debugging Failed Tests

### 1. Run Test in UI Mode
```bash
pnpm exec playwright test --ui [test-file]
```

### 2. Generate Trace
```bash
pnpm exec playwright test [test-file] --trace on
pnpm exec playwright show-trace trace.zip
```

### 3. Check Screenshots
Failed tests automatically capture screenshots in `test-results/`

### 4. Enable Debug Logs
```typescript
test('debug test', async ({ page }) => {
  page.on('console', msg => console.log('PAGE LOG:', msg.text()));
  page.on('pageerror', err => console.log('PAGE ERROR:', err));

  // Test code...
});
```

## Performance Best Practices

### Keep Tests Fast

**✅ DO**:
- Use mocks instead of real API calls
- Minimize page.goto() calls (reuse page state)
- Use `fullyParallel: true` (configured in playwright.config.ts)
- Set appropriate timeouts (don't wait longer than necessary)

**❌ DON'T**:
- Make real external API calls
- Use `page.waitForTimeout()` unnecessarily
- Create large test data sets
- Run tests sequentially if they can be parallel

**Target**: Tests should complete in <10 seconds each

### Flaky Test Prevention

1. **Wait for network idle** before assertions:
   ```typescript
   await page.waitForLoadState('networkidle');
   ```

2. **Use stable selectors** (getByRole > CSS classes)

3. **Avoid timing assumptions**:
   ```typescript
   // Bad: await page.waitForTimeout(1000);
   // Good: await expect(element).toBeVisible();
   ```

4. **Retry on flakiness**:
   Configured in `playwright.config.ts`: `retries: process.env.CI ? 2 : 0`

## Documentation Requirements

### Test File Header

```typescript
/**
 * E2E Tests: [Feature Name]
 *
 * Coverage:
 * - User journey: [Journey description]
 * - Priority: P1/P2/P3
 * - Pass rate target: 90%+
 *
 * Related:
 * - Issue: #[number]
 * - Page Object: [PageName]
 * - API: [Endpoints tested]
 */
```

### Test Descriptions

```typescript
test.describe('Feature Area', () => {
  test('should [expected behavior] when [condition]', async ({ page }) => {
    // Arrange: Set up test data and state

    // Act: Perform user action

    // Assert: Verify expected outcome
  });
});
```

## Code Review Checklist

Before submitting PR with E2E tests:

- [ ] All tests use Page Object Model (no direct page interactions in tests)
- [ ] Tests are independent (can run in any order)
- [ ] Accessibility-first selectors (getByRole, getByLabel)
- [ ] No `page.waitForTimeout()` or arbitrary delays
- [ ] API responses properly mocked
- [ ] Test data created and cleaned up in beforeEach/afterEach
- [ ] All tests passing locally (90%+ pass rate)
- [ ] TypeScript compilation passing (`pnpm typecheck`)
- [ ] JSDoc comments for new Page Object methods
- [ ] Error scenarios covered (not just happy paths)
- [ ] Edge cases tested (empty states, large data, etc.)

## Common Pitfalls

### 1. Shared State

❌ **Wrong**:
```typescript
let userId = 'shared-user-123'; // Shared across all tests

test('test 1', async () => {
  await updateUser(userId); // Affects test 2
});

test('test 2', async () => {
  const user = await getUser(userId); // Depends on test 1
});
```

✅ **Right**:
```typescript
test('test 1', async () => {
  const userId = await createTestUser(); // Isolated
  await updateUser(userId);
  await deleteTestUser(userId); // Cleanup
});

test('test 2', async () => {
  const userId = await createTestUser(); // Independent
  const user = await getUser(userId);
  await deleteTestUser(userId);
});
```

### 2. Force Clicks

❌ **Wrong**:
```typescript
await page.click('button', { force: true }); // Bypasses actionability checks
```

✅ **Right**:
```typescript
await expect(page.getByRole('button')).toBeVisible(); // Wait for visibility
await page.getByRole('button').click(); // Actionable click
```

### 3. CSS Selectors

❌ **Wrong**:
```typescript
await page.locator('.submit-button').click(); // Fragile
```

✅ **Right**:
```typescript
await page.getByRole('button', { name: /submit/i }).click(); // Semantic
```

## Getting Help

**Documentation**:
- POM Architecture: `docs/testing/pom-architecture-design.md`
- Migration Guide: `docs/testing/pom-migration-guide.md`
- Coding Standards: `docs/testing/pom-coding-standards.md`
- E2E README: `apps/web/e2e/README.md`

**Examples**:
- Auth flows: `e2e/auth-2fa-complete.spec.ts` (comprehensive)
- Admin features: `e2e/admin-prompts-management.spec.ts` (Monaco editor)
- Form interactions: `e2e/auth-password-reset.spec.ts` (validation)

**Tools**:
- Playwright Docs: https://playwright.dev/
- Playwright Inspector: `pnpm exec playwright test --debug`
- Trace Viewer: `pnpm exec playwright show-trace trace.zip`

---

**Remember**: Write tests that verify real user value, not implementation details. Focus on user journeys, not code coverage.

🚀 Happy testing!

