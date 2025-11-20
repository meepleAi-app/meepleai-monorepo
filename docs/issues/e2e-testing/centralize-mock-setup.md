# Centralize Mock Setup Functions

**Issue ID**: E2E-003
**Data**: 2025-11-20
**Priorità**: 🔴 ALTA
**Categoria**: Code Quality & Maintainability
**Effort Stimato**: 6-8 ore
**Status**: 🔴 Not Started

---

## 📋 Problem Description

Le funzioni di mock setup (autenticazione, API routes, dati) sono **duplicate in 5+ file di test** con logica simile ma implementazione leggermente diversa.

```typescript
// ❌ ANTI-PATTERN: Setup duplicato in ogni test file

// In demo-user-login.spec.ts
async function setupAuthMocking(page: any, role, email) { ... }

// In pdf-upload-journey.spec.ts
async function setupAuthRoutes(page: Page) { ... }

// In admin.spec.ts
async function mockAuthenticatedUser(page: Page) { ... }

// In authenticated.spec.ts
async function setupMockAuth(page: Page, role, email) { ... }
```

**Problemi**:
- Logica duplicata in 5+ file (manutenzione difficile)
- Inconsistenza tra implementazioni (alcuni hanno features che altri no)
- Bug fixing richiede update in multipli file
- Difficoltà aggiungere nuove features (es. 2FA, OAuth state)

---

## 🎯 Impact & Risks

### Impatto
- **Maintainability**: Cambiamenti al mock auth richiedono update in 5+ file
- **Consistency**: Implementazioni diverse portano a behavior diversi
- **Bug Risk**: Bug fix in un file potrebbe non essere applicato agli altri
- **Developer Experience**: Confusione su quale setup usare

### Rischi
🔴 **Alto**: Bug in mock auth non rilevati (inconsistenza tra test)
🟡 **Medio**: Tempo sprecato in manutenzione
🟡 **Medio**: Regression quando si aggiorna solo alcuni file
🟢 **Basso**: Onboarding difficile per nuovi developer

---

## 📊 Current Situation

### Files con Setup Duplicato

```bash
# Find all setup functions
$ grep -r "async function setup" apps/web/e2e/*.spec.ts

demo-user-login.spec.ts:  async function setupAuthMocking(...)
demo-user-login.spec.ts:  async function mockFailedAuth(...)
pdf-upload-journey.spec.ts:  async function setupAuthRoutes(...)
pdf-upload-journey.spec.ts:  async function setupGamesRoutes(...)
pdf-upload-journey.spec.ts:  async function setupPdfRoutes(...)
admin.spec.ts:  async function mockAuthenticatedUser(...)
error-handling.spec.ts:  async function setupErrorRoutes(...)
```

**Totale**: 8+ funzioni duplicate
**LOC Duplicato**: ~400-500 lines

### Esempio di Duplicazione

```typescript
// File 1: demo-user-login.spec.ts (lines 44-83)
async function setupAuthMocking(page: any, role, email) {
  let isAuthenticated = false;
  const userResponse = {
    user: { id: `${role}-id`, email, displayName: `Test ${role}`, role },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route: any) => {
    if (isAuthenticated) {
      await route.fulfill({ status: 200, body: JSON.stringify(userResponse) });
    } else {
      await route.fulfill({ status: 401, body: JSON.stringify({ error: 'Not authenticated' }) });
    }
  });
  // ... 30+ more lines
}

// File 2: admin.spec.ts (lines 6-22) - SAME LOGIC, different name
async function mockAuthenticatedUser(page: Page) {
  await page.route(`${apiBase}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@example.com', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
      })
    });
  });
}

// File 3: fixtures/auth.ts (lines 9-38) - SAME LOGIC AGAIN, better implementation
export async function setupMockAuth(page: Page, role, email) {
  // ... same logic with better type safety
}
```

---

## ✅ Implementation Recommendations

### Target Structure

```
apps/web/e2e/
├── fixtures/
│   ├── auth.ts (existing, expand)
│   ├── api-mocks.ts (new)
│   ├── data-factories.ts (new)
│   └── test-helpers.ts (new)
```

### 1. Centralize Auth Mocking

**File**: `apps/web/e2e/fixtures/auth.ts` (expand existing)

```typescript
import { Page } from '@playwright/test';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';

export type UserRole = 'Admin' | 'Editor' | 'User';
export type AuthState = 'authenticated' | 'unauthenticated' | 'requires2fa';

