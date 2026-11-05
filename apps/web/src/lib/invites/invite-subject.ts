/**
 * What an invitation invites to — the words after «<host> ti invita a» (#4910).
 *
 * The heading and the accepted summary used the game's name and nothing else, so a night with no game read
 * «ti invita a giocare a » and stopped. The night's own title stands in when there is no game, and the verb changes
 * with it: «giocare a Serata del sabato» is not Italian.
 */

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

export interface InviteSubject {
  /** The words between the host's name and the subject. */
  readonly verb: string;
  /** The game, or the title of the night when there is no game. Empty only if both are blank. */
  readonly name: string;
}

export function inviteSubject(
  invitation: Pick<PublicGameNightInvitation, 'primaryGameName' | 'title'>
): InviteSubject {
  const game = invitation.primaryGameName?.trim() ?? '';
  if (game !== '') return { verb: 'ti invita a giocare a', name: game };

  const title = invitation.title.trim();
  if (title !== '') return { verb: 'ti invita a', name: title };

  return { verb: 'ti invita a una serata di gioco', name: '' };
}
