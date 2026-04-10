/**
 * diary-utils — Shared event metadata and payload parsers for diary timelines
 *
 * Used by both SessionDiaryTimeline and GameNightDiaryPanel to avoid
 * duplicating the EVENT_META mapping and parseSummary logic.
 *
 * Session Flow v2.1
 */

import {
  BookOpen,
  Bot,
  Dice5,
  Flag,
  Gamepad2,
  MessageSquare,
  Pause,
  Play,
  RefreshCw,
  Shuffle,
  Trophy,
  UserMinus,
  Users,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

// ─── Types ────────────────────────────────────────────────────────────────

export interface EventMeta {
  icon: LucideIcon;
  color: string;
  label: string;
}

// ─── Event type → icon/color/label map (aligned with backend event types) ─

export const EVENT_META: Record<string, EventMeta> = {
  // Session lifecycle
  session_created: { icon: Play, color: 'text-emerald-500', label: 'Sessione avviata' },
  session_paused: { icon: Pause, color: 'text-amber-500', label: 'Sessione in pausa' },
  session_resumed: { icon: Play, color: 'text-emerald-500', label: 'Sessione ripresa' },
  session_finalized: { icon: Flag, color: 'text-slate-500', label: 'Sessione finalizzata' },

  // Participants
  participant_added: { icon: Users, color: 'text-purple-500', label: 'Partecipante aggiunto' },
  participant_removed: { icon: UserMinus, color: 'text-rose-500', label: 'Partecipante rimosso' },

  // Turn management
  turn_order_set: { icon: Shuffle, color: 'text-indigo-500', label: 'Ordine turni impostato' },
  turn_advanced: { icon: RefreshCw, color: 'text-blue-500', label: 'Turno avanzato' },

  // Gameplay
  dice_rolled: { icon: Dice5, color: 'text-orange-500', label: 'Lancio dadi' },
  score_updated: { icon: Trophy, color: 'text-green-500', label: 'Punteggio aggiornato' },
  note_added: { icon: BookOpen, color: 'text-cyan-500', label: 'Nota aggiunta' },

  // Agent
  agent_query: { icon: MessageSquare, color: 'text-violet-500', label: 'Domanda agente' },
  agent_downgrade: { icon: Bot, color: 'text-amber-600', label: 'Fallback agente' },

  // Game night
  gamenight_created: { icon: Gamepad2, color: 'text-primary', label: 'Serata creata' },
  gamenight_game_added: { icon: Gamepad2, color: 'text-emerald-600', label: 'Gioco aggiunto' },
  gamenight_completed: { icon: Flag, color: 'text-primary', label: 'Serata completata' },
};

export const FALLBACK_META: EventMeta = {
  icon: BookOpen,
  color: 'text-muted-foreground',
  label: 'Evento',
};

/** Resolve event metadata from event type string. */
export function getEventMeta(eventType: string): EventMeta {
  return EVENT_META[eventType] ?? FALLBACK_META;
}

// ─── Payload parser ────────────────────────────────────────────────────────

/** Extract a human-readable summary from diary entry payload JSON. */
export function parseSummary(eventType: string, payload: string | null): string | null {
  if (!payload) return null;

  try {
    const data = JSON.parse(payload);

    switch (eventType) {
      case 'dice_rolled':
        return data.formula ? `${data.formula} → ${data.total ?? ''}` : null;
      case 'score_updated':
        return data.newValue !== undefined ? `Nuovo punteggio: ${data.newValue}` : null;
      case 'turn_advanced':
        return data.toParticipantId ? 'Prossimo giocatore' : null;
      default:
        return null;
    }
  } catch {
    return null;
  }
}
