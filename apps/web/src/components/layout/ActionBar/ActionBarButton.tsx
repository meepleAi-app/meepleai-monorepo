'use client';

/**
 * ActionBarButton — Individual action button for the NavActionBar
 * Issue #5038 — ActionBar Component
 *
 * Renders a single NavAction as a button with:
 * - Primary (filled) or secondary/ghost styling
 * - Icon + label layout
 * - Badge overlay support
 * - aria-disabled pattern (consistent with QuickAction)
 * - Optional tooltip for disabled state
 */

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import type { NavAction } from '@/types/navigation';
import { cn } from '@/lib/utils';

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ActionBarButtonProps {
  action: NavAction;
  /** Compact mode for mobile (smaller text) */
  compact?: boolean;
}

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * ActionBarButton
 *
 * Single action button in the NavActionBar.
 * Uses aria-disabled instead of native disabled to keep keyboard focusable.
 */
export function ActionBarButton({ action, compact = false }: ActionBarButtonProps) {
  const Icon = action.icon;
  const isDisabled = action.disabled ?? false;

  const button = (
    <button
      type="button"
      onClick={isDisabled ? undefined : action.onClick}
      aria-disabled={isDisabled || undefined}
      aria-label={action.label}
      data-testid={`action-bar-btn-${action.id}`}
      className={cn(
        // Base layout
        'relative inline-flex items-center gap-2 rounded-lg px-3 py-2',
        'text-sm font-medium',
        'transition-all duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        // Compact (mobile) adjustments
        compact && 'px-2.5 py-1.5 text-xs',
        // Variant: primary
        action.variant === 'primary' && [
          'bg-primary text-primary-foreground',
          'hover:bg-primary/90',
          isDisabled && 'opacity-50 cursor-not-allowed hover:bg-primary',
        ],
        // Variant: secondary (default)
        (action.variant === 'secondary' || !action.variant) && [
          'bg-secondary text-secondary-foreground',
          'hover:bg-secondary/80',
          isDisabled && 'opacity-50 cursor-not-allowed hover:bg-secondary',
        ],
        // Variant: ghost
        action.variant === 'ghost' && [
          'text-foreground/80 hover:bg-accent hover:text-foreground',
          isDisabled && 'opacity-50 cursor-not-allowed hover:bg-transparent hover:text-foreground/80',
        ],
      )}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span className={cn('truncate', compact && 'max-w-[64px]')}>{action.label}</span>

      {/* Badge */}
      {action.badge !== undefined && action.badge !== null && (
        <span
          className={cn(
            'absolute -top-1 -right-1',
            'inline-flex h-4 min-w-[1rem] items-center justify-center',
            'rounded-full px-1 text-[10px] font-semibold tabular-nums',
            'bg-destructive text-destructive-foreground',
          )}
          aria-label={`${action.badge} items`}
        >
          {action.badge}
        </span>
      )}
    </button>
  );

  // Wrap disabled button with tooltip if disabledTooltip is provided
  if (isDisabled && action.disabledTooltip) {
    return (
      <TooltipProvider delayDuration={300}>
        <Tooltip>
          <TooltipTrigger asChild>{button}</TooltipTrigger>
          <TooltipContent side="top" sideOffset={8}>
            <p>{action.disabledTooltip}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  return button;
}
