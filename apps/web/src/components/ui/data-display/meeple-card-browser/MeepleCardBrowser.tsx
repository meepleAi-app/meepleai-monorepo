'use client';

/**
 * MeepleCardBrowser - Full-screen overlay with horizontal card carousel
 *
 * Features:
 * - Scroll-snap carousel for swiping between cards
 * - Browser history integration (back button closes)
 * - ESC key handler
 * - Deck stack drawer toggle
 * - Carousel indicator (n/total)
 *
 * @module components/ui/data-display/meeple-card-browser/MeepleCardBrowser
 */

import React, { useEffect, useCallback, useRef } from 'react';

import { motion, AnimatePresence, useMotionValue, useTransform } from 'framer-motion';
import { X, Layers } from 'lucide-react';

import { useCardBrowser } from './CardBrowserContext';
import { DeckStackDrawer } from './DeckStackDrawer';
import { MeepleCard } from '../meeple-card';

export function MeepleCardBrowser() {
  const { isOpen, cards, currentIndex, close, setIndex, origin } = useCardBrowser();
  const [showDeckStack, setShowDeckStack] = React.useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  // Browser history integration — push state when overlay opens
  useEffect(() => {
    if (isOpen) {
      history.pushState({ cardBrowser: true }, '');
    }
  }, [isOpen]);

  // Close overlay on browser back button
  useEffect(() => {
    const handlePopState = () => {
      if (isOpen) close();
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [isOpen, close]);

  // ESC key closes overlay
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        close();
        if (history.state?.cardBrowser) history.back();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, close]);

  // Scroll snap to current index when it changes
  useEffect(() => {
    if (scrollRef.current && isOpen) {
      const container = scrollRef.current;
      const cardWidth = container.offsetWidth;
      container.scrollTo({ left: currentIndex * cardWidth, behavior: 'smooth' });
    }
  }, [currentIndex, isOpen]);

  // Detect scroll-snap settle to update current index after swipe
  const handleScroll = useCallback(() => {
    if (!scrollRef.current) return;
    const container = scrollRef.current;
    const cardWidth = container.offsetWidth;
    if (cardWidth === 0) return;
    const newIndex = Math.round(container.scrollLeft / cardWidth);
    if (newIndex !== currentIndex && newIndex >= 0 && newIndex < cards.length) {
      setIndex(newIndex);
    }
  }, [currentIndex, cards.length, setIndex]);

  const handleClose = useCallback(() => {
    close();
    if (history.state?.cardBrowser) history.back();
  }, [close]);

  // Swipe-down close gesture
  const dragY = useMotionValue(0);
  const overlayOpacity = useTransform(dragY, [0, 200], [1, 0.3]);
  const overlayScale = useTransform(dragY, [0, 200], [1, 0.92]);

  const handleDragEnd = useCallback(
    (_: unknown, info: { offset: { y: number }; velocity: { y: number } }) => {
      if (info.offset.y > 100 || info.velocity.y > 300) handleClose();
    },
    [handleClose]
  );

  if (!isOpen || cards.length === 0) return null;

  const currentCard = cards[currentIndex];

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          key="card-browser-drag"
          className="fixed inset-0 z-50"
          style={{ opacity: overlayOpacity, scale: overlayScale }}
          drag="y"
          dragConstraints={{ top: 0, bottom: 0 }}
          dragElastic={0.4}
          onDrag={(_, info) => dragY.set(Math.max(0, info.offset.y))}
          onDragEnd={handleDragEnd}
        >
          <motion.div
            key="card-browser-overlay"
            className="flex flex-col h-full bg-background/95 backdrop-blur-sm"
            initial={{ opacity: 0, scale: 0.85 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, y: '100%' }}
            transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
            style={origin ? { transformOrigin: `${origin.x}px ${origin.y}px` } : undefined}
            role="dialog"
            aria-modal="true"
            aria-label="Card browser"
          >
          {/* Header bar */}
          <div className="flex items-center justify-between px-4 py-2 border-b border-border/30">
            <button
              onClick={handleClose}
              className="p-2 rounded-full hover:bg-muted min-h-[44px] min-w-[44px] flex items-center justify-center"
              aria-label="Close"
            >
              <X size={20} />
            </button>
            <span className="text-sm text-muted-foreground" data-testid="carousel-indicator">
              {currentIndex + 1}/{cards.length}
            </span>
            <button
              onClick={() => setShowDeckStack(true)}
              className="p-2 rounded-full hover:bg-muted min-h-[44px] min-w-[44px] flex items-center justify-center"
              aria-label="History"
            >
              <Layers size={20} />
            </button>
          </div>

          {/* Carousel container */}
          <div
            ref={scrollRef}
            className="flex-1 overflow-x-auto overflow-y-hidden snap-x snap-mandatory flex"
            style={{ scrollSnapType: 'x mandatory' }}
            onScroll={handleScroll}
            data-testid="carousel-container"
          >
            {cards.map(card => (
              <div
                key={card.id}
                className="w-full flex-shrink-0 snap-center p-4 flex flex-col"
                style={{ scrollSnapAlign: 'center' }}
              >
                <MeepleCard
                  entity={card.entity}
                  variant="expanded"
                  title={card.title}
                  subtitle={card.subtitle}
                  imageUrl={card.imageUrl}
                />
              </div>
            ))}
          </div>

          {/* Deck stack drawer */}
          <DeckStackDrawer
            isOpen={showDeckStack}
            currentCardId={currentCard?.id ?? ''}
            onClose={() => setShowDeckStack(false)}
          />
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
