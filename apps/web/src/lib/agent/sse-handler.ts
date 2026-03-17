/**
 * SSE Handler - Server-Sent Events Wrapper
 * Issue #3237 (FRONT-001)
 *
 * Manages SSE connections for agent chat streaming with:
 * - Auto-reconnection with exponential backoff
 * - Event type routing
 * - Error recovery
 */

import { logger } from '@/lib/logger';

import type { SSEEvent } from './types';

export interface SSEHandlerOptions {
  url: string;
  onMessage?: (event: SSEEvent) => void;
  onError?: (error: Error) => void;
  onOpen?: () => void;
  onClose?: () => void;
  maxReconnectAttempts?: number;
}

export class SSEHandler {
  private eventSource: EventSource | null = null;
  private reconnectAttempts = 0;
  private reconnectTimeout: NodeJS.Timeout | null = null;
  private options: Required<SSEHandlerOptions>;

  constructor(options: SSEHandlerOptions) {
    this.options = {
      onMessage: () => {},
      onError: () => {},
      onOpen: () => {},
      onClose: () => {},
      maxReconnectAttempts: 5,
      ...options,
    };
  }

  /**
   * Connect to SSE endpoint
   */
  connect(): void {
    try {
      this.eventSource = new EventSource(this.options.url, {
        withCredentials: true,
      });

      this.eventSource.onopen = () => {
        this.reconnectAttempts = 0;
        this.options.onOpen();
      };

      this.eventSource.onmessage = event => {
        try {
          const parsed: SSEEvent = JSON.parse(event.data);
          this.options.onMessage(parsed);
        } catch (error) {
          logger.error('[SSEHandler] Failed to parse event:', error);
        }
      };

      this.eventSource.onerror = () => {
        const error = new Error('SSE connection failed');
        this.options.onError(error);

        // Auto-reconnect with exponential backoff
        if (this.reconnectAttempts < this.options.maxReconnectAttempts) {
          const delay = Math.min(Math.pow(2, this.reconnectAttempts) * 1000, 30000);
          this.reconnectAttempts += 1;

          logger.warn(
            `[SSEHandler] Reconnecting in ${delay / 1000}s (attempt ${this.reconnectAttempts}/${this.options.maxReconnectAttempts})`
          );

          this.reconnectTimeout = setTimeout(() => {
            this.disconnect();
            this.connect();
          }, delay);
        } else {
          logger.error('[SSEHandler] Max reconnection attempts reached');
          this.disconnect();
        }
      };
    } catch (error) {
      const err = error instanceof Error ? error : new Error('Failed to create EventSource');
      this.options.onError(err);
    }
  }

  /**
   * Disconnect from SSE endpoint
   */
  disconnect(): void {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = null;
    }

    if (this.eventSource) {
      this.eventSource.close();
      this.eventSource = null;
      this.options.onClose();
    }

    this.reconnectAttempts = 0;
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.eventSource?.readyState === EventSource.OPEN;
  }
}
