'use client';

import type { MouseEvent } from 'react';

import { cn } from '@/lib/utils';

interface MiniCardDeckProps {
  covers: string[]; // image URLs
  count?: number; // total count (may differ from covers.length)
  onClick?: () => void;
  className?: string;
}

export function MiniCardDeck({ covers, count, onClick, className }: MiniCardDeckProps) {
  const displayCovers = covers.slice(0, 4);
  const total = count ?? covers.length;

  const handleClick = (e: MouseEvent) => {
    e.stopPropagation(); // Prevent parent card click
    onClick?.();
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn('relative flex items-center', className)}
      aria-label={`${total} carte. Clicca per sfogliare.`}
    >
      {displayCovers.map((src, i) => (
        <div
          key={i}
          className="w-10 h-14 rounded-md overflow-hidden border-2 border-background shadow-sm flex-shrink-0"
          style={{ marginLeft: i > 0 ? '-8px' : 0, zIndex: displayCovers.length - i }}
        >
          <img src={src} alt="" className="w-full h-full object-cover" />
        </div>
      ))}
      {total > displayCovers.length && (
        <span className="ml-1 text-xs font-medium text-muted-foreground">
          +{total - displayCovers.length}
        </span>
      )}
    </button>
  );
}
