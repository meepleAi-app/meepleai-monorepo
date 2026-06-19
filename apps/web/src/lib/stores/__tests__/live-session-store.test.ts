import { describe, expect, it, beforeEach } from 'vitest';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';

describe('useLiveSessionStore — Block A #2389 contract evolution', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().reset();
  });

  it('initial state — scoringType is null', () => {
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
  });

  it('initial state — scoreData is null', () => {
    expect(useLiveSessionStore.getState().scoreData).toBeNull();
  });

  it('setScoringConfig writes scoringType + scoreData', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    expect(useLiveSessionStore.getState().scoringType).toBe('Points');
    expect(useLiveSessionStore.getState().scoreData).toEqual({
      scores: [{ playerId: 'p1', points: 10 }],
    });
  });

  it('PlayerInfo carries an optional displayName', () => {
    useLiveSessionStore.getState().setSession({
      players: [{ id: 'p1', name: 'Aaron', displayName: 'Aaron D.', isHost: true, isOnline: true }],
    });
    expect(useLiveSessionStore.getState().players[0]?.displayName).toBe('Aaron D.');
  });

  // #2430 Block B+: rateLimitedUntil persistence
  it('initial state — rateLimitedUntil is null', () => {
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeNull();
  });

  it('setRateLimitedUntil writes a positive timestamp', () => {
    const deadline = 1_700_000_000_000;
    useLiveSessionStore.getState().setRateLimitedUntil(deadline);
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBe(deadline);
  });

  it('setRateLimitedUntil(null) clears the deadline', () => {
    useLiveSessionStore.getState().setRateLimitedUntil(1_700_000_000_000);
    useLiveSessionStore.getState().setRateLimitedUntil(null);
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeNull();
  });
});
