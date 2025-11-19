# Worker Mocking Patterns for Testing

**Version**: 1.0
**Last Updated**: 2025-11-17
**Related Issue**: #1238 (FE-TEST-010)

## Overview

This guide documents patterns for testing Web Worker-based components in the MeepleAI frontend codebase, specifically for the Upload Queue Web Worker architecture (FE-IMP-008).

## Architecture Context

### Upload Queue Components

```
useUploadQueue (React Hook)
    ↓
UploadQueueStore (useSyncExternalStore bridge)
    ↓
Upload Queue Worker (Web Worker)
    ↓
Browser Fetch API (PDF upload)
```

**Key Challenge**: Store instantiates Worker at module load time, requiring mocks to be in place BEFORE any imports.

## Mock Infrastructure

### MockUploadWorker

Location: `apps/web/src/__tests__/helpers/uploadQueueMocks.ts`

**Features**:
- Full worker message protocol simulation (10+ message types)
- Configurable upload delays and error simulation
- ArrayBuffer file transfer support
- Worker crash simulation for lifecycle testing
- State inspection helpers (active uploads, cache size)

**Example Usage**:

```typescript
import { MockUploadWorker } from '../../__tests__/helpers/uploadQueueMocks';

// Create instance with configuration
const mockWorker = new MockUploadWorker({
  uploadDelay: 10,     // ms delay for upload simulation
  autoUpload: true,    // Automatically start uploads when files added
  apiBase: 'http://localhost:5080'
});

// Configure error simulation
mockWorker.setUploadError('item-id', 'Test error message');

// Inspect state
console.log(mockWorker.getActiveUploadsCount());
console.log(mockWorker.getFileDataCacheSize());

// Simulate worker crash
mockWorker.simulateCrash();
```

### MockBroadcastChannel

Simulates multi-tab communication for coordination testing.

**Features**:
- Multi-instance message broadcasting
- Channel name-based routing
- Event listener support
- Cleanup utilities

**Example Usage**:

```typescript
import { MockBroadcastChannel } from '../../__tests__/helpers/uploadQueueMocks';

// Mock globally
global.BroadcastChannel = MockBroadcastChannel as any;

// Create channel
const channel = new MockBroadcastChannel('upload-queue-sync');

// Listen for messages
channel.onmessage = (event) => {
  console.log('Received:', event.data);
};

// Send message
channel.postMessage({ type: 'QUEUE_SYNC', payload: {} });

// Cleanup between tests
MockBroadcastChannel.clearAll();
```

## Test Setup Pattern

### Problem: Module-Level Worker Instantiation

The `UploadQueueStore` creates a Worker in its constructor, which runs at module load time:

```typescript
// UploadQueueStore.ts (simplified)
class UploadQueueStore {
  constructor() {
    if (typeof window !== 'undefined') {
      this.initializeWorker(); // Runs immediately!
    }
  }

  private initializeWorker() {
    this.worker = new Worker(
      new URL('../workers/uploadQueue.worker.ts', import.meta.url),
      { type: 'module' }
    );
  }
}

// Module-level instantiation
export const uploadQueueStore = new UploadQueueStore();
```

### Solution: Mock BEFORE Imports

**Pattern 1: Shared Mock Instance** (Recommended)

```typescript
import { MockUploadWorker, MockBroadcastChannel } from '../../__tests__/helpers/uploadQueueMocks';

// Create shared mock worker instance
let mockWorkerInstance: MockUploadWorker;

// Mock Worker globally BEFORE any imports
global.Worker = jest.fn().mockImplementation(() => {
  mockWorkerInstance = new MockUploadWorker({ uploadDelay: 10, autoUpload: true });
  return mockWorkerInstance;
});

// NOW import modules that use Worker
import { renderHook, act, waitFor } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';

describe('Upload Queue Tests', () => {
  let mockWorker: MockUploadWorker;

  beforeEach(() => {
    mockWorker = mockWorkerInstance; // Reference the shared instance
    // ... other setup
  });

  it('should test something', () => {
    // Can now use mockWorker methods
    mockWorker.setUploadError('id', 'error');
  });
});
```

**Pattern 2: Factory Function** (Alternative)

```typescript
// test-utils/workerMockFactory.ts
export function createWorkerMock() {
  let mockWorkerInstance: MockUploadWorker;

  global.Worker = jest.fn().mockImplementation(() => {
    mockWorkerInstance = new MockUploadWorker({ uploadDelay: 10, autoUpload: true });
    return mockWorkerInstance;
  });

  return () => mockWorkerInstance;
}

// In test file
import { createWorkerMock } from '../../test-utils/workerMockFactory';

const getWorkerMock = createWorkerMock();

import { useUploadQueue } from '../useUploadQueue';

describe('Tests', () => {
  const mockWorker = getWorkerMock();
  // ... tests
});
```

## Test Structure Template

### Complete Test File Structure

