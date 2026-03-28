'use client';

import React from 'react';
import { cn } from '@/lib/utils';
import { Dice5, BarChart3, MessageCircle, Users } from 'lucide-react';

export type SessionTab = 'game' | 'scores' | 'chat' | 'players';

interface TabItem {
  id: SessionTab;
  label: string;
  icon: React.ElementType;
}

const tabs: TabItem[] = [
  { id: 'game', label: 'Gioco', icon: Dice5 },
  { id: 'scores', label: 'Punteggi', icon: BarChart3 },
  { id: 'chat', label: 'Chiedi', icon: MessageCircle },
  { id: 'players', label: 'Giocatori', icon: Users },
];

export interface SessionBottomNavProps {
  activeTab: SessionTab;
  onTabChange: (tab: SessionTab) => void;
}

export function SessionBottomNav({ activeTab, onTabChange }: SessionBottomNavProps) {
  return (
    <nav
      aria-label="Session navigation"
      className={cn(
        'fixed bottom-0 inset-x-0 z-40',
        'flex items-center justify-around',
        'h-[var(--size-session-bottom-nav)]',
        'bg-[var(--gaming-bg-elevated)] border-t border-[var(--gaming-border-glass)]',
        'safe-area-pb'
      )}
    >
      {tabs.map((tab) => {
        const active = activeTab === tab.id;
        const Icon = tab.icon;
        return (
          <button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            role="tab"
            aria-selected={active}
            className={cn(
              'flex flex-col items-center justify-center gap-0.5 px-4 py-1',
              'text-[10px] font-medium transition-colors',
              active
                ? 'text-[var(--gaming-text-accent)]'
                : 'text-[var(--gaming-text-secondary)]'
            )}
          >
            <Icon className="h-5 w-5" />
            <span>{tab.label}</span>
          </button>
        );
      })}
    </nav>
  );
}
