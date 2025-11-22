# Upload Queue Worker Testing Guide

This guide explains how to test components that use the Upload Queue Web Worker.

## Overview

The Upload Queue uses a Web Worker to handle file uploads off the main thread. Testing components that use this worker requires proper mocking to avoid creating actual Worker instances in tests.

## MockUploadWorker

The `MockUploadWorker` class (in `helpers/uploadQueueMocks.ts`) provides a full simulation of the real worker's behavior.

### Features

- **WORKER_READY signaling**: Emits ready signal on initialization
- **State management**: Maintains upload queue state and metrics
- **Upload simulation**: Simulates file uploads with configurable delays and errors
- **Message protocol**: Implements full `WorkerRequest`/`WorkerResponse` protocol
- **Persistence**: Emits `PERSIST_REQUEST` messages
- **Progress tracking**: Emits `UPLOAD_PROGRESS` messages

### Basic Setup

```typescript
import { setupWorkerMock, MockBroadcastChannel } from '../helpers/uploadQueueMocks';

describe('My Component', () => {
  beforeEach(() => {
    // Setup worker mock
    setupWorkerMock({ uploadDelay: 0, autoUpload: true });

    // Mock BroadcastChannel
    global.BroadcastChannel = MockBroadcastChannel;

    // Mock localStorage
    const localStorageMock = {
      getItem: jest.fn(),
      setItem: jest.fn(),
      removeItem: jest.fn(),
      clear: jest.fn(),
      key: jest.fn(),
      length: 0
    };
    Object.defineProperty(window, 'localStorage', { value: localStorageMock });
  });
});
```

### Configuration Options

```typescript
const mockWorker = setupWorkerMock({
  autoUpload: true,        // Auto-start uploads when files are added
  uploadDelay: 0,          // Delay in ms for upload simulation (0 for instant)
  simulateErrors: {},      // Map of item ID to error message
  apiBase: 'http://localhost:5080'  // API base URL
});
```

### Simulating Errors

```typescript
it('should handle upload errors', async () => {
  const mockWorker = setupWorkerMock({ autoUpload: false });

  // Configure specific upload to fail
  mockWorker.setUploadError('item-id', 'Simulated error');

  // Or mock fetch to return error
  global.fetch = jest.fn().mockResolvedValue({
    ok: false,
    status: 500,
    json: async () => ({ error: 'Upload failed' })
  });
});
```

### Testing Upload Flow

```typescript
it('should upload file successfully', async () => {
  // Mock successful upload response
  global.fetch = jest.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ documentId: 'pdf-123', fileName: 'test.pdf' })
  });

  const mockWorker = setupWorkerMock({ uploadDelay: 10 });

  // Render component and trigger upload
  const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

  // Component uses useUploadQueue hook which uses the mocked worker
  // ... render and interact with component ...

  // Wait for upload to complete
  await waitFor(() => {
    expect(screen.getByText(/success/i)).toBeInTheDocument();
  });
});
```

### Testing Worker Behaviors

#### Worker Initialization

```typescript
it('should wait for WORKER_READY before processing', async () => {
  const mockWorker = setupWorkerMock();

  // Wait for ready signal
  await new Promise(resolve => setTimeout(resolve, 50));

  expect(uploadQueueStore.isWorkerReady()).toBe(true);
});
```

#### File Buffering

```typescript
it('should buffer files when worker is not ready', async () => {
  const mockWorker = setupWorkerMock();

  // Add files immediately (before WORKER_READY)
  const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
  await uploadQueueStore.addFiles([file], 'game-1', 'en');

  // Wait for worker to be ready and process buffered files
  await new Promise(resolve => setTimeout(resolve, 100));

  const snapshot = uploadQueueStore.getSnapshot();
  expect(snapshot.items.length).toBeGreaterThan(0);
});
```

#### State Persistence

```typescript
it('should persist state to localStorage', async () => {
  const setItemSpy = jest.spyOn(localStorage, 'setItem');

  const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
  await uploadQueueStore.addFiles([file], 'game-1', 'en');

  await new Promise(resolve => setTimeout(resolve, 100));

  expect(setItemSpy).toHaveBeenCalledWith(
    'meepleai-upload-queue',
    expect.any(String)
  );
});
```

#### Crash Recovery

