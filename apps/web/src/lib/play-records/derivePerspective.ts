/**
 * derivePerspective — Task 0.1 (Issue #1488 / Epic #1475 Phase D).
 *
 * Pure function that classifies a `PlayRecordDto` from the current user's
 * point of view. Used by `PlayRecordHeroPodium`, `OutcomeBadge`, ranking
 * banners across `/play-records/*` routes.
 *
 * Decision precedence (top-to-bottom):
 *   1. status === 'InProgress' | 'Planned' → pending (EC-6)
 *   2. outcomeType === 'none' OR winnerPlayerIds.length === 0 → cooperative (EC-1, EC-3)
 *   3. currentUserId NOT in players[].userId → spectator (EC-4, EC-5)
 *   4. currentUser is winner AND winnerPlayerIds.length === 1 → won
 *   5. currentUser is winner AND winnerPlayerIds.length > 1 → tie
 *   6. otherwise → lost
 *
 * Discriminated by `kind` so consumers can switch on a single union literal.
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 0 Step 1
 */
import type {
  PlayRecordOutcomeType,
  PlayRecordStatus,
  SessionPlayer,
} from '@/lib/api/schemas/play-records.schemas';

export type PerspectiveKind = 'won' | 'lost' | 'tie' | 'cooperative' | 'spectator' | 'pending';

export interface Perspective {
  /** Discriminator: which UX bucket this record falls into. */
  readonly kind: PerspectiveKind;
  /**
   * The player.id matching the current user (or null when not a player —
   * `spectator` or guest-only). Used to highlight "you" in classifica.
   */
  readonly currentUserPlayerId: string | null;
}

export interface DerivePerspectiveInput {
  /** Authenticated user id; null when unauthenticated/anonymous. */
  readonly currentUserId: string | null;
  /** Players seated for this record. */
  readonly players: ReadonlyArray<Pick<SessionPlayer, 'id' | 'userId' | 'displayName'>>;
  /**
   * Player ids derived BE-side (post-#1663 Phase 1) as winners of this record.
   * Empty array implies cooperative (or in-progress) — never "tied with zero".
   */
  readonly winnerPlayerIds: ReadonlyArray<string>;
  /**
   * Outcome classification: "competitive" when at least one player carries a
   * `wins` dimension; "none" for cooperative / narrative / unscored games.
   * `undefined` during BE rollout — treated like "competitive" but the
   * `winnerPlayerIds.length === 0` short-circuit still routes to cooperative.
   */
  readonly outcomeType: PlayRecordOutcomeType | undefined;
  readonly status: PlayRecordStatus;
}

function findCurrentPlayer(
  players: DerivePerspectiveInput['players'],
  currentUserId: string | null
): { id: string } | null {
  if (currentUserId === null) return null;
  const match = players.find(p => p.userId === currentUserId);
  return match ?? null;
}

export function derivePerspective(input: DerivePerspectiveInput): Perspective {
  const { currentUserId, players, winnerPlayerIds, outcomeType, status } = input;

  // Step 1: EC-6 — InProgress / Planned have no winners yet.
  if (status === 'InProgress' || status === 'Planned') {
    const currentPlayer = findCurrentPlayer(players, currentUserId);
    return { kind: 'pending', currentUserPlayerId: currentPlayer?.id ?? null };
  }

  // Step 2: EC-1, EC-3 — cooperative / unscored.
  if (outcomeType === 'none' || winnerPlayerIds.length === 0) {
    const currentPlayer = findCurrentPlayer(players, currentUserId);
    return { kind: 'cooperative', currentUserPlayerId: currentPlayer?.id ?? null };
  }

  // Step 3: EC-4, EC-5 — current user isn't a seated player.
  const currentPlayer = findCurrentPlayer(players, currentUserId);
  if (currentPlayer === null) {
    return { kind: 'spectator', currentUserPlayerId: null };
  }

  // Step 4-6: competitive matrix.
  const userIsWinner = winnerPlayerIds.includes(currentPlayer.id);
  if (!userIsWinner) {
    return { kind: 'lost', currentUserPlayerId: currentPlayer.id };
  }
  if (winnerPlayerIds.length > 1) {
    return { kind: 'tie', currentUserPlayerId: currentPlayer.id };
  }
  return { kind: 'won', currentUserPlayerId: currentPlayer.id };
}
