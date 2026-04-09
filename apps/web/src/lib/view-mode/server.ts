/**
 * Server-side view mode cookie reader.
 *
 * Uses Next.js 16 `cookies()` from `next/headers` to read the
 * `meepleai_view_mode` cookie from Server Components, Route Handlers,
 * and Server Actions. This enables SSR-consistent rendering of the
 * correct shell (admin vs user) on first paint.
 *
 * @see https://nextjs.org/docs/app/api-reference/functions/cookies
 */
import { cookies } from 'next/headers';

import { VIEW_MODE_COOKIE, VIEW_MODES, type ViewMode } from './constants';

/**
 * Read the view mode cookie from the current request's cookies.
 * Returns null if absent or invalid.
 *
 * MUST be called from Server Components, Route Handlers, or Server Actions.
 */
export async function readViewModeCookieServer(): Promise<ViewMode | null> {
  const cookieStore = await cookies();
  const cookie = cookieStore.get(VIEW_MODE_COOKIE);

  if (!cookie) return null;

  const value = cookie.value;
  return (VIEW_MODES as readonly string[]).includes(value) ? (value as ViewMode) : null;
}
