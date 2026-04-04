'use client';

/**
 * ActivityFeed — Scrollable feed of game session events (scores, turns, pauses, resumes).
 *
 * Renders in the desktop center panel of LiveSessionView.
 * Italian UI strings.
 *
 * Issue #341
 */

import { formatDistanceToNow } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
import { Trophy, Play, Pause, RotateCcw } from 'lucide-react';

import { ScrollArea } from '@/components/ui/primitives/scroll-area';

export interface ActivityEvent {
  id: string;
  type: 'score' | 'turn_advance' | 'pause' | 'resume';
  playerName?: string;
  value?: number;
  dimension?: string;
  timestamp: string;
}

interface ActivityFeedProps {
  events: ActivityEvent[];
}

const ICONS = {
  score: Trophy,
  turn_advance: Play,
  pause: Pause,
  resume: RotateCcw,
} as const;

function formatEvent(event: ActivityEvent): string {
  switch (event.type) {
    case 'score':
      return `${event.playerName} ha segnato ${event.value} punti${event.dimension && event.dimension !== 'default' ? ` (${event.dimension})` : ''}`;
    case 'turn_advance':
      return 'Turno avanzato';
    case 'pause':
      return 'Partita in pausa';
    case 'resume':
      return 'Partita ripresa';
    default:
      return '';
  }
}

export function ActivityFeed({ events }: ActivityFeedProps) {
  if (events.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-sm text-muted-foreground">
        Nessuna attività ancora
      </div>
    );
  }

  return (
    <ScrollArea className="h-full">
      <div className="space-y-2 p-4">
        {events.map(event => {
          const Icon = ICONS[event.type] ?? Play;
          return (
            <div key={event.id} className="flex items-start gap-3 text-sm">
              <div className="mt-0.5 flex h-6 w-6 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
                <Icon className="h-3 w-3 text-amber-600" />
              </div>
              <div className="flex-1">
                <p>{formatEvent(event)}</p>
                <p className="text-xs text-muted-foreground">
                  {formatDistanceToNow(new Date(event.timestamp), {
                    addSuffix: true,
                    locale: itLocale,
                  })}
                </p>
              </div>
            </div>
          );
        })}
      </div>
    </ScrollArea>
  );
}
