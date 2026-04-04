'use client';

import React from 'react';

import { Pause, Play } from 'lucide-react';

import { cn } from '@/lib/utils';

/**
 * Auto-play control button
 */
export function AutoPlayButton({
  isPlaying,
  onClick,
}: {
  isPlaying: boolean;
  onClick: () => void;
}) {
  const Icon = isPlaying ? Pause : Play;

  return (
    <button
      onClick={onClick}
      className={cn(
        'w-10 h-10 rounded-full',
        'flex items-center justify-center',
        'bg-muted/80 hover:bg-muted',
        'text-muted-foreground hover:text-foreground',
        'transition-all duration-200',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
      )}
      aria-label={isPlaying ? 'Pause auto-play' : 'Start auto-play'}
    >
      <Icon className="w-4 h-4" />
    </button>
  );
}
