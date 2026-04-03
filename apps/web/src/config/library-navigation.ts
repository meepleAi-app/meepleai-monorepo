/**
 * Library Section Navigation Configuration
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 * Nav UX Simplification — riduzione a 3 tab con label italiane
 *
 * Tab ID mapping (nuovo → vecchio):
 *   games    ← private  (personal library, default)
 *   wishlist ← wishlist (unchanged)
 *   catalogo ← collection (shared catalog, ex-"public")
 *
 * Alpha Mode: solo games + wishlist (catalogo nascosto)
 */

import { type LucideIcon, BookOpenIcon, Gamepad2, Heart } from 'lucide-react';

/**
 * Tab definition for library section navigation
 */
export interface LibraryTab {
  /** URL-safe tab identifier */
  id: string;
  /** Display label */
  label: string;
  /** Tab icon */
  icon: LucideIcon;
  /** Route path */
  href: string;
}

/**
 * Library section tabs — query-param based navigation
 *
 * Default (/library, no tab param) renders personal library ("I Miei Giochi").
 * Alpha Mode: catalogo tab nascosto.
 */
const _ALL_LIBRARY_TABS: LibraryTab[] = [
  {
    id: 'games',
    label: 'I Miei Giochi',
    icon: Gamepad2,
    href: '/library',
  },
  {
    id: 'wishlist',
    label: 'Wishlist',
    icon: Heart,
    href: '/library?tab=wishlist',
  },
  {
    id: 'catalogo',
    label: 'Catalogo',
    icon: BookOpenIcon,
    href: '/library?tab=catalogo',
  },
];

// ─── Alpha Mode Filtering ────────────────────────────────────────────────────

const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_LIBRARY_TAB_IDS = new Set(['games', 'wishlist']);

/** Library tabs — filtered by ALPHA_MODE when active */
export const LIBRARY_TABS: LibraryTab[] = isAlphaMode
  ? _ALL_LIBRARY_TABS.filter(tab => ALPHA_LIBRARY_TAB_IDS.has(tab.id))
  : _ALL_LIBRARY_TABS;

/**
 * Determines the active tab ID from pathname + search params.
 *
 * Default (/library with no ?tab) → 'games'.
 * Accepts full URL (pathname + search) or just pathname.
 */
export function getActiveLibraryTab(pathname: string, search?: string): string {
  const tab = search
    ? new URLSearchParams(search.startsWith('?') ? search.slice(1) : search).get('tab')
    : null;

  if (tab === 'wishlist') return 'wishlist';
  if (tab === 'catalogo') return 'catalogo';
  return 'games';
}
