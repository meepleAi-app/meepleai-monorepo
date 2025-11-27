# Mock Test Fixtures

Comprehensive mock data infrastructure for consistent, type-safe testing across the frontend test suite.

## Overview

This directory provides:

1. **Type-Safe Mock Factories**: Generate complete mock objects that match backend API responses
2. **Validation Helpers**: Type guards to ensure mock data completeness
3. **Reusable Presets**: Common mock scenarios (admin user, successful response, error states)
4. **Centralized Consistency**: Single source of truth for mock data structures

## Quick Start

```typescript
import {
  createMockAuthResponse,
  createMockGame,
  createMockDashboardStats,
  mockAdminAuth,
} from '../fixtures/common-fixtures';

// Use factory functions with sensible defaults
const authResponse = createMockAuthResponse();
const game = createMockGame();
const stats = createMockDashboardStats();

// Or use preset shortcuts
const adminAuth = mockAdminAuth();
```

## File Structure

### `common-fixtures.ts` (PRIMARY)

**Purpose**: Central repository for all common mock data types and factories

**Exports**:

#### Auth Fixtures
- `MockUser` - User type matching backend
- `MockAuthResponse` - Auth response with user + expiration
- `MockSessionStatusResponse` - Session status with remaining time (AUTH-05)
- `createMockUser(overrides?)` - Factory for custom users
- `createMockAuthResponse(userOverrides?)` - Factory for auth responses
- `createMockSessionStatus(overrides?)` - Factory for session status
- `mockAdminAuth()`, `mockEditorAuth()`, `mockUserAuth()` - Role presets

#### Game Fixtures
- `MockGame` - Game entity type
- `createMockGame(overrides?)` - Game factory
- `mockChessGame()`, `mockTicTacToeGame()` - Preset games

#### RuleSpec Fixtures
- `MockRuleAtom` - Individual rule type
- `MockRuleSpec` - Complete RuleSpec type
- `createMockRuleAtom(overrides?)` - Rule factory
- `createMockRuleSpec(overrides?)` - RuleSpec factory

#### Analytics Fixtures (NEW)
- `MockDashboardMetrics` - Dashboard metrics type
- `MockTimeSeriesDataPoint` - Chart data type
- `MockDashboardStats` - Complete dashboard response
- `createMockDashboardMetrics(overrides?)` - Metrics factory
- `createMockTimeSeriesData(days, baseValue)` - Chart data generator
- `createMockDashboardStats(overrides?)` - Complete stats factory

#### PDF Fixtures (NEW)
- `MockPdfDocument` - PDF document type
- `createMockPdfDocument(overrides?)` - PDF factory
- `createMockPdfList(count)` - Generate multiple PDFs

#### Type Validation Helpers (NEW)
- `isValidMockUser(obj)` - Type guard for users
- `isValidMockAuthResponse(obj)` - Type guard for auth
- `isValidMockGame(obj)` - Type guard for games
- `isValidMockRuleSpec(obj)` - Type guard for RuleSpecs
- `validateMockData(name, data, validator)` - Assert with helpful errors

### `upload-mocks.ts`

**Purpose**: Specialized mocks for upload workflow tests

**Key Functions**:
- `setupUploadMocks(config)` - Complete upload flow router
- `createAuthMock(options)` - Deprecated, use `common-fixtures`
- `createGameMock(options)` - Deprecated, use `common-fixtures`
- `createPdfMock(options)` - Deprecated, use `createMockPdfDocument`
- `createRuleSpecMock(options)` - Deprecated, use `createMockRuleSpec`

**Note**: This file now wraps `common-fixtures` for backward compatibility.

### `test-helpers.ts`

**Purpose**: Reusable test utilities for common testing patterns

**Key Functions**:
- `waitForAuthComplete(timeout)` - Wait for auth check
- `waitForLoadingComplete(timeout)` - Wait for loading states
- `waitForPageReady(timeout)` - Combined auth + loading wait
- `waitForEditorReady(timeout)` - Editor-specific loading
- `getEditorTextarea()` - Get main textarea element
- `getButtonByName(name)` - Get button with helpful errors
- `waitForText(text, timeout)` - Wait for text to appear
- `waitForError(errorText?, timeout)` - Wait for error message
- `fillField(label, value, user)` - Fill form field
- `clickAndWaitForReady(buttonName, user, timeout)` - Click + wait

### `sse-test-helpers.ts` (Issue #1502)

**Purpose**: Server-Sent Events (SSE) mocking utilities for streaming tests

**Core Functions**:
- `createSSEResponse(events)` - Create SSE Response with ReadableStream
- `createSSEErrorResponse(code, message)` - Create error SSE response

**Event Builders**:
- `createTokenEvent(token)` - Token streaming event
- `createStateUpdateEvent(state)` - State update event
- `createCitationsEvent(citations)` - Citations event
- `createFollowUpQuestionsEvent(questions)` - Follow-up questions event
- `createCompleteEvent(totalTokens, confidence, snippets?)` - Completion event
- `createErrorEvent(code, message)` - Error event

