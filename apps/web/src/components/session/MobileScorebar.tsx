'use client';

interface PlayerScore {
  id: string;
  name: string;
  score: number;
}

interface MobileScorebarProps {
  players: PlayerScore[];
}

export function MobileScorebar({ players }: MobileScorebarProps) {
  return (
    <div
      data-testid="mobile-scorebar"
      className="flex gap-2 px-3 overflow-x-auto scrollbar-none lg:hidden"
      style={{ height: 'var(--mobile-scorebar-height, 52px)' }}
    >
      {players.map(p => (
        <div
          key={p.id}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-card border border-border shrink-0"
        >
          <div className="h-6 w-6 rounded-full bg-primary/20 flex items-center justify-center text-[10px] font-bold">
            {p.name[0]}
          </div>
          <div className="flex flex-col">
            <span className="text-xs font-medium truncate max-w-[60px]">{p.name}</span>
            <span className="text-sm font-bold tabular-nums">{p.score}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
