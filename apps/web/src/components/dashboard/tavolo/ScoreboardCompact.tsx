'use client';

export interface ScoreboardPlayer {
  id: string;
  name: string;
  initial: string;
  score: number;
  rank: number;
  color: string;
}

interface ScoreboardCompactProps {
  players: ScoreboardPlayer[];
}

function rankLabel(rank: number): string {
  if (rank === 1) return '🏆';
  return `${rank}°`;
}

export function ScoreboardCompact({ players }: ScoreboardCompactProps) {
  const sorted = [...players].sort((a, b) => b.score - a.score);

  return (
    <div data-testid="scoreboard-compact" className="space-y-2">
      <p className="font-quicksand text-xs font-bold uppercase tracking-wide text-muted-foreground">
        Punteggi
      </p>
      {sorted.length === 0 ? (
        <p className="font-nunito text-xs text-muted-foreground">Nessun giocatore</p>
      ) : (
        <div className="flex flex-col gap-1.5">
          {sorted.map(player => (
            <div
              key={player.id}
              className="flex items-center gap-2"
              data-testid={`scoreboard-player-${player.id}`}
            >
              {/* Avatar circle */}
              <div
                className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white"
                style={{ backgroundColor: player.color }}
                aria-label={player.name}
              >
                {player.initial}
              </div>

              {/* Name */}
              <span className="flex-1 truncate font-nunito text-sm text-foreground">
                {player.name}
              </span>

              {/* Score */}
              <span className="font-quicksand text-sm font-bold text-foreground">
                {player.score}
              </span>

              {/* Rank */}
              <span className="w-6 text-right font-nunito text-xs text-muted-foreground">
                {rankLabel(player.rank)}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
