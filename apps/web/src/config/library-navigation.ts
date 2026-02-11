/**
 * Library Section Navigation Configuration
 * Issue #4055 - Add Navigation Links for Private Games Section
 *
 * Defines the 3-tab library section navigation:
 * Collection (main), Private Games, My Proposals.
 */

import {
  type LucideIcon,
  BookOpenIcon,
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
 * Library section tabs - route-based navigation
 */
export const LIBRARY_TABS: LibraryTab[] = [
  {
    id: 'collection',
    label: 'Collezione',
    icon: BookOpenIcon,
    href: '/library',
  },
  {
    id: 'private',
    label: 'Giochi Privati',
    icon: LockIcon,
    href: '/library/private',
  },
  {
    id: 'proposals',
    label: 'Le Mie Proposte',
    icon: SendIcon,
    href: '/library/proposals',
  },
];

/**
 * Check if a pathname matches a library tab
 */
export function getActiveLibraryTab(pathname: string): string {
  // Exact match for sub-routes first (more specific)
  if (pathname.startsWith('/library/private')) return 'private';
  if (pathname.startsWith('/library/proposals')) return 'proposals';
  // Default: collection (matches /library exactly or /library with no sub-route)
  if (pathname === '/library') return 'collection';
  // For game detail pages (/library/games/...), still highlight collection
  return 'collection';
}
