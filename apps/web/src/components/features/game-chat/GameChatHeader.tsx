/**
 * GameChatHeader — sticky top dell'orchestrator.
 * Mostra game-icon + titolo + l'intento del thread + (opzionale) subtitle.
 *
 * #4999 (ADR-098 §5): l'intento è quello salvato sul thread, in sola lettura. Prima c'era un
 * «agente» tutor/arbitro che viveva solo nel client: diceva «Tutor» qualunque fosse l'intento
 * con cui il server rispondeva. L'intento si sceglie solo nel selettore della chat desktop.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { ANSWER_INTENT_LABELS, type AnswerIntent } from '@/lib/chat/answer-intent';

export interface GameChatHeaderProps {
  readonly gameTitle: string;
  readonly gameIcon: string;
  readonly intent: AnswerIntent;
  readonly subtitle?: string;
  readonly className?: string;
}

export function GameChatHeader({
  gameTitle,
  gameIcon,
  intent,
  subtitle,
  className,
}: GameChatHeaderProps): ReactElement {
  return (
    <header
      data-testid="game-chat-header"
      data-intent={intent}
      className={clsx('flex items-center gap-3 border-b border-border-light px-4 py-3', className)}
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
            <span className="sr-only">Intento della risposta: </span>
            {ANSWER_INTENT_LABELS[intent]}
          </span>
          {subtitle && <span>· {subtitle}</span>}
        </div>
      </div>
    </header>
  );
}
