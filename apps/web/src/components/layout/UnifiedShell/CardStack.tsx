'use client';

import { AnimatePresence, motion } from 'framer-motion';
import { ChevronRight, ChevronLeft } from 'lucide-react';

import { useDashboardMode } from '@/components/dashboard';
import { SessionPanel } from '@/components/dashboard/SessionPanel';
import { SessionPanelCollapsed } from '@/components/dashboard/SessionPanelCollapsed';
import { useSwipeGesture } from '@/hooks/useSwipeGesture';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

import { CardStackItem } from './CardStackItem';

export function CardStack() {
  const { cards, focusedIdx, pinnedIds, expandedStack, focusCard, discardCard, toggleExpandStack } =
    useCardHand();
  const { isGameMode } = useDashboardMode();

  const level = expandedStack ? ('card' as const) : ('mini' as const);
  const pinnedCards = cards.filter(c => pinnedIds.has(c.id));
  const dynamicCards = cards.filter(c => !pinnedIds.has(c.id));

  const { handlers: swipeHandlers } = useSwipeGesture({
    onSwipeUp: () => {
      if (focusedIdx < cards.length - 1) focusCard(focusedIdx + 1);
    },
    onSwipeDown: () => {
      if (focusedIdx > 0) focusCard(focusedIdx - 1);
    },
    threshold: 40,
  });

  return (
    <div
      className={cn(
        'flex flex-col h-full border-r border-border/30 bg-background/50 backdrop-blur-sm',
        'transition-all duration-200',
        expandedStack ? 'w-[180px]' : 'w-14'
      )}
      data-testid="card-stack"
      {...swipeHandlers}
    >
      {/* Expand/collapse toggle */}
      <button
        type="button"
        onClick={toggleExpandStack}
        className={cn(
          'flex items-center justify-center py-2',
          'text-muted-foreground/50 hover:text-muted-foreground',
          'transition-colors'
        )}
        aria-label={expandedStack ? 'Collapse card stack' : 'Expand card stack'}
      >
        {expandedStack ? <ChevronLeft className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
      </button>

      {/* Dynamic cards (grow upward — flex-end) */}
      <div
        className="flex-1 flex flex-col justify-end gap-1.5 px-1.5 overflow-y-auto scrollbar-none"
        data-testid="card-stack-dynamic"
      >
        {dynamicCards.map(card => {
          const globalIdx = cards.indexOf(card);
          return (
            <CardStackItem
              key={card.id}
              card={card}
              level={level}
              index={globalIdx}
              isFocused={globalIdx === focusedIdx}
              isPinned={false}
              onFocus={focusCard}
              onDiscard={discardCard}
            />
          );
        })}
      </div>

      {/* Dynamic slot — session panel when in game mode */}
      <AnimatePresence>
        {isGameMode && (
          <motion.div
            layout
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            transition={{ duration: 0.2 }}
            className="px-1.5"
            data-testid="card-stack-dynamic-slot"
          >
            {expandedStack ? <SessionPanel /> : <SessionPanelCollapsed />}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Separator */}
      {pinnedCards.length > 0 && dynamicCards.length > 0 && (
        <div className="mx-2 my-1 border-t border-border/20" />
      )}

      {/* Pinned cards (always at bottom) */}
      <div className="flex flex-col gap-1.5 px-1.5 pb-2 pt-1" data-testid="card-stack-pinned">
        {pinnedCards.map(card => {
          const globalIdx = cards.indexOf(card);
          return (
            <CardStackItem
              key={card.id}
              card={card}
              level={level}
              index={globalIdx}
              isFocused={globalIdx === focusedIdx}
              isPinned={true}
              onFocus={focusCard}
              onDiscard={discardCard}
            />
          );
        })}
      </div>
    </div>
  );
}
