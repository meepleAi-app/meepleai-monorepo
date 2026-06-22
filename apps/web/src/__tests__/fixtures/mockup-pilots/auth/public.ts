/**
 * Public Landing page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `public` cluster Storybook story con axis matrix:
 *   page:  'landing' | 'pricing' | 'about' | 'contact'
 *   state: 'default' | 'mobile-drawer-open'
 *
 * NOTE: LandingPage is a Server Component that calls getServerUser() and
 * redirects to /library if authenticated. Storybook Webpack cannot render
 * server components directly, so the canonical hero in the mockup matrix is
 * `WelcomeHero` (client primitive composed by LandingPage).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

export type PublicState = 'default' | 'mobile-drawer-open';

export const MOCK_AUTH_PUBLIC_NO_USER = {
  error: 'Unauthorized',
};

export function mswForPublicState(_state: PublicState) {
  return [
    http.get('*/api/v1/auth/me', () =>
      HttpResponse.json(MOCK_AUTH_PUBLIC_NO_USER, { status: 401 })
    ),
    http.get('*/api/v1/auth/session/status', () =>
      HttpResponse.json({ authenticated: false }, { status: 200 })
    ),
  ];
}
