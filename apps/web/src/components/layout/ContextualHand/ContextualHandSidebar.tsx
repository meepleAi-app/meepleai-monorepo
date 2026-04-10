'use client';

import { useEffect, useState } from 'react';

import { ChevronLeft, ChevronRight, Hand } from 'lucide-react';

import { cn } from '@/lib/utils';
import {
  useContextualHandStore,
  selectContext,
  selectIsLoading,
} from '@/stores/contextual-hand';

import { ContextualHandSlot } from './ContextualHandSlot';

/**
 * ContextualHandSidebar — desktop right sidebar (md+ only).
 *
 * Layout:
 *   280px expanded  |  52px collapsed (icons only)
 *
 * Reads from useContextualHandStore. Shows 4 slots: session, game, agent, toolkit.
 * Initializes the store on mount to recover any active/paused session.
 */
export function ContextualHandSidebar() {
  const context = useContextualHandStore(selectContext);
  const isLoading = useContextualHandStore(selectIsLoading);
  const initialize = useContextualHandStore(s => s.initialize);
  const [isCollapsed, setIsCollapsed] = useState(false);

  useEffect(() => {
    initialize();
  }, [initialize]);

  const isIdle = context === 'idle';

  return (
    <aside
      data-testid="contextual-hand-sidebar"
      aria-label="La mia mano"
      className={cn(
        'hidden md:flex flex-col shrink-0 sticky top-0 h-dvh z-30',
        'border-l border-[var(--nh-border-default)] bg-[var(--nh-bg-base)]',
        'transition-[width] duration-200 ease-out',
        isCollapsed ? 'w-[52px]' : 'w-[280px]'
      )}
    >
      {/* ── Header ───────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 border-b border-[var(--nh-border-default)] px-2 py-2.5">
        {!isCollapsed && (
          <>
            <Hand className="h-4 w-4 text-primary" />
            <span className="font-quicksand text-sm font-semibold text-[var(--nh-text-default)]">
              La Mia Mano
            </span>
          </>
        )}
        <button
          onClick={() => setIsCollapsed(prev => !prev)}
          className={cn(
            'flex h-7 w-7 items-center justify-center rounded-md transition-colors hover:bg-muted',
            isCollapsed && 'mx-auto'
          )}
          aria-label={isCollapsed ? 'Espandi pannello' : 'Comprimi pannello'}
        >
          {isCollapsed ? (
            <ChevronLeft className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground ml-auto" />
          )}
        </button>
      </div>

      {/* ── Body ─────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto p-2 space-y-2">
        {isLoading ? (
          <div className="flex items-center justify-center py-6">
            <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          </div>
        ) : isIdle && !isCollapsed ? (
          <div className="space-y-1 px-1 py-4 text-center">
            <p className="text-sm text-muted-foreground">Nessuna partita attiva.</p>
            <p className="text-xs text-muted-foreground/70">
              Avvia una partita dalla tua libreria.
            </p>
          </div>
        ) : (
          <>
            <ContextualHandSlot slotType="session" collapsed={isCollapsed} />
            <ContextualHandSlot slotType="game" collapsed={isCollapsed} />
            <ContextualHandSlot slotType="agent" collapsed={isCollapsed} />
            <ContextualHandSlot slotType="toolkit" collapsed={isCollapsed} />
          </>
        )}
      </div>
    </aside>
  );
}
