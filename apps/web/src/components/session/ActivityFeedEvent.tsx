'use client';

import { Dice5, Bot, Trophy, Camera, FileText, Mic, RefreshCw, Pause, Play } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { ActivityEvent, ActivityEventType } from '@/store/session';

import type { LucideIcon } from 'lucide-react';

const EVENT_CONFIG: Record<ActivityEventType, { icon: LucideIcon; color: string; label: string }> =
  {
    dice_roll: { icon: Dice5, color: 'text-orange-500', label: 'Lancio dadi' },
    ai_tip: { icon: Bot, color: 'text-purple-500', label: 'Suggerimento AI' },
    score_update: { icon: Trophy, color: 'text-green-500', label: 'Punteggio' },
    photo: { icon: Camera, color: 'text-blue-500', label: 'Foto' },
    note: { icon: FileText, color: 'text-yellow-500', label: 'Nota' },
    audio_note: { icon: Mic, color: 'text-pink-500', label: 'Audio' },
    turn_change: { icon: RefreshCw, color: 'text-gray-500', label: 'Turno' },
    pause_resume: { icon: Pause, color: 'text-yellow-500', label: 'Pausa' },
    session_start: { icon: Play, color: 'text-green-500', label: 'Inizio' },
  };

interface ActivityFeedEventProps {
  event: ActivityEvent;
}

export function ActivityFeedEvent({ event }: ActivityFeedEventProps) {
  const config = EVENT_CONFIG[event.type];
  const Icon = config.icon;
  const data = event.data;
  const time = new Date(event.timestamp).toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div data-testid="activity-event" className="flex gap-3 py-2 px-3">
      <div className={cn('mt-0.5 shrink-0', config.color)}>
        <Icon className="h-4 w-4" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          {data.playerName !== undefined && data.playerName !== null && (
            <span className="font-medium text-sm text-foreground">{String(data.playerName)}</span>
          )}
          <span className="text-xs text-muted-foreground">{time}</span>
        </div>
        <div className="text-sm text-muted-foreground mt-0.5">
          {event.type === 'dice_roll' && (
            <span>
              Lancio: {String(data.total)} (
              {Array.isArray(data.values) ? (data.values as number[]).join('+') : ''})
            </span>
          )}
          {event.type === 'score_update' && (
            <span>
              {String(data.action)} → {String(data.newScore)} punti
            </span>
          )}
          {event.type === 'note' && <span>{String(data.text)}</span>}
          {event.type === 'ai_tip' && <span className="italic">{String(data.text)}</span>}
          {event.type === 'turn_change' && (
            <span>
              Turno {String(data.from)} → {String(data.to)}
            </span>
          )}
          {event.type === 'pause_resume' && (
            <span>{data.paused ? 'Sessione in pausa' : 'Sessione ripresa'}</span>
          )}
          {event.type === 'session_start' && <span>Sessione iniziata</span>}
        </div>
      </div>
    </div>
  );
}
