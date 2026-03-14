// apps/web/src/hooks/use-bottom-nav-actions.ts
'use client';

import { useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { ENTITY_ACTIONS, DEFAULT_ACTIONS } from '@/config/entity-actions';
import { useCardHand } from '@/stores/use-card-hand';

import type { LucideIcon } from 'lucide-react';

export interface BottomNavAction {
  id: string;
  label: string;
  icon: LucideIcon;
  onClick: () => void;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  badge?: number | string;
  hidden?: boolean;
  disabled?: boolean;
}

export function useBottomNavActions(): BottomNavAction[] {
  const { cards, focusedIdx, drawCard } = useCardHand();
  const router = useRouter();

  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length ? cards[focusedIdx] : null;

  return useMemo(() => {
    if (!focusedCard) {
      // No card focused — show default "draw card" actions
      return DEFAULT_ACTIONS.map(def => ({
        id: def.id,
        label: def.label,
        icon: def.icon,
        variant: def.variant,
        onClick: () => {
          if (def.drawCard) {
            drawCard({
              id: `section-${def.id}`,
              entity: def.drawCard.entity,
              title: def.label,
              href: def.drawCard.href,
            });
            router.push(def.drawCard.href);
          }
        },
      }));
    }

    // Card focused — show entity-specific actions
    const entityActions = ENTITY_ACTIONS[focusedCard.entity] ?? ENTITY_ACTIONS.custom;
    return entityActions.map(def => ({
      id: def.id,
      label: def.label,
      icon: def.icon,
      variant: def.variant,
      onClick: () => {
        if (def.route) {
          router.push(`${focusedCard.href}${def.route}`);
        }
        // Action-specific handlers will be provided by page components
        // via a registry pattern in Phase 2
      },
    }));
  }, [focusedCard, drawCard, router]);
}
