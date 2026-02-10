/**
 * QuickActionsMenu - Context Menu Feature for MeepleCard
 * Issue #3825 - MeepleCard Feature Extensions
 *
 * Dropdown menu with configurable quick actions for card operations.
 *
 * Features:
 * - Trigger button ("..." MoreVertical icon)
 * - Role-based action filtering (admin-only items)
 * - Hidden action support (runtime visibility control)
 * - Destructive action styling (red for delete)
 * - Separator support between action groups
 * - Event propagation stopped
 * - Keyboard accessible (Arrow keys, Enter, Esc)
 *
 * @example
 * ```tsx
 * <QuickActionsMenu
 *   actions={[
 *     { icon: Eye, label: 'View Details', onClick: () => {} },
 *     { icon: Share2, label: 'Share', onClick: () => {}, separator: true },
 *     { icon: Edit, label: 'Edit', onClick: () => {}, adminOnly: true },
 *   ]}
 *   userRole="user"
 * />
 * ```
 */

'use client';

import { MoreVertical, type LucideIcon } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface QuickAction {
  /** Lucide icon component */
  icon: LucideIcon;
  /** Action label */
  label: string;
  /** Click handler */
  onClick: () => void | Promise<void>;
  /** Disabled state */
  disabled?: boolean;
  /** Hidden at runtime (e.g., based on permissions) */
  hidden?: boolean;
  /** Admin-only action (filtered based on userRole) */
  adminOnly?: boolean;
  /** Destructive action (red styling for delete/remove) */
  destructive?: boolean;
  /** Add separator after this item */
  separator?: boolean;
}

export interface QuickActionsMenuProps {
  /** Actions to display in menu */
  actions: QuickAction[];
  /** Current user role (for filtering admin actions) */
  userRole?: 'user' | 'editor' | 'admin';
  /** Size variant for trigger button */
  size?: 'sm' | 'md' | 'lg';
  /** Additional CSS classes */
  className?: string;
  /** Test ID for testing */
  'data-testid'?: string;
}

// ============================================================================
// Component
// ============================================================================

export function QuickActionsMenu({
  actions,
  userRole = 'user',
  size = 'md',
  className,
  'data-testid': testId,
}: QuickActionsMenuProps) {
  // Filter actions based on role and hidden flag
  const visibleActions = actions.filter((action) => {
    // Skip if explicitly hidden
    if (action.hidden) return false;

    // Skip admin-only actions for non-admin users
    if (action.adminOnly && userRole !== 'admin' && userRole !== 'editor') {
      return false;
    }

    return true;
  });

  // Don't render if no visible actions
  if (visibleActions.length === 0) {
    return null;
  }

  const handleActionClick = async (
    e: React.MouseEvent,
    action: QuickAction
  ) => {
    // Stop propagation to prevent card onClick
    e.stopPropagation();

    if (action.disabled) return;

    try {
      await action.onClick();
    } catch (error) {
      // Error handling delegated to action callback
      console.error('QuickActionsMenu: action error:', error);
    }
  };

  const iconSize =
    size === 'sm' ? 'h-4 w-4' : size === 'lg' ? 'h-6 w-6' : 'h-5 w-5';

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size={size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'icon'}
          onClick={(e) => e.stopPropagation()}
          aria-label="Open quick actions menu"
          className={cn('rounded-full', className)}
          data-testid={testId || 'quick-actions-trigger'}
        >
          <MoreVertical className={iconSize} />
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" data-testid="quick-actions-menu">
        {visibleActions.map((action, index) => {
          const ActionIcon = action.icon;

          return (
            <div key={`${action.label}-${index}`}>
              <DropdownMenuItem
                onClick={(e) => handleActionClick(e, action)}
                disabled={action.disabled}
                className={cn(
                  action.destructive &&
                    'text-destructive focus:text-destructive focus:bg-destructive/10'
                )}
                data-testid={`action-${action.label.toLowerCase().replace(/\s+/g, '-')}`}
              >
                <ActionIcon className="mr-2 h-4 w-4" />
                <span>{action.label}</span>
              </DropdownMenuItem>

              {/* Separator after this item if specified */}
              {action.separator && <DropdownMenuSeparator />}
            </div>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default QuickActionsMenu;
