'use client';

import { useEffect, useRef, useState } from 'react';

import { cn } from '@/lib/utils';

import {
  GAME_TABS,
  GameAiChatTab,
  GameHouseRulesTab,
  GameInfoTab,
  GamePartiteTab,
  GameToolboxTab,
  type GameTabId,
} from './tabs';

interface GameTabsPanelProps {
  gameId: string;
  initialTab?: GameTabId;
  onTabChange?: (tab: GameTabId) => void;
  isPrivateGame?: boolean;
  isNotInLibrary?: boolean;
  /**
   * Optional count badge map (#2102 M2). When provided, renders a pill overlay
   * top-right of the tab button. Undefined entries fall back to no pill.
   */
  tabCounts?: Partial<Record<GameTabId, number>>;
}

/**
 * Desktop tabs panel for the game detail page.
 * Horizontal underline tabs (mockup sp3 parity) + scrollable content area.
 *
 * #2105 M7 (Epic #2096) — Layout restructure:
 *   Pre-M7 layout was a SplitView 50/50 with vertical rail (74px) on the
 *   right. Mockup actually ships hero full-width top + horizontal underline
 *   tabs below + tab content full-width below. This panel now matches that:
 *   horizontal tablist with an animated underline indicator that slides
 *   between active tabs (same easing as M2, axis swapped).
 *
 * #2102 M2 (Epic #2096) — Tabs V2 mockup parity:
 *  - Active state: `--c-game` entity color (bg /0.08 + text-game-text)
 *  - Count pill: top-right overlay badge with tabular-nums
 *  - Animated selection indicator: 3px border-bottom absolute, transitions
 *    `left` (offsetLeft) + `width` (offsetWidth) with
 *    `cubic-bezier(.4,0,.2,1) 300ms` to slide between active tabs.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4
 * Mockup: `admin-mockups/design_files/sp3-shared-game-detail.jsx:328 Tabs V2`
 */
