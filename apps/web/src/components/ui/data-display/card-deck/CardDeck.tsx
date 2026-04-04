'use client';

import { useRef, useState, useCallback, type ReactNode, type TouchEvent } from 'react';

import { cn } from '@/lib/utils';

interface CardDeckProps<T> {
  items: T[];
  activeIndex: number;
  onIndexChange: (index: number) => void;
  renderItem: (item: T, isActive: boolean) => ReactNode;
  emptyState?: ReactNode;
  header?: ReactNode;
  showIndicators?: boolean;
  showArrows?: boolean;
  keyboardNav?: boolean;
  snapThreshold?: number;
  className?: string;
}

export function CardDeck<T>({
  items,
  activeIndex,
  onIndexChange,
  renderItem,
  emptyState,
  header,
  showIndicators = true,
  showArrows = true,
  keyboardNav = true,
  snapThreshold = 0.3,
  className,
}: CardDeckProps<T>) {
  const containerRef = useRef<HTMLDivElement>(null);
  const touchStartX = useRef(0);
  const [dragOffset, setDragOffset] = useState(0);
  const [isDragging, setIsDragging] = useState(false);

  const goTo = useCallback(
    (index: number) => {
      if (index >= 0 && index < items.length) onIndexChange(index);
    },
    [items.length, onIndexChange]
  );

  const handleTouchStart = useCallback((e: TouchEvent) => {
    touchStartX.current = e.touches[0].clientX;
    setIsDragging(true);
  }, []);

  const handleTouchMove = useCallback(
    (e: TouchEvent) => {
      if (!isDragging) return;
      const delta = e.touches[0].clientX - touchStartX.current;
      setDragOffset(delta);
    },
    [isDragging]
  );

  const handleTouchEnd = useCallback(() => {
    setIsDragging(false);
    const containerWidth = containerRef.current?.offsetWidth ?? 300;
    const ratio = Math.abs(dragOffset) / containerWidth;

    if (ratio > snapThreshold) {
      if (dragOffset < 0) goTo(activeIndex + 1);
      else goTo(activeIndex - 1);
    }
    setDragOffset(0);
  }, [dragOffset, snapThreshold, activeIndex, goTo]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (!keyboardNav) return;
      if (e.key === 'ArrowLeft') {
        e.preventDefault();
        goTo(activeIndex - 1);
      }
      if (e.key === 'ArrowRight') {
        e.preventDefault();
        goTo(activeIndex + 1);
      }
      if (e.key === 'Home') {
        e.preventDefault();
        goTo(0);
      }
      if (e.key === 'End') {
        e.preventDefault();
        goTo(items.length - 1);
      }
    },
    [keyboardNav, activeIndex, items.length, goTo]
  );

  if (items.length === 0) {
    return emptyState ? <div className={className}>{emptyState}</div> : null;
  }

  // Render active + adjacent (virtualized: max 3)
  const visibleIndices = [activeIndex - 1, activeIndex, activeIndex + 1].filter(
    i => i >= 0 && i < items.length
  );

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      {header}

      <div
        ref={containerRef}
        className="relative overflow-hidden touch-pan-y"
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
        onKeyDown={handleKeyDown}
        tabIndex={0}
        role="region"
        aria-roledescription="carousel"
        aria-label="Card deck"
      >
        <div
          className={cn(
            'flex transition-transform duration-200 ease-out motion-reduce:transition-none',
            isDragging && 'transition-none'
          )}
          style={{
            transform: `translateX(calc(-${activeIndex * 100}% + ${dragOffset}px))`,
          }}
        >
          {items.map((item, i) => {
            const isVisible = visibleIndices.includes(i);
            return (
              <div
                key={i}
                className="w-full flex-shrink-0 px-1"
                role="tabpanel"
                aria-hidden={i !== activeIndex}
                aria-label={`Card ${i + 1} di ${items.length}`}
              >
                {isVisible ? renderItem(item, i === activeIndex) : <div className="h-32" />}
              </div>
            );
          })}
        </div>

        {/* Arrows (desktop) */}
        {showArrows && items.length > 1 && (
          <>
            <button
              type="button"
              onClick={() => goTo(activeIndex - 1)}
              disabled={activeIndex === 0}
              className="absolute left-1 top-1/2 -translate-y-1/2 hidden md:flex w-8 h-8 items-center justify-center rounded-full bg-background/80 shadow-sm disabled:opacity-30 hover:bg-background transition-colors"
              aria-label="Precedente"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="w-4 h-4"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <polyline points="15 18 9 12 15 6" />
              </svg>
            </button>
            <button
              type="button"
              onClick={() => goTo(activeIndex + 1)}
              disabled={activeIndex === items.length - 1}
              className="absolute right-1 top-1/2 -translate-y-1/2 hidden md:flex w-8 h-8 items-center justify-center rounded-full bg-background/80 shadow-sm disabled:opacity-30 hover:bg-background transition-colors"
              aria-label="Successivo"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="w-4 h-4"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </button>
          </>
        )}
      </div>

      {/* Dot indicators */}
      {showIndicators && items.length > 1 && (
        <div className="flex justify-center gap-1.5" role="tablist">
          {items.map((_, i) => (
            <button
              key={i}
              type="button"
              role="tab"
              aria-selected={i === activeIndex}
              aria-label={`Card ${i + 1} di ${items.length}`}
              onClick={() => goTo(i)}
              className={cn(
                'w-2 h-2 rounded-full transition-colors',
                i === activeIndex ? 'bg-primary' : 'bg-muted-foreground/30'
              )}
            />
          ))}
        </div>
      )}
    </div>
  );
}
