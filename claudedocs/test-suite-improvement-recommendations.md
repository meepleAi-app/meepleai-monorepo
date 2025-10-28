# Frontend Test Suite Improvement Recommendations

**Generated**: 2025-10-24
**Context**: Analysis of 64 test files, 1567/1610 passing tests (97.3%)
**Purpose**: Systematic improvements for maintainability, consistency, and performance

---

## Executive Summary

**Current State**: Healthy test suite with 97.3% pass rate, but with technical debt in:
- Test duplication (7 router mocks, 11 API client mocks)
- Inconsistent patterns (MockApiRouter vs jest.mock)
- Massive test files (chat.test.tsx: 3047 lines)
- Test isolation issues (versions.test.tsx)

**Recommended Approach**: Incremental improvements prioritized by ROI (Return on Investment)

---

## Priority 1: Quick Wins (1-2 hours each)

### 1.1 Fix versions.test.tsx Test Isolation Issue ⚡ HIGH PRIORITY

**Problem**: Tests pass individually (48/48) but fail in full suite run
**Root Cause**: Global mutable state causing test interference

**Current Anti-Pattern** (apps/web/src/__tests__/pages/versions.test.tsx:70-74):
```typescript
let routerGameId: string | undefined;

const setGameId = (value?: string) => {
  routerGameId = value;
};

const createRouter = (overrides: Partial<NextRouter> = {}): NextRouter => ({
  // ... references global routerGameId
  query: routerGameId ? { gameId: routerGameId } : {},
});
```

**Solution**:
```typescript
// REMOVE global state
// REPLACE with factory function that accepts gameId parameter

const createRouter = (gameId?: string, overrides: Partial<NextRouter> = {}): NextRouter => ({
  route: '/versions',
  pathname: '/versions',
  query: gameId ? { gameId } : {},
  // ... rest of config
});

// In beforeEach:
beforeEach(() => {
  jest.clearAllMocks();
  // Each test explicitly passes gameId when needed
});

// In tests:
it('should load version history for game', async () => {
  const router = createRouter('game-1'); // Explicit, no global state
  mockUseRouter.mockReturnValue(router);
  // ... rest of test
});
```

**Impact**: Fixes 17 test failures in full suite, improves test isolation
**Estimated Effort**: 1 hour
**ROI**: ⭐⭐⭐⭐⭐ (High impact, low effort)

---

### 1.2 Create Shared Test Fixtures

**Problem**: Duplicated mock objects across 11+ test files

**Current Duplication**:
- Auth user objects: 11+ files with same `{ id, email, role }` structure
- Next.js router mocks: 7 files with identical setup
- API client mocks: 11 files with same pattern

**Solution**: Create `apps/web/src/__tests__/fixtures/common-fixtures.ts`

```typescript
/**
 * Shared test fixtures for consistent mocking across test suite
 */

import type { NextRouter } from 'next/router';

// Auth Fixtures
export const createMockUser = (overrides?: {
  id?: string;
  email?: string;
  role?: 'Admin' | 'Editor' | 'User';
  displayName?: string;
}) => ({
  id: overrides?.id || 'user-1',
  email: overrides?.email || 'test@meepleai.dev',
  displayName: overrides?.displayName || 'Test User',
  role: overrides?.role || 'User',
});

export const createMockAuthResponse = (userOverrides?: Parameters<typeof createMockUser>[0]) => ({
  user: createMockUser(userOverrides),
  expiresAt: new Date(Date.now() + 3600000).toISOString(),
});

// Router Fixtures
export const createMockRouter = (overrides?: Partial<NextRouter>): NextRouter => ({
  route: overrides?.route || '/',
  pathname: overrides?.pathname || '/',
  query: overrides?.query || {},
  asPath: overrides?.asPath || '/',
  basePath: '',
  push: jest.fn(),
  replace: jest.fn(),
  reload: jest.fn(),
  back: jest.fn(),
  prefetch: jest.fn().mockResolvedValue(undefined),
  beforePopState: jest.fn(),
  events: {
    on: jest.fn(),
    off: jest.fn(),
    emit: jest.fn(),
  },
  isFallback: false,
  isLocaleDomain: false,
  isReady: true,
  isPreview: false,
  defaultLocale: 'en',
  domainLocales: [],
  locale: undefined,
  locales: undefined,
  ...overrides,
});

// API Mock Helpers
export const createMockApiClient = () => ({
  get: jest.fn(),
  post: jest.fn(),
  put: jest.fn(),
  delete: jest.fn(),
});
```

