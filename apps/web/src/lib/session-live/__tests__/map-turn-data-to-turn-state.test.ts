/**
 * mapTurnDataToTurnState — unit tests. Issue #2483 Task 3.
 *
 * Covers:
 *   - All 7 FE TurnOrderType variants (1:1 BE names, explicit mapping table)
 *   - BE-only "Free" string → FE 'None' mapping
 *   - Unknown string → UnknownBranch-safe fallback { type: 'None' }
 *   - null input → fallback { type: 'None' }
 *   - Per-variant fields (round/totalRounds/activePlayerId/playOrder for RoundRobin;
 *     phases/activePhaseIndex for Sequential/Custom/Simultaneous; etc.)
 *
 * mapScoreDataToPanelData pattern: pure function, no mocks needed.
 */

import { describe, it, expect } from 'vitest';

import { mapTurnDataToTurnState } from '../map-turn-data-to-turn-state';
import type { TurnState } from '../turn-state';

// ─── Shared session data stub ─────────────────────────────────────────────────

const SESSION_DATA = {
  currentTurn: 3,
  totalTurns: 10,
  activePlayerId: 'player-001',
  players: [
    { id: 'player-001', name: 'Alice' },
    { id: 'player-002', name: 'Bob' },
  ],
} as const;

const PLAY_ORDER = ['player-001', 'player-002'];

// ─── RoundRobin ───────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — RoundRobin', () => {
  it('returns RoundRobin state with round/totalRounds/activePlayerId/playOrder', () => {
    const result = mapTurnDataToTurnState('RoundRobin', SESSION_DATA);
    expect(result.type).toBe('RoundRobin');
    if (result.type !== 'RoundRobin') return;
    expect(result.round).toBe(SESSION_DATA.currentTurn);
    expect(result.totalRounds).toBe(SESSION_DATA.totalTurns);
    expect(result.activePlayerId).toBe(SESSION_DATA.activePlayerId);
    expect(result.playOrder).toEqual(PLAY_ORDER);
  });
});

// ─── Sequential ───────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — Sequential', () => {
  it('returns Sequential state with phases + activePhaseIndex', () => {
    const result = mapTurnDataToTurnState('Sequential', SESSION_DATA);
    expect(result.type).toBe('Sequential');
    if (result.type !== 'Sequential') return;
    expect(Array.isArray(result.phases)).toBe(true);
    expect(typeof result.activePhaseIndex).toBe('number');
  });

  it('activePhaseIndex is bounded to phases array length', () => {
    const result = mapTurnDataToTurnState('Sequential', SESSION_DATA);
    if (result.type !== 'Sequential') return;
    expect(result.activePhaseIndex).toBeGreaterThanOrEqual(0);
    // phases length must be >= 1 so that activePhaseIndex is valid
    expect(result.phases.length).toBeGreaterThanOrEqual(1);
    expect(result.activePhaseIndex).toBeLessThan(result.phases.length);
  });
});

// ─── Simultaneous ─────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — Simultaneous', () => {
  it('returns Simultaneous state', () => {
    const result = mapTurnDataToTurnState('Simultaneous', SESSION_DATA);
    expect(result.type).toBe('Simultaneous');
  });
});

// ─── Realtime ────────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — Realtime', () => {
  it('returns { type: "Realtime" }', () => {
    const result = mapTurnDataToTurnState('Realtime', SESSION_DATA);
    expect(result).toEqual({ type: 'Realtime' });
  });
});

// ─── None ────────────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — None', () => {
  it('returns { type: "None" }', () => {
    const result = mapTurnDataToTurnState('None', SESSION_DATA);
    expect(result).toEqual({ type: 'None' });
  });
});

// ─── Custom ───────────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — Custom', () => {
  it('returns Custom state with phases + activePhaseIndex', () => {
    const result = mapTurnDataToTurnState('Custom', SESSION_DATA);
    expect(result.type).toBe('Custom');
    if (result.type !== 'Custom') return;
    expect(Array.isArray(result.phases)).toBe(true);
    expect(typeof result.activePhaseIndex).toBe('number');
  });
});

// ─── FirstPlayerToken ─────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — FirstPlayerToken', () => {
  it('returns FirstPlayerToken state with round/totalRounds/tokenHolderId/playOrder', () => {
    const result = mapTurnDataToTurnState('FirstPlayerToken', SESSION_DATA);
    expect(result.type).toBe('FirstPlayerToken');
    if (result.type !== 'FirstPlayerToken') return;
    expect(result.round).toBe(SESSION_DATA.currentTurn);
    expect(result.totalRounds).toBe(SESSION_DATA.totalTurns);
    expect(result.tokenHolderId).toBe(SESSION_DATA.activePlayerId);
    expect(result.playOrder).toEqual(PLAY_ORDER);
  });
});

// ─── BE "Free" → FE "None" mapping ───────────────────────────────────────────

describe('mapTurnDataToTurnState — BE "Free" string', () => {
  it('maps BE "Free" to FE None variant', () => {
    const result = mapTurnDataToTurnState('Free', SESSION_DATA);
    expect(result.type).toBe('None');
  });
});

// ─── Unknown string → safe None fallback ─────────────────────────────────────

describe('mapTurnDataToTurnState — unknown string', () => {
  it('maps an unknown BE string to safe None fallback', () => {
    const result = mapTurnDataToTurnState('TotallyInventedType', SESSION_DATA);
    expect(result.type).toBe('None');
  });

  it('safe None fallback passes as valid TurnState type', () => {
    const result: TurnState = mapTurnDataToTurnState('???', SESSION_DATA);
    expect(result.type).toBe('None');
  });
});

// ─── null input ───────────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — null', () => {
  it('maps null to safe None fallback', () => {
    const result = mapTurnDataToTurnState(null, SESSION_DATA);
    expect(result.type).toBe('None');
  });

  it('null with no session data also returns None fallback', () => {
    const result = mapTurnDataToTurnState(null, null);
    expect(result.type).toBe('None');
  });
});

// ─── null session data ────────────────────────────────────────────────────────

describe('mapTurnDataToTurnState — null session data', () => {
  it('handles null sessionData gracefully for RoundRobin', () => {
    const result = mapTurnDataToTurnState('RoundRobin', null);
    expect(result.type).toBe('RoundRobin');
    if (result.type !== 'RoundRobin') return;
    expect(result.round).toBe(0);
    expect(result.totalRounds).toBe(0);
    expect(result.activePlayerId).toBe('');
    expect(result.playOrder).toEqual([]);
  });

  it('handles null sessionData gracefully for Sequential', () => {
    const result = mapTurnDataToTurnState('Sequential', null);
    expect(result.type).toBe('Sequential');
  });
});