```typescript
/**
 * Upload Queue Tests - [Category]
 * Tests: X total
 * Coverage: [Component] functionality
 */

import { MockUploadWorker, MockBroadcastChannel } from '../../__tests__/helpers/uploadQueueMocks';

// Shared mock instance
let mockWorkerInstance: MockUploadWorker;

// Mock Worker BEFORE imports
global.Worker = jest.fn().mockImplementation(() => {
  mockWorkerInstance = new MockUploadWorker({ uploadDelay: 10, autoUpload: true });
  return mockWorkerInstance;
});

import { renderHook, act, waitFor } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';

describe('Upload Queue - [Category]', () => {
  let mockWorker: MockUploadWorker;
  let mockFetch: jest.SpyInstance;
  let mockLocalStorage: { [key: string]: string };

  beforeEach(() => {
    jest.useFakeTimers();
    mockWorker = mockWorkerInstance;

    // Mock fetch
    mockFetch = jest.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({ documentId: 'pdf-123' }),
      status: 200
    } as Response);

    // Mock localStorage
    mockLocalStorage = {};
    Storage.prototype.getItem = jest.fn((key) => mockLocalStorage[key] || null);
    Storage.prototype.setItem = jest.fn((key, value) => {
      mockLocalStorage[key] = value;
    });

    // Mock BroadcastChannel
    global.BroadcastChannel = MockBroadcastChannel as any;
  });

  afterEach(() => {
    jest.useRealTimers();
    jest.restoreAllMocks();
    MockBroadcastChannel.clearAll();
  });

  it('should test upload functionality', async () => {
    const { result } = renderHook(() => useUploadQueue());

    const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

    await act(async () => {
      await result.current.addFiles([file], 'game-123', 'en');
      jest.advanceTimersByTime(500);
    });

    await waitFor(() => {
      expect(result.current.queue[0].status).toBe('success');
    });
  });
});
```

## Common Testing Scenarios

### 1. Testing Upload Success

```typescript
it('should complete upload successfully', async () => {
  const { result } = renderHook(() => useUploadQueue());

  const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

  await act(async () => {
    await result.current.addFiles([file], 'game-123', 'en');
    jest.advanceTimersByTime(500); // Simulate upload time
  });

  await waitFor(() => {
    expect(result.current.queue[0].status).toBe('success');
    expect(result.current.queue[0].pdfId).toBeDefined();
  });
});
```

### 2. Testing Upload Errors

```typescript
it('should handle upload error', async () => {
  mockFetch.mockRejectedValueOnce(new Error('Upload failed'));

  const { result } = renderHook(() => useUploadQueue());

  const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

  await act(async () => {
    await result.current.addFiles([file], 'game-123', 'en');
    jest.advanceTimersByTime(500);
  });

  await waitFor(() => {
    expect(result.current.queue[0].status).toBe('failed');
    expect(result.current.queue[0].error).toContain('Upload failed');
  });
});
```

### 3. Testing Worker Crash Recovery

```typescript
it('should recover from worker crash', async () => {
  const { result } = renderHook(() => useUploadQueue());

  await act(async () => {
    jest.advanceTimersByTime(50); // Wait for worker ready
  });

  act(() => {
    mockWorker.simulateCrash();
    jest.advanceTimersByTime(200); // Wait for restart
  });

  await waitFor(() => {
    expect(result.current.workerError).toBeDefined();
  });
});
```

### 4. Testing Concurrent Uploads

```typescript
it('should enforce 3 concurrent upload limit', async () => {
  const { result } = renderHook(() => useUploadQueue());

  const files = Array.from({ length: 5 }, (_, i) =>
    new File([`content ${i}`], `test${i}.pdf`, { type: 'application/pdf' })
  );

  await act(async () => {
    await result.current.addFiles(files, 'game-123', 'en');
    jest.advanceTimersByTime(50);
  });

  await waitFor(() => {
    const activeCount = mockWorker.getActiveUploadsCount();
    expect(activeCount).toBeLessThanOrEqual(3);
  });
});
```

### 5. Testing Persistence

```typescript
it('should persist queue to localStorage', async () => {
  const { result } = renderHook(() => useUploadQueue());

  const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

  await act(async () => {
    await result.current.addFiles([file], 'game-123', 'en');
    jest.advanceTimersByTime(100);
  });

  await waitFor(() => {
    expect(Storage.prototype.setItem).toHaveBeenCalledWith(
      'meepleai-upload-queue',
      expect.any(String)
    );
  });
});
```

## Timing & Async Handling

### Jest Fake Timers

Always use fake timers for consistent, fast tests:

```typescript
beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.useRealTimers();
});

// In tests
await act(async () => {
  jest.advanceTimersByTime(500); // Advance 500ms
});
```

### Waiting for State Updates

Use `waitFor` for async state changes:

```typescript
await waitFor(() => {
  expect(result.current.queue[0].status).toBe('success');
}, { timeout: 5000 });
```

### Act Wrapping

Wrap all state updates in `act()`:

```typescript
await act(async () => {
  await result.current.addFiles(files, 'game-123', 'en');
  jest.advanceTimersByTime(100);
});
```

