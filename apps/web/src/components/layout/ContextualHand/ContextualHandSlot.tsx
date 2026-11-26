'use client';

import { Gamepad2, Play, Pause, Hash, BookOpen } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { CardStatus } from '@/components/ui/data-display/meeple-card/types';
import { cn } from '@/lib/utils';
import {
  useContextualHandStore,
  selectCurrentSession,
  selectContext,
  selectKbReadiness,
} from '@/stores/contextual-hand';

// ─── Types ────────────────────────────────────────────────────────────────

/**
 * The slots of the hand. #4780: «Agente» and «Toolkit» are gone — on the mobile bar they stayed empty (#4771), and
 * the hand had nothing else to show in them.
 */
export type HandSlotType = 'game' | 'session' | 'kb';

interface ContextualHandSlotProps {
  slotType: HandSlotType;
  /** Collapsed mode — icon only, no card. */
  collapsed?: boolean;
  className?: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────

function slotIcon(slotType: HandSlotType) {
  switch (slotType) {
    case 'game':
      return <Gamepad2 className="h-4 w-4" />;
    case 'session':
      return <Hash className="h-4 w-4" />;
    case 'kb':
      return <BookOpen className="h-4 w-4" />;
  }
}

function slotLabel(slotType: HandSlotType) {
  switch (slotType) {
    case 'game':
      return 'Gioco';
    case 'session':
      return 'Partita';
    case 'kb':
      return 'Knowledge Base';
  }
}

// ─── Component ────────────────────────────────────────────────────────────

export function ContextualHandSlot({ slotType, collapsed, className }: ContextualHandSlotProps) {
  const context = useContextualHandStore(selectContext);
  const currentSession = useContextualHandStore(selectCurrentSession);
  const kbReadiness = useContextualHandStore(selectKbReadiness);
  const pauseSession = useContextualHandStore(s => s.pauseSession);
  const resumeSession = useContextualHandStore(s => s.resumeSession);
  const checkKbReadiness = useContextualHandStore(s => s.checkKbReadiness);

  // ── Collapsed: icon-only pill ───────────────────────────────────────
  if (collapsed) {
    const hasContent = context !== 'idle' && currentSession;
    return (
      <div
        className={cn(
          'flex h-10 w-10 items-center justify-center rounded-lg transition-colors',
          hasContent ? 'bg-primary/10 text-primary' : 'text-muted-foreground/50',
          className
        )}
        title={slotLabel(slotType)}
      >
        {slotIcon(slotType)}
      </div>
    );
  }

  // ── Idle: empty slot placeholder ────────────────────────────────────
  if (context === 'idle' || !currentSession) {
    return (
      <div
        className={cn(
          'flex items-center gap-2 rounded-lg border border-dashed border-muted-foreground/25 p-2.5 text-muted-foreground',
          className
        )}
      >
        <span className="opacity-50">{slotIcon(slotType)}</span>
        <span className="text-xs">{slotLabel(slotType)}</span>
      </div>
    );
  }

  // ── Slot-specific rendering ─────────────────────────────────────────

  if (slotType === 'session') {
    const isPaused = context === 'paused';
    const statusMap: Record<string, CardStatus> = {
      active: 'active',
      paused: 'paused',
      setup: 'setup',
    };

    return (
      <div className={cn('space-y-1.5', className)}>
        <MeepleCard
          entity="session"
          variant="compact"
          title={`#${currentSession.sessionCode}`}
          status={statusMap[context] ?? 'active'}
          badge={isPaused ? 'In pausa' : 'Attiva'}
        />
        <button
          data-testid="pause-resume-toggle"
          onClick={isPaused ? resumeSession : pauseSession}
          className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary/10 px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20 min-h-[44px]"
        >
          {isPaused ? <Play className="h-3.5 w-3.5" /> : <Pause className="h-3.5 w-3.5" />}
          {isPaused ? 'Riprendi' : 'Pausa'}
        </button>
      </div>
    );
  }

  if (slotType === 'game') {
    return (
      <div className={cn('space-y-1.5', className)}>
        <MeepleCard
          entity="game"
          variant="compact"
          title={currentSession.gameId ? 'Gioco corrente' : 'Nessun gioco'}
          badge={currentSession.gameId ? undefined : '--'}
        />
      </div>
    );
  }

  // kb
  const isReady = kbReadiness?.isReady ?? false;
  return (
    <div className={cn('space-y-1.5', className)}>
      <MeepleCard
        entity="kb"
        variant="compact"
        title="Knowledge Base"
        badge={kbReadiness ? (isReady ? 'Pronta' : 'Non pronta') : '--'}
        status={isReady ? 'active' : undefined}
      />
      {currentSession?.gameId && (
        <button
          onClick={() => checkKbReadiness(currentSession.gameId)}
          className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary/10 px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20 min-h-[44px]"
        >
          <BookOpen className="h-3.5 w-3.5" />
          Vedi stato
        </button>
      )}
    </div>
  );
}
