'use client';

/**
 * DiaryEventRow — Single diary event row with type-specific icon and formatting.
 */

import React from 'react';

import type { DiaryEvent, DiaryEventType } from '../types';

const EVENT_ICON: Record<DiaryEventType, string> = {
  dice_roll: '🎲',
  score_change: '🏆',
  turn_change: '🔄',
  note_added: '📝',
  manual_entry: '✍️',
  player_joined: '👤',
  round_advance: '🔔',
  score_reset: '♻️',
  timer_end: '⏱️',
};

function formatTime(timestamp: number): string {
  const d = new Date(timestamp);
  const h = d.getHours().toString().padStart(2, '0');
  const m = d.getMinutes().toString().padStart(2, '0');
  return `${h}:${m}`;
}

function describeEvent(event: DiaryEvent): string {
  const { type, payload, playerName } = event;
  const who = playerName ? `${playerName} ` : '';

  switch (type) {
    case 'dice_roll': {
      const formula = payload.formula as string | undefined;
      const total = payload.total as number | undefined;
      const rolls = payload.rolls as Array<{ value: number }> | undefined;
      const results = rolls?.map(r => r.value).join(',');
      return `${who}lancia ${formula ?? '?'} → [${results ?? '?'}] = ${total ?? '?'}`;
    }
    case 'score_change': {
      const category = payload.category as string | undefined;
      const delta = payload.delta as number | undefined;
      const newTotal = payload.newTotal as number | undefined;
      const deltaStr = typeof delta === 'number' ? (delta >= 0 ? `+${delta}` : `${delta}`) : '';
      return `${who}${deltaStr} ${category ?? ''} (totale: ${newTotal ?? '?'})`;
    }
    case 'turn_change': {
      const nextPlayer = payload.nextPlayerName as string | undefined;
      return `Turno → ${nextPlayer ?? playerName ?? '?'}`;
    }
    case 'note_added': {
      const preview = payload.preview as string | undefined;
      return `${who}aggiunge nota${preview ? `: "${preview}"` : ''}`;
    }
    case 'manual_entry': {
      const text = payload.text as string | undefined;
      return text ? `${who}${text}` : who;
    }
    case 'player_joined': {
      return `${who}si unisce alla partita`;
    }
    case 'round_advance': {
      const round = payload.round as number | undefined;
      return `Inizio Round ${round ?? '?'}`;
    }
    case 'score_reset': {
      return 'Punteggi azzerati';
    }
    case 'timer_end': {
      const dur = payload.durationSeconds as number | undefined;
      if (!dur) return 'Timer scaduto';
      const m = Math.floor(dur / 60);
      const s = dur % 60;
      const label = m > 0 ? `${m}m${s > 0 ? ` ${s}s` : ''}` : `${s}s`;
      return `Timer scaduto (${label})`;
    }
    default:
      return JSON.stringify(payload);
  }
}

export interface DiaryEventRowProps {
  event: DiaryEvent;
}

export function DiaryEventRow({ event }: DiaryEventRowProps) {
  const icon = EVENT_ICON[event.type] ?? '•';
  const time = formatTime(event.timestamp);
  const desc = describeEvent(event);

  return (
    <div
      className="flex items-start gap-2 py-1.5 text-xs text-gray-600"
      data-testid={`diary-event-${event.id}`}
    >
      <span className="shrink-0 font-mono text-[10px] text-gray-400">{time}</span>
      <span className="shrink-0">{icon}</span>
      <span className="flex-1 leading-snug">{desc}</span>
    </div>
  );
}
