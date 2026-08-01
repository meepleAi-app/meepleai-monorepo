import { describe, expect, it, beforeEach } from 'vitest';

import { useLiveSessionStore, type RuleDispute } from '@/lib/stores/live-session-store';
import type { TurnOrderType } from '@/lib/session-live/turn-state';

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

  // Block C cleanup (#2389): legacy `scores` map + `updateScore` action removed.
  it('exposes no legacy `scores` field on the store', () => {
    expect((useLiveSessionStore.getState() as Record<string, unknown>).scores).toBeUndefined();
  });

  it('exposes no `updateScore` action on the store', () => {
    expect((useLiveSessionStore.getState() as Record<string, unknown>).updateScore).toBeUndefined();
  });

  it('resolveProposal removes the proposal without touching any scores field', () => {
    const store = useLiveSessionStore.getState();
    store.addProposal({ id: 'pr1', playerName: 'Alice', delta: 5, timestamp: Date.now() });
    store.addProposal({ id: 'pr2', playerName: 'Bob', delta: 3, timestamp: Date.now() });

    expect(useLiveSessionStore.getState().pendingProposals).toHaveLength(2);

    // Resolve accepted=true — must NOT throw even though there is no `scores` map any more
    useLiveSessionStore.getState().resolveProposal('pr1', true);
    expect(useLiveSessionStore.getState().pendingProposals).toEqual([
      expect.objectContaining({ id: 'pr2' }),
    ]);
    expect((useLiveSessionStore.getState() as Record<string, unknown>).scores).toBeUndefined();

    // Resolve accepted=false — also must not regress
    useLiveSessionStore.getState().resolveProposal('pr2', false);
    expect(useLiveSessionStore.getState().pendingProposals).toEqual([]);
  });

  it('resolveProposal is a no-op when the proposalId is unknown', () => {
    useLiveSessionStore.getState().addProposal({
      id: 'pr1',
      playerName: 'Alice',
      delta: 5,
      timestamp: Date.now(),
    });
    useLiveSessionStore.getState().resolveProposal('unknown-id', true);
    expect(useLiveSessionStore.getState().pendingProposals).toHaveLength(1);
  });

  // #2483 Task 2 — turnOrderType field
  it('initial state — turnOrderType is null', () => {
    expect(useLiveSessionStore.getState().turnOrderType).toBeNull();
  });

  it('setTurnOrderType writes the turnOrderType value', () => {
    useLiveSessionStore.getState().setTurnOrderType('Sequential');
    expect(useLiveSessionStore.getState().turnOrderType).toBe('Sequential');
  });

  it('setTurnOrderType(null) clears the value', () => {
    useLiveSessionStore.getState().setTurnOrderType('RoundRobin');
    useLiveSessionStore.getState().setTurnOrderType(null);
    expect(useLiveSessionStore.getState().turnOrderType).toBeNull();
  });

  it('reset() clears turnOrderType to null', () => {
    useLiveSessionStore.getState().setTurnOrderType('Custom');
    useLiveSessionStore.getState().reset();
    expect(useLiveSessionStore.getState().turnOrderType).toBeNull();
  });

  it('setTurnOrderType accepts all valid TurnOrderType variants', () => {
    const variants: TurnOrderType[] = [
      'RoundRobin',
      'Sequential',
      'Simultaneous',
      'Realtime',
      'None',
      'Custom',
      'FirstPlayerToken',
    ];
    for (const variant of variants) {
      useLiveSessionStore.getState().setTurnOrderType(variant);
      expect(useLiveSessionStore.getState().turnOrderType).toBe(variant);
    }
  });

  // #3025 L1 — opaque live game-state field
  it('initial state — gameState is null', () => {
    expect(useLiveSessionStore.getState().gameState).toBeNull();
  });

  it('setGameState writes an opaque object', () => {
    const state = { round: 3, activePlayer: 'p1', dev: { catan: { longestRoad: 'p2' } } };
    useLiveSessionStore.getState().setGameState(state);
    expect(useLiveSessionStore.getState().gameState).toEqual(state);
  });

  it('setGameState(null) clears the value', () => {
    useLiveSessionStore.getState().setGameState({ round: 1 });
    useLiveSessionStore.getState().setGameState(null);
    expect(useLiveSessionStore.getState().gameState).toBeNull();
  });

  it('setGameState replaces (not merges) the previous state', () => {
    useLiveSessionStore.getState().setGameState({ a: 1, b: 2 });
    useLiveSessionStore.getState().setGameState({ c: 3 });
    expect(useLiveSessionStore.getState().gameState).toEqual({ c: 3 });
  });

  it('reset() clears gameState to null', () => {
    useLiveSessionStore.getState().setGameState({ round: 5 });
    useLiveSessionStore.getState().reset();
    expect(useLiveSessionStore.getState().gameState).toBeNull();
  });

  // #3391 (finding C8) — setDisputes bulk-hydration for REST reload of the Arbitro tab
  it('initial state — disputes is empty', () => {
    expect(useLiveSessionStore.getState().disputes).toEqual([]);
  });

  it('setDisputes replaces the dispute list (bulk hydration on reload)', () => {
    // A stale SignalR-appended dispute should be replaced by the authoritative REST snapshot.
    useLiveSessionStore.getState().addDispute({
      id: 'stale',
      description: 'stale',
      verdict: 'x',
      ruleReferences: [],
      raisedByPlayerName: 'X',
      timestamp: '2020-01-01T00:00:00Z',
    });

    const hydrated: RuleDispute[] = [
      {
        id: 'd1',
        description: 'Can I play two cards?',
        verdict: 'No — one per turn.',
        ruleReferences: ['p.12'],
        raisedByPlayerName: 'Alice',
        timestamp: '2026-01-01T00:00:00Z',
      },
      {
        id: 'd2',
        description: 'Does a tie break by score?',
        verdict: 'Yes.',
        ruleReferences: ['p.4', 'p.5'],
        raisedByPlayerName: 'Bob',
        timestamp: '2026-01-01T00:05:00Z',
      },
    ];

    useLiveSessionStore.getState().setDisputes(hydrated);

    expect(useLiveSessionStore.getState().disputes).toEqual(hydrated);
  });

  it('reset() clears disputes to an empty list', () => {
    useLiveSessionStore.getState().setDisputes([
      {
        id: 'd1',
        description: 'x',
        verdict: 'y',
        ruleReferences: [],
        raisedByPlayerName: 'A',
        timestamp: '2026-01-01T00:00:00Z',
      },
    ]);
    useLiveSessionStore.getState().reset();
    expect(useLiveSessionStore.getState().disputes).toEqual([]);
  });
});
