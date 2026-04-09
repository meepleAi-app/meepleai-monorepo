'use client';

import { useState } from 'react';

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
}

/**
 * Desktop right-panel of the game detail page.
 * Vertical rail on the left (74px) + scrollable content area on the right.
 * Pattern: VSCode sidebar.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4
 */
export function GameTabsPanel({
  gameId,
  initialTab = 'info',
  onTabChange,
  isPrivateGame,
  isNotInLibrary,
}: GameTabsPanelProps) {
  const [activeTab, setActiveTab] = useState<GameTabId>(initialTab);

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
        className="flex w-[74px] flex-col gap-1 border-r border-border bg-muted/30 p-2"
      >
        {GAME_TABS.map(tab => {
          const isActive = tab.id === activeTab;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              id={`game-tab-${tab.id}`}
              aria-selected={isActive}
              aria-controls={`game-tabpanel-${tab.id}`}
              tabIndex={isActive ? 0 : -1}
              onClick={() => handleSelect(tab.id)}
              className={cn(
                'flex flex-col items-center gap-1 rounded-lg border px-2 py-3 transition-colors',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                isActive
                  ? 'border-primary/30 bg-primary/10 text-primary'
                  : 'border-transparent text-muted-foreground hover:bg-muted/60 hover:text-foreground'
              )}
              data-testid={`game-tab-${tab.id}`}
            >
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
