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
 * Desktop right-panel of the game detail page.
 * Vertical rail on the left (74px) + scrollable content area on the right.
 * Pattern: VSCode sidebar.
 *
 * #2102 M2 (Epic #2096) — Tabs V2 mockup parity:
 *  - Active state: `--c-game` entity color (bg /0.08 + text-game-text + ring)
 *  - Count pill: top-right overlay badge with tabular-nums (mockup parity)
 *  - Animated selection indicator: 3px border-left absolute, transitions
 *    `top` (offsetTop of active tab) + `height` (offsetHeight) with
 *    `cubic-bezier(.4,0,.2,1) 300ms` to slide between active tabs (vertical
 *    rail orientation adaptation of the mockup's horizontal underline).
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
  // #2102 M2: animated selection indicator (vertical rail). Tracks the
  // offsetTop + offsetHeight of the active tab button so the indicator can
  // CSS-transition between positions on tab change.
  const tabRefs = useRef<Partial<Record<GameTabId, HTMLButtonElement | null>>>({});
  const [indicator, setIndicator] = useState({ top: 0, height: 0 });

  useEffect(() => {
    const el = tabRefs.current[activeTab];
    if (el) {
      setIndicator({ top: el.offsetTop, height: el.offsetHeight });
    }
  }, [activeTab]);

  const handleSelect = (tab: GameTabId) => {
    if (tab === activeTab) return;
    setActiveTab(tab);
    onTabChange?.(tab);
  };

  const tabProps = {
    gameId,
    variant: 'desktop' as const,
    isPrivateGame,
    isNotInLibrary,
  };

  return (
    <div className="flex h-full overflow-hidden rounded-2xl border border-border bg-card shadow-sm">
      {/* Vertical rail */}
      <div
        role="tablist"
        aria-orientation="vertical"
        aria-label="Dettagli gioco"
        className="relative flex w-[74px] flex-col gap-1 border-r border-border bg-muted/30 p-2"
      >
        {/* #2102 M2 animated selection indicator */}
        <span
          aria-hidden="true"
          data-slot="game-tabs-indicator"
          className="pointer-events-none absolute left-0 w-[3px] rounded-r-md bg-[hsl(var(--c-game))]"
          style={{
            top: indicator.top,
            height: indicator.height,
            transition:
              'top 300ms cubic-bezier(0.4,0,0.2,1), height 300ms cubic-bezier(0.4,0,0.2,1)',
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
              className={cn(
                'relative flex flex-col items-center gap-1 rounded-lg px-2 py-3 transition-colors duration-200',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game)/0.4)]',
                isActive
                  ? 'bg-[hsl(var(--c-game)/0.08)] text-[hsl(var(--c-game))]'
                  : 'text-muted-foreground hover:bg-muted/60 hover:text-foreground'
              )}
              data-testid={`game-tab-${tab.id}`}
              data-active={isActive}
            >
              {/* #2102 M2 count pill overlay (top-right corner) */}
              {count != null && (
                <span
                  aria-hidden="true"
                  data-slot="game-tab-count-pill"
                  className={cn(
                    'absolute right-1 top-1 inline-flex min-w-[16px] items-center justify-center rounded-full px-1 py-px',
                    'font-[var(--font-jetbrains)] text-[9px] font-extrabold leading-none tabular-nums',
                    isActive
                      ? 'bg-[hsl(var(--c-game))] text-white'
                      : 'bg-muted text-muted-foreground'
                  )}
                >
                  {count}
                </span>
              )}
              <span className="text-lg" aria-hidden="true">
                {tab.icon}
              </span>
              <span className="text-[9px] font-bold uppercase tracking-wide">{tab.label}</span>
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
