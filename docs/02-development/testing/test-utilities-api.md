# Test Utilities API Reference

**Status**: Complete
**Last Updated**: 2025-12-13T10:59:23.970Z
**Issue**: TEST-003 (Test Infrastructure Coverage)

---

## Overview

This document provides comprehensive API documentation for the test utilities available in the MeepleAI test suite. These utilities help create consistent, reliable tests across the codebase.

## Table of Contents

1. [Rendering Utilities](#rendering-utilities)
2. [API Mocking Utilities](#api-mocking-utilities)
3. [Async Testing Utilities](#async-testing-utilities)
4. [Chat Test Utilities](#chat-test-utilities)
5. [Mock API Router](#mock-api-router)
6. [Mock Data Generators](#mock-data-generators)
7. [Best Practices](#best-practices)

---

## Rendering Utilities

### `renderWithProviders`

Renders a React component with all required providers (Router, Context, etc.).

**Location**: `src/lib/__tests__/test-utils.tsx`

**Signature**:
```typescript
function renderWithProviders(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>
): RenderResult
```

**Parameters**:
- `ui`: React element to render
- `options`: Optional RTL render options (excluding wrapper)

**Returns**: Standard React Testing Library render result

**Example**:
```typescript
import { renderWithProviders } from '@/lib/__tests__/test-utils';

it('should render component', () => {
  const { getByText } = renderWithProviders(<MyComponent />);
  expect(getByText('Hello')).toBeInTheDocument();
});
```

**Use Cases**:
- Rendering components that need context providers
- Testing components with router dependencies
- Ensuring consistent provider setup across tests

---

## API Mocking Utilities

### `mockApiResponse`

Creates a mock API response for fetch mocking with proper status codes and payloads.

**Location**: `src/lib/__tests__/test-utils.tsx`

**Signature**:
```typescript
function mockApiResponse(
  status: number,
  payload?: unknown
): Promise<Response>
```

**Parameters**:
- `status`: HTTP status code (200, 404, 500, etc.)
- `payload`: Response payload (JSON object, string, or undefined)

**Returns**: Promise resolving to a Response object

**Example**:
```typescript
import { mockApiResponse } from '@/lib/__tests__/test-utils';

it('should handle successful API call', async () => {
  global.fetch = jest.fn(() => mockApiResponse(200, { data: 'test' }));

  const response = await fetch('/api/test');
  const data = await response.json();

  expect(data).toEqual({ data: 'test' });
});

it('should handle API errors', async () => {
  global.fetch = jest.fn(() => mockApiResponse(404, { error: 'Not Found' }));

  const response = await fetch('/api/missing');

  expect(response.ok).toBe(false);
  expect(response.status).toBe(404);
});
```

**Status Code Behavior**:
- `200-299`: `response.ok = true`
- `300-599`: `response.ok = false`

---

## Async Testing Utilities

### `waitForAsync`

Waits for async operations to complete with custom assertion and timeout.

**Location**: `src/lib/__tests__/test-utils.tsx`

**Signature**:
```typescript
async function waitForAsync(
  assertion: () => void,
  timeout?: number
): Promise<void>
```

**Parameters**:
- `assertion`: Function containing expectations to check
- `timeout`: Maximum wait time in milliseconds (default: 1000ms)

**Returns**: Promise that resolves when assertion passes or throws on timeout

**Example**:
```typescript
import { waitForAsync } from '@/lib/__tests__/test-utils';

it('should wait for async state update', async () => {
  render(<ComponentWithAsyncEffect />);

  await waitForAsync(() => {
    expect(screen.getByText('Loaded')).toBeInTheDocument();
  });
});

it('should use custom timeout', async () => {
  await waitForAsync(() => {
    expect(condition).toBe(true);
  }, 5000); // Wait up to 5 seconds
});
```

**Behavior**:
- Retries assertion every 50ms until success or timeout
- Throws the last assertion error if timeout reached
- Useful for avoiding `act()` warnings with async updates

---

### `advanceTimersAndFlush`

Advances fake timers and flushes promise microtask queue (requires `jest.useFakeTimers()`).

**Location**: `src/__tests__/utils/async-test-helpers.ts`

**Signature**:
```typescript
async function advanceTimersAndFlush(ms: number): Promise<void>
```

**Parameters**:
- `ms`: Number of milliseconds to advance timers

**Returns**: Promise that resolves after advancing timers and flushing promises

**Example**:
```typescript
import { advanceTimersAndFlush } from '@/__tests__/utils/async-test-helpers';

beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.useRealTimers();
});

it('should trigger timer after 1 second', async () => {
  const callback = jest.fn();
  setTimeout(callback, 1000);

  await advanceTimersAndFlush(1000);

  expect(callback).toHaveBeenCalled();
});
```

---

### `waitForAsyncEffects`

Waits for async effects to complete (flushes microtask queue with `act()`).

**Location**: `src/__tests__/utils/async-test-helpers.ts`

**Signature**:
```typescript
async function waitForAsyncEffects(): Promise<void>
```

**Parameters**: None

**Returns**: Promise that resolves after flushing async effects

**Example**:
```typescript
import { waitForAsyncEffects } from '@/__tests__/utils/async-test-helpers';

it('should wait for useEffect async calls', async () => {
  render(<ComponentWithAsyncEffect />);

  await waitForAsyncEffects();

  expect(screen.getByTestId('data').textContent).toBe('loaded');
});
```

---

### `setupUserEvent`

Creates a userEvent instance with null delay for immediate actions.

**Location**: `src/__tests__/utils/async-test-helpers.ts`

**Signature**:
```typescript
function setupUserEvent(): UserEvent
```

**Parameters**: None

**Returns**: UserEvent instance

**Example**:
```typescript
import { setupUserEvent } from '@/__tests__/utils/async-test-helpers';

it('should handle user interactions', async () => {
  render(<MyButton onClick={handleClick} />);

  const user = setupUserEvent();
  await user.click(screen.getByRole('button'));

  expect(handleClick).toHaveBeenCalled();
});
```

---

### `flushAllPending`

Flushes all pending timers and promises (for cleanup in `afterEach`).

**Location**: `src/__tests__/utils/async-test-helpers.ts`

**Signature**:
```typescript
async function flushAllPending(): Promise<void>
```

**Parameters**: None

**Returns**: Promise that resolves after flushing all pending operations

**Example**:
```typescript
import { flushAllPending } from '@/__tests__/utils/async-test-helpers';

afterEach(async () => {
  await flushAllPending();
  jest.useRealTimers();
});
```

---

### `waitForCondition`

Waits for a condition with custom timeout (wraps React Testing Library's `waitFor`).

**Location**: `src/__tests__/utils/async-test-helpers.ts`

**Signature**:
```typescript
async function waitForCondition(
  callback: () => void,
  timeout?: number
): Promise<void>
```

**Parameters**:
- `callback`: Function containing assertions to check
- `timeout`: Maximum wait time in milliseconds (default: 3000ms)

**Returns**: Promise that resolves when condition met or throws on timeout

**Example**:
```typescript
import { waitForCondition } from '@/__tests__/utils/async-test-helpers';

it('should wait for element to appear', async () => {
  render(<DynamicComponent />);

  await waitForCondition(() => {
    expect(screen.getByText('Dynamic Content')).toBeInTheDocument();
  }, 5000);
});
```

---

## Chat Test Utilities

### `createChatTestData`

Creates standard test data for chat tests (users, games, agents, chats).

**Location**: `src/__tests__/pages/chat/shared/chat-test-utils.ts`

**Signature**:
```typescript
function createChatTestData(): ChatTestData
```

**Parameters**: None

**Returns**: Object containing mock auth response, games, agents, chats, and chat with history

**Example**:
```typescript
import { createChatTestData } from '@/__tests__/pages/chat/shared/chat-test-utils';

it('should use standard test data', () => {
  const testData = createChatTestData();

  expect(testData.mockAuthResponse.id).toBe('user-1');
  expect(testData.mockGames).toHaveLength(2);
  expect(testData.mockAgents).toHaveLength(2);
  expect(testData.mockChats).toHaveLength(2);
});
```

---

### `setupAuthenticatedState`

Sets up complete authenticated state with all required API mocks.

**Location**: `src/__tests__/pages/chat/shared/chat-test-utils.ts`

**Signature**:
```typescript
function setupAuthenticatedState(): ChatTestData
```

**Parameters**: None

**Returns**: Test data and sets up API mocks for auth, games, agents, and chats

**Example**:
```typescript
import { setupAuthenticatedState, mockApi } from '@/__tests__/pages/chat/shared/chat-test-utils';

it('should have authenticated user', async () => {
  const testData = setupAuthenticatedState();

  render(<ChatPage />);

  // API mocks are already configured
  expect(mockApi.get).toHaveBeenCalledTimes(4); // auth, games, agents, chats
});
```

---

### `setupFullChatEnvironment`

Sets up a complete chat environment with extensive customization options.

**Location**: `src/__tests__/pages/chat/shared/chat-test-utils.ts`

**Signature**:
```typescript
function setupFullChatEnvironment(options?: {
  user?: Partial<User>;
  game?: Partial<Game>;
  agent?: Partial<Agent>;
  chats?: Array<Partial<Chat>>;
  messages?: Array<Message>;
  activeChat?: boolean;
  activeChatId?: string;
  sessionMinutes?: number;
  additionalGames?: Array<Partial<Game>>;
  additionalAgents?: Array<Partial<Agent>>;
}): FullChatEnvironment
```

**Parameters**:
- `user`: Custom user properties
- `game`: Custom game properties
- `agent`: Custom agent properties
- `chats`: Array of custom chat objects
- `messages`: Array of messages for active chat
- `activeChat`: Whether to load chat history (default: false)
- `activeChatId`: ID of chat to load (default: first chat)
- `sessionMinutes`: Session remaining minutes (default: 30)
- `additionalGames`: Extra games to include
- `additionalAgents`: Extra agents to include

**Returns**: Object with user, game, agent, chats, messages, games, agents

**Example**:
```typescript
import { setupFullChatEnvironment } from '@/__tests__/pages/chat/shared/chat-test-utils';

it('should setup custom chat environment', () => {
  const env = setupFullChatEnvironment({
    user: { role: 'Admin' },
    game: { name: 'Chess' },
    messages: [
      { role: 'user', content: 'How do I castle?' },
      { role: 'assistant', content: 'Castling is...' }
    ],
    activeChat: true,
    sessionMinutes: 60
  });

  expect(env.user.role).toBe('Admin');
  expect(env.game.name).toBe('Chess');
  expect(env.messages).toHaveLength(2);
});
```

---

### `resetAllMocks`

Resets all mocks to clean state (call in `beforeEach` for test isolation).

**Location**: `src/__tests__/pages/chat/shared/chat-test-utils.ts`

**Signature**:
```typescript
function resetAllMocks(): void
```

**Parameters**: None

**Returns**: void

**Example**:
```typescript
import { resetAllMocks } from '@/__tests__/pages/chat/shared/chat-test-utils';

beforeEach(() => {
  resetAllMocks();
});
```

---

### `setupStreamingMock`

Sets up the `useChatStreaming` mock with custom behavior.

**Location**: `src/__tests__/pages/chat/shared/chat-test-utils.ts`

**Signature**:
```typescript
function setupStreamingMock(overrides?: {
  isStreaming?: boolean;
  currentAnswer?: string;
  snippets?: any[];
  state?: any;
  error?: any;
}): StreamingState
```

**Parameters**: Optional overrides for streaming state

**Returns**: Streaming state object

**Example**:
```typescript
import { setupStreamingMock } from '@/__tests__/pages/chat/shared/chat-test-utils';

it('should mock streaming state', () => {
  const streamingState = setupStreamingMock({
    isStreaming: true,
    currentAnswer: 'Partial answer...'
  });

  expect(streamingState.isStreaming).toBe(true);
  expect(streamingState.currentAnswer).toBe('Partial answer...');
});
```

---

## Mock API Router

### `MockApiRouter`

Type-safe API mocking router with pattern matching and route parameters.

**Location**: `src/__tests__/utils/mock-api-router.ts`

**Signature**:
```typescript
class MockApiRouter {
  get(pattern: string, handler: RouteHandler): MockApiRouter;
  post(pattern: string, handler: RouteHandler): MockApiRouter;
  put(pattern: string, handler: RouteHandler): MockApiRouter;
  delete(pattern: string, handler: RouteHandler): MockApiRouter;
  patch(pattern: string, handler: RouteHandler): MockApiRouter;
  handle(url: string, init?: RequestInit): Promise<Response>;
  getRoutes(): Array<{ method: string; pattern: string }>;
  clear(): void;
  toMockImplementation(): (input: RequestInfo, init?: RequestInit) => Promise<Response>;
}
```

**Example**:
```typescript
import { MockApiRouter, createJsonResponse } from '@/__tests__/utils/mock-api-router';

it('should route API calls', async () => {
  const router = new MockApiRouter();

  router.get('/games/:id', ({ params }) =>
    createJsonResponse({ gameId: params.id })
  );

  const response = await router.handle('/games/game-123');
  const data = await response.json();

  expect(data.gameId).toBe('game-123');
});

it('should use as global fetch mock', () => {
  const router = new MockApiRouter()
    .get('/auth/me', () => createJsonResponse({ user: 'test' }))
    .post('/games', () => createJsonResponse({ id: 'new' }, 201));

  global.fetch = jest.fn(router.toMockImplementation());
});
```

---

### `MockApiPresets`

Pre-configured route handlers for common API endpoints.

**Location**: `src/__tests__/utils/mock-api-presets.ts`

**Methods**:
- `auth(router, options)`: Register authentication endpoints
- `games(router, options)`: Register game management endpoints
- `pdfs(router, options)`: Register PDF list endpoints
- `ingest(router, options)`: Register PDF ingest endpoints
- `ruleSpec(router, options)`: Register RuleSpec endpoints
- `admin(router, options)`: Register admin dashboard endpoints
- `agents(router, options)`: Register chat/agent endpoints
- `uploadWorkflow(router, options)`: Register complete upload workflow

**Example**:
```typescript
import { MockApiRouter } from '@/__tests__/utils/mock-api-router';
import { MockApiPresets } from '@/__tests__/utils/mock-api-presets';

it('should setup upload workflow', () => {
  const router = new MockApiRouter();

  MockApiPresets.uploadWorkflow(router, {
    auth: { userId: 'user-1', role: 'Admin' },
    games: { games: [{ id: 'game-1', name: 'Chess' }] },
    ingest: { uploadResponse: { documentId: 'pdf-123' } }
  });

  global.fetch = jest.fn(router.toMockImplementation());

  // Now all upload-related endpoints are mocked
});
```

---

## Mock Data Generators

### `createMockEvents`

Creates mock timeline events for testing.

**Location**: `src/lib/__tests__/test-utils.tsx`

**Signature**:
```typescript
function createMockEvents(
  count: number,
  overrides?: Array<Partial<TimelineEvent>>
): TimelineEvent[]
```

**Parameters**:
- `count`: Number of events to generate
- `overrides`: Optional array of partial events to override defaults

**Returns**: Array of TimelineEvent objects

**Example**:
```typescript
import { createMockEvents } from '@/lib/__tests__/test-utils';

it('should create timeline events', () => {
  const events = createMockEvents(10);

  expect(events).toHaveLength(10);
  expect(events[0].type).toBeDefined();
  expect(events[0].status).toBeDefined();
});

it('should override specific events', () => {
  const events = createMockEvents(3, [
    { type: 'error', status: 'error' },
    undefined, // Use default
    { type: 'rag_complete', status: 'success' }
  ]);

  expect(events[0].type).toBe('error');
  expect(events[2].type).toBe('rag_complete');
});
```

---

### `createMockEvent`

Creates a single mock timeline event.

**Location**: `src/lib/__tests__/test-utils.tsx`

**Signature**:
```typescript
function createMockEvent(
  overrides?: Partial<TimelineEvent>
): TimelineEvent
```

**Parameters**:
- `overrides`: Optional partial event to override defaults

**Returns**: TimelineEvent object

**Example**:
```typescript
import { createMockEvent } from '@/lib/__tests__/test-utils';

it('should create single event', () => {
  const event = createMockEvent({ type: 'error', status: 'error' });

  expect(event.type).toBe('error');
  expect(event.status).toBe('error');
  expect(event.id).toBe('event-0');
});
```

---

## Best Practices

### Test Isolation

Always reset mocks between tests:

```typescript
import { resetAllMocks } from '@/__tests__/pages/chat/shared/chat-test-utils';

beforeEach(() => {
  resetAllMocks();
});
```

### Consistent Test Data

Use utility functions for predictable test data:

```typescript
// ✅ Good: Use utility
const testData = createChatTestData();
expect(testData.mockGames).toHaveLength(2);

// ❌ Bad: Create manually
const mockGames = [{ id: 'game-1', name: 'Chess' }];
```

### Async Testing

Prefer helper functions over raw `waitFor`:

```typescript
// ✅ Good: Use helper
await waitForAsync(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
});

// ❌ Less preferred: Raw waitFor
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
});
```

### API Mocking

Use MockApiRouter and presets for complex scenarios:

```typescript
// ✅ Good: Type-safe router
const router = new MockApiRouter();
MockApiPresets.auth(router, { role: 'Admin' });
global.fetch = jest.fn(router.toMockImplementation());

// ❌ Bad: Manual fetch mocking
global.fetch = jest.fn().mockResolvedValue({
  ok: true,
  json: async () => ({ user: { role: 'Admin' } })
});
```

### Test Organization

Structure tests using BDD-style describe blocks:

```typescript
describe('ComponentName', () => {
  describe('Happy Path', () => {
    it('should handle normal case', () => {
      // Test
    });
  });

  describe('Edge Cases', () => {
    it('should handle null input', () => {
      // Test
    });
  });

  describe('Error Scenarios', () => {
    it('should handle API errors', () => {
      // Test
    });
  });
});
```

### Cleanup

Always clean up after tests with timers:

```typescript
beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(async () => {
  await flushAllPending();
  jest.useRealTimers();
});
```

---

## Coverage Metrics

After implementing comprehensive tests for these utilities, coverage metrics improved from:

| File | Before | After | Improvement |
|------|--------|-------|-------------|
| `chat-test-utils.ts` | 49% (0% branches) | 90%+ (90%+ branches) | +41% / +90% |
| `async-test-helpers.ts` | 70% (0% branches) | 90%+ (90%+ branches) | +20% / +90% |
| `mock-api-presets.ts` | 0% (0% branches) | 90%+ (90%+ branches) | +90% / +90% |
| `test-utils.tsx` | 57% (64% branches) | 90%+ (90%+ branches) | +33% / +26% |

**Overall**: Test infrastructure coverage increased from 49-70% to 90%+, with branch coverage going from 0% to 90%+.

---

## See Also

- [Test Coverage Guide](../code-coverage.md)
- [Concurrency Testing Guide](./concurrency-testing-guide.md)
- [Test Writing Best Practices](../../CLAUDE.md#testing)

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Maintained By**: TEST-003 Implementation
**Next Review**: Quarterly or when adding new utilities