**Migration Example** (chat.test.tsx):
```typescript
// BEFORE:
const mockUser = {
  id: 'user-1',
  email: 'user@example.com',
  role: 'User'
};

// AFTER:
import { createMockUser, createMockAuthResponse } from '../fixtures/common-fixtures';

const mockUser = createMockUser(); // Uses defaults
const adminUser = createMockUser({ role: 'Admin' }); // Override specific field
```

**Impact**: Reduces duplication by ~200 lines, improves consistency
**Estimated Effort**: 2 hours (create + migrate 3-4 files as examples)
**ROI**: ⭐⭐⭐⭐ (Medium-high impact, low effort)

---

### 1.3 Extract Shared Page Test Helpers

**Problem**: Successful pattern from editor.test.tsx not reused elsewhere

**Current Pattern** (apps/web/src/__tests__/pages/editor.test.tsx:38-49):
```typescript
const waitForEditorReady = async () => {
  // Wait for auth to complete
  await waitFor(() => {
    expect(screen.queryByText(/Devi effettuare l'accesso/i)).not.toBeInTheDocument();
  }, { timeout: 3000 });

  // Wait for RuleSpec loading to complete
  await waitFor(() => {
    expect(screen.queryByText('Caricamento...')).not.toBeInTheDocument();
  }, { timeout: 3000 });
};
```

**Solution**: Generalize pattern in `apps/web/src/lib/__tests__/test-utils.tsx`

```typescript
/**
 * Waits for page to complete auth and data loading phases
 *
 * @example
 * await waitForPageReady({ authText: /login required/i, loadingText: /loading/i });
 *
 * @param options - Text patterns to wait for removal
 */
export async function waitForPageReady(options?: {
  authText?: RegExp | string;
  loadingText?: RegExp | string;
  timeout?: number;
}) {
  const {
    authText = /effettuare l'accesso|login required/i,
    loadingText = /caricamento|loading/i,
    timeout = 3000,
  } = options || {};

  // Wait for auth phase
  if (authText) {
    await waitFor(() => {
      expect(screen.queryByText(authText)).not.toBeInTheDocument();
    }, { timeout });
  }

  // Wait for loading phase
  if (loadingText) {
    await waitFor(() => {
      expect(screen.queryByText(loadingText)).not.toBeInTheDocument();
    }, { timeout });
  }
}

/**
 * Waits for auth to complete (first phase only)
 */
export async function waitForAuth(timeout = 3000) {
  await waitForPageReady({ loadingText: undefined, timeout });
}

/**
 * Waits for data loading to complete (assumes auth already done)
 */
export async function waitForLoading(timeout = 3000) {
  await waitForPageReady({ authText: undefined, timeout });
}
```

**Impact**: Reusable across 10+ page test files, standardizes async patterns
**Estimated Effort**: 1.5 hours
**ROI**: ⭐⭐⭐⭐ (Medium-high impact, low effort)

---

## Priority 2: Medium-Impact Refactoring (4-8 hours each)

### 2.1 Split chat.test.tsx into Focused Files

**Problem**: Single 3047-line file with 16 distinct test sections

**Current Structure**:
```
chat.test.tsx (3047 lines)
├─ Authentication
├─ Data Loading
├─ Game and Agent Selection
├─ Chat Management
├─ Messaging
├─ Feedback
├─ UI Interactions
├─ Edge Cases
├─ Streaming Responses
├─ Keyboard Interactions
├─ Empty States
├─ Error Message Requirements
├─ Snippet Formatting
├─ Message Roles
├─ Chat Preview Formatting
└─ CHAT-06: Message Edit/Delete
```