**Composite Helpers** (Common Scenarios):
- `createTokenStreamResponse(tokens, options?)` - Complete token stream with completion
- `createStateUpdateResponse(states)` - State updates with completion
- `createCitationsResponse(citations, answer?, confidence?)` - Citations with optional answer

## Mock API Router

Located in `__tests__/utils/mock-api-router.ts`

**Purpose**: Fluent API for defining mock API routes with pattern matching

```typescript
import { MockApiRouter, createJsonResponse, createErrorResponse } from '../utils/mock-api-router';

const router = new MockApiRouter()
  .get('/api/v1/auth/me', () => createJsonResponse(mockAdminAuth()))
  .get('/api/v1/games', () => createJsonResponse([mockChessGame()]))
  .get('/api/v1/games/:gameId/rulespec', ({ params }) =>
    createJsonResponse(createMockRuleSpec({ gameId: params.gameId }))
  )
  .post('/api/v1/games', () => createJsonResponse(createMockGame(), 201));

global.fetch = jest.fn(router.toMockImplementation());
```

**Features**:
- URL pattern matching with `:param` syntax
- Normalized path handling (trailing slashes, query strings)
- Clear error messages when routes don't match
- Type-safe route handlers with context

## Usage Patterns

### Pattern 0: SSE Streaming Mocks (Issue #1502)

```typescript
import { createSSEResponse, createTokenEvent, createCompleteEvent } from '../fixtures/sse-test-helpers';
import type { Mock } from 'vitest';

test('handles token streaming', async () => {
  const events = [
    createTokenEvent('Hello'),
    createTokenEvent(' World'),
    createCompleteEvent(2, 0.95),
  ];

  (global.fetch as Mock).mockResolvedValue(createSSEResponse(events));

  const { result } = renderHook(() => useStreamingChat());
  await act(async () => {
    await result.current[1].startStreaming('game-123', 'test');
  });

  await waitFor(() => {
    expect(result.current[0].currentAnswer).toBe('Hello World');
  });
});
```

**Or use composite helpers**:
```typescript
import { createTokenStreamResponse } from '../fixtures/sse-test-helpers';

test('simplified token streaming', async () => {
  (global.fetch as Mock).mockResolvedValue(
    createTokenStreamResponse(['Hello', ' World'], {
      totalTokens: 2,
      confidence: 0.95,
    })
  );
  // ... test code
});
```

### Pattern 1: Simple Mock Response

```typescript
import { createMockAuthResponse } from '../fixtures/common-fixtures';
import { api } from '../../lib/api';

jest.mock('../../lib/api');
const mockApi = api as jest.Mocked<typeof api>;

test('renders user info', async () => {
  mockApi.get.mockResolvedValueOnce(createMockAuthResponse());
  render(<UserProfile />);

  await waitFor(() => {
    expect(screen.getByText('test@meepleai.dev')).toBeInTheDocument();
  });
});
```

### Pattern 2: Custom Mock Data

```typescript
import { createMockDashboardStats, createMockDashboardMetrics } from '../fixtures/common-fixtures';

const customStats = createMockDashboardStats({
  metrics: createMockDashboardMetrics({
    totalUsers: 500,
    apiRequestsToday: 10000,
  }),
  userTrend: createMockTimeSeriesData(30, 100), // 30 days, base 100
});

mockApi.get.mockResolvedValueOnce(customStats);
```

### Pattern 3: Complete Upload Flow

```typescript
import { setupUploadMocks } from '../fixtures/upload-mocks';

const mockFetch = setupUploadMocks({
  auth: createAuthMock({ role: 'Editor' }),
  games: [createGameMock()],
  uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
  pdfStatusSequence: [
    { processingStatus: 'pending' },
    { processingStatus: 'processing' },
    { processingStatus: 'completed' },
  ],
  ruleSpec: createRuleSpecMock(),
});

global.fetch = mockFetch;
```

### Pattern 4: Type Validation

```typescript
import { validateMockData, isValidMockAuthResponse } from '../fixtures/common-fixtures';

// At start of test setup
const authData = createMockAuthResponse({ role: 'Admin' });
validateMockData('AuthResponse', authData, isValidMockAuthResponse);
// Throws descriptive error if incomplete

// Or manual validation
if (!isValidMockAuthResponse(authData)) {
  throw new Error('Invalid auth response structure');
}
```

## Best Practices

### 1. Always Use Factory Functions

❌ **Bad**: Manual object construction
```typescript
const user = {
  id: 'user-1',
  email: 'test@example.com',
  // Missing displayName, role - test will fail
};
```

✅ **Good**: Use factory with defaults
```typescript
const user = createMockUser({ role: 'Admin' });
// All required fields present with sensible defaults
```

### 2. Validate Complex Mocks

```typescript
// For critical tests, validate mock completeness
const stats = createMockDashboardStats();
validateMockData('DashboardStats', stats, (obj) => {
  return obj && obj.metrics && Array.isArray(obj.userTrend);
});
```

