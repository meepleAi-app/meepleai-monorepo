/**
 * mapDiary — #2633 Slice C2.
 *
 * PURE projection from the night diary read (`GET /game-nights/{id}/diary` →
 * `GameNightDiaryDto`) onto the `NightLiveHub`'s three diary arrays. Composed with the
 * live view model in `NightLiveClientView` (panel D2): the live read supplies the
 * `sessionId → gameId/title` lookup this mapper joins against.
 *
 * Determinism (mirrors mapNightLive's purity contract): no `Date.now()`, no
 * `Math.random()`, no module-level mutable state. Timestamps are read as UTC (the BE
 * serializes a bare `DateTime` with no offset — parsing it as browser-local would skew
 * ordering, panel timezone must-fix), and the displayed label is UTC `HH:MM`.
 */

import type {
  DiaryEvent,
  DiaryEventKind,
  DiaryGameRef,
} from '@/components/features/game-nights/live';
import type { GameNightDiaryDto } from '@/lib/api/schemas/game-nights.schemas';
import type { NightSessionRef } from '@/lib/game-nights/mapNightLive';
import { hashToHue } from '@/lib/games/cover-utils';

export interface NightDiaryViewModel {
  readonly diaryEvents: readonly DiaryEvent[];
  readonly diaryGames: readonly DiaryGameRef[];
  readonly diaryPlayers: readonly import('@/components/features/game-nights/live').DiaryPlayerRef[];
}

/**
 * Panel D6: keyed off the WRITE-SIDE emitter vocabulary (score_updated, turn_advanced,
 * dice_rolled, session_paused…), with the legacy display-switch aliases (score_update,
 * dice_roll, pause_resume) mapped too. Any unlisted type falls through to `system` in
 * {@link toKind} — never dropped (D8).
 */
const KIND_BY_EVENT_TYPE: Readonly<Record<string, DiaryEventKind>> = {
  // score
  score_updated: 'score',
  score_update: 'score',
  score: 'score',
  // turn
  turn_advanced: 'turn',
  turn: 'turn',
  // lifecycle milestones → end (KIND_TO_ENTITY: end → event)
  game_started: 'end',
  game_completed: 'end',
  night_started: 'end',
  night_finalized: 'end',
  // discrete player actions → custom
  dice_rolled: 'custom',
  dice_roll: 'custom',
  card_draw: 'custom',
  card_drawn: 'custom',
  photo: 'custom',
  note_added: 'custom',
  resource_update: 'custom',
  // ambient / meta → system
  session_paused: 'system',
  session_resumed: 'system',
  pause_resume: 'system',
  player_joined: 'system',
  dispute_resolved: 'system',
};

/** D8: unknown/new event types render as `system` rather than being silently skipped. */
export function toKind(eventType: string): DiaryEventKind {
  return KIND_BY_EVENT_TYPE[eventType] ?? 'system';
}

// D7: the row icon comes STRICTLY from the FE kind map — never from Description[0]
// (multi-codepoint emoji → surrogate-pair mojibake). The server Description keeps its
// own inline emoji in `text`; the icon is the categorical kind glyph.
const ICON_BY_KIND: Readonly<Record<DiaryEventKind, string>> = {
  turn: '🔄',
  score: '📊',
  custom: '📝',
  end: '🏁',
  system: '⚙️',
};

// Deterministic placeholder emoji per game (no BGG asset, no emoji field on the wire).
const GAME_EMOJI_PALETTE = ['🎲', '🃏', '🎯', '🧩', '♟️', '🎴', '🀄', '🎰'] as const;
function gameEmoji(gameId: string): string {
  return GAME_EMOJI_PALETTE[hashToHue(gameId) % GAME_EMOJI_PALETTE.length];
}

const OFFSET_RE = /([zZ]|[+-]\d{2}:?\d{2})$/;

/** UTC `HH:MM`. The bare BE timestamp is pinned to UTC (append `Z`) so ordering + label
 * are stable regardless of the browser timezone. */
export function toTimeLabel(timestamp: string): string {
  const iso = OFFSET_RE.test(timestamp) ? timestamp : `${timestamp}Z`;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const hh = String(d.getUTCHours()).padStart(2, '0');
  const mm = String(d.getUTCMinutes()).padStart(2, '0');
  return `${hh}:${mm}`;
}

/**
 * Projects the diary entries into the hub's arrays. `sessions` is the live read's
 * `sessionId → {gameId, gameTitle}` lookup (D4: the join stays FE-side, zero BE change).
 * An entry whose SessionId is not in the live sessions (night-level or not-yet-cached)
 * maps to `gameId: null` — rendered as an ungrouped event, never a crash (D4/AC3).
 */
export function mapDiary(
  dto: GameNightDiaryDto,
  sessions: readonly NightSessionRef[]
): NightDiaryViewModel {
  const gameIdBySession = new Map(sessions.map(s => [s.sessionId, s.gameId]));
  const titleByGame = new Map(sessions.map(s => [s.gameId, s.gameTitle]));

  const diaryEvents: DiaryEvent[] = dto.entries.map(e => {
    const kind = toKind(e.eventType);
    return {
      id: e.id,
      time: toTimeLabel(e.timestamp),
      gameId: gameIdBySession.get(e.sessionId) ?? null,
      kind,
      icon: ICON_BY_KIND[kind],
      actors: [], // D5 minimal: no actor avatars (guest-capable roster is #2634)
      text: e.description,
    };
  });

  // One DiaryGameRef per distinct game that actually has events, titles from the live lookup.
  const seen = new Set<string>();
  const diaryGames: DiaryGameRef[] = [];
  for (const event of diaryEvents) {
    if (event.gameId == null || seen.has(event.gameId)) continue;
    seen.add(event.gameId);
    diaryGames.push({
      id: event.gameId,
      title: titleByGame.get(event.gameId) ?? 'Gioco',
      emoji: gameEmoji(event.gameId),
    });
  }

  return { diaryEvents, diaryGames, diaryPlayers: [] };
}
