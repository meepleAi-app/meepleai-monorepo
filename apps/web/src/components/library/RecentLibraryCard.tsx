/**
 * RecentLibraryCard Component (Issue #2612)
 *
 * Simplified library card for dashboard "Recently Added" widget.
 * Shows cover image, title, favorite badge, and "Added X ago" timestamp.
 * View-only with "Gestisci" CTA linking to library.
 */

'use client';

import Image from 'next/image';
import Link from 'next/link';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Library, Star } from 'lucide-react';
import type { UserLibraryEntry } from '@/lib/api';

export interface RecentLibraryCardProps {
  /** Library entry data */
  game: UserLibraryEntry;
}

/**
 * Compact card for dashboard widget showing recently added games.
 * Links to library page for full management.
 */
export function RecentLibraryCard({ game }: RecentLibraryCardProps) {
  return (
    <Card
      className="hover:shadow-md transition-shadow overflow-hidden"
      data-testid="recent-library-card"
    >
      {/* Cover Image */}
      <div className="relative h-32 bg-muted">
        {game.gameImageUrl ? (
          <Image
            src={game.gameImageUrl}
            alt={game.gameTitle}
            fill
            className="object-cover"
            sizes="(max-width: 768px) 100vw, (max-width: 1024px) 33vw, 20vw"
          />
        ) : (
          <div className="flex items-center justify-center h-full text-muted-foreground">
            <Library className="h-8 w-8" />
          </div>
        )}
        {/* Favorite Badge */}
        {game.isFavorite && (
          <div className="absolute top-2 right-2">
            <Star className="h-5 w-5 fill-yellow-400 text-yellow-400" />
          </div>
        )}
      </div>

      <CardContent className="p-3 space-y-2">
        {/* Title */}
        <h3 className="font-semibold text-sm line-clamp-2" title={game.gameTitle}>
          {game.gameTitle}
        </h3>

        {/* Added Time */}
        <p className="text-xs text-muted-foreground">
          Aggiunto {formatDistanceToNow(new Date(game.addedAt), { addSuffix: true, locale: it })}
        </p>

        {/* Manage CTA */}
        <Button variant="outline" size="sm" className="w-full" asChild>
          <Link href="/library">Gestisci</Link>
        </Button>
      </CardContent>
    </Card>
  );
}