export interface MockAuthOptions {
  role?: UserRole;
  email?: string;
  displayName?: string;
  authState?: AuthState;
  sessionExpiry?: number; // minutes
}

/**
 * Setup mock authentication routes with configurable state
 * Replaces: setupAuthMocking, mockAuthenticatedUser, setupMockAuth
 */
export async function setupMockAuth(
  page: Page,
  options: MockAuthOptions = {}
): Promise<MockAuthController> {
  const {
    role = 'User',
    email = `${role.toLowerCase()}@meepleai.dev`,
    displayName = `Test ${role}`,
    authState = 'authenticated',
    sessionExpiry = 60
  } = options;

  let currentAuthState = authState;

  const userResponse = {
    user: {
      id: `${role.toLowerCase()}-test-id`,
      email,
      displayName,
      role
    },
    expiresAt: new Date(Date.now() + sessionExpiry * 60 * 1000).toISOString()
  };

  // Mock /auth/me endpoint
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    switch (currentAuthState) {
      case 'authenticated':
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(userResponse)
        });
        break;

      case 'unauthenticated':
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Not authenticated' })
        });
        break;

      case 'requires2fa':
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            requiresTwoFactor: true,
            sessionToken: 'temp-session-token'
          })
        });
        break;
    }
  });

  // Mock /auth/login endpoint
  await page.route(`${API_BASE}/api/v1/auth/login`, async (route) => {
    currentAuthState = 'authenticated';
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  });

  // Mock /auth/logout endpoint
  await page.route(`${API_BASE}/api/v1/auth/logout`, async (route) => {
    currentAuthState = 'unauthenticated';
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Logged out' })
    });
  });

  // Return controller for dynamic state changes
  return {
    setAuthState: (newState: AuthState) => { currentAuthState = newState; },
    getAuthState: () => currentAuthState,
    getUserResponse: () => userResponse
  };
}

export interface MockAuthController {
  setAuthState(state: AuthState): void;
  getAuthState(): AuthState;
  getUserResponse(): any;
}

/**
 * Quick setup for authenticated user (most common case)
 */
export async function setupAuthenticatedUser(
  page: Page,
  role: UserRole = 'User'
): Promise<MockAuthController> {
  return setupMockAuth(page, { role, authState: 'authenticated' });
}

/**
 * Quick setup for unauthenticated state
 */
export async function setupUnauthenticatedUser(page: Page): Promise<MockAuthController> {
  return setupMockAuth(page, { authState: 'unauthenticated' });
}
```

### 2. Centralize API Mocks

**File**: `apps/web/e2e/fixtures/api-mocks.ts` (new)

```typescript
import { Page, Route } from '@playwright/test';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';

/**
 * Mock games API with configurable data
 * Replaces: setupGamesRoutes (duplicated in 3+ files)
 */
export async function setupGamesMock(page: Page, games: any[] = []) {
  const defaultGames = games.length > 0 ? games : [
    {
      id: 'chess',
      name: 'Chess',
      description: 'Classic chess game',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
  ];

  await page.route(`${API_BASE}/api/v1/games`, async (route: Route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(defaultGames)
      });
    } else {
      await route.continue();
    }
  });

  return { games: defaultGames };
}

/**
 * Mock PDF API with upload tracking
 * Replaces: setupPdfRoutes (in pdf-upload-journey.spec.ts)
 */
export async function setupPdfMock(page: Page, gameId: string) {
  const pdfs: any[] = [];
  let nextPdfId = 1;

  await page.route(`${API_BASE}/games/${gameId}/pdfs`, async (route: Route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs })
      });
    }
  });

  await page.route(`${API_BASE}/ingest/pdf`, async (route: Route) => {
    if (route.request().method() === 'POST') {
      const documentId = `doc-${nextPdfId}`;
      const newPdf = {
        id: `pdf-${nextPdfId++}`,
        gameId,
        fileName: 'test-rulebook.pdf',
        uploadedAt: new Date().toISOString(),
        status: 'Completed'
      };
      pdfs.push(newPdf);

      await route.fulfill({
        status: 200,
        body: JSON.stringify({ documentId })
      });
    }
  });

  return { pdfs };
}

/**
 * Mock streaming chat API
 * Replaces: inline route mocking in chat-streaming.spec.ts
 */
