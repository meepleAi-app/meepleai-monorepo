/**
 * Test helpers and mocks for Upload Queue tests
 * FE-IMP-008: Web Worker implementation
 *
 * CRITICAL: Only import TYPES from UploadQueueStore to prevent singleton initialization
 */

import type { UploadQueueItem, UploadStatus } from '../../workers/uploadQueue.worker';

/**
 * Creates a mock UploadQueueItem for testing
 * Matches the worker's file metadata structure
 */
export function createMockUploadQueueItem(
  overrides: Partial<UploadQueueItem> = {}
): UploadQueueItem {
  const defaultItem: UploadQueueItem = {
    id: overrides.id || 'test-id', // Use consistent ID for tests
    file: {
      name: 'test.pdf',
      size: 1024000, // 1 MB
      type: 'application/pdf',
      lastModified: Date.now(),
    },
    gameId: 'game-123',
    language: 'en',
    status: 'pending' as UploadStatus,
    progress: 0,
    retryCount: 0,
    createdAt: Date.now(),
    ...overrides,
  };

  return defaultItem;
}

/**
 * Creates multiple mock items at once
 */
export function createMockUploadQueueItems(
  count: number,
  overrides: Partial<UploadQueueItem> = {}
): UploadQueueItem[] {
  return Array.from({ length: count }, (_, i) =>
    createMockUploadQueueItem({
      id: `item-${i}`,
      file: {
        name: `test-${i}.pdf`,
        size: 1024000,
        type: 'application/pdf',
        lastModified: Date.now(),
      },
      ...overrides,
    })
  );
}

import type {
  WorkerRequest,
  WorkerResponse,
  UploadQueueState,
  FileData,
} from '../../workers/uploadQueue.worker';

/**
 * Enhanced Mock Worker for testing with full protocol simulation
 * Simulates the Upload Queue Web Worker behavior including:
 * - WORKER_READY signal on initialization
 * - State management and updates
 * - Upload simulation with configurable behavior
 * - Error simulation and retry logic
 * - Persistence requests
 */
export class MockUploadWorker {
  private _onmessage: ((event: MessageEvent) => void) | null = null;
  private _pendingMessages: MessageEvent[] = []; // Queue messages until onmessage is set

  onerror: ((event: ErrorEvent) => void) | null = null;
  onmessageerror: ((event: MessageEvent) => void) | null = null;

  // Property getter/setter to intercept onmessage assignment and flush pending messages
  get onmessage(): ((event: MessageEvent) => void) | null {
    return this._onmessage;
  }

  set onmessage(handler: ((event: MessageEvent) => void) | null) {
    console.log(
      '[MockWorker] onmessage setter called, pending messages:',
      this._pendingMessages.length
    );
    this._onmessage = handler;

    // Flush any pending messages that were queued before handler was set
    if (handler && this._pendingMessages.length > 0) {
      console.log('[MockWorker] Flushing', this._pendingMessages.length, 'pending messages');
      const pending = [...this._pendingMessages];
      this._pendingMessages = [];
      pending.forEach(event => {
        console.log('[MockWorker] Delivering queued message:', event.data.type);
        handler(event);
      });
    }
  }

  private messageHandlers: Array<(event: MessageEvent) => void> = [];
  private state: UploadQueueState = {
    items: [],
    metrics: {
      totalUploads: 0,
      successfulUploads: 0,
      failedUploads: 0,
      cancelledUploads: 0,
      totalBytesUploaded: 0,
    },
  };
  private activeUploads = new Map<string, boolean>();
  private fileDataCache = new Map<string, ArrayBuffer>();
  private clearedItemIds = new Set<string>();

  // Configuration for mock behavior
  private config: {
    autoUpload: boolean;
    uploadDelay: number;
    simulateErrors: Record<string, string>; // Map of item ID to error message
    apiBase: string;
  };

  constructor(config?: Partial<MockUploadWorker['config']>) {
    this.config = {
      autoUpload: true,
      uploadDelay: 10,
      simulateErrors: {},
      apiBase: 'http://localhost:8080',
      ...config,
    };

    // Send WORKER_READY immediately after construction
    setTimeout(() => {
      this.emit({ type: 'WORKER_READY' });
    }, 0);
  }

  postMessage(message: WorkerRequest, transfer?: Transferable[]): void {
    // Handle worker requests asynchronously
    setTimeout(() => {
      this.handleMessage(message);
    }, 0);
  }

