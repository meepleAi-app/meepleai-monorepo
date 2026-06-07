'use client';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

interface GameNightPlayer {
  id: string;
  userId?: string; // present if User-linked (asse A invariante #16)
  name: string;
  avatar?: string;
  isGuest: boolean;
  rsvpStatus?: 'Pending' | 'Accepted' | 'Declined' | 'Maybe';
}

interface GameNightSessionEntry {
  id: string;
  playOrder: number;
  gameTitle: string;
  status: 'Pending' | 'InProgress' | 'Completed' | 'Skipped';
  winnerName?: string; // MVP display
  scoreDisplay?: string; // formatted polymorphic (e.g., "87 pt", "Win", "3 obiettivi")
}

export interface GameNightDrawerContentProps {
  gameNightId: string;
  title: string;
  date: string; // ISO
  location?: string;
  status: 'Draft' | 'Published' | 'InProgress' | 'Completed' | 'Cancelled';
  players: GameNightPlayer[];
  sessions: GameNightSessionEntry[];
}

const STATUS_LABEL: Record<GameNightDrawerContentProps['status'], string> = {
  Draft: 'Bozza',
  Published: 'Pianificata',
  InProgress: 'In corso',
  Completed: 'Completata',
  Cancelled: 'Annullata',
};

const STATUS_ENTITY_COLOR: Record<GameNightDrawerContentProps['status'], string> = {
  Draft: 'bg-muted text-muted-foreground',
  Published: 'bg-[hsl(var(--c-event))] text-white',
  InProgress: 'bg-[hsl(var(--c-danger))] text-white',
  Completed: 'bg-[hsl(var(--c-game))] text-white',
  Cancelled: 'bg-muted text-muted-foreground',
};

export function GameNightDrawerContent({
  gameNightId: _gameNightId,
  title,
  date,
  location,
  status,
  players,
  sessions,
}: GameNightDrawerContentProps) {
  const pushDrawer = useCascadeNavigationStore(s => s.pushDrawer);

  const handlePlayerClick = (playerId: string) => {
    pushDrawer('player', playerId);
  };

  return (
    <div className="flex h-full flex-col gap-6 p-6" data-testid="game-night-drawer-content">
      {/* Header */}
      <header>
        <h2 className="text-2xl font-bold">{title}</h2>
        <div className="mt-1 flex items-center gap-3 text-sm text-muted-foreground">
          <time dateTime={date}>{formatDateDisplay(date)}</time>
          {location && <span>· {location}</span>}
        </div>
        <span
          data-testid="status-badge"
          className={`mt-2 inline-block rounded-full px-3 py-1 text-xs font-semibold ${STATUS_ENTITY_COLOR[status]}`}
        >
          {STATUS_LABEL[status]}
        </span>
      </header>

      {/* Players list */}
      <section aria-labelledby="players-heading">
        <h3 id="players-heading" className="mb-3 text-sm font-semibold uppercase tracking-wide">
          Giocatori ({players.length})
        </h3>
        <ul className="space-y-2">
          {players.map(player => (
            <li key={player.id}>
              <button
                type="button"
                onClick={() => handlePlayerClick(player.id)}
                disabled={player.isGuest}
                className="flex w-full items-center gap-3 rounded-md p-2 text-left transition-colors hover:bg-muted disabled:cursor-not-allowed disabled:opacity-60"
                data-testid={`player-row-${player.id}`}
              >
                <div className="h-8 w-8 rounded-full bg-muted" aria-hidden>
                  {player.avatar && (
                    <img
                      src={player.avatar}
                      alt=""
                      className="h-full w-full rounded-full object-cover"
                    />
                  )}
                </div>
                <span className="flex-1 font-medium">{player.name}</span>
                {player.isGuest ? (
                  <span className="text-xs uppercase text-muted-foreground">Guest</span>
                ) : (
                  <span className="text-xs uppercase text-[hsl(var(--c-game))]">✓ User</span>
                )}
              </button>
            </li>
          ))}
        </ul>
      </section>

      {/* Sessions list (InProgress + Completed only) */}
      {(status === 'InProgress' || status === 'Completed') && sessions.length > 0 && (
        <section aria-labelledby="sessions-heading">
          <h3 id="sessions-heading" className="mb-3 text-sm font-semibold uppercase tracking-wide">
            Partite ({sessions.length})
          </h3>
          <ul className="space-y-2">
            {sessions
              .slice()
              .sort((a, b) => a.playOrder - b.playOrder)
              .map(session => (
                <li
                  key={session.id}
                  className="rounded-md border p-3"
                  data-testid={`session-row-${session.id}`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-medium">
                      Session {session.playOrder}: {session.gameTitle}
                    </span>
                    <span className="text-xs uppercase text-muted-foreground">
                      {session.status}
                    </span>
                  </div>
                  {(session.winnerName || session.scoreDisplay) && (
                    <div className="mt-1 text-sm text-muted-foreground">
                      {session.winnerName && <span>MVP {session.winnerName}</span>}
                      {session.winnerName && session.scoreDisplay && <span> · </span>}
                      {session.scoreDisplay && <span>{session.scoreDisplay}</span>}
                    </div>
                  )}
                </li>
              ))}
          </ul>
        </section>
      )}

      {/* CTA contextual per status */}
      <footer className="mt-auto flex flex-wrap gap-2 border-t pt-4">
        {status === 'Published' && (
          <>
            <button
              type="button"
              className="rounded-md bg-primary px-4 py-2 text-primary-foreground"
            >
              Modifica RSVP
            </button>
            <button type="button" className="rounded-md border px-4 py-2">
              Cancel
            </button>
          </>
        )}
        {status === 'InProgress' && (
          <>
            <button
              type="button"
              className="rounded-md bg-primary px-4 py-2 text-primary-foreground"
            >
              Aggiungi session
            </button>
            <button type="button" className="rounded-md border px-4 py-2">
              Termina serata
            </button>
          </>
        )}
        {status === 'Completed' && (
          <>
            <button
              type="button"
              className="rounded-md bg-primary px-4 py-2 text-primary-foreground"
            >
              Aggiungi note
            </button>
            <button type="button" className="rounded-md border px-4 py-2">
              Aggiungi foto
            </button>
          </>
        )}
      </footer>
    </div>
  );
}

function formatDateDisplay(iso: string): string {
  const date = new Date(iso);
  return date.toLocaleDateString('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}
