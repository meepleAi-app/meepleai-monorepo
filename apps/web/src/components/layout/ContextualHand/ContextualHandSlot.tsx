'use client';

import {
  Gamepad2,
  Bot,
  Wrench,
  Play,
  Pause,
  Dice5,
  MessageSquare,
  Hash,
} from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import { cn } from '@/lib/utils';
import {
  useContextualHandStore,
  selectCurrentSession,
  selectCreateResult,
  selectContext,
} from '@/stores/contextual-hand';

import type { MeepleEntityType, CardStatus } from '@/components/ui/data-display/meeple-card/types';

// ─── Types ────────────────────────────────────────────────────────────────

export type HandSlotType = 'game' | 'agent' | 'toolkit' | 'session';

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
    case 'agent':
      return <Bot className="h-4 w-4" />;
    case 'toolkit':
      return <Wrench className="h-4 w-4" />;
    case 'session':
      return <Hash className="h-4 w-4" />;
  }
}

function slotLabel(slotType: HandSlotType) {
  switch (slotType) {
    case 'game':
      return 'Gioco';
    case 'agent':
      return 'Agente AI';
    case 'toolkit':
      return 'Toolkit';
    case 'session':
      return 'Partita';
  }
}

function slotEntity(slotType: HandSlotType): MeepleEntityType {
  switch (slotType) {
    case 'game':
      return 'game';
    case 'agent':
      return 'agent';
    case 'toolkit':
      return 'toolkit';
    case 'session':
      return 'session';
  }
}

// ─── Component ────────────────────────────────────────────────────────────

export function ContextualHandSlot({ slotType, collapsed, className }: ContextualHandSlotProps) {
  const context = useContextualHandStore(selectContext);
  const currentSession = useContextualHandStore(selectCurrentSession);
  const createResult = useContextualHandStore(selectCreateResult);
  const pauseSession = useContextualHandStore(s => s.pauseSession);
  const resumeSession = useContextualHandStore(s => s.resumeSession);

  // ── Collapsed: icon-only pill ───────────────────────────────────────
  if (collapsed) {
    const hasContent = context !== 'idle' && currentSession;
    return (
      <div
        className={cn(
          'flex h-10 w-10 items-center justify-center rounded-lg transition-colors',
          hasContent
            ? 'bg-primary/10 text-primary'
            : 'text-muted-foreground/50',
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

  if (slotType === 'agent') {
    const agentId = createResult?.agentDefinitionId;
    return (
      <div className={cn('space-y-1.5', className)}>
        <MeepleCard
          entity="agent"
          variant="compact"
          title={agentId ? 'Agente AI' : 'Nessun agente'}
          badge={agentId ? 'Pronto' : '--'}
        />
        {agentId && (
          <button
            className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary/10 px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20 min-h-[44px]"
          >
            <MessageSquare className="h-3.5 w-3.5" />
            Chiedi
          </button>
        )}
      </div>
    );
  }

  // toolkit
  const toolkitId = createResult?.toolkitId;
  return (
    <div className={cn('space-y-1.5', className)}>
      <MeepleCard
        entity="toolkit"
        variant="compact"
        title={toolkitId ? 'Toolkit' : 'Nessun toolkit'}
        badge={toolkitId ? 'Attivo' : '--'}
      />
      {toolkitId && (
        <button
          className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary/10 px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20 min-h-[44px]"
        >
          <Dice5 className="h-3.5 w-3.5" />
          Tira dado
        </button>
      )}
    </div>
  );
}
