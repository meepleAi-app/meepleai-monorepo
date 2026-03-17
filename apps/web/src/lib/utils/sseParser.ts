/**
 * SSE Parser Utility (Issue #1007)
 *
 * Robust parser for Server-Sent Events (SSE) format.
 * Handles incomplete chunks, buffering, and event extraction.
 *
 * SSE Format Specification:
 * ```
 * data: {"type":"token","data":{"token":"Hello"},"timestamp":"2025-01-15T10:00:00Z"}
 *
 * data: {"type":"complete","data":{"totalTokens":10},"timestamp":"2025-01-15T10:00:01Z"}
 *
 * ```
 *
 * Features:
 * - Buffer management for incomplete chunks
 * - Multi-event parsing in single chunk
 * - Type-safe event validation with Zod
 * - Error handling for malformed data
 *
 * @see https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events
 */

import {
  RagStreamingEventSchema,
  type RagStreamingEvent,
} from '@/lib/api/schemas/streaming.schemas';
import { logger } from '@/lib/logger';

/**
 * SSE Parser for handling streaming event data
 *
 * Usage:
 * ```typescript
 * const parser = new SSEParser();
 * const events = parser.parse(chunk);
 * events.forEach(handleEvent);
 * ```
 */
export class SSEParser {
  private buffer = '';

  /**
   * Parse SSE chunk into events
   *
   * Handles:
   * - Incomplete chunks (buffer management)
   * - Multiple events in single chunk
   * - SSE format extraction ("data: {json}\n\n")
   * - JSON parsing and Zod validation
   *
   * @param chunk - Raw string chunk from ReadableStream
   * @returns Array of parsed and validated events
   */
  parse(chunk: string): RagStreamingEvent[] {
    // Append new chunk to buffer
    this.buffer += chunk;

    const events: RagStreamingEvent[] = [];

    // Split on double newline (SSE event separator)
    // Last part might be incomplete, keep in buffer
    const parts = this.buffer.split('\n\n');
    this.buffer = parts.pop() || '';

    for (const part of parts) {
      if (!part.trim()) continue;

      try {
        const event = this.parseEvent(part);
        if (event) {
          events.push(event);
        }
      } catch (error) {
        logger.error(
          `[SSEParser] Failed to parse event: ${part}`,
          error instanceof Error ? error : undefined
        );
        // Continue parsing other events even if one fails
      }
    }

    return events;
  }

  /**
   * Parse a single SSE event from string
   *
   * Extracts "data: " line and parses JSON payload
   * Validates with Zod schema
   *
   * @param eventString - Single SSE event string
   * @returns Parsed and validated event or null if invalid
   */
  private parseEvent(eventString: string): RagStreamingEvent | null {
    // SSE format: "data: {json}"
    // May have multiple lines, we only care about "data:" lines
    const lines = eventString.split('\n');

    let dataLine: string | null = null;

    for (const line of lines) {
      const trimmed = line.trim();
      if (trimmed.startsWith('data:')) {
        dataLine = trimmed.substring(5).trim(); // Remove "data: " prefix
        break;
      }
    }

    if (!dataLine) {
      // No data line found, might be a comment or empty event
      return null;
    }

    try {
      // Parse JSON payload
      const json = JSON.parse(dataLine);

      // Validate with Zod schema
      const event = RagStreamingEventSchema.parse(json);

      return event;
    } catch (error) {
      logger.error(
        `[SSEParser] JSON parse error: ${dataLine}`,
        error instanceof Error ? error : undefined
      );
      throw error;
    }
  }

  /**
   * Reset parser state
   * Useful for starting a new stream
   */
  reset(): void {
    this.buffer = '';
  }

  /**
   * Get current buffer content
   * Useful for debugging incomplete chunks
   */
  getBuffer(): string {
    return this.buffer;
  }
}

/**
 * Create a new SSE parser instance
 * Factory function for convenience
 */
export function createSSEParser(): SSEParser {
  return new SSEParser();
}
