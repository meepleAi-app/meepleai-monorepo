'use client';

import { useState, type ReactNode } from 'react';

interface FlipCardProps {
  front: ReactNode;
  back: ReactNode;
  trigger?: 'card' | 'button';
  className?: string;
}

export function FlipCard({ front, back, trigger = 'card', className = '' }: FlipCardProps) {
  const [flipped, setFlipped] = useState(false);
  const handleFlip = () => setFlipped(f => !f);

  return (
    <div
      className={`[perspective:1000px] ${className}`}
      onClick={trigger === 'card' ? handleFlip : undefined}
    >
      <div
        className={`relative transition-transform duration-[600ms] [transform-style:preserve-3d] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] ${flipped ? '[transform:rotateY(180deg)]' : ''}`}
      >
        <div className="[backface-visibility:hidden]">
          {front}
          {trigger === 'button' && (
            <button
              onClick={e => {
                e.stopPropagation();
                handleFlip();
              }}
              className="absolute bottom-2 right-2 z-20 flex h-7 w-7 items-center justify-center rounded-full bg-white/85 text-xs shadow-sm backdrop-blur-sm"
            >
              🔄
            </button>
          )}
        </div>
        <div className="absolute inset-0 [backface-visibility:hidden] [transform:rotateY(180deg)]">
          {back}
          <button
            onClick={e => {
              e.stopPropagation();
              handleFlip();
            }}
            className="absolute right-2 top-2 z-20 flex h-7 w-7 items-center justify-center rounded-full bg-white/85 text-xs shadow-sm backdrop-blur-sm"
          >
            ✕
          </button>
        </div>
      </div>
    </div>
  );
}
