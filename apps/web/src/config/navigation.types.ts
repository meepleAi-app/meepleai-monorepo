/**
 * Unified Navigation Type Definitions
 * Single source of truth for all navigation item types.
 *
 * Used by: UnifiedHeader, MobileNavDrawer, BottomNav, ActionBar, Navbar
 */

import type { LucideIcon } from 'lucide-react';

/**
 * Visibility rules for navigation items based on auth state and user role.
 */
export interface NavItemVisibility {
  /** Only show when authenticated */
  authOnly?: boolean;
  /** Only show when NOT authenticated */
  anonOnly?: boolean;
  /** Minimum role required (uses role hierarchy: SuperAdmin > Admin > Editor > User) */
  minRole?: 'Editor' | 'Admin';
}

/**
 * Sub-item for navigation items with children (e.g., Library dropdown).
 */
export interface UnifiedNavSubItem {
  /** Unique identifier */
  id: string;
  /** Route path */
  href: string;
  /** Display label */
  label: string;
  /** Accessible description */
  ariaLabel: string;
  /** Lucide icon component */
  icon?: LucideIcon;
}

/**
 * Unified navigation item used across all navigation components.
 *
 * - `icon`: LucideIcon component for React rendering (UnifiedHeader, MobileNavDrawer)
 * - `iconName`: string key for ICON_MAP lookup (ActionBar, FAB)
 */
export interface UnifiedNavItem {
  /** Unique identifier */
  id: string;
  /** Route path */
  href: string;
  /** Lucide icon component for React rendering */
  icon: LucideIcon;
  /** Icon name string for ICON_MAP lookup in ActionBar */
  iconName: string;
  /** Display label */
  label: string;
  /** Accessible description */
  ariaLabel: string;
  /** Responsive ordering (lower = more important, shown first on mobile) */
  priority: number;
  /** Test ID for e2e testing */
  testId: string;
  /** Pattern for active state matching */
  activePattern?: RegExp;
  /** Visibility rules based on auth/role */
  visibility?: NavItemVisibility;
  /** Sub-items (e.g., Library children: Collezione, Giochi Privati, Proposte) */
  children?: UnifiedNavSubItem[];
  /** Navigation group for sidebar organization (e.g., 'strumenti') */
  group?: string;
  /** Hide from main nav lists (item accessible elsewhere, e.g. user dropdown) */
  hideFromMainNav?: boolean;
}