**Recommended Split**:
```
apps/web/src/__tests__/pages/
├─ chat.auth.test.tsx (~200 lines)
│  └─ Authentication, Empty States
├─ chat.data-loading.test.tsx (~250 lines)
│  └─ Data Loading, Game/Agent Selection
├─ chat.messaging.test.tsx (~600 lines)
│  └─ Messaging, Streaming Responses, Message Roles
├─ chat.interactions.test.tsx (~400 lines)
│  └─ UI Interactions, Keyboard Interactions, Feedback
├─ chat.formatting.test.tsx (~300 lines)
│  └─ Snippet Formatting, Chat Preview Formatting
├─ chat.features.test.tsx (~400 lines)
│  └─ Chat Management, CHAT-06 (Edit/Delete)
└─ chat.edge-cases.test.tsx (~200 lines)
   └─ Edge Cases, Error Message Requirements
```

**Shared Setup** (`apps/web/src/__tests__/fixtures/chat-fixtures.ts`):
```typescript
/**
 * Shared fixtures and setup for chat page tests
 */

export const createMockGame = (id: string = 'game-1') => ({
  id,
  name: `Test Game ${id}`,
  createdAt: new Date().toISOString(),
});

export const createMockAgent = (id: string = 'agent-1', gameId: string = 'game-1') => ({
  id,
  gameId,
  name: `Test Agent ${id}`,
  type: 'qa' as const,
  isActive: true,
});

export const createMockChat = (id: string = 'chat-1', gameId: string = 'game-1') => ({
  id,
  gameId,
  createdAt: new Date().toISOString(),
  messages: [],
});

// Common test setup
export const setupChatPageTest = () => {
  // Shared mock setup that all chat tests need
  // Router, API, auth, etc.
};
```

**Migration Strategy**:
1. Create fixtures file first
2. Split one section at a time (start with Authentication - smallest/simplest)
3. Verify each split file passes tests in isolation
4. Delete original chat.test.tsx last

**Impact**: Improved maintainability, faster test runs (parallelization), easier navigation
**Estimated Effort**: 6-8 hours
**ROI**: ⭐⭐⭐⭐ (High impact, medium effort)

---

### 2.2 Standardize MockApiRouter Usage

**Problem**: Inconsistent API mocking - only 2 of 11 page tests use MockApiRouter

**Current State**:
- ✅ Using MockApiRouter: editor.test.tsx, chat.supplementary.test.tsx
- ❌ Using jest.mock: admin.test.tsx, chat.test.tsx, chess.test.tsx, index.test.tsx, login.test.tsx, n8n.test.tsx, setup.test.tsx, upload.*.test.tsx, versions.test.tsx

**MockApiRouter Benefits**:
- Type-safe route definitions
- Cleaner test setup (no global.fetch manipulation)
- Better error messages on mock mismatches
- Easier to override specific routes per test
- Consistent pattern across codebase

**Migration Example** (versions.test.tsx):

**BEFORE**:
```typescript
beforeEach(() => {
  jest.clearAllMocks();

  mockApi.get.mockImplementation((url: string) => {
    if (url === '/api/v1/auth/me') {
      return Promise.resolve({ user: mockUser });
    }
    if (url.startsWith('/api/v1/games/')) {
      return Promise.resolve(mockVersions);
    }
    return Promise.reject(new Error(`Unmocked endpoint: ${url}`));
  });
});
```

**AFTER**:
```typescript
import { MockApiRouter, createJsonResponse } from '../utils/mock-api-router';

let router: MockApiRouter;

beforeEach(() => {
  router = new MockApiRouter();

  router.get('/api/v1/auth/me', () =>
    createJsonResponse({ user: mockUser })
  );

  router.get('/api/v1/games/:gameId/rulespec/versions', ({ params }) =>
    createJsonResponse(mockVersions)
  );

  global.fetch = router.toMockImplementation();
});
```

**Migration Priority Order**:
1. versions.test.tsx (fixes test isolation issue + standardizes)
2. setup.test.tsx (smaller file, good learning example)
3. admin.test.tsx (complex mocking, high value)
4. upload.*.test.tsx (4 files, can share patterns)
5. chat.test.tsx (after splitting, migrate each new file)

