/**
 * isImmersiveRoute unit tests.
 *
 * Immersive routes replace the global navbar chrome (MobileBottomBar hides
 * itself, DesktopShell drops its bottom-bar padding) with an in-session layout.
 * #3146 Slice 2: the GameNight live hub (/game-nights/[id]/live) joins this set
 * so its own mobile tab-nav is the sole bottom surface (no double bottom-nav
 * with the global 5-tab bar).
 */

import { describe, expect, it } from 'vitest';

import { isImmersiveRoute } from '../immersive-routes';

describe('isImmersiveRoute', () => {
  it('treats the session live view as immersive', () => {
    expect(isImmersiveRoute('/sessions/abc-123/live')).toBe(true);
    expect(isImmersiveRoute('/sessions/abc-123/live/anything')).toBe(true);
  });

  it('treats the library play view as immersive', () => {
    expect(isImmersiveRoute('/library/game-1/play')).toBe(true);
  });

  it('treats the GameNight live hub as immersive (#3146 Slice 2)', () => {
    expect(isImmersiveRoute('/game-nights/night-1/live')).toBe(true);
    expect(isImmersiveRoute('/game-nights/night-1/live/')).toBe(true);
  });

  it('does NOT treat the GameNight detail / summary as immersive', () => {
    expect(isImmersiveRoute('/game-nights/night-1')).toBe(false);
    expect(isImmersiveRoute('/game-nights/night-1/summary')).toBe(false);
  });

  it('does NOT treat unrelated routes as immersive', () => {
    expect(isImmersiveRoute('/library')).toBe(false);
    expect(isImmersiveRoute('/sessions/abc-123')).toBe(false);
    expect(isImmersiveRoute('/')).toBe(false);
  });
});
