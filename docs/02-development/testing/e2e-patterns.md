# E2E Testing Patterns - Playwright Best Practices

**Source**: E2E test fixes (Issues #795, #797, Completed 2025-11-07)
**Framework**: Playwright
**Coverage Target**: 80%+ critical user journeys
**Status**: Production patterns from 228 test fixes

---

## Core Principles

### 1. **Test Real User Journeys, Not Implementation**
```typescript
// ❌ BAD: Testing implementation details
test('Should call fetchGames API', async ({ page }) => {
  const apiCalled = page.waitForRequest('/api/games');
  // ...
});

// ✅ GOOD: Testing user outcome
test('User can see game list after login', async ({ page }) => {
  await login(page);
  await expect(page.locator('[data-testid="game-card"]')).toHaveCount(3);
});
```

### 2. **Use Fixtures for Setup Complexity**
```typescript
// fixtures/setupUser.ts
import { test as base } from '@playwright/test';

export const test = base.extend({
  authenticatedPage: async ({ page }, use) => {
    // Login once per test
    await page.goto('/login');
    await page.fill('[name="email"]', 'user@test.com');
    await page.fill('[name="password"]', 'Test123!');
    await page.click('button[type="submit"]');
    await page.waitForURL('/');

    await use(page);

    // Cleanup (logout)
    await page.click('[data-testid="user-menu"]');
    await page.click('text=Logout');
  }
});

// Use in tests
test('Authenticated user can upload PDF', async ({ authenticatedPage }) => {
  await authenticatedPage.goto('/upload');
  // Test logic...
});
```

**Benefits**:
- Reusable setup across tests
- Automatic cleanup
- Less code duplication

---

## Common Patterns

### Pattern 1: API Mocking with Playwright

**To-Be Pattern**:
```typescript
import { test, expect } from '@playwright/test';

test('Mock API response', async ({ page }) => {
  // Intercept and mock API
  await page.route('/api/games', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        games: [
          { id: '1', name: 'Chess', minPlayers: 2, maxPlayers: 2 },
          { id: '2', name: 'Catan', minPlayers: 3, maxPlayers: 4 }
        ]
      })
    });
  });

  await page.goto('/games');

  // Verify mock data displayed
  await expect(page.locator('[data-testid="game-card"]')).toHaveCount(2);
  await expect(page.locator('text=Chess')).toBeVisible();
});
```

**Use Cases**:
- Testing UI without backend dependency
- Simulating error states
- Testing edge cases (empty lists, large datasets)

---

### Pattern 2: OAuth Button Testing

**Challenge**: OAuth redirects to external provider
**Solution**: Mock OAuth flow or test only button presence

**To-Be Pattern**:
```typescript
test('OAuth buttons present and functional', async ({ page }) => {
  await page.goto('/login');

  // Verify buttons exist
  const googleBtn = page.locator('[data-testid="oauth-google"]');
  const discordBtn = page.locator('[data-testid="oauth-discord"]');
  const githubBtn = page.locator('[data-testid="oauth-github"]');

  await expect(googleBtn).toBeVisible();
  await expect(discordBtn).toBeVisible();
  await expect(githubBtn).toBeVisible();

  // Verify href points to OAuth endpoint (don't follow redirect)
  await expect(googleBtn).toHaveAttribute('href', /\/api\/v1\/auth\/oauth\/google\/login/);
});
```

**Alternative**: E2E OAuth flow with test provider (complex, avoid unless critical)

---

### Pattern 3: File Upload Testing

**To-Be Pattern**:
```typescript
test('PDF upload flow', async ({ page }) => {
  await page.goto('/upload');

  // Prepare file
  const filePath = path.join(__dirname, 'fixtures', 'test.pdf');

  // Upload file
  const fileInput = page.locator('input[type="file"]');
  await fileInput.setInputFiles(filePath);

  // Wait for upload to complete
  await page.waitForSelector('[data-testid="upload-success"]', { timeout: 30000 });

  // Verify success message
  await expect(page.locator('text=Upload successful')).toBeVisible();
});
```

**Best Practices**:
- Use small test files (< 1MB)
- Increase timeout for upload operations (30s+)
- Verify both client and server confirmation

---

### Pattern 4: Streaming/SSE Testing

**To-Be Pattern** (Chat with Server-Sent Events):
```typescript
test('Chat streaming response', async ({ page }) => {
  await page.goto('/chat');

  // Type question
  await page.fill('[data-testid="chat-input"]', 'What are the rules?');
  await page.click('[data-testid="send-button"]');

  // Wait for streaming to start
  await page.waitForSelector('[data-testid="ai-message"]', { timeout: 5000 });

  // Wait for streaming to complete (no more updates)
  await page.waitForFunction(() => {
    const message = document.querySelector('[data-testid="ai-message"]');
    return message?.textContent && message.textContent.length > 50;
  }, { timeout: 30000 });

  // Verify complete response
  const response = await page.locator('[data-testid="ai-message"]').textContent();
  expect(response).toContain('rules');
  expect(response.length).toBeGreaterThan(50);
});
```

**Key Points**:
- Longer timeouts for AI/streaming (30s+)
- Wait for content completion, not just element presence
- Use `waitForFunction` for custom conditions

---

## Playwright Configuration Best Practices

### playwright.config.ts (Optimized)

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',

  // Parallelization
  fullyParallel: true,
  workers: process.env.CI ? 1 : 4, // Serial in CI, parallel local

  // Timeouts
  timeout: 30000, // 30s per test
  expect: { timeout: 5000 }, // 5s for assertions

  // Retries
  retries: process.env.CI ? 2 : 0, // Retry flaky tests in CI

  // Reporter
  reporter: [
    ['html', { outputFolder: 'playwright-report' }],
    ['list'] // Console output
  ],

  use: {
    // Base URL
    baseURL: 'http://localhost:3000',

    // Screenshots/videos on failure
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',

    // Tracing
    trace: 'retain-on-failure',
  },

  // Projects (browsers)
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    // Only test Chromium in CI (faster), all browsers locally
    ...(!process.env.CI ? [
      { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
      { name: 'webkit', use: { ...devices['Desktop Safari'] } },
    ] : []),
  ],

  // Dev server
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
    timeout: 120000, // 2 min for dev server startup
  },
});
```

**Key Optimizations**:
- Parallel workers (local: 4, CI: 1 to avoid flakiness)
- Retries only in CI (2 retries for flaky tests)
- Screenshots/videos only on failure (save disk space)
- Single browser in CI (Chromium sufficient for most cases)

---

## Test Organization Best Practices

### File Structure

```
tests/e2e/
├── fixtures/
│   ├── setupUser.ts (authentication fixture)
│   ├── setupGames.ts (game data fixture)
│   └── test-data/ (test PDFs, images, etc.)
├── specs/
│   ├── auth/
│   │   ├── login.spec.ts
│   │   ├── registration.spec.ts
│   │   └── oauth.spec.ts
│   ├── games/
│   │   ├── game-list.spec.ts
│   │   ├── game-detail.spec.ts
│   │   └── game-search.spec.ts
│   ├── chat/
│   │   └── chat.spec.ts
│   └── upload/
│       └── pdf-upload.spec.ts
└── playwright.config.ts
```

**Guidelines**:
- Group by feature area (auth, games, chat, etc.)
- One spec file per major user journey
- Shared fixtures in `fixtures/`
- Test data in `fixtures/test-data/`

---

### Test Naming Convention

```typescript
// ✅ GOOD: Describes user outcome
test('User can login with valid credentials', async ({ page }) => { });
test('Upload fails with invalid PDF format', async ({ page }) => { });
test('Admin can create new API key', async ({ page }) => { });