export function GameTabsPanel({
  gameId,
  initialTab = 'info',
  onTabChange,
  isPrivateGame,
  isNotInLibrary,
  tabCounts,
}: GameTabsPanelProps) {
  const [activeTab, setActiveTab] = useState<GameTabId>(initialTab);
  // #2105 M7: animated underline indicator (horizontal). Tracks offsetLeft
  // + offsetWidth of the active tab button so the indicator can
  // CSS-transition between positions on tab change.
  const tabRefs = useRef<Partial<Record<GameTabId, HTMLButtonElement | null>>>({});
  const tablistRef = useRef<HTMLDivElement>(null);
  const [indicator, setIndicator] = useState({ left: 0, width: 0 });

  // #2105 M7 review follow-up: ResizeObserver re-measures the indicator on
  // viewport / container layout changes so the underline doesn't freeze in a
  // stale position after browser resize or DevTools open. Re-runs every
  // active-tab change too.
  useEffect(() => {
    const measure = () => {
      const el = tabRefs.current[activeTab];
      if (el) setIndicator({ left: el.offsetLeft, width: el.offsetWidth });
    };
    measure();
    if (typeof ResizeObserver === 'undefined' || !tablistRef.current) return;
    const ro = new ResizeObserver(measure);
    ro.observe(tablistRef.current);
    return () => ro.disconnect();
  }, [activeTab]);

  // #2105 M7 review follow-up: sync activeTab when the parent updates
  // initialTab (e.g. URL searchParam change in App Router). Without this the
  // initial value is frozen for the lifetime of the component.
  useEffect(() => {
    if (initialTab && initialTab !== activeTab) {
      setActiveTab(initialTab);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialTab]);

  const handleSelect = (tab: GameTabId) => {
    if (tab === activeTab) return;
    setActiveTab(tab);
    onTabChange?.(tab);
  };

  // #2105 M7 review fix: WCAG 2.1 §4.1.2 / APG Tabs pattern — horizontal
  // orientation must support ArrowLeft/Right + Home/End keyboard navigation
  // with focus moving + the underline animating along.
  const handleKeyDown = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    const tabs = GAME_TABS.map(t => t.id);
    const idx = tabs.indexOf(activeTab);
    let nextIdx: number | null = null;
    if (e.key === 'ArrowRight') nextIdx = (idx + 1) % tabs.length;
    else if (e.key === 'ArrowLeft') nextIdx = (idx - 1 + tabs.length) % tabs.length;
    else if (e.key === 'Home') nextIdx = 0;
    else if (e.key === 'End') nextIdx = tabs.length - 1;
    if (nextIdx === null) return;
    e.preventDefault();
    const nextId = tabs[nextIdx];
    handleSelect(nextId);
    tabRefs.current[nextId]?.focus();
  };

  const tabProps = {
    gameId,
    variant: 'desktop' as const,
    isPrivateGame,
    isNotInLibrary,
  };

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-2xl border border-border bg-card shadow-sm">
      {/* Horizontal tablist — mockup sp3 parity */}
      <div
        ref={tablistRef}
        role="tablist"
        aria-orientation="horizontal"
        aria-label="Dettagli gioco"
        className="relative flex items-end gap-1 overflow-x-auto border-b border-border bg-muted/30 px-2 pt-2"
      >
        {/* #2105 M7 animated underline indicator */}
        <span
          aria-hidden="true"
          data-slot="game-tabs-indicator"
          className="pointer-events-none absolute bottom-0 h-[3px] rounded-t-md bg-[hsl(var(--c-game))]"
          style={{
            left: indicator.left,
            width: indicator.width,
            transition:
              'left 300ms cubic-bezier(0.4,0,0.2,1), width 300ms cubic-bezier(0.4,0,0.2,1)',
          }}
        />
        {GAME_TABS.map(tab => {
          const isActive = tab.id === activeTab;
          const count = tabCounts?.[tab.id];
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              ref={el => {
                tabRefs.current[tab.id] = el;
              }}
              id={`game-tab-${tab.id}`}
              aria-selected={isActive}
              aria-controls={`game-tabpanel-${tab.id}`}
              tabIndex={isActive ? 0 : -1}
              onClick={() => handleSelect(tab.id)}
              onKeyDown={handleKeyDown}
              className={cn(
                'relative flex shrink-0 items-center gap-2 rounded-t-lg px-4 py-2.5 transition-colors duration-200',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game)/0.4)]',
                isActive
                  ? 'bg-[hsl(var(--c-game)/0.08)] text-[hsl(var(--c-game))]'
                  : 'text-muted-foreground hover:bg-muted/60 hover:text-foreground'
              )}
              data-testid={`game-tab-${tab.id}`}
              data-active={isActive}
            >
              <span className="text-base" aria-hidden="true">
                {tab.icon}
              </span>
              <span className="text-[11px] font-bold uppercase tracking-wide">{tab.label}</span>
              {/* #2102 M2 count pill (inline after label in horizontal orientation) */}
              {count != null && (
                <span
                  aria-hidden="true"
                  data-slot="game-tab-count-pill"
                  className={cn(
                    'inline-flex min-w-[18px] items-center justify-center rounded-full px-1.5 py-px',
                    'font-[var(--font-jetbrains)] text-[10px] font-extrabold leading-none tabular-nums',
                    isActive
                      ? 'bg-[hsl(var(--c-game))] text-white'
                      : 'bg-muted text-muted-foreground'
                  )}
                >
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Content area — id reflects activeTab so aria-controls wiring works */}
      <div id={`game-tabpanel-${activeTab}`} className="flex-1 overflow-y-auto">
        {activeTab === 'info' && <GameInfoTab {...tabProps} />}
        {activeTab === 'aiChat' && <GameAiChatTab {...tabProps} />}
        {activeTab === 'toolbox' && <GameToolboxTab {...tabProps} />}
        {activeTab === 'houseRules' && <GameHouseRulesTab {...tabProps} />}
        {activeTab === 'partite' && <GamePartiteTab {...tabProps} />}
      </div>
    </div>
  );
}
