'use client';

import { useState } from 'react';

import { Sheet, SheetContent } from '@/components/ui/navigation/sheet';
import { cn } from '@/lib/utils';

import {
  GAME_TABS,
  GameAiChatTab,
  GameHouseRulesTab,
  GameInfoTab,
  GamePartiteTab,
  GameToolboxTab,
  type GameTabId,
} from '../tabs';

interface GameDetailsDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  initialTab?: GameTabId;
  isPrivateGame?: boolean;
  isNotInLibrary?: boolean;
}

/**
 * Mobile top-sheet drawer containing the 5 game detail tabs.
 *
 * Reuses the S4 shared tab component contract (`variant='mobile'`).
 * Slides down from the top (mirroring admin-mockups/mobile-card-layout-mockup.html).
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.5
 */
export function GameDetailsDrawer({
  open,
  onOpenChange,
  gameId,
  initialTab = 'info',
  isPrivateGame,
  isNotInLibrary,
}: GameDetailsDrawerProps) {
  const [activeTab, setActiveTab] = useState<GameTabId>(initialTab);

  const tabProps = {
    gameId,
    variant: 'mobile' as const,
    isPrivateGame,
    isNotInLibrary,
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="top"
        className="h-auto max-h-[85vh] rounded-b-2xl p-0"
        data-testid="game-details-drawer"
      >
        {/* Drawer handle */}
        <div className="mx-auto mt-2 h-1 w-10 rounded-full bg-border" aria-hidden="true" />

        {/* Tab bar */}
        <div
          role="tablist"
          aria-label="Dettagli gioco"
          className="flex gap-1 overflow-x-auto border-b border-border px-3 py-2"
          style={{ scrollbarWidth: 'none' }}
        >
          {GAME_TABS.map(tab => {
            const isActive = tab.id === activeTab;
            return (
              <button
                key={tab.id}
                type="button"
                role="tab"
                id={`game-mobile-tab-${tab.id}`}
                aria-selected={isActive}
                aria-controls={`game-mobile-tabpanel-${tab.id}`}
                onClick={() => setActiveTab(tab.id)}
                className={cn(
                  'flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full px-3 py-1.5 text-xs font-bold transition-colors',
                  'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isActive
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-transparent text-muted-foreground hover:bg-muted/60'
                )}
                data-testid={`game-mobile-tab-${tab.id}`}
              >
                <span aria-hidden="true">{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab content */}
        <div
          id={`game-mobile-tabpanel-${activeTab}`}
          className="flex-1 overflow-y-auto"
          style={{ maxHeight: 'calc(85vh - 100px)' }}
        >
          {activeTab === 'info' && <GameInfoTab {...tabProps} />}
          {activeTab === 'aiChat' && <GameAiChatTab {...tabProps} />}
          {activeTab === 'toolbox' && <GameToolboxTab {...tabProps} />}
          {activeTab === 'houseRules' && <GameHouseRulesTab {...tabProps} />}
          {activeTab === 'partite' && <GamePartiteTab {...tabProps} />}
        </div>
      </SheetContent>
    </Sheet>
  );
}
