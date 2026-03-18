/**
 * FocusedCardArea - Swipeable center area for card-focus mobile layout
 *
 * Wraps a single MeepleCard in a swipeable container with Framer Motion
 * drag gestures. Displays swipe arrow indicators when prev/next cards exist.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

'use client';

import { type ReactNode } from 'react';

import { motion, useAnimation, type PanInfo } from 'framer-motion';
import { ChevronLeft, ChevronRight } from 'lucide-react';

import { cn } from '@/lib/utils';

interface FocusedCardAreaProps {
  children: ReactNode;
  onSwipeLeft: () => void;
  onSwipeRight: () => void;
  hasPrev?: boolean;
  hasNext?: boolean;
  className?: string;
}

const SWIPE_THRESHOLD = 50;

export function FocusedCardArea({
  children,
  onSwipeLeft,
  onSwipeRight,
  hasPrev = false,
  hasNext = false,
  className,
}: FocusedCardAreaProps) {
  const controls = useAnimation();

  const handleDragEnd = (_: unknown, info: PanInfo) => {
    const { offset } = info;
    if (offset.x < -SWIPE_THRESHOLD && hasNext) {
      onSwipeLeft();
    } else if (offset.x > SWIPE_THRESHOLD && hasPrev) {
      onSwipeRight();
    }
    controls.start({ x: 0, transition: { type: 'spring', stiffness: 300, damping: 30 } });
  };

  return (
    <div
      className={cn('relative flex-1 flex items-center justify-center overflow-hidden', className)}
      data-testid="focused-card-area"
    >
      {/* Prev indicator */}
      {hasPrev && (
        <button
          onClick={onSwipeRight}
          className="absolute left-2 z-10 p-1.5 rounded-full bg-card/80 backdrop-blur-sm border border-border/30 text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Previous card"
        >
          <ChevronLeft className="w-5 h-5" />
        </button>
      )}

      {/* Swipeable content */}
      <motion.div
        drag="x"
        dragConstraints={{ left: 0, right: 0 }}
        onDragEnd={handleDragEnd}
        animate={controls}
        className="w-full max-w-md px-4"
      >
        {children}
      </motion.div>

      {/* Next indicator */}
      {hasNext && (
        <button
          onClick={onSwipeLeft}
          className="absolute right-2 z-10 p-1.5 rounded-full bg-card/80 backdrop-blur-sm border border-border/30 text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Next card"
        >
          <ChevronRight className="w-5 h-5" />
        </button>
      )}
    </div>
  );
}
