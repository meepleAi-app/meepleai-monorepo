'use client';

import { useState } from 'react';

import { Plus, Users } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useGameNightStore } from '@/store/game-night';

import { DealtGameCard } from './DealtGameCard';
import { GameNightTimeline } from './GameNightTimeline';
import { InlineGamePicker } from './InlineGamePicker';

const ROTATIONS = [-2, 1, -1, 2, -0.5, 1.5];

interface GameNightPlanningLayoutProps {
  title: string;
  availableGames?: Array<{
    id: string;
    title: string;
    thumbnailUrl?: string;
    minPlayers?: number;
    maxPlayers?: number;
  }>;
}

export function GameNightPlanningLayout({
  title,
  availableGames = [],
}: GameNightPlanningLayoutProps) {
  const { players, selectedGames, timeline, removeGame, addGame } = useGameNightStore();
  const [showPicker, setShowPicker] = useState(false);

  return (
    <div data-testid="planning-layout" className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6">
      {/* Left column — info, players */}
      <div className="space-y-6">
        <div>
          <h2 className="text-xl font-bold font-quicksand">{title}</h2>
        </div>

        <div>
          <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            <Users className="inline h-4 w-4 mr-1.5" />
            Giocatori ({players.length})
          </h3>
          {players.length === 0 ? (
            <p className="text-sm text-muted-foreground">Invita giocatori per iniziare</p>
          ) : (
            <div className="space-y-2">
              {players.map(p => (
                <div key={p.id} className="flex items-center gap-2 text-sm">
                  <div className="h-6 w-6 rounded-full bg-primary/20 flex items-center justify-center text-[10px] font-bold">
                    {p.name[0]}
                  </div>
                  <span>{p.name}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Right column — table, picker, timeline */}
      <div className="space-y-6">
        {/* Dealt cards area */}
        <div data-testid="dealt-cards-area">
          <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            Sul Tavolo ({selectedGames.length})
          </h3>
          {selectedGames.length === 0 ? (
            <button
              onClick={() => setShowPicker(true)}
              className={cn(
                'w-full h-32 rounded-xl border-2 border-dashed border-border',
                'flex flex-col items-center justify-center gap-2',
                'text-muted-foreground hover:border-primary/30 hover:text-foreground transition-colors'
              )}
            >
              <Plus className="h-6 w-6" />
              <span className="text-sm">Aggiungi giochi al tavolo</span>
            </button>
          ) : (
            <div className="flex flex-wrap gap-4 p-4 rounded-xl bg-muted/30 border border-border min-h-[120px]">
              {selectedGames.map((game, i) => (
                <DealtGameCard
                  key={game.id}
                  game={game}
                  onRemove={removeGame}
                  rotation={ROTATIONS[i % ROTATIONS.length]}
                />
              ))}
              <button
                onClick={() => setShowPicker(true)}
                className="w-[140px] h-[120px] rounded-xl border-2 border-dashed border-border flex items-center justify-center text-muted-foreground hover:border-primary/30 transition-colors"
              >
                <Plus className="h-5 w-5" />
              </button>
            </div>
          )}
        </div>

        {/* Inline picker */}
        {showPicker && (
          <InlineGamePicker
            games={availableGames}
            onSelect={game => {
              addGame(game);
              setShowPicker(false);
            }}
            playerCount={players.length || undefined}
            excludeIds={selectedGames.map(g => g.id)}
          />
        )}

        {/* Timeline */}
        <div>
          <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            Programma
          </h3>
          {timeline.length > 0 ? (
            <GameNightTimeline slots={timeline} />
          ) : (
            <p className="text-sm text-muted-foreground">
              Il programma verrà generato automaticamente
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
