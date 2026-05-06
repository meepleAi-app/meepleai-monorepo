/**
 * Tests for composeSessionLiveState (Wave D.2, Issue #750)
 *
 * Validates:
 * - Initial DTO → base state conversion
 * - Each of 12 event types → expected state transition
 * - Player-leave marks offline (does NOT remove player)
 * - Heartbeat is a no-op
 * - Multiple sequential events accumulate correctly
 *
 * Foundation schema (Issue #746): SessionEvent is flat-field, NO id/payload wrapper.
 * Idempotency (dedup by SSE envelope ID) is handled by useSessionLiveStream, not here.
 */

import { describe, expect, it } from 'vitest';

import type { SessionEvent } from '../sse-events';
import { composeSessionLiveState } from '../compose-session-live-state';
import type { InitialSessionData } from '../compose-session-live-state';

// ============================================================================
// Fixtures
// ============================================================================

const PLAYER_ALICE = {
  id: 'p-alice',
  displayName: 'Alice',
  role: 'Player',
  totalScore: 10,
  isActive: true,
};

const PLAYER_BOB = {
  id: 'p-bob',
  displayName: 'Bob',
  role: 'Player',
  totalScore: 5,
  isActive: true,
};

const BASE_SESSION: InitialSessionData = {
  status: 'InProgress',
  currentTurnIndex: 2,
  currentTurnPlayerId: 'p-alice',
  players: [PLAYER_ALICE, PLAYER_BOB],
};

const TS = '2026-01-01T10:00:00Z';

function makeScoreEvent(
  participantId: string,
  score: number
): Extract<SessionEvent, { type: 'session:score' }> {
  return {
    type: 'session:score',
    sessionId: 'sess-1',
    participantId,
    score,
    updatedBy: participantId,
    timestamp: TS,
  };
}

// ============================================================================
// Base state conversion
// ============================================================================

describe('composeSessionLiveState — initial DTO → base state', () => {
  it('converts players correctly', () => {
    const state = composeSessionLiveState(BASE_SESSION, []);
    expect(state.players).toHaveLength(2);
    const alice = state.players.find(p => p.id === 'p-alice');
    expect(alice?.name).toBe('Alice');
    expect(alice?.score).toBe(10);
    expect(alice?.role).toBe('Player');
    expect(alice?.isOnline).toBe(true);
  });

  it('maps status InProgress', () => {
    const state = composeSessionLiveState(BASE_SESSION, []);
    expect(state.status).toBe('InProgress');
  });

  it('maps status Paused', () => {
    const state = composeSessionLiveState({ ...BASE_SESSION, status: 'Paused' }, []);
    expect(state.status).toBe('Paused');
  });

  it('defaults status to Setup for unknown values', () => {
    const state = composeSessionLiveState({ ...BASE_SESSION, status: 'Created' }, []);
    expect(state.status).toBe('Setup');
  });

  it('maps currentTurnIndex → currentTurn', () => {
    const state = composeSessionLiveState(BASE_SESSION, []);
    expect(state.currentTurn).toBe(2);
  });

  it('maps currentTurnPlayerId → activePlayerId', () => {
    const state = composeSessionLiveState(BASE_SESSION, []);
    expect(state.activePlayerId).toBe('p-alice');
  });

  it('empty players → empty array', () => {
    const state = composeSessionLiveState({ status: 'InProgress' }, []);
    expect(state.players).toHaveLength(0);
  });

  it('empty events → empty actionLog', () => {
    const state = composeSessionLiveState(BASE_SESSION, []);
    expect(state.actionLog).toHaveLength(0);
  });

  it('accepts fixture-style currentTurn field directly', () => {
    const state = composeSessionLiveState({ currentTurn: 7, totalTurns: 20 }, []);
    expect(state.currentTurn).toBe(7);
    expect(state.totalTurns).toBe(20);
  });

  it('normalizes player.name fallback for displayName', () => {
    const state = composeSessionLiveState(
      { players: [{ id: 'p1', name: 'Direct Name', totalScore: 0 }] },
      []
    );
    expect(state.players[0].name).toBe('Direct Name');
  });

  it('uses player.score when totalScore absent', () => {
    const state = composeSessionLiveState(
      { players: [{ id: 'p1', displayName: 'P1', score: 33 }] },
      []
    );
    expect(state.players[0].score).toBe(33);
  });
});

// ============================================================================
// session:score
// ============================================================================

