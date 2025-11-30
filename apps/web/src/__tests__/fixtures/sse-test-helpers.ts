/**
 * Server-Sent Events (SSE) Mocking Helpers (Issue #1502)
 *
 * Centralized utilities for mocking SSE responses in tests.
 * Eliminates duplication of SSE mocking logic across test files.
 *
 * @example Basic SSE Response
 * ```typescript
 * import { createSSEResponse } from '../fixtures/sse-test-helpers';
 *
 * const events = [
 *   JSON.stringify({ type: 'token', data: { token: 'Hello' } }),
 *   JSON.stringify({ type: 'complete', data: { totalTokens: 1 } }),
 * ];
 *
 * (global.fetch as Mock).mockResolvedValue(createSSEResponse(events));
 * ```
 *
 * @example Using Event Builders
 * ```typescript
 * import { createSSEResponse, createTokenEvent, createCompleteEvent } from '../fixtures/sse-test-helpers';
 *
 * const events = [
 *   createTokenEvent('Hello'),
 *   createTokenEvent(' World'),
 *   createCompleteEvent(2, 0.95),
 * ];
 *
 * (global.fetch as Mock).mockResolvedValue(createSSEResponse(events));
 * ```
 *
 * @example Error Response
 * ```typescript
 * import { createSSEErrorResponse } from '../fixtures/sse-test-helpers';
 *
 * (global.fetch as Mock).mockResolvedValue(
 *   createSSEErrorResponse('INTERNAL_ERROR', 'Something went wrong')
 * );
 * ```
 */

// =============================================================================
// CORE SSE RESPONSE CREATION
// =============================================================================

/**
 * Options for configuring SSE response behavior
 */
export interface SSEOptions {
  /**
   * Delay in milliseconds between events (default: 10ms)
   * - 0ms: Instant delivery (fast tests, may not catch race conditions)
   * - 10ms: Default (realistic timing, catches most race conditions)
   * - 50-100ms: Slow delivery (stress testing, timeout scenarios)
   *
   * @default 10
   */
  eventDelay?: number;

  /**
   * AbortSignal for cancellation testing
   * When signal is aborted, stream will stop sending events
   */
  signal?: AbortSignal;
}

/**
 * Creates a mock SSE Response with proper headers and streaming format
 *
 * **Issue #1495: Improved with async event delivery to prevent race conditions**
 *
 * Key improvements:
 * - ✅ Sequential event delivery with configurable delays
 * - ✅ Proper SSE message ordering (no race conditions)
 * - ✅ Cancellation support via AbortSignal
 * - ✅ Timeout testing support (configurable delays)
 * - ✅ Robust token accumulation (state updates settle between events)
 *
 * @param events - Array of SSE event strings (already JSON.stringify'd)
 * @param options - Optional configuration for delays and cancellation
 * @returns Response object with ReadableStream and SSE headers
 *
 * @example Basic usage (with default 10ms delay)
 * ```typescript
 * const events = [
 *   JSON.stringify({ type: 'token', data: { token: 'Hello' } }),
 *   JSON.stringify({ type: 'complete', data: { totalTokens: 1 } }),
 * ];
 * const response = createSSEResponse(events);
 * ```
 *
 * @example Fast mode (0ms delay for quick tests)
 * ```typescript
 * const response = createSSEResponse(events, { eventDelay: 0 });
 * ```
 *
 * @example Cancellation testing
 * ```typescript
 * const controller = new AbortController();
 * const response = createSSEResponse(events, { signal: controller.signal });
 * // Later: controller.abort() will stop the stream
 * ```
 *
 * @example Timeout scenario testing
 * ```typescript
 * const response = createSSEResponse(events, { eventDelay: 100 });
 * // Slower delivery to test timeout handling
 * ```
 */