// ❌ BAD: Implementation-focused
test('POST /api/login returns 200', async ({ page }) => { });
test('Button click triggers handleSubmit', async ({ page }) => { });
```

**Pattern**: `[Actor] can [action] [with/when condition]`

---

## Debugging Failing E2E Tests

### Step 1: Run with UI Mode
```bash
pnpm test:e2e:ui
```
**Benefits**: Visual browser, step-through debugging, time travel

### Step 2: Enable Verbose Logging
```typescript
test('Debug failing test', async ({ page }) => {
  page.on('console', msg => console.log('BROWSER:', msg.text()));
  page.on('response', resp => console.log('RESPONSE:', resp.url(), resp.status()));

  // Test logic...
});
```

### Step 3: Take Screenshots at Each Step
```typescript
test('Debug with screenshots', async ({ page }) => {
  await page.goto('/login');
  await page.screenshot({ path: 'debug-1-login-page.png' });

  await page.fill('[name="email"]', 'test@test.com');
  await page.screenshot({ path: 'debug-2-email-filled.png' });

  // ...
});
```

### Step 4: Check Network Activity
```typescript
test('Debug network issues', async ({ page }) => {
  page.on('request', req => console.log('REQUEST:', req.method(), req.url()));
  page.on('requestfailed', req => console.log('FAILED:', req.url(), req.failure()?.errorText));

  // Test logic...
});
```

---

## Common E2E Test Failures & Solutions

### Failure 1: Element Not Found
**Error**: `Timeout 5000ms exceeded waiting for locator`
**Causes**:
- Element selector wrong
- Element not rendered yet
- Element hidden by CSS

**Solutions**:
```typescript
// ✅ Use data-testid (most reliable)
await page.locator('[data-testid="submit-button"]').click();

