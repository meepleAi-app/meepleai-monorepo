/**
 * Library Section Navigation Configuration
 * Defines the library section sub-navigation tabs.
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
  if (pathname.startsWith('/library/private')) return 'private';
  if (pathname.startsWith('/library/proposals')) return 'proposals';
  if (pathname === '/library') return 'collection';
  return 'collection';
}
