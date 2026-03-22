'use client';

import React from 'react';

import { LiveTimer } from './LiveTimer';
import { SubContextIcon } from './SubContextIcon';

import type { SheetContext } from '../DashboardEngine';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const ALL_CONTEXTS: SheetContext[] = ['scores', 'rules-ai', 'timer', 'photos', 'players'];

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

interface SessionNavBarProps {
  gameName: string;
  sessionStartedAt: Date;
  isPaused: boolean;
  activeSheet: SheetContext | null;
  onOpenSheet: (context: SheetContext) => void;
  onExit: () => void;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function SessionNavBar({
  gameName,
  sessionStartedAt,
  isPaused,
  activeSheet,
  onOpenSheet,
  onExit,
}: SessionNavBarProps): React.JSX.Element {
  return (
    <nav
      className="flex w-full items-center justify-between gap-2 px-3 py-1"
      aria-label="Session navigation"
      data-testid="session-nav-bar"
    >
      {/* Left section */}
      <div className="flex min-w-0 items-center gap-2">
        <button
          type="button"
          onClick={onExit}
          className="shrink-0 text-sm text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Esci dalla sessione"
          data-testid="session-exit-button"
        >
          ← Esci
        </button>

        <span
          className="min-w-0 max-w-[12rem] truncate text-sm font-semibold text-foreground"
          title={gameName}
          data-testid="session-game-name"
        >
          🎲 {gameName}
        </span>

        <LiveTimer startedAt={sessionStartedAt} isPaused={isPaused} />
      </div>

      {/* Right section — sub-context icons */}
      <div className="flex shrink-0 items-center gap-1" data-testid="session-sub-context-icons">
        {ALL_CONTEXTS.map(ctx => (
          <SubContextIcon
            key={ctx}
            context={ctx}
            isActive={activeSheet === ctx}
            onClick={onOpenSheet}
          />
        ))}
      </div>
    </nav>
  );
}
