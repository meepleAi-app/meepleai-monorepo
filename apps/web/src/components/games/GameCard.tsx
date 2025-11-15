import React from 'react';
import Link from 'next/link';
import { Game } from '@/lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Users, Clock, Calendar } from 'lucide-react';

interface GameCardProps {
  game: Game;
  onClick?: () => void;
}

export const GameCard = React.memo(function GameCard({ game, onClick }: GameCardProps) {
<<<<<<< HEAD
  // If onClick is provided, use it (for backwards compatibility)
  // Otherwise, default to navigating to the detail page
=======
>>>>>>> f06affe5 (feat(performance): Optimize re-renders and component performance (#1093))
  const hasClickHandler = !!onClick;

  const CardWrapper = ({ children }: { children: React.ReactNode }) => {
    if (hasClickHandler) {
      return (
        <Card
          className="transition-shadow hover:shadow-md cursor-pointer"
          onClick={onClick}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              onClick();
            }
          }}
          aria-label={`Game: ${game.title}`}
        >
          {children}
        </Card>
      );
    }

    return (
      <Link href={`/games/${game.id}`} className="block">
        <Card className="transition-shadow hover:shadow-md cursor-pointer" aria-label={`Game: ${game.title}`}>
          {children}
        </Card>
      </Link>
    );
  };

  return (
    <CardWrapper>
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
    </CardWrapper>
  );
});