describe('composeSessionLiveState — session:score', () => {
  it('updates correct player score', () => {
    const state = composeSessionLiveState(BASE_SESSION, [makeScoreEvent('p-alice', 99)]);
    const alice = state.players.find(p => p.id === 'p-alice');
    expect(alice?.score).toBe(99);
  });

  it('does not affect other players', () => {
    const state = composeSessionLiveState(BASE_SESSION, [makeScoreEvent('p-alice', 99)]);
    const bob = state.players.find(p => p.id === 'p-bob');
    expect(bob?.score).toBe(5); // unchanged
  });

  it('handles unknown participantId gracefully (no crash, no new player)', () => {
    const state = composeSessionLiveState(BASE_SESSION, [makeScoreEvent('p-unknown', 1)]);
    expect(state.players).toHaveLength(2); // no new player added
  });

  it('multiple score events accumulate correctly', () => {
    const state = composeSessionLiveState(BASE_SESSION, [
      makeScoreEvent('p-alice', 20),
      makeScoreEvent('p-alice', 35), // last wins
    ]);
    expect(state.players.find(p => p.id === 'p-alice')?.score).toBe(35);
  });
});

// ============================================================================
// session:turn
// ============================================================================

describe('composeSessionLiveState — session:turn', () => {
  it('updates activePlayerId and currentTurn', () => {
    const event: Extract<SessionEvent, { type: 'session:turn' }> = {
      type: 'session:turn',
      sessionId: 'sess-1',
      turnNumber: 5,
      activePlayerId: 'p-bob',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.currentTurn).toBe(5);
    expect(state.activePlayerId).toBe('p-bob');
  });
});

// ============================================================================
// session:player-join
// ============================================================================

