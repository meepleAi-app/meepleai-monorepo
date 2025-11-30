/**
 * Tests for test-utils mock creation utilities
 */

import {
  createMockEvents,
  createMockEvent,
} from './test-utils';
import { eventCycles } from './test-utils.test-helpers';

describe('test-utils - Mock Creation', () => {
  describe('createMockEvents', () => {
    describe('Basic Functionality', () => {
      it('should create requested number of events', () => {
        const events = createMockEvents(3);
        expect(events).toHaveLength(3);
      });

      it('should create zero events', () => {
        const events = createMockEvents(0);
        expect(events).toHaveLength(0);
      });

      it('should create large number of events', () => {
        const events = createMockEvents(100);
        expect(events).toHaveLength(100);
      });
    });

    describe('Event Properties', () => {
      it('should create events with different types', () => {
        const events = createMockEvents(6);
        const types = events.map((e) => e.type);
        expect(types).toContain('message');
        expect(types).toContain('rag_search');
        expect(types).toContain('rag_retrieval');
      });

      it('should cycle through event types', () => {
        const events = createMockEvents(12);
        const types = events.map((e) => e.type);

        // With 6 types, event 0 and event 6 should have same type
        expect(types[0]).toBe(types[6]);
        expect(types[1]).toBe(types[7]);
      });

      it('should cycle through statuses', () => {
        const events = createMockEvents(8);
        const statuses = events.map((e) => e.status);

        // With 4 statuses, event 0 and event 4 should have same status
        expect(statuses[0]).toBe(statuses[4]);
      });

      it('should generate sequential IDs', () => {
        const events = createMockEvents(5);

        expect(events[0].id).toBe('event-0');
        expect(events[1].id).toBe('event-1');
        expect(events[2].id).toBe('event-2');
        expect(events[3].id).toBe('event-3');
        expect(events[4].id).toBe('event-4');
      });

      it('should create events with sequential timestamps', () => {
        const events = createMockEvents(3);
        expect(events[0].timestamp.getTime()).toBeGreaterThan(
          events[1].timestamp.getTime()
        );
        expect(events[1].timestamp.getTime()).toBeGreaterThan(
          events[2].timestamp.getTime()
        );
      });

      it('should have 1 minute gap between event timestamps', () => {
        const events = createMockEvents(3);

        const gap1 = events[0].timestamp.getTime() - events[1].timestamp.getTime();
        const gap2 = events[1].timestamp.getTime() - events[2].timestamp.getTime();

        expect(gap1).toBe(60000); // 1 minute in ms
        expect(gap2).toBe(60000);
      });
    });

    describe('Event Data', () => {
      it('should include message in event data', () => {
        const events = createMockEvents(3);

        events.forEach((event, i) => {
          expect(event.data.message).toBe(`Event ${i} message`);
        });
      });

      it('should alternate role between user and assistant', () => {
        const events = createMockEvents(4);

        expect(events[0].data.role).toBe('user'); // i=0, even
        expect(events[1].data.role).toBe('assistant'); // i=1, odd
        expect(events[2].data.role).toBe('user'); // i=2, even
        expect(events[3].data.role).toBe('assistant'); // i=3, odd
      });

      it('should include citations for every 3rd event', () => {
        const events = createMockEvents(6);

        expect(events[0].data.citations).toBeDefined(); // i=0, 0 % 3 == 0
        expect(events[1].data.citations).toBeUndefined(); // i=1
        expect(events[2].data.citations).toBeUndefined(); // i=2
        expect(events[3].data.citations).toBeDefined(); // i=3, 3 % 3 == 0
      });

      it('should include metrics for every 4th event', () => {
        const events = createMockEvents(8);

        expect(events[0].data.metrics).toBeDefined(); // i=0, 0 % 4 == 0
        expect(events[1].data.metrics).toBeUndefined(); // i=1
        expect(events[2].data.metrics).toBeUndefined(); // i=2
        expect(events[3].data.metrics).toBeUndefined(); // i=3
        expect(events[4].data.metrics).toBeDefined(); // i=4, 4 % 4 == 0
      });

      it('should include error message for error type events', () => {
        const events = createMockEvents(12); // Need 12 to get error type (type index 5)

        const errorEvents = events.filter((e) => e.type === 'error');
        errorEvents.forEach((event, originalIndex) => {
          expect(event.data.error).toContain('Error message');
        });
      });

      it('should not include error for non-error events', () => {
        const events = createMockEvents(5);

        const nonErrorEvents = events.filter((e) => e.type !== 'error');
        nonErrorEvents.forEach((event) => {
          expect(event.data.error).toBeUndefined();
        });
      });

      it('should include standard endpoint', () => {
        const events = createMockEvents(3);

        events.forEach((event) => {
          expect(event.data.endpoint).toBe('/api/v1/agents/qa');
        });
      });

      it('should vary gameId based on index', () => {
        const events = createMockEvents(15);

        expect(events[0].data.gameId).toBe('game-0'); // Math.floor(0/5) = 0
        expect(events[5].data.gameId).toBe('game-1'); // Math.floor(5/5) = 1
        expect(events[10].data.gameId).toBe('game-2'); // Math.floor(10/5) = 2
      });

      it('should vary chatId based on index', () => {
        const events = createMockEvents(9);

        expect(events[0].data.chatId).toBe('chat-0'); // Math.floor(0/3) = 0
        expect(events[3].data.chatId).toBe('chat-1'); // Math.floor(3/3) = 1
        expect(events[6].data.chatId).toBe('chat-2'); // Math.floor(6/3) = 2
      });
    });

    describe('Citations (Snippets)', () => {
      it('should create 2 snippets when citations are included', () => {
        const events = createMockEvents(1); // First event has citations

        expect(events[0].data.citations).toHaveLength(2);
      });

      it('should have snippet with required properties', () => {
        const events = createMockEvents(1);

        const snippet = events[0].data.citations![0];
        expect(snippet).toHaveProperty('text');
        expect(snippet).toHaveProperty('source');
        expect(snippet).toHaveProperty('page');
        expect(snippet).toHaveProperty('line');
      });

      it('should have page numbers between 1 and 100', () => {
        const events = createMockEvents(3); // First event (i=0) has citations

        const snippet = events[0].data.citations![0];
        expect(snippet.page).toBeGreaterThanOrEqual(1);
        expect(snippet.page).toBeLessThanOrEqual(101);
      });

      it('should have line numbers between 1 and 50', () => {
        const events = createMockEvents(3);

        const snippet = events[0].data.citations![0];
        expect(snippet.line).toBeGreaterThanOrEqual(1);
        expect(snippet.line).toBeLessThanOrEqual(51);
      });
    });

    describe('Metrics', () => {
      it('should have required metric properties', () => {
        const events = createMockEvents(1); // First event has metrics

        const metrics = events[0].data.metrics!;
        expect(metrics).toHaveProperty('latencyMs');
        expect(metrics).toHaveProperty('promptTokens');
        expect(metrics).toHaveProperty('completionTokens');
        expect(metrics).toHaveProperty('totalTokens');
        expect(metrics).toHaveProperty('confidence');
      });

      it('should have totalTokens equal to prompt + completion', () => {
        const events = createMockEvents(1);

        const metrics = events[0].data.metrics!;
        expect(metrics.totalTokens).toBe(
          (metrics.promptTokens || 0) + (metrics.completionTokens || 0)
        );
      });

      it('should have latency between 100 and 2100 ms', () => {
        const events = createMockEvents(4); // Event 0 has metrics

        const metrics = events[0].data.metrics!;
        expect(metrics.latencyMs).toBeGreaterThanOrEqual(100);
        expect(metrics.latencyMs).toBeLessThanOrEqual(2100);
      });

      it('should have confidence between 0.7 and 1.0', () => {
        const events = createMockEvents(4);

        const metrics = events[0].data.metrics!;
        expect(metrics.confidence).toBeGreaterThanOrEqual(0.7);
        expect(metrics.confidence).toBeLessThanOrEqual(1.0);
      });
    });

    describe('Overrides', () => {
      it('should apply overrides to specific events', () => {
        const events = createMockEvents(3, [
          { type: 'error', status: 'error' },
          undefined,
          { type: 'rag_complete', status: 'success' },
        ]);

        expect(events[0].type).toBe('error');
        expect(events[0].status).toBe('error');
        expect(events[2].type).toBe('rag_complete');
        expect(events[2].status).toBe('success');
      });

      it('should not override when override is undefined', () => {
        const events = createMockEvents(2, [undefined, undefined]);

        // Should use default type rotation
        expect(events[0].type).toBeDefined();
        expect(events[1].type).toBeDefined();
      });

      it('should partially override event properties', () => {
        const events = createMockEvents(1, [{ type: 'error' }]);

        expect(events[0].type).toBe('error');
        // Other properties should still be present
        expect(events[0].status).toBeDefined();
        expect(events[0].id).toBeDefined();
      });
    });
  });

  describe('createMockEvent', () => {
    describe('Happy Path', () => {
      it('should create single event', () => {
        const event = createMockEvent();

        expect(event).toHaveProperty('id');
        expect(event).toHaveProperty('type');
        expect(event).toHaveProperty('status');
        expect(event).toHaveProperty('timestamp');
        expect(event).toHaveProperty('data');
      });

      it('should accept overrides', () => {
        const event = createMockEvent({ type: 'error', status: 'error' });

        expect(event.type).toBe('error');
        expect(event.status).toBe('error');
      });

      it('should have event-0 as id', () => {
        const event = createMockEvent();

        expect(event.id).toBe('event-0');
      });
    });

    describe('Different Event Types', () => {
      it('should create message event', () => {
        const event = createMockEvent({ type: 'message' });

        expect(event.type).toBe('message');
      });

      it('should create rag_search event', () => {
        const event = createMockEvent({ type: 'rag_search' });

        expect(event.type).toBe('rag_search');
      });

      it('should create error event', () => {
        const event = createMockEvent({ type: 'error' });

        expect(event.type).toBe('error');
        // Note: error message is only auto-generated during createMockEvents based on index
        // When using createMockEvent with override, you need to manually add error in data
      });
    });

    describe('Edge Cases', () => {
      it('should handle empty overrides', () => {
        const event = createMockEvent({});

        expect(event).toHaveProperty('id');
        expect(event).toHaveProperty('type');
      });

      it('should handle undefined overrides', () => {
        const event = createMockEvent(undefined);

        expect(event).toHaveProperty('id');
        expect(event).toHaveProperty('type');
      });

      it('should override with null values', () => {
        const event = createMockEvent({ status: 'pending' as any });

        expect(event.status).toBe('pending');
      });
    });
  });
});
