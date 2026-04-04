/**
 * Layout System Type Definitions
 * Issue #3287 - Phase 1: Core Layout Structure
 *
 * Provides type safety for the mobile-first layout system including:
 * - Context management types
 * - Action bar configuration
 * - FAB (Floating Action Button) configuration
 * - Responsive breakpoint detection
 */

import type { LucideIcon } from 'lucide-react';

/**
 * Supported layout contexts for context-aware navigation
 * Each context determines which actions are available in ActionBar and FAB
 *
 * Issue #3479 - Extended contexts for Layout System v2
 */
export type LayoutContext =
  // Core contexts
  | 'default'
  | 'dashboard'
  | 'library'
  | 'library_empty'
  | 'library_selection'
  | 'game_detail'
  | 'game_detail_not_owned'
  | 'session_active'
  | 'session_setup'
  | 'session_end'
  | 'chat'
  | 'document_viewer'
  | 'catalog'
  | 'search'
  | 'wishlist'
  | 'notifications'
  | 'profile'
  | 'settings';

/**
 * Device viewport classification
 * Based on Tailwind breakpoints with mobile-first approach
 */
export type DeviceType = 'mobile' | 'tablet' | 'desktop';

/**
 * Responsive breakpoint detection result
 */
export interface ResponsiveState {
  /** Current device type classification */
  deviceType: DeviceType;
  /** True if viewport < 640px */
  isMobile: boolean;
  /** True if viewport 640px - 1023px */
  isTablet: boolean;
  /** True if viewport >= 1024px */
  isDesktop: boolean;
  /** Current viewport width in pixels */
  viewportWidth: number;
}

/**
 * Action item for ActionBar and FAB menus
 * Supports icons, labels, and priority-based ordering
 */
export interface Action {
  /** Unique identifier for the action */
  id: string;
  /** Display label for the action */
  label: string;
  /** Lucide icon component or icon name string */
  icon: LucideIcon | string;
  /** Handler function when action is triggered */
  onClick?: () => void;
  /** Action identifier for event dispatching */
  action?: string;
  /** Priority for ordering (lower = more visible) */
  priority: number;
  /** Whether action is currently disabled */
  isDisabled?: boolean;
  /** Accessible description for screen readers */
  ariaLabel?: string;
  /** Optional keyboard shortcut */
  shortcut?: string;
  /** Variant for styling (primary actions get accent color) */
  variant?: 'default' | 'primary' | 'destructive';
  /** Context to switch to when action is triggered */
  contextChange?: LayoutContext;
  /** Visibility rules based on user role (optional, backward-compatible) */
  visibility?: { minRole?: 'editor' | 'admin' };
}

/**
 * FAB-specific action with context association
 * Extends Action with FAB-specific properties
 */
export interface FABAction extends Omit<Action, 'priority'> {
  /** Context in which this FAB action is displayed */
  context: LayoutContext;
  /** Quick menu items shown on long-press */
  quickMenuItems?: Omit<Action, 'priority'>[];
}

/**
 * Configuration for FAB appearance and behavior
 */
export interface FABConfig {
  /** Whether FAB should be visible */
  visible: boolean;
  /** Primary action for tap */
  primaryAction?: FABAction;
  /** Position from bottom (in pixels) */
  bottomOffset?: number;
  /** Position from right (in pixels) */
  rightOffset?: number;
}

/**
 * Configuration for ActionBar
 */
export interface ActionBarConfig {
  /** Actions to display in the bar */
  actions: Action[];
  /** Maximum visible actions (rest go to overflow) */
  maxVisible?: number;
  /** Whether to show the action bar */
  visible: boolean;
}

/**
 * Multi-select mode state for batch operations
 */
export interface MultiSelectState {
  /** Whether multi-select mode is active */
  isActive: boolean;
  /** Currently selected item IDs */
  selectedIds: string[];
  /** Total count of selectable items */
  totalCount: number;
}

/**
 * Breadcrumb item for navigation context
 */
export interface BreadcrumbItem {
  /** Display label */
  label: string;
  /** Optional href for navigation */
  href?: string;
  /** Icon to display */
  icon?: LucideIcon;
  /** Whether this is the current (active) item */
  isCurrent?: boolean;
}

/**
 * Main layout context state
 * Manages all layout-related state across the application
 */