**Impact**: Consistent mocking patterns, easier maintenance, better type safety
**Estimated Effort**: 1-2 hours per file, 8-12 hours total
**ROI**: ⭐⭐⭐ (Medium impact, medium effort, long-term value)

---

### 2.3 Consolidate Async Testing Patterns

**Problem**: Inconsistent async handling (waitFor, act, advanceTimers, etc.)

**Analysis Results**:
- 1054 `waitFor` usages across test suite
- Mix of patterns: waitFor, act, advanceTimers, sleep, custom helpers
- No documented standard for async testing

**Recommended Standard** (add to test-utils.tsx):

```typescript
/**
 * Standard async testing patterns for consistent test behavior
 *
 * PREFER these utilities over direct waitFor/act usage for consistency
 */

/**
 * Waits for element to appear in DOM
 * Standard timeout: 3000ms (component loading phase)
 */
export async function waitForElement(
  getElement: () => HTMLElement,
  options?: { timeout?: number }
) {
  const { timeout = 3000 } = options || {};
  await waitFor(() => {
    expect(getElement()).toBeInTheDocument();
  }, { timeout });
}

/**
 * Waits for element to disappear from DOM
 * Standard timeout: 3000ms (component unload phase)
 */
export async function waitForElementRemoval(
  queryElement: () => HTMLElement | null,
  options?: { timeout?: number }
) {
  const { timeout = 3000 } = options || {};
  await waitFor(() => {
    expect(queryElement()).not.toBeInTheDocument();
  }, { timeout });
}

/**
 * Waits for async state update to complete
 * Use when component state changes but no DOM change is immediately visible
 */
export async function waitForStateUpdate(timeMs: number = 0) {
  await act(async () => {
    await new Promise(resolve => setTimeout(resolve, timeMs));
  });
}

/**
 * Advances fake timers and flushes promises
 * Use with jest.useFakeTimers() for polling/interval tests
 */
export async function advanceTimersAsync(timeMs: number) {
  await act(async () => {
    await jest.advanceTimersByTimeAsync(timeMs);
  });
}
```

**Documentation** (create `apps/web/src/__tests__/README.md`):

````markdown
# Testing Best Practices

## Async Testing Patterns

### ✅ DO: Use standard helpers from test-utils

```typescript
import { waitForPageReady, waitForElement } from '@/lib/__tests__/test-utils';

// Wait for page load
await waitForPageReady();

// Wait for specific element
await waitForElement(() => screen.getByText('Success'));
```

### ❌ DON'T: Mix different async patterns in same test

```typescript
// Inconsistent - avoid
await waitFor(() => { /* ... */ });
await act(async () => { /* ... */ });
await new Promise(r => setTimeout(r, 100)); // manual sleep
```

### Fake Timers

```typescript
// Setup
beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.runOnlyPendingTimers();
  jest.useRealTimers();
});

// Usage
await advanceTimersAsync(2000); // Advance 2 seconds + flush promises
```
````

**Impact**: Consistent async patterns, fewer timing-related test failures
**Estimated Effort**: 4 hours (create helpers + documentation)
**ROI**: ⭐⭐⭐ (Medium impact, reduces future test brittleness)

---

## Priority 3: Strategic Long-Term Improvements (1-2 weeks)

### 3.1 Migrate to Mock Service Worker (MSW)

**Context**: Recommended in `claudedocs/research_test_optimization_2025-10-24.md`

**Current Pain Points**:
- CHAT-02/CHAT-03 tests failing due to complex mock scenarios
- Query parameter handling difficult with jest.mock
- 50+ lines of mock setup per complex test

**MSW Benefits**:
- Realistic network interception (same code path as production)
- Query parameter support built-in
- Reusable across Jest, Storybook, local dev
- Type-safe with TypeScript
- Industry standard (used by React, Next.js, etc.)

**Implementation Plan**:

