/**
 * Tests for parseSseEvent (Wave D.2, Issue #750)
 *
 * Validates:
 * - Each known event type returns typed SessionEvent (flat-field Foundation schema)
 * - Unknown event type returns null
 * - Malformed JSON returns null
 * - Missing required fields returns null
 * - v1 carryover field normalization (participantId / displayName / newScore / etc.)
 *
 * Foundation schema (Issue #746): SessionEvent has NO id/payload wrapper.
 * Fields are flat: type, sessionId, participantId, score, etc.
 */

import { describe, expect, it } from 'vitest';

import { parseSseEvent } from '../parse-sse-event';

// ============================================================================
// Helpers
// ============================================================================

const makeJson = (obj: Record<string, unknown>) => JSON.stringify(obj);
const SESSION_ID = 'sess-abc';

// ============================================================================
// Null / invalid cases
// ============================================================================

describe('parseSseEvent — null/invalid cases', () => {
  it('returns null for unknown event type', () => {
    expect(parseSseEvent('unknown:event', makeJson({ score: 1 }), SESSION_ID)).toBeNull();
  });

  it('returns null for empty string event type', () => {
    expect(parseSseEvent('', makeJson({ score: 1 }), SESSION_ID)).toBeNull();
  });

  it('returns null for malformed JSON', () => {
    expect(parseSseEvent('session:score', '{not-json}', SESSION_ID)).toBeNull();
  });

  it('returns null for non-object JSON (array)', () => {
    expect(parseSseEvent('session:score', JSON.stringify([1, 2, 3]), SESSION_ID)).toBeNull();
  });

  it('returns null for non-object JSON (string)', () => {
    expect(parseSseEvent('session:score', JSON.stringify('hello'), SESSION_ID)).toBeNull();
  });

  it('returns null for session:score missing participantId and playerId', () => {
    expect(parseSseEvent('session:score', makeJson({ score: 10 }), SESSION_ID)).toBeNull();
  });

  it('returns null for session:score missing score and newScore', () => {
    expect(
      parseSseEvent('session:score', makeJson({ participantId: 'p1' }), SESSION_ID)
    ).toBeNull();
  });

  it('returns null for session:turn missing turnNumber', () => {
    expect(
      parseSseEvent('session:turn', makeJson({ activePlayerId: 'p1' }), SESSION_ID)
    ).toBeNull();
  });

  it('returns null for session:turn missing activePlayerId', () => {
    expect(parseSseEvent('session:turn', makeJson({ turnNumber: 3 }), SESSION_ID)).toBeNull();
  });

  it('returns null for session:player-join missing participantId/playerId', () => {
    expect(
      parseSseEvent('session:player-join', makeJson({ playerName: 'Alice' }), SESSION_ID)
    ).toBeNull();
  });

  it('returns null for session:player-join missing name', () => {
    expect(
      parseSseEvent('session:player-join', makeJson({ participantId: 'p1' }), SESSION_ID)
    ).toBeNull();
  });
});

// ============================================================================
// session:score
// ============================================================================

describe('parseSseEvent — session:score', () => {
  it('parses session:score with participantId', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ participantId: 'p1', score: 42 }),
      SESSION_ID
    );
    expect(result).not.toBeNull();
    expect(result?.type).toBe('session:score');
    if (result?.type === 'session:score') {
      expect(result.participantId).toBe('p1');
      expect(result.score).toBe(42);
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('normalizes playerId → participantId (v1 carryover)', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ playerId: 'p2', score: 99 }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:score');
    if (result?.type === 'session:score') {
      expect(result.participantId).toBe('p2');
    }
  });

  it('normalizes newScore → score (v1 BE field name)', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ participantId: 'p1', newScore: 77 }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:score');
    if (result?.type === 'session:score') {
      expect(result.score).toBe(77);
    }
  });

  it('sets updatedBy from data or defaults to participantId', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ participantId: 'p1', score: 10 }),
      SESSION_ID
    );
    if (result?.type === 'session:score') {
      expect(result.updatedBy).toBe('p1');
    }
  });

  it('sets updatedBy from explicit field', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ participantId: 'p1', score: 10, updatedBy: 'host-1' }),
      SESSION_ID
    );
    if (result?.type === 'session:score') {
      expect(result.updatedBy).toBe('host-1');
    }
  });
});

