/**
 * QuickActionsGrid - Dashboard Widget for Quick Navigation
 * Issue #3313 - Implement QuickActionsGrid
 *
 * Features:
 * - 5 quick action buttons with icons and labels
 * - Responsive grid: 2-col mobile, 5-col desktop
 * - Hover/focus states
 * - Touch-friendly (min 44x44px)
 * - Navigation to key app sections
 *
 * @example
 * ```tsx
 * <QuickActionsGrid />
 * ```
 */

'use client';

import { motion } from 'framer-motion';
import {
  Library,
  Dices,
  MessageSquare,
  Search,
  Settings,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface QuickAction {
  id: string;
  icon: LucideIcon;
  label: string;
  href: string;
  iconColor: string;
  bgColor: string;
}

export interface QuickActionsGridProps {
  /** Custom actions (overrides defaults) */
  actions?: QuickAction[];
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Default Actions
// ============================================================================

const DEFAULT_ACTIONS: QuickAction[] = [
  {
    id: 'library',
    icon: Library,
    label: 'Vai alla Collezione',
    href: '/library',
    iconColor: 'text-amber-600 dark:text-amber-400',
    bgColor: 'bg-amber-500/20 hover:bg-amber-500/30',
  },
  {
    id: 'session',
    icon: Dices,
    label: 'Nuova Sessione',
    href: '/toolkit',
    iconColor: 'text-emerald-600 dark:text-emerald-400',
    bgColor: 'bg-emerald-500/20 hover:bg-emerald-500/30',
  },
  {
    id: 'chat',
    icon: MessageSquare,
    label: 'Chat AI Regole',
    href: '/chat',
    iconColor: 'text-blue-600 dark:text-blue-400',
    bgColor: 'bg-blue-500/20 hover:bg-blue-500/30',
  },
  {
    id: 'catalog',
    icon: Search,
    label: 'Esplora Catalogo',
    href: '/games/catalog',
    iconColor: 'text-purple-600 dark:text-purple-400',
    bgColor: 'bg-purple-500/20 hover:bg-purple-500/30',
  },
  {
    id: 'settings',
    icon: Settings,
    label: 'Impostazioni',
    href: '/settings',
    iconColor: 'text-slate-600 dark:text-slate-400',
    bgColor: 'bg-slate-500/20 hover:bg-slate-500/30',
  },
];

// ============================================================================
// Skeleton Component
// ============================================================================

function QuickActionsGridSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="quick-actions-skeleton"
    >
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-5">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex flex-col items-center gap-2 p-3">
            <Skeleton className="h-12 w-12 rounded-xl" />
            <Skeleton className="h-3 w-20" />
          </div>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Action Button Component
// ============================================================================

function ActionButton({ action, index }: { action: QuickAction; index: number }) {
  const Icon = action.icon;

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
    >
      <Link
        href={action.href}
        className={cn(
          'flex flex-col items-center gap-2 p-3 rounded-xl',
          'transition-all duration-200',
          'hover:scale-105 hover:shadow-md',
          'focus:outline-none focus:ring-2 focus:ring-primary/50 focus:ring-offset-2',
          'min-h-[88px] min-w-[88px]', // Touch-friendly: 44x44 * 2 for padding
          action.bgColor
        )}
        data-testid={`quick-action-${action.id}`}
      >
        <div
          className={cn(
            'flex h-12 w-12 items-center justify-center rounded-xl',
            'bg-background/50'
          )}
        >
          <Icon className={cn('h-6 w-6', action.iconColor)} />
        </div>
        <span
          className="text-xs font-medium text-center leading-tight"
          data-testid={`quick-action-label-${action.id}`}
        >
          {action.label}
        </span>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// QuickActionsGrid Component
// ============================================================================

export function QuickActionsGrid({
  actions = DEFAULT_ACTIONS,
  isLoading = false,
  className,
}: QuickActionsGridProps) {
  if (isLoading) {
    return <QuickActionsGridSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="quick-actions-grid"
    >
      <div
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-5"
        data-testid="actions-container"
      >
        {actions.map((action, index) => (
          <ActionButton key={action.id} action={action} index={index} />
        ))}
      </div>
    </section>
  );
}

export default QuickActionsGrid;