describe('composeSessionLiveState — session:player-join', () => {
  it('appends new player with score 0', () => {
    const event: Extract<SessionEvent, { type: 'session:player-join' }> = {
      type: 'session:player-join',
      sessionId: 'sess-1',
      participantId: 'p-carol',
      playerName: 'Carol',
      role: 'Player',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.players).toHaveLength(3);
    const carol = state.players.find(p => p.id === 'p-carol');
    expect(carol?.name).toBe('Carol');
    expect(carol?.score).toBe(0);
    expect(carol?.isOnline).toBe(true);
  });

  it('marks existing player online (idempotent join)', () => {
    const event: Extract<SessionEvent, { type: 'session:player-join' }> = {
      type: 'session:player-join',
      sessionId: 'sess-1',
      participantId: 'p-alice',
      playerName: 'Alice',
      role: 'Player',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.players).toHaveLength(2); // no duplicate
    const alice = state.players.find(p => p.id === 'p-alice');
    expect(alice?.isOnline).toBe(true);
  });

  it('Host role is preserved on join', () => {
    const event: Extract<SessionEvent, { type: 'session:player-join' }> = {
      type: 'session:player-join',
      sessionId: 'sess-1',
      participantId: 'p-host',
      playerName: 'Host',
      role: 'Host',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    const host = state.players.find(p => p.id === 'p-host');
    expect(host?.role).toBe('Host');
  });
});

// ============================================================================
// session:player-leave
// ============================================================================

describe('composeSessionLiveState — session:player-leave', () => {
  it('marks player offline, does NOT remove', () => {
    const event: Extract<SessionEvent, { type: 'session:player-leave' }> = {
      type: 'session:player-leave',
      sessionId: 'sess-1',
      participantId: 'p-bob',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.players).toHaveLength(2); // still 2
    const bob = state.players.find(p => p.id === 'p-bob');
    expect(bob?.isOnline).toBe(false);
  });

  it('does not affect other players online status', () => {
    const event: Extract<SessionEvent, { type: 'session:player-leave' }> = {
      type: 'session:player-leave',
      sessionId: 'sess-1',
      participantId: 'p-bob',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    const alice = state.players.find(p => p.id === 'p-alice');
    expect(alice?.isOnline).toBe(true);
  });

  it('join then leave → player is offline', () => {
    const join: Extract<SessionEvent, { type: 'session:player-join' }> = {
      type: 'session:player-join',
      sessionId: 'sess-1',
      participantId: 'p-carol',
      playerName: 'Carol',
      role: 'Player',
      timestamp: TS,
    };
    const leave: Extract<SessionEvent, { type: 'session:player-leave' }> = {
      type: 'session:player-leave',
      sessionId: 'sess-1',
      participantId: 'p-carol',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [join, leave]);
    const carol = state.players.find(p => p.id === 'p-carol');
    expect(carol?.isOnline).toBe(false);
    expect(state.players).toHaveLength(3); // still present
  });
});

// ============================================================================
// session:role-change
// ============================================================================

describe('composeSessionLiveState — session:role-change', () => {
  it('updates only target player role', () => {
    const event: Extract<SessionEvent, { type: 'session:role-change' }> = {
      type: 'session:role-change',
      sessionId: 'sess-1',
      participantId: 'p-alice',
      oldRole: 'Player',
      newRole: 'Host',
      assignedBy: 'p-alice',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    const alice = state.players.find(p => p.id === 'p-alice');
    expect(alice?.role).toBe('Host');
    const bob = state.players.find(p => p.id === 'p-bob');
    expect(bob?.role).toBe('Player'); // unchanged
  });

  it('role-change to Spectator is preserved', () => {
    const event: Extract<SessionEvent, { type: 'session:role-change' }> = {
      type: 'session:role-change',
      sessionId: 'sess-1',
      participantId: 'p-bob',
      oldRole: 'Player',
      newRole: 'Spectator',
      assignedBy: 'p-alice',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.players.find(p => p.id === 'p-bob')?.role).toBe('Spectator');
  });
});

// ============================================================================
// session:pause / session:resume
// ============================================================================

describe('composeSessionLiveState — session:pause / session:resume', () => {
  it('pause sets status to Paused', () => {
    const event: Extract<SessionEvent, { type: 'session:pause' }> = {
      type: 'session:pause',
      sessionId: 'sess-1',
      pausedBy: 'host-1',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.status).toBe('Paused');
  });

  it('resume sets status to InProgress', () => {
    const paused = { ...BASE_SESSION, status: 'Paused' };
    const event: Extract<SessionEvent, { type: 'session:resume' }> = {
      type: 'session:resume',
      sessionId: 'sess-1',
      resumedBy: 'host-1',
      timestamp: TS,
    };
    const state = composeSessionLiveState(paused, [event]);
    expect(state.status).toBe('InProgress');
  });

  it('pause then resume restores InProgress', () => {
    const pause: Extract<SessionEvent, { type: 'session:pause' }> = {
      type: 'session:pause',
      sessionId: 'sess-1',
      pausedBy: 'host-1',
      timestamp: TS,
    };
    const resume: Extract<SessionEvent, { type: 'session:resume' }> = {
      type: 'session:resume',
      sessionId: 'sess-1',
      resumedBy: 'host-1',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [pause, resume]);
    expect(state.status).toBe('InProgress');
  });
});

// ============================================================================
// session:endgame
// ============================================================================

describe('composeSessionLiveState — session:endgame', () => {
  it('sets status to Paused (final state)', () => {
    const event: Extract<SessionEvent, { type: 'session:endgame' }> = {
      type: 'session:endgame',
      sessionId: 'sess-1',
      finalScores: [],
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.status).toBe('Paused');
  });

  it('applies final scores when provided', () => {
    const event: Extract<SessionEvent, { type: 'session:endgame' }> = {
      type: 'session:endgame',
      sessionId: 'sess-1',
      finalScores: [
        { participantId: 'p-alice', score: 150, winner: true },
        { participantId: 'p-bob', score: 90, winner: false },
      ],
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.players.find(p => p.id === 'p-alice')?.score).toBe(150);
    expect(state.players.find(p => p.id === 'p-bob')?.score).toBe(90);
  });

  it('does not crash on empty finalScores', () => {
    const event: Extract<SessionEvent, { type: 'session:endgame' }> = {
      type: 'session:endgame',
      sessionId: 'sess-1',
      finalScores: [],
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.players).toHaveLength(2);
    expect(state.status).toBe('Paused');
  });
});

// ============================================================================
// session:chat
// ============================================================================

describe('composeSessionLiveState — session:chat', () => {
  it('appends chat entry to actionLog with type=chat', () => {
    const event: Extract<SessionEvent, { type: 'session:chat' }> = {
      type: 'session:chat',
      sessionId: 'sess-1',
      messageId: 'msg-1',
      senderId: 'p-alice',
      content: 'Hello!',
      visibility: 'shared',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.actionLog).toHaveLength(1);
    expect(state.actionLog[0].type).toBe('chat');
    expect(state.actionLog[0].content).toBe('Hello!');
    expect(state.actionLog[0].id).toBe('msg-1');
  });

  it('two chat messages → actionLog length 2', () => {
    const makeChat = (
      id: string,
      content: string
    ): Extract<SessionEvent, { type: 'session:chat' }> => ({
      type: 'session:chat',
      sessionId: 'sess-1',
      messageId: id,
      senderId: 'p-alice',
      content,
      visibility: 'shared',
      timestamp: TS,
    });
    const state = composeSessionLiveState(BASE_SESSION, [
      makeChat('m1', 'Hi'),
      makeChat('m2', 'Bye'),
    ]);
    expect(state.actionLog).toHaveLength(2);
  });
});

// ============================================================================
// session:tool-execution
// ============================================================================

describe('composeSessionLiveState — session:tool-execution', () => {
  it('appends tool entry to actionLog with type=tool', () => {
    const event: Extract<SessionEvent, { type: 'session:tool-execution' }> = {
      type: 'session:tool-execution',
      sessionId: 'sess-1',
      tool: 'dice',
      outcome: 7,
      executedBy: 'p-alice',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.actionLog).toHaveLength(1);
    expect(state.actionLog[0].type).toBe('tool');
    expect(state.actionLog[0].content).toBe('7');
  });

  it('serializes complex outcome to JSON string', () => {
    const event: Extract<SessionEvent, { type: 'session:tool-execution' }> = {
      type: 'session:tool-execution',
      sessionId: 'sess-1',
      tool: 'dice',
      outcome: { rolls: [3, 4], total: 7 },
      executedBy: 'p-alice',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.actionLog[0].content).toBe(JSON.stringify({ rolls: [3, 4], total: 7 }));
  });
});

// ============================================================================
// session:diary
// ============================================================================

describe('composeSessionLiveState — session:diary', () => {
  it('appends diary entry to actionLog with type=event', () => {
    const event: Extract<SessionEvent, { type: 'session:diary' }> = {
      type: 'session:diary',
      sessionId: 'sess-1',
      entryId: 'note-1',
      authorId: 'p-alice',
      content: 'A diary note',
      timestamp: TS,
    };
    const state = composeSessionLiveState(BASE_SESSION, [event]);
    expect(state.actionLog).toHaveLength(1);
    expect(state.actionLog[0].type).toBe('event');
    expect(state.actionLog[0].content).toBe('A diary note');
    expect(state.actionLog[0].id).toBe('note-1');
  });
});

// ============================================================================
// heartbeat
// ============================================================================

describe('composeSessionLiveState — heartbeat', () => {
  it('is a complete no-op (state unchanged)', () => {
    const event: Extract<SessionEvent, { type: 'heartbeat' }> = {
      type: 'heartbeat',
      timestamp: TS,
    };
    const stateBefore = composeSessionLiveState(BASE_SESSION, []);
    const stateAfter = composeSessionLiveState(BASE_SESSION, [event]);
    expect(stateAfter.status).toBe(stateBefore.status);
    expect(stateAfter.players).toHaveLength(stateBefore.players.length);
    expect(stateAfter.actionLog).toHaveLength(0);
    expect(stateAfter.currentTurn).toBe(stateBefore.currentTurn);
  });
});

// ============================================================================
// Multi-event sequences
// ============================================================================

describe('composeSessionLiveState — multi-event sequences', () => {
  it('applies multiple events in order', () => {
    const events: SessionEvent[] = [
      makeScoreEvent('p-alice', 20),
      makeScoreEvent('p-bob', 15),
      {
        type: 'session:chat',
        sessionId: 'sess-1',
        messageId: 'm1',
        senderId: 'p-alice',
        content: 'hi',
        visibility: 'shared',
        timestamp: TS,
      },
    ];
    const state = composeSessionLiveState(BASE_SESSION, events);
    expect(state.players.find(p => p.id === 'p-alice')?.score).toBe(20);
    expect(state.players.find(p => p.id === 'p-bob')?.score).toBe(15);
    expect(state.actionLog).toHaveLength(1);
  });

  it('applies events in provided order (reducer assumes pre-sorted input)', () => {
    // Score update 1 → 2: final score should be 2
    const state = composeSessionLiveState(BASE_SESSION, [
      makeScoreEvent('p-alice', 1),
      makeScoreEvent('p-alice', 2),
    ]);
    expect(state.players.find(p => p.id === 'p-alice')?.score).toBe(2);
  });
});
