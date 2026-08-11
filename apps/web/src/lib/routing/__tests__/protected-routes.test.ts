import { describe, expect, it } from 'vitest';

import { isAdminPath, isProtectedPath } from '../protected-routes';

describe('isProtectedPath (Issue #2846)', () => {
  describe('public share routes under a protected prefix stay public', () => {
    // Pre-fix these matched `startsWith('/library')` / `startsWith('/game-nights')`
    // / `startsWith('/play-records')` and were redirected to /login.
    it.each([
      '/library/shared/abc123token', // #DD
      '/game-nights/shared/nighttoken',
      '/play-records/shared/recaptoken',
    ])('%s is NOT protected', pathname => {
      expect(isProtectedPath(pathname)).toBe(false);
    });
  });

  describe('sibling public landing is not swallowed by boundary matching', () => {
    it('/library-public is NOT protected (#G)', () => {
      expect(isProtectedPath('/library-public')).toBe(false);
    });
  });

  describe('public catalog detail is a separate top-level route', () => {
    it.each(['/shared-games', '/shared-games/some-game-id'])('%s is NOT protected', pathname => {
      expect(isProtectedPath(pathname)).toBe(false);
    });
  });

  describe('protected routes remain protected', () => {
    it.each([
      '/library',
      '/library/private',
      '/library/private/add',
      '/games', // hub
      '/games/some-catalog-id', // #EE — intended gating (public catalog is /shared-games/{id})
      '/game-nights',
      '/game-nights/upcoming',
      '/play-records',
      '/play-records/new',
      '/admin',
      '/editor',
      '/dashboard',
    ])('%s IS protected', pathname => {
      expect(isProtectedPath(pathname)).toBe(true);
    });
  });

  describe('unrelated public pages are not protected', () => {
    it.each(['/', '/about', '/login', '/register', '/faq', '/pricing'])(
      '%s is NOT protected',
      pathname => {
        expect(isProtectedPath(pathname)).toBe(false);
      }
    );
  });
});

describe('isAdminPath', () => {
  it.each(['/admin', '/admin/users', '/admin/shared-games/categories'])('%s IS admin', pathname => {
    expect(isAdminPath(pathname)).toBe(true);
  });

  it.each(['/administrator', '/library', '/games', '/'])(
    '%s is NOT admin (boundary-aware)',
    pathname => {
      expect(isAdminPath(pathname)).toBe(false);
    }
  );
});