export async function setupChatStreamingMock(
  page: Page,
  options: {
    tokens?: string[];
    error?: string;
    citations?: any[];
  } = {}
) {
  const { tokens = ['Hello', ' world'], error, citations = [] } = options;

  await page.route('**/api/v1/agents/qa/stream', async (route: Route) => {
    if (error) {
      const sseData = `event: error\ndata: ${JSON.stringify({ message: error })}\n\n`;
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: sseData
      });
    } else {
      const events = [
        'event: stateUpdate\ndata: {"state":"Thinking..."}\n\n',
        ...tokens.map(token => `event: token\ndata: {"token":"${token}"}\n\n`),
        citations.length > 0
          ? `event: citations\ndata: ${JSON.stringify({ snippets: citations })}\n\n`
          : '',
        'event: complete\ndata: {"totalTokens":' + tokens.length + '}\n\n'
      ].filter(Boolean).join('');

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: events
      });
    }
  });
}
```

### 3. Data Factories

**File**: `apps/web/e2e/fixtures/data-factories.ts` (new)

```typescript
/**
 * Generate test data with sensible defaults
 */
export const DataFactory = {
  createUser(overrides: Partial<any> = {}) {
    return {
      id: `user-${Math.random().toString(36).slice(2)}`,
      email: `test-${Date.now()}@example.com`,
      displayName: 'Test User',
      role: 'User',
      createdAt: new Date().toISOString(),
      ...overrides
    };
  },

  createGame(overrides: Partial<any> = {}) {
    return {
      id: `game-${Math.random().toString(36).slice(2)}`,
      name: 'Test Game',
      description: 'A test board game',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      ...overrides
    };
  },

  createPdf(gameId: string, overrides: Partial<any> = {}) {
    return {
      id: `pdf-${Math.random().toString(36).slice(2)}`,
      gameId,
      fileName: 'test-rulebook.pdf',
      fileSizeBytes: 1024 * 100,
      uploadedAt: new Date().toISOString(),
      status: 'Completed',
      pageCount: 10,
      ...overrides
    };
  }
};
```

---

## 📝 Migration Checklist

### Phase 1: Create Centralized Fixtures (Day 1)
- [ ] Expand `fixtures/auth.ts` with `setupMockAuth`
- [ ] Create `fixtures/api-mocks.ts`
- [ ] Create `fixtures/data-factories.ts`
- [ ] Add TypeScript types
- [ ] Write unit tests for fixtures

### Phase 2: Migrate Test Files (Days 2-3)
- [ ] Migrate `demo-user-login.spec.ts` (remove 2 setup functions)
- [ ] Migrate `pdf-upload-journey.spec.ts` (remove 3 setup functions)
- [ ] Migrate `admin.spec.ts` (remove 1 setup function)
- [ ] Migrate `chat-streaming.spec.ts` (use setupChatStreamingMock)
- [ ] Migrate remaining files

### Phase 3: Cleanup & Documentation (Day 4)
- [ ] Delete duplicate setup functions
- [ ] Update README with new fixtures
- [ ] Add JSDoc documentation
- [ ] Update migration guide

---

## ✅ Acceptance Criteria

### Must Have
- [ ] Tutti i setup auth centralizzati in `fixtures/auth.ts`
- [ ] Tutti i setup API centralizzati in `fixtures/api-mocks.ts`
- [ ] Zero funzioni setup duplicate nei test files
- [ ] Tutti i test passano dopo migrazione
- [ ] TypeScript types per tutti i fixtures

### Should Have
- [ ] Data factories per generazione dati test
- [ ] Unit tests per fixtures
- [ ] Documentazione completa
- [ ] Migration guide aggiornata

---

## 📁 Files to Create/Modify

### Create
- `apps/web/e2e/fixtures/api-mocks.ts`
- `apps/web/e2e/fixtures/data-factories.ts`
- `apps/web/e2e/fixtures/test-helpers.ts`

### Modify
- `apps/web/e2e/fixtures/auth.ts` (expand)
- `apps/web/e2e/demo-user-login.spec.ts` (remove setup)
- `apps/web/e2e/pdf-upload-journey.spec.ts` (remove setup)
- `apps/web/e2e/admin.spec.ts` (remove setup)
- `apps/web/e2e/chat-streaming.spec.ts` (use centralized)
- `apps/web/e2e/README.md` (document fixtures)

---

## 🔗 Related Issues

- [Complete POM Migration](./complete-pom-migration.md) - Include fixtures in POM
- [Add Negative Test Scenarios](./add-negative-test-scenarios.md) - Use centralized error mocks

---

**Created**: 2025-11-20
**Last Updated**: 2025-11-20
**Owner**: QA Team
