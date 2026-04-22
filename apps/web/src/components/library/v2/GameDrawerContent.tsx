'use client';
import { useState } from 'react';

import { getEntityToken } from '@/components/ui/v2/entity-tokens';

export interface GameDrawerGame {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly description?: string;
  readonly minPlayers?: number;
  readonly maxPlayers?: number;
  readonly playTimeMinutes?: number;
  readonly sessionCount: number;
  readonly chatCount: number;
}

type Tab = 'info' | 'sessions' | 'chat';

export interface GameDrawerContentProps {
  readonly game: GameDrawerGame;
  readonly initialTab?: Tab;
}

const TABS: ReadonlyArray<{ id: Tab; label: string }> = [
  { id: 'info', label: 'Info' },
  { id: 'sessions', label: 'Sessioni' },
  { id: 'chat', label: 'Chat' },
];

export function GameDrawerContent({
  game,
  initialTab = 'info',
}: GameDrawerContentProps): React.JSX.Element {
  const [tab, setTab] = useState<Tab>(initialTab);
  const t = getEntityToken('game');

  return (
    <div className="flex flex-col gap-4">
      <header className={`-mx-4 -mt-4 px-4 py-6 ${t.bgSoft}`}>
        <div className={`mb-1 text-xs ${t.text}`}>
          {t.emoji} {t.label}
        </div>
        <h2 className="font-heading text-2xl font-semibold">{game.title}</h2>
        {game.publisher && <p className="text-sm text-muted-foreground">{game.publisher}</p>}
      </header>

      <nav role="tablist" className="flex gap-1 border-b border-border">
        {TABS.map(x => (
          <button
            key={x.id}
            role="tab"
            type="button"
            aria-selected={tab === x.id}
            onClick={() => setTab(x.id)}
            className={`px-3 py-2 text-sm transition border-b-2 ${
              tab === x.id
                ? `${t.border} text-foreground`
                : 'border-transparent text-muted-foreground'
            }`}
          >
            {x.label}
          </button>
        ))}
      </nav>

      <section role="tabpanel" className="text-sm">
        {tab === 'info' && (
          <div className="space-y-3">
            {game.description && <p>{game.description}</p>}
            <dl className="grid grid-cols-2 gap-2 text-xs">
              {game.minPlayers != null && game.maxPlayers != null && (
                <div>
                  <dt className="text-muted-foreground">Giocatori</dt>
                  <dd>
                    {game.minPlayers}–{game.maxPlayers}
                  </dd>
                </div>
              )}
              {game.playTimeMinutes != null && (
                <div>
                  <dt className="text-muted-foreground">Durata</dt>
                  <dd>{game.playTimeMinutes} min</dd>
                </div>
              )}
            </dl>
          </div>
        )}
        {tab === 'sessions' && (
          <p className="text-muted-foreground">{game.sessionCount} sessioni registrate.</p>
        )}
        {tab === 'chat' && (
          <p className="text-muted-foreground">{game.chatCount} thread di chat.</p>
        )}
      </section>
    </div>
  );
}
