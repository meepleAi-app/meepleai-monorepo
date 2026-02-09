/**
 * Private Game Card Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.3)
 *
 * Simple card for displaying private games with actions.
 */

'use client';

import { Edit2, Trash2, Share2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';

export interface PrivateGameCardProps {
  game: PrivateGameDto;
  onEdit?: (game: PrivateGameDto) => void;
  onDelete?: (gameId: string) => void;
  onPropose?: (game: PrivateGameDto) => void;
}

export function PrivateGameCard({ game, onEdit, onDelete, onPropose }: PrivateGameCardProps) {
  const subtitle = [
    `${game.minPlayers}-${game.maxPlayers} players`,
    game.yearPublished ? `(${game.yearPublished})` : null,
    game.playingTimeMinutes ? `${game.playingTimeMinutes} min` : null,
  ]
    .filter(Boolean)
    .join(' • ');

  return (
    <Card className="relative">
      <Badge variant="secondary" className="absolute top-4 right-4 bg-purple-100 text-purple-700 border-purple-300">
        Private
      </Badge>

      <CardHeader>
        <CardTitle>{game.title}</CardTitle>
        <CardDescription>{subtitle}</CardDescription>
      </CardHeader>

      {(game.description || game.imageUrl) && (
        <CardContent className="space-y-3">
          {game.imageUrl && (
            /* eslint-disable-next-line @next/next/no-img-element */
            <img
              src={game.imageUrl}
              alt={game.title}
              className="w-full h-48 object-cover rounded-md"
            />
          )}
          {game.description && <p className="text-sm text-muted-foreground line-clamp-3">{game.description}</p>}
        </CardContent>
      )}

      <CardContent>
        <div className="flex gap-2 pt-3 border-t">
          {onEdit && (
            <Button variant="outline" size="sm" onClick={() => onEdit(game)} className="flex-1">
              <Edit2 className="h-4 w-4 mr-1" />
              Edit
            </Button>
          )}
          {onPropose && (
            <Button size="sm" onClick={() => onPropose(game)} className="flex-1">
              <Share2 className="h-4 w-4 mr-1" />
              Propose
            </Button>
          )}
          {onDelete && (
            <Button variant="destructive" size="sm" onClick={() => onDelete(game.id)}>
              <Trash2 className="h-4 w-4" />
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
