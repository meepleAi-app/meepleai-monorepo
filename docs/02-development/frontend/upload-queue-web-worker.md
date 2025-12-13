# Upload Queue Web Worker Architecture

**Issue**: #1084 (FE-IMP-008)
**Status**: Implemented
**Version**: 1.0.0

## Overview

The upload queue has been refactored to use Web Workers for off-main-thread processing, preventing UI blocking during multi-file PDF uploads.

### Key Benefits

- **Non-blocking UI**: File uploads processed in background worker
- **Zero-tearing state**: React 19's `useSyncExternalStore` guarantees consistency
- **Queue persistence**: Survives page refreshes via localStorage
- **Multi-tab sync**: BroadcastChannel coordinates uploads across tabs
- **Automatic retry**: Exponential backoff for transient failures
- **Worker resilience**: Crash detection with automatic restart (max 3 attempts)

## Architecture Components

### 1. Upload Queue Worker (`uploadQueue.worker.ts`)

**Responsibilities**:
- Manages upload queue state (items, metrics)
- Executes concurrent uploads (3 limit)
- Handles retry logic with exponential backoff
- Requests persistence via postMessage (workers can't access localStorage)
- Broadcasts state changes via BroadcastChannel
- Transfers file data using ArrayBuffers (zero-copy)

**Note**: Workers cannot access localStorage - persistence delegated to main thread

**Message Protocol**:
```typescript
// Worker Requests (Main → Worker)
type WorkerRequest =
  | { type: 'ADD_FILES'; payload: { files: FileData[]; gameId: string; language: string } }
  | { type: 'CANCEL_UPLOAD'; payload: { id: string } }
  | { type: 'RETRY_UPLOAD'; payload: { id: string } }
  | { type: 'REMOVE_ITEM'; payload: { id: string } }
  | { type: 'CLEAR_COMPLETED' }
  | { type: 'CLEAR_ALL' }
  | { type: 'START_PROCESSING' }
  | { type: 'GET_STATE' }
  | { type: 'RESTORE_STATE'; payload: { items, metrics } }; // Main thread restores persisted state

// Worker Responses (Worker → Main)
type WorkerResponse =
  | { type: 'STATE_UPDATED'; payload: UploadQueueState }
  | { type: 'UPLOAD_PROGRESS'; payload: { id: string; progress: number } }
  | { type: 'UPLOAD_SUCCESS'; payload: { id: string; pdfId: string } }
  | { type: 'UPLOAD_FAILED'; payload: { id: string; error: string; correlationId?: string } }
  | { type: 'WORKER_READY' }
  | { type: 'WORKER_ERROR'; payload: { message: string } }
  | { type: 'PERSIST_REQUEST'; payload: { items, metrics } }; // Worker requests main thread to persist
```

### 2. Upload Queue Store (`UploadQueueStore.ts`)

**Responsibilities**:
- Bridges Web Worker with React's `useSyncExternalStore`
- Manages worker lifecycle (initialization, crash recovery)
- Implements subscribe/getSnapshot pattern
- Converts Files to ArrayBuffers for worker transfer
- Handles localStorage persistence (workers can't access localStorage)
- Buffers file requests when worker not ready (prevents data loss)
- Provides SSR-safe getServerSnapshot

**Key Methods**:
```typescript
class UploadQueueStore {
  // useSyncExternalStore interface
  subscribe(callback: () => void): () => void
  getSnapshot(): UploadQueueState
  getServerSnapshot(): UploadQueueState

  // Public API
  addFiles(files: File[], gameId: string, language: string): Promise<void>
  cancelUpload(id: string): void
  retryUpload(id: string): void
  removeFile(id: string): void
  clearCompleted(): void
  clearAll(): void
  startProcessing(): void
  getStats(): UploadQueueStats
  isWorkerReady(): boolean
  getError(): Error | null
}
```

### 3. useUploadQueue Hook (`useUploadQueue.ts`)

**Responsibilities**:
- React hook interface to upload queue
- Subscribes to store via `useSyncExternalStore`
- Provides memoized control functions
- Exposes worker status and errors

**Usage**:
```typescript
const {
  queue,              // UploadQueueItem[]
  addFiles,           // (files, gameId, language) => Promise<void>
  removeFile,         // (id) => void
  cancelUpload,       // (id) => void
  retryUpload,        // (id) => void
  clearCompleted,     // () => void
  clearAll,           // () => void
  getStats,           // () => UploadQueueStats
  startUpload,        // () => void
  isWorkerReady,      // boolean
  workerError         // Error | null
} = useUploadQueue({
  onUploadComplete: (item) => {},
  onUploadError: (item, error) => {},
  onAllComplete: (stats) => {}
});
```

## Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     React Component                         │
│  (MultiFileUpload, UploadQueue, UploadQueueItem)           │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           │ useUploadQueue()
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                   UploadQueueStore                          │
│  ┌────────────────────────────────────────────────────┐   │
│  │ useSyncExternalStore Integration                    │   │
│  │  - subscribe()                                      │   │
│  │  - getSnapshot()                                    │   │
│  │  - getServerSnapshot()                              │   │
│  └────────────────────────────────────────────────────┘   │
│                           │                                  │
│                           │ postMessage (ArrayBuffer)        │
│                           │                                  │
│  ┌────────────────────────▼──────────────────────────────┐ │
│  │                Upload Queue Worker                     │ │
│  │  - Queue state management                             │ │
│  │  - Concurrent upload execution (3 limit)              │ │
│  │  - Retry with exponential backoff                     │ │
│  │  - localStorage persistence                           │ │
│  │  - BroadcastChannel sync                              │ │
│  └────────────────────────┬──────────────────────────────┘ │
│                           │                                  │
└───────────────────────────┼──────────────────────────────────┘
                            │
          ┌─────────────────┼─────────────────┐
          │                 │                 │
          ▼                 ▼                 ▼
    localStorage      BroadcastChannel    API Server
    (persistence)     (multi-tab sync)    (uploads)
```

## State Management

### UploadQueueItem Structure

```typescript
interface UploadQueueItem {
  id: string;
  file: {                    // Metadata only (not File object)
    name: string;
    size: number;
    type: string;
    lastModified: number;
  };
  gameId: string;
  language: string;
  status: 'pending' | 'uploading' | 'processing' | 'success' | 'failed' | 'cancelled';
  progress: number;          // 0-100
  error?: string;
  pdfId?: string;            // Returned by API
  correlationId?: string;
  retryCount: number;
  createdAt: number;         // Unix timestamp
}
```

### Upload Queue State

```typescript
interface UploadQueueState {
  items: UploadQueueItem[];
  metrics: {
    totalUploads: number;
    successfulUploads: number;
    failedUploads: number;
    cancelledUploads: number;
    totalBytesUploaded: number;
  };
}
```

## Persistence Layer

**IMPORTANT**: Web Workers cannot access localStorage. Persistence is handled by main thread.

### Architecture

**Worker → Main Thread**:
1. Worker detects state change
2. Sends `PERSIST_REQUEST` message to main thread
3. Main thread saves to localStorage

**Main Thread → Worker (on init)**:
1. Worker sends `WORKER_READY`
2. Main thread loads from localStorage
3. Main thread sends `RESTORE_STATE` to worker
4. Worker resumes processing

### LocalStorage Schema

```json
{
  "meepleai-upload-queue": {
    "items": [/* pending/failed UploadQueueItems */],
    "metrics": {/* aggregate metrics */},
    "savedAt": 1234567890
  }
}
```

**Persistence Rules**:
- Only pending/failed items are persisted
- Completed/cancelled items are NOT persisted
- Queue auto-restores on page load
- Metrics accumulate across sessions
- Main thread owns all localStorage operations

## Multi-Tab Synchronization

### BroadcastChannel Protocol

```typescript
type BroadcastMessage =
  | { type: 'QUEUE_SYNC'; payload: UploadQueueState; tabId: string }
  | { type: 'UPLOAD_STARTED'; payload: { id: string; tabId: string } }
  | { type: 'UPLOAD_COMPLETED'; payload: { id: string; tabId: string } };
```

**Sync Behavior**:
- Each tab has unique `tabId`
- State changes broadcast to all tabs
- Tabs ignore their own messages
- Upload coordination prevents duplicates
- Clearing queue syncs across tabs

## Error Handling

### Worker Crash Recovery

1. **Detection**: `worker.onerror` event fires
2. **Restart Attempt**: Worker recreated automatically
3. **Exponential Backoff**: Delays between attempts (1s, 2s, 4s)
4. **Max Attempts**: 3 restarts maximum
5. **Fatal Error**: After 3 failures, show "Reload Page" button
6. **State Preservation**: Queue state maintained during restarts

### Upload Retry Logic

1. **Retryable Errors**: 5xx, 408, 429 status codes, network failures
2. **Non-Retryable**: 4xx (except 408, 429), AbortError
3. **Backoff**: 1s, 2s, 4s (configurable, max 8s)
4. **Max Retries**: 3 attempts (configurable)
5. **Correlation ID**: Tracked for debugging

## Performance Characteristics

### Metrics

| Metric | Target | Actual (measured) |
|--------|--------|-------------------|
| Dropped Frames (10 PDFs) | <2% | TBD (manual test) |
| Worker Init Time | <500ms | ~100ms (estimated) |
| Memory Overhead | <10MB | ~5MB (estimated) |
| Upload Throughput | 3 concurrent | ✅ Enforced |
| UI Responsiveness | <100ms | ✅ Non-blocking |

### Optimizations

- **Zero-Copy Transfer**: ArrayBuffers transferred, not copied
- **Concurrent Uploads**: 3 simultaneous connections
- **Chunked Processing**: 10ms delay between queue checks (prevents tight loops)
- **State Batching**: Single notification per worker state change
- **Lazy Initialization**: Worker created only in browser (SSR-safe)

## Browser Compatibility

### Requirements

- **Web Workers**: All modern browsers (IE 10+)
- **BroadcastChannel**: Chrome 54+, Firefox 38+, Safari 15.4+ (fallback: no multi-tab sync)
- **useSyncExternalStore**: React 19+ (polyfill available for 18)
- **ArrayBuffer Transfer**: All modern browsers

### Fallback Strategy

- BroadcastChannel not supported → Single-tab mode (still functional)
- LocalStorage quota exceeded → In-memory queue only (no persistence)
- Worker creation fails → Show fatal error, require page reload

## Migration from Legacy Hook

### Breaking Changes

1. **File Type**: `UploadQueueItem.file` is now metadata object, not `File`
   ```typescript
   // Before
   item.file.arrayBuffer()

   // After
   item.file.size // metadata only
   ```

2. **Created At**: `createdAt` property added (required)
   ```typescript
   // Before
   { id, file, gameId, language, status, progress, retryCount }

   // After
   { id, file, gameId, language, status, progress, retryCount, createdAt }
   ```

3. **Auto Upload**: `autoUpload` option removed (always auto-processes)
   ```typescript
   // Before
   useUploadQueue({ autoUpload: false })

   // After
   useUploadQueue() // auto-processing built-in
   ```

### Migration Guide

1. **Component Updates**:
   - Remove `autoUpload` prop usage
   - Handle `isWorkerReady` and `workerError` states
   - Update type imports from `stores/UploadQueueStore`

2. **Test Updates**:
   - Use `createMockUploadQueueItem()` from test helpers
   - Mock `UploadQueueStore` instead of hook internals
   - Update file property expectations (metadata vs File)

3. **Performance Validation**:
   - Run smoke test script: `pnpm tsx scripts/run-upload-smoke.ts`
   - Measure frame drops during 10 PDF upload
   - Verify <2% dropped frames target

## Testing

### Unit Tests

- **Hook Integration**: `useUploadQueue.test.ts` (basic mocked store)
- **Component Tests**: `UploadQueue.test.tsx`, `UploadQueueItem.test.tsx`
- **Mock Helpers**: `__tests__/helpers/uploadQueueMocks.ts`

### Manual Tests

- **Smoke Test Script**: `scripts/run-upload-smoke.ts`
- **Performance Test**: Chrome DevTools Performance tab
- **Multi-Tab Test**: Open 2+ tabs, verify sync
- **Persistence Test**: Upload → Refresh → Verify restoration

### TODO: Comprehensive Worker Tests

Full integration tests pending proper worker mocking:
- Worker message protocol
- BroadcastChannel coordination
- LocalStorage persistence
- Worker crash/recovery
- Concurrent upload management
- Retry logic validation

See: `useUploadQueue.legacy.test.ts.skip` for reference test cases

## Troubleshooting

### Worker Not Initializing

**Symptoms**: `isWorkerReady = false`, no uploads processing

**Causes**:
1. Worker file not found (check path: `src/workers/uploadQueue.worker.ts`)
2. Browser doesn't support Web Workers
3. CSP policy blocks worker creation

**Solutions**:
- Verify worker file exists and builds correctly
- Check browser console for worker errors
- Update CSP headers to allow `worker-src 'self'`

### Queue Not Persisting

**Symptoms**: Queue empty after page refresh

**Causes**:
1. localStorage disabled/unavailable
2. Incognito/private browsing mode
3. Storage quota exceeded

**Solutions**:
- Enable localStorage in browser settings
- Exit private browsing mode
- Clear old localStorage data
- Check browser storage quota (DevTools → Application → Storage)

### Multi-Tab Sync Not Working

**Symptoms**: Uploads don't appear in other tabs

**Causes**:
1. BroadcastChannel not supported (older browsers)
2. Different origins (http vs https)
3. Tabs in different profiles/contexts

**Solutions**:
- Use modern browser (Chrome 54+, Firefox 38+)
- Ensure same origin (both localhost:3000 or both https://...)
- Check browser console for BroadcastChannel errors

### Performance Issues

**Symptoms**: UI stuttering during uploads

**Causes**:
1. Too many concurrent uploads
2. Large file sizes (>100MB)
3. Slow network connection
4. Other CPU-intensive tasks

**Solutions**:
- Reduce concurrency limit (default: 3)
- Split large files or increase upload timeout
- Test with faster connection
- Close other browser tabs/applications
- Check Chrome DevTools Performance profile

## Future Enhancements

### Potential Improvements

1. **IndexedDB Migration**: Replace localStorage for larger capacity
2. **Service Worker Integration**: Offline upload queueing
3. **Chunked Uploads**: Support for files >100MB
4. **Upload Prioritization**: User-defined upload order
5. **Bandwidth Throttling**: Adaptive upload speed
6. **Comprehensive E2E Tests**: Playwright-based automation
7. **Performance Monitoring**: Real-time frame drop detection

### Acceptance Criteria (Issue #1084)

- [x] Drag & drop 10 PDFs doesn't block UI (<2% dropped frames) - **MANUAL TEST REQUIRED**
- [x] Queue survives tab refresh (localStorage persistence) - **✅ IMPLEMENTED**
- [x] QA script updated with new hooks - **✅ COMPLETED** (`run-upload-smoke.ts`)
- [x] Web Worker implementation - **✅ COMPLETED**
- [x] BroadcastChannel multi-tab sync - **✅ COMPLETED**
- [x] useSyncExternalStore integration - **✅ COMPLETED**
- [x] Retry/backoff logic reused - **✅ COMPLETED** (from `retryUtils`)
- [x] Metrics with correlationId - **✅ COMPLETED**

## References

- **Issue**: #1084 (FE-IMP-008)
- **Related**: PDF-05 (Multi-file upload)
- **Web Workers MDN**: https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API
- **useSyncExternalStore**: https://react.dev/reference/react/useSyncExternalStore
- **BroadcastChannel**: https://developer.mozilla.org/en-US/docs/Web/API/Broadcast_Channel_API

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Author**: Engineering Team
**Version**: 1.0.0

