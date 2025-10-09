# MeepleAI Frontend Testing Guide

This guide explains the testing utilities and patterns used in the MeepleAI web application.

## Table of Contents

- [Overview](#overview)
- [MockApiRouter](#mockapirouter)
- [MockApiPresets](#mockapipresets)
- [Upload Test Fixtures](#upload-test-fixtures)
- [Best Practices](#best-practices)
- [Migration Guide](#migration-guide)

## Overview

The MeepleAI frontend uses Jest and React Testing Library for unit/integration tests, with a custom `MockApiRouter` utility to simplify API mocking across tests.

**Key Testing Utilities:**
- `MockApiRouter`: Centralized route-based API mocking
- `MockApiPresets`: Pre-configured mock handlers for common endpoints
- `upload-mocks.ts`: Domain-specific mock factories for upload workflow

## MockApiRouter

`MockApiRouter` is a fluent API for defining mock HTTP endpoints with pattern matching support, eliminating the need for complex `if-else` chains in test mocks.

### Basic Usage

```typescript
import { MockApiRouter, createJsonResponse } from '@/__tests__/utils/mock-api-router';

// Create a router
const router = new MockApiRouter();

// Register routes
router
  .get('/auth/me', () => createJsonResponse({ user: { id: 'user-1' } }))
  .get('/games', () => createJsonResponse([{ id: 'game-1', name: 'Chess' }]))
  .post('/ingest/pdf', () => createJsonResponse({ documentId: 'pdf-123' }, 201));

// Use as fetch mock
const mockFetch = jest.fn(router.toMockImplementation());
global.fetch = mockFetch;
```

### Route Parameters

MockApiRouter supports URL parameters using `:paramName` syntax:

```typescript
router.get('/games/:gameId/pdfs/:pdfId', ({ params }) => {
  // params.gameId and params.pdfId are extracted from URL
  return createJsonResponse({
    gameId: params.gameId,
    pdfId: params.pdfId
  });
});

// Matches: /games/game-123/pdfs/pdf-456
// params = { gameId: 'game-123', pdfId: 'pdf-456' }
```

### HTTP Methods

All standard HTTP methods are supported:

```typescript
router
  .get('/resource', () => createJsonResponse({ action: 'read' }))
  .post('/resource', () => createJsonResponse({ action: 'create' }, 201))
  .put('/resource', () => createJsonResponse({ action: 'update' }))
  .patch('/resource', () => createJsonResponse({ action: 'partial-update' }))
  .delete('/resource', () => createJsonResponse({ action: 'delete' }, 204));
```

### Route Context

Route handlers receive a context object with request details:

```typescript
router.post('/games/:id', (context) => {
  console.log(context.params);  // { id: 'game-123' }
  console.log(context.url);     // '/games/game-123'
  console.log(context.method);  // 'POST'
  console.log(context.init);    // RequestInit (body, headers, etc.)

  return createJsonResponse({ ok: true });
});
```

### Error Handling

When no route matches, MockApiRouter throws a helpful error with available routes:

```typescript
// If you call an unregistered endpoint:
await router.handle('/unknown/endpoint');

// Error message:
// MockApiRouter: No handler for GET /unknown/endpoint
//
// Available routes:
//   GET /auth/me
//   GET /games
//   POST /ingest/pdf
```

### Utility Methods

```typescript
// Get list of registered routes (useful for debugging)
const routes = router.getRoutes();
// [
//   { method: 'GET', pattern: '/auth/me' },
//   { method: 'POST', pattern: '/games' }
// ]

// Clear all routes
router.clear();
```

### Response Helpers

```typescript
import { createJsonResponse, createErrorResponse } from '@/__tests__/utils/mock-api-router';

// Success response (default status 200)
createJsonResponse({ data: 'success' });

// Success with custom status
createJsonResponse({ id: '123' }, 201);

// Error response
createErrorResponse(404, { error: 'Not Found' });

// Error with custom statusText
createErrorResponse(400, { error: 'Invalid' }, 'Bad Request');
```

## MockApiPresets

`MockApiPresets` provides pre-configured route handlers for common MeepleAI API endpoints, reducing boilerplate in tests.

### Available Presets

#### Authentication

```typescript
import { MockApiRouter } from '@/__tests__/utils/mock-api-router';
import { MockApiPresets } from '@/__tests__/utils/mock-api-presets';

const router = new MockApiRouter();

// Authenticated user
MockApiPresets.auth(router, {
  userId: 'user-1',
  role: 'Admin',
  email: 'admin@example.com',
  displayName: 'Admin User'
});

// Unauthorized user
MockApiPresets.auth(router, { unauthorized: true });
```

#### Games

```typescript
// List games + create game
MockApiPresets.games(router, {
  games: [
    { id: 'game-1', name: 'Chess' },
    { id: 'game-2', name: 'Monopoly' }
  ],
  createResponse: { id: 'game-new', name: 'New Game' }
});

// Game creation error
MockApiPresets.games(router, {
  createError: { status: 400, message: 'Name already exists' }
});
```

#### PDFs

```typescript
MockApiPresets.pdfs(router, {
  pdfs: [
    { id: 'pdf-1', fileName: 'rules.pdf', fileSizeBytes: 1024000 },
    { id: 'pdf-2', fileName: 'guide.pdf', fileSizeBytes: 512000 }
  ]
});
```

#### PDF Ingest

```typescript
MockApiPresets.ingest(router, {
  uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
  statusResponses: [
    { documentId: 'pdf-123', fileName: 'rules.pdf', processingStatus: 'processing' },
    { documentId: 'pdf-123', fileName: 'rules.pdf', processingStatus: 'completed' }
  ],
  retrySuccess: true
});

// Upload error
MockApiPresets.ingest(router, {
  uploadError: { status: 413, message: 'File too large' }
});
```

#### RuleSpec

```typescript
MockApiPresets.ruleSpec(router, {
  ruleSpec: {
    gameId: 'game-1',
    version: 'v1',
    rules: [
      { id: 'r1', text: 'Rule 1', section: 'Setup', page: '1', line: '1' }
    ]
  },
  publishSuccess: true
});

// RuleSpec error
MockApiPresets.ruleSpec(router, {
  ruleSpecError: { status: 404, message: 'RuleSpec not found' }
});
```

#### Complete Upload Workflow

```typescript
// Combines auth, games, pdfs, ingest, and ruleSpec
MockApiPresets.uploadWorkflow(router, {
  auth: { userId: 'user-1', role: 'Admin' },
  games: { games: [{ id: 'game-1', name: 'Chess' }] },
  pdfs: { pdfs: [] },
  ingest: {
    uploadResponse: { documentId: 'pdf-123' },
    statusResponses: [
      { documentId: 'pdf-123', fileName: 'test.pdf', processingStatus: 'completed' }
    ]
  },
  ruleSpec: {
    ruleSpec: {
      gameId: 'game-1',
      version: 'v1',
      rules: [{ id: 'r1', text: 'Test rule', section: null, page: null, line: null }]
    }
  }
});
```

#### Admin Dashboard

```typescript
MockApiPresets.admin(router, {
  requests: [
    { id: '1', endpoint: 'qa', query: 'How to play?', status: 'Success' }
  ],
  stats: { totalRequests: 100, avgLatency: 250 }
});
```

#### Chat Agents

```typescript
MockApiPresets.agents(router, {
  qaResponse: {
    answer: 'The game supports 2-4 players.',
    snippets: [{ source: 'PDF:rules', text: 'Players: 2-4', page: 1 }]
  },
  explainResponse: {
    answer: 'Here is how to set up the game...',
    snippets: []
  },
  feedbackSuccess: true
});

// Agent error
MockApiPresets.agents(router, {
  qaError: { status: 500, message: 'AI service unavailable' }
});
```

## Upload Test Fixtures

The `upload-mocks.ts` fixture provides domain-specific factories for upload workflow tests.

### Mock Factories

```typescript
import {
  createAuthMock,
  createGameMock,
  createPdfMock,
  createRuleSpecMock
} from '@/__tests__/fixtures/upload-mocks';

const auth = createAuthMock({ userId: 'user-1', role: 'Admin' });
const game = createGameMock({ id: 'game-1', name: 'Chess' });
const pdf = createPdfMock({ id: 'pdf-1', fileName: 'rules.pdf', processingStatus: 'completed' });
const ruleSpec = createRuleSpecMock({
  gameId: 'game-1',
  rules: [
    { id: 'r1', text: 'Move pieces legally', section: 'Rules', page: '5', line: '12' }
  ]
});
```

### setupUploadMocks

High-level helper for upload workflow tests (refactored to use MockApiRouter internally):

```typescript
import { setupUploadMocks } from '@/__tests__/fixtures/upload-mocks';

const mockFetch = setupUploadMocks({
  auth: createAuthMock({ userId: 'user-3', role: 'Admin' }),
  games: [createGameMock({ id: 'game-1', name: 'Chess' })],
  pdfs: { pdfs: [] },
  uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
  pdfStatusSequence: [
    { processingStatus: 'processing' },
    { processingStatus: 'completed' }
  ],
  ruleSpec: createRuleSpecMock({
    gameId: 'game-1',
    rules: [{ id: 'r1', text: 'Test rule', section: null, page: null, line: null }]
  })
});

global.fetch = mockFetch;
```

## Best Practices

### 1. Use MockApiRouter for All New Tests

**❌ Old pattern (avoid):**
```typescript
mockFetch.mockImplementation((input, init) => {
  const url = typeof input === 'string' ? input : input.toString();
  const method = init?.method ?? 'GET';

  if (url.endsWith('/auth/me')) {
    return createJsonResponse({ user: {...} });
  }
  if (url.endsWith('/games') && method === 'GET') {
    return createJsonResponse([...]);
  }
  // ... 20+ more if statements
  throw new Error(`Unexpected fetch: ${url}`);
});
```

**✅ New pattern (recommended):**
```typescript
const router = new MockApiRouter()
  .get('/auth/me', () => createJsonResponse({ user: {...} }))
  .get('/games', () => createJsonResponse([...]));

mockFetch.mockImplementation(router.toMockImplementation());
```

### 2. Use Presets for Common Scenarios

```typescript
// Instead of manually registering all routes
const router = new MockApiRouter();
MockApiPresets.uploadWorkflow(router, {
  auth: { userId: 'user-1', role: 'Admin' },
  games: { games: [{ id: 'game-1', name: 'Chess' }] }
});
```

### 3. Leverage Route Parameters

```typescript
// Extract IDs from URLs automatically
router.get('/games/:gameId/pdfs/:pdfId', ({ params }) => {
  console.log(`Fetching PDF ${params.pdfId} for game ${params.gameId}`);
  return createJsonResponse({ id: params.pdfId });
});
```

### 4. Use Descriptive Error Messages

MockApiRouter automatically provides helpful error messages when routes don't match. When debugging test failures, check the error message for the list of available routes.

### 5. Keep Mocks Close to Reality

Mock responses should match the actual API response structure. Use TypeScript interfaces to ensure consistency:

```typescript
interface Game {
  id: string;
  name: string;
  createdAt: string;
}

router.get('/games', (): Promise<Response> =>
  createJsonResponse<Game[]>([
    { id: 'game-1', name: 'Chess', createdAt: '2024-01-01T00:00:00Z' }
  ])
);
```

## Migration Guide

### Migrating from Manual Mock Implementation

**Before:**
```typescript
mockFetch.mockImplementation((input, init) => {
  const url = typeof input === 'string' ? input : input.toString();
  const method = init?.method ?? 'GET';

  if (url.endsWith('/games/game-1/pdfs')) {
    return createJsonResponse({ pdfs: [...] });
  }
  if (url.includes('/games/') && url.includes('/pdfs')) {
    return createJsonResponse({ pdfs: [] });
  }
  throw new Error(`Unexpected: ${url}`);
});
```

**After:**
```typescript
const router = new MockApiRouter()
  .get('/games/:gameId/pdfs', ({ params }) => {
    if (params.gameId === 'game-1') {
      return createJsonResponse({ pdfs: [...] });
    }
    return createJsonResponse({ pdfs: [] });
  });

mockFetch.mockImplementation(router.toMockImplementation());
```

### Migrating to Presets

**Before:**
```typescript
const router = new MockApiRouter()
  .get('/auth/me', () => createJsonResponse({ user: { id: 'user-1', role: 'Admin' } }))
  .get('/games', () => createJsonResponse([{ id: 'game-1', name: 'Chess' }]))
  .post('/games', () => createJsonResponse({ id: 'game-new' }, 201));
```

**After:**
```typescript
const router = new MockApiRouter();
MockApiPresets.auth(router, { userId: 'user-1', role: 'Admin' });
MockApiPresets.games(router, {
  games: [{ id: 'game-1', name: 'Chess' }],
  createResponse: { id: 'game-new', name: 'New Game' }
});
```

## Examples

### Complete Test Example

```typescript
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { MockApiRouter } from '@/__tests__/utils/mock-api-router';
import { MockApiPresets } from '@/__tests__/utils/mock-api-presets';
import UploadPage from '../upload';

describe('UploadPage', () => {
  const originalFetch = global.fetch;

  afterAll(() => {
    global.fetch = originalFetch;
  });

  it('uploads PDF successfully', async () => {
    const router = new MockApiRouter();
    MockApiPresets.uploadWorkflow(router, {
      auth: { userId: 'user-1', role: 'Admin' },
      games: { games: [{ id: 'game-1', name: 'Chess' }] },
      ingest: {
        uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
        statusResponses: [
          { documentId: 'pdf-123', fileName: 'rules.pdf', processingStatus: 'completed' }
        ]
      }
    });

    const mockFetch = jest.fn(router.toMockImplementation());
    global.fetch = mockFetch;

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
    const file = new File(['pdf content'], 'rules.pdf', { type: 'application/pdf' });

    fireEvent.change(fileInput, { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));
    fireEvent.click(screen.getByRole('button', { name: /Upload & Continue/i }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/ingest/pdf'),
        expect.objectContaining({ method: 'POST' })
      )
    );
  });
});
```

## Troubleshooting

### "No handler for GET /some/endpoint"

This error means you forgot to register a route. Check:
1. The URL pattern is correct (including leading `/`)
2. The HTTP method matches (GET, POST, etc.)
3. Route parameters match your URL structure

### Tests Pass Locally But Fail in CI

Make sure:
1. You're not relying on specific URL formats (localhost vs. full URLs)
2. Route patterns use parameters (`:id`) instead of hardcoded IDs
3. Mock responses match production API structure

### MockApiRouter Not Matching Routes

Common issues:
- **Trailing slashes**: `/games/` !== `/games`
- **Query parameters**: Not supported yet (use base path only)
- **Case sensitivity**: Paths are case-sensitive

## Further Reading

- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [React Testing Library](https://testing-library.com/docs/react-testing-library/intro/)
- [MeepleAI CLAUDE.md](../../CLAUDE.md) - Project-level testing guidelines
