'use client';

import { useState, useCallback } from 'react';

import { GridCard } from '../variants/GridCard';

import type { Carousel3DProps } from '../types';

export function Carousel3D({
  cards,
  activeIndex: controlledIndex,
  onNavigate,
  className = '',
}: Carousel3DProps) {
  const [internalIndex, setInternalIndex] = useState(0);
  const activeIndex = controlledIndex ?? internalIndex;

  const navigate = useCallback(
    (idx: number) => {
      const clamped = Math.max(0, Math.min(cards.length - 1, idx));
      setInternalIndex(clamped);
      onNavigate?.(clamped);
    },
    [cards.length, onNavigate]
  );

  if (cards.length === 0) return null;

  return (
    <div className={`relative hidden items-center justify-center gap-4 py-8 md:flex ${className}`}>
      <button
        onClick={() => navigate(activeIndex - 1)}
        disabled={activeIndex === 0}
        className="z-10 flex h-10 w-10 items-center justify-center rounded-full bg-white/80 shadow-md backdrop-blur-sm transition-transform hover:scale-110 disabled:opacity-30"
      >
        ‹
      </button>
      <div className="relative flex items-center justify-center" style={{ perspective: '1200px' }}>
        {cards.map((card, i) => {
          const offset = i - activeIndex;
          const isActive = offset === 0;
          const isVisible = Math.abs(offset) <= 1;
          if (!isVisible) return null;
          return (
            <div
              key={card.id ?? i}
              className="absolute transition-all duration-[400ms] ease-out"
              style={{
                transform: isActive
                  ? 'translateX(0) rotateY(0deg) scale(1)'
                  : `translateX(${offset * 35}%) rotateY(${offset * -5}deg) scale(0.85)`,
                zIndex: isActive ? 10 : 5,
                filter: isActive ? 'none' : 'blur(2px)',
                opacity: isActive ? 1 : 0.6,
                pointerEvents: isActive ? 'auto' : 'none',
              }}
              onClick={!isActive ? () => navigate(i) : undefined}
            >
              <GridCard {...card} />
            </div>
          );
        })}
      </div>
      <button
        onClick={() => navigate(activeIndex + 1)}
        disabled={activeIndex === cards.length - 1}
        className="z-10 flex h-10 w-10 items-center justify-center rounded-full bg-white/80 shadow-md backdrop-blur-sm transition-transform hover:scale-110 disabled:opacity-30"
      >
        ›
      </button>
    </div>
  );
}
