/**
 * Library Section Navigation Configuration
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 * Defines the library section sub-navigation tabs.
 */

import { type LucideIcon, BookOpenIcon, Gamepad2, Heart, SendHorizontal } from 'lucide-react';

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
 * Library section tabs — query-param based navigation (Issue #5039)
 * Issue #5167 — renamed: Games (personal) / Collection (shared catalog, default)
 *
 * Tabs use ?tab= query params on /library instead of sub-routes.
 * Default (/library, no tab param) renders Collection (shared catalog).
 * Proposals tab added for inline share request management.
 */
const _ALL_LIBRARY_TABS: LibraryTab[] = [
  {
    id: 'collection',
    label: 'Collection',
    icon: BookOpenIcon,
    href: '/library',
  },
  {
    id: 'private',
    label: 'Games',
    icon: Gamepad2,
    href: '/library?tab=private',
  },
  {
    id: 'wishlist',
    label: 'Wishlist',
    icon: Heart,
    href: '/library?tab=wishlist',
  },
  {
    id: 'proposals',
    label: 'Proposals',
    icon: SendHorizontal,
    href: '/library?tab=proposals',
  },
];

// ─── Alpha Mode Filtering ────────────────────────────────────────────────────

const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_LIBRARY_TAB_IDS = new Set(['collection', 'private', 'wishlist']);

/** Library tabs — filtered by ALPHA_MODE when active */
export const LIBRARY_TABS: LibraryTab[] = isAlphaMode
  ? _ALL_LIBRARY_TABS.filter(tab => ALPHA_LIBRARY_TAB_IDS.has(tab.id))
  : _ALL_LIBRARY_TABS;

/**
 * Check if a pathname+search matches a library tab (Issue #5039)
 * Issue #5167 — updated for new tab structure (collection/private/wishlist)
 *
 * Default (/library with no ?tab) → 'collection' (shared catalog).
 * Accepts full URL (pathname + search) or just pathname.
 */
export function getActiveLibraryTab(pathname: string, search?: string): string {
  const tab = search
    ? new URLSearchParams(search.startsWith('?') ? search.slice(1) : search).get('tab')
    : null;

  if (tab === 'private') return 'private';
  if (tab === 'wishlist') return 'wishlist';
  if (tab === 'proposals') return 'proposals';
  if (pathname === '/library') return 'collection';
  return 'collection';
}
