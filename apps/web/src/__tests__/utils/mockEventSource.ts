/**
 * MockEventSource - Test utility for SSE/Streaming tests
 *
 * Provides a minimal EventSource mock that simulates real SSE behavior
 * without heavy mocking. Tests can control the event stream to verify
 * component behavior.
 *
 * Issue #2760: MSW Infrastructure Setup
 * Issue #2765: Sprint 5 - Streaming Hooks Testing
 *
 * @example
 * ```typescript
 * import { MockEventSource, setupMockEventSource } from '@/__tests__/utils/mockEventSource';
 *
 * setupMockEventSource();
 *
 * it('handles SSE chunks', async () => {
 *   const { result } = renderHook(() => useChunkStreaming('/api/stream'));
 *
 *   await waitFor(() => expect(result.current.isConnected).toBe(true));
 *
 *   const eventSource = MockEventSource.getLatest()!;
 *   act(() => eventSource.simulateSSEChunk({ chunk: 'Hello' }));
 *
 *   expect(result.current.content).toBe('Hello');
 * });
 * ```
 */

export class MockEventSource {
  /** Track all created instances for test assertions */
  static instances: MockEventSource[] = [];

  /** Event handlers */
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  onopen: (() => void) | null = null;

  /** Connection state: 0=CONNECTING, 1=OPEN, 2=CLOSED */
  readyState = 0;

  /** The URL this EventSource was created with */
  url: string;

  /** Whether credentials were requested */
  withCredentials: boolean;

  /** Event listener maps for addEventListener/removeEventListener */
  private _listeners: Map<string, Set<EventListener>> = new Map();

  /** Static constants matching real EventSource */
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
    MockEventSource.instances.push(this);

    // Simulate async connection (like real EventSource)
    queueMicrotask(() => {
      if (this.readyState !== MockEventSource.CLOSED) {
        this.readyState = MockEventSource.OPEN;
        this.onopen?.();
        this._emit('open', new Event('open'));
      }
    });
  }

  /**
   * Close the EventSource connection
   */
  close() {
    this.readyState = MockEventSource.CLOSED;
  }

  /**
   * Add event listener
   */
  addEventListener(type: string, listener: EventListener) {
    if (!this._listeners.has(type)) {
      this._listeners.set(type, new Set());
    }
    this._listeners.get(type)!.add(listener);

    // Also set the on* handler for compatibility
    if (type === 'message' && !this.onmessage) {
      this.onmessage = listener as (event: MessageEvent) => void;
    } else if (type === 'error' && !this.onerror) {
      this.onerror = listener as (event: Event) => void;
    } else if (type === 'open' && !this.onopen) {
      this.onopen = listener as () => void;
    }
  }

  /**
   * Remove event listener
   */
  removeEventListener(type: string, listener: EventListener) {
    this._listeners.get(type)?.delete(listener);
  }

  /**
   * Internal: emit event to all listeners
   */
  private _emit(type: string, event: Event) {
    this._listeners.get(type)?.forEach((listener) => listener(event));
  }

  // ========== Test Helper Methods ==========

  /**
   * Simulate a raw message event
   * @param data - The data string to send
   * @param eventType - Optional event type (defaults to 'message')
   */
  simulateMessage(data: string, eventType = 'message') {
    if (this.readyState !== MockEventSource.OPEN) return;

    const messageEvent = new MessageEvent(eventType, { data });

    if (eventType === 'message') {
      this.onmessage?.(messageEvent);
    }
    this._emit(eventType, messageEvent);
  }

  /**
   * Simulate an SSE chunk with JSON data (common pattern)
   * @param chunk - Object to JSON.stringify and send
   */
  simulateSSEChunk(chunk: object) {
    this.simulateMessage(`data: ${JSON.stringify(chunk)}\n\n`);
  }

  /**
   * Simulate SSE stream completion with [DONE] marker
   */
  simulateDone() {
    this.simulateMessage('data: [DONE]\n\n');
  }

  /**
   * Simulate a connection error
   * @param error - Optional error message
   */
  simulateError(error?: string) {
    const errorEvent = new Event('error');
    if (error) {
      (errorEvent as Event & { message?: string }).message = error;
    }
    this.onerror?.(errorEvent);
    this._emit('error', errorEvent);
  }

  /**
   * Simulate connection close from server
   */
  simulateClose() {
    this.close();
  }

  // ========== Static Helper Methods ==========

  /**
   * Reset all instances (call in afterEach)
   */
  static reset() {
    MockEventSource.instances.forEach((es) => es.close());
    MockEventSource.instances = [];
  }

  /**
   * Get the most recently created instance
   */
  static getLatest(): MockEventSource | undefined {
    return MockEventSource.instances[MockEventSource.instances.length - 1];
  }

  /**
   * Get instance by URL pattern
   */
  static getByUrl(urlPattern: string | RegExp): MockEventSource | undefined {
    return MockEventSource.instances.find((es) =>
      typeof urlPattern === 'string' ? es.url.includes(urlPattern) : urlPattern.test(es.url)
    );
  }

  /**
   * Get all instances matching URL pattern
   */
  static getAllByUrl(urlPattern: string | RegExp): MockEventSource[] {
    return MockEventSource.instances.filter((es) =>
      typeof urlPattern === 'string' ? es.url.includes(urlPattern) : urlPattern.test(es.url)
    );
  }
}

/**
 * Setup helper for tests - installs MockEventSource globally
 *
 * @example
 * ```typescript
 * import { setupMockEventSource } from '@/__tests__/utils/mockEventSource';
 *
 * describe('StreamingComponent', () => {
 *   setupMockEventSource();
 *
 *   it('connects to stream', () => {
 *     // Tests can now use MockEventSource.getLatest()
 *   });
 * });
 * ```
 */
export const setupMockEventSource = () => {
  const original = global.EventSource;

  beforeAll(() => {
    global.EventSource = MockEventSource as unknown as typeof EventSource;
  });

  afterEach(() => {
    MockEventSource.reset();
  });

  afterAll(() => {
    global.EventSource = original;
  });
};

/**
 * Helper to create SSE data chunks for realistic testing
 */
export const createSSEChunks = (messages: string[], chunkSize = 10): string[] => {
  const chunks: string[] = [];
  let current = '';

  messages.forEach((msg) => {
    const data = `data: ${JSON.stringify({ chunk: msg })}\n\n`;

    for (let i = 0; i < data.length; i += chunkSize) {
      current += data.slice(i, i + chunkSize);
      if (current.includes('\n\n')) {
        chunks.push(current);
        current = '';
      }
    }
  });

  if (current) {
    chunks.push(current);
  }

  return chunks;
};
