/**
 * SmartFAB — Context-Aware Floating Action Button
 * Issue #6 from mobile-first-ux-epic.md
 *
 * A single floating button on mobile that changes icon and action
 * based on the current route context. Hides during fast scroll.
 *
 * Design:
 *   - 56px diameter, bg-primary, rounded-full
 *   - Position: right-4, above MobileTabBar
 *   - Mobile only (md:hidden)
 *   - Hides when FloatingActionBar has actions (avoid duplication)
 *   - Hides during scroll down (reuses useScrollDirection)
 */

'use client';

import { useMemo } from 'react';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { resolveSmartFabAction } from '@/config/smart-fab';
import { useNavigation } from '@/context/NavigationContext';
import { usePrefersReducedMotion } from '@/hooks/useResponsive';
import { useScrollDirection } from '@/hooks/useScrollDirection';
import { cn } from '@/lib/utils';

export function SmartFAB() {
  const pathname = usePathname();
  const scrollDirection = useScrollDirection({ threshold: 50 });
  const prefersReducedMotion = usePrefersReducedMotion();
  const { actionBarActions } = useNavigation();

  const action = useMemo(() => resolveSmartFabAction(pathname), [pathname]);

  // Hide when FloatingActionBar has visible actions (avoid duplication)
  const hasFloatingActions = actionBarActions.filter(a => !a.hidden).length > 0;
  if (hasFloatingActions) return null;

  const isHiddenByScroll = scrollDirection === 'down';
  const Icon = action.icon;

  // Actions starting with # are in-page actions (not navigation)
  const isLink = !action.href.startsWith('#');

  const buttonContent = (
    <>
      <Icon className="h-6 w-6" />
      <span className="sr-only">{action.label}</span>
    </>
  );

  const sharedClassName = cn(
    // Mobile only
    'md:hidden',
    // Position: right side, above MobileTabBar
    'fixed right-4 z-45',
    'bottom-[calc(72px+1rem)]',
    // Size: 56px circle
    'h-14 w-14',
    'flex items-center justify-center',
    'rounded-full',
    // Design: primary color + elevation
    'bg-primary text-primary-foreground',
    'shadow-lg shadow-primary/25',
    // Interaction
    'active:scale-95',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
    // Entrance animation
    'animate-in fade-in-0 zoom-in-75 duration-200',
    // Auto-hide on scroll
    isHiddenByScroll &&
      (prefersReducedMotion ? 'invisible' : 'translate-y-[calc(100%+24px)] opacity-0'),
    !prefersReducedMotion && 'transition-[transform,opacity] duration-200 ease-in-out'
  );

  if (isLink) {
    return (
      <Link
        href={action.href}
        className={sharedClassName}
        aria-label={action.label}
        data-testid="smart-fab"
      >
        {buttonContent}
      </Link>
    );
  }

  return (
    <button
      type="button"
      className={sharedClassName}
      aria-label={action.label}
      data-testid="smart-fab"
      onClick={() => {
        // Haptic feedback
        if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
          navigator.vibrate(10);
        }
      }}
    >
      {buttonContent}
    </button>
  );
}