export function createSSEResponse(events: string[], options: SSEOptions = {}): Response {
  const { eventDelay = 10, signal } = options;

  // Validate eventDelay range
  if (eventDelay < 0 || eventDelay > 1000) {
    throw new Error(`eventDelay must be between 0-1000ms, got ${eventDelay}`);
  }

  // Warn if delay is too high (slow tests)
  if (eventDelay > 100) {
    console.warn(`[SSE Helper] High eventDelay (${eventDelay}ms) may slow down tests significantly`);
  }

  // Async generator for sequential event delivery
  async function* generateEvents() {
    const encoder = new TextEncoder();

    for (let i = 0; i < events.length; i++) {
      // Check if stream was cancelled
      if (signal?.aborted) {
        console.log(`[SSE Helper] Stream cancelled after ${i} events`);
        break;
      }

      // Add delay between events (except first event)
      // This simulates real network/processing delays and prevents race conditions
      if (i > 0 && eventDelay > 0) {
        await new Promise(resolve => setTimeout(resolve, eventDelay));
      }

      // Yield SSE-formatted event
      const sseData = `data: ${events[i]}\n\n`;
      yield encoder.encode(sseData);
    }
  }

  // Create ReadableStream from async generator
  const stream = ReadableStream.from(generateEvents());

  return new Response(stream, {
    status: 200,
    headers: {
      'Content-Type': 'text/event-stream',
    },
  });
}

/**
 * Creates a mock SSE Response that returns an error event
 *
 * @param code - Error code (e.g., 'INTERNAL_ERROR', 'VALIDATION_ERROR')
 * @param message - Error message
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns Response with SSE error event
 *
 * @example
 * const response = createSSEErrorResponse('INTERNAL_ERROR', 'Something went wrong');
 */
export function createSSEErrorResponse(
  code: string,
  message: string,
  timestamp?: string
): Response {
  const errorEvent = JSON.stringify({
    type: 'error',
    data: { message, code },
    timestamp: timestamp || new Date().toISOString(),
  });

  return createSSEResponse([errorEvent]);
}

// =============================================================================
// EVENT BUILDER HELPERS
// =============================================================================

/**
 * Creates a token event string for SSE streaming
 *
 * @param token - Token text to stream
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns JSON string for SSE token event
 *
 * @example
 * const event = createTokenEvent('Hello');
 * // Returns: '{"type":"token","data":{"token":"Hello"},"timestamp":"2025-01-15T10:00:00Z"}'
 */
export function createTokenEvent(token: string, timestamp?: string): string {
  return JSON.stringify({
    type: 'token',
    data: { token },
    timestamp: timestamp || new Date().toISOString(),
  });
}

/**
 * Creates a state update event string for SSE streaming
 *
 * @param state - State message (e.g., 'Generating embeddings...', 'Searching database...')
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns JSON string for SSE state update event
 *
 * @example
 * const event = createStateUpdateEvent('Generating embeddings...');
 */
export function createStateUpdateEvent(state: string, timestamp?: string): string {
  return JSON.stringify({
    type: 'stateUpdate',
    data: { state },
    timestamp: timestamp || new Date().toISOString(),
  });
}

/**
 * Creates a citations event string for SSE streaming
 *
 * @param citations - Array of citation objects with source, page, text, score
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns JSON string for SSE citations event
 *
 * @example
 * const event = createCitationsEvent([
 *   { source: 'rules.pdf', page: 1, text: 'Game rules', score: 0.95 },
 * ]);
 */
export function createCitationsEvent(
  citations: Array<{ source: string; page: number; text: string; score: number }>,
  timestamp?: string
): string {
  return JSON.stringify({
    type: 'citations',
    data: { citations },
    timestamp: timestamp || new Date().toISOString(),
  });
}

/**
 * Creates a follow-up questions event string for SSE streaming
 *
 * @param questions - Array of follow-up question strings
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns JSON string for SSE follow-up questions event
 *
 * @example
 * const event = createFollowUpQuestionsEvent(['Question 1?', 'Question 2?']);
 */
export function createFollowUpQuestionsEvent(questions: string[], timestamp?: string): string {
  return JSON.stringify({
    type: 'followUpQuestions',
    data: { questions },
    timestamp: timestamp || new Date().toISOString(),
  });
}

/**
 * Creates a complete event string for SSE streaming
 *
 * @param totalTokens - Total number of tokens in the response
 * @param confidence - Confidence score (0-1) or null
 * @param snippets - Optional array of citation snippets
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns JSON string for SSE complete event
 *
 * @example
 * const event = createCompleteEvent(42, 0.95);
 * const eventWithSnippets = createCompleteEvent(42, 0.95, [
 *   { source: 'manual.pdf', page: 5, text: 'Setup guide', score: 0.88 },
 * ]);
 */