```typescript
it('should recover from worker crash', async () => {
  const mockWorker = setupWorkerMock();

  await new Promise(resolve => setTimeout(resolve, 50));

  // Simulate crash
  if (mockWorker.onerror) {
    mockWorker.onerror(new ErrorEvent('error', { message: 'Worker crashed' }));
  }

  // Store should attempt restart
  await new Promise(resolve => setTimeout(resolve, 200));

  // After successful restart, should be ready again
  expect(uploadQueueStore.isWorkerReady()).toBe(true);
});
```

### Testing Progress Updates

```typescript
it('should track upload progress', async () => {
  const progressValues: number[] = [];

  const unsubscribe = uploadQueueStore.subscribe(() => {
    const snapshot = uploadQueueStore.getSnapshot();
    const uploadingItem = snapshot.items.find(item => item.status === 'uploading');
    if (uploadingItem) {
      progressValues.push(uploadingItem.progress);
    }
  });

  const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
  await uploadQueueStore.addFiles([file], 'game-1', 'en');

  uploadQueueStore.startProcessing();

  await new Promise(resolve => setTimeout(resolve, 200));

  unsubscribe();

  // Should have progress values: 20, 40, 60, 80, 100
  expect(progressValues.length).toBeGreaterThan(0);
});
```

### Testing Retry Logic

```typescript
it('should retry failed uploads', async () => {
  // First attempt fails, second succeeds
  global.fetch = jest.fn()
    .mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => ({ error: 'Temporary error' })
    })
    .mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'pdf-123' })
    });

  const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
  await uploadQueueStore.addFiles([file], 'game-1', 'en');

  uploadQueueStore.startProcessing();

  // Wait for failure
  await new Promise(resolve => setTimeout(resolve, 100));

  const snapshot1 = uploadQueueStore.getSnapshot();
  const failedItem = snapshot1.items.find(item => item.status === 'failed');

  expect(failedItem).toBeDefined();
  expect(failedItem?.error).toBeDefined();

  // Retry
  uploadQueueStore.retryUpload(failedItem!.id);

  // Wait for success
  await new Promise(resolve => setTimeout(resolve, 100));

  const snapshot2 = uploadQueueStore.getSnapshot();
  const retriedItem = snapshot2.items.find(item => item.id === failedItem!.id);

  expect(retriedItem?.status).toBe('success');
  expect(retriedItem?.retryCount).toBe(1);
});
```

## Integration with Page Tests

When testing pages that use the upload queue, the worker mock is set up globally in `beforeEach`:

```typescript
describe('UploadPage', () => {
  beforeEach(() => {
    setupWorkerMock({ uploadDelay: 0, autoUpload: true });
    global.BroadcastChannel = MockBroadcastChannel;

    // Mock localStorage
    const localStorageMock = { /* ... */ };
    Object.defineProperty(window, 'localStorage', { value: localStorageMock });
  });

  it('should upload PDF successfully', async () => {
    // Mock API responses
    global.fetch = setupUploadMocks({
      auth: createAuthMock(),
      games: [createGameMock()],
      uploadResponse: { documentId: 'pdf-123' }
    });

    render(<UploadPage />);

    // Interact with upload form
    const fileInput = screen.getByLabelText(/PDF File/i);
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    fireEvent.change(fileInput, { target: { files: [file] } });

    const uploadButton = screen.getByRole('button', { name: /Upload/i });
    fireEvent.click(uploadButton);

    // Wait for success
    await waitFor(() => {
      expect(screen.getByText(/Success/i)).toBeInTheDocument();
    });
  });
});
```

## Common Issues

### Worker not ready

If tests fail with "worker not ready", add a delay:

```typescript
await new Promise(resolve => setTimeout(resolve, 50));
```

### State not persisting

Ensure localStorage mock is set up:

```typescript
Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
  writable: true
});
```

### Progress not updating

Use `subscribe()` to track state changes:

```typescript
const unsubscribe = uploadQueueStore.subscribe(() => {
  // Handle state changes
});

// Don't forget to unsubscribe
unsubscribe();
```

## Test Files

- `helpers/uploadQueueMocks.ts` - Mock implementation
- `stores/UploadQueueStore.worker.test.ts` - Worker behavior tests
- `pages/upload.pdf-upload.test.tsx` - Page integration tests
- `components/MultiFileUpload.test.tsx` - Component tests

## References

- [FE-IMP-008 Architecture Doc](../../docs/02-development/frontend/upload-queue-web-worker.md)
- [Worker Implementation](../../workers/uploadQueue.worker.ts)
- [Store Implementation](../../stores/UploadQueueStore.ts)
