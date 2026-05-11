/**
 * GameChatHeader — sticky top dell'orchestrator.
 * Mostra game-icon + titolo + agent-badge + (opzionale) subtitle.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type AgentKind = 'tutor' | 'arbitro';

export interface GameChatHeaderProps {
  readonly gameTitle: string;
  readonly gameIcon: string;
  readonly agent: AgentKind;
  readonly subtitle?: string;
  readonly className?: string;
}

const AGENT_LABEL: Record<AgentKind, string> = {
  tutor: '🧙 Tutor',
  arbitro: '⚖️ Arbitro',
};

export function GameChatHeader({
  gameTitle,
  gameIcon,
  agent,
  subtitle,
  className,
}: GameChatHeaderProps): ReactElement {
  return (
    <header
      data-testid="game-chat-header"
      data-agent={agent}
      className={clsx(
        'flex items-center gap-3 border-b border-border-light px-4 py-3',
        className
      )}
    >
      <span
        aria-hidden="true"
        className="flex h-9 w-9 items-center justify-center rounded-md bg-[hsl(var(--c-game)/0.15)] text-lg"
      >
        {gameIcon}
      </span>
      <div>
        <div className="font-bold text-base leading-tight">{gameTitle}</div>
        <div className="flex items-center gap-2 font-mono text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-agent)/0.15)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-[hsl(var(--c-agent))]">
            {AGENT_LABEL[agent]}
          </span>
          {subtitle && <span>· {subtitle}</span>}
        </div>
      </div>
    </header>
  );
}
