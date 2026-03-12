// Barrel exports for layout module
// Issue #3286 - Layout System Implementation

// Phase 1: Core Layout Structure (#3287)
export {
  LayoutProvider,
  useLayout,
  useLayoutResponsive,
  useLayoutFAB,
  useLayoutActionBar,
  useLayoutMultiSelect,
  useLayoutContext,
} from './LayoutProvider';
export { Layout, PageHeader, PageContent, EmptyState, LoadingState } from './Layout';

// Logo relocated to TopNavbar (#5033)
export { Logo } from './TopNavbar';
export type { LogoProps } from './TopNavbar';

// Phase 4: ActionBar System (#3290)
export {
  ActionBar,
  ActionBarSpacer,
  ActionBarItem,
  OverflowMenu,
  MultiSelectBar,
} from './ActionBar';
export type {
  ActionBarProps,
  ActionBarItemProps,
  OverflowMenuProps,
  MultiSelectBarProps,
} from './ActionBar';

// Layout System v2 - UnifiedActionBar (#3479)
export { UnifiedActionBar, UnifiedActionBarSpacer } from './ActionBar';
export type { UnifiedActionBarProps } from './ActionBar';

// Phase 5: Smart FAB (#3291) - REMOVED: FAB functionality integrated into UnifiedActionBar

// Phase 6: Breadcrumb & Polish (#3292)
export { Breadcrumb, getContextConfig } from './Breadcrumb';
export type { BreadcrumbProps } from './Breadcrumb';

// Existing components
export { ThemeSwitcher } from './ThemeSwitcher';
export { toast } from './Toast';
export { KeyboardShortcutsHelp } from './KeyboardShortcutsHelp';
export { CommandPalette } from './CommandPalette';
