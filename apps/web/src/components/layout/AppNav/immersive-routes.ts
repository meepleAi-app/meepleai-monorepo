/**
 * Immersive routes where the global navbar chrome is replaced by an in-session
 * layout. Shared by {@link MobileBottomBar} (to hide itself) and `DesktopShell`
 * (to drop the bottom-bar padding) so the two stay in sync.
 */
const IMMERSIVE_ROUTE_PATTERNS = [/^\/sessions\/live(\/|$)/, /^\/library\/[^/]+\/play(\/|$)/];

export function isImmersiveRoute(pathname: string): boolean {
  return IMMERSIVE_ROUTE_PATTERNS.some(pattern => pattern.test(pathname));
}
