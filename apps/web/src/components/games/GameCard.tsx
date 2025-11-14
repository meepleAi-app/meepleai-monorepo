import React from 'react';
import { Game } from '@/lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Users, Clock, Calendar } from 'lucide-react';

interface GameCardProps {
  game: Game;
  onClick?: () => void;
}

export function GameCard({ game, onClick }: GameCardProps) {
  const hasClickHandler = !!onClick;

  return (
    <Card
      className={`transition-shadow hover:shadow-md ${hasClickHandler ? 'cursor-pointer' : ''}`}
      onClick={onClick}
      role={hasClickHandler ? 'button' : undefined}
      tabIndex={hasClickHandler ? 0 : undefined}
      onKeyDown={hasClickHandler ? (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      } : undefined}
      aria-label={`Game: ${game.title}`}
    >
      <CardHeader>
        <div className="flex justify-between items-start gap-2">
          <CardTitle className="text-lg font-semibold line-clamp-2">
            {game.title}
          </CardTitle>
          {game.bggId && (
            <Badge variant="secondary" className="shrink-0">
              BGG
            </Badge>
          )}
        </div>
        {game.publisher && (
          <p className="text-sm text-muted-foreground line-clamp-1">
            {game.publisher}
          </p>
        )}
      </CardHeader>

      <CardContent className="space-y-2">
        <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
          {/* Player Count */}
          {(game.minPlayers !== null || game.maxPlayers !== null) && (
            <div className="flex items-center gap-1">
              <Users className="h-4 w-4" />
              <span>
                {game.minPlayers === game.maxPlayers
                  ? `${game.minPlayers}p`
                  : `${game.minPlayers || '?'}-${game.maxPlayers || '?'}p`}
              </span>
            </div>
          )}

          {/* Play Time */}
          {(game.minPlayTimeMinutes !== null || game.maxPlayTimeMinutes !== null) && (
            <div className="flex items-center gap-1">
              <Clock className="h-4 w-4" />
              <span>
                {game.minPlayTimeMinutes === game.maxPlayTimeMinutes
                  ? `${game.minPlayTimeMinutes}min`
                  : `${game.minPlayTimeMinutes || '?'}-${game.maxPlayTimeMinutes || '?'}min`}
              </span>
            </div>
          )}

          {/* Year Published */}
          {game.yearPublished && (
            <div className="flex items-center gap-1">
              <Calendar className="h-4 w-4" />
              <span>{game.yearPublished}</span>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
