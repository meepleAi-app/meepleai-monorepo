'use client';

import React from 'react';

import type { SheetContext } from '../DashboardEngine';

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------

const CONTEXT_CONFIG: Record<SheetContext, { emoji: string; label: string }> = {
  scores: { emoji: '🏆', label: 'Punteggi' },
  'rules-ai': { emoji: '🤖', label: 'Regole AI' },
  timer: { emoji: '⏱', label: 'Timer' },
  photos: { emoji: '📸', label: 'Foto' },
  players: { emoji: '👥', label: 'Giocatori' },
};

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

interface SubContextIconProps {
  context: SheetContext;
  isActive: boolean;
  onClick: (context: SheetContext) => void;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function SubContextIcon({
  context,
  isActive,
  onClick,
}: SubContextIconProps): React.JSX.Element {
  const { emoji, label } = CONTEXT_CONFIG[context];

  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={isActive}
      onClick={() => onClick(context)}
      className={[
        'flex h-7 w-7 items-center justify-center rounded-md text-base transition-all',
        isActive
          ? 'bg-primary text-primary-foreground shadow-[0_0_8px_2px_hsl(25_95%_38%_/_0.6)]'
          : 'text-muted-foreground hover:bg-accent hover:text-foreground',
      ].join(' ')}
      data-testid={`sub-context-icon-${context}`}
    >
      <span aria-hidden="true">{emoji}</span>
    </button>
  );
}