export interface LayoutState {
  /** Current page context */
  context: LayoutContext;
  /** Responsive state from viewport detection */
  responsive: ResponsiveState;
  /** FAB configuration */
  fab: FABConfig;
  /** ActionBar configuration */
  actionBar: ActionBarConfig;
  /** Multi-select state */
  multiSelect: MultiSelectState;
  /** Current breadcrumb trail */
  breadcrumbs: BreadcrumbItem[];
  /** Whether hamburger menu is open */
  isMenuOpen: boolean;
  /** Whether keyboard is visible (affects FAB position) */
  isKeyboardVisible: boolean;
  /** Whether modal is open (hides FAB) */
  isModalOpen: boolean;
  /** Current scroll direction */
  scrollDirection: 'up' | 'down' | 'none';
}

/**
 * Layout context actions for state management
 */
export interface LayoutActions {
  /** Set the current layout context */
  setContext: (context: LayoutContext) => void;
  /** Update FAB configuration */
  setFABConfig: (config: Partial<FABConfig>) => void;
  /** Update ActionBar configuration */
  setActionBarConfig: (config: Partial<ActionBarConfig>) => void;
  /** Toggle multi-select mode */
  toggleMultiSelect: (enabled?: boolean) => void;
  /** Add item to selection */
  addToSelection: (id: string) => void;
  /** Remove item from selection */
  removeFromSelection: (id: string) => void;
  /** Clear all selections */
  clearSelection: () => void;
  /** Select all items */
  selectAll: (ids: string[]) => void;
  /** Set breadcrumb trail */
  setBreadcrumbs: (items: BreadcrumbItem[]) => void;
  /** Toggle hamburger menu */
  toggleMenu: (open?: boolean) => void;
  /** Set keyboard visibility */
  setKeyboardVisible: (visible: boolean) => void;
  /** Set modal visibility */
  setModalOpen: (open: boolean) => void;
  /** Set scroll direction */
  setScrollDirection: (direction: 'up' | 'down' | 'none') => void;
  /** Register actions for current context */
  registerActions: (actions: Action[]) => void;
  /** Clear registered actions */
  clearActions: () => void;
}

/**
 * Complete layout context value (state + actions)
 */
export interface LayoutContextValue extends LayoutState, LayoutActions {}

/**
 * Default FAB configuration per context
 */
export interface FABContextConfig {
  [key: string]: FABAction;
}

/**
 * Breakpoint values for responsive detection (in pixels)
 */
export interface Breakpoints {
  /** Small screens (mobile) */
  sm: number;
  /** Medium screens (tablet) */
  md: number;
  /** Large screens (desktop) */
  lg: number;
  /** Extra large screens */
  xl: number;
  /** 2x Extra large screens */
  '2xl': number;
}

/**
 * Context domains for organizing LayoutContext values into logical groups.
 * The flat LayoutContext type is kept for backward compatibility;
 * domains are organizational metadata for documentation and tooling.
 */
export const CONTEXT_DOMAINS = {
  core: ['default', 'dashboard'],
  library: ['library', 'library_empty', 'library_selection'],
  game: ['game_detail', 'game_detail_not_owned', 'catalog'],
  session: ['session_setup', 'session_active', 'session_end'],
  communication: ['chat'],
  content: ['document_viewer', 'search'],
  user: ['profile', 'settings', 'wishlist', 'notifications'],
} as const;

/**
 * Default breakpoint values matching Tailwind defaults
 */
export const BREAKPOINTS: Breakpoints = {
  sm: 640,
  md: 768,
  lg: 1024,
  xl: 1280,
  '2xl': 1536,
};

/**
 * ActionBar slot counts per device type
 */
export const ACTION_BAR_SLOTS: Record<DeviceType, number> = {
  mobile: 3,
  tablet: 4,
  desktop: 6,
};

/**
 * FAB dimensions and positioning (in pixels)
 */
export const FAB_DIMENSIONS = {
  size: 56,
  bottomOffset: 80,
  rightOffset: 16,
} as const;

/**
 * Animation timing constants (in milliseconds)
 */
export const ANIMATION_TIMING = {
  /** Standard transition duration */
  base: 200,
  /** Fast transitions */
  fast: 150,
  /** Slow transitions */
  slow: 300,
  /** Long press threshold for FAB quick menu */
  longPress: 500,
  /** Stagger delay between ActionBar items */
  stagger: 50,
} as const;
