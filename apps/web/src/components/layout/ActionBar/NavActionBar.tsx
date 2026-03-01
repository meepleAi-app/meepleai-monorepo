'use client';

/**
 * NavActionBar — Contextual L3 action bar driven by NavigationContext
 * Issue #5038 — ActionBar Component
 *
 * Renders action buttons declared via useSetNavConfig({ actionBar: [...] }).
 * Renders nothing when the actionBar array is empty (zero layout impact).
 *
 * Layout:
 * - Desktop: sticky to bottom of content area, horizontal button row
 * - Mobile: fixed bottom with safe-area-inset, compact icon+label layout
 *
 * Overflow:
 * - Desktop: max 6 visible actions, rest in "•••" overflow menu
 * - Mobile: max 4 visible actions, rest in "•••" overflow menu
 *
 * Actions:
 * - hidden: true → filtered out entirely
 * - disabled + disabledTooltip → aria-disabled pattern
 * - variant: primary | secondary | ghost
 * - badge: numeric/string overlay
 *
 * ARIA: role="toolbar" + aria-label="Page actions"
 */

import { useNavigation } from '@/context/NavigationContext';
import { cn } from '@/lib/utils';

import { ActionBarButton } from './ActionBarButton';
import { ActionBarOverflow } from './ActionBarOverflow';

// ─── Constants ────────────────────────────────────────────────────────────────

/** Max visible actions on desktop */
const DESKTOP_MAX = 6;
/** Max visible actions on mobile */
const MOBILE_MAX = 4;

// ─── Props ────────────────────────────────────────────────────────────────────

export interface NavActionBarProps {
  /** Additional className for the outer wrapper */
  className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * NavActionBar
 *
 * Sticky/fixed contextual action bar for the L3 slot.
 * Reads actions from NavigationContext; renders nothing when empty.
 * Designed for the `actionBar` slot of <LayoutShell>.
 */
export function NavActionBar({ className }: NavActionBarProps) {
  const { actionBarActions } = useNavigation();

  // Filter out hidden actions
  const visibleActions = actionBarActions.filter((a) => !a.hidden);

  // Return null when no actions are configured
  if (visibleActions.length === 0) return null;

  // Split into visible + overflow for desktop (max 6)
  const desktopVisible = visibleActions.slice(0, DESKTOP_MAX);
  const desktopOverflow = visibleActions.slice(DESKTOP_MAX);

  // Split for mobile (max 4)
  const mobileVisible = visibleActions.slice(0, MOBILE_MAX);
  const mobileOverflow = visibleActions.slice(MOBILE_MAX);

  return (
    <>
      {/* ── Desktop ActionBar ─────────────────────────────────────────────── */}
      <div
        className={cn(
          'hidden md:flex items-center gap-2',
          'sticky bottom-0 z-20',
          'border-t border-border/50',
          'bg-background/95 backdrop-blur-sm',
          'px-4 py-2.5',
          className,
        )}
        role="toolbar"
        aria-label="Page actions"
        data-testid="nav-action-bar-desktop"
      >
        {desktopVisible.map((action) => (
          <ActionBarButton key={action.id} action={action} />
        ))}

        {desktopOverflow.length > 0 && (
          <ActionBarOverflow actions={desktopOverflow} />
        )}
      </div>

      {/* ── Mobile ActionBar ──────────────────────────────────────────────── */}
      <div
        className={cn(
          'flex md:hidden items-center gap-1.5',
          'fixed bottom-0 left-0 right-0 z-40',
          'pb-[env(safe-area-inset-bottom)]',
          'border-t border-border/50',
          'bg-background/95 backdrop-blur-sm',
          'px-3 py-2',
          'shadow-[0_-2px_10px_rgba(0,0,0,0.08)]',
        )}
        role="toolbar"
        aria-label="Page actions"
        data-testid="nav-action-bar-mobile"
      >
        {mobileVisible.map((action) => (
          <ActionBarButton key={action.id} action={action} compact />
        ))}

        {mobileOverflow.length > 0 && (
          <ActionBarOverflow actions={mobileOverflow} />
        )}
      </div>
    </>
  );
}
