'use client';

import { useMemo } from 'react';

import { usePathname } from 'next/navigation';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { useCardHand } from '@/stores/use-card-hand';

export interface ContextualEntity {
  type: MeepleEntityType;
  id: string | null;
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
}

const ENTITY_ROUTE_PATTERNS: Array<{
  pattern: RegExp;
  entityType: MeepleEntityType;
  extractId: (pathname: string) => string | null;
}> = [
  {
    pattern: /^\/library\/games\/([^/]+)$/,
    entityType: 'game',
    extractId: p => p.match(/^\/library\/games\/([^/]+)$/)?.[1] ?? null,
  },
  {
    pattern: /^\/games\/([^/]+)$/,
    entityType: 'game',
    extractId: p => p.match(/^\/games\/([^/]+)$/)?.[1] ?? null,
  },
  {
    pattern: /^\/sessions\/([^/]+)/,
    entityType: 'session',
    extractId: p => p.match(/^\/sessions\/([^/]+)/)?.[1] ?? null,
  },
  {
    pattern: /^\/chat\/([^/]+)$/,
    entityType: 'chatSession',
    extractId: p => {
      const id = p.match(/^\/chat\/([^/]+)$/)?.[1];
      return id === 'new' ? null : (id ?? null);
    },
  },
];

export function useContextualEntity(): ContextualEntity | null {
  const pathname = usePathname();
  const { cards, focusedIdx } = useCardHand();
  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length ? cards[focusedIdx] : null;

  return useMemo(() => {
    if (!pathname) return null;

    for (const { pattern, entityType, extractId } of ENTITY_ROUTE_PATTERNS) {
      if (pattern.test(pathname)) {
        const id = extractId(pathname);
        if (!id) return null;

        const title =
          focusedCard?.id === id
            ? focusedCard.title
            : (cards.find(c => c.id === id)?.title ?? entityType);

        return {
          type: entityType,
          id,
          title,
          icon: ENTITY_NAV_ICONS[entityType] ?? ENTITY_NAV_ICONS.game,
          color: entityColors[entityType]?.hsl ?? '220 70% 50%',
        };
      }
    }

    return null;
  }, [pathname, focusedCard, cards]);
}
