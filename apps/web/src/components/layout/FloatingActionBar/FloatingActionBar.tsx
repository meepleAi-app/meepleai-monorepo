/**
 * FloatingActionBar — 3-Tier Navigation System, Level 3
 * Issue #5038 — FloatingActionBar Component
 *
 * Floating glassmorphism pill fixed at the bottom-center of the viewport.
 * Actions are registered per-page via useSetNavConfig() and cleared on unmount.
 *
 * Design (Concept 4 — Floating):
 *   - Fixed bottom-center, pill-shaped card
 *   - Glassmorphism: bg-card/80 backdrop-blur-md
 *   - Primary action: filled button (bg-primary)
 *   - Secondary/ghost actions: icon-only with tooltip
 *   - Hides automatically when no actions are configured
 *   - Hidden actions are excluded from render
 *   - Disabled actions show aria-disabled + tooltip on hover
 *
 * Accessibility:
 *   - role="toolbar" with aria-label
 *   - Each button has aria-label from action.label
 *   - Disabled state via aria-disabled (not HTML disabled) for tooltip support
 */

'use client';

import { useRef, useState } from 'react';

import { useNavigation } from '@/context/NavigationContext';
import { NAV_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

// ─── FloatingActionBar ────────────────────────────────────────────────────────

export interface FloatingActionBarProps {
  className?: string;
}

export function FloatingActionBar({ className }: FloatingActionBarProps) {
  const { actionBarActions } = useNavigation();

  // Only render visible actions
  const visibleActions = actionBarActions.filter(a => !a.hidden);

  // Hide when no actions configured
  if (visibleActions.length === 0) return null;

  // Separate primary from secondary for visual hierarchy
  const primaryActions = visibleActions.filter(a => a.variant === 'primary');
  const otherActions = visibleActions.filter(a => a.variant !== 'primary');

  return (
    <div
      data-testid={NAV_TEST_IDS.floatingActionBar}
      className={cn(
        // Positioning: fixed bottom-center
        // On mobile: above MobileTabBar (72px + 1.5rem gap)
        'fixed left-1/2 -translate-x-1/2 z-50',
        'bottom-[calc(72px+1.5rem)] md:bottom-6',
        // Shape
        'rounded-2xl',
        // Glassmorphism card
        'bg-card/85 backdrop-blur-md backdrop-saturate-150',
        'border border-border/60',
        'shadow-lg shadow-black/10',
        // Layout
        'flex items-center gap-1 p-1.5',
        // Fade-in animation
        'animate-in fade-in-0 slide-in-from-bottom-4 duration-300',
        className
      )}
      role="toolbar"
      aria-label="Azioni contestuali"
    >
      {/* Secondary / ghost actions first (left side) */}
      {otherActions.map(action => (
        <ActionButton key={action.id} action={action} compact />
      ))}

      {/* Divider if both groups present */}
      {otherActions.length > 0 && primaryActions.length > 0 && (
        <div className="w-px h-6 bg-border/60 mx-0.5" aria-hidden="true" />
      )}

      {/* Primary actions (right side, filled) */}
      {primaryActions.map(action => (
        <ActionButton key={action.id} action={action} />
      ))}
    </div>
  );
}

// ─── ActionButton ──────────────────────────────────────────────────────────────

interface ActionButtonProps {
  action: ReturnType<typeof useNavigation>['actionBarActions'][number];
  compact?: boolean;
}

function ActionButton({ action, compact = false }: ActionButtonProps) {
  const [showTooltip, setShowTooltip] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const Icon = action.icon;
  const isPrimary = action.variant === 'primary';
  const isDestructive = action.variant === 'destructive';
  const isDisabled = action.disabled;
  const tooltipId = `action-tooltip-${action.id}`;

  const tooltipText =
    isDisabled && action.disabledTooltip
      ? action.disabledTooltip
      : !compact
        ? undefined
        : action.label;

  function handleMouseEnter() {
    if (!tooltipText) return;
    timeoutRef.current = setTimeout(() => setShowTooltip(true), 400);
  }

  function handleMouseLeave() {
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    setShowTooltip(false);
  }

  function handleClick() {
    if (isDisabled) return;
    action.onClick?.();
  }

  return (
    <div className="relative" onMouseEnter={handleMouseEnter} onMouseLeave={handleMouseLeave}>
      <button
        type="button"
        aria-label={action.label}
        aria-disabled={isDisabled}
        aria-describedby={tooltipText ? tooltipId : undefined}
        onClick={handleClick}
        className={cn(
          // Base layout
          'relative flex items-center gap-2',
          'rounded-xl',
          'transition-all duration-150',
          // Primary variant
          isPrimary && [
            'bg-primary text-primary-foreground',
            'px-4 py-2',
            'text-sm font-semibold font-nunito',
            'hover:bg-primary/90 active:scale-95',
            isDisabled && 'opacity-50 cursor-not-allowed hover:bg-primary',
          ],
          // Secondary variant
          action.variant === 'secondary' && [
            'bg-muted text-foreground',
            'p-2.5',
            'hover:bg-muted/80 active:scale-95',
            isDisabled && 'opacity-50 cursor-not-allowed hover:bg-muted',
          ],
          // Destructive variant
          isDestructive && [
            'bg-destructive/10 text-destructive',
            'p-2.5',
            'hover:bg-destructive/20 active:scale-95',
            isDisabled && 'opacity-50 cursor-not-allowed hover:bg-destructive/10',
          ],
          // Ghost (default for compact)
          !isPrimary &&
            !isDestructive &&
            action.variant !== 'secondary' && [
              'text-foreground/70',
              'p-2.5',
              'hover:bg-muted hover:text-foreground active:scale-95',
              isDisabled &&
                'opacity-50 cursor-not-allowed hover:bg-transparent hover:text-foreground/70',
            ]
        )}
      >
        <Icon className={cn('shrink-0', isPrimary ? 'h-4 w-4' : 'h-4 w-4')} />

        {/* Show label for primary or non-compact */}
        {(isPrimary || !compact) && (
          <span className="text-sm font-semibold font-nunito whitespace-nowrap">
            {action.label}
          </span>
        )}

        {/* Badge */}
        {action.badge !== undefined && action.badge !== null && (
          <span
            className={cn(
              'absolute -top-1 -right-1',
              'inline-flex items-center justify-center',
              'h-4 min-w-4 px-1 rounded-full',
              'text-[10px] font-bold leading-none',
              'bg-primary text-primary-foreground'
            )}
          >
            {typeof action.badge === 'number' && action.badge > 99 ? '99+' : action.badge}
          </span>
        )}
      </button>

      {/* Tooltip */}
      {showTooltip && tooltipText && (
        <div
          id={tooltipId}
          role="tooltip"
          className={cn(
            'absolute bottom-full left-1/2 -translate-x-1/2 mb-2',
            'px-2.5 py-1 rounded-lg',
            'bg-foreground text-background',
            'text-xs font-medium font-nunito whitespace-nowrap',
            'pointer-events-none',
            'animate-in fade-in-0 zoom-in-95 duration-100'
          )}
        >
          {tooltipText}
          {/* Arrow */}
          <div className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-foreground" />
        </div>
      )}
    </div>
  );
}
