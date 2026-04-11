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
  BookOpen,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { MeepleEntityType, CardStatus } from '@/components/ui/data-display/meeple-card/types';
import { cn } from '@/lib/utils';
import {
  useContextualHandStore,
  selectCurrentSession,
  selectCreateResult,
  selectContext,
  selectKbReadiness,
} from '@/stores/contextual-hand';

// ─── Types ────────────────────────────────────────────────────────────────

export type HandSlotType = 'game' | 'agent' | 'toolkit' | 'session' | 'kb';

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
    case 'kb':
      return <BookOpen className="h-4 w-4" />;
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
    case 'kb':
      return 'Knowledge Base';
  }
}

function _slotEntity(slotType: HandSlotType): MeepleEntityType {
  switch (slotType) {
    case 'game':
      return 'game';
    case 'agent':
      return 'agent';
    case 'toolkit':
      return 'toolkit';
    case 'session':
      return 'session';
    case 'kb':
      return 'kb';
  }
}

// ─── Component ────────────────────────────────────────────────────────────

export function ContextualHandSlot({ slotType, collapsed, className }: ContextualHandSlotProps) {
  const router = useRouter();
  const context = useContextualHandStore(selectContext);
  const currentSession = useContextualHandStore(selectCurrentSession);
  const createResult = useContextualHandStore(selectCreateResult);
  const kbReadiness = useContextualHandStore(selectKbReadiness);
  const pauseSession = useContextualHandStore(s => s.pauseSession);
  const resumeSession = useContextualHandStore(s => s.resumeSession);
  const rollDice = useContextualHandStore(s => s.rollDice);
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
            onClick={() => router.push(`/chat?agentId=${agentId}`)}
            className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary/10 px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20 min-h-[44px]"
          >
            <MessageSquare className="h-3.5 w-3.5" />
            Chiedi
          </button>
        )}
      </div>
    );
  }

  if (slotType === 'kb') {
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

  // toolkit
  const toolkitId = createResult?.toolkitId;
  const firstParticipantId = createResult?.participants?.[0]?.id;
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
          disabled={!firstParticipantId}
          title={!firstParticipantId ? 'Avvia la partita' : undefined}
          onClick={async () => {
            if (!firstParticipantId) return;
            await rollDice(firstParticipantId, '2d6');
          }}
          className="flex w-full items-center justify-center gap-1.5 rounded-md bg-primary/10 px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20 min-h-[44px] disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Dice5 className="h-3.5 w-3.5" />
          Tira dado
        </button>
      )}
    </div>
  );
}
