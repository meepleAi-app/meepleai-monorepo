/**
 * Route authorization policy for the auth proxy (Issue #2846).
 *
 * Extracted from `proxy.ts` so the protected/public/admin route sets and the
 * boundary-aware decision can be unit-tested in isolation (the proxy function
 * itself needs a full NextRequest/edge harness).
 */

import { matchesRoutePrefix } from './route-matching';

/**
 * Protected routes that require authentication.
 * Unauthenticated users hitting these are redirected to /login.
 *
 * Kept alphabetised so the diff is easy to read and additions are mechanical.
 * Sources of truth for what belongs here:
 *   - any top-level path under `src/app/(authenticated)/` whose Server Component
 *     should not render for an anonymous visitor
 *   - any path under `src/app/(chat)/`
 *   - `/admin` (its own (dashboard) subtree)
 *
 * `#2118` sweep: added the remaining `(authenticated)` top-levels that were
 * silently missing from this list.
 */
export const PROTECTED_ROUTES = [
  '/admin',
  '/agents',
  '/chat',
  '/dashboard',
  '/discover',
  '/editor',
  '/game-nights',
  '/gamebook',
  '/games',
  '/hub',
  '/knowledge-base',
  '/library',
  '/n8n',
  '/notifications',
  '/onboarding',
  '/pipeline-builder',
  '/play-records',
  '/players',
  '/private-games',
  '/profile',
  '/sessions',
  '/settings',
  '/setup',
  '/toolkit',
  '/toolkits',
  '/upload',
  '/versions',
] as const;

/**
 * Public routes that live UNDER a protected prefix and must stay reachable by
 * guests (Issue #2846). Boundary-aware matching already stops `/library` from
 * swallowing the sibling `/library-public` landing (#G), but the public share
 * pages under the `(public)` route group are genuine sub-paths of a protected
 * prefix, so `matchesRoutePrefix` would still gate them. They are whitelisted
 * here and subtracted from the protected set.
 *
 * These mirror `apps/web/src/app/(public)/**` share routes:
 *   - `/library/shared/{token}`      (#DD) — shared library links
 *   - `/game-nights/shared/{token}`  — shared game-night summaries
 *   - `/play-records/shared/{token}` — shared play-record recaps
 *
 * NOTE: the public catalog detail is `/shared-games/{id}` (its own top-level
 * public route, not under any protected prefix), so `/games/{id}` correctly
 * stays protected (#EE — intended gating, confirmed 2026-07-12).
 */
export const PUBLIC_ROUTES = [
  '/library/shared',
  '/game-nights/shared',
  '/play-records/shared',
] as const;

/**
 * Admin-only routes that require the admin (or superadmin) role.
 */
export const ADMIN_ONLY_ROUTES = ['/admin'] as const;

/**
 * True when `pathname` requires authentication: it is boundary-matched by a
 * protected prefix AND is not one of the whitelisted public share routes.
 */
export function isProtectedPath(pathname: string): boolean {
  return (
    matchesRoutePrefix(pathname, PROTECTED_ROUTES) && !matchesRoutePrefix(pathname, PUBLIC_ROUTES)
  );
}

/**
 * True when `pathname` is admin-only (boundary-aware).
 */
export function isAdminPath(pathname: string): boolean {
  return matchesRoutePrefix(pathname, ADMIN_ONLY_ROUTES);
}
