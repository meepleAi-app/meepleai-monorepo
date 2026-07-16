import { describe, it, expect } from 'vitest';

import { LiveSessionDtoSchema } from '../live-sessions.schemas';

/**
 * #3025 L1 — the LiveSessionDto carries an optional, opaque `gameState` field
 * (game-agnostic JSON; per-game typing is L2). It must be back-compat: absent or
 * null are both valid, and any JSON value passes through unchanged.
 */
const BASE_DTO = {
  id: '11111111-1111-4111-8111-111111111111',
  sessionCode: 'ABC123',
  gameId: null,
  gameName: 'Mage Knight',
  gameSlug: 'mage-knight',
  createdByUserId: '22222222-2222-4222-8222-222222222222',
  status: 'InProgress' as const,
  visibility: 'Private' as const,
  groupId: null,
  createdAt: '2026-01-01T10:00:00Z',
  startedAt: '2026-01-01T10:05:00Z',
  pausedAt: null,
  completedAt: null,
  updatedAt: '2026-01-01T10:10:00Z',
  lastSavedAt: null,
  currentTurnIndex: 0,
  currentTurnPlayerId: null,
  agentMode: 'None' as const,
  notes: null,
  players: [],
  teams: [],
  roundScores: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
};

describe('LiveSessionDtoSchema — gameState (#3025 L1)', () => {
  it('parses a DTO with gameState absent (back-compat)', () => {
    const parsed = LiveSessionDtoSchema.parse(BASE_DTO);
    expect(parsed.gameState).toBeUndefined();
  });

  it('parses a DTO with gameState explicitly null', () => {
    const parsed = LiveSessionDtoSchema.parse({ ...BASE_DTO, gameState: null });
    expect(parsed.gameState).toBeNull();
  });

  it('passes an opaque object through unchanged', () => {
    const gameState = { round: 3, dev: { catan: { longestRoad: 'p1', points: { p1: 7 } } } };
    const parsed = LiveSessionDtoSchema.parse({ ...BASE_DTO, gameState });
    expect(parsed.gameState).toEqual(gameState);
  });

  it('accepts a primitive gameState (opaque at L1)', () => {
    const parsed = LiveSessionDtoSchema.parse({ ...BASE_DTO, gameState: 42 });
    expect(parsed.gameState).toBe(42);
  });
});
