# Playwright Best Practices - MeepleAI

**Purpose**: Consolidated best practices, patterns, and utilities for E2E testing with Playwright.

**Related**: Issue #2919

## Table of Contents
- [Configuration Overview](#configuration-overview)
- [Page Object Model (POM)](#page-object-model-pom)
- [Test Helpers & Utilities](#test-helpers--utilities)
- [Parallel Execution & Retry Logic](#parallel-execution--retry-logic)
- [Authentication Patterns](#authentication-patterns)
- [Critical E2E Patterns](#critical-e2e-patterns)
- [Example Tests](#example-tests)

---

## Configuration Overview

### Browser Projects (6 Total)
```typescript
// playwright.config.ts - Issue #1497
projects: [
  { name: 'desktop-chrome', viewport: { width: 1920, height: 1080 } },
  { name: 'desktop-firefox', viewport: { width: 1920, height: 1080 } },
  { name: 'desktop-safari', viewport: { width: 1920, height: 1080 } },
  { name: 'mobile-chrome', viewport: { width: 390, height: 844 } },
  { name: 'mobile-safari', viewport: { width: 390, height: 844 } },
  { name: 'tablet-chrome', viewport: { width: 1024, height: 1366 } },
]
```

### Execution Strategy
- **Local**: `fullyParallel: true`, `workers: 2` (fast feedback)
- **CI**: `fullyParallel: false`, `workers: 1` (stability, axe-core race condition prevention)
- **Retry**: `2 in CI`, `0 local` (CI transient failures only)

### Timeout Configuration
```typescript
timeout: process.env.CI === 'true' ? 90000 : 60000, // 90s CI, 60s local
actionTimeout: 10000,     // 10s for clicks/fills
navigationTimeout: 60000, // 60s for page.goto
```

### Coverage Reporting (Issue #1498)
```bash
# E2E code coverage via @bgotink/playwright-coverage
pnpm test:e2e:coverage        # Run with coverage
pnpm test:e2e:coverage:report # View HTML report
```

**Watermarks** (Issue #1498: conservative start):
- Statements/Functions/Branches/Lines: 30% low, 60% high

---

## Page Object Model (POM)

### Base Page Pattern
**File**: `e2e/pages/base/BasePage.ts`

```typescript
import { Page, Locator, expect } from '@playwright/test';

export abstract class BasePage {
  constructor(public readonly page: Page) {}

  // Must be implemented by subclasses
  abstract goto(): Promise<void>;

  // Common utilities provided
  async waitForLoad(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }

  async waitForElement(locator: Locator, options = {}): Promise<void> {
    await expect(locator).toBeVisible({ timeout: options.timeout || 10000 });
  }

  async screenshot(name: string): Promise<void> {
    await this.page.screenshot({ path: `e2e-screenshots/${name}.png`, fullPage: true });
  }
}
```

### Example: Admin Dashboard Page
**File**: `e2e/pages/admin/AdminPage.ts`

```typescript
import { Locator, expect } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export class AdminPage extends BasePage {
  // Locators (lazy initialization)
  readonly serviceStatusCard: Locator;
  readonly apiHealthIndicator: Locator;

  constructor(page: Page) {
    super(page);
    this.serviceStatusCard = page.getByTestId('service-status-card');
    this.apiHealthIndicator = page.getByTestId('api-health');
  }

  async goto(): Promise<void> {
    await this.page.goto('/admin/dashboard');
    await this.waitForLoad();
  }

  async waitForServiceStatus(): Promise<void> {
    await this.waitForElement(this.serviceStatusCard);
  }

  async getApiHealthStatus(): Promise<string> {
    return await this.apiHealthIndicator.textContent() || '';
  }
}
```

### Page Object Hierarchy
```
e2e/pages/
├── base/
│   └── BasePage.ts           # Foundation for all page objects
├── admin/
│   └── AdminPage.ts          # Admin dashboard page
├── auth/
│   ├── LoginPage.ts          # Login page
│   ├── RegisterPage.ts       # Registration page
│   └── ProfilePage.ts        # User profile page
├── chat/
│   └── ChatPage.ts           # Chat interface page
├── game/
│   ├── GamePage.ts           # Game details page
│   └── SharedGameCatalogPage.ts
├── home/
│   └── HomePage.ts           # Landing page
├── upload/
│   └── UploadPage.ts         # PDF upload page
└── helpers/
    ├── AdminHelper.ts        # Admin workflow utilities
    ├── AuthHelper.ts         # Auth workflow utilities
    ├── ChatHelper.ts         # Chat workflow utilities
    └── GamesHelper.ts        # Game workflow utilities
```

---

## Test Helpers & Utilities

### 1. WaitHelper - Intelligent Waiting Strategies
**File**: `e2e/helpers/WaitHelper.ts`

```typescript
import { Page, expect } from '@playwright/test';

export class WaitHelper {
  constructor(private readonly page: Page) {}

  async waitForElementVisible(selector: string, timeout = 10000): Promise<void> {
    await expect(this.page.locator(selector)).toBeVisible({ timeout });
  }

  async waitForElementHidden(selector: string, timeout = 10000): Promise<void> {
    await expect(this.page.locator(selector)).not.toBeVisible({ timeout });
  }

  async waitForTextContent(selector: string, text: string): Promise<void> {
    await expect(this.page.locator(selector)).toContainText(text);
  }

  async waitForNetworkIdle(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }
}
```

### 2. Responsive Utilities - Viewport Testing
**File**: `e2e/helpers/responsive-utils.ts`

```typescript
import { Page, expect } from '@playwright/test';

export async function setViewport(page: Page, viewport: 'mobile' | 'tablet' | 'desktop') {
  const viewports = {
    mobile: { width: 390, height: 844 },
    tablet: { width: 1024, height: 1366 },
    desktop: { width: 1920, height: 1080 },
  };
  await page.setViewportSize(viewports[viewport]);
}

export async function isMobileViewport(page: Page): Promise<boolean> {
  const viewport = page.viewportSize();
  return viewport ? viewport.width < 768 : false;
}
```

### 3. Assertions - Domain-Specific Checks
**File**: `e2e/helpers/assertions.ts`

```typescript
import { Page, expect } from '@playwright/test';

export async function assertSuccessToast(page: Page, message: string): Promise<void> {
  const toast = page.locator('[role="status"]', { hasText: message });
  await expect(toast).toBeVisible({ timeout: 5000 });
}

export async function assertErrorMessage(page: Page, message: string): Promise<void> {
  const error = page.locator('[role="alert"]', { hasText: message });
  await expect(error).toBeVisible({ timeout: 5000 });
}
```

### 4. Mocks - API Response Mocking
**File**: `e2e/helpers/mocks.ts`

```typescript
import { Page } from '@playwright/test';

export async function mockApiSuccess(page: Page, endpoint: string, data: any): Promise<void> {
  await page.route(`**${endpoint}`, route => {
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
  });
}

export async function mockApiError(page: Page, endpoint: string, status = 500): Promise<void> {
  await page.route(`**${endpoint}`, route => {
    route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ error: 'Mock error' }) });
  });
}
```

---

## Parallel Execution & Retry Logic

### Running Tests in Parallel
```bash
# Run all tests in parallel (local default)
pnpm test:e2e

# Run tests in shards (CI parallelization)
pnpm test:e2e:shard1  # Shard 1/4
pnpm test:e2e:shard2  # Shard 2/4
pnpm test:e2e:shard3  # Shard 3/4
pnpm test:e2e:shard4  # Shard 4/4

# Run in parallel with custom script
pnpm test:e2e:parallel
```

### Retry Strategy (Issue #2008)
```typescript
// playwright.config.ts
retries: process.env.CI === 'true' ? 2 : 0,
```

**Rationale**:
- **CI**: 2 retries for transient network/timeout failures
- **Local**: 0 retries for fast feedback on failures

### Test Groups (Organized Execution)
```bash
# Run specific test groups
pnpm test:e2e --grep @smoke     # Critical paths only
pnpm test:e2e --grep @admin     # Admin dashboard tests
pnpm test:e2e --grep @auth      # Authentication flows
pnpm test:e2e --grep @regression # Full regression suite
```

---

## Authentication Patterns

### 1. API Authentication (Recommended)
**File**: `e2e/fixtures/auth.ts`

```typescript
import { Page } from '@playwright/test';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export async function authenticateViaAPI(
  page: Page,
  email: string,
  password: string
): Promise<boolean> {
  try {
    const response = await page.request.post(`${API_BASE}/api/v1/auth/login`, {
      headers: { 'Content-Type': 'application/json' },
      data: { email, password },
    });

    if (response.ok()) {
      console.log('✅ Authentication successful via API');
      return true;
    }
    return false;
  } catch (error) {
    console.error('❌ Authentication error:', error);
    return false;
  }
}
```

**Usage**:
```typescript
import { test } from '@playwright/test';
import { authenticateViaAPI } from './fixtures/auth';

test('admin dashboard', async ({ page }) => {
  await authenticateViaAPI(page, 'admin@example.com', 'password');
  await page.goto('/admin/dashboard');
  // Test authenticated content
});
```

### 2. Role-Based Testing
**File**: `e2e/fixtures/roles.ts`

```typescript
import { test as base, Page } from '@playwright/test';

type RoleFixture = {
  adminPage: Page;
  userPage: Page;
};

export const test = base.extend<RoleFixture>({
  adminPage: async ({ browser }, use) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await authenticateViaAPI(page, 'admin@example.com', 'password');
    await use(page);
    await context.close();
  },

  userPage: async ({ browser }, use) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await authenticateViaAPI(page, 'user@example.com', 'password');
    await use(page);
    await context.close();
  },
});

export { expect } from '@playwright/test';
```

**Usage**:
```typescript
import { test, expect } from './fixtures/roles';

test('admin can access admin panel', async ({ adminPage }) => {
  await adminPage.goto('/admin');
  await expect(adminPage.locator('h1')).toContainText('Admin Dashboard');
});

test('user cannot access admin panel', async ({ userPage }) => {
  await userPage.goto('/admin');
  await expect(userPage).toHaveURL(/\/unauthorized/);
});
```

---

## Critical E2E Patterns

### Pattern 1: Response Timing (Most Critical)
```typescript
// ✅ CORRECT: Set up listener BEFORE action
const responsePromise = page.waitForResponse(url => url.includes('/api/chat'));
await sendButton.click();
const response = await responsePromise;

// ❌ WRONG: Listener after action - response missed
await sendButton.click();
const response = await page.waitForResponse(url => url.includes('/api/chat'));
```

**Rationale**: Playwright's `waitForResponse` must be set up BEFORE the action that triggers the request, otherwise the response will be missed.

### Pattern 2: React Navigation Fallback
```typescript
// Wait for React navigation with fallback
await page.waitForTimeout(3000);
if (!page.url().includes('/dashboard')) {
  // Fallback to full page navigation if React routing fails
  await page.goto('/dashboard');
  await page.waitForLoadState('networkidle');
}
```

**Rationale**: React client-side routing can sometimes fail in E2E tests. Always provide a fallback to full page navigation.

### Pattern 3: Mobile Viewport Handling
```typescript
// Check if UI element is hidden on mobile
const isMobile = page.viewportSize()!.width < 768;

if (isMobile) {
  // Use API for actions when UI is hidden (hidden md:flex patterns)
  await page.request.post('/api/action', { data: { ... } });
} else {
  // Use UI element for actions
  await page.getByRole('button', { name: 'Action' }).click();
}
```

**Rationale**: Mobile viewports may hide certain UI elements (e.g., `hidden md:flex`). Use `page.request` API for actions when UI elements are not visible.

### Pattern 4: Cookie Sync with page.request
```typescript
// page.request doesn't sync cookies with browser automatically
await page.request.post('/api/logout', { headers: { Cookie: cookies } });

// Manual cookie sync required
await context.clearCookies();
await page.goto('/login');
```

**Rationale**: `page.request` API does not automatically sync cookies with the browser context. Always manually clear cookies after API calls that modify session state.

### Pattern 5: CORS Avoidance
```typescript
// ✅ CORRECT: Use page.request to bypass CORS
await page.request.get('https://external-api.com/data');

// ❌ WRONG: page.evaluate(fetch) triggers CORS errors
await page.evaluate(() => fetch('https://external-api.com/data'));
```

**Rationale**: Playwright's `page.request` API bypasses browser CORS restrictions, while `page.evaluate(fetch)` is subject to CORS policies.

---

## Example Tests

### Example 1: Simple Login Test
```typescript
import { test, expect } from '@playwright/test';
import { LoginPage } from './pages/auth/LoginPage';

test.describe('Authentication', () => {
  test('successful login', async ({ page }) => {
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.fillEmail('user@example.com');
    await loginPage.fillPassword('password');
    await loginPage.clickLogin();

    await expect(page).toHaveURL(/\/dashboard/);
  });
});
```

### Example 2: API-Based Authentication
```typescript
import { test, expect } from '@playwright/test';
import { authenticateViaAPI } from './fixtures/auth';
import { AdminPage } from './pages/admin/AdminPage';

test.describe('Admin Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await authenticateViaAPI(page, 'admin@example.com', 'password');
  });

  test('displays service status', async ({ page }) => {
    const adminPage = new AdminPage(page);

    await adminPage.goto();
    await adminPage.waitForServiceStatus();

    const status = await adminPage.getApiHealthStatus();
    expect(status).toContain('Healthy');
  });
});
```

### Example 3: Multi-Viewport Test
```typescript
import { test, expect } from '@playwright/test';
import { setViewport } from './helpers/responsive-utils';

test.describe('Responsive Layout', () => {
  const viewports = ['mobile', 'tablet', 'desktop'] as const;

  for (const viewport of viewports) {
    test(`navigation works on ${viewport}`, async ({ page }) => {
      await setViewport(page, viewport);
      await page.goto('/');

      const nav = page.getByRole('navigation');
      await expect(nav).toBeVisible();
    });
  }
});
```

### Example 4: Mocked API Test
```typescript
import { test, expect } from '@playwright/test';
import { mockApiSuccess, mockApiError } from './helpers/mocks';

test.describe('Error Handling', () => {
  test('displays error on API failure', async ({ page }) => {
    await mockApiError(page, '/api/games', 500);
    await page.goto('/games');

    const errorMessage = page.locator('[role="alert"]');
    await expect(errorMessage).toBeVisible();
    await expect(errorMessage).toContainText('Failed to load games');
  });

  test('displays data on API success', async ({ page }) => {
    await mockApiSuccess(page, '/api/games', { games: [{ id: 1, name: 'Test Game' }] });
    await page.goto('/games');

    await expect(page.getByText('Test Game')).toBeVisible();
  });
});
```

---

## Running Tests

### Local Development
```bash
# Run all tests
pnpm test:e2e

# Run with UI mode (interactive debugging)
pnpm test:e2e:ui

# Run specific test file
pnpm test:e2e e2e/admin/service-status.spec.ts

# Run with coverage
pnpm test:e2e:coverage
```

### CI/CD
```bash
# Sharded execution (4 shards)
pnpm test:e2e:shard1
pnpm test:e2e:shard2
pnpm test:e2e:shard3
pnpm test:e2e:shard4

# Visual tests (desktop-chrome only)
pnpm test:e2e:visual

# Full regression suite
pnpm test:e2e --project=desktop-chrome --project=mobile-chrome
```

### Debugging
```bash
# Open Playwright inspector
PWDEBUG=1 pnpm test:e2e

# Generate HTML report
pnpm test:e2e:report

# Run in headed mode (see browser)
pnpm test:e2e --headed
```

---

## Best Practices Summary

1. **Always use Page Object Model** for maintainability
2. **Set up response listeners BEFORE actions** to avoid race conditions
3. **Provide React navigation fallbacks** for reliability
4. **Test all viewports** (desktop, tablet, mobile)
5. **Use API authentication** instead of UI login for faster tests
6. **Mock API responses** for negative test scenarios
7. **Run tests in parallel locally**, sequentially in CI for stability
8. **Use retry logic in CI only** for transient failures
9. **Organize tests by domain** (admin, auth, chat, games)
10. **Document critical patterns** in memory files (e2e-playwright-patterns)

---

## Additional Resources

- [Playwright Documentation](https://playwright.dev/)
- [Issue #2919: Playwright Configuration & Best Practices](https://github.com/meepleAi-app/meepleai-monorepo/issues/2919)
- [Issue #1497: Multi-Browser Testing](https://github.com/meepleAi-app/meepleai-monorepo/issues/1497)
- [Issue #1498: E2E Code Coverage](https://github.com/meepleAi-app/meepleai-monorepo/issues/1498)
- [Issue #2008: Retry Strategy](https://github.com/meepleAi-app/meepleai-monorepo/issues/2008)
- [Memory: e2e-playwright-patterns](apps/web/e2e/README-BEST-PRACTICES.md)
