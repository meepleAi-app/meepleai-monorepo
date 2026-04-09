/**
 * View Mode — Cookie constants
 *
 * The `meepleai_view_mode` cookie stores the admin user's preferred shell.
 * Client-writable (not HttpOnly). Read server-side via `cookies()` from `next/headers`
 * in layout.tsx guards for SSR-consistent rendering.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.1
 */

/** Cookie name used across client and server to persist view mode */
export const VIEW_MODE_COOKIE = 'meepleai_view_mode';

/** The two valid view mode values */
export type ViewMode = 'admin' | 'user';

/** All valid view modes (for runtime validation) */
export const VIEW_MODES: readonly ViewMode[] = ['admin', 'user'] as const;

/** Cookie max-age: undefined = session cookie (cleared when browser closes) */
export const VIEW_MODE_COOKIE_MAX_AGE: number | undefined = undefined;

/** SameSite attribute — lax allows top-level navigation redirects */
export const VIEW_MODE_COOKIE_SAMESITE = 'lax' as const;

/** Path scope — whole app */
export const VIEW_MODE_COOKIE_PATH = '/' as const;
