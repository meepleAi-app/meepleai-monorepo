/**
 * Immersive routes where the global navbar chrome is replaced by an in-session
 * layout. Shared by {@link MobileBottomBar} (to hide itself) and `DesktopShell`
 * (to drop the bottom-bar padding) so the two stay in sync.
 */
const IMMERSIVE_ROUTE_PATTERNS = [
  /^\/sessions\/[^/]+\/live(\/|$)/,
  /^\/library\/[^/]+\/play(\/|$)/,
  // #3146 Slice 2: the GameNight live hub renders its own mobile tab-nav
  // (NightLiveHub MobileBody), so it hides the global bottom bar to avoid a
  // double bottom-nav — like the session live view above.
  /^\/game-nights\/[^/]+\/live(\/|$)/,
];

export function isImmersiveRoute(pathname: string): boolean {
  return IMMERSIVE_ROUTE_PATTERNS.some(pattern => pattern.test(pathname));
}
