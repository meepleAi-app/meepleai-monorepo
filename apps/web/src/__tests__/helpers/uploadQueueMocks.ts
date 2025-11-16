/**
 * Test helpers and mocks for Upload Queue tests
 * FE-IMP-008: Web Worker implementation
 */

import type { UploadQueueItem, UploadStatus } from '../../stores/UploadQueueStore';

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
      lastModified: Date.now()
    },
    gameId: 'game-123',
    language: 'en',
    status: 'pending' as UploadStatus,
    progress: 0,
    retryCount: 0,
    createdAt: Date.now(),
    ...overrides
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
        lastModified: Date.now()
      },
      ...overrides
    })
  );
}

/**
 * Mock worker for testing
 * Provides a simple mock that doesn't actually create a Web Worker
 */
export class MockUploadWorker {
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: ErrorEvent) => void) | null = null;
  onmessageerror: ((event: MessageEvent) => void) | null = null;

  private messageHandlers: Array<(event: MessageEvent) => void> = [];

  postMessage(message: any, transfer?: Transferable[]): void {
    // Simulate async worker response
    setTimeout(() => {
      if (this.onmessage) {
        this.onmessage(new MessageEvent('message', { data: message }));
      }
    }, 0);
  }

  terminate(): void {
    // No-op for mock
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
}

/**
 * Mock BroadcastChannel for testing
 */
export class MockBroadcastChannel {
  name: string;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onmessageerror: ((event: MessageEvent) => void) | null = null;

  constructor(name: string) {
    this.name = name;
  }

  postMessage(message: any): void {
    // No-op for mock
  }

  close(): void {
    // No-op for mock
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    // No-op for mock
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    // No-op for mock
  }

  dispatchEvent(event: Event): boolean {
    return true;
  }
}
