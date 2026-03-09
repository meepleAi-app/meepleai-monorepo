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
import { useResponsive, usePrefersReducedMotion } from '@/hooks/useResponsive';
import { useScrollDirection } from '@/hooks/useScrollDirection';
import { useVirtualKeyboard } from '@/hooks/useVirtualKeyboard';
import { NAV_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

// ─── FloatingActionBar ────────────────────────────────────────────────────────

export interface FloatingActionBarProps {
  className?: string;
}

export function FloatingActionBar({ className }: FloatingActionBarProps) {
  const { actionBarActions } = useNavigation();
  const { isMobile } = useResponsive();
  const scrollDirection = useScrollDirection({ threshold: 50 });
  const prefersReducedMotion = usePrefersReducedMotion();
  const { isKeyboardOpen } = useVirtualKeyboard();

  // Only render visible actions
  const visibleActions = actionBarActions.filter(a => !a.hidden);

  // Hide when no actions configured or keyboard is open
  if (visibleActions.length === 0 || isKeyboardOpen) return null;

  // Auto-hide on scroll down (mobile only)
  const isHiddenByScroll = isMobile && scrollDirection === 'down';

  // Separate primary from secondary for visual hierarchy
  const primaryActions = visibleActions.filter(a => a.variant === 'primary');
  const otherActions = visibleActions.filter(a => a.variant !== 'primary');

  return (
    <div
      data-testid={NAV_TEST_IDS.floatingActionBar}
      aria-hidden={isHiddenByScroll}
      className={cn(
        // Positioning: fixed bottom-center, above MobileTabBar on mobile
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
        // Auto-hide on scroll (mobile only)
        isHiddenByScroll &&
          (prefersReducedMotion ? 'invisible' : 'translate-y-[calc(100%+24px)] opacity-0'),
        // Smooth transition for show/hide
        !prefersReducedMotion && 'transition-[transform,opacity] duration-200 ease-in-out',
        className
      )}
      role="toolbar"
      aria-label="Azioni contestuali"
    >
      {/* Secondary / ghost actions first (left side) */}
      {otherActions.map(action => (
        <ActionButton key={action.id} action={action} compact isMobile={isMobile} />
      ))}

      {/* Divider if both groups present */}
      {otherActions.length > 0 && primaryActions.length > 0 && (
        <div className="w-px h-6 bg-border/60 mx-0.5" aria-hidden="true" />
      )}

      {/* Primary actions (right side, filled) */}
      {primaryActions.map(action => (
        <ActionButton key={action.id} action={action} isMobile={isMobile} />
      ))}
    </div>
  );
}

// ─── ActionButton ──────────────────────────────────────────────────────────────

interface ActionButtonProps {
  action: ReturnType<typeof useNavigation>['actionBarActions'][number];
  compact?: boolean;
  isMobile?: boolean;
}

function ActionButton({ action, compact = false, isMobile = false }: ActionButtonProps) {
  const [showTooltip, setShowTooltip] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const longPressRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const prefersReducedMotion = usePrefersReducedMotion();

  const Icon = action.icon;
  const isPrimary = action.variant === 'primary';
  const isDestructive = action.variant === 'destructive';
  const isDisabled = action.disabled;
  const tooltipId = `action-tooltip-${action.id}`;
  const showInlineLabel = isMobile && compact;

  const tooltipText =
    isDisabled && action.disabledTooltip
      ? action.disabledTooltip
      : !compact
        ? undefined
        : action.label;

  function handleMouseEnter() {
    if (isMobile || !tooltipText) return;
    timeoutRef.current = setTimeout(() => setShowTooltip(true), 400);
  }

  function handleMouseLeave() {
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    setShowTooltip(false);
  }

  // Long-press for disabled tooltip on mobile
  function handleTouchStart() {
    if (!isMobile || !isDisabled || !action.disabledTooltip) return;
    longPressRef.current = setTimeout(() => setShowTooltip(true), 500);
  }

  function handleTouchEnd() {
    if (longPressRef.current) clearTimeout(longPressRef.current);
    if (showTooltip) setTimeout(() => setShowTooltip(false), 2000);
  }

  function handleClick() {
    if (isDisabled) return;
    action.onClick?.();
  }

  return (
    <div
      className="relative"
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onTouchStart={handleTouchStart}
      onTouchEnd={handleTouchEnd}
    >
      <button
        type="button"
        aria-label={action.label}
        aria-disabled={isDisabled}
        aria-describedby={tooltipText ? tooltipId : undefined}
        onClick={handleClick}
        className={cn(
          // Base layout
          'relative flex items-center',
          'rounded-xl',
          'transition-all duration-150',
          // Mobile compact: vertical layout (icon + label below)
          showInlineLabel ? 'flex-col gap-0.5 px-2.5 py-1.5' : 'gap-2',
          // Primary variant
          isPrimary && [
            'bg-primary text-primary-foreground',
            !showInlineLabel && 'px-4 py-2',
            'text-sm font-semibold font-nunito',
            'hover:bg-primary/90 active:scale-95',
            isDisabled && 'opacity-50 cursor-not-allowed hover:bg-primary',
          ],
          // Secondary variant
          action.variant === 'secondary' && [
            'bg-muted text-foreground',
            !showInlineLabel && 'p-2.5',
            'hover:bg-muted/80 active:scale-95',
            isDisabled && 'opacity-50 cursor-not-allowed hover:bg-muted',
          ],
          // Destructive variant
          isDestructive && [
            'bg-destructive/10 text-destructive',
            !showInlineLabel && 'p-2.5',
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

        {/* Mobile compact: inline label below icon (like iOS tab bar) */}
        {showInlineLabel && (
          <span
            className="text-[10px] font-medium font-nunito leading-none whitespace-nowrap"
            data-testid={`action-label-${action.id}`}
          >
            {action.label}
          </span>
        )}

        {/* Desktop: show label for primary or non-compact */}
        {!showInlineLabel && (isPrimary || !compact) && (
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

      {/* Tooltip — desktop hover or mobile long-press for disabled */}
      {showTooltip && (tooltipText || (isMobile && isDisabled && action.disabledTooltip)) && (
        <div
          id={tooltipId}
          role="tooltip"
          className={cn(
            'absolute bottom-full left-1/2 -translate-x-1/2 mb-2',
            'px-2.5 py-1 rounded-lg',
            'bg-foreground text-background',
            'text-xs font-medium font-nunito whitespace-nowrap',
            'pointer-events-none',
            !prefersReducedMotion && 'animate-in fade-in-0 zoom-in-95 duration-100'
          )}
        >
          {tooltipText || action.disabledTooltip}
          {/* Arrow */}
          <div className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-foreground" />
        </div>
      )}
    </div>
  );
}
