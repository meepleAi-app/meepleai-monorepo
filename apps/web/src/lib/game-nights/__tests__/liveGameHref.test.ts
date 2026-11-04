/**
 * liveGameHref — #4752 (the night → live game bridge): where a game of a night is played.
 */

import { describe, it, expect } from 'vitest';

import { liveGameHref, startedGameHref } from '../liveGameHref';

const LIVE = 'bbbb1111-1111-4111-8111-111111111111';

describe('liveGameHref', () => {
  it('is the live page of the live game', () => {
    expect(liveGameHref(LIVE)).toBe(`/sessions/${LIVE}/live`);
  });
});

describe('startedGameHref', () => {
  it('goes straight to the live game the start created', () => {
    expect(startedGameHref('night-1', { liveSessionId: LIVE })).toBe(`/sessions/${LIVE}/live`);
  });

  it.each([undefined, null])(
    'falls back to the night page when the backend did not say (liveSessionId %s)',
    liveSessionId => {
      expect(startedGameHref('night-1', { liveSessionId })).toBe('/game-nights/night-1/live');
    }
  );
});