// ✅ Wait for element to be visible
await page.waitForSelector('[data-testid="element"]', { state: 'visible' });

// ✅ Increase timeout for slow operations
await page.locator('text=Loading...', { timeout: 30000 }).waitFor({ state: 'hidden' });
```

---

### Failure 2: Form Submission Not Working
**Error**: Button clicks but nothing happens
**Causes**:
- Button disabled (validation not met)
- Form needs all required fields
- API mock not working

**Solutions**:
```typescript
// Check button state before clicking
const button = page.locator('button[type="submit"]');
await expect(button).toBeEnabled(); // Verify enabled first

// Fill all required fields
await page.fill('[name="email"]', 'test@test.com');
await page.fill('[name="password"]', 'Test123!');
await page.check('[name="terms"]'); // Don't forget checkboxes!

// Wait for submission to complete
await Promise.all([
  page.waitForResponse('/api/submit'),
  page.click('button[type="submit"]')
]);
```

---

### Failure 3: Flaky Tests (Sometimes Pass, Sometimes Fail)
**Causes**:
- Race conditions
- Timing issues
- External dependencies (APIs)

**Solutions**:
```typescript
// ✅ Use auto-waiting (Playwright built-in)
await page.click('button'); // Waits for element to be actionable

// ✅ Wait for network idle (all requests done)
await page.goto('/dashboard', { waitUntil: 'networkidle' });

// ✅ Retry-ability: Use expect with timeout
await expect(page.locator('text=Success')).toBeVisible({ timeout: 10000 });

// ❌ BAD: Fixed waits (brittle)
await page.waitForTimeout(5000); // Avoid this!
```

---

### Failure 4: Authentication Issues in Tests
**Error**: Tests fail because not logged in
**Solution**: Use authentication fixtures

**Pattern**:
```typescript
// fixtures/auth.ts
export const test = base.extend({
  authenticatedPage: async ({ page }, use) => {
    // Login
    await page.goto('/login');
    await page.fill('[name="email"]', 'test@meepleai.dev');
    await page.fill('[name="password"]', 'Test123!');
    await page.click('button[type="submit"]');
    await page.waitForURL('/');

    // Store auth state (reusable across tests)
    await page.context().storageState({ path: 'playwright/.auth/user.json' });

    await use(page);
  }
});

// Reuse auth state (faster)
test.use({ storageState: 'playwright/.auth/user.json' });
```

---

## Test Data Management

### Use Test Fixtures (Static Data)

```typescript
// fixtures/test-data/games.json
[
  {
    "id": "test-game-1",
    "name": "Chess",
    "minPlayers": 2,
    "maxPlayers": 2
  },
  {
    "id": "test-game-2",
    "name": "Catan",
    "minPlayers": 3,
    "maxPlayers": 4
  }
]

// Load in test
import games from '../fixtures/test-data/games.json';

test('Game list displays test data', async ({ page }) => {
  await page.route('/api/games', route => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({ games })
    });
  });

  await page.goto('/games');
  await expect(page.locator('text=Chess')).toBeVisible();
});
```

**Benefits**:
- Consistent test data across tests
- Easy to update (single source of truth)
- Version controlled

---

### Seed Test Database (Alternative)

**For integration tests**:
```typescript
test.beforeAll(async () => {
  // Seed database with test data
  await seedDatabase([
    { email: 'user1@test.com', role: 'User' },
    { email: 'admin@test.com', role: 'Admin' }
  ]);
});

