/**
 * #4745, #4856 — a link that is no longer alive (withdrawn, expired, a cancelled night) comes back from the backend
 * with only its state and the host's name: the id fields are the empty guid, the time is the default date, the
 * title is blank and the counts are zero. The schema must accept that shape (the pages show «not valid any more»
 * from it), and it must carry `nightCancelled` when the NIGHT is what was cancelled.
 */

import { describe, expect, it } from 'vitest';

import { PublicGameNightInvitationSchema } from '../game-night-invitations';

const DEAD_LINK = {
  token: 'abcdefghijklmnopqrstuv',
  status: 'Accepted',
  expiresAt: '2026-11-18T12:00:00+00:00',
  respondedAt: null,
  hostUserId: '00000000-0000-0000-0000-000000000000',
  hostDisplayName: 'Marta',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '00000000-0000-0000-0000-000000000000',
  title: '',
  scheduledAt: '0001-01-01T00:00:00+00:00',
  location: null,
  durationMinutes: null,
  expectedPlayers: 0,
  acceptedSoFar: 0,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
  respondedByName: null,
};

describe('PublicGameNightInvitationSchema — a link that is no longer alive', () => {
  it('accepts the emptied shape the backend sends for it', () => {
    expect(PublicGameNightInvitationSchema.safeParse(DEAD_LINK).success).toBe(true);
  });

  it('carries nightCancelled when the night was cancelled', () => {
    const parsed = PublicGameNightInvitationSchema.parse({ ...DEAD_LINK, nightCancelled: true });

    expect(parsed.nightCancelled).toBe(true);
    expect(parsed.status).toBe('Accepted');
  });

  it('does not require nightCancelled: an older backend does not send it', () => {
    const parsed = PublicGameNightInvitationSchema.parse(DEAD_LINK);

    expect(parsed.nightCancelled).toBeUndefined();
  });

  it('still refuses a payload without the host name', () => {
    const { hostDisplayName: _omitted, ...withoutHost } = DEAD_LINK;

    expect(PublicGameNightInvitationSchema.safeParse(withoutHost).success).toBe(false);
  });
});
