/**
 * Client-side view mode cookie helpers.
 *
 * These functions are safe to call only in the browser. For server-side
 * cookie reading, use `./server.ts` which uses `next/headers`.
 */
import {
  VIEW_MODE_COOKIE,
  VIEW_MODE_COOKIE_PATH,
  VIEW_MODE_COOKIE_SAMESITE,
  VIEW_MODES,
  type ViewMode,
} from './constants';

/**
 * Read the view mode cookie from `document.cookie`.
 * Returns null if the cookie is absent or contains an invalid value.
 *
 * Safe to call only in browser environments (no-op in SSR).
 */
export function readViewModeCookie(): ViewMode | null {
  if (typeof document === 'undefined') return null;

  const match = document.cookie
    .split(';')
    .map(c => c.trim())
    .find(c => c.startsWith(`${VIEW_MODE_COOKIE}=`));

  if (!match) return null;

  const value = match.slice(VIEW_MODE_COOKIE.length + 1);
  return (VIEW_MODES as readonly string[]).includes(value) ? (value as ViewMode) : null;
}

/**
 * Write the view mode cookie to `document.cookie`.
 * Session-scoped (no Max-Age), SameSite=Lax, client-readable.
 *
 * Safe to call only in browser environments (no-op in SSR).
 */
export function writeViewModeCookie(mode: ViewMode): void {
  if (typeof document === 'undefined') return;

  const parts = [
    `${VIEW_MODE_COOKIE}=${mode}`,
    `path=${VIEW_MODE_COOKIE_PATH}`,
    `SameSite=${VIEW_MODE_COOKIE_SAMESITE === 'lax' ? 'Lax' : VIEW_MODE_COOKIE_SAMESITE}`,
  ];

  // Secure flag in https contexts only
  if (typeof window !== 'undefined' && window.location.protocol === 'https:') {
    parts.push('Secure');
  }

  document.cookie = parts.join('; ');
}

/**
 * Clear the view mode cookie by setting an expired date.
 * Safe to call only in browser environments (no-op in SSR).
 */
export function clearViewModeCookie(): void {
  if (typeof document === 'undefined') return;

  const parts = [
    `${VIEW_MODE_COOKIE}=`,
    `path=${VIEW_MODE_COOKIE_PATH}`,
    'Max-Age=0',
    `SameSite=${VIEW_MODE_COOKIE_SAMESITE === 'lax' ? 'Lax' : VIEW_MODE_COOKIE_SAMESITE}`,
  ];

  document.cookie = parts.join('; ');
}
