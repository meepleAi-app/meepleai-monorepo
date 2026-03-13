'use client';

import { Calendar, MapPin, Users, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';
import type { GameNightSummary, GameNightStatus } from '@/store/game-night';

const STATUS_CONFIG: Record<GameNightStatus, { label: string; className: string }> = {
  upcoming: { label: 'Prossima', className: 'bg-green-500/10 text-green-500 border-green-500/20' },
  draft: { label: 'Bozza', className: 'bg-muted text-muted-foreground border-border' },
  completed: {
    label: 'Completata',
    className: 'bg-muted/50 text-muted-foreground border-border/50',
  },
};

const DEFAULT_STATUS = {
  label: 'Sconosciuto',
  className: 'bg-muted text-muted-foreground border-border',
};

interface GameNightCardProps {
  night: GameNightSummary;
}

export function GameNightCard({ night }: GameNightCardProps) {
  const status = STATUS_CONFIG[night.status] ?? DEFAULT_STATUS;
  const dateStr = new Date(night.date).toLocaleDateString('it-IT', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <Link
      href={`/game-nights/${night.id}`}
      className={cn(
        'block rounded-xl border border-border bg-card p-4',
        'hover:border-primary/30 hover:shadow-md transition-all duration-200',
        night.status === 'completed' && 'opacity-60'
      )}
    >
      <div className="flex items-start justify-between mb-3">
        <h3 className="font-semibold font-quicksand text-foreground truncate">{night.title}</h3>
        <span
          className={cn(
            'text-xs px-2 py-0.5 rounded-full border font-medium shrink-0 ml-2',
            status.className
          )}
        >
          {status.label}
        </span>
      </div>

      <div className="space-y-1.5 text-sm text-muted-foreground">
        <div className="flex items-center gap-2">
          <Calendar className="h-3.5 w-3.5 shrink-0" />
          <span>{dateStr}</span>
        </div>
        {night.location && (
          <div className="flex items-center gap-2">
            <MapPin className="h-3.5 w-3.5 shrink-0" />
            <span className="truncate">{night.location}</span>
          </div>
        )}
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-1.5">
            <Users className="h-3.5 w-3.5" />
            <span>{night.playerCount}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <Gamepad2 className="h-3.5 w-3.5" />
            <span>{night.gameCount}</span>
          </div>
        </div>
      </div>

      {night.playerAvatars.length > 0 && (
        <div className="flex -space-x-2 mt-3" data-testid="player-avatars">
          {night.playerAvatars.slice(0, 4).map((url, i) => (
            <img key={i} src={url} alt="" className="h-6 w-6 rounded-full border-2 border-card" />
          ))}
          {night.playerAvatars.length > 4 && (
            <span className="h-6 w-6 rounded-full bg-muted flex items-center justify-center text-[10px] font-medium border-2 border-card">
              +{night.playerAvatars.length - 4}
            </span>
          )}
        </div>
      )}

      {night.gameThumbnails.length > 0 && (
        <div className="flex gap-1.5 mt-2" data-testid="game-thumbnails">
          {night.gameThumbnails.slice(0, 3).map((url, i) => (
            <img key={i} src={url} alt="" className="h-8 w-8 rounded-md object-cover" />
          ))}
        </div>
      )}
    </Link>
  );
}
