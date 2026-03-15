// apps/web/src/hooks/use-bottom-nav-actions.ts
'use client';

import { useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { SESSION_QUICK_ACTIONS } from '@/config/entity-actions';
import type { BottomNavActionDef } from '@/config/entity-actions';
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
  const { cards, focusedIdx } = useCardHand();
  const router = useRouter();

  // Detect if user is in a session — used for notification muting
  const isInSession = useCardHand(s => {
    const focused = s.focusedIdx >= 0 ? s.cards[s.focusedIdx] : null;
    return focused?.entity === 'session';
  });

  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length ? cards[focusedIdx] : null;

  return useMemo(() => {
    if (!focusedCard || !isInSession) {
      return [];
    }

    // Session card focused — show session-specific quick actions
    return SESSION_QUICK_ACTIONS.map((def: BottomNavActionDef) => ({
      id: def.id,
      label: def.label,
      icon: def.icon,
      variant: (def.variant === 'primary' ? 'primary' : 'ghost') as BottomNavAction['variant'],
      onClick: () => {
        if (def.route) {
          router.push(`${focusedCard.href}${def.route}`);
        }
      },
    }));
  }, [focusedCard, isInSession, router]);
}
