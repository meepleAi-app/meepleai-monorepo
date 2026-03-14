'use client';

/**
 * CardLinkBar - Tabbed related-cards bar below expanded card
 *
 * Shows entity-specific tabs (e.g. Related, Similar, Collections for games)
 * with horizontally scrollable compact cards in each tab.
 * Only renders tabs that have content.
 *
 * @module components/ui/data-display/meeple-card-browser/CardLinkBar
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import { MeepleCard } from '../meeple-card';

import type { MeepleEntityType } from '../meeple-card-styles';
import type { CardRef } from './CardBrowserContext';

interface TabDefinition {
  key: string;
  label: string;
}

const TAB_CONFIG: Record<MeepleEntityType, TabDefinition[]> = {
  game: [
    { key: 'related', label: 'Related' },
    { key: 'similar', label: 'Similar' },
    { key: 'collections', label: 'Collections' },
  ],
  player: [
    { key: 'sessions', label: 'Sessions' },
    { key: 'games', label: 'Games' },
  ],
  session: [
    { key: 'players', label: 'Players' },
    { key: 'game', label: 'Game' },
    { key: 'chat', label: 'Chat' },
  ],
  agent: [
    { key: 'chats', label: 'Chats' },
    { key: 'games', label: 'Games' },
  ],
  kb: [
    { key: 'documents', label: 'Documents' },
    { key: 'game', label: 'Source' },
  ],
  chatSession: [
    { key: 'agent', label: 'Agent' },
    { key: 'game', label: 'Game' },
  ],
  event: [
    { key: 'participants', label: 'Players' },
    { key: 'games', label: 'Games' },
  ],
  toolkit: [{ key: 'agents', label: 'Agents' }],
  tool: [{ key: 'toolbox', label: 'Toolbox' }],
  custom: [{ key: 'links', label: 'Links' }],
};

interface CardLinkBarProps {
  entityType: MeepleEntityType;
  entityId: string;
  /** Map from tab key to cards for that tab */
  tabData: Record<string, CardRef[]>;
  onCardTap: (card: CardRef) => void;
}

export function CardLinkBar({ entityType, tabData, onCardTap }: CardLinkBarProps) {
  const tabs = TAB_CONFIG[entityType] ?? [];
  // Filter to only tabs that have content
  const activeTabs = tabs.filter(tab => (tabData[tab.key]?.length ?? 0) > 0);

  const [activeTabKey, setActiveTabKey] = useState(activeTabs[0]?.key ?? '');

  if (activeTabs.length === 0) return null;

  const currentCards = tabData[activeTabKey] ?? [];

  return (
    <div className="flex flex-col gap-2" data-testid="card-link-bar">
      {/* Tab header */}
      <div
        className="flex overflow-x-auto gap-1 flex-nowrap px-4"
        role="tablist"
        data-testid="card-link-tabs"
      >
        {activeTabs.map(tab => (
          <button
            key={tab.key}
            role="tab"
            aria-selected={activeTabKey === tab.key}
            onClick={() => setActiveTabKey(tab.key)}
            className={cn(
              'px-3 py-1.5 rounded-full text-sm whitespace-nowrap transition-colors',
              activeTabKey === tab.key
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted/50 text-muted-foreground hover:bg-muted'
            )}
            data-testid="card-link-tab"
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Card scroll */}
      <div
        className="flex overflow-x-auto gap-2 flex-nowrap px-4 pb-2"
        role="tabpanel"
        data-testid="card-link-panel"
      >
        {currentCards.slice(0, 10).map(card => (
          <button
            key={card.id}
            onClick={() => onCardTap(card)}
            className="flex-shrink-0"
            data-testid="card-link-item"
          >
            <MeepleCard
              entity={card.entity}
              variant="compact"
              title={card.title}
              subtitle={card.subtitle}
              imageUrl={card.imageUrl}
            />
          </button>
        ))}
      </div>
    </div>
  );
}
