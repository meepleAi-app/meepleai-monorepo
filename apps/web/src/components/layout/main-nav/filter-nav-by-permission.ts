import type { MainNavItem } from './main-nav-config';

/**
 * Minimal authentication context for nav filtering. MVP only needs the
 * authenticated bit; future per-item permission filtering will extend this
 * (e.g. `roles?: string[]`, `entitlements?: string[]`).
 */
export interface UserContext {
  authenticated: boolean;
}

/**
 * Returns the main nav items the user is allowed to see.
 *
 *   - MVP semantics: all 8 items when authenticated, empty when not.
 *   - Pure: does not mutate the input array.
 *   - Future: per-item permission/role checks (see `filterNavByRole` in
 *     admin-nav for the analogous pattern with `minRole`).
 */
export function filterNavByPermission(
  items: ReadonlyArray<MainNavItem>,
  ctx: UserContext
): ReadonlyArray<MainNavItem> {
  if (!ctx.authenticated) return [];
  return items;
}