## Best Practices

### ✅ DO

1. **Mock Worker Before Imports**: Always mock `global.Worker` before importing modules that use it
2. **Use Fake Timers**: Control timing with `jest.useFakeTimers()` for consistent tests
3. **Test Real Scenarios**: Use actual File objects, not mocks, to test file handling
4. **Clean Up**: Always restore mocks in `afterEach()`
5. **Test Worker Lifecycle**: Include tests for crash recovery, restart limits, cleanup
6. **Inspect Mock State**: Use helper methods (`getActiveUploadsCount`, etc.) for assertions

### ❌ DON'T

1. **Import Before Mocking**: Never import Worker-using modules before setting up mocks
2. **Use Real Timers**: Avoid `jest.useRealTimers()` during tests (too slow, inconsistent)
3. **Skip Cleanup**: Always clear mocks between tests to prevent state leakage
4. **Ignore Timing**: Don't forget to advance timers after triggering async operations
5. **Mock Everything**: Use real File/Blob objects when testing file handling
6. **Forget Act**: Always wrap state updates in `act()` to avoid warnings

## Troubleshooting

### Issue: "Worker is not defined"

**Symptom**: Error at module load time about Worker not being defined

**Cause**: Worker mock not set up before importing modules

**Fix**: Move Worker mock before ALL imports

```typescript
// ❌ WRONG
import { useUploadQueue } from '../useUploadQueue'; // Too early!
global.Worker = jest.fn().mockImplementation(/* ... */);

// ✅ CORRECT
global.Worker = jest.fn().mockImplementation(/* ... */);
import { useUploadQueue } from '../useUploadQueue'; // Now safe
```

### Issue: Tests Timing Out

**Symptom**: Tests hang and timeout after 5 seconds

**Cause**: Not advancing fake timers or forgetting to wait for async operations

**Fix**: Ensure timers are advanced and use `waitFor`:

```typescript
await act(async () => {
  await result.current.addFiles(files, 'game-123', 'en');
  jest.advanceTimersByTime(500); // Don't forget this!
});

await waitFor(() => {
  expect(result.current.queue[0].status).toBe('success');
});
```

### Issue: Flaky Tests

**Symptom**: Tests pass/fail inconsistently

**Cause**: Race conditions with async operations or insufficient waits

**Fix**: Use `waitFor` with conditions, not fixed delays:

```typescript
// ❌ WRONG - Fixed delay
await act(async () => {
  jest.advanceTimersByTime(500);
});
expect(result.current.queue[0].status).toBe('success'); // Might fail

// ✅ CORRECT - Wait for condition
await waitFor(() => {
  expect(result.current.queue[0].status).toBe('success');
});
```

### Issue: Mock State Leaking Between Tests

**Symptom**: Second test fails because first test's state persists

**Cause**: Missing cleanup in `afterEach`

**Fix**: Comprehensive cleanup:

```typescript
afterEach(() => {
  jest.useRealTimers();
  jest.restoreAllMocks();
  MockBroadcastChannel.clearAll();
  mockWorker.clearAllErrors(); // Clear mock configuration
});
```

## Performance Considerations

### Fast Test Execution

- **Mock Delays**: Use minimal delays (10ms) in tests, not realistic delays (100ms+)
- **Parallel Execution**: Split tests into multiple files for parallel Jest execution
- **Selective Testing**: Use `.only` during development, remove before commit

### Coverage Optimization

- **Target Coverage**: Aim for 90%+ on worker, store, and hook code
- **Edge Cases**: Test boundary conditions (0 files, 100 files, file size limits)
- **Error Paths**: Test all error scenarios (network, validation, worker crashes)

## File Organization

```
apps/web/src/
├── hooks/
│   ├── __tests__/
│   │   ├── useUploadQueue.core.test.ts       # Queue management, lifecycle, concurrency
│   │   ├── useUploadQueue.errors.test.ts     # Error handling, message protocol
│   │   ├── useUploadQueue.persistence.test.ts # Persistence, multi-tab
│   │   └── useUploadQueue.lifecycle.test.ts  # Worker lifecycle, buffering, callbacks
│   └── useUploadQueue.ts
├── stores/
│   └── UploadQueueStore.ts
├── workers/
│   └── uploadQueue.worker.ts
└── __tests__/
    └── helpers/
        └── uploadQueueMocks.ts               # MockUploadWorker, MockBroadcastChannel
```

## References

- **Implementation**: FE-IMP-008 (Upload Queue Web Worker) - Issue #1084
- **Testing**: FE-TEST-010 (Integration Tests) - Issue #1238
- **Mock Helpers**: `apps/web/src/__tests__/helpers/uploadQueueMocks.ts`
- **Jest Docs**: https://jestjs.io/docs/mock-functions
- **Testing Library**: https://testing-library.com/docs/react-testing-library/intro/

---

**Maintained By**: Frontend Team
**Status**: Living Document - Update as patterns evolve
