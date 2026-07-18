import { describe, it, expect } from 'vitest';

import { isImmersiveRoute } from './immersive-routes';

describe('isImmersiveRoute', () => {
  it('matches the session-live route (existing)', () => {
    expect(isImmersiveRoute('/sessions/abc/live')).toBe(true);
    expect(isImmersiveRoute('/sessions/abc/live/score')).toBe(true);
  });

  it('matches the library play route (existing)', () => {
    expect(isImmersiveRoute('/library/abc/play')).toBe(true);
  });

  it('matches the game-night live route (new)', () => {
    expect(isImmersiveRoute('/game-nights/abc/live')).toBe(true);
    expect(isImmersiveRoute('/game-nights/abc/live/anything')).toBe(true);
  });

  it('does not match non-immersive routes', () => {
    expect(isImmersiveRoute('/game-nights/abc')).toBe(false);
    expect(isImmersiveRoute('/game-nights/abc/summary')).toBe(false);
    expect(isImmersiveRoute('/sessions/abc')).toBe(false);
  });
});
