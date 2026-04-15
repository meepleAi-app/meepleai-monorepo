'use client';

import { useState } from 'react';

import { usePathname } from 'next/navigation';
import { useShallow } from 'zustand/react/shallow';

import { useCardHand, selectPinnedCards, selectRecentCards } from '@/lib/stores/card-hand-store';
import { cn } from '@/lib/utils';

import { HandRailCard } from './HandRailCard';

export function HandRail() {
  const [expanded, setExpanded] = useState(false);
  const pathname = usePathname();
  const pinned = useCardHand(useShallow(selectPinnedCards));
  const recent = useCardHand(useShallow(selectRecentCards));

  if (pinned.length === 0 && recent.length === 0) return null;

  return (
    <aside
      data-testid="hand-rail"
      aria-label="Mano di navigazione"
      className={cn(
        'hidden md:flex flex-col flex-shrink-0',
        'h-[calc(100dvh-56px)] sticky top-[56px]',
        'bg-[var(--bg-base)]/95 border-r border-[var(--border-glass)]',
        'transition-[width] duration-200 ease-out overflow-hidden',
        expanded ? 'w-[200px]' : 'w-[64px]'
      )}
      onMouseEnter={() => setExpanded(true)}
      onMouseLeave={() => setExpanded(false)}
    >
      <div className="flex flex-col gap-1 p-2 flex-1 overflow-y-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {pinned.length > 0 && (
          <>
            <span
              className={cn(
                'text-[7.5px] font-[800] tracking-[0.1em] uppercase text-[var(--text-tertiary)] px-[3px] mt-1',
                'transition-opacity duration-150',
                expanded ? 'opacity-100' : 'opacity-0'
              )}
            >
              📌 Fissate
            </span>
            {pinned.map(card => (
              <HandRailCard
                key={card.id}
                card={card}
                expanded={expanded}
                active={pathname === card.href || pathname.startsWith(card.href + '/')}
              />
            ))}
            <div className="h-px bg-[var(--border-glass)] my-1" />
          </>
        )}

        {recent.length > 0 && (
          <>
            <span
              className={cn(
                'text-[7.5px] font-[800] tracking-[0.1em] uppercase text-[var(--text-tertiary)] px-[3px]',
                'transition-opacity duration-150',
                expanded ? 'opacity-100' : 'opacity-0'
              )}
            >
              🕐 Recenti
            </span>
            {recent.map(card => (
              <HandRailCard
                key={card.id}
                card={card}
                expanded={expanded}
                active={pathname === card.href || pathname.startsWith(card.href + '/')}
              />
            ))}
          </>
        )}
      </div>
    </aside>
  );
}
