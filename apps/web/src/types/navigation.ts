/**
 * Navigation System Type Definitions
 * Issue #5034 - Navigation Config System
 *
 * Defines types for the 3-tier navigation system:
 * Navbar (L1) → MiniNav (L2) → ActionBar (L3)
 */

import type { LucideIcon } from 'lucide-react';

/**
 * A tab entry for the MiniNav (L2) component.
 * Link-based tab rendered with Next.js <Link>.
 */
export interface NavTab {
  /** Unique identifier for the tab */
  id: string;
  /** Display label */
  label: string;
  /** Route href */
  href: string;
  /** Lucide icon (optional) */
  icon?: LucideIcon;
  /** Badge count or label (optional) */
  badge?: number | string;
}

/**
 * An action button for the ActionBar (L3) component.
 * Max 6 on desktop, max 4 on mobile (rest in overflow "...").
 */
export interface NavAction {
  /** Unique identifier for the action */
  id: string;
  /** Display label */
  label: string;
  /** Lucide icon (required for ActionBar) */
  icon: LucideIcon;
  /** Click handler */
  onClick: () => void;
  /** Visual variant — primary is highlighted, secondary/ghost are neutral */
  variant?: 'primary' | 'secondary' | 'ghost';
  /** Badge count or label (optional) */
  badge?: number | string;
  /** Hide this action (not rendered but tracked for potential re-show) */
  hidden?: boolean;
  /** Disable this action — renders with aria-disabled, not disabled attr */
  disabled?: boolean;
  /** Tooltip shown when action is disabled */
  disabledTooltip?: string;
}

/**
 * Page-level navigation configuration.
 * Set via useSetNavConfig() in layouts or pages.
 */
export interface PageNavConfig {
  /** MiniNav (L2) tabs for this page/section */
  miniNav?: NavTab[];
  /** ActionBar (L3) actions for this page/section */
  actionBar?: NavAction[];
  /**
   * Sub-section zone identifier.
   * Used for zone-aware ActionBar switching within a single page.
   * E.g. "game-detail/agent", "game-detail/overview"
   */
  zone?: string;
}

/**
 * Value exposed by NavigationContext to consumers.
 */
export interface NavigationContextValue {
  /** Current MiniNav tabs — set by the active page via useSetNavConfig */
  miniNavTabs: NavTab[];
  /** Current ActionBar actions — set by the active page via useSetNavConfig */
  actionBarActions: NavAction[];
  /** Current active zone (sub-section within a page) */
  activeZone: string | null;
  /** Low-level setter — prefer useSetNavConfig for page-level configuration */
  setNavConfig: (config: PageNavConfig) => void;
  /** Clear the navigation configuration (resets all to empty) */
  clearNavConfig: () => void;
}
