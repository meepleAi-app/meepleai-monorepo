/**
 * ActionBar Components
 * Issue #3290 - Phase 4: ActionBar System
 * Issue #3292 - Phase 6: MultiSelectBar
 * Issue #3479 - Layout System v2: UnifiedActionBar
 * Issue #5038 - 3-tier NavActionBar (NavigationContext-driven)
 *
 * Exports all action bar related components.
 */

// ─── New 3-tier NavActionBar (Issue #5038) ────────────────────────────────────
export { NavActionBar, type NavActionBarProps } from './NavActionBar';
export { ActionBarButton, type ActionBarButtonProps } from './ActionBarButton';
export { ActionBarOverflow, type ActionBarOverflowProps } from './ActionBarOverflow';

// ─── Legacy ActionBar (kept for migration period) ─────────────────────────────
export { ActionBar, ActionBarSpacer, type ActionBarProps } from './ActionBar';
export { ActionBarItem, type ActionBarItemProps } from './ActionBarItem';
export { OverflowMenu, type OverflowMenuProps } from './OverflowMenu';
export { MultiSelectBar, type MultiSelectBarProps } from './MultiSelectBar';

// Layout System v2 - UnifiedActionBar
export {
  UnifiedActionBar,
  UnifiedActionBarSpacer,
  type UnifiedActionBarProps,
} from './UnifiedActionBar';
