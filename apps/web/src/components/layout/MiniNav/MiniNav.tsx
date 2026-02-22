'use client';

/**
 * MiniNav — Horizontal scrollable L2 navigation bar
 * Issue #5037 — MiniNav Component
 *
 * Renders tabs declared via NavigationContext (useSetNavConfig).
 * Positioned sticky below the Navbar (48px height on desktop, 44px on mobile).
 *
 * Behavior:
 * - Reads `miniNavTabs` from useNavigation()
 * - Returns null when there are no tabs (zero layout impact)
 * - Horizontal scroll on mobile (scrollbar hidden)
 * - Scroll-to-active-tab on mount and on pathname change
 * - Active tab detected via usePathname() prefix-matching
 *
 * Desktop optimization:
 * - If tabs > 8: shows L/R scroll arrow buttons
 * - Otherwise: clips (no arrows needed)
 *
 * ARIA:
 * - role="tablist" on the scroll container
 * - role="tab" + aria-selected on each tab (MiniNavTab)
 */

import { useRef, useEffect, useCallback, useState } from 'react';

import { ChevronLeft, ChevronRight } from 'lucide-react';
import { usePathname, useSearchParams } from 'next/navigation';

import { useNavigation } from '@/context/NavigationContext';
import { Button } from '@/components/ui/primitives/button';
import { NAV_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

import { MiniNavTab } from './MiniNavTab';

// ─── Constants ────────────────────────────────────────────────────────────────

/** Show scroll arrows when tabs exceed this count */
const ARROW_THRESHOLD = 8;

/** Scroll amount per arrow click (px) */
const SCROLL_STEP = 120;

// ─── Props ────────────────────────────────────────────────────────────────────

export interface MiniNavProps {
  /** Additional className for the outer wrapper */
  className?: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Determine if a tab href matches the current pathname + search.
 *
 * Query-param tabs (e.g. href="/library?tab=wishlist"):
 *   Active only when pathname matches AND the exact query param is present.
 *
 * Path-only tabs (e.g. href="/library"):
 *   Active when pathname matches AND no ?tab= param overrides it.
 *   Falls back to prefix-match for nested routes.
 */
function isTabActive(href: string, pathname: string, search: string): boolean {
  const [hrefPath, hrefQuery] = href.split('?');
  if (hrefQuery) {
    // Query-param tab: both path and query must match exactly
    return pathname === hrefPath && search === `?${hrefQuery}`;
  }
  // Path-only tab: suppress if a ?tab= param is active (another tab owns it)
  const currentParams = new URLSearchParams(search.replace(/^\?/, ''));
  if (currentParams.has('tab')) return false;
  if (hrefPath === '/') return pathname === '/';
  return pathname.startsWith(hrefPath + '/') || pathname === hrefPath;
}

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * MiniNav
 *
 * Sticky horizontal tab bar driven by NavigationContext.
 * Designed to be placed in the `miniNav` slot of <LayoutShell>.
 */
export function MiniNav({ className }: MiniNavProps) {
  const { miniNavTabs } = useNavigation();
  const pathname = usePathname();
  const rawSearchParams = useSearchParams();
  const search = rawSearchParams.toString() ? `?${rawSearchParams.toString()}` : '';
  const scrollRef = useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  const showArrows = miniNavTabs.length > ARROW_THRESHOLD;

  // ── Scroll state tracking ─────────────────────────────────────────────────

  const updateScrollState = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    setCanScrollLeft(el.scrollLeft > 0);
    setCanScrollRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 1);
  }, []);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    updateScrollState();
    el.addEventListener('scroll', updateScrollState, { passive: true });
    window.addEventListener('resize', updateScrollState, { passive: true });
    return () => {
      el.removeEventListener('scroll', updateScrollState);
      window.removeEventListener('resize', updateScrollState);
    };
  }, [updateScrollState, miniNavTabs]);

  // ── Scroll to active tab on mount / tab change ─────────────────────────────

  useEffect(() => {
    const container = scrollRef.current;
    if (!container) return;

    const activeIndex = miniNavTabs.findIndex((tab) =>
      isTabActive(tab.href, pathname, search)
    );
    if (activeIndex === -1) return;

    // Find the active tab element and scroll it into view
    const tabEls = container.querySelectorAll('[role="tab"]');
    const activeEl = tabEls[activeIndex] as HTMLElement | undefined;
    if (!activeEl) return;

    activeEl.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
  }, [pathname, search, miniNavTabs]);

  // ── Arrow scroll handlers ─────────────────────────────────────────────────

  function scrollLeft() {
    scrollRef.current?.scrollBy({ left: -SCROLL_STEP, behavior: 'smooth' });
  }

  function scrollRight() {
    scrollRef.current?.scrollBy({ left: SCROLL_STEP, behavior: 'smooth' });
  }

  // ── Guard: no tabs → render nothing ──────────────────────────────────────

  if (miniNavTabs.length === 0) return null;

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <div
      className={cn(
        'relative flex items-stretch',
        'h-12 md:h-12',
        'border-b border-border/50',
        'bg-background/95 backdrop-blur-sm',
        className,
      )}
      data-testid={NAV_TEST_IDS.miniNav}
    >
      {/* Left scroll arrow (desktop, tabs > ARROW_THRESHOLD) */}
      {showArrows && (
        <Button
          variant="ghost"
          size="icon"
          className={cn(
            'hidden md:flex h-full w-8 shrink-0 rounded-none',
            'transition-opacity duration-150',
            canScrollLeft ? 'opacity-100' : 'opacity-0 pointer-events-none',
          )}
          onClick={scrollLeft}
          aria-label="Scorri tab a sinistra"
          data-testid={NAV_TEST_IDS.miniNavScrollLeft}
          tabIndex={canScrollLeft ? 0 : -1}
        >
          <ChevronLeft className="h-4 w-4" aria-hidden="true" />
        </Button>
      )}

      {/* Scrollable tab list */}
      <div
        ref={scrollRef}
        role="tablist"
        aria-label="Section navigation"
        className={cn(
          'flex flex-1 items-stretch overflow-x-auto',
          // Hide scrollbar on all browsers
          'scrollbar-none',
          '[&::-webkit-scrollbar]:hidden',
          '[-ms-overflow-style:none]',
          '[scrollbar-width:none]',
          // Left/right padding when arrows shown
          showArrows ? 'px-0' : 'px-2',
        )}
        data-testid={NAV_TEST_IDS.miniNavTablist}
      >
        {miniNavTabs.map((tab) => (
          <MiniNavTab
            key={tab.id}
            tab={tab}
            isActive={isTabActive(tab.href, pathname, search)}
          />
        ))}
      </div>

      {/* Right scroll arrow (desktop, tabs > ARROW_THRESHOLD) */}
      {showArrows && (
        <Button
          variant="ghost"
          size="icon"
          className={cn(
            'hidden md:flex h-full w-8 shrink-0 rounded-none',
            'transition-opacity duration-150',
            canScrollRight ? 'opacity-100' : 'opacity-0 pointer-events-none',
          )}
          onClick={scrollRight}
          aria-label="Scorri tab a destra"
          data-testid={NAV_TEST_IDS.miniNavScrollRight}
          tabIndex={canScrollRight ? 0 : -1}
        >
          <ChevronRight className="h-4 w-4" aria-hidden="true" />
        </Button>
      )}
    </div>
  );
}