test.afterAll(async () => {
  // Cleanup
  await cleanupDatabase();
});
```

**Use When**: Testing real database interactions (integration tests)

---

## Performance Optimization

### 1. Parallel Test Execution

**Configuration**:
```typescript
// playwright.config.ts
export default defineConfig({
  fullyParallel: true,
  workers: 4, // Run 4 tests in parallel (local)
});
```

**Isolation**: Each test gets fresh browser context (no cross-test pollution)

---

### 2. Reuse Authentication State

```typescript
// global-setup.ts
async function globalSetup() {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  // Login once
  await page.goto('/login');
  await page.fill('[name="email"]', 'test@test.com');
  await page.fill('[name="password"]', 'Test123!');
  await page.click('button[type="submit"]');

  // Save auth state
  await page.context().storageState({ path: 'playwright/.auth/user.json' });
  await browser.close();
}

// Use in config
export default defineConfig({
  globalSetup: require.resolve('./global-setup'),
});

// Tests use saved state
test.use({ storageState: 'playwright/.auth/user.json' });
```

**Speed Up**: Login once globally, reuse across all tests (~50% faster)

---

### 3. Skip Unnecessary Waits

```typescript
// ❌ BAD: Fixed timeout (slow + brittle)
await page.waitForTimeout(3000);

// ✅ GOOD: Wait for specific condition (fast + reliable)
await page.waitForLoadState('networkidle');
await page.waitForSelector('[data-testid="loaded"]');
await expect(page.locator('text=Ready')).toBeVisible();
```

---

## CI/CD Integration

### GitHub Actions Configuration

```yaml
# .github/workflows/e2e-tests.yml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20

      - name: Install dependencies
        run: pnpm install

      - name: Install Playwright browsers
        run: pnpm exec playwright install --with-deps chromium

      - name: Start backend
        run: |
          cd apps/api && dotnet run &
          sleep 10

      - name: Run E2E tests
        run: pnpm test:e2e

      - name: Upload report on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: playwright-report/
```

**Key Points**:
- Install only Chromium in CI (faster)
- Start backend before tests
- Upload report on failure (debugging)

---

## Coverage Strategy

### Critical User Journeys (80%+ Target)

**Must Cover**:
1. Authentication (login, registration, logout, password reset)
2. Game browsing (search, filter, detail view)
3. PDF upload (select file, upload, processing)
4. Chat (ask question, receive answer, view history)
5. Admin (user management, API keys, analytics)

**Nice to Have**:
- OAuth flows (complex, mock if possible)
- Edge cases (error states, empty states)
- Accessibility (keyboard navigation, screen readers)

**Skip**:
- Unit-level logic (covered by Jest)
- Internal API details (covered by integration tests)
- Styling/visual regression (separate tool: Percy, Chromatic)

---

## Lessons Learned from 228 Test Fixes

### 1. **Data-testid > Text Selectors**
- `[data-testid="button"]` is more stable than `text=Click Me`
- Text changes break tests (translations, copywriting)
- data-testid explicitly marks test hooks

### 2. **Mock External Dependencies**
- APIs should be mocked by default
- Only test real APIs in integration tests (separate suite)
- Mock complex flows (OAuth, payment providers)

### 3. **Fixtures Reduce Boilerplate**
- Authentication fixture saved ~500 lines of duplicated login code
- Game data fixture ensured consistency across 20 tests

### 4. **Flaky Tests = Bad Selectors OR Race Conditions**
- 95% of flakiness fixed by proper waits (`waitForSelector`, `expect().toBeVisible()`)
- 5% fixed by data-testid (text selectors too fragile)

### 5. **CI vs Local Differences**
- CI needs longer timeouts (slower machines)
- Parallel workers (4 local, 1 CI) reduce flakiness
- Screenshots/videos essential for CI debugging

---

## To-Be E2E Testing Checklist

### For Every New Feature

- [ ] Identify critical user journey
- [ ] Write E2E test covering happy path
- [ ] Add error case tests (at least 1)
- [ ] Use fixtures for setup
- [ ] Use data-testid for selectors
- [ ] Mock external APIs
- [ ] Verify in CI (not just local)
- [ ] Add to E2E test documentation

### Before Merging PR

- [ ] All E2E tests pass locally
- [ ] All E2E tests pass in CI
- [ ] No flaky tests (run 3 times, all pass)
- [ ] Coverage maintained or improved
- [ ] New features have E2E tests

---

**Knowledge extracted from**:
- e2e-test-fixes-summary.md (160 lines)
- e2e-test-investigation-final.md (356 lines)
- e2e-auth-oauth-buttons-fix-summary.md
- Issue #795, #797 (228 test fixes)

**Status**: Production-ready E2E testing patterns for all future development
