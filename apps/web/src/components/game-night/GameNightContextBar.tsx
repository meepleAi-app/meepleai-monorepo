'use client';

import { Calendar, Users, Dice5, Pencil } from 'lucide-react';

interface GameNightContextBarProps {
  eventName?: string;
  eventDate?: string;
  confirmedCount?: number;
  totalInvited?: number;
  gamesCount?: number;
}

export function GameNightContextBar({
  eventName = 'Serata',
  eventDate,
  confirmedCount = 0,
  totalInvited = 0,
  gamesCount = 0,
}: GameNightContextBarProps) {
  return (
    <div className="flex items-center gap-3 w-full overflow-x-auto text-sm">
      <span className="font-medium text-foreground truncate shrink-0">{eventName}</span>
      {eventDate && (
        <span className="flex items-center gap-1 text-muted-foreground shrink-0">
          <Calendar className="w-3.5 h-3.5" />
          {eventDate}
        </span>
      )}
      <span className="flex items-center gap-1 text-muted-foreground shrink-0">
        <Users className="w-3.5 h-3.5" />
        {confirmedCount}/{totalInvited}
      </span>
      <span className="flex items-center gap-1 text-muted-foreground shrink-0">
        <Dice5 className="w-3.5 h-3.5" />
        {gamesCount} giochi
      </span>
      <button
        type="button"
        className="p-1.5 rounded-lg hover:bg-muted transition-colors ml-auto shrink-0"
        aria-label="Modifica evento"
      >
        <Pencil className="w-4 h-4 text-muted-foreground" />
      </button>
    </div>
  );
}
