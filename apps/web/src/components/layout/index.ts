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

// Logo — TopNavbar removed, use MeepleLogo directly from '@/components/ui/meeple/meeple-logo'

// Phase 4-6: ActionBar + Breadcrumb removed — replaced by UnifiedShell ContextualBottomNav

// Existing components
export { toast } from './Toast';
export { KeyboardShortcutsHelp } from './KeyboardShortcutsHelp';
export { CommandPalette } from './CommandPalette';
