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
 * Creates a mock SSE Response with proper headers and streaming format
 *
 * @param events - Array of SSE event strings (already JSON.stringify'd)
 * @returns Response object with ReadableStream and SSE headers
 *
 * @example
 * const events = [
 *   JSON.stringify({ type: 'token', data: { token: 'Hello' }, timestamp: '2025-01-15T10:00:00Z' }),
 *   JSON.stringify({ type: 'complete', data: { totalTokens: 1 }, timestamp: '2025-01-15T10:00:01Z' }),
 * ];
 * const response = createSSEResponse(events);
 */
export function createSSEResponse(events: string[]): Response {
  const sseData = events.map(e => `data: ${e}\n\n`).join('');
  const encoder = new TextEncoder();
  const data = encoder.encode(sseData);

  const stream = new ReadableStream({
    start(controller) {
      controller.enqueue(data);
      controller.close();
    },
  });

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
 * @param options - Optional completion data (totalTokens, confidence, citations)
 * @returns Response with SSE token and complete events
 *
 * @example
 * const response = createTokenStreamResponse(['Hello', ' ', 'World'], {
 *   totalTokens: 3,
 *   confidence: 0.95,
 * });
 */
export function createTokenStreamResponse(
  tokens: string[],
  options?: {
    totalTokens?: number;
    confidence?: number | null;
    citations?: Array<{ source: string; page: number; text: string; score: number }>;
  }
): Response {
  const events = [
    ...tokens.map(token => createTokenEvent(token)),
    createCompleteEvent(
      options?.totalTokens ?? tokens.length,
      options?.confidence ?? null,
      options?.citations
    ),
  ];

  return createSSEResponse(events);
}

/**
 * Creates a complete SSE response with state updates
 *
 * @param states - Array of state message strings
 * @returns Response with SSE state update and complete events
 *
 * @example
 * const response = createStateUpdateResponse([
 *   'Generating embeddings...',
 *   'Searching database...',
 *   'Generating response...',
 * ]);
 */
export function createStateUpdateResponse(states: string[]): Response {
  const events = [
    ...states.map(state => createStateUpdateEvent(state)),
    createCompleteEvent(0, null),
  ];

  return createSSEResponse(events);
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
