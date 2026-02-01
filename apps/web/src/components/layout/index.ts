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
export {
  Layout,
  PageHeader,
  PageContent,
  EmptyState,
  LoadingState,
} from './Layout';

// Phase 2: Navbar Components (#3288)
export {
  Navbar,
  HamburgerButton,
  HamburgerMenu,
  Logo,
  NavItems,
  NavItemButton,
  ProfileBar,
} from './Navbar';
export type {
  NavbarProps,
  HamburgerButtonProps,
  HamburgerMenuProps,
  LogoProps,
  NavItem,
  NavItemsProps,
  NavItemButtonProps,
  ProfileBarProps,
} from './Navbar';

// Phase 3: GlobalSearch Component (#3289)
export {
  GlobalSearch,
  SearchTrigger,
  SearchInput,
  SearchResults,
  RecentSearches,
} from './GlobalSearch';
export type {
  GlobalSearchProps,
  SearchTriggerProps,
  SearchInputProps,
  SearchResultsProps,
  SearchResult,
  SearchResultType,
  RecentSearchesProps,
} from './GlobalSearch';

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

// Phase 5: Smart FAB (#3291)
export { SmartFAB, QuickMenu } from './SmartFAB';
export type { SmartFABProps, QuickMenuProps } from './SmartFAB';

// Phase 6: Breadcrumb & Polish (#3292)
export { Breadcrumb, getContextConfig } from './Breadcrumb';
export type { BreadcrumbProps } from './Breadcrumb';

// Existing components
export { ThemeSwitcher } from './ThemeSwitcher';
export { toast } from './Toast';
export { KeyboardShortcutsHelp } from './KeyboardShortcutsHelp';
export { CommandPalette } from './CommandPalette';
