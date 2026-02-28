'use client';

import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { BookPlus, Clock, Users, Star, Brain, CalendarDays, ExternalLink } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useAddGameToLibrary } from '@/hooks/queries';
import type { SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

interface GameDiscoverHeroProps {
  game: SharedGameDetail;
}

export function GameDiscoverHero({ game }: GameDiscoverHeroProps) {
  const router = useRouter();
  const addToLibrary = useAddGameToLibrary();

  function handleAddToLibrary() {
    addToLibrary.mutate(
      { gameId: game.id },
      { onSuccess: () => router.push(`/library/games/${game.id}`) }
    );
  }

  const publisherNames = game.publishers.map(p => p.name).join(', ');
  const designerNames = game.designers.map(d => d.name).join(', ');

  return (
    <div className="flex flex-col gap-6 sm:flex-row sm:gap-8">
      {/* Cover image */}
      <div className="relative flex-shrink-0 self-start">
        <div className="relative h-64 w-48 overflow-hidden rounded-lg shadow-lg sm:h-72 sm:w-56">
          {game.imageUrl ? (
            <Image
              src={game.imageUrl}
              alt={game.title}
              fill
              className="object-cover"
              sizes="(max-width: 640px) 192px, 224px"
              priority
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center bg-muted">
              <span className="text-4xl">🎲</span>
            </div>
          )}
        </div>
      </div>

      {/* Info */}
      <div className="flex flex-1 flex-col gap-4">
        {/* Title + year */}
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
            {game.title}
          </h1>
          {game.yearPublished > 0 && (
            <p className="mt-1 flex items-center gap-1.5 text-sm text-muted-foreground">
              <CalendarDays className="h-3.5 w-3.5" />
              {game.yearPublished}
              {publisherNames && <span className="mx-1">·</span>}
              {publisherNames && <span>{publisherNames}</span>}
            </p>
          )}
          {designerNames && (
            <p className="mt-0.5 text-sm text-muted-foreground">Autore: {designerNames}</p>
          )}
        </div>

        {/* Stats */}
        <div className="flex flex-wrap gap-3">
          <StatBadge icon={<Users className="h-3.5 w-3.5" />}>
            {game.minPlayers === game.maxPlayers
              ? `${game.minPlayers} giocatori`
              : `${game.minPlayers}–${game.maxPlayers} giocatori`}
          </StatBadge>
          {game.playingTimeMinutes > 0 && (
            <StatBadge icon={<Clock className="h-3.5 w-3.5" />}>
              {game.playingTimeMinutes} min
            </StatBadge>
          )}
          {game.averageRating != null && (
            <StatBadge icon={<Star className="h-3.5 w-3.5 fill-yellow-400 text-yellow-400" />}>
              {game.averageRating.toFixed(1)} / 10
            </StatBadge>
          )}
          {game.complexityRating != null && (
            <StatBadge icon={<Brain className="h-3.5 w-3.5" />}>
              Complessità {game.complexityRating.toFixed(1)} / 5
            </StatBadge>
          )}
        </div>

        {/* Categories */}
        {game.categories.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {game.categories.map(cat => (
              <Badge key={cat.id} variant="secondary" className="text-xs">
                {cat.name}
              </Badge>
            ))}
          </div>
        )}

        {/* Description */}
        {game.description && (
          <p className="line-clamp-4 text-sm leading-relaxed text-muted-foreground">
            {game.description}
          </p>
        )}

        {/* CTA */}
        <div className="mt-auto flex flex-wrap items-center gap-3 pt-2">
          <Button
            onClick={handleAddToLibrary}
            disabled={addToLibrary.isPending}
            size="lg"
            className="gap-2"
          >
            <BookPlus className="h-4 w-4" />
            {addToLibrary.isPending ? 'Aggiunta in corso…' : 'Aggiungi alla Libreria'}
          </Button>
          {game.bggId && (
            <a
              href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
            >
              <ExternalLink className="h-3.5 w-3.5" />
              BoardGameGeek
            </a>
          )}
        </div>
      </div>
    </div>
  );
}

function StatBadge({ icon, children }: { icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="inline-flex items-center gap-1.5 rounded-md border bg-muted/40 px-2.5 py-1 text-sm text-foreground">
      {icon}
      {children}
    </div>
  );
}
