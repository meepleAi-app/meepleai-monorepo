import { describe, expect, it } from 'vitest';

import { nightSittingLeftOpen } from '../night-sitting-left-open';

const CREATOR = 'aaaa1111-1111-4111-8111-111111111111';
const NIGHT = 'bbbb2222-2222-4222-8222-222222222222';

function session(over: Partial<Parameters<typeof nightSittingLeftOpen>[0] & object> = {}) {
  return {
    status: 'Completed',
    createdByUserId: CREATOR,
    gameNightId: NIGHT,
    nightSittingStillOpen: true,
    ...over,
  };
}

// #4780: the follow-up of «Termina partita» did not reach the night — the creator is told, and gets the link.
describe('nightSittingLeftOpen', () => {
  it('names the night to the creator of a completed game whose sitting the night still holds open', () => {
    expect(nightSittingLeftOpen(session(), CREATOR)).toBe(NIGHT);
  });

  it.each([
    ['another participant', session(), 'cccc3333-3333-4333-8333-333333333333'],
    ['nobody signed in', session(), null],
    ['a game still being played', session({ status: 'InProgress' }), CREATOR],
    ['a night that closed the sitting', session({ nightSittingStillOpen: false }), CREATOR],
    [
      'an older payload without the fields',
      session({ nightSittingStillOpen: undefined, gameNightId: undefined }),
      CREATOR,
    ],
    ['a game with no night', session({ gameNightId: null }), CREATOR],
  ])('says nothing for %s', (_case, s, user) => {
    expect(nightSittingLeftOpen(s, user)).toBeNull();
  });

  it('says nothing without a session', () => {
    expect(nightSittingLeftOpen(null, CREATOR)).toBeNull();
  });
});
