/**
 * Unified Navigation Configuration
 * Single source of truth for ALL navigation items across the app.
 *
 * Replaces 5 divergent NAV_ITEMS definitions:
 * - config/navigation.ts (ActionBar) — this file
 * - components/layout/UnifiedHeader.tsx (desktop header)
 * - components/layout/Navbar/Navbar.tsx (old desktop nav)
 * - components/layout/Navbar/HamburgerMenu.tsx (old mobile menu)
 * - components/layout/BottomNav.tsx (removed)
 *
 * Consumers should use the `useNavigationItems` hook, NOT import directly.
 */

import {
  BookOpen,
  Brain,
  Calendar,
  Clock,
  History,
  LayoutDashboard,
  ShieldIcon,
  User,
  Users,
  Users2,
} from 'lucide-react';

import { LIBRARY_TABS } from '@/config/library-navigation';

import type { UnifiedNavItem, UnifiedNavSubItem, NavItemVisibility } from './navigation.types';

// Re-export types for backward compatibility
export type { UnifiedNavItem, UnifiedNavSubItem, NavItemVisibility };

/**
 * Legacy NavItem type for backward compatibility with ActionBar.
 * New code should use UnifiedNavItem via useNavigationItems hook.
 */
export interface NavItem {
  id: string;
  href: string;
  icon: string;
  label: string;
  ariaLabel: string;
  priority: number;
  testId: string;
  activePattern?: RegExp;
}

/**
 * Build library children from LIBRARY_TABS (library-navigation.ts).
 */
const LIBRARY_CHILDREN: UnifiedNavSubItem[] = LIBRARY_TABS.map(tab => ({
  id: tab.id,
  href: tab.href,
  label: tab.label,
  ariaLabel: `Navigate to ${tab.label}`,
  icon: tab.icon,
}));

/**
 * Unified navigation items — single source of truth.
 *
 * | id        | label     | priority | visibility   | note                     |
 * |-----------|-----------|----------|-------------|--------------------------|
 * | welcome   | Welcome   | 0        | anonOnly    | landing page only        |
 * | library   | Libreria  | 2        | authOnly    | children: tabs           |
 * | chat      | Chat      | 3        | authOnly    |                          |
 * | profile   | Profilo   | 6        | authOnly    |                          |
 * | agents    | Agenti    | 7        | authOnly    |                          |
 * | sessions  | Sessioni  | 8        | authOnly    |                          |
 */
const _ALL_NAV_ITEMS: UnifiedNavItem[] = [
  {
    id: 'welcome',
    href: '/',
    icon: LayoutDashboard,
    iconName: 'layout-dashboard',
    label: 'Welcome',
    ariaLabel: 'Navigate to welcome page',
    priority: 0,
    testId: 'nav-welcome',
    activePattern: /^\/$/,
    visibility: { anonOnly: true },
  },
  {
    id: 'library',
    href: '/library',
    icon: BookOpen,
    iconName: 'book-open',
    label: 'Libreria',
    ariaLabel: 'Navigate to your game library',
    priority: 2,
    testId: 'nav-library',
    activePattern: /^\/library/,
    visibility: { authOnly: true },
    children: LIBRARY_CHILDREN,
  },
  {
    id: 'chat',
    href: '/chat',
    icon: History,
    iconName: 'message-square',
    label: 'Chat',
    ariaLabel: 'Navigate to chat history',
    priority: 3,
    testId: 'nav-chat',
    activePattern: /^\/chat/,
    visibility: { authOnly: true },
  },
  {
    id: 'profile',
    href: '/profile',
    icon: User,
    iconName: 'user',
    label: 'Profilo',
    ariaLabel: 'Navigate to your profile',
    priority: 6,
    testId: 'nav-profile',
    activePattern: /^\/profile/,
    visibility: { authOnly: true },
    hideFromMainNav: true,
  },
  {
    id: 'agents',
    href: '/agents',
    icon: Users,
    iconName: 'users',
    label: 'Agenti',
    ariaLabel: 'Navigate to agents list',
    priority: 7,
    testId: 'nav-agents',
    activePattern: /^\/agents/,
    visibility: { authOnly: true },
    group: 'strumenti',
  },
  {
    id: 'sessions',
    href: '/sessions',
    icon: Calendar,
    iconName: 'calendar',
    label: 'Sessioni',
    ariaLabel: 'Navigate to play sessions',
    priority: 8,
    testId: 'nav-sessions',
    activePattern: /^\/sessions/,
    visibility: { authOnly: true },
    group: 'strumenti',
  },
  {
    id: 'play-records',
    href: '/play-records',
    icon: Clock,
    iconName: 'clock',
    label: 'Sessioni recenti',
    ariaLabel: 'Navigate to recent play records',
    priority: 9,
    testId: 'nav-play-records',
    activePattern: /^\/play-records/,
    visibility: { authOnly: true },
    group: 'strumenti',
  },
  {
    id: 'players',
    href: '/players',
    icon: Users2,
    iconName: 'users-2',
    label: 'Giocatori',
    ariaLabel: 'Navigate to players list',
    priority: 10,
    testId: 'nav-players',
    activePattern: /^\/players/,
    visibility: { authOnly: true },
    group: 'strumenti',
  },
  {
    id: 'knowledge-base',
    href: '/knowledge-base',
    icon: Brain,
    iconName: 'brain',
    label: 'Knowledge Base',
    ariaLabel: 'Navigate to knowledge base',
    priority: 11,
    testId: 'nav-knowledge-base',
    activePattern: /^\/knowledge-base/,
    visibility: { authOnly: true },
    group: 'strumenti',
  },
  {
    id: 'admin',
    href: '/admin',
    icon: ShieldIcon,
    iconName: 'shield',
    label: 'Admin Hub',
    ariaLabel: 'Navigate to admin hub',
    priority: 12,
    testId: 'nav-admin',
    activePattern: /^\/admin/,
    visibility: { authOnly: true, minRole: 'Admin' },
    group: 'admin',
  },
];