// ============================================================================
// session:turn
// ============================================================================

describe('parseSseEvent — session:turn', () => {
  it('parses session:turn', () => {
    const result = parseSseEvent(
      'session:turn',
      makeJson({ turnNumber: 3, activePlayerId: 'p1' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:turn');
    if (result?.type === 'session:turn') {
      expect(result.turnNumber).toBe(3);
      expect(result.activePlayerId).toBe('p1');
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('normalizes currentTurn → turnNumber (v1 carryover)', () => {
    const result = parseSseEvent(
      'session:turn',
      makeJson({ currentTurn: 5, activePlayerId: 'p2' }),
      SESSION_ID
    );
    if (result?.type === 'session:turn') {
      expect(result.turnNumber).toBe(5);
    }
  });
});

// ============================================================================
// session:player-join
// ============================================================================

describe('parseSseEvent — session:player-join', () => {
  it('parses session:player-join with role', () => {
    const result = parseSseEvent(
      'session:player-join',
      makeJson({ participantId: 'p1', playerName: 'Alice', role: 'Host' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:player-join');
    if (result?.type === 'session:player-join') {
      expect(result.participantId).toBe('p1');
      expect(result.playerName).toBe('Alice');
      expect(result.role).toBe('Host');
    }
  });

  it('defaults role to Player when absent (v1 carryover: BE ParticipantAddedEvent has no role)', () => {
    const result = parseSseEvent(
      'session:player-join',
      makeJson({ participantId: 'p2', playerName: 'Bob' }),
      SESSION_ID
    );
    if (result?.type === 'session:player-join') {
      expect(result.role).toBe('Player');
    }
  });

  it('normalizes displayName → playerName (v1 BE field name)', () => {
    const result = parseSseEvent(
      'session:player-join',
      makeJson({ participantId: 'p3', displayName: 'Carol' }),
      SESSION_ID
    );
    if (result?.type === 'session:player-join') {
      expect(result.playerName).toBe('Carol');
    }
  });

  it('normalizes playerId → participantId (v1 carryover)', () => {
    const result = parseSseEvent(
      'session:player-join',
      makeJson({ playerId: 'p4', playerName: 'Dave' }),
      SESSION_ID
    );
    if (result?.type === 'session:player-join') {
      expect(result.participantId).toBe('p4');
    }
  });
});

// ============================================================================
// session:player-leave
// ============================================================================

describe('parseSseEvent — session:player-leave', () => {
  it('parses session:player-leave', () => {
    const result = parseSseEvent(
      'session:player-leave',
      makeJson({ participantId: 'p3' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:player-leave');
    if (result?.type === 'session:player-leave') {
      expect(result.participantId).toBe('p3');
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('normalizes playerId → participantId for player-leave', () => {
    const result = parseSseEvent('session:player-leave', makeJson({ playerId: 'p4' }), SESSION_ID);
    if (result?.type === 'session:player-leave') {
      expect(result.participantId).toBe('p4');
    }
  });

  it('returns null when participantId/playerId missing', () => {
    expect(
      parseSseEvent('session:player-leave', makeJson({ timestamp: 'ts' }), SESSION_ID)
    ).toBeNull();
  });
});

// ============================================================================
// session:role-change
// ============================================================================

describe('parseSseEvent — session:role-change', () => {
  it('parses session:role-change with oldRole and newRole', () => {
    const result = parseSseEvent(
      'session:role-change',
      makeJson({ participantId: 'p1', oldRole: 'Player', newRole: 'Host', assignedBy: 'host-1' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:role-change');
    if (result?.type === 'session:role-change') {
      expect(result.participantId).toBe('p1');
      expect(result.oldRole).toBe('Player');
      expect(result.newRole).toBe('Host');
      expect(result.assignedBy).toBe('host-1');
    }
  });

  it('normalizes previousRole → oldRole (v1 BE field name)', () => {
    const result = parseSseEvent(
      'session:role-change',
      makeJson({ participantId: 'p1', previousRole: 'Spectator', newRole: 'Player' }),
      SESSION_ID
    );
    if (result?.type === 'session:role-change') {
      expect(result.oldRole).toBe('Spectator');
    }
  });

  it('normalizes changedBy → assignedBy (v1 BE field name)', () => {
    const result = parseSseEvent(
      'session:role-change',
      makeJson({ participantId: 'p1', oldRole: 'Player', newRole: 'Host', changedBy: 'admin' }),
      SESSION_ID
    );
    if (result?.type === 'session:role-change') {
      expect(result.assignedBy).toBe('admin');
    }
  });

  it('returns null when participantId missing', () => {
    expect(
      parseSseEvent('session:role-change', makeJson({ newRole: 'Host' }), SESSION_ID)
    ).toBeNull();
  });
});

// ============================================================================
// session:pause / session:resume
// ============================================================================

describe('parseSseEvent — session:pause / session:resume', () => {
  it('parses session:pause', () => {
    const ts = '2026-01-01T10:00:00Z';
    const result = parseSseEvent(
      'session:pause',
      makeJson({ pausedBy: 'host-1', timestamp: ts }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:pause');
    if (result?.type === 'session:pause') {
      expect(result.pausedBy).toBe('host-1');
      expect(result.sessionId).toBe(SESSION_ID);
      expect(result.timestamp).toBe(ts);
    }
  });

  it('session:pause defaults pausedBy to system', () => {
    const result = parseSseEvent(
      'session:pause',
      makeJson({ timestamp: '2026-01-01T10:00:00Z' }),
      SESSION_ID
    );
    if (result?.type === 'session:pause') {
      expect(result.pausedBy).toBe('system');
    }
  });

  it('parses session:pause with optional reason', () => {
    const result = parseSseEvent(
      'session:pause',
      makeJson({ pausedBy: 'host-1', reason: 'Break', timestamp: '2026-01-01T10:00:00Z' }),
      SESSION_ID
    );
    if (result?.type === 'session:pause') {
      expect(result.reason).toBe('Break');
    }
  });

  it('parses session:resume', () => {
    const result = parseSseEvent(
      'session:resume',
      makeJson({ resumedBy: 'host-2', timestamp: '2026-01-01T10:05:00Z' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:resume');
    if (result?.type === 'session:resume') {
      expect(result.resumedBy).toBe('host-2');
    }
  });

  it('session:resume defaults resumedBy to system', () => {
    const result = parseSseEvent(
      'session:resume',
      makeJson({ timestamp: '2026-01-01T10:05:00Z' }),
      SESSION_ID
    );
    if (result?.type === 'session:resume') {
      expect(result.resumedBy).toBe('system');
    }
  });
});

// ============================================================================
// session:endgame
// ============================================================================

describe('parseSseEvent — session:endgame', () => {
  it('parses session:endgame with finalScores array', () => {
    const result = parseSseEvent(
      'session:endgame',
      makeJson({
        finalScores: [
          { participantId: 'p1', score: 150, winner: true },
          { participantId: 'p2', score: 90, winner: false },
        ],
      }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:endgame');
    if (result?.type === 'session:endgame') {
      expect(result.finalScores).toHaveLength(2);
      expect(result.finalScores[0].participantId).toBe('p1');
      expect(result.finalScores[0].score).toBe(150);
      expect(result.finalScores[0].winner).toBe(true);
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('normalizes BE FinalRanks dict → finalScores array (v1 carryover)', () => {
    const result = parseSseEvent(
      'session:endgame',
      makeJson({ finalRanks: { p1: 1, p2: 2 }, winnerId: 'p1' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:endgame');
    if (result?.type === 'session:endgame') {
      expect(result.finalScores).toHaveLength(2);
      const p1 = result.finalScores.find(s => s.participantId === 'p1');
      expect(p1?.winner).toBe(true);
    }
  });

  it('normalizes playerId → participantId in finalScores (v1 carryover)', () => {
    const result = parseSseEvent(
      'session:endgame',
      makeJson({
        finalScores: [{ playerId: 'p1', score: 100, winner: true }],
      }),
      SESSION_ID
    );
    if (result?.type === 'session:endgame') {
      expect(result.finalScores[0].participantId).toBe('p1');
    }
  });

  it('returns endgame with empty finalScores when none provided', () => {
    const result = parseSseEvent(
      'session:endgame',
      makeJson({ timestamp: '2026-01-01T12:00:00Z' }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:endgame');
    if (result?.type === 'session:endgame') {
      expect(result.finalScores).toHaveLength(0);
    }
  });
});

// ============================================================================
// session:chat
// ============================================================================

describe('parseSseEvent — session:chat', () => {
  it('parses session:chat', () => {
    const ts = '2026-01-01T10:30:00Z';
    const result = parseSseEvent(
      'session:chat',
      makeJson({
        messageId: 'msg-1',
        senderId: 'p1',
        content: 'Hello!',
        visibility: 'shared',
        timestamp: ts,
      }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:chat');
    if (result?.type === 'session:chat') {
      expect(result.messageId).toBe('msg-1');
      expect(result.senderId).toBe('p1');
      expect(result.content).toBe('Hello!');
      expect(result.visibility).toBe('shared');
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('defaults visibility to shared', () => {
    const result = parseSseEvent(
      'session:chat',
      makeJson({
        messageId: 'msg-2',
        senderId: 'p1',
        content: 'hi',
        timestamp: '2026-01-01T10:30:00Z',
      }),
      SESSION_ID
    );
    if (result?.type === 'session:chat') {
      expect(result.visibility).toBe('shared');
    }
  });

  it('accepts private visibility', () => {
    const result = parseSseEvent(
      'session:chat',
      makeJson({
        messageId: 'msg-3',
        senderId: 'p1',
        content: 'secret',
        visibility: 'private',
        timestamp: '2026-01-01T10:30:00Z',
      }),
      SESSION_ID
    );
    if (result?.type === 'session:chat') {
      expect(result.visibility).toBe('private');
    }
  });

  it('returns null when messageId missing', () => {
    expect(
      parseSseEvent(
        'session:chat',
        makeJson({ senderId: 'p1', content: 'hi', timestamp: 'ts' }),
        SESSION_ID
      )
    ).toBeNull();
  });
});

// ============================================================================
// session:tool-execution
// ============================================================================

describe('parseSseEvent — session:tool-execution', () => {
  it('parses session:tool-execution with tool and outcome', () => {
    const result = parseSseEvent(
      'session:tool-execution',
      makeJson({
        participantId: 'p1',
        tool: 'dice',
        outcome: 7,
        timestamp: '2026-01-01T10:00:00Z',
      }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:tool-execution');
    if (result?.type === 'session:tool-execution') {
      expect(result.tool).toBe('dice');
      expect(result.outcome).toBe(7);
      expect(result.executedBy).toBe('p1');
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('normalizes toolType → tool (v1 carryover BE field name)', () => {
    const result = parseSseEvent(
      'session:tool-execution',
      makeJson({
        participantId: 'p1',
        toolType: 'timer',
        outcome: 30,
        timestamp: '2026-01-01T10:00:00Z',
      }),
      SESSION_ID
    );
    if (result?.type === 'session:tool-execution') {
      expect(result.tool).toBe('timer');
    }
  });

  it('falls back to dice for unknown tool types (v1 gap: CoinFlipped/WheelSpun)', () => {
    const result = parseSseEvent(
      'session:tool-execution',
      makeJson({
        participantId: 'p1',
        tool: 'coin',
        outcome: 'heads',
        timestamp: '2026-01-01T10:00:00Z',
      }),
      SESSION_ID
    );
    if (result?.type === 'session:tool-execution') {
      expect(result.tool).toBe('dice'); // normalized to closest known type
    }
  });

  it('normalizes result → outcome (v1 carryover)', () => {
    const result = parseSseEvent(
      'session:tool-execution',
      makeJson({
        participantId: 'p1',
        tool: 'dice',
        result: '12',
        timestamp: '2026-01-01T10:00:00Z',
      }),
      SESSION_ID
    );
    if (result?.type === 'session:tool-execution') {
      expect(result.outcome).toBe('12');
    }
  });

  it('returns null when participantId/playerId missing', () => {
    expect(
      parseSseEvent(
        'session:tool-execution',
        makeJson({ tool: 'dice', outcome: 5, timestamp: 'ts' }),
        SESSION_ID
      )
    ).toBeNull();
  });
});

// ============================================================================
// session:diary
// ============================================================================

describe('parseSseEvent — session:diary', () => {
  it('parses session:diary', () => {
    const ts = '2026-01-01T11:00:00Z';
    const result = parseSseEvent(
      'session:diary',
      makeJson({ entryId: 'note-1', authorId: 'p1', content: 'My note', timestamp: ts }),
      SESSION_ID
    );
    expect(result?.type).toBe('session:diary');
    if (result?.type === 'session:diary') {
      expect(result.entryId).toBe('note-1');
      expect(result.authorId).toBe('p1');
      expect(result.content).toBe('My note');
      expect(result.sessionId).toBe(SESSION_ID);
    }
  });

  it('normalizes noteId → entryId (v1 BE NoteSavedEvent field name)', () => {
    const result = parseSseEvent(
      'session:diary',
      makeJson({ noteId: 'note-2', authorId: 'p1', content: 'Note', timestamp: 'ts' }),
      SESSION_ID
    );
    if (result?.type === 'session:diary') {
      expect(result.entryId).toBe('note-2');
    }
  });

  it('normalizes participantId → authorId', () => {
    const result = parseSseEvent(
      'session:diary',
      makeJson({ entryId: 'note-3', participantId: 'p2', content: 'Note', timestamp: 'ts' }),
      SESSION_ID
    );
    if (result?.type === 'session:diary') {
      expect(result.authorId).toBe('p2');
    }
  });

  it('returns null when entryId/noteId missing', () => {
    expect(
      parseSseEvent(
        'session:diary',
        makeJson({ authorId: 'p1', content: 'Note', timestamp: 'ts' }),
        SESSION_ID
      )
    ).toBeNull();
  });

  it('returns null when authorId/participantId missing', () => {
    expect(
      parseSseEvent(
        'session:diary',
        makeJson({ entryId: 'note-4', content: 'Note', timestamp: 'ts' }),
        SESSION_ID
      )
    ).toBeNull();
  });
});

// ============================================================================
// heartbeat
// ============================================================================

describe('parseSseEvent — heartbeat', () => {
  it('parses heartbeat with timestamp', () => {
    const ts = '2026-01-01T00:00:00Z';
    const result = parseSseEvent('heartbeat', makeJson({ timestamp: ts }), SESSION_ID);
    expect(result?.type).toBe('heartbeat');
    if (result?.type === 'heartbeat') {
      expect(result.timestamp).toBe(ts);
    }
  });

  it('heartbeat with empty data still returns event with generated timestamp', () => {
    const result = parseSseEvent('heartbeat', makeJson({}), SESSION_ID);
    expect(result?.type).toBe('heartbeat');
    if (result?.type === 'heartbeat') {
      expect(typeof result.timestamp).toBe('string');
      expect(result.timestamp.length).toBeGreaterThan(0);
    }
  });
});

// ============================================================================
// sessionId resolution
// ============================================================================

describe('parseSseEvent — sessionId resolution', () => {
  it('uses provided sessionId param', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ participantId: 'p1', score: 5 }),
      'session-xyz'
    );
    if (result?.type === 'session:score') {
      expect(result.sessionId).toBe('session-xyz');
    }
  });

  it('falls back to sessionId in data when param is empty', () => {
    const result = parseSseEvent(
      'session:score',
      makeJson({ participantId: 'p1', score: 5, sessionId: 'data-session' }),
      ''
    );
    if (result?.type === 'session:score') {
      expect(result.sessionId).toBe('data-session');
    }
  });
});
