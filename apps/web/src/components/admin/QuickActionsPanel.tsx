/**
 * QuickActionsPanel Component - Issue #2788
 *
 * Re-export of QuickActions component with gradient-based styling.
 * This file exists for clarity and naming consistency with the issue requirements.
 *
 * The component provides:
 * - 6 action buttons with gradient backgrounds
 * - Dynamic badge counts
 * - Hover animations (icon scale + orange border)
 * - Responsive grid layout (2→3 columns)
 *
 * @example
 * ```tsx
 * import { QuickActionsPanel } from '@/components/admin/QuickActionsPanel';
 *
 * // With dynamic badge counts
 * <QuickActionsPanel badges={{ 'approve-games': 5, 'view-alerts': 12 }} />
 *
 * // With default actions
 * <QuickActionsPanel />
 * ```
 */

export {
  QuickActions as QuickActionsPanel,
  defaultQuickActions,
  type QuickAction,
  type QuickActionsProps,
  type GradientKey,
} from './QuickActions';
