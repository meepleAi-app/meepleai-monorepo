/**
 * SharedLibraryGameCard Component (Issue #2614)
 *
 * Read-only game card for public shared library view.
 * Displays game info without any actions or editing capabilities.
 */

'use client';

import Image from 'next/image';

import { Card, CardContent, CardHeader } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import { Star, Calendar, FileText } from 'lucide-react';
import type { SharedLibraryGame } from '@/lib/api/schemas/library.schemas';

interface SharedLibraryGameCardProps {
  game: SharedLibraryGame;
  showNotes?: boolean;
}

export function SharedLibraryGameCard({ game, showNotes = false }: SharedLibraryGameCardProps) {
  return (
    <Card className="overflow-hidden transition-shadow hover:shadow-md">
      <CardHeader className="p-0">
        {/* Game Image */}
        <div className="relative aspect-[4/3] w-full bg-muted">
          {game.imageUrl ? (
            <Image
              src={game.imageUrl}
              alt={game.title}
              fill
              className="object-cover"
              sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
            />
          ) : (
            <div className="flex h-full items-center justify-center">
              <span className="text-4xl text-muted-foreground/50">
                {game.title.charAt(0).toUpperCase()}
              </span>
            </div>
          )}
          {/* Favorite Badge */}
          {game.isFavorite && (
            <Badge
              variant="secondary"
              className="absolute top-2 right-2 gap-1 bg-amber-500/90 text-white"
            >
              <Star className="h-3 w-3 fill-current" />
              Preferito
            </Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-3 p-4">
        {/* Title and Publisher */}
        <div>
          <h3 className="font-semibold text-lg line-clamp-2">{game.title}</h3>
          {game.publisher && (
            <p className="text-sm text-muted-foreground">{game.publisher}</p>
          )}
        </div>

        {/* Year Published */}
        {game.yearPublished && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="h-4 w-4" />
            <span>{game.yearPublished}</span>
          </div>
        )}

        {/* Notes (if included and enabled) */}
        {showNotes && game.notes && (
          <div className="rounded-lg bg-muted/50 p-3">
            <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground mb-1">
              <FileText className="h-3 w-3" />
              Note
            </div>
            <p className="text-sm line-clamp-3">{game.notes}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
