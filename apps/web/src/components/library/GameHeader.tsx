import { Star, Users, Clock } from 'lucide-react';
import Image from 'next/image';

import { Badge } from '@/components/ui/data-display/badge';

interface GameHeaderProps {
  title: string;
  publisher?: string | null;
  year?: number | null;
  rating?: number | null;
  coverUrl?: string | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  playingTimeMinutes?: number | null;
}

export function GameHeader({
  title,
  publisher,
  year,
  rating,
  coverUrl,
  minPlayers,
  maxPlayers,
  playingTimeMinutes,
}: GameHeaderProps) {
  const playerLabel =
    minPlayers != null && maxPlayers != null
      ? minPlayers === maxPlayers
        ? `${minPlayers} giocatori`
        : `${minPlayers}–${maxPlayers} giocatori`
      : null;

  const durationLabel = playingTimeMinutes != null ? `${playingTimeMinutes} min` : null;

  return (
    <div className="flex items-start gap-5">
      {/* Cover */}
      <div className="w-[120px] h-[120px] rounded-xl overflow-hidden border border-border shrink-0 bg-secondary">
        {coverUrl ? (
          <Image
            src={coverUrl}
            alt={title}
            width={120}
            height={120}
            className="w-full h-full object-cover"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-4xl">🎲</div>
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0 space-y-2">
        <h1 className="font-bold text-2xl text-foreground leading-tight">{title}</h1>

        {(publisher || year) && (
          <p className="text-sm text-muted-foreground">
            {[publisher, year ? `(${year})` : null].filter(Boolean).join(' ')}
          </p>
        )}

        <div className="flex items-center flex-wrap gap-2">
          {rating != null && (
            <Badge
              variant="secondary"
              className="gap-1 bg-[hsl(var(--e-game))]/15 text-[hsl(var(--e-game))] border-[hsl(var(--e-game))]/30"
            >
              <Star className="w-3 h-3 fill-current" />
              {rating.toFixed(1)}
            </Badge>
          )}
          {playerLabel && (
            <Badge variant="secondary" className="gap-1">
              <Users className="w-3 h-3" />
              {playerLabel}
            </Badge>
          )}
          {durationLabel && (
            <Badge variant="secondary" className="gap-1">
              <Clock className="w-3 h-3" />
              {durationLabel}
            </Badge>
          )}
        </div>
      </div>
    </div>
  );
}
