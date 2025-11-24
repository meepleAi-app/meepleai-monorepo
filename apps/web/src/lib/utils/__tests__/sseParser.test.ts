/**
 * SSE Parser Tests (Issue #1007)
 *
 * Comprehensive test suite for SSEParser utility
 * Tests: complete events, incomplete chunks, multiple events, malformed data, buffer management
 */

import { describe, it, expect, beforeEach } from '@jest/globals';
import { SSEParser } from '../sseParser';
import { StreamingEventType } from '@/lib/api/schemas/streaming.schemas';

describe('SSEParser', () => {
  let parser: SSEParser;

  beforeEach(() => {
    parser = new SSEParser();
  });

  describe('Complete Event Parsing', () => {
    it('should parse a complete SSE event', () => {
      const chunk = 'data: {"type":"token","data":{"token":"Hello"},"timestamp":"2025-01-15T10:00:00Z"}\n\n';

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1);
      expect(events[0].type).toBe(StreamingEventType.Token);
      expect(events[0].data).toEqual({ token: 'Hello' });
      expect(events[0].timestamp).toBe('2025-01-15T10:00:00Z');
    });

    it('should parse stateUpdate event', () => {
      const chunk =
        'data: {"type":"stateUpdate","data":{"state":"Searching vector database..."},"timestamp":"2025-01-15T10:00:00Z"}\n\n';

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1);
      expect(events[0].type).toBe(StreamingEventType.StateUpdate);
      expect(events[0].data).toEqual({ state: 'Searching vector database...' });
    });

    it('should parse citations event', () => {
      const chunk =
        'data: {"type":"citations","data":{"citations":[{"source":"rules.pdf","page":1,"text":"Game rules"}]},"timestamp":"2025-01-15T10:00:00Z"}\n\n';

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1);
      expect(events[0].type).toBe(StreamingEventType.Citations);
      expect(events[0].data).toHaveProperty('citations');
    });

    it('should parse complete event', () => {
      const chunk =
        'data: {"type":"complete","data":{"totalTokens":100,"confidence":0.95},"timestamp":"2025-01-15T10:00:00Z"}\n\n';

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1);
      expect(events[0].type).toBe(StreamingEventType.Complete);
      expect(events[0].data).toEqual({ totalTokens: 100, confidence: 0.95 });
    });

    it('should parse error event', () => {
      const chunk =
        'data: {"type":"error","data":{"message":"Something went wrong","code":"INTERNAL_ERROR"},"timestamp":"2025-01-15T10:00:00Z"}\n\n';

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1);
      expect(events[0].type).toBe(StreamingEventType.Error);
      expect(events[0].data).toEqual({ message: 'Something went wrong', code: 'INTERNAL_ERROR' });
    });
  });

  describe('Multiple Events in Single Chunk', () => {
    it('should parse multiple events separated by double newline', () => {
      const chunk = [
        'data: {"type":"stateUpdate","data":{"state":"Processing..."},"timestamp":"2025-01-15T10:00:00Z"}',
        '',
        'data: {"type":"token","data":{"token":"Hello"},"timestamp":"2025-01-15T10:00:01Z"}',
        '',
        'data: {"type":"token","data":{"token":" World"},"timestamp":"2025-01-15T10:00:02Z"}',
        '',
        '',
      ].join('\n');

      const events = parser.parse(chunk);

      expect(events).toHaveLength(3);
      expect(events[0].type).toBe(StreamingEventType.StateUpdate);
      expect(events[1].type).toBe(StreamingEventType.Token);
      expect(events[1].data).toEqual({ token: 'Hello' });
      expect(events[2].type).toBe(StreamingEventType.Token);
      expect(events[2].data).toEqual({ token: ' World' });
    });

    it('should handle mixed event types in batch', () => {
      const chunk = [
        'data: {"type":"stateUpdate","data":{"state":"Starting..."},"timestamp":"2025-01-15T10:00:00Z"}',
        '',
        'data: {"type":"citations","data":{"citations":[]},"timestamp":"2025-01-15T10:00:01Z"}',
        '',
        'data: {"type":"complete","data":{"totalTokens":50,"confidence":0.9},"timestamp":"2025-01-15T10:00:02Z"}',
        '',
        '',
      ].join('\n');

      const events = parser.parse(chunk);

      expect(events).toHaveLength(3);
      expect(events[0].type).toBe(StreamingEventType.StateUpdate);
      expect(events[1].type).toBe(StreamingEventType.Citations);
      expect(events[2].type).toBe(StreamingEventType.Complete);
    });
  });

  describe('Incomplete Chunk Handling (Buffering)', () => {
    it('should buffer incomplete chunk and complete on next parse', () => {
      // First chunk: incomplete (no double newline)
      const chunk1 = 'data: {"type":"token","data":{"token":"Hel';

      const events1 = parser.parse(chunk1);
      expect(events1).toHaveLength(0); // No complete event yet

      // Second chunk: completes the event
      const chunk2 = 'lo"},"timestamp":"2025-01-15T10:00:00Z"}\n\n';

      const events2 = parser.parse(chunk2);
      expect(events2).toHaveLength(1);
      expect(events2[0].type).toBe(StreamingEventType.Token);
      expect(events2[0].data).toEqual({ token: 'Hello' });
    });

    it('should handle multiple incomplete chunks', () => {
      const events1 = parser.parse('data: {"type":"token",');
      expect(events1).toHaveLength(0);

      const events2 = parser.parse('"data":{"token":"');
      expect(events2).toHaveLength(0);

      const events3 = parser.parse('Test"},"timestamp":"2025-01-15T10:00:00Z"}\n\n');
      expect(events3).toHaveLength(1);
      expect(events3[0].data).toEqual({ token: 'Test' });
    });

    it('should handle incomplete event at end of batch', () => {
      const chunk = [
        'data: {"type":"token","data":{"token":"Complete"},"timestamp":"2025-01-15T10:00:00Z"}',
        '',
        'data: {"type":"token","data":{"token":"Incomplete',
      ].join('\n');

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1); // Only complete event processed
      expect(events[0].data).toEqual({ token: 'Complete' });

      // Buffer should contain incomplete event
      expect(parser.getBuffer()).toContain('Incomplete');
    });
  });

  describe('Malformed Data Handling', () => {
    it('should skip empty chunks', () => {
      const events = parser.parse('\n\n\n');
      expect(events).toHaveLength(0);
    });

    it('should skip events without data line', () => {
      const chunk = 'event: ping\n\n';
      const events = parser.parse(chunk);
      expect(events).toHaveLength(0);
    });

    it('should handle invalid JSON gracefully', () => {
      const chunk = 'data: {invalid json}\n\n';

      // Should not throw, but log error
      const events = parser.parse(chunk);
      expect(events).toHaveLength(0);
    });

    it('should continue parsing after malformed event', () => {
      const chunk = [
        'data: {invalid}',
        '',
        'data: {"type":"token","data":{"token":"Valid"},"timestamp":"2025-01-15T10:00:00Z"}',
        '',
        '',
      ].join('\n');

      const events = parser.parse(chunk);

      expect(events).toHaveLength(1); // Only valid event
      expect(events[0].data).toEqual({ token: 'Valid' });
    });
  });

  describe('Buffer Management', () => {
    it('should reset buffer on reset()', () => {
      parser.parse('data: {"incomplete');
      expect(parser.getBuffer()).toBeTruthy();

      parser.reset();
      expect(parser.getBuffer()).toBe('');
    });

    it('should maintain buffer across multiple parse calls', () => {
      parser.parse('data: {"type":"token"');
      const buffer1 = parser.getBuffer();
      expect(buffer1).toBeTruthy();

      parser.parse(',"data":{"token":"Test"}');
      const buffer2 = parser.getBuffer();
      expect(buffer2.length).toBeGreaterThan(buffer1.length);
    });

    it('should clear buffer after complete event', () => {
      parser.parse('data: {"incomplete');
      expect(parser.getBuffer()).toBeTruthy();

      parser.parse('"},"timestamp":"2025-01-15T10:00:00Z"}\n\n');
      // Buffer should be empty or contain only trailing content
      const buffer = parser.getBuffer();
      expect(buffer).toBe('');
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty data field', () => {
      const chunk = 'data: \n\n';
      const events = parser.parse(chunk);
      expect(events).toHaveLength(0);
    });

    it('should handle whitespace-only chunks', () => {
      const events = parser.parse('   \n   \n');
      expect(events).toHaveLength(0);
    });

    it('should handle events with extra whitespace', () => {
      const chunk =
        '  data:   {"type":"token","data":{"token":"Trim"},"timestamp":"2025-01-15T10:00:00Z"}  \n\n';

      const events = parser.parse(chunk);
      expect(events).toHaveLength(1);
      expect(events[0].data).toEqual({ token: 'Trim' });
    });

    it('should handle very long token strings', () => {
      const longToken = 'A'.repeat(10000);
      const chunk = `data: {"type":"token","data":{"token":"${longToken}"},"timestamp":"2025-01-15T10:00:00Z"}\n\n`;

      const events = parser.parse(chunk);
      expect(events).toHaveLength(1);
      expect(events[0].data).toEqual({ token: longToken });
    });
  });

  describe('Real-World SSE Patterns', () => {
    it('should handle backend SSE format exactly', () => {
      // Simulate exact backend format from AiEndpoints.cs
      const chunk = [
        'data: {"type":"stateUpdate","data":{"state":"Generating embeddings..."},"timestamp":"2025-01-15T10:00:00.000Z"}',
        '',
        'data: {"type":"citations","data":{"citations":[{"source":"manual.pdf","page":5,"text":"Game setup requires...","score":0.95}]},"timestamp":"2025-01-15T10:00:01.000Z"}',
        '',
        'data: {"type":"token","data":{"token":"The"},"timestamp":"2025-01-15T10:00:02.000Z"}',
        '',
        'data: {"type":"token","data":{"token":" game"},"timestamp":"2025-01-15T10:00:02.100Z"}',
        '',
        'data: {"type":"complete","data":{"totalTokens":2,"confidence":0.92,"snippets":[]},"timestamp":"2025-01-15T10:00:03.000Z"}',
        '',
        '',
      ].join('\n');

      const events = parser.parse(chunk);

      expect(events).toHaveLength(5);
      expect(events[0].type).toBe(StreamingEventType.StateUpdate);
      expect(events[1].type).toBe(StreamingEventType.Citations);
      expect(events[2].type).toBe(StreamingEventType.Token);
      expect(events[3].type).toBe(StreamingEventType.Token);
      expect(events[4].type).toBe(StreamingEventType.Complete);

      // Verify token accumulation
      const tokens = events
        .filter((e) => e.type === StreamingEventType.Token)
        .map((e: any) => e.data.token);
      expect(tokens.join('')).toBe('The game');
    });
  });
});
