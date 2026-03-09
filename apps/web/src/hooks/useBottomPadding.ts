/**
 * useBottomPadding - Dynamic content padding based on visible bottom bars
 *
 * Issue #2 from mobile-first-ux-epic.md
 *
 * Calculates appropriate bottom padding for main content area based on:
 * - MobileTabBar visibility (mobile only, always visible on mobile)
 * - FloatingActionBar visibility (has actions registered)
 *
 * Padding scenarios:
 * - Mobile + TabBar + ActionBar: pb-36 (144px) — clears both bars
 * - Mobile + TabBar only:        pb-24 (96px)  — clears tab bar + gap
 * - Desktop + ActionBar:         pb-24 (96px)  — clears action bar
 * - Desktop only:                pb-6  (24px)  — minimal spacing
 */

import { useNavigation } from '@/context/NavigationContext';
import { useResponsive } from '@/hooks/useResponsive';

export type BottomPaddingClass = 'pb-36' | 'pb-24' | 'pb-6';

export function useBottomPadding(): BottomPaddingClass {
  const { isMobile } = useResponsive();
  const { actionBarActions } = useNavigation();

  const hasActionBar = actionBarActions.filter(a => !a.hidden).length > 0;

  if (isMobile) {
    // MobileTabBar is always visible on mobile
    return hasActionBar ? 'pb-36' : 'pb-24';
  }

  // Desktop: no MobileTabBar
  return hasActionBar ? 'pb-24' : 'pb-6';
}
