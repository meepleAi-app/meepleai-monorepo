/**
 * Boundary guard for the two backend role enums' numeric wire contracts (issue #3392).
 *
 * Backend `SseJsonOptions` disables JsonStringEnumConverter, so role enums cross the wire as
 * INTEGERS. Two different backend enums share overlapping member names but map to OPPOSITE
 * numbers:
 *
 *   SessionTracking.ParticipantRole        → Spectator=0, Player=1, Host=2
 *   GameManagement.SessionParticipantRole  → Host=0,      Player=1, Spectator=2
 *
 * These tests pin each contract and assert the two are NOT conflated. Any future edit that
 * makes the two numbering schemes agree at index 0 or 2 (the real conflation vector) fails here.
 */
import { describe, expect, it } from 'vitest';

import {
  SESSION_TRACKING_ROLE_BY_NUMBER,
  GAME_MANAGEMENT_ROLE_BY_NUMBER,
  decodeSessionTrackingRole,
  decodeGameManagementRole,
} from '../participant-role';

describe('SessionTracking numeric role contract (Spectator=0, Player=1, Host=2)', () => {
  it('maps each integer to the SessionTracking role', () => {
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[0]).toBe('Spectator');
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[1]).toBe('Player');
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[2]).toBe('Host');
  });

  it('decodeSessionTrackingRole resolves each integer correctly', () => {
    expect(decodeSessionTrackingRole(0)).toBe('Spectator');
    expect(decodeSessionTrackingRole(1)).toBe('Player');
    expect(decodeSessionTrackingRole(2)).toBe('Host');
  });

  it('decodeSessionTrackingRole falls back to Player for out-of-range values', () => {
    expect(decodeSessionTrackingRole(-1)).toBe('Player');
    expect(decodeSessionTrackingRole(3)).toBe('Player');
    expect(decodeSessionTrackingRole(99)).toBe('Player');
  });
});

describe('GameManagement numeric role contract (Host=0, Player=1, Spectator=2)', () => {
  it('maps each integer to the GameManagement role', () => {
    expect(GAME_MANAGEMENT_ROLE_BY_NUMBER[0]).toBe('Host');
    expect(GAME_MANAGEMENT_ROLE_BY_NUMBER[1]).toBe('Player');
    expect(GAME_MANAGEMENT_ROLE_BY_NUMBER[2]).toBe('Spectator');
  });

  it('decodeGameManagementRole resolves each integer correctly', () => {
    expect(decodeGameManagementRole(0)).toBe('Host');
    expect(decodeGameManagementRole(1)).toBe('Player');
    expect(decodeGameManagementRole(2)).toBe('Spectator');
  });

  it('decodeGameManagementRole falls back to Player for out-of-range values', () => {
    expect(decodeGameManagementRole(-1)).toBe('Player');
    expect(decodeGameManagementRole(3)).toBe('Player');
  });
});

describe('the two contracts must NOT be conflated (issue #3392 core guard)', () => {
  it('index 0 means the OPPOSITE role in each contract (Spectator vs Host)', () => {
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[0]).toBe('Spectator');
    expect(GAME_MANAGEMENT_ROLE_BY_NUMBER[0]).toBe('Host');
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[0]).not.toBe(GAME_MANAGEMENT_ROLE_BY_NUMBER[0]);
  });

  it('index 2 means the OPPOSITE role in each contract (Host vs Spectator)', () => {
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[2]).toBe('Host');
    expect(GAME_MANAGEMENT_ROLE_BY_NUMBER[2]).toBe('Spectator');
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[2]).not.toBe(GAME_MANAGEMENT_ROLE_BY_NUMBER[2]);
  });

  it('index 1 is the only value the two contracts agree on (Player)', () => {
    expect(SESSION_TRACKING_ROLE_BY_NUMBER[1]).toBe('Player');
    expect(GAME_MANAGEMENT_ROLE_BY_NUMBER[1]).toBe('Player');
  });

  it('decoding the same integer against each contract yields inverted roles at 0 and 2', () => {
    expect(decodeSessionTrackingRole(0)).not.toBe(decodeGameManagementRole(0));
    expect(decodeSessionTrackingRole(2)).not.toBe(decodeGameManagementRole(2));
    // Cross-check: SessionTracking Host (2) would be mis-decoded as Spectator under the GM contract.
    expect(decodeGameManagementRole(2)).toBe('Spectator');
    expect(decodeSessionTrackingRole(2)).toBe('Host');
  });
});
