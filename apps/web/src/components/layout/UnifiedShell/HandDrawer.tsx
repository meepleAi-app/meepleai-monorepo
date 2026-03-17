'use client';

import { useDashboardMode } from '@/components/dashboard';
import { SessionPanel } from '@/components/dashboard/SessionPanel';
import type { HandCard } from '@/stores/use-card-hand';
import { useCardHand } from '@/stores/use-card-hand';

import { HandDrawerCard } from './HandDrawerCard';

interface HandDrawerProps {
  onPlaceholderClick?: (card: HandCard) => void;
}

export function HandDrawer({ onPlaceholderClick }: HandDrawerProps = {}) {
  const { cards, focusedIdx, isHandCollapsed, focusCard, maxHandSize, collapseHand } =
    useCardHand();
  const { isGameMode } = useDashboardMode();

  if (cards.length === 0 || isHandCollapsed) {
    return null;
  }

  return (
    <nav
      role="navigation"
      aria-label="Carte in mano"
      className="bg-white/55 backdrop-blur-[12px] border-b border-[rgba(180,130,80,0.1)] px-2.5 py-1 pb-1.5"
    >
      {/* Grip handle */}
      <button
        type="button"
        aria-label="Espandi/comprimi mano"
        onClick={() => collapseHand()}
        className="mx-auto mb-1.5 block cursor-pointer border-none bg-transparent p-0"
        style={{ width: 36, height: 4, borderRadius: 2, background: 'rgba(180,130,80,0.2)' }}
      />

      {/* Horizontal scroll area */}
      <div
        className="flex gap-1.5 overflow-x-auto"
        style={{
          scrollbarWidth: 'none',
          msOverflowStyle: 'none',
        }}
      >
        {/* Session panel as first item in game mode */}
        {isGameMode && (
          <div className="shrink-0 w-[200px] border-r border-[rgba(180,130,80,0.15)] pr-1.5">
            <SessionPanel />
          </div>
        )}

        {cards.map((card, idx) => (
          <HandDrawerCard
            key={card.id}
            card={card}
            isFocused={idx === focusedIdx}
            onClick={() => focusCard(idx)}
            onPlaceholderClick={onPlaceholderClick}
          />
        ))}
      </div>

      {/* Card count */}
      <div className="text-[8px] text-black/20 font-quicksand text-center mt-0.5">
        {cards.length}/{maxHandSize}
      </div>
    </nav>
  );
}
