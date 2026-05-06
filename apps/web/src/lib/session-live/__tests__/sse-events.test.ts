/**
 * Unit tests for sse-events.ts (Wave D.2, Issue #746)
 *
 * Foundation sub-PR: SSE event types are REFERENCE ONLY (no actual SSE handling).
 * Tests verify enum membership, discriminated union shape, and type guard.
 */
import { describe, expect, it } from 'vitest';
import {
  SESSION_EVENT_TYPES,
  isSessionEvent,
  type SessionEventType,
  type SessionEvent,
} from '../sse-events';

describe('SESSION_EVENT_TYPES array', () => {
  it('contains exactly 12 event types', () => {
    expect(SESSION_EVENT_TYPES).toHaveLength(12);
  });

  it('includes all expected event type strings', () => {
    const expected: SessionEventType[] = [
      'session:score',
      'session:turn',
      'session:player-join',
      'session:player-leave',
      'session:role-change',
      'session:pause',
      'session:resume',
      'session:endgame',
      'session:chat',
      'session:tool-execution',
      'session:diary',
      'heartbeat',
    ];
    for (const type of expected) {
      expect(SESSION_EVENT_TYPES).toContain(type);
    }
  });

  it('is typed as ReadonlyArray (array with 12 members)', () => {
    // ReadonlyArray is a TypeScript type constraint, not a runtime frozen object.
    expect(Array.isArray(SESSION_EVENT_TYPES)).toBe(true);
    expect(SESSION_EVENT_TYPES.length).toBe(12);
  });

  it('contains no duplicates', () => {
    const set = new Set(SESSION_EVENT_TYPES);
    expect(set.size).toBe(SESSION_EVENT_TYPES.length);
  });
});

describe('isSessionEvent type guard', () => {
  it('returns false for null', () => {
    expect(isSessionEvent(null)).toBe(false);
  });

  it('returns false for undefined', () => {
    expect(isSessionEvent(undefined)).toBe(false);
  });

  it('returns false for a string', () => {
    expect(isSessionEvent('session:score')).toBe(false);
  });

  it('returns false for a number', () => {
    expect(isSessionEvent(42)).toBe(false);
  });

  it('returns false for an empty object', () => {
    expect(isSessionEvent({})).toBe(false);
  });

  it('returns false for object with unknown type', () => {
    expect(isSessionEvent({ type: 'session:unknown', timestamp: '2026-05-05T00:00:00Z' })).toBe(
      false
    );
  });

  it('returns true for a valid session:score event shape', () => {
    const event: SessionEvent = {
      type: 'session:score',
      sessionId: 'abc-123',
      participantId: 'p-1',
      score: 10,
      updatedBy: 'host-id',
      timestamp: '2026-05-05T00:00:00Z',
    };
    expect(isSessionEvent(event)).toBe(true);
  });

  it('returns true for a valid heartbeat event', () => {
    const event: SessionEvent = {
      type: 'heartbeat',
      timestamp: '2026-05-05T00:00:00Z',
    };
    expect(isSessionEvent(event)).toBe(true);
  });

  it('returns true for all valid event types with minimal shape', () => {
    for (const type of SESSION_EVENT_TYPES) {
      expect(isSessionEvent({ type, timestamp: '2026-05-05T00:00:00Z' })).toBe(true);
    }
  });

  it('returns false for object with type=null', () => {
    expect(isSessionEvent({ type: null })).toBe(false);
  });

  it('returns false for array', () => {
    expect(isSessionEvent([])).toBe(false);
  });
});

describe('SessionEvent discriminated union completeness', () => {
  it('session:endgame payload has finalScores array shape', () => {
    const event: SessionEvent = {
      type: 'session:endgame',
      sessionId: 'sess-1',
      finalScores: [
        { participantId: 'p-1', score: 100, winner: true },
        { participantId: 'p-2', score: 80, winner: false },
      ],
      timestamp: '2026-05-05T00:00:00Z',
    };
    expect(event.finalScores).toHaveLength(2);
    expect(event.finalScores[0].winner).toBe(true);
  });

  it('session:chat payload has visibility field', () => {
    const event: SessionEvent = {
      type: 'session:chat',
      sessionId: 'sess-1',
      messageId: 'msg-1',
      senderId: 'p-1',
      content: 'Hello!',
      visibility: 'shared',
      timestamp: '2026-05-05T00:00:00Z',
    };
    expect(event.visibility).toBe('shared');
  });

  it('session:pause reason field is optional', () => {
    const withReason: SessionEvent = {
      type: 'session:pause',
      sessionId: 'sess-1',
      pausedBy: 'host-1',
      reason: 'Bathroom break',
      timestamp: '2026-05-05T00:00:00Z',
    };
    const withoutReason: SessionEvent = {
      type: 'session:pause',
      sessionId: 'sess-1',
      pausedBy: 'host-1',
      timestamp: '2026-05-05T00:00:00Z',
    };
    expect(withReason.reason).toBe('Bathroom break');
    expect(withoutReason.reason).toBeUndefined();
  });
});
