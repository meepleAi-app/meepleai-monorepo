'use client';

import { Timer, Users, Trophy } from 'lucide-react';

interface LiveSessionContextBarProps {
  gameName?: string;
  gameImageUrl?: string;
  timerDisplay?: string;
  turnNumber?: number;
  playerCount?: number;
  leaderName?: string;
  leaderScore?: number;
  onGameClick?: () => void;
  onPlayersClick?: () => void;
  onScoreClick?: () => void;
}

export function LiveSessionContextBar({
  gameName = 'Sessione',
  gameImageUrl,
  timerDisplay = '0:00:00',
  turnNumber,
  playerCount = 0,
  leaderName,
  leaderScore,
  onGameClick,
  onPlayersClick,
  onScoreClick,
}: LiveSessionContextBarProps) {
  return (
    <div className="flex items-center gap-3 w-full overflow-x-auto text-sm">
      <button
        type="button"
        onClick={onGameClick}
        className="flex items-center gap-1.5 shrink-0 hover:opacity-80 transition-opacity"
      >
        {gameImageUrl && (
          <img src={gameImageUrl} alt="" className="w-7 h-7 rounded-full object-cover" />
        )}
        <span className="font-medium text-foreground truncate max-w-[120px]">{gameName}</span>
      </button>

      <span className="flex items-center gap-1 text-muted-foreground font-mono shrink-0">
        <Timer className="w-3.5 h-3.5" />
        {timerDisplay}
      </span>

      {turnNumber != null && <span className="text-muted-foreground shrink-0">T{turnNumber}</span>}

      <button
        type="button"
        onClick={onPlayersClick}
        className="flex items-center gap-1 text-muted-foreground hover:text-foreground transition-colors shrink-0"
      >
        <Users className="w-3.5 h-3.5" />
        {playerCount}
      </button>

      {leaderName && leaderScore != null && (
        <button
          type="button"
          onClick={onScoreClick}
          className="flex items-center gap-1 text-muted-foreground hover:text-foreground transition-colors shrink-0 ml-auto"
        >
          <Trophy className="w-3.5 h-3.5 text-amber-500" />
          {leaderName}: {leaderScore}
        </button>
      )}
    </div>
  );
}
