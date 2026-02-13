/**
 * BggPreviewCard - Issue #4141
 */

import { Users, Clock, Star } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { BggGameDetailsDto } from '@/types/bgg';

interface BggPreviewCardProps {
  game: BggGameDetailsDto;
}

export function BggPreviewCard({ game }: BggPreviewCardProps) {
  return (
    <Card className="bg-white/70 backdrop-blur-md border-2 border-amber-200">
      <CardHeader className="pb-3">
        <div className="flex items-start gap-3">
          {game.thumbnail ? (
            <img
              src={game.thumbnail}
              alt={game.name}
              className="w-20 h-20 rounded object-cover flex-shrink-0"
              loading="lazy"
            />
          ) : (
            <div className="w-20 h-20 rounded bg-gray-200 flex items-center justify-center flex-shrink-0">
              <span className="text-xs text-gray-500">No image</span>
            </div>
          )}

          <div className="flex-1 min-w-0">
            <CardTitle className="font-quicksand text-xl text-gray-900 mb-1">
              {game.name}
            </CardTitle>
            <p className="font-nunito text-sm text-gray-600">
              Published: {game.yearPublished}
            </p>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div className="flex items-center gap-2 text-sm">
            <Users className="w-4 h-4 text-gray-500" aria-hidden="true" />
            <span className="font-nunito text-gray-700">
              {game.minPlayers === game.maxPlayers
                ? `${game.minPlayers} players`
                : `${game.minPlayers}-${game.maxPlayers} players`}
            </span>
          </div>

          <div className="flex items-center gap-2 text-sm">
            <Clock className="w-4 h-4 text-gray-500" aria-hidden="true" />
            <span className="font-nunito text-gray-700">{game.playingTime} min</span>
          </div>

          <div className="flex items-center gap-2 text-sm">
            <Star className="w-4 h-4 text-amber-500 fill-amber-500" aria-hidden="true" />
            <span className="font-nunito text-gray-700 font-semibold">
              {game.rating.toFixed(1)} / 10
            </span>
          </div>
        </div>

        <div>
          <Badge variant="secondary" className="bg-amber-100 text-amber-900">
            Age {game.minAge}+
          </Badge>
        </div>

        {game.description && (
          <div>
            <p className="font-nunito text-sm text-gray-700 line-clamp-3">
              {game.description}
            </p>
          </div>
        )}

        {(game.categories || game.mechanics) && (
          <div className="space-y-2 pt-2 border-t border-gray-200">
            {game.categories && game.categories.length > 0 && (
              <div>
                <p className="font-nunito text-xs font-semibold text-gray-600 mb-1">
                  Categories
                </p>
                <div className="flex flex-wrap gap-1">
                  {game.categories.map((category) => (
                    <Badge
                      key={category}
                      variant="outline"
                      className="text-xs bg-white/50"
                    >
                      {category}
                    </Badge>
                  ))}
                </div>
              </div>
            )}

            {game.mechanics && game.mechanics.length > 0 && (
              <div>
                <p className="font-nunito text-xs font-semibold text-gray-600 mb-1">
                  Mechanics
                </p>
                <div className="flex flex-wrap gap-1">
                  {game.mechanics.map((mechanic) => (
                    <Badge
                      key={mechanic}
                      variant="outline"
                      className="text-xs bg-white/50"
                    >
                      {mechanic}
                    </Badge>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        <div className="pt-2 border-t border-gray-200">
          <p className="font-nunito text-xs text-gray-500">
            BoardGameGeek ID: {game.id}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
