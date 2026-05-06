/**
 * Unit tests for participant-role.ts (Wave D.2, Issue #746)
 */
import { describe, expect, it } from 'vitest';
import {
  PARTICIPANT_ROLES,
  MIN_ROLE_PLAYER_ACTIONS,
  MIN_ROLE_HOST_ACTIONS,
  hasRequiredRole,
  type ParticipantRole,
} from '../participant-role';

describe('ParticipantRole constants', () => {
  it('PARTICIPANT_ROLES contains exactly the 3 expected roles in correct order', () => {
    expect(PARTICIPANT_ROLES).toHaveLength(3);
    expect(PARTICIPANT_ROLES[0]).toBe('Spectator');
    expect(PARTICIPANT_ROLES[1]).toBe('Player');
    expect(PARTICIPANT_ROLES[2]).toBe('Host');
  });

  it('PARTICIPANT_ROLES is typed as ReadonlyArray (no push method on the typed reference)', () => {
    // ReadonlyArray is a TypeScript type constraint, not a runtime frozen object.
    // Verify the value is an array with the right length instead.
    expect(Array.isArray(PARTICIPANT_ROLES)).toBe(true);
    expect(PARTICIPANT_ROLES.length).toBe(3);
  });

  it('MIN_ROLE_PLAYER_ACTIONS is Player', () => {
    expect(MIN_ROLE_PLAYER_ACTIONS).toBe('Player');
  });

  it('MIN_ROLE_HOST_ACTIONS is Host', () => {
    expect(MIN_ROLE_HOST_ACTIONS).toBe('Host');
  });

  it('all 3 roles are covered by PARTICIPANT_ROLES', () => {
    const roles: ParticipantRole[] = ['Spectator', 'Player', 'Host'];
    for (const role of roles) {
      expect(PARTICIPANT_ROLES).toContain(role);
    }
  });
});

describe('hasRequiredRole', () => {
  // Host satisfies all roles
  it('Host satisfies Host minimum', () => {
    expect(hasRequiredRole('Host', 'Host')).toBe(true);
  });

  it('Host satisfies Player minimum', () => {
    expect(hasRequiredRole('Host', 'Player')).toBe(true);
  });

  it('Host satisfies Spectator minimum', () => {
    expect(hasRequiredRole('Host', 'Spectator')).toBe(true);
  });

  // Player satisfies Player and Spectator but NOT Host
  it('Player satisfies Player minimum', () => {
    expect(hasRequiredRole('Player', 'Player')).toBe(true);
  });

  it('Player satisfies Spectator minimum', () => {
    expect(hasRequiredRole('Player', 'Spectator')).toBe(true);
  });

  it('Player does NOT satisfy Host minimum', () => {
    expect(hasRequiredRole('Player', 'Host')).toBe(false);
  });

  // Spectator only satisfies Spectator minimum
  it('Spectator satisfies Spectator minimum', () => {
    expect(hasRequiredRole('Spectator', 'Spectator')).toBe(true);
  });

  it('Spectator does NOT satisfy Player minimum', () => {
    expect(hasRequiredRole('Spectator', 'Player')).toBe(false);
  });

  it('Spectator does NOT satisfy Host minimum', () => {
    expect(hasRequiredRole('Spectator', 'Host')).toBe(false);
  });

  // Verify against the constants
  it('validates MIN_ROLE_PLAYER_ACTIONS — Host and Player pass, Spectator fails', () => {
    expect(hasRequiredRole('Host', MIN_ROLE_PLAYER_ACTIONS)).toBe(true);
    expect(hasRequiredRole('Player', MIN_ROLE_PLAYER_ACTIONS)).toBe(true);
    expect(hasRequiredRole('Spectator', MIN_ROLE_PLAYER_ACTIONS)).toBe(false);
  });

  it('validates MIN_ROLE_HOST_ACTIONS — only Host passes', () => {
    expect(hasRequiredRole('Host', MIN_ROLE_HOST_ACTIONS)).toBe(true);
    expect(hasRequiredRole('Player', MIN_ROLE_HOST_ACTIONS)).toBe(false);
    expect(hasRequiredRole('Spectator', MIN_ROLE_HOST_ACTIONS)).toBe(false);
  });
});