export function createCompleteEvent(
  totalTokens: number,
  confidence: number | null,
  snippets?: Array<{ source: string; page: number; text: string; score: number }>,
  timestamp?: string
): string {
  return JSON.stringify({
    type: 'complete',
    data: {
      totalTokens,
      confidence,
      ...(snippets ? { snippets } : {}),
    },
    timestamp: timestamp || new Date().toISOString(),
  });
}

/**
 * Creates an error event string for SSE streaming
 *
 * @param code - Error code (e.g., 'INTERNAL_ERROR', 'VALIDATION_ERROR')
 * @param message - Error message
 * @param timestamp - Optional timestamp (defaults to current time)
 * @returns JSON string for SSE error event
 *
 * @example
 * const event = createErrorEvent('INTERNAL_ERROR', 'Something went wrong');
 */
export function createErrorEvent(code: string, message: string, timestamp?: string): string {
  return JSON.stringify({
    type: 'error',
    data: { message, code },
    timestamp: timestamp || new Date().toISOString(),
  });
}

// =============================================================================
// COMPOSITE HELPERS (COMMON SCENARIOS)
// =============================================================================

/**
 * Creates a complete SSE response with token streaming
 *
 * @param tokens - Array of token strings to stream
 * @param completionOptions - Optional completion data (totalTokens, confidence, citations)
 * @param sseOptions - Optional SSE behavior configuration (delays, cancellation)
 * @returns Response with SSE token and complete events
 *
 * @example
 * const response = createTokenStreamResponse(['Hello', ' ', 'World'], {
 *   totalTokens: 3,
 *   confidence: 0.95,
 * });
 *
 * @example With custom delay
 * const response = createTokenStreamResponse(['A', 'B'], {}, { eventDelay: 0 });
 */
export function createTokenStreamResponse(
  tokens: string[],
  completionOptions?: {
    totalTokens?: number;
    confidence?: number | null;
    citations?: Array<{ source: string; page: number; text: string; score: number }>;
  },
  sseOptions?: SSEOptions
): Response {
  const events = [
    ...tokens.map(token => createTokenEvent(token)),
    createCompleteEvent(
      completionOptions?.totalTokens ?? tokens.length,
      completionOptions?.confidence ?? null,
      completionOptions?.citations
    ),
  ];

  return createSSEResponse(events, sseOptions);
}

/**
 * Creates a complete SSE response with state updates
 *
 * @param states - Array of state message strings
 * @param sseOptions - Optional SSE behavior configuration (delays, cancellation)
 * @returns Response with SSE state update and complete events
 *
 * @example
 * const response = createStateUpdateResponse([
 *   'Generating embeddings...',
 *   'Searching database...',
 *   'Generating response...',
 * ]);
 *
 * @example With custom delay
 * const response = createStateUpdateResponse(['State 1', 'State 2'], { eventDelay: 0 });
 */
export function createStateUpdateResponse(states: string[], sseOptions?: SSEOptions): Response {
  const events = [
    ...states.map(state => createStateUpdateEvent(state)),
    createCompleteEvent(0, null),
  ];

  return createSSEResponse(events, sseOptions);
}

/**
 * Creates a complete SSE response with citations
 *
 * @param citations - Array of citation objects
 * @param answer - Optional answer tokens to stream
 * @param confidence - Optional confidence score
 * @returns Response with SSE citations, tokens, and complete events
 *
 * @example
 * const response = createCitationsResponse([
 *   { source: 'rules.pdf', page: 1, text: 'Game rules', score: 0.95 },
 * ], ['The', ' answer'], 0.9);
 */
export function createCitationsResponse(
  citations: Array<{ source: string; page: number; text: string; score: number }>,
  answer?: string[],
  confidence?: number
): Response {
  const events = [
    createCitationsEvent(citations),
    ...(answer || []).map(token => createTokenEvent(token)),
    createCompleteEvent(answer?.length ?? 0, confidence ?? null, citations),
  ];

  return createSSEResponse(events);
}

// =============================================================================
// TYPE EXPORTS
// =============================================================================

/**
 * Type for SSE event builders
 */
export type SSEEventBuilder = (timestamp?: string) => string;

/**
 * Type for citation objects used in SSE responses
 */
export type Citation = {
  source: string;
  page: number;
  text: string;
  score: number;
};
