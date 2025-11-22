/**
 * Test utilities for React component testing
 * Provides reusable helpers for rendering, mocking, and testing React components
 */

import { render, RenderOptions } from '@testing-library/react';
import { ReactElement } from 'react';
import React from 'react';
import {
  TimelineEvent,
  TimelineEventType,
  TimelineEventStatus,
  Snippet,
  TimelineMetrics
} from '@/lib/timeline-types';

/**
 * Renders component with common providers
 *
 * @example
 * const { getByText } = renderWithProviders(<MyComponent />);
 *
 * @param ui - React element to render
 * @param options - RTL render options (excluding wrapper)
 * @returns RTL render result
 */
export function renderWithProviders(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>
) {
  // For now, just wrap standard render
  // Can add providers later (Router, Context, etc.)
  return render(ui, options);
}

/**
 * Creates mock API response for fetch mocking
 *
 * @example
 * global.fetch = jest.fn(() => mockApiResponse(200, { data: 'test' }));
 *
 * @param status - HTTP status code
 * @param payload - Response payload (JSON or text)
 * @returns Promise resolving to Response object
 */
export function mockApiResponse(status: number, payload?: unknown): Promise<Response> {
  return Promise.resolve({
    ok: status >= 200 && status < 300,
    status,
    json: async () => payload,
    text: async () => typeof payload === 'string' ? payload : JSON.stringify(payload),
    headers: new Headers(),
  } as Response);
}

/**
 * Waits for async operations to complete
 * Useful for testing async state updates without act() warnings
 *
 * @example
 * await waitForAsync(() => expect(screen.getByText('Loaded')).toBeInTheDocument());
 *
 * @param assertion - Assertion function to retry
 * @param timeout - Maximum wait time in milliseconds (default: 1000)
 * @throws AssertionError if assertion fails after timeout
 */
export async function waitForAsync(assertion: () => void, timeout = 1000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    try {
      assertion();
      return;
    } catch (e) {
      await new Promise(resolve => setTimeout(resolve, 50));
    }
  }
  assertion(); // Final attempt that will throw if still failing
}

/**
 * Creates mock timeline events for testing
 * Generates realistic test data with configurable count and overrides
 *
 * @example
 * const events = createMockEvents(10);
 * const customEvents = createMockEvents(5, [{ type: 'error', status: 'error' }]);
 *
 * @param count - Number of events to generate
 * @param overrides - Array of partial event objects to override defaults
 * @returns Array of TimelineEvent objects
 */
export function createMockEvents(
  count: number,
  overrides?: Array<Partial<TimelineEvent> | undefined>
): TimelineEvent[] {
  const types: TimelineEventType[] = [
    'message',
    'rag_search',
    'rag_retrieval',
    'rag_generation',
    'rag_complete',
    'error'
  ];
  const statuses: TimelineEventStatus[] = ['pending', 'in_progress', 'success', 'error'];

  return Array.from({ length: count }, (_, i) => {
    const baseEvent: TimelineEvent = {
      id: `event-${i}`,
      type: types[i % types.length],
      status: statuses[i % statuses.length],
      timestamp: new Date(Date.now() - i * 60000), // Each event 1 minute apart
      data: {
        message: `Event ${i} message`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        citations: i % 3 === 0 ? createMockSnippets(2) : undefined,
        metrics: i % 4 === 0 ? createMockMetrics() : undefined,
        error: types[i % types.length] === 'error' ? `Error message ${i}` : undefined,
        endpoint: '/api/v1/agents/qa',
        gameId: `game-${Math.floor(i / 5)}`,
        chatId: `chat-${Math.floor(i / 3)}`
      }
    };

    return {
      ...baseEvent,
      ...(overrides?.[i] || {})
    };
  });
}

/**
 * Creates mock snippet objects for citations
 *
 * @param count - Number of snippets to generate
 * @returns Array of Snippet objects
 */
function createMockSnippets(count: number): Snippet[] {
  return Array.from({ length: count }, (_, i) => ({
    text: `Snippet ${i} text content from the rulebook`,
    source: `rulebook-${i}.pdf`,
    page: Math.floor(Math.random() * 100) + 1,
    line: Math.floor(Math.random() * 50) + 1
  }));
}

/**
 * Creates mock metrics object
 *
 * @returns TimelineMetrics object with realistic values
 */
function createMockMetrics(): TimelineMetrics {
  const promptTokens = Math.floor(Math.random() * 1000) + 100;
  const completionTokens = Math.floor(Math.random() * 500) + 50;

  return {
    latencyMs: Math.floor(Math.random() * 2000) + 100,
    promptTokens,
    completionTokens,
    totalTokens: promptTokens + completionTokens,
    confidence: Math.random() * 0.3 + 0.7 // 0.7 to 1.0
  };
}

/**
 * Creates a specific event for testing edge cases
 *
 * @example
 * const errorEvent = createMockEvent({ type: 'error', status: 'error' });
 *
 * @param overrides - Partial event object to override defaults
 * @returns TimelineEvent object
 */
export function createMockEvent(overrides?: Partial<TimelineEvent>): TimelineEvent {
  return createMockEvents(1, overrides ? [overrides] : undefined)[0];
}
