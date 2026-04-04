'use client';

import { cn } from '@/lib/utils';

export interface MiniNavTab {
  id: string;
  label: string;
}

interface ContextMiniNavProps {
  tabs: MiniNavTab[];
  activeTab: string;
  onTabChange: (tabId: string) => void;
  className?: string;
}

/**
 * ContextMiniNav — Tier 2 of the 3-tier layout system.
 *
 * Contextual tab bar, sticky below the TopNavbar (top-[52px]).
 * Used on entity detail pages (game, session, player, etc.)
 * to switch between sections (Panoramica, Sessioni, Documenti…).
 *
 * NOT part of the global shell — import directly in the page/layout that needs it.
 */
export function ContextMiniNav({ tabs, activeTab, onTabChange, className }: ContextMiniNavProps) {
  return (
    <div
      className={cn('sticky top-[52px] z-40', 'h-10 bg-card border-b border-border/50', className)}
    >
      <div className="h-full max-w-[1200px] mx-auto px-4 flex items-center gap-1">
        {tabs.map(tab => (
          <button
            key={tab.id}
            type="button"
            onClick={() => onTabChange(tab.id)}
            className={cn(
              'px-4 py-1.5 rounded-md font-body font-semibold text-sm transition-all',
              activeTab === tab.id
                ? 'bg-entity-game/15 text-entity-game'
                : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
    </div>
  );
}
