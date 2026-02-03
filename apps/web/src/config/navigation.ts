/**
 * Navigation Configuration
 * Issue #3479 - Layout System v2: Unified ActionBar
 *
 * Defines navigation items for the unified bottom navigation.
 * These items are always visible in the ActionBar alongside context actions.
 */

/**
 * Navigation item configuration for unified bottom navigation.
 */
export interface NavItem {
  /** Unique identifier */
  id: string;
  /** Route path */
  href: string;
  /** Icon name (lucide icon) */
  icon: string;
  /** Display label */
  label: string;
  /** Accessible description */
  ariaLabel: string;
  /** Priority for ordering (lower = more visible on mobile) */
  priority: number;
  /** Test ID for e2e testing */
  testId: string;
  /** Pattern for active state matching */
  activePattern?: RegExp;
}

/**
 * Primary navigation items for authenticated users.
 * Order by priority: Dashboard → Library → Chat → Toolkit → Catalogo → Profilo
 *
 * Mobile shows first 4, tablet shows 5, desktop shows all (but nav is in header).
 * ActionBar is mobile-only; desktop uses UnifiedHeader for navigation.
 */
export const NAV_ITEMS: NavItem[] = [
  {
    id: 'home',
    href: '/dashboard',
    icon: 'home',
    label: 'Home',
    ariaLabel: 'Vai alla dashboard',
    priority: 1,
    testId: 'nav-home',
    activePattern: /^\/dashboard$/,
  },
  {
    id: 'library',
    href: '/library',
    icon: 'book-open',
    label: 'Libreria',
    ariaLabel: 'Vai alla tua libreria giochi',
    priority: 2,
    testId: 'nav-library',
    activePattern: /^\/library/,
  },
  {
    id: 'chat',
    href: '/chat',
    icon: 'message-square',
    label: 'Chat',
    ariaLabel: 'Apri la chat AI',
    priority: 3,
    testId: 'nav-chat',
    activePattern: /^\/chat/,
  },
  {
    id: 'toolkit',
    href: '/toolkit',
    icon: 'dice-6',
    label: 'Toolkit',
    ariaLabel: 'Vai al toolkit di gioco',
    priority: 4,
    testId: 'nav-toolkit',
    activePattern: /^\/toolkit/,
  },
  {
    id: 'catalog',
    href: '/games',
    icon: 'gamepad-2',
    label: 'Catalogo',
    ariaLabel: 'Sfoglia il catalogo giochi',
    priority: 5,
    testId: 'nav-catalog',
    activePattern: /^\/games/,
  },
  {
    id: 'profile',
    href: '/profile',
    icon: 'user',
    label: 'Profilo',
    ariaLabel: 'Vai al tuo profilo',
    priority: 6,
    testId: 'nav-profile',
    activePattern: /^\/profile/,
  },
];

/**
 * Maximum visible navigation items per breakpoint.
 * ActionBar is mobile-only; desktop navigation is handled by UnifiedHeader.
 */
export const MAX_NAV_ITEMS = {
  mobile: 4,   // Home, Library, Chat, Toolkit
  tablet: 5,   // + Catalogo
  desktop: 6,  // All items (but desktop uses header, not actionbar)
} as const;

/**
 * Total slots available per breakpoint.
 * Includes nav items + FAB (primary action) + context actions + overflow button.
 * Note: ActionBar is mobile/tablet only. Desktop uses UnifiedHeader.
 */
export const TOTAL_SLOTS = {
  mobile: 6,    // 4 nav + 1 FAB + 1 overflow
  tablet: 7,    // 5 nav + 1 FAB + 1 overflow
  desktop: 8,   // 6 nav + 1 FAB + 1 overflow (but not rendered on desktop)
} as const;

/**
 * Get navigation items for a specific breakpoint.
 * Returns items sorted by priority, limited to max for breakpoint.
 */
export function getNavItemsForBreakpoint(
  breakpoint: 'mobile' | 'tablet' | 'desktop'
): NavItem[] {
  // eslint-disable-next-line security/detect-object-injection -- breakpoint is typed union, not user input
  const max = MAX_NAV_ITEMS[breakpoint];
  return [...NAV_ITEMS]
    .sort((a, b) => a.priority - b.priority)
    .slice(0, max);
}

/**
 * Get overflow navigation items for a specific breakpoint.
 * Returns items that don't fit in visible slots.
 */
export function getOverflowNavItems(
  breakpoint: 'mobile' | 'tablet' | 'desktop'
): NavItem[] {
  // eslint-disable-next-line security/detect-object-injection -- breakpoint is typed union, not user input
  const max = MAX_NAV_ITEMS[breakpoint];
  return [...NAV_ITEMS]
    .sort((a, b) => a.priority - b.priority)
    .slice(max);
}

/**
 * Check if a route matches a navigation item.
 */
export function isNavItemActive(item: NavItem, pathname: string): boolean {
  if (item.activePattern) {
    return item.activePattern.test(pathname);
  }
  // Fallback: exact match for home, startsWith for others
  if (item.href === '/' || item.href === '/dashboard') {
    return pathname === item.href;
  }
  return pathname.startsWith(item.href);
}

/**
 * Get context action slots available for a breakpoint.
 * Total slots - nav items - 1 (for overflow).
 */
export function getContextActionSlots(
  breakpoint: 'mobile' | 'tablet' | 'desktop'
): number {
  // eslint-disable-next-line security/detect-object-injection -- breakpoint is typed union, not user input
  return TOTAL_SLOTS[breakpoint] - MAX_NAV_ITEMS[breakpoint] - 1;
}