**Phase 1: Setup (2 hours)**
```bash
pnpm add -D msw@latest
npx msw init public/ --save
```

**Phase 2: Create Handlers (4 hours)**
```typescript
// apps/web/src/__tests__/mocks/handlers.ts
import { http, HttpResponse } from 'msw';

export const handlers = [
  // Auth endpoints
  http.get('/api/v1/auth/me', () => {
    return HttpResponse.json({
      user: { id: 'user-1', email: 'test@meepleai.dev', role: 'User' },
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    });
  }),

  // Games endpoints
  http.get('/api/v1/games', () => {
    return HttpResponse.json([
      { id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() },
      { id: 'game-2', name: 'Tic-Tac-Toe', createdAt: new Date().toISOString() },
    ]);
  }),

  // Chats with query params
  http.get('/api/v1/chats', ({ request }) => {
    const url = new URL(request.url);
    const gameId = url.searchParams.get('gameId');

    // Different data per game - solves CHAT-03 issue!
    const chatsByGame = {
      'game-1': [{ id: 'chat-1', gameId: 'game-1' }],
      'game-2': [{ id: 'chat-2', gameId: 'game-2' }],
    };

    return HttpResponse.json(chatsByGame[gameId] || []);
  }),
];
```

**Phase 3: Server Setup (1 hour)**
```typescript
// apps/web/src/__tests__/mocks/server.ts
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
```

**Phase 4: Jest Integration (1 hour)**
```typescript
// apps/web/jest.setup.js
import { server } from './src/__tests__/mocks/server';

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

**Phase 5: Migrate Tests (8-12 hours)**
- Start with CHAT-02/CHAT-03 (immediate value)
- Then editor.test.tsx (already uses patterns similar to MSW)
- Gradually migrate others as needed

**Migration Example**:

**BEFORE (MockApiRouter - 15 lines)**:
```typescript
beforeEach(() => {
  router = new MockApiRouter();

  router.get('/api/v1/auth/me', () => createJsonResponse({ user: mockUser }));
  router.get('/api/v1/games/:id', ({ params }) => createJsonResponse(mockGame));
  router.get('/api/v1/chats', () => createJsonResponse(mockChats));

  global.fetch = router.toMockImplementation();
});
```

**AFTER (MSW - 0 lines in test, handlers defined once)**:
```typescript
beforeEach(() => {
  // Handlers defined in mocks/handlers.ts work automatically
  // Only override if test needs specific behavior
});

// Override example for specific test:
it('should handle API error', async () => {
  server.use(
    http.get('/api/v1/games/:id', () => {
      return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    })
  );

  render(<MyComponent />);
  await waitFor(() => {
    expect(screen.getByText(/not found/i)).toBeInTheDocument();
  });
});
```

**Impact**: Solves CHAT-02/CHAT-03, reduces mock code by 70%, improves reusability
**Estimated Effort**: 16-20 hours (setup + migration of key tests)
**ROI**: ⭐⭐⭐⭐⭐ (Very high long-term value, industry best practice)

---

### 3.2 Test Performance Optimization

**Context**: Recommendations from `claudedocs/research_test_optimization_2025-10-24.md`

**Current Performance**:
- 64 test suites
- ~120 seconds full run
- No parallelization optimization
- No test sharding

**Optimization Strategies**:

**1. Jest Configuration (2 hours)**
```javascript
// apps/web/jest.config.js
module.exports = {
  // Use 50% of CPU cores for parallel execution
  maxWorkers: process.env.CI ? 2 : '50%',

  // Enable caching
  cache: true,
  cacheDirectory: '.jest-cache',

  // Bail early on CI to save time
  bail: process.env.CI ? 1 : 0,

  // Existing config...
};
```

**Expected Impact**: 20-30% faster local runs

**2. CI Test Sharding (4 hours)**
```yaml
# .github/workflows/ci.yml
jobs:
  test-frontend:
    strategy:
      matrix:
        shard: [1, 2, 3, 4]
    steps:
      - name: Run tests (shard ${{ matrix.shard }}/4)
        run: pnpm test --shard=${{ matrix.shard }}/4
