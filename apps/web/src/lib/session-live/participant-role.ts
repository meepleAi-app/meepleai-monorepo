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
