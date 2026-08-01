/**
 * Frontend mirror of backend ParticipantRole enum.
 * Source: apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ParticipantRole.cs
 *
 * Backend values: Spectator=0, Player=1, Host=2
 * Frontend uses string literals for readability; mapping occurs at API layer.
 *
 * Wave D.2 Foundation sub-PR — Issue #746
 */

export type ParticipantRole = 'Spectator' | 'Player' | 'Host';

export const PARTICIPANT_ROLES: ReadonlyArray<ParticipantRole> = ['Spectator', 'Player', 'Host'];

/**
 * Server-enforced minimum role per command category (Phase 0.5 contract §4.4).
 *
 * Player-level: score updates, dice rolls, card draws, timer usage.
 * Host-level: pause, resume, kick participant, endgame.
 *
 * 403 Forbidden is returned when the current viewer's role is below the minimum.
 * Optimistic UI must handle 403 rollback (Interactions sub-PR concern).
 */
export const MIN_ROLE_PLAYER_ACTIONS: ParticipantRole = 'Player'; // score, dice, card, timer
export const MIN_ROLE_HOST_ACTIONS: ParticipantRole = 'Host'; // pause, resume, kick, endgame

/**
 * Returns true when the given role satisfies the minimum required role.
 * Role hierarchy: Host > Player > Spectator.
 */
export function hasRequiredRole(viewerRole: ParticipantRole, minRole: ParticipantRole): boolean {
  const hierarchy: Record<ParticipantRole, number> = {
    Spectator: 0,
    Player: 1,
    Host: 2,
  };
  return hierarchy[viewerRole] >= hierarchy[minRole];
}

// ============================================================================
// Numeric wire contracts (issue #3392)
// ============================================================================
//
// `SseJsonOptions` on the backend
// (apps/api/src/Api/Infrastructure/Serialization/SseJsonOptions.cs) disables
// JsonStringEnumConverter, so backend role enums serialize as INTEGERS on the wire.
// TWO different backend enums share overlapping member names but map to OPPOSITE numbers:
//
//   SessionTracking.ParticipantRole        → Spectator=0, Player=1, Host=2
//       (privilege-ascending, LOAD-BEARING: compared ordinally in ValidatePlayerRoleBehavior)
//   GameManagement.SessionParticipantRole  → Host=0, Player=1, Spectator=2
//       (equality-only; formerly the homonymous `ParticipantRole`, renamed in #3392)
//
// The two constants below pin each numeric contract as code so a raw wire integer is never
// decoded against the wrong enum. Conflating the two schemes (e.g. reusing one map for the
// other backend) will fail participant-role.test.ts.

/**
 * SessionTracking.ParticipantRole numeric contract (Spectator=0, Player=1, Host=2).
 * Mirror of apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ParticipantRole.cs.
 */
export const SESSION_TRACKING_ROLE_BY_NUMBER = {
  0: 'Spectator',
  1: 'Player',
  2: 'Host',
} as const satisfies Record<0 | 1 | 2, ParticipantRole>;

/**
 * GameManagement.SessionParticipantRole numeric contract (Host=0, Player=1, Spectator=2).
 * Mirror of apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/SessionParticipantRole.cs.
 */
export const GAME_MANAGEMENT_ROLE_BY_NUMBER = {
  0: 'Host',
  1: 'Player',
  2: 'Spectator',
} as const satisfies Record<0 | 1 | 2, ParticipantRole>;

function decodeRole(contract: Record<0 | 1 | 2, ParticipantRole>, value: number): ParticipantRole {
  // Narrow to the known indices; anything else is an out-of-range wire value → safe default.
  return value === 0 || value === 1 || value === 2 ? contract[value] : 'Player';
}

/**
 * Decode a numeric wire value using the SessionTracking contract (Spectator=0, Player=1, Host=2).
 * Out-of-range values fall back to 'Player'.
 */
export function decodeSessionTrackingRole(value: number): ParticipantRole {
  return decodeRole(SESSION_TRACKING_ROLE_BY_NUMBER, value);
}

/**
 * Decode a numeric wire value using the GameManagement contract (Host=0, Player=1, Spectator=2).
 * Out-of-range values fall back to 'Player'.
 */
export function decodeGameManagementRole(value: number): ParticipantRole {
  return decodeRole(GAME_MANAGEMENT_ROLE_BY_NUMBER, value);
}