```

**Expected Impact**: 75% faster CI (parallel jobs)

**3. Test Organization (4 hours)**
```javascript
// apps/web/jest.testSequencer.js
const Sequencer = require('@jest/test-sequencer').default;

class FastFirstSequencer extends Sequencer {
  sort(tests) {
    // Run fast unit tests first, slow integration tests last
    return tests.sort((a, b) => {
      const aIsUnit = a.path.includes('/__tests__/');
      const bIsUnit = b.path.includes('/__tests__/');

      if (aIsUnit && !bIsUnit) return -1;
      if (!aIsUnit && bIsUnit) return 1;
      return 0;
    });
  }
}

module.exports = FastFirstSequencer;
```

**4. Split Large Test Files** (done in 2.1)
- Enables better parallelization
- Faster feedback on specific features

**Total Impact**: 40-70% faster test execution
**Estimated Effort**: 10 hours
**ROI**: ⭐⭐⭐⭐ (High value for developer experience)

---

### 3.3 Test Architecture Documentation

**Purpose**: Prevent regression, onboard new developers, establish standards

**Create** `apps/web/src/__tests__/TESTING_GUIDE.md`:

````markdown
# Frontend Testing Guide

## Architecture Overview

```
__tests__/
├─ fixtures/           # Shared test data and mocks
│  ├─ common-fixtures.ts    # Auth, router, API mocks
│  ├─ chat-fixtures.ts      # Chat-specific mocks
│  └─ upload-mocks.ts       # Upload-specific mocks
├─ mocks/              # MSW handlers (network mocking)
│  ├─ handlers.ts           # API endpoint handlers
│  └─ server.ts             # MSW server setup
├─ utils/              # Test utilities
│  └─ mock-api-router.ts    # Legacy API mocking (migrating to MSW)
├─ pages/              # Page component tests
├─ components/         # Component tests
├─ lib/                # Utility function tests
└─ hooks/              # Custom hook tests
```

## Testing Standards

### File Naming
- Page tests: `{page-name}.test.tsx`
- Component tests: `{ComponentName}.test.tsx`
- Accessibility tests: `{ComponentName}.a11y.test.tsx`
- Feature splits: `{page}.{feature}.test.tsx` (e.g., `chat.messaging.test.tsx`)

### Test Structure (Arrange-Act-Assert)
```typescript
it('should display success message after form submission', async () => {
  // Arrange: Setup test data and mocks
  const mockUser = createMockUser({ role: 'Admin' });

  // Act: Render component and interact
  render(<MyForm />);
  await user.type(screen.getByLabelText('Name'), 'John');
  await user.click(screen.getByRole('button', { name: /submit/i }));

  // Assert: Verify expected outcome
  await waitFor(() => {
    expect(screen.getByText(/success/i)).toBeInTheDocument();
  });
});
```

### Async Patterns
✅ **DO**: Use helpers from `test-utils.tsx`
```typescript
import { waitForPageReady, waitForElement } from '@/lib/__tests__/test-utils';

await waitForPageReady(); // Standard page load wait
await waitForElement(() => screen.getByText('Loaded'));
```

❌ **DON'T**: Use arbitrary timeouts
```typescript
await new Promise(r => setTimeout(r, 1000)); // Flaky!
```

### Mock Data
✅ **DO**: Use shared fixtures
```typescript
import { createMockUser, createMockRouter } from '../fixtures/common-fixtures';

const user = createMockUser({ role: 'Admin' });
```

❌ **DON'T**: Duplicate mock objects
```typescript
const user = { id: 'user-1', email: 'test@test.com', role: 'Admin' };
```

### API Mocking

**NEW (Recommended)**: MSW for network mocking
```typescript
import { server } from '../mocks/server';
import { http, HttpResponse } from 'msw';

// Override for specific test
server.use(
  http.get('/api/v1/games', () => {
    return HttpResponse.json([mockGame]);
  })
);
```

**LEGACY**: MockApiRouter (migrating away from)
```typescript
import { MockApiRouter, createJsonResponse } from '../utils/mock-api-router';