  terminate(): void {
    // Clear all active uploads and state
    this.activeUploads.clear();
    this.fileDataCache.clear();
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type === 'message' && typeof listener === 'function') {
      this.messageHandlers.push(listener as (event: MessageEvent) => void);
    }
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type === 'message' && typeof listener === 'function') {
      const index = this.messageHandlers.indexOf(listener as (event: MessageEvent) => void);
      if (index > -1) {
        this.messageHandlers.splice(index, 1);
      }
    }
  }

  dispatchEvent(event: Event): boolean {
    return true;
  }

  // ============================================================================
  // Mock Worker Implementation
  // ============================================================================

  private handleMessage(message: WorkerRequest): void {
    switch (message.type) {
      case 'ADD_FILES':
        this.handleAddFiles(
          message.payload.files,
          message.payload.gameId,
          message.payload.language
        );
        break;
      case 'CANCEL_UPLOAD':
        this.handleCancelUpload(message.payload.id);
        break;
      case 'RETRY_UPLOAD':
        this.handleRetryUpload(message.payload.id);
        break;
      case 'REMOVE_ITEM':
        this.handleRemoveItem(message.payload.id);
        break;
      case 'CLEAR_COMPLETED':
        this.handleClearCompleted();
        break;
      case 'CLEAR_ALL':
        this.handleClearAll();
        break;
      case 'START_PROCESSING':
        this.handleStartProcessing();
        break;
      case 'GET_STATE':
        this.emitStateUpdate();
        break;
      case 'RESTORE_STATE':
        this.handleRestoreState(message.payload.items, message.payload.metrics);
        break;
    }
  }

  private handleAddFiles(files: FileData[], gameId: string, language: string): void {
    files.forEach(fileData => {
      const id = `upload-${Date.now()}-${Math.random().toString(36).substring(7)}`;

      const item: UploadQueueItem = {
        id,
        file: {
          name: fileData.name,
          size: fileData.size,
          type: fileData.type,
          lastModified: fileData.lastModified,
        },
        gameId,
        language,
        status: 'pending',
        progress: 0,
        retryCount: 0,
        createdAt: Date.now(),
      };

      this.state.items.push(item);
      this.fileDataCache.set(id, fileData.arrayBuffer);
    });

    this.state.metrics.totalUploads += files.length;
    this.emitStateUpdate();
    this.emitPersistRequest();

    if (this.config.autoUpload) {
      this.handleStartProcessing();
    }
  }

  private handleCancelUpload(id: string): void {
    const item = this.state.items.find(i => i.id === id);
    if (!item) return;

    if (item.status === 'uploading' || item.status === 'processing') {
      this.activeUploads.delete(id);
      item.status = 'cancelled';
      item.progress = 0;
      this.state.metrics.cancelledUploads++;
      this.emitStateUpdate();
      this.emitPersistRequest();
    }
  }

  private handleRetryUpload(id: string): void {
    const item = this.state.items.find(i => i.id === id);
    if (!item || item.status !== 'failed') return;

    item.status = 'pending';
    item.progress = 0;
    item.error = undefined;
    item.correlationId = undefined;
    item.retryCount++;

    this.emitStateUpdate();

    if (this.config.autoUpload) {
      this.simulateUpload(item);
    }
  }

  private handleRemoveItem(id: string): void {
    const index = this.state.items.findIndex(i => i.id === id);
    if (index > -1) {
      this.state.items.splice(index, 1);
      this.fileDataCache.delete(id);
      this.emitStateUpdate();
      this.emitPersistRequest();
    }
  }

  private handleClearCompleted(): void {
    const completedIds = this.state.items
      .filter(
        item => item.status === 'success' || item.status === 'failed' || item.status === 'cancelled'
      )
      .map(item => item.id);

    this.clearedItemIds.clear();
    completedIds.forEach(id => this.clearedItemIds.add(id));

    this.state.items = this.state.items.filter(item => !completedIds.includes(item.id));
    completedIds.forEach(id => this.fileDataCache.delete(id));

    this.emitStateUpdate();
    this.emit({
      type: 'CLEAR_COMPLETED_DONE',
      payload: { clearedIds: completedIds },
    });
    this.emitPersistRequest();
  }

  private handleClearAll(): void {
    this.state.items = [];
    this.fileDataCache.clear();
    this.activeUploads.clear();
    this.emitStateUpdate();
    this.emitPersistRequest();
  }

  private handleStartProcessing(): void {
    const pendingItems = this.state.items.filter(item => item.status === 'pending');
    pendingItems.forEach(item => this.simulateUpload(item));
  }

  private handleRestoreState(items: UploadQueueItem[], metrics: UploadQueueState['metrics']): void {
    this.state.items = items;
    this.state.metrics = metrics;
    this.emitStateUpdate();
  }

  private async simulateUpload(item: UploadQueueItem): Promise<void> {
    if (this.activeUploads.has(item.id)) return;

    this.activeUploads.set(item.id, true);
    item.status = 'uploading';
    item.progress = 0;
    this.emitStateUpdate();

    // Simulate upload delay
    await new Promise(resolve => setTimeout(resolve, this.config.uploadDelay));

    // Check for simulated errors
    if (this.config.simulateErrors[item.id]) {
      const error = this.config.simulateErrors[item.id];
      item.status = 'failed';
      item.error = error;
      item.correlationId = `mock-corr-id-${Date.now()}`;
      this.state.metrics.failedUploads++;
      this.activeUploads.delete(item.id);
      // Keep file data for potential retry (don't delete on failure)

      this.emit({
        type: 'UPLOAD_FAILED',
        payload: {
          id: item.id,
          error,
          correlationId: item.correlationId,
        },
      });

      this.emitStateUpdate();
      this.emitPersistRequest();
      return;
    }

    // Simulate progress updates
    for (let progress = 20; progress <= 100; progress += 20) {
      item.progress = progress;
      this.emit({
        type: 'UPLOAD_PROGRESS',
        payload: { id: item.id, progress },
      });

      if (progress < 100) {
        await new Promise(resolve => setTimeout(resolve, this.config.uploadDelay / 5));
      }
    }

    // Simulate API call using global fetch (which should be mocked in tests)
    try {
      const fileData = this.fileDataCache.get(item.id);
      if (!fileData) {
        throw new Error(`File data not found for upload ${item.id}`);
      }

      const formData = new FormData();
      const blob = new Blob([fileData], { type: item.file.type });
      formData.append('file', blob, item.file.name);
      formData.append('gameId', item.gameId);
      formData.append('language', item.language);

      const response = await fetch(`${this.config.apiBase}/api/v1/ingest/pdf`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ error: 'Upload failed' }));
        throw new Error(errorData.error || `Upload failed with status ${response.status}`);
      }

      const data = await response.json();
      const pdfId = data.documentId || `pdf-${Date.now()}`;

      item.status = 'success';
      item.progress = 100;
      item.pdfId = pdfId;
      this.state.metrics.successfulUploads++;
      this.state.metrics.totalBytesUploaded += item.file.size;
      this.activeUploads.delete(item.id);
      this.fileDataCache.delete(item.id); // Clean up memory

      this.emit({
        type: 'UPLOAD_SUCCESS',
        payload: { id: item.id, pdfId },
      });

      this.emitStateUpdate();
      this.emitPersistRequest();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Upload failed';
      item.status = 'failed';
      item.error = errorMessage;
      item.correlationId = `mock-corr-id-${Date.now()}`;
      this.state.metrics.failedUploads++;
      this.activeUploads.delete(item.id);
      // Keep file data for potential retry (don't delete on failure)

      this.emit({
        type: 'UPLOAD_FAILED',
        payload: {
          id: item.id,
          error: errorMessage,
          correlationId: item.correlationId,
        },
      });

      this.emitStateUpdate();
      this.emitPersistRequest();
    }
  }

  private emit(response: WorkerResponse): void {
    const event = new MessageEvent('message', { data: response });

    console.log(
      '[MockWorker] Emitting:',
      response.type,
      'handler attached:',
      !!this._onmessage,
      'queued:',
      this._pendingMessages.length
    );

    if (this._onmessage) {
      this._onmessage(event);
    } else {
      // Queue message if handler not attached yet (race condition prevention)
      this._pendingMessages.push(event);
      console.log('[MockWorker] Queued message (total pending:', this._pendingMessages.length, ')');
    }

    this.messageHandlers.forEach(handler => handler(event));
  }

  private emitStateUpdate(): void {
    this.emit({
      type: 'STATE_UPDATED',
      payload: this.state,
    });
  }

  private emitPersistRequest(): void {
    this.emit({
      type: 'PERSIST_REQUEST',
      payload: {
        items: this.state.items,
        metrics: this.state.metrics,
      },
    });
  }

  // ============================================================================
  // Test Helpers
  // ============================================================================

  /**
   * Configure error simulation for specific uploads
   */
  public setUploadError(itemId: string, error: string): void {
    this.config.simulateErrors[itemId] = error;
  }

  /**
   * Get current state for assertions
   */
  public getState(): UploadQueueState {
    return this.state;
  }

  /**
   * Set upload delay for tests
   */
  public setUploadDelay(delay: number): void {
    this.config.uploadDelay = delay;
  }

  /**
   * Enable/disable auto upload
   */
  public setAutoUpload(enabled: boolean): void {
    this.config.autoUpload = enabled;
  }

  /**
   * Simulate worker crash (for lifecycle testing)
   */
  public simulateCrash(): void {
    const errorEvent = new ErrorEvent('error', {
      message: 'Simulated worker crash',
      error: new Error('Simulated worker crash'),
    });

    if (this.onerror) {
      this.onerror(errorEvent);
    }
  }

  /**
   * Get active uploads count
   */
  public getActiveUploadsCount(): number {
    return this.activeUploads.size;
  }

  /**
   * Get file data cache size (for memory leak detection)
   */
  public getFileDataCacheSize(): number {
    return this.fileDataCache.size;
  }

  /**
   * Clear all error simulations
   */
  public clearAllErrors(): void {
    this.config.simulateErrors = {};
  }

  /**
   * Wait for all active uploads to complete
   */
  public async waitForAllUploads(): Promise<void> {
    while (this.activeUploads.size > 0) {
      await new Promise(resolve => setTimeout(resolve, 10));
    }
  }
}

