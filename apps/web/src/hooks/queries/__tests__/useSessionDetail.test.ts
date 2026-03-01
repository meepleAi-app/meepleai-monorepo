/**
 * Tests for useSessionDetail hooks
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 */

import { describe, expect, it } from 'vitest';

import { sessionDetailKeys } from '../useSessionDetail';
import { sessionsKeys } from '../useActiveSessions';

// ============================================================================
// Query Key Tests
// ============================================================================

describe('sessionDetailKeys', () => {
  it('inherits sessionsKeys.all', () => {
    expect(sessionDetailKeys.all).toEqual(sessionsKeys.all);
    expect(sessionDetailKeys.all).toEqual(['sessions']);
  });

  it('inherits sessionsKeys.detail', () => {
    const id = 'session-123';
    expect(sessionDetailKeys.detail(id)).toEqual(sessionsKeys.detail(id));
    expect(sessionDetailKeys.detail(id)).toEqual(['sessions', 'detail', 'session-123']);
  });

  it('creates state key nested under detail', () => {
    expect(sessionDetailKeys.state('session-123')).toEqual([
      'sessions',
      'detail',
      'session-123',
      'state',
    ]);
  });

  it('creates snapshots key nested under detail', () => {
    expect(sessionDetailKeys.snapshots('session-123')).toEqual([
      'sessions',
      'detail',
      'session-123',
      'snapshots',
    ]);
  });

  it('creates toolkit key nested under detail', () => {
    expect(sessionDetailKeys.toolkit('session-123')).toEqual([
      'sessions',
      'detail',
      'session-123',
      'toolkit',
    ]);
  });

  it('uses different keys for different sessions', () => {
    const key1 = sessionDetailKeys.state('session-1');
    const key2 = sessionDetailKeys.state('session-2');
    expect(key1).not.toEqual(key2);
  });

  it('state key invalidates with detail key', () => {
    const detailKey = sessionDetailKeys.detail('session-123');
    const stateKey = sessionDetailKeys.state('session-123');
    // state key starts with detail key prefix
    expect(stateKey.slice(0, detailKey.length)).toEqual(detailKey);
  });
});
