import { describe, expect, it } from 'vitest';

import { matchesRoutePrefix } from './route-matching';

describe('matchesRoutePrefix', () => {
  const routes = ['/library', '/games', '/admin', '/chat'];

  it('matches an exact route', () => {
    expect(matchesRoutePrefix('/library', routes)).toBe(true);
    expect(matchesRoutePrefix('/chat', routes)).toBe(true);
  });

  it('matches a sub-path of a route', () => {
    expect(matchesRoutePrefix('/library/abc-123', routes)).toBe(true);
    expect(matchesRoutePrefix('/games/xyz/faqs', routes)).toBe(true);
  });

  it('does NOT match a sibling path that only shares a prefix (regression: /library-public)', () => {
    // Plain `'/library-public'.startsWith('/library')` returns true and wrongly
    // redirected this public landing to /login. Boundary matching fixes it.
    expect(matchesRoutePrefix('/library-public', routes)).toBe(false);
  });

  it('does NOT match other prefix-collision siblings', () => {
    expect(matchesRoutePrefix('/gamespider', routes)).toBe(false);
    expect(matchesRoutePrefix('/administrator', routes)).toBe(false);
  });

  it('does NOT match an unrelated public path', () => {
    expect(matchesRoutePrefix('/faq', routes)).toBe(false);
    expect(matchesRoutePrefix('/shared-games', routes)).toBe(false);
  });

  it('returns false for an empty routes list', () => {
    expect(matchesRoutePrefix('/library', [])).toBe(false);
  });
});