/**
 * Setup helper to mock the Web Worker in UploadQueueStore
 * Returns the mock worker instance for test control
 *
 * Usage in tests:
 * ```typescript
 * const mockWorker = setupWorkerMock();
 * // mockWorker is now used by UploadQueueStore instead of real Worker
 * // You can control its behavior:
 * mockWorker.setUploadDelay(0); // Fast tests
 * mockWorker.setUploadError('item-id', 'Test error');
 * ```
 */
export function setupWorkerMock(config?: Partial<MockUploadWorker['config']>): MockUploadWorker {
  const mockWorker = new MockUploadWorker(config);

  // Mock the Worker constructor globally
  // @ts-expect-error - Mocking global Worker for tests
  global.Worker = vi.fn(() => mockWorker);

  return mockWorker;
}

/**
 * Mock BroadcastChannel for testing
 * Simulates multi-tab communication
 */
export class MockBroadcastChannel {
  name: string;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onmessageerror: ((event: MessageEvent) => void) | null = null;

  private static channels = new Map<string, MockBroadcastChannel[]>();
  private messageHandlers: Array<(event: MessageEvent) => void> = [];

  constructor(name: string) {
    this.name = name;

    // Register this channel instance
    if (!MockBroadcastChannel.channels.has(name)) {
      MockBroadcastChannel.channels.set(name, []);
    }
    MockBroadcastChannel.channels.get(name)!.push(this);
  }

  postMessage(message: any): void {
    // Broadcast to all other channels with the same name
    const channels = MockBroadcastChannel.channels.get(this.name) || [];
    channels.forEach(channel => {
      if (channel !== this) {
        const event = new MessageEvent('message', { data: message });
        if (channel.onmessage) {
          channel.onmessage(event);
        }
        channel.messageHandlers.forEach(handler => handler(event));
      }
    });
  }

  close(): void {
    // Unregister this channel
    const channels = MockBroadcastChannel.channels.get(this.name);
    if (channels) {
      const index = channels.indexOf(this);
      if (index > -1) {
        channels.splice(index, 1);
      }
    }
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type === 'message' && typeof listener === 'function') {
      this.messageHandlers.push(listener as (event: MessageEvent) => void);
    }
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type === 'message' && typeof listener === 'function') {
      const index = this.messageHandlers.indexOf(listener as (event: MessageEvent) => void);
      if (index > -1) {
        this.messageHandlers.splice(index, 1);
      }
    }
  }

  dispatchEvent(event: Event): boolean {
    return true;
  }

  /**
   * Test helper: Clear all channels (use between tests)
   */
  static clearAll(): void {
    MockBroadcastChannel.channels.clear();
  }
}
