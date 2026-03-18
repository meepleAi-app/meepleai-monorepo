'use client';

import { Dice5, CheckCircle, Loader2, XCircle } from 'lucide-react';

interface GameWithKbItem {
  gameId: string;
  title: string;
  imageUrl: string | null;
  overallKbStatus: 'ready' | 'processing' | 'failed';
}

interface GameWithKbListProps {
  games: GameWithKbItem[];
  onSelect: (gameId: string) => void;
}

export function GameWithKbList({ games, onSelect }: GameWithKbListProps) {
  return (
    <div className="space-y-1">
      <p className="text-sm font-medium text-muted-foreground mb-2">
        Scegli un gioco con regolamento:
      </p>
      {games.map(game => {
        const isReady = game.overallKbStatus === 'ready';
        const isProcessing = game.overallKbStatus === 'processing';

        return (
          <button
            key={game.gameId}
            onClick={() => isReady && onSelect(game.gameId)}
            aria-disabled={!isReady}
            className={`w-full flex items-center justify-between px-3 py-2 rounded-md text-sm transition-colors
              ${isReady ? 'hover:bg-accent cursor-pointer' : 'opacity-50 cursor-not-allowed'}`}
          >
            <div className="flex items-center gap-2">
              <Dice5 className="h-4 w-4" />
              <span>{game.title}</span>
            </div>
            <div>
              {isReady && <CheckCircle className="h-4 w-4 text-green-600" />}
              {isProcessing && <Loader2 className="h-4 w-4 text-amber-600 animate-spin" />}
              {game.overallKbStatus === 'failed' && <XCircle className="h-4 w-4 text-red-600" />}
            </div>
          </button>
        );
      })}
    </div>
  );
}
