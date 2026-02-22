/**
 * Library Section Navigation Configuration
 * Defines the library section sub-navigation tabs.
 */

import {
  type LucideIcon,
  BookOpenIcon,
  Heart,
  LockIcon,
  SendIcon,
} from 'lucide-react';

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
 *
 * Tabs now use ?tab= query params on /library instead of sub-routes.
 * "Proposte" moved to /discover?tab=proposals (community section).
 */
export const LIBRARY_TABS: LibraryTab[] = [
  {
    id: 'collection',
    label: 'Collezione',
    icon: BookOpenIcon,
    href: '/library',
  },
  {
    id: 'wishlist',
    label: 'Wishlist',
    icon: Heart,
    href: '/library?tab=wishlist',
  },
  {
    id: 'private',
    label: 'Giochi Privati',
    icon: LockIcon,
    href: '/library?tab=private',
  },
  {
    id: 'proposals',
    label: 'Le Mie Proposte',
    icon: SendIcon,
    href: '/discover?tab=proposals',
  },
];

/**
 * Check if a pathname+search matches a library tab (Issue #5039)
 *
 * Accepts full URL (pathname + search) or just pathname.
 */
export function getActiveLibraryTab(pathname: string, search?: string): string {
  const tab = search
    ? new URLSearchParams(search.startsWith('?') ? search.slice(1) : search).get('tab')
    : null;

  if (tab === 'wishlist') return 'wishlist';
  if (tab === 'private') return 'private';
  // proposals live under /discover
  if (pathname.startsWith('/discover') && tab === 'proposals') return 'proposals';
  if (pathname === '/library') return 'collection';
  return 'collection';
}
