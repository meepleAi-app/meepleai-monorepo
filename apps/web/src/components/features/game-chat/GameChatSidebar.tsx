/**
 * GameChatSidebar — sidebar desktop (260px) con game-mini e history rail per quel gioco.
 *
 * #4999 (ADR-098 §5): qui c'era l'interruttore tutor/arbitro. Cambiava l'etichetta e niente altro,
 * perché la risposta segue l'intento salvato sul thread. L'intento ora si legge nell'intestazione.
 * Spec: §3.3
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface ChatHistoryItem {
  readonly id: string;
  readonly question: string;
  readonly when: string;
  readonly active?: boolean;
}

export interface GameChatSidebarProps {
  readonly gameTitle: string;
  readonly gameIcon: string;
  readonly history: readonly ChatHistoryItem[];
  readonly onHistorySelect: (id: string) => void;
  readonly className?: string;
}

export function GameChatSidebar({
  gameTitle,
  gameIcon,
  history,
  onHistorySelect,
  className,
}: GameChatSidebarProps): ReactElement {
  return (
    <aside
      data-slot="game-chat-sidebar"
      className={clsx(
        'flex w-[260px] shrink-0 flex-col gap-3 border-r border-border-light p-3',
        className
      )}
    >
      <div className="flex items-center gap-2 rounded-md border border-border-light bg-card p-3">
        <span
          aria-hidden="true"
          className="flex h-8 w-8 items-center justify-center rounded-sm bg-[hsl(var(--c-game)/0.18)] text-base"
        >
          {gameIcon}
        </span>
        <div>
          <div className="font-bold text-sm leading-tight">{gameTitle}</div>
          <div className="font-mono text-[10px] text-muted-foreground">Chat regole</div>
        </div>
      </div>

      <div>
        <h4 className="mb-2 font-bold text-xs uppercase tracking-wider text-muted-foreground">
          Conversazioni recenti
        </h4>
        {history.length === 0 ? (
          <p className="px-2 text-xs text-muted-foreground">Nessuna conversazione recente.</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {history.map(h => (
              <li key={h.id}>
                <button
                  type="button"
                  onClick={() => onHistorySelect(h.id)}
                  className={clsx(
                    'flex w-full flex-col gap-0.5 rounded-md border px-3 py-2 text-left',
                    h.active
                      ? 'border-[hsl(var(--c-chat)/0.3)] bg-[hsl(var(--c-chat)/0.08)]'
                      : 'border-transparent hover:border-border-light hover:bg-muted'
                  )}
                >
                  <span className="text-xs leading-tight">{h.question}</span>
                  <span className="font-mono text-[10px] text-muted-foreground">{h.when}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </aside>
  );
}