### 3. Use Presets for Common Scenarios

```typescript
// Instead of building from scratch
const auth = mockAdminAuth(); // Admin user preset
const game = mockChessGame(); // Chess game preset
```

### 4. Handle Locale-Sensitive Values

```typescript
// Numbers may format differently in test vs production
expect(screen.getByText((content, element) => {
  const text = element?.textContent || '';
  return text === '1,250' || text === '1250'; // Both formats
})).toBeInTheDocument();
```

### 5. Use MockApiRouter for Complex Flows

```typescript
const router = new MockApiRouter()
  .get('/api/v1/games/:gameId', ({ params }) => {
    return createJsonResponse(createMockGame({ id: params.gameId }));
  })
  .post('/api/v1/games/:gameId/pdfs', ({ params, init }) => {
    // Access request body: init?.body
    return createJsonResponse({ success: true }, 201);
  });
```

## Migration Guide

### Migrating Old Tests to New Fixtures

**Before**:
```typescript
const mockUser = {
  id: 'user-1',
  email: 'test@example.com',
  displayName: 'Test User',
  role: 'Admin',
};

const mockAuthResponse = {
  user: mockUser,
  expiresAt: new Date(Date.now() + 3600000).toISOString(),
};
```

**After**:
```typescript
import { createMockAuthResponse } from '../fixtures/common-fixtures';

const mockAuthResponse = createMockAuthResponse({ role: 'Admin' });
// Or use preset: mockAdminAuth()
```

## Troubleshooting

### Issue: "Unable to find element with text: X"

**Cause**: Mock data doesn't match what component actually renders

**Solution**:
1. Check component's formatting logic (e.g., `toLocaleString()`)
2. Use flexible text matchers in assertions
3. Verify mock has all required fields using type validation

### Issue: "MockApiRouter: No handler for GET /api/..."

**Cause**: URL pattern doesn't match registered route

**Solution**:
1. Check for trailing slashes: `/api/games` vs `/api/games/`
2. Verify query parameters are stripped in pattern
3. Use `router.getRoutes()` to debug available routes

### Issue: Test fails with "undefined property"

**Cause**: Mock object missing required nested properties

**Solution**:
1. Use factory functions that provide complete structures
2. Validate mocks with type guards
3. Check `validateMockObject()` error messages for missing fields

## Contributing

When adding new mock types:

1. Add types and factories to `common-fixtures.ts`
2. Add type guard validators
3. Create preset functions for common scenarios
4. Update this README with examples
5. Ensure backward compatibility with existing tests

## Testing Async Components

When testing components with async effects (API calls, timers, intervals):

### Key Patterns

1. **Use waitFor for all assertions that depend on async state**:
   ```typescript
   await waitFor(() => {
     expect(screen.getByText('Loaded')).toBeInTheDocument();
   });
   ```

2. **Use act() when advancing timers**:
   ```typescript
   import { advanceTimersAndFlush } from '../utils/async-test-helpers';

   await advanceTimersAndFlush(2000); // Advance 2 seconds and flush promises
   ```

3. **Never wrap render() in act()** - React Testing Library handles this automatically:
   ```typescript
   ❌ await act(async () => { render(<Component />); });
   ✅ render(<Component />);
   ```

4. **Use setupUserEvent() for user interactions**:
   ```typescript
   import { setupUserEvent } from '../utils/async-test-helpers';

   const user = setupUserEvent();
   await user.click(button);
   ```

### Available Helpers

From `__tests__/utils/async-test-helpers.ts`:

- `advanceTimersAndFlush(ms)` - Advance fake timers and flush promises
- `waitForAsyncEffects()` - Wait for async effects to complete
- `setupUserEvent()` - Setup userEvent with act() handling
- `flushAllPending()` - Flush all pending timers and promises
- `waitForCondition(callback, timeout)` - Custom timeout wrapper

### Complete Example

```typescript
import { advanceTimersAndFlush, setupUserEvent, flushAllPending } from '../utils/async-test-helpers';

describe('ProcessingProgress', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(async () => {
    await flushAllPending();
    jest.useRealTimers();
  });

  it('should poll every 2 seconds', async () => {
    const user = setupUserEvent();
    render(<ProcessingProgress pdfId="test-id" />);

    await waitFor(() => {
      expect(mockApi).toHaveBeenCalledTimes(1);
    });

    await advanceTimersAndFlush(2000);

    await waitFor(() => {
      expect(mockApi).toHaveBeenCalledTimes(2);
    });
  });
});
```

See `TESTING_PATTERNS.md` for comprehensive guidance on React act() warnings and async testing best practices.

## Related Documentation

- Mock API Router: `__tests__/utils/mock-api-router.ts`
- Test Helpers: `__tests__/fixtures/test-helpers.ts`
- Async Test Helpers: `__tests__/utils/async-test-helpers.ts`
- Testing Patterns: `TESTING_PATTERNS.md`
- API Types: `lib/api.ts`