// ─── Alpha Mode Filtering ────────────────────────────────────────────────────

const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_NAV_IDS = new Set([
  'welcome',
  'dashboard',
  'library',
  'chat',
  'catalog',
  'profile',
  'admin',
]);

/** Unified navigation items — filtered by ALPHA_MODE when active */
export const UNIFIED_NAV_ITEMS: UnifiedNavItem[] = isAlphaMode
  ? _ALL_NAV_ITEMS.filter(item => ALPHA_NAV_IDS.has(item.id))
  : _ALL_NAV_ITEMS;

// ---------------------------------------------------------------------------
// Helper functions
// ---------------------------------------------------------------------------

/**
 * Filter navigation items by authentication state and user role.
 */
export function filterNavItemsByRole(
  items: UnifiedNavItem[],
  options: {
    isAuthenticated: boolean;
    isAuthLoading?: boolean;
    userRole?: string | null;
  }
): UnifiedNavItem[] {
  const { isAuthenticated, isAuthLoading = false, userRole } = options;

  // While auth is loading, only show items with no auth restrictions
  if (isAuthLoading) {
    return items.filter(item => !item.visibility?.authOnly && !item.visibility?.anonOnly);
  }

  const roleLower = userRole?.toLowerCase() ?? '';

  return items.filter(item => {
    const vis = item.visibility;
    if (!vis) return true;

    // Auth-only: only for authenticated users
    if (vis.authOnly && !isAuthenticated) return false;
    // Anon-only: only for non-authenticated users
    if (vis.anonOnly && isAuthenticated) return false;

    // Role check
    if (vis.minRole) {
      const minRole = vis.minRole.toLowerCase();
      if (minRole === 'admin') {
        if (roleLower !== 'admin' && roleLower !== 'superadmin') return false;
      } else if (minRole === 'editor') {
        if (roleLower !== 'editor' && roleLower !== 'admin' && roleLower !== 'superadmin')
          return false;
      }
    }

    return true;
  });
}

/**
 * Get navigation items for a specific breakpoint (for ActionBar).
 * Returns items sorted by priority, limited to max for breakpoint.
 */
export function getNavItemsForBreakpoint(breakpoint: 'mobile' | 'tablet' | 'desktop'): NavItem[] {
  const max = MAX_NAV_ITEMS[breakpoint];
  return [...NAV_ITEMS].sort((a, b) => a.priority - b.priority).slice(0, max);
}

/**
 * Get overflow navigation items for a specific breakpoint.
 */
export function getOverflowNavItems(breakpoint: 'mobile' | 'tablet' | 'desktop'): NavItem[] {
  const max = MAX_NAV_ITEMS[breakpoint];
  return [...NAV_ITEMS].sort((a, b) => a.priority - b.priority).slice(max);
}

/**
 * Check if a route matches a unified navigation item.
 */
export function isUnifiedNavItemActive(item: UnifiedNavItem, pathname: string): boolean {
  if (item.activePattern) {
    return item.activePattern.test(pathname);
  }
  if (item.href === '/' || item.href === '/library') {
    return pathname === item.href;
  }
  return pathname.startsWith(item.href);
}

/**
 * Check if a route matches a legacy navigation item.
 */
export function isNavItemActive(item: NavItem, pathname: string): boolean {
  if (item.activePattern) {
    return item.activePattern.test(pathname);
  }
  if (item.href === '/' || item.href === '/library') {
    return pathname === item.href;
  }
  return pathname.startsWith(item.href);
}

/**
 * Get context action slots available for a breakpoint.
 */
export function getContextActionSlots(breakpoint: 'mobile' | 'tablet' | 'desktop'): number {
  return TOTAL_SLOTS[breakpoint] - MAX_NAV_ITEMS[breakpoint] - 1;
}

// ---------------------------------------------------------------------------
// Legacy constants for ActionBar backward compatibility
// ---------------------------------------------------------------------------

/**
 * Legacy NAV_ITEMS for ActionBar (string-based icons).
 * ActionBar uses icon name strings, not LucideIcon components.
 */
export const NAV_ITEMS: NavItem[] = UNIFIED_NAV_ITEMS.filter(
  item => item.visibility?.authOnly || !item.visibility
) // ActionBar only for auth users
  .filter(item => !item.visibility?.anonOnly)
  .filter(item => ['dashboard', 'library', 'catalog'].includes(item.id)) // ActionBar subset (3 items + central FAB)
  .map(item => ({
    id: item.id === 'dashboard' ? 'home' : item.id,
    href: item.href,
    icon: item.iconName,
    label: item.id === 'dashboard' ? 'Home' : item.label,
    ariaLabel: item.ariaLabel,
    priority: item.priority,
    testId: item.testId,
    activePattern: item.activePattern,
  }));

/**
 * Maximum visible navigation items per breakpoint (ActionBar).
 */
export const MAX_NAV_ITEMS = {
  mobile: 3,
  tablet: 5,
  desktop: 6,
} as const;

/**
 * Total slots available per breakpoint (ActionBar).
 */
export const TOTAL_SLOTS = {
  mobile: 6,
  tablet: 7,
  desktop: 8,
} as const;
