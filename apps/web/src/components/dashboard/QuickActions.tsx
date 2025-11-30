/**
 * QuickActions Component (Issue #1834: UI-007)
 *
 * Dashboard action cards in 2-column grid
 *
 * Features:
 * - Default actions: "Add Game", "New Chat"
 * - Extensible via actions prop
 * - Responsive 2-column grid
 * - Integration with QuickActionCard
 *
 * Usage:
 * ```tsx
 * // Default actions
 * <QuickActions />
 *
 * // Custom actions
 * <QuickActions actions={customActions} />
 * ```
 */

import React from 'react';
import { useRouter } from 'next/router';
import { QuickActionCard, type QuickActionCardProps } from './QuickActionCard';
import { PlusCircle, MessageSquarePlus, LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface QuickAction {
  /** Unique action identifier */
  id: string;
  /** Lucide icon component */
  icon: LucideIcon;
  /** Action title */
  title: string;
  /** Optional description */
  description?: string;
  /** Click handler */
  onClick: () => void;
  /** Card variant */
  variant?: QuickActionCardProps['variant'];
}

export interface QuickActionsProps {
  /** Custom actions (optional, uses defaults if not provided) */
  actions?: QuickAction[];
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Default Actions
// ============================================================================

/**
 * Get default quick actions
 */
function useDefaultActions(): QuickAction[] {
  const router = useRouter();

  return React.useMemo(
    () => [
      {
        id: 'add-game',
        icon: PlusCircle,
        title: 'Add Game',
        description: 'Add a new board game to your collection',
        onClick: () => router.push('/games/add'),
        variant: 'default' as const,
      },
      {
        id: 'new-chat',
        icon: MessageSquarePlus,
        title: 'New Chat',
        description: 'Start a conversation about game rules',
        onClick: () => router.push('/chat/new'),
        variant: 'default' as const,
      },
    ],
    [router]
  );
}

// ============================================================================
// QuickActions Component
// ============================================================================

export const QuickActions = React.memo(function QuickActions({
  actions: customActions,
  className,
}: QuickActionsProps) {
  const defaultActions = useDefaultActions();
  const actions = customActions || defaultActions;

  return (
    <div
      className={cn('grid grid-cols-1 sm:grid-cols-2 gap-4', className)}
      role="region"
      aria-label="Quick actions"
    >
      {actions.map(action => (
        <QuickActionCard
          key={action.id}
          icon={action.icon}
          title={action.title}
          description={action.description}
          onClick={action.onClick}
          variant={action.variant}
        />
      ))}
    </div>
  );
});