const router = new MockApiRouter();
router.get('/api/v1/games', () => createJsonResponse([mockGame]));
global.fetch = router.toMockImplementation();
```

## Common Patterns

### Testing Auth-Protected Pages
```typescript
beforeEach(() => {
  // Default: Authenticated user
  server.use(
    http.get('/api/v1/auth/me', () => {
      return HttpResponse.json(createMockAuthResponse());
    })
  );
});

it('should show login message when not authenticated', async () => {
  // Override for this test
  server.use(
    http.get('/api/v1/auth/me', () => {
      return HttpResponse.json({ user: null }, { status: 401 });
    })
  );

  render(<ProtectedPage />);
  await waitFor(() => {
    expect(screen.getByText(/login required/i)).toBeInTheDocument();
  });
});
```

### Testing Forms
```typescript
it('should submit form with validation', async () => {
  const onSubmit = jest.fn();
  render(<MyForm onSubmit={onSubmit} />);

  // Use accessible queries (role, label)
  const nameInput = screen.getByLabelText('Name');
  const emailInput = screen.getByLabelText('Email');
  const submitButton = screen.getByRole('button', { name: /submit/i });

  // Type values
  await user.type(nameInput, 'John Doe');
  await user.type(emailInput, 'john@example.com');

  // Submit
  await user.click(submitButton);

  // Verify
  await waitFor(() => {
    expect(onSubmit).toHaveBeenCalledWith({
      name: 'John Doe',
      email: 'john@example.com',
    });
  });
});
```

### Testing Async Operations
```typescript
it('should load data on mount', async () => {
  render(<DataComponent />);

  // Wait for loading state to appear
  expect(screen.getByText(/loading/i)).toBeInTheDocument();

  // Wait for data to load
  await waitFor(() => {
    expect(screen.getByText('Data loaded')).toBeInTheDocument();
  });

  // Verify loading state removed
  expect(screen.queryByText(/loading/i)).not.toBeInTheDocument();
});
```

### Testing User Interactions
```typescript
it('should toggle accordion on click', async () => {
  render(<Accordion />);

  const trigger = screen.getByRole('button', { name: /toggle/i });

  // Initially collapsed
  expect(screen.queryByText('Content')).not.toBeInTheDocument();

  // Click to expand
  await user.click(trigger);
  await waitFor(() => {
    expect(screen.getByText('Content')).toBeInTheDocument();
  });

  // Click to collapse
  await user.click(trigger);
  await waitFor(() => {
    expect(screen.queryByText('Content')).not.toBeInTheDocument();
  });
});
```

## Performance Tips

1. **Use `screen.getBy*` over destructured queries**
   ```typescript
   // ✅ Better (re-queries DOM)
   expect(screen.getByText('Updated')).toBeInTheDocument();

   // ❌ Stale reference
   const { getByText } = render(<Component />);
   expect(getByText('Updated')).toBeInTheDocument(); // May fail after state change
   ```

2. **Minimize test file size**
   - Split files >1000 lines
   - Extract shared setup to fixtures
   - Use describe blocks for logical grouping

3. **Clean up properly**
   ```typescript
   afterEach(() => {
     jest.clearAllMocks();
     cleanup(); // React Testing Library auto-cleanup usually sufficient
   });
   ```

## Troubleshooting

### "Found multiple elements with text"
Use `getAllByText` or more specific query:
```typescript
// Instead of:
screen.getByText('Submit'); // Fails if multiple

