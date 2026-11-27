import { isSessionCreator } from './session-creator';

/**
 * #4780 — «Termina partita» completes the live game, then (best effort) the sitting of its game night. When that
 * follow-up fails, the game is over but the night still counts it in progress, and only «Completa partita» on the
 * night closes it. The live page says so to the one who can do it — the creator, who on a night's game is its
 * organizer — and links the night.
 *
 * Returns the id of the night to link, or `null` when there is nothing to say.
 */
export function nightSittingLeftOpen(
  session:
    | {
        readonly status: string;
        readonly createdByUserId: string;
        readonly gameNightId?: string | null;
        readonly nightSittingStillOpen?: boolean;
      }
    | null
    | undefined,
  userId: string | null | undefined
): string | null {
  if (session == null || session.status !== 'Completed') return null;
  if (session.nightSittingStillOpen !== true || !session.gameNightId) return null;
  return isSessionCreator(session, userId) ? session.gameNightId : null;
}
