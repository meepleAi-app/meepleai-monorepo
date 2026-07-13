/**
 * Boundary-aware route prefix matching.
 *
 * A pathname matches a route only when it equals the route exactly or starts
 * with `route + '/'`. This prevents prefix-collision bugs where an unrelated
 * public path is caught by a protected prefix — e.g. `/library-public` being
 * matched by the protected route `/library`, since plain
 * `'/library-public'.startsWith('/library')` returns `true`.
 *
 * Used by the auth proxy to decide whether a request path is protected /
 * admin-only, and to validate the post-login `from` redirect target.
 */
export function matchesRoutePrefix(pathname: string, routes: readonly string[]): boolean {
  return routes.some(route => pathname === route || pathname.startsWith(`${route}/`));
}
