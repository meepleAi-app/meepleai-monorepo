'use client';

import { Pause, Play } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useSessionStore } from '@/store/session';

interface MobileStatusBarProps {
  gameName: string;
  currentPlayer: string;
}

export function MobileStatusBar({ gameName, currentPlayer }: MobileStatusBarProps) {
  const { status, isPaused, currentTurn, togglePause } = useSessionStore();
  const isLive = status === 'live' || status === 'paused';

  return (
    <div
      data-testid="mobile-status-bar"
      className={cn(
        'flex items-center justify-between px-3 lg:hidden',
        'h-[var(--mobile-status-bar-height,36px)]',
        'bg-card border-b border-border text-sm'
      )}
    >
      <div className="flex items-center gap-2 min-w-0">
        {isLive && (
          <span
            className={cn(
              'inline-flex items-center gap-1 text-xs font-bold',
              isPaused ? 'text-yellow-500' : 'text-green-500'
            )}
          >
            <span
              className={cn(
                'h-2 w-2 rounded-full',
                isPaused ? 'bg-yellow-500' : 'bg-green-500 animate-pulse'
              )}
            />
            {isPaused ? 'PAUSA' : 'LIVE'}
          </span>
        )}
        <span className="font-medium truncate">{gameName}</span>
      </div>

      <div className="flex items-center gap-3 shrink-0">
        <span className="text-xs text-muted-foreground">Turno {currentTurn}</span>
        {isLive && (
          <button
            onClick={togglePause}
            aria-label={isPaused ? 'Riprendi' : 'Pausa'}
            className="p-1 rounded-md hover:bg-muted transition-colors"
          >
            {isPaused ? (
              <Play className="h-4 w-4 text-green-500" />
            ) : (
              <Pause className="h-4 w-4 text-yellow-500" />
            )}
          </button>
        )}
      </div>
    </div>
  );
}
