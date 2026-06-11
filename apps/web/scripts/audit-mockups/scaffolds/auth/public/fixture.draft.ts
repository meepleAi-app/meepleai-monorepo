/**
 * Public Landing page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `public` cluster Storybook story con axis matrix:
 *   page:  'landing' | 'pricing' | 'about' | 'contact'
 *   state: 'default' | 'mobile-drawer-open'
 *
 * Stage axis discovery: grep `currentPage|page === |navigate` in public.jsx
 * (lines 7-58: PublicNav routing).
 *
 * Cross-references handlers in:
 *   apps/web/src/__tests__/mocks/handlers/auth.handlers.ts (anonymous user)
 *
 * Co-located target path:
 *   apps/web/src/__tests__/fixtures/mockup-pilots/auth/public.ts
 *
 * NOTE: LandingPage is a Server Component that calls `getServerUser()`. In
 * Storybook we route this through MSW handlers + a header decorator (see
 * page.stories.tsx already in place).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

export type PublicState = 'default' | 'mobile-drawer-open';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── No data fixtures needed (landing is static marketing) ─────────────────
// LandingPage composes WelcomeHero + HowItWorksSteps + RulesQuickDemo +
// SocialProofBar + WelcomeCTA — all of which are SSR'd from constants.
// MSW handlers only need to gate the `/api/v1/auth/me` anonymous response
// so the server `redirect('/library')` is NOT triggered.

// ── Anonymous auth response (forces "no user" → no redirect) ──────────────

export const MOCK_AUTH_PUBLIC_NO_USER = {
  error: 'Unauthorized',
};

// ── MSW handler switcher (state-driven scenarios) ─────────────────────────

export function mswForState(_state: PublicState) {
  // All states return the same MSW config: anonymous user.
  // Visual variants are CSS-only (responsive breakpoint via Storybook
  // viewport addon).
  return [
    http.get(`${API_BASE}/api/v1/auth/me`, () =>
      HttpResponse.json(MOCK_AUTH_PUBLIC_NO_USER, { status: 401 })
    ),
    // Stub `/api/v1/auth/session/status` to also return anonymous, since
    // some client-side hooks may probe it (e.g. PublicHeader auth-aware
    // CTA "Accedi" vs "Dashboard").
    http.get(`${API_BASE}/api/v1/auth/session/status`, () =>
      HttpResponse.json({ authenticated: false }, { status: 200 })
    ),
  ];
}
