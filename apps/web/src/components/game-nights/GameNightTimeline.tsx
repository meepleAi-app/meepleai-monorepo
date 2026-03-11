'use client';

import { Clock, Coffee, Gamepad2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { TimelineSlot } from '@/store/game-night';

const SLOT_CONFIG = {
  game: { icon: Gamepad2, bg: 'bg-primary/10 border-primary/20', label: 'Gioco' },
  break: { icon: Coffee, bg: 'bg-yellow-500/10 border-yellow-500/20', label: 'Pausa' },
  free: { icon: Clock, bg: 'bg-muted border-border', label: 'Libero' },
} as const;

interface GameNightTimelineProps {
  slots: TimelineSlot[];
  gameNames?: Record<string, string>;
}

export function GameNightTimeline({ slots, gameNames = {} }: GameNightTimelineProps) {
  const total = slots.reduce((sum, s) => sum + s.durationMinutes, 0);

  return (
    <div
      data-testid="timeline"
      className="flex gap-1 h-10 rounded-lg overflow-hidden border border-border"
    >
      {slots.map(slot => {
        const config = SLOT_CONFIG[slot.type];
        const Icon = config.icon;
        const width = total > 0 ? (slot.durationMinutes / total) * 100 : 0;
        const label =
          slot.type === 'game' && slot.gameId
            ? (gameNames[slot.gameId] ?? config.label)
            : config.label;

        return (
          <div
            key={slot.id}
            data-testid="timeline-slot"
            style={{ width: `${width}%` }}
            className={cn('flex items-center gap-1 px-2 border', config.bg, 'min-w-0')}
          >
            <Icon className="h-3.5 w-3.5 shrink-0" />
            <span className="text-xs font-medium truncate">{label}</span>
            <span className="text-[10px] text-muted-foreground ml-auto shrink-0">
              {slot.durationMinutes}min
            </span>
          </div>
        );
      })}
    </div>
  );
}
