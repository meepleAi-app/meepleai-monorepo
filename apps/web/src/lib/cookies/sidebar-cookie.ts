/**
 * Sidebar cookie utility for RSC support.
 * Writes sidebar collapsed state to a cookie so Server Components
 * can read it and render the correct layout without flash.
 */

export const SIDEBAR_COOKIE_NAME = 'meepleai-sidebar-collapsed';
export const SIDEBAR_COOKIE_MAX_AGE = 60 * 60 * 24 * 365; // 1 year

export function parseSidebarCookie(value: string | undefined): boolean {
  return value === 'true';
}