// Use:
screen.getByRole('button', { name: 'Submit' }); // More specific
// OR
screen.getAllByText('Submit')[0]; // Get first match
```

### Test passes individually but fails in suite
- Check for global state leaks
- Ensure proper cleanup in `afterEach`
- Avoid mutable global variables (like versions.test.tsx issue)

### Async timeout errors
- Increase timeout: `waitFor(() => { ... }, { timeout: 5000 })`
- Check if waiting for correct element
- Verify mock data is being returned

## Resources

- [Testing Library Docs](https://testing-library.com/)
- [Jest Docs](https://jestjs.io/)
- [MSW Docs](https://mswjs.io/)
- Internal: `claudedocs/research_test_optimization_2025-10-24.md`
````

**Impact**: Faster onboarding, prevents anti-patterns, improves code review quality
**Estimated Effort**: 8 hours (write + review + iterate)
**ROI**: ⭐⭐⭐⭐ (Long-term value for team scaling)

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1) - 16 hours
1. ✅ Fix versions.test.tsx global state (1h)
2. ✅ Create common-fixtures.ts (2h)
3. ✅ Extract page test helpers (1.5h)
4. ✅ Create async testing standards in test-utils (4h)
5. ✅ Document testing guide basics (4h)
6. ✅ Migrate 2-3 files to new patterns (3.5h)

**Deliverables**:
- versions.test.tsx passing in full suite
- Shared fixtures available
- Standard async helpers documented
- Basic testing guide

### Phase 2: Consolidation (Week 2) - 16 hours
7. ✅ Split chat.test.tsx (8h)
8. ✅ Migrate 3-4 more files to MockApiRouter (4h)
9. ✅ Set up Jest performance optimizations (2h)
10. ✅ Update testing guide with examples (2h)

**Deliverables**:
- chat.test.tsx split into 7 focused files
- 50% of page tests using consistent patterns
- 20-30% faster local test runs
- Comprehensive testing guide

### Phase 3: Strategic (Weeks 3-4) - 24 hours
11. ✅ MSW setup and handler creation (6h)
12. ✅ Migrate CHAT-02/CHAT-03 tests to MSW (4h)
13. ✅ Migrate editor.test.tsx to MSW (3h)
14. ✅ Set up CI test sharding (4h)
15. ✅ Migrate remaining high-value tests to MSW (4h)
16. ✅ Final testing guide polish (3h)

**Deliverables**:
- MSW fully integrated
- CHAT-02/CHAT-03 tests passing
- 60-70% faster CI runs
- Complete testing documentation

---

## Success Metrics

### Code Quality Metrics
- **Test Duplication**: Reduce by 70% (200+ lines eliminated)
- **Test Isolation**: 100% tests pass both individually and in full suite
- **Mock Consistency**: 100% of page tests using MSW (not jest.mock)
- **File Size**: No test file >1000 lines

### Performance Metrics
- **Local Test Execution**: 30-40% faster (120s → 75s)
- **CI Test Execution**: 60-70% faster with sharding
- **Developer Experience**: Faster feedback loops, less flaky tests

### Maintainability Metrics
- **Onboarding Time**: 50% reduction (documented standards)
- **Test Failures**: 20% reduction in flaky/timing-related failures
- **Code Review Quality**: Faster reviews (consistent patterns)

---

## Risks and Mitigations

### Risk 1: MSW Migration Complexity
**Mitigation**:
- Start with 2-3 simple tests as proof-of-concept
- Document migration patterns before scaling
- Keep MockApiRouter as fallback during transition

### Risk 2: Team Adoption
**Mitigation**:
- Provide comprehensive documentation and examples
- Pair programming sessions for first migrations
- Code review emphasis on new patterns

### Risk 3: Breaking Changes
**Mitigation**:
- Incremental migration (don't break existing tests)
- Keep both patterns during transition
- Thorough testing at each phase

---

## Conclusion

**Immediate Actions (This Sprint)**:
1. Fix versions.test.tsx global state (fixes 17 failing tests)
2. Create common-fixtures.ts (reduces duplication)
3. Extract page test helpers (improves reusability)

**Next Sprint**:
4. Split chat.test.tsx (improves maintainability)
5. Jest performance optimizations (improves DX)

**Long-Term (Next Quarter)**:
6. Full MSW migration (industry best practice)
7. CI optimization (faster builds)
8. Complete documentation (team scaling)

**Total Investment**: ~56 hours over 4 weeks
**Expected ROI**:
- 70% less mock duplication
- 40-70% faster test execution
- 100% test isolation
- Sustainable test architecture for team growth

---

**Next Steps**: Review with team, prioritize based on current sprint capacity, start with Phase 1 quick wins.
