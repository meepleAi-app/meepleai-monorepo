/**
 * Unit tests for session-live-visual-test-fixture.ts (Wave D.2, Issue #746)
 *
 * Covers:
 *   - Fixture shape validation: deterministic UUIDs, role distribution, player count
 *   - Fixture variants: Spectator/Host/Paused override correctly
 *   - parseStateOverride: gated/ungated × valid/invalid
 *   - IS_VISUAL_TEST_BUILD and STATE_OVERRIDE_ENABLED constants
 */
import { describe, expect, it } from 'vitest';
import {
  IS_VISUAL_TEST_BUILD,
  STATE_OVERRIDE_ENABLED,
  VISUAL_TEST_FIXTURE_SESSION,
  VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR,
  VISUAL_TEST_FIXTURE_SESSION_AS_HOST,
  VISUAL_TEST_FIXTURE_SESSION_PAUSED,
  parseStateOverride,
  type LiveSessionFixture,
  type LiveSessionFixturePlayer,
} from '../session-live-visual-test-fixture';

// ---------------------------------------------------------------------------
// IS_VISUAL_TEST_BUILD and STATE_OVERRIDE_ENABLED
// ---------------------------------------------------------------------------

describe('IS_VISUAL_TEST_BUILD', () => {
  it('is false in test environment (NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED is not set to 1)', () => {
    // In vitest, NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED is not set to '1'
    expect(IS_VISUAL_TEST_BUILD).toBe(false);
  });
});

describe('STATE_OVERRIDE_ENABLED', () => {
  it('is true in test environment (NODE_ENV !== production)', () => {
    // vitest sets NODE_ENV to 'test', so this should be true
    expect(STATE_OVERRIDE_ENABLED).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Primary fixture shape
// ---------------------------------------------------------------------------

describe('VISUAL_TEST_FIXTURE_SESSION', () => {
  it('has a deterministic Wave D.2 sentinel UUID', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION.id).toBe('00000000-0000-4000-8000-000000000d20');
  });

  it('has status InProgress', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION.status).toBe('InProgress');
  });

  it('has viewerRole Player (default)', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION.viewerRole).toBe('Player');
  });

  it('has exactly 5 players', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION.players).toHaveLength(5);
  });

  it('has exactly 5 action log entries', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION.actionLog).toHaveLength(5);
  });

  it('players cover all 3 roles (1 Host, 3 Player, 1 Spectator)', () => {
    const roles = VISUAL_TEST_FIXTURE_SESSION.players.map(p => p.role);
    const hostCount = roles.filter(r => r === 'Host').length;
    const playerCount = roles.filter(r => r === 'Player').length;
    const spectatorCount = roles.filter(r => r === 'Spectator').length;
    expect(hostCount).toBe(1);
    expect(playerCount).toBe(3);
    expect(spectatorCount).toBe(1);
  });

  it('viewerId matches a player in the players array', () => {
    const playerIds = VISUAL_TEST_FIXTURE_SESSION.players.map(p => p.id);
    expect(playerIds).toContain(VISUAL_TEST_FIXTURE_SESSION.viewerId);
  });

  it('activePlayerId matches a player in the players array', () => {
    const playerIds = VISUAL_TEST_FIXTURE_SESSION.players.map(p => p.id);
    expect(playerIds).toContain(VISUAL_TEST_FIXTURE_SESSION.activePlayerId);
  });

  it('all players have deterministic UUIDs', () => {
    for (const player of VISUAL_TEST_FIXTURE_SESSION.players) {
      expect(player.id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
    }
  });

  it('action log covers the 5 expected types', () => {
    const types = VISUAL_TEST_FIXTURE_SESSION.actionLog.map(e => e.type);
    expect(types).toContain('score');
    expect(types).toContain('tool');
    expect(types).toContain('chat');
    expect(types).toContain('agent');
    expect(types).toContain('event');
  });

  it('currentTurn and totalTurns are positive integers with currentTurn <= totalTurns', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION.currentTurn).toBeGreaterThan(0);
    expect(VISUAL_TEST_FIXTURE_SESSION.totalTurns).toBeGreaterThan(0);
    expect(VISUAL_TEST_FIXTURE_SESSION.currentTurn).toBeLessThanOrEqual(
      VISUAL_TEST_FIXTURE_SESSION.totalTurns
    );
  });

  it('at least one player is online', () => {
    const onlinePlayers = VISUAL_TEST_FIXTURE_SESSION.players.filter(p => p.isOnline);
    expect(onlinePlayers.length).toBeGreaterThan(0);
  });

  it('spectator has score 0', () => {
    const spectator = VISUAL_TEST_FIXTURE_SESSION.players.find(p => p.role === 'Spectator');
    expect(spectator?.score).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// Role variant fixtures
// ---------------------------------------------------------------------------

describe('VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR', () => {
  it('has viewerRole Spectator', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR.viewerRole).toBe('Spectator');
  });

  it('viewerId points to the Spectator player', () => {
    const spectatorPlayer = VISUAL_TEST_FIXTURE_SESSION.players.find(p => p.role === 'Spectator');
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR.viewerId).toBe(spectatorPlayer?.id);
  });

  it('shares the same session id as primary fixture', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR.id).toBe(VISUAL_TEST_FIXTURE_SESSION.id);
  });

  it('has the same players array as primary fixture', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR.players).toBe(
      VISUAL_TEST_FIXTURE_SESSION.players
    );
  });
});

describe('VISUAL_TEST_FIXTURE_SESSION_AS_HOST', () => {
  it('has viewerRole Host', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_HOST.viewerRole).toBe('Host');
  });

  it('viewerId points to a Host player', () => {
    const hostPlayer = VISUAL_TEST_FIXTURE_SESSION.players.find(p => p.role === 'Host');
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_HOST.viewerId).toBe(hostPlayer?.id);
  });

  it('shares the same session id as primary fixture', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_AS_HOST.id).toBe(VISUAL_TEST_FIXTURE_SESSION.id);
  });
});

describe('VISUAL_TEST_FIXTURE_SESSION_PAUSED', () => {
  it('has status Paused', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_PAUSED.status).toBe('Paused');
  });

  it('has the same viewerRole as primary fixture (Player)', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_PAUSED.viewerRole).toBe(
      VISUAL_TEST_FIXTURE_SESSION.viewerRole
    );
  });

  it('shares the same session id as primary fixture', () => {
    expect(VISUAL_TEST_FIXTURE_SESSION_PAUSED.id).toBe(VISUAL_TEST_FIXTURE_SESSION.id);
  });
});

// ---------------------------------------------------------------------------
// parseStateOverride (on fixture module directly)
// ---------------------------------------------------------------------------

describe('parseStateOverride', () => {
  it('returns loading for ?state=loading (STATE_OVERRIDE_ENABLED=true in test)', () => {
    expect(parseStateOverride(new URLSearchParams('state=loading'))).toBe('loading');
  });

  it('returns not-found for ?state=not-found', () => {
    expect(parseStateOverride(new URLSearchParams('state=not-found'))).toBe('not-found');
  });

  it('returns null for ?state=error (intentionally excluded)', () => {
    expect(parseStateOverride(new URLSearchParams('state=error'))).toBeNull();
  });

  it('returns null for absent state param', () => {
    expect(parseStateOverride(new URLSearchParams(''))).toBeNull();
  });

  it('returns null for unknown override value', () => {
    expect(parseStateOverride(new URLSearchParams('state=arbitrary-value'))).toBeNull();
  });
});
